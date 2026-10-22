using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LoongRacing.Tests
{
    public sealed class HarborCircuitTests
    {
        private Scene loaded;
        private KartRouteProgress progress;
        private ArcadeKartController kart;
        private TrackRoute temporaryRoute;
        private string recordPrefix;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=1f;AudioListener.pause=false;
            if(loaded.IsValid() && loaded.isLoaded)yield return SceneManager.UnloadSceneAsync(loaded);
            if(temporaryRoute!=null)Object.Destroy(temporaryRoute);
            if(recordPrefix!=null)
            {PlayerPrefs.DeleteKey(recordPrefix+".lap");PlayerPrefs.DeleteKey(recordPrefix+".total.1");PlayerPrefs.DeleteKey(recordPrefix+".total.3");}
        }

        private IEnumerator LoadCircuit(string sceneName="HarborCircuit")
        {
            yield return SceneManager.LoadSceneAsync(sceneName,LoadSceneMode.Additive);
            loaded=SceneManager.GetSceneByName(sceneName);
            progress=null;
            foreach(var root in loaded.GetRootGameObjects())
            {
                var candidate=root.GetComponentInChildren<KartRouteProgress>();
                if(candidate!=null)progress=candidate;
                var hud=root.GetComponent<PracticeHud>();if(hud!=null)hud.gameObject.SetActive(false);
            }
            Assert.That(progress,Is.Not.Null);
            kart=progress.GetComponent<ArcadeKartController>();
            temporaryRoute=Object.Instantiate(progress.Route);
            temporaryRoute.name="HarborQA-"+System.Guid.NewGuid().ToString("N");
            progress.Configure(temporaryRoute);
            recordPrefix=kart.GetComponent<KartRaceSession>().RecordPrefix;
            kart.GetComponent<KartKeyboardInput>().enabled=false;
            var race=kart.GetComponent<KartRaceSession>();
            if(race!=null)race.StartPractice();
        }

        [UnityTest]
        public IEnumerator OptionalJumpNaturallyEnablesAirAndLandingBoosts()
        {
            yield return LoadCircuit();
            yield return VerifyJump();
        }

        private IEnumerator VerifyJump()
        {
            var route=progress.Route;
            route.Sample(36f,out Vector3 p,out Vector3 f);f.y=0f;f.Normalize();
            kart.RecoverToPose(p-Vector3.Cross(Vector3.up,f)*5f+Vector3.up*0.65f,Quaternion.LookRotation(f));
            kart.SetDriveInput(1f,0f);
            bool air=false,landing=false;float peak=0f,elapsed=0f;
            while(elapsed<7f && !landing)
            {
                kart.SetDriveInput(1f,0f);
                if(kart.AirBoostReady && !air)air=kart.TryUseQuickBoost();
                if(kart.LandingBoostReady && !landing)landing=kart.TryUseQuickBoost();
                peak=Mathf.Max(peak,kart.Body.position.y);
                elapsed+=Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            Debug.Log($"[Jump QA] air={air} landing={landing} peak={peak:0.00}m speed={kart.SpeedKph:0.0}km/h pos={kart.Body.position} ground={kart.IsGrounded} throttle={kart.Throttle} rescues={progress.RecoveryCount}");
            Assert.That(air,Is.True,"Actual ramp must create a usable air boost window.");
            Assert.That(landing,Is.True,"Natural landing must create a separate landing boost window.");
            Assert.That(peak,Is.GreaterThan(1.8f));
            Assert.That(progress.RecoveryCount,Is.Zero);
        }

        [UnityTest]
        public IEnumerator NeonCoupeJumpHasSeparateAirAndLandingBoosts()
        {
            yield return LoadCircuit("NeonHarbor");
            Assert.That(kart.GetComponent<KartVisualRig>(),Is.Not.Null);
            yield return VerifyJump();
        }

        [UnityTest]
        public IEnumerator NeonRigWheelsSteerRollAndBrakeAtBoundAnchors()
        {
            yield return LoadCircuit("NeonHarbor");
            var rig=kart.GetComponent<KartVisualRig>();
            Assert.That(rig,Is.Not.Null);
            Assert.That(rig.Exhausts.Length,Is.EqualTo(2));Assert.That(rig.RearContacts.Length,Is.EqualTo(2));
            Assert.That(rig.Exhausts[0].localPosition.z,Is.LessThan(-1.4f));
            Assert.That(rig.RearContacts[0].localPosition.x,Is.LessThan(0f));
            Assert.That(kart.GetComponent<BoxCollider>().size.x,Is.LessThan(1.4f));
            kart.SetDriveInput(1,0);
            for(int i=0;i<60;i++)yield return new WaitForFixedUpdate();
            Assert.That(rig.WheelAngle,Is.GreaterThan(1f));
            kart.SetDriveInput(1,.6f);
            for(int i=0;i<15;i++)yield return new WaitForFixedUpdate();
            Transform pivot=null;
            foreach(var t in kart.GetComponentsInChildren<Transform>())if(t.name=="Wheel_FL Steering")pivot=t;
            Assert.That(pivot,Is.Not.Null);Assert.That(Mathf.Abs(Mathf.DeltaAngle(0,pivot.localEulerAngles.y)),Is.GreaterThan(8f));
            kart.SetDriveInput(-1,0);yield return null;
            Assert.That(rig.BrakeLightsOn,Is.True);
            int solid=0;foreach(var c in kart.GetComponentsInChildren<Collider>())if(c.enabled && !c.isTrigger)solid++;
            Assert.That(solid,Is.EqualTo(1),"Decorative meshes must not add physics colliders.");
        }

        [UnityTest]
        public IEnumerator NeonCoupeFinishesThreePhysicalLapsWithoutRecovery()
        {
            yield return LoadCircuit("NeonHarbor");
            Time.timeScale=3f;var race=kart.GetComponent<KartRaceSession>();race.StartChallenge(3);
            while(race.State==KartRaceState.Countdown)yield return new WaitForFixedUpdate();
            int gates=0;progress.ProgressChanged+=()=>gates++;
            float elapsed=0f,offset=0f;
            while(elapsed<140f && race.State!=KartRaceState.Finished)
            {
                progress.Route.FindNearest(kart.Body.position,out Vector3 p,out Vector3 f,out float along);
                float speed=kart.SpeedKph/3.6f;
                progress.Route.Sample(along+9f+speed*.35f,out Vector3 target,out Vector3 tf);
                Vector3 delta=Vector3.ProjectOnPlane(target-kart.Body.position,Vector3.up);
                float yaw=2f*Mathf.Max(8f,speed)*Mathf.Sin(Vector3.SignedAngle(kart.transform.forward,delta,Vector3.up)*Mathf.Deg2Rad)/Mathf.Max(1f,delta.magnitude);
                float rate=Mathf.Lerp(kart.Tuning.lowSpeedSteering,kart.Tuning.highSpeedSteering,Mathf.InverseLerp(12f,kart.Tuning.maximumForwardSpeed,speed))*Mathf.Deg2Rad;
                kart.SetDriveInput(speed>29f?-.3f:1f,Mathf.Clamp(yaw/rate,-1f,1f));
                offset=Mathf.Max(offset,Vector3.ProjectOnPlane(kart.Body.position-p,Vector3.up).magnitude);
                elapsed+=Time.fixedDeltaTime;yield return new WaitForFixedUpdate();
            }
            Debug.Log($"[Neon QA] state={race.State} laps={race.LapTimes.Count} total={race.ElapsedSeconds:0.000}s gates={gates} offset={offset:0.00}m rescues={progress.RecoveryCount}");
            Assert.That(race.State,Is.EqualTo(KartRaceState.Finished));Assert.That(race.LapTimes.Count,Is.EqualTo(3));
            Assert.That(gates,Is.EqualTo(36));Assert.That(progress.RecoveryCount,Is.Zero);
            Assert.That(offset,Is.LessThan(3f));Assert.That(kart.DrivingEnabled,Is.False);
        }

        [UnityTest]
        public IEnumerator SingleLapChallengeFinishesThroughAllTwelveGatesWithPhysicalDriving()
        {
            yield return LoadCircuit();
            Time.timeScale=3f;
            var race=kart.GetComponent<KartRaceSession>();race.StartChallenge(1);
            while(race.State==KartRaceState.Countdown)yield return new WaitForFixedUpdate();
            var route=progress.Route;
            float elapsed=0f,peakHeight=0f,maxOffset=0f;
            int gates=0;progress.ProgressChanged+=()=>gates++;
            // Test driver uses the same throttle/steering interface as the keyboard.
            // It never moves the body or awards checkpoints directly.
            while(elapsed<65f && progress.CompletedCircuits==0)
            {
                route.FindNearest(kart.Body.position,out Vector3 nearest,out Vector3 forward,out float along);
                float speed=kart.SpeedKph/3.6f;
                float lookahead=9f+speed*0.35f;
                route.Sample(along+lookahead,out Vector3 target,out Vector3 targetForward);
                Vector3 toTarget=Vector3.ProjectOnPlane(target-kart.Body.position,Vector3.up);
                float angle=Vector3.SignedAngle(kart.transform.forward,toTarget,Vector3.up)*Mathf.Deg2Rad;
                float yaw=2f*Mathf.Max(8f,speed)*Mathf.Sin(angle)/Mathf.Max(1f,toTarget.magnitude);
                float steeringRate=Mathf.Lerp(kart.Tuning.lowSpeedSteering,kart.Tuning.highSpeedSteering,
                    Mathf.InverseLerp(12f,kart.Tuning.maximumForwardSpeed,speed))*Mathf.Deg2Rad;
                float steer=Mathf.Clamp(yaw/steeringRate,-1f,1f);
                float throttle=speed>29f ? -0.3f : 1f;
                kart.SetDriveInput(throttle,steer);
                peakHeight=Mathf.Max(peakHeight,kart.Body.position.y);
                maxOffset=Mathf.Max(maxOffset,Vector3.ProjectOnPlane(kart.Body.position-nearest,Vector3.up).magnitude);
                elapsed+=Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            Debug.Log($"[Circuit QA] elapsed={elapsed:0.00}s gates={gates} height={peakHeight:0.00}m maxOffset={maxOffset:0.00}m recoveries={progress.RecoveryCount}");
            Assert.That(progress.CompletedCircuits,Is.EqualTo(1),"A physically driven full route must complete within the budget.");
            Assert.That(progress.RecoveryCount,Is.Zero,"The track must be drivable without recovery.");
            Assert.That(gates,Is.EqualTo(12));
            Assert.That(peakHeight,Is.GreaterThan(6f),"The driver must traverse the elevated S section.");
            Assert.That(maxOffset,Is.LessThan(route.HalfWidth-1f),"Centre lane must stay clear and within barriers.");
            Assert.That(race.State,Is.EqualTo(KartRaceState.Finished));
            Assert.That(race.LapTimes.Count,Is.EqualTo(1));
            Assert.That(race.ElapsedSeconds,Is.GreaterThan(25f));
            Assert.That(kart.DrivingEnabled,Is.False);
        }
    }
}
