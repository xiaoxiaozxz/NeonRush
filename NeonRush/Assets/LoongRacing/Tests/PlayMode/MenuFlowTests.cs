using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LoongRacing.Tests
{
    public sealed class MenuFlowTests
    {
        private GameObject root;
        private KartTuning tuning;
        private TrackRoute route;
        private ArcadeKartController kart;
        private KartRaceSession race;
        private PracticeHud hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            root = new GameObject("Menu Flow Test");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(550, -0.5f, 550);
            floor.transform.localScale = new Vector3(250, 1, 250);
            route = ScriptableObject.CreateInstance<TrackRoute>();
            route.name = "MenuQA-" + System.Guid.NewGuid().ToString("N");
            route.Configure("Menu Test", 9.5f, new[] { new Vector3(500, 0, 500), new Vector3(500, 0, 600),
                new Vector3(600, 0, 600), new Vector3(600, 0, 500), new Vector3(500, 0, 500) }, new[] { 50f, 150f, 250f, 400f });
            var car = new GameObject("Menu Kart");
            car.SetActive(false);
            car.transform.SetParent(root.transform);
            car.transform.position = new Vector3(500, 0.6f, 508);
            car.layer = 2;
            car.AddComponent<BoxCollider>();
            car.AddComponent<Rigidbody>();
            tuning = ScriptableObject.CreateInstance<KartTuning>();
            kart = car.AddComponent<ArcadeKartController>();
            kart.Configure(tuning, null, null);
            car.AddComponent<KartRouteProgress>().Configure(route);
            race = car.AddComponent<KartRaceSession>();
            car.SetActive(true);
            var ui = new GameObject("Menu UI");
            ui.SetActive(false);
            ui.transform.SetParent(root.transform);
            hud = ui.AddComponent<PracticeHud>();
            hud.Configure(kart);
            ui.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey(race.BestLapKey);
            PlayerPrefs.DeleteKey(race.RecordPrefix + ".total.1");
            PlayerPrefs.DeleteKey(race.RecordPrefix + ".total.3");
            Object.Destroy(root);
            Object.Destroy(route);
            Object.Destroy(tuning);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenuSettingsAndSelectionStartTheChosenMode()
        {
            Assert.That(hud.IsMenuVisible, Is.True);
            hud.BackFromMenu();
            Assert.That(hud.IsPaused, Is.False, "Escape at the main menu must not open a second pause menu.");
            hud.NavigateMenu(3);
            hud.ConfirmMenuSelection();
            Assert.That(hud.IsSettingsOpen, Is.True);
            Assert.That(kart.DrivingEnabled, Is.False);
            hud.BackFromMenu();
            Assert.That(hud.IsSettingsOpen, Is.False);
            Assert.That(race.State, Is.EqualTo(KartRaceState.Ready));
            hud.NavigateMenu(-2);
            hud.ConfirmMenuSelection();
            Assert.That(race.TotalLaps, Is.EqualTo(3));
            Assert.That(race.State, Is.EqualTo(KartRaceState.Countdown));
            Assert.That(kart.DrivingEnabled, Is.False, "Menu confirmation must still respect countdown input locking.");
            Assert.That(hud.IsMenuVisible, Is.False);
            race.ReturnToMenu();
            yield return null;
            hud.NavigateMenu(2);
            hud.ConfirmMenuSelection();
            Assert.That(race.State, Is.EqualTo(KartRaceState.Practice));
            Assert.That(kart.DrivingEnabled, Is.True);
            Assert.That(hud.IsMenuVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator LeavingPauseSettingsKeepsTheClockStoppedUntilResume()
        {
            race.StartChallenge(1);
            yield return new WaitForSeconds(3.2f);
            Assert.That(race.State, Is.EqualTo(KartRaceState.Racing));
            hud.TogglePause();
            float elapsed = race.ElapsedSeconds;
            hud.NavigateMenu(3);
            hud.ConfirmMenuSelection();
            Assert.That(hud.IsSettingsOpen, Is.True);
            hud.BackFromMenu();
            Assert.That(hud.IsSettingsOpen, Is.False);
            Assert.That(hud.IsPaused, Is.True);
            Assert.That(hud.IsMenuVisible, Is.True);
            Assert.That(AudioListener.pause, Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(race.ElapsedSeconds, Is.EqualTo(elapsed).Within(0.001f));
            hud.BackFromMenu();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(hud.IsMenuVisible, Is.False);
            yield return new WaitForSeconds(0.1f);
            Assert.That(race.ElapsedSeconds, Is.GreaterThan(elapsed));
        }

        [UnityTest]
        public IEnumerator ReturningFromPauseRestoresMainMenuAndCountdownControls()
        {
            race.StartPractice();
            yield return null;
            hud.TogglePause();
            hud.NavigateMenu(2);
            hud.ConfirmMenuSelection();
            Assert.That(race.State, Is.EqualTo(KartRaceState.Ready));
            Assert.That(hud.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(kart.DrivingEnabled, Is.False);
            Assert.That(hud.IsMenuVisible, Is.True);
            hud.ConfirmMenuSelection();
            Assert.That(race.State, Is.EqualTo(KartRaceState.Countdown));
            Assert.That(race.TotalLaps, Is.EqualTo(1));
            yield return null;
        }
    }
}
