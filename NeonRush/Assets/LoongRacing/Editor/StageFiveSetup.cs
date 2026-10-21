using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoongRacing.Editor
{
    public static class StageFiveSetup
    {
        [MenuItem("NeonRush/Apply Stage 5 Upgrade")]
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != HarborCircuitBuilder.ScenePath) throw new InvalidOperationException("Open Harbor Circuit first.");
            var kart = UnityEngine.Object.FindFirstObjectByType<ArcadeKartController>();
            var progress = kart.GetComponent<KartRouteProgress>();
            progress.ConfigureStart(-6f);
            progress.Route.Sample(-6f, out Vector3 start, out Vector3 forward);
            forward.y = 0f;
            kart.transform.SetPositionAndRotation(start+Vector3.up*0.65f,Quaternion.LookRotation(forward));
            if (kart.GetComponent<KartRaceSession>() == null) kart.gameObject.AddComponent<KartRaceSession>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(kart.transform);
            EditorUtility.SetDirty(progress);
            var camera = UnityEngine.Object.FindFirstObjectByType<KartChaseCamera>();
            if (camera != null) camera.SnapToTarget();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return scene.path;
        }
    }
}
