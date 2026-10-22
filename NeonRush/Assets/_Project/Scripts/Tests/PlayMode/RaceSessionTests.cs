using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LoongRacing.Tests
{
    public sealed class RaceSessionTests
    {
        private GameObject root;
        private TrackRoute route;
        private KartTuning tuning;
        private ArcadeKartController kart;
        private KartRouteProgress progress;
        private KartRaceSession race;
        private string recordPrefix;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale=1f;AudioListener.pause=false;
            root=new GameObject("Race Session Test");root.AddComponent<AudioListener>();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);
            floor.transform.position=new Vector3(550f,-0.5f,550f);floor.transform.localScale=new Vector3(250f,1f,250f);
            route=ScriptableObject.CreateInstance<TrackRoute>();route.name="RaceTest-"+Guid.NewGuid().ToString("N");
            route.Configure("Race Test",9.5f,new[] {new Vector3(500,0,500),new Vector3(500,0,600),new Vector3(600,0,600),
                new Vector3(600,0,500),new Vector3(500,0,500)},new[] {50f,150f,250f,400f});
            var car=new GameObject("Race Kart");car.SetActive(false);car.transform.SetParent(root.transform);car.layer=2;
            car.transform.position=new Vector3(500f,0.6f,508f);
            var box=car.AddComponent<BoxCollider>();box.center=new Vector3(0f,-0.05f,0f);box.size=new Vector3(1.72f,0.9f,3.2f);
            car.AddComponent<Rigidbody>();
            tuning=ScriptableObject.CreateInstance<KartTuning>();
            kart=car.AddComponent<ArcadeKartController>();kart.Configure(tuning,null,null);
            progress=car.AddComponent<KartRouteProgress>();progress.Configure(route);
            car.AddComponent<KartRaceSession>();car.SetActive(true);
            race=car.GetComponent<KartRaceSession>();
            recordPrefix=race.RecordPrefix;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey(recordPrefix+".lap");PlayerPrefs.DeleteKey(recordPrefix+".total.1");PlayerPrefs.DeleteKey(recordPrefix+".total.3");
            UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(route);UnityEngine.Object.Destroy(tuning);
            Time.timeScale=1f;AudioListener.pause=false;yield return null;
        }

        private bool Cross(int gate,bool reverse=false)
        {
            route.Sample(route.CheckpointDistance(gate),out Vector3 p,out Vector3 f);f.y=0f;f.Normalize();p+=Vector3.up*0.6f;
            return progress.EvaluateMovement(p-f*(reverse?-2f:2f),p+f*(reverse?-2f:2f));
        }

        private IEnumerator WaitForGo()
        {
            float timeout=Time.realtimeSinceStartup+5f;
            while(race.State==KartRaceState.Countdown && Time.realtimeSinceStartup<timeout)yield return null;
            Assert.That(race.State,Is.EqualTo(KartRaceState.Racing));
        }

        private IEnumerator CompleteLap()
        {
            for(int i=0;i<route.CheckpointCount;i++)
            {
                yield return new WaitForSeconds(0.06f);
                Assert.That(Cross(i),Is.True);
            }
        }

        [UnityTest]
        public IEnumerator CountdownLocksControlsAndBothClocksRespectPause()
        {
            Assert.That(race.State,Is.EqualTo(KartRaceState.Ready));
            Assert.That(kart.DrivingEnabled,Is.False);
            Assert.That(Cross(0),Is.False);
            race.StartChallenge(1);
            Vector3 start=kart.Body.position;
            kart.SetDriveInput(1f,1f);kart.SetDriftInput(true);kart.Body.linearVelocity=Vector3.forward*15f;
            yield return new WaitForSeconds(0.25f);
            Assert.That(Vector3.ProjectOnPlane(kart.Body.position-start,Vector3.up).magnitude,Is.LessThan(0.05f));
            Assert.That(Cross(0),Is.False,"Countdown must not award checkpoints.");
            Assert.That(kart.TryUseNitro(),Is.False);
            int number=race.CountdownNumber;Time.timeScale=0f;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(race.CountdownNumber,Is.EqualTo(number));
            Time.timeScale=1f;yield return WaitForGo();
            Assert.That(kart.DrivingEnabled,Is.True);
            Assert.That(race.ElapsedSeconds,Is.LessThan(0.15f),"Countdown must not count towards race time.");
            yield return new WaitForSeconds(0.2f);
            float elapsed=race.ElapsedSeconds,lap=race.CurrentLapSeconds;
            Time.timeScale=0f;yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(race.ElapsedSeconds,Is.EqualTo(elapsed).Within(0.001f));
            Assert.That(race.CurrentLapSeconds,Is.EqualTo(lap).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator OrderedFinishProducesOneResultAndRetryClearsRunOnly()
        {
            race.StartChallenge(1);yield return WaitForGo();
            Assert.That(Cross(3),Is.False);Assert.That(Cross(0,true),Is.False);
            Assert.That(race.LapTimes.Count,Is.Zero);
            yield return CompleteLap();
            Assert.That(race.State,Is.EqualTo(KartRaceState.Finished));
            Assert.That(race.LapTimes.Count,Is.EqualTo(1));
            Assert.That(race.ElapsedSeconds,Is.GreaterThan(0.2f));
            Assert.That(kart.DrivingEnabled,Is.False);
            Assert.That(Cross(0),Is.False,"Finished races must not keep collecting gates.");
            float result=race.ElapsedSeconds,best=race.BestLap;
            yield return new WaitForSeconds(0.15f);
            Assert.That(race.ElapsedSeconds,Is.EqualTo(result));
            race.StartChallenge(1);
            Assert.That(race.State,Is.EqualTo(KartRaceState.Countdown));
            Assert.That(race.ElapsedSeconds,Is.Zero);Assert.That(race.LapTimes.Count,Is.Zero);
            Assert.That(progress.NextCheckpointIndex,Is.Zero);Assert.That(progress.RecoveryCount,Is.Zero);
            Assert.That(race.BestLap,Is.EqualTo(best));
        }

        [UnityTest]
        public IEnumerator ThreeLapsKeepTimingThroughRecoveryAndSaveSeparateTotal()
        {
            race.StartChallenge(3);yield return WaitForGo();
            yield return CompleteLap();
            Assert.That(race.CurrentLap,Is.EqualTo(2));Assert.That(race.State,Is.EqualTo(KartRaceState.Racing));
            float before=race.ElapsedSeconds;kart.RequestRecovery();
            yield return new WaitForSeconds(0.15f);
            Assert.That(progress.RecoveryCount,Is.EqualTo(1));Assert.That(race.ElapsedSeconds,Is.GreaterThan(before));
            yield return CompleteLap();yield return CompleteLap();
            Assert.That(race.State,Is.EqualTo(KartRaceState.Finished));
            Assert.That(race.LapTimes.Count,Is.EqualTo(3));
            float total=0f;foreach(float lap in race.LapTimes){Assert.That(lap,Is.GreaterThan(0.2f));total+=lap;}
            Assert.That(total,Is.EqualTo(race.ElapsedSeconds).Within(0.001f));
            Assert.That(RaceRecordStore.Load(recordPrefix+".total.3"),Is.EqualTo(total).Within(0.001f));
            Assert.That(RaceRecordStore.Load(recordPrefix+".total.1"),Is.Zero);
            float best=race.BestLap;race.ReturnToMenu();
            Assert.That(race.BestLap,Is.EqualTo(best));
        }

        [UnityTest]
        public IEnumerator PracticeDoesNotWriteRecordsAndFullResetReturnsToMenu()
        {
            race.StartPractice();yield return CompleteLap();
            Assert.That(progress.CompletedCircuits,Is.EqualTo(1));Assert.That(race.LapTimes.Count,Is.Zero);
            Assert.That(RaceRecordStore.Load(race.BestLapKey),Is.Zero);
            kart.ResetToSpawn();
            Assert.That(race.State,Is.EqualTo(KartRaceState.Ready));
            Assert.That(kart.DrivingEnabled,Is.False);Assert.That(progress.NextCheckpointIndex,Is.Zero);
        }

        [Test]
        public void RecordsPersistOnlyFinitePositiveImprovements()
        {
            string key=recordPrefix+".lap";
            Assert.That(RaceRecordStore.TryImprove(key,float.NaN),Is.False);
            Assert.That(RaceRecordStore.TryImprove(key,float.PositiveInfinity),Is.False);
            Assert.That(RaceRecordStore.TryImprove(key,0f),Is.False);
            Assert.That(RaceRecordStore.TryImprove(key,42f),Is.True);
            Assert.That(RaceRecordStore.TryImprove(key,43f),Is.False);
            Assert.That(RaceRecordStore.TryImprove(key,40f),Is.True);
            Assert.That(RaceRecordStore.Load(key),Is.EqualTo(40f));
        }
    }
}
