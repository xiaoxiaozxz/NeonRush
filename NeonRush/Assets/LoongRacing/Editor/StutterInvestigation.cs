using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LoongRacing.Editor
{
    // Explicit, temporary measurement run. Not attached to gameplay or startup.
    public static class StutterInvestigation
    {
        [Serializable] public struct Frame
        {
            public float elapsed, frameMs, mainMs, hudMs, gcMs, along;
            public long allocatedBytes, hudAllocatedBytes;
        }
        [Serializable] public class SlowSample { public string name; public float ms; public int children; }
        [Serializable] public class Spike { public float elapsed, ms; public List<SlowSample> samples; }
        [Serializable] public class Report
        {
            public string label, context = "Unity Editor, actual driving, no drift/collision input; first 3 seconds excluded";
            public int width, height, frames, gen0Collections, gen1Collections, gen2Collections, recoveries;
            public double meanMs, p95Ms, p99Ms, maxMs, hudMeanMs, allocatedBytesPerFrame, hudAllocatedBytesPerRepaint;
            public int over33Ms, over50Ms, over100Ms;
            public List<Spike> spikes;
            public Frame[] trace;
        }
        public static bool Running { get; private set; }
        private static ArcadeKartController kart;
        private static KartRouteProgress progress;
        private static PracticeHud hud;
        private static KartKeyboardInput input;
        private static ProfilerRecorder main, alloc, gc, hudTime;
        private static readonly List<Frame> frames = new List<Frame>(10000);
        private static readonly List<Spike> spikes = new List<Spike>();
        private static double started;
        private static float duration;
        private static string label;
        private static int lastFrame, lastProfileFrame, gen0, gen1, gen2;
        private static bool oldProfiler, oldProfileEditor, oldDriver, oldCpu, oldHud, oldInput, oldBackground;
        private static float targetSpeed;

        public static void Start(string name, float seconds = 35f, bool showHud = true, float speedMetresPerSecond = 29f, bool captureCpuTrace = false)
        {
            if (!EditorApplication.isPlaying || Running) throw new InvalidOperationException("Enter Play and finish the previous measurement first.");
            kart = UnityEngine.Object.FindFirstObjectByType<ArcadeKartController>();
            progress = kart.GetComponent<KartRouteProgress>();
            hud = UnityEngine.Object.FindFirstObjectByType<PracticeHud>();
            input = kart.GetComponent<KartKeyboardInput>();
            oldHud = hud.enabled; oldInput = input.enabled; oldBackground = Application.runInBackground;
            if (hud.IsPaused) hud.TogglePause();
            kart.GetComponent<KartRaceSession>().StartPractice();
            input.enabled = false; hud.enabled = showHud; Application.runInBackground = true;
            oldProfiler = UnityEngine.Profiling.Profiler.enabled; oldProfileEditor = ProfilerDriver.profileEditor;
            oldDriver = ProfilerDriver.enabled; oldCpu = ProfilerDriver.IsAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU);
            if (captureCpuTrace)
            {
                ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU, true);
                ProfilerDriver.profileEditor = true; ProfilerDriver.enabled = true;
            }
            main = Recorder("Main Thread"); alloc = Recorder("GC.Alloc"); gc = Recorder("GC.Collect"); hudTime = Recorder("NeonRush.HudDraw");
            frames.Clear(); spikes.Clear(); label = name; duration = seconds;
            started = EditorApplication.timeSinceStartup; lastFrame = lastProfileFrame = -1;
            targetSpeed = speedMetresPerSecond;
            gen0 = GC.CollectionCount(0); gen1 = GC.CollectionCount(1); gen2 = GC.CollectionCount(2);
            Running = true; EditorApplication.update += Tick; EditorApplication.playModeStateChanged += OnMode;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        private static ProfilerRecorder Recorder(string name)
        {
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name == name)
                    return ProfilerRecorder.StartNew(description.Category, name, 1,
                        ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
            }
            return default;
        }

        private static void OnMode(PlayModeStateChange state)
        { if (state == PlayModeStateChange.ExitingPlayMode && Running) Finish(); }

        private static void Tick()
        {
            if (!Running) return;
            if (kart == null) { Finish(); return; }
            float elapsed = (float)(EditorApplication.timeSinceStartup - started);
            if (elapsed >= duration) { Finish(); return; }
            if (hud.IsPaused) hud.TogglePause();
            progress.Route.FindNearest(kart.Body.position, out Vector3 nearest, out Vector3 forward, out float along);
            float speed = kart.SpeedKph / 3.6f;
            progress.Route.Sample(along + 9f + speed * .35f, out Vector3 target, out Vector3 tangent);
            Vector3 delta = Vector3.ProjectOnPlane(target - kart.Body.position, Vector3.up);
            float yaw = 2 * Mathf.Max(8, speed) * Mathf.Sin(Vector3.SignedAngle(kart.transform.forward, delta, Vector3.up) * Mathf.Deg2Rad) / Mathf.Max(1, delta.magnitude);
            float rate = Mathf.Lerp(kart.Tuning.lowSpeedSteering, kart.Tuning.highSpeedSteering,
                Mathf.InverseLerp(12, kart.Tuning.maximumForwardSpeed, speed)) * Mathf.Deg2Rad;
            kart.SetDriveInput(speed > targetSpeed ? -.3f : 1, Mathf.Clamp(yaw / rate, -1, 1));
            if (elapsed < 3f || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            frames.Add(new Frame { elapsed = elapsed, frameMs = Time.unscaledDeltaTime * 1000,
                mainMs = main.Valid ? main.LastValue / 1000000f : 0, hudMs = hudTime.Valid ? hudTime.LastValue / 1000000f : 0,
                gcMs = gc.Valid ? gc.LastValue / 1000000f : 0, allocatedBytes = alloc.Valid ? alloc.LastValue : 0,
                // This Unity Mono build returns zero for GetAllocatedBytesForCurrentThread even for test allocations.
                // Use the global GC.Alloc recorder above; do not report zero as a successful HUD allocation measurement.
                hudAllocatedBytes = -1, along = along });
            int profileFrame = ProfilerDriver.lastFrameIndex;
            if (profileFrame == lastProfileFrame || spikes.Count >= 16) return;
            lastProfileFrame = profileFrame;
            using (var data = ProfilerDriver.GetRawFrameDataView(profileFrame, 0))
            {
                if (!data.valid || data.frameTimeMs < 30) return;
                var samples = new List<SlowSample>();
                for (int i = 0; i < data.sampleCount; i++)
                    if (data.GetSampleTimeMs(i) > 3)
                        samples.Add(new SlowSample { name = data.GetSampleName(i), ms = data.GetSampleTimeMs(i), children = data.GetSampleChildrenCount(i) });
                spikes.Add(new Spike { elapsed = elapsed, ms = data.frameTimeMs,
                    samples = samples.OrderByDescending(s => s.ms).Take(32).ToList() });
            }
        }

        private static void Finish()
        {
            if (!Running) return;
            Running = false; EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= OnMode;
            var sorted = frames.Select(f => (double)f.frameMs).OrderBy(v => v).ToArray();
            var report = new Report { label = label, width = Screen.width, height = Screen.height, frames = sorted.Length,
                gen0Collections = GC.CollectionCount(0) - gen0, gen1Collections = GC.CollectionCount(1) - gen1, gen2Collections = GC.CollectionCount(2) - gen2,
                recoveries = progress == null ? -1 : progress.RecoveryCount, spikes = spikes.ToList(), trace = frames.ToArray() };
            if (sorted.Length > 0)
            {
                report.meanMs = sorted.Average(); report.p95Ms = sorted[Mathf.CeilToInt(sorted.Length * .95f) - 1];
                report.p99Ms = sorted[Mathf.CeilToInt(sorted.Length * .99f) - 1]; report.maxMs = sorted[sorted.Length - 1];
                report.over33Ms = sorted.Count(v => v > 33.333); report.over50Ms = sorted.Count(v => v > 50); report.over100Ms = sorted.Count(v => v > 100);
                report.hudMeanMs = frames.Average(f => f.hudMs); report.allocatedBytesPerFrame = frames.Average(f => f.allocatedBytes);
                report.hudAllocatedBytesPerRepaint = frames.Average(f => f.hudAllocatedBytes);
            }
            main.Dispose(); alloc.Dispose(); gc.Dispose(); hudTime.Dispose();
            UnityEngine.Profiling.Profiler.enabled = oldProfiler; ProfilerDriver.profileEditor = oldProfileEditor;
            ProfilerDriver.enabled = oldDriver; ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU, oldCpu);
            if (hud != null) hud.enabled = oldHud;
            if (input != null) input.enabled = oldInput;
            Application.runInBackground = oldBackground;
            if (kart != null) kart.GetComponent<KartRaceSession>().ReturnToMenu();
            string directory = Path.Combine(Application.dataPath, "../Captures/StutterInvestigation"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, label + ".json"), JsonUtility.ToJson(report, true));
            SessionState.SetString("StutterInvestigation.LastReport", label);
            Debug.Log($"[Stutter QA] {label}: mean {report.meanMs:0.00}ms, P99 {report.p99Ms:0.00}ms, max {report.maxMs:0.00}ms; >50ms {report.over50Ms}");
        }
    }
}
