using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace LoongRacing.Editor
{
    // Explicit editor QA run. No automation or input driver is shipped with the car.
    public static class NeonPerformanceValidation
    {
        [Serializable] public sealed class Summary
        {
            public int count;
            public double mean,median,p95;
            public static Summary From(List<double> values)
            {
                var s=new Summary{count=values.Count};if(values.Count==0)return s;
                values.Sort();foreach(double v in values)s.mean+=v;s.mean/=values.Count;
                s.median=values[values.Count/2];s.p95=values[Mathf.Clamp(Mathf.CeilToInt(values.Count*.95f)-1,0,values.Count-1)];return s;
            }
        }
        [Serializable] public sealed class Report
        {
            public string context="Unity Editor Game View, uncapped, one physical lap, first 2s excluded";
            public string gpu,state;
            public int width,height,recoveries;
            public float lapSeconds;
            public bool gpuTimingsAvailable;
            public Summary frameMs,sampleSectionFrameMs,mainThreadMs,gpuMs,drawCalls;
        }
        private static ArcadeKartController kart;
        private static KartRouteProgress progress;
        private static KartRaceSession race;
        private static PracticeHud hud;
        private static TrackRoute original,temporary;
        private static ProfilerRecorder main,draws;
        private static readonly List<double> frames=new List<double>(),sample=new List<double>(),cpu=new List<double>(),gpu=new List<double>(),calls=new List<double>();
        private static readonly FrameTiming[] timings=new FrameTiming[1];
        private static int lastFrame,previousCap,previousVsync;
        private static double deadline;
        public static bool Running {get;private set;}

        public static void Start()
        {
            if(!EditorApplication.isPlaying || Running)throw new InvalidOperationException("Enter Play in Neon Harbor first.");
            kart=UnityEngine.Object.FindFirstObjectByType<ArcadeKartController>();progress=kart.GetComponent<KartRouteProgress>();race=kart.GetComponent<KartRaceSession>();hud=UnityEngine.Object.FindFirstObjectByType<PracticeHud>();
            if(hud.IsPaused)hud.TogglePause();kart.GetComponent<KartKeyboardInput>().enabled=false;
            original=progress.Route;temporary=UnityEngine.Object.Instantiate(original);temporary.name="NeonPerformance-"+Guid.NewGuid().ToString("N");progress.Configure(temporary);
            race.StartChallenge(1);frames.Clear();sample.Clear();cpu.Clear();gpu.Clear();calls.Clear();lastFrame=-1;
            previousCap=Application.targetFrameRate;previousVsync=QualitySettings.vSyncCount;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
            Time.timeScale=1;AudioListener.pause=true;
            main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);
            deadline=EditorApplication.timeSinceStartup+80;Running=true;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnModeChange;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
        private static void OnModeChange(PlayModeStateChange state)
        {if(Running && state==PlayModeStateChange.ExitingPlayMode)Finish();}

        private static void Tick()
        {
            if(hud.IsPaused)hud.TogglePause();AudioListener.pause=true;
            if(race.State==KartRaceState.Finished || EditorApplication.timeSinceStartup>deadline){Finish();return;}
            if(race.State!=KartRaceState.Racing)return;
            progress.Route.FindNearest(kart.Body.position,out Vector3 p,out Vector3 f,out float along);
            float speed=kart.SpeedKph/3.6f;progress.Route.Sample(along+9f+speed*.35f,out Vector3 target,out Vector3 tf);
            Vector3 delta=Vector3.ProjectOnPlane(target-kart.Body.position,Vector3.up);
            float yaw=2*Mathf.Max(8,speed)*Mathf.Sin(Vector3.SignedAngle(kart.transform.forward,delta,Vector3.up)*Mathf.Deg2Rad)/Mathf.Max(1,delta.magnitude);
            float rate=Mathf.Lerp(kart.Tuning.lowSpeedSteering,kart.Tuning.highSpeedSteering,Mathf.InverseLerp(12,kart.Tuning.maximumForwardSpeed,speed))*Mathf.Deg2Rad;
            kart.SetDriveInput(speed>29?-.3f:1,Mathf.Clamp(yaw/rate,-1,1));
            FrameTimingManager.CaptureFrameTimings();
            if(Time.frameCount==lastFrame || race.ElapsedSeconds<2)return;lastFrame=Time.frameCount;
            double ms=Time.unscaledDeltaTime*1000;if(ms>0){frames.Add(ms);if(along>=125 && along<=245)sample.Add(ms);}
            if(main.Valid && main.LastValue>0)cpu.Add(main.LastValue/1000000.0);
            if(draws.Valid)calls.Add(draws.LastValue);
            if(FrameTimingManager.GetLatestTimings(1,timings)>0 && timings[0].gpuFrameTime>0)gpu.Add(timings[0].gpuFrameTime);
        }
        private static void Finish()
        {
            if(!Running)return;Running=false;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnModeChange;
            var report=new Report{gpu=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,state=race.State.ToString(),lapSeconds=race.ElapsedSeconds,
                recoveries=progress.RecoveryCount,gpuTimingsAvailable=gpu.Count>0,frameMs=Summary.From(frames),sampleSectionFrameMs=Summary.From(sample),
                mainThreadMs=Summary.From(cpu),gpuMs=Summary.From(gpu),drawCalls=Summary.From(calls)};
            string json=JsonUtility.ToJson(report,true);File.WriteAllText(Application.dataPath+"/../Captures/NeonHarbor/performance.json",json);SessionState.SetString("NeonBench.Report",json);
            main.Dispose();draws.Dispose();Application.targetFrameRate=previousCap;QualitySettings.vSyncCount=previousVsync;
            string key=race.RecordPrefix;PlayerPrefs.DeleteKey(key+".lap");PlayerPrefs.DeleteKey(key+".total.1");PlayerPrefs.Save();
            // Keep the finish screen for visual review; restore the original route without resetting results.
            var so=new SerializedObject(progress);so.FindProperty("route").objectReferenceValue=original;so.ApplyModifiedPropertiesWithoutUndo();UnityEngine.Object.Destroy(temporary);
            Time.timeScale=0;Debug.Log("[Neon Performance] "+json);
        }
    }
}
