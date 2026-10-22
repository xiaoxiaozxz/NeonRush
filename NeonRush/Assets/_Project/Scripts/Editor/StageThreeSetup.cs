using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoongRacing.Editor
{
    public static class StageThreeSetup
    {
        [MenuItem("NeonRush/Apply Stage 3 Upgrade")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before upgrading.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != TrainingSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open the training scene before upgrading.");
            var tuning = AssetDatabase.LoadAssetAtPath<KartTuning>("Assets/_Project/Data/ArcadeKartTuning.asset");
            tuning.driftTargetAngle = 48f;
            tuning.driftEntrySlip = 2f;
            tuning.driftChargePerSecond = 68f;
            EditorUtility.SetDirty(tuning);
            const string materialPath = "Assets/_Project/Art/Materials/BoostPlume.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.color = new Color(0.03f, 0.78f, 1f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            const string prefabPath = "Assets/_Project/Prefabs/BlockKart.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var effects = prefab.GetComponent<KartBoostEffects>() ?? prefab.AddComponent<KartBoostEffects>();
                effects.Configure(material);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var kart = UnityEngine.Object.FindFirstObjectByType<ArcadeKartController>();
            var sceneEffects = kart.GetComponent<KartBoostEffects>() ?? kart.gameObject.AddComponent<KartBoostEffects>();
            sceneEffects.Configure(material);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sceneEffects);
            EditorUtility.SetDirty(sceneEffects);
            AddPracticeRamp();
            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NeonRush] E drift, Q nitro, W contextual boosts and optional jump lane ready.");
        }

        private static void AddPracticeRamp()
        {
            if (GameObject.Find("Boost Practice Ramp") != null) return;
            const string path = "Assets/_Project/Art/Meshes/BoostPracticeRamp.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Boost Practice Wedge" };
                mesh.vertices = new[] { new Vector3(-2.5f, 0f, -6f), new Vector3(2.5f, 0f, -6f),
                    new Vector3(-2.5f, 1.65f, 6f), new Vector3(2.5f, 1.65f, 6f),
                    new Vector3(-2.5f, 0f, 6f), new Vector3(2.5f, 0f, 6f) };
                mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5, 0, 1, 5, 0, 5, 4, 0, 4, 2, 1, 3, 5 };
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            var ramp = new GameObject("Boost Practice Ramp");
            ramp.transform.SetPositionAndRotation(new Vector3(-31f, 0.02f, 30f), Quaternion.Euler(0f, 180f, 0f));
            ramp.AddComponent<MeshFilter>().sharedMesh = mesh;
            ramp.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Curb_Cyan.mat");
            ramp.AddComponent<MeshCollider>().sharedMesh = mesh;
            var label = new GameObject("AIR BOOST - W");
            label.transform.SetParent(ramp.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.62f, -1.8f);
            label.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            var text = label.AddComponent<TextMesh>();
            text.text = "AIR / W"; text.fontSize = 64; text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter; text.color = Color.white;
        }
    }
}
