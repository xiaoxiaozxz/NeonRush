using System.IO;
using LoongRacing.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoongRacing.Tests
{
    public sealed class ProjectAssetsTests
    {
        [TestCase(TrainingSceneBuilder.ScenePath)]
        [TestCase(HarborCircuitBuilder.ScenePath)]
        [TestCase(NeonHarborSetup.ScenePath)]
        public void MigratedScenesKeepTheirComponentAndAssetReferences(string path)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                ArcadeKartController kart = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),
                            Is.Zero, path + ": " + child.name);
                    if (kart == null) kart = root.GetComponentInChildren<ArcadeKartController>(true);
                }
                Assert.That(kart, Is.Not.Null, path);
                Assert.That(kart.Tuning, Is.Not.Null, path + " tuning");
                var progress = kart.GetComponent<KartRouteProgress>();
                if (progress != null) Assert.That(progress.Route, Is.Not.Null, path + " route");
                AssertReferences(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), path);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void SceneInspectionPreservesTheOpenEditorScenes()
        {
            var before = EditorSceneManager.GetSceneManagerSetup();
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool wasDirty = active.IsValid() && active.isDirty;
            MigratedScenesKeepTheirComponentAndAssetReferences(NeonHarborSetup.ScenePath);
            var after = EditorSceneManager.GetSceneManagerSetup();
            Assert.That(after.Length, Is.EqualTo(before.Length));
            for (int i = 0; i < before.Length; i++)
            {
                Assert.That(after[i].path, Is.EqualTo(before[i].path));
                Assert.That(after[i].isLoaded, Is.EqualTo(before[i].isLoaded));
                Assert.That(after[i].isActive, Is.EqualTo(before[i].isActive));
            }
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle, Is.EqualTo(active.handle));
            if (active.IsValid()) Assert.That(active.isDirty, Is.EqualTo(wasDirty));
        }

        [TestCase("Assets/_Project/Prefabs/BlockKart.prefab")]
        [TestCase("Assets/_Project/Prefabs/VelaRacer.prefab")]
        public void MigratedPrefabsKeepTheirComponentsAndDependencies(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, child.name);
            Assert.That(prefab.GetComponent<ArcadeKartController>().Tuning, Is.Not.Null);
            AssertReferences(prefab, path);
        }

        [Test]
        public void BuildScenesAndAudioBankResolveAtTheirNewPaths()
        {
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(NeonHarborSetup.ScenePath));
            foreach (var scene in EditorBuildSettings.scenes)
                Assert.That(File.Exists(scene.path), Is.True, scene.path);
            var bank = AssetDatabase.LoadAssetAtPath<KartAudioBank>(KartAudioSetup.BankPath);
            Assert.That(bank, Is.Not.Null);
            Assert.That(bank.driveLoop, Is.Not.Null);
            Assert.That(bank.nitro, Is.Not.Null);
        }

        private static void AssertReferences(Object asset, string path)
        {
            Assert.That(asset, Is.Not.Null, path);
            foreach (var dependency in AssetDatabase.GetDependencies(path, true))
            {
                if (dependency.StartsWith("Assets/"))
                    Assert.That(AssetDatabase.LoadMainAssetAtPath(dependency), Is.Not.Null, dependency);
            }
        }
    }
}
