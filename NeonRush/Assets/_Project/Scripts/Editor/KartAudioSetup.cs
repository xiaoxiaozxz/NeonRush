using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoongRacing.Editor
{
    public static class KartAudioSetup
    {
        public const string BankPath = "Assets/_Project/Data/KartAudioBank.asset";
        [MenuItem("NeonRush/Install Racing Audio")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before installing audio.");
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            var bank = AssetDatabase.LoadAssetAtPath<KartAudioBank>(BankPath);
            if (bank == null) { bank = ScriptableObject.CreateInstance<KartAudioBank>(); AssetDatabase.CreateAsset(bank, BankPath); }
            bank.idleLoop = Clip("engine_idle_loop"); bank.driveLoop = Clip("engine_drive_loop");
            bank.tyreLoop = Clip("tyre_skid_loop"); bank.windLoop = Clip("wind_loop");
            bank.collisions = new[] { Clip("collision_01"), Clip("collision_02"), Clip("collision_03") };
            bank.reset = Clip("reset"); bank.pause = Clip("ui_pause"); bank.resume = Clip("ui_resume");
            bank.gasReady = Clip("gas_ready"); bank.nitro = Clip("nitro_burst");
            bank.miniBoost = Clip("mini_boost"); bank.airBoost = Clip("air_boost"); bank.landing = Clip("landing");
            bank.countdown = Clip("countdown_tick"); bank.raceStart = Clip("race_start");
            EditorUtility.SetDirty(bank);
            const string prefabPath = "Assets/_Project/Prefabs/BlockKart.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var audio = prefab.GetComponent<KartAudioController>() ?? prefab.AddComponent<KartAudioController>();
                audio.Configure(bank);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (var kart in UnityEngine.Object.FindObjectsByType<ArcadeKartController>(FindObjectsSortMode.None))
            {
                var audio = kart.GetComponent<KartAudioController>() ?? kart.gameObject.AddComponent<KartAudioController>();
                audio.Configure(bank);
                PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
                EditorUtility.SetDirty(audio);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[NeonRush] 17 original racing SFX installed in the kart prefab and training scene.");
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/" + name + ".wav");
            if (clip == null) throw new InvalidOperationException("Missing audio clip: " + name);
            return clip;
        }
    }
}
