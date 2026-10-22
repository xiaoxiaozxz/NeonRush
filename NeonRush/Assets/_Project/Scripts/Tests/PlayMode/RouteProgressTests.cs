using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LoongRacing.Tests
{
    public sealed class RouteProgressTests
    {
        private GameObject root;
        private ArcadeKartController kart;
        private KartRouteProgress progress;
        private TrackRoute route;
        private KartTuning tuning;
        private PhysicsMaterial friction;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            root = new GameObject("Route Test");
            root.AddComponent<AudioListener>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(550f,-0.5f,550f);
            floor.transform.localScale = new Vector3(250f,1f,250f);
            route = ScriptableObject.CreateInstance<TrackRoute>();
            route.Configure("Test",9.5f,new[] {new Vector3(500,0,500),new Vector3(500,0,600),
                new Vector3(600,0,600),new Vector3(600,0,500),new Vector3(500,0,500)},new[] {50f,150f,250f,400f});
            var vehicle = new GameObject("Route Kart");
            vehicle.SetActive(false);vehicle.transform.SetParent(root.transform);vehicle.layer=2;
            vehicle.transform.position=new Vector3(500f,0.6f,508f);
            var box=vehicle.AddComponent<BoxCollider>();box.center=new Vector3(0f,-0.05f,0f);box.size=new Vector3(1.72f,0.9f,3.2f);
            friction=new PhysicsMaterial("Route Test Grip") {dynamicFriction=0f,staticFriction=0f,frictionCombine=PhysicsMaterialCombine.Minimum};
            box.sharedMaterial=friction;
            vehicle.AddComponent<Rigidbody>();
            tuning=ScriptableObject.CreateInstance<KartTuning>();
            kart=vehicle.AddComponent<ArcadeKartController>();kart.Configure(tuning,null,null);
            progress=vehicle.AddComponent<KartRouteProgress>();progress.Configure(route);
            vehicle.SetActive(true);
            for(int i=0;i<12;i++)yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);Object.Destroy(route);Object.Destroy(tuning);Object.Destroy(friction);
            Time.timeScale=1f;yield return null;
        }

        private bool Cross(int index,bool reverse=false,float side=0f,float height=0.6f,float span=2f)
        {
            route.Sample(route.CheckpointDistance(index),out Vector3 p,out Vector3 f);
            f.y=0f;f.Normalize();p+=Vector3.Cross(Vector3.up,f)*side+Vector3.up*height;
            return progress.EvaluateMovement(p-f*span*(reverse?-1f:1f),p+f*span*(reverse?-1f:1f));
        }

        [Test]
        public void OnlyOrderedForwardCrossingsWithinGateCount()
        {
            Assert.That(Cross(3),Is.False,"Finish alone cannot complete a circuit.");
            Assert.That(Cross(1),Is.False,"A future checkpoint cannot be skipped to.");
            Assert.That(Cross(0,true),Is.False,"Reverse crossing must fail.");
            Assert.That(Cross(0,false,11f),Is.False,"Passing beside the gate must fail.");
            Assert.That(Cross(0,false,0f,9f),Is.False,"Flying over the gate must fail.");
            Assert.That(Cross(0,false,0f,0.6f,30f),Is.False,"Teleport segments must fail.");
            Assert.That(Cross(0),Is.True);
            Assert.That(Cross(0),Is.False,"Duplicate crossing cannot add progress.");
            Assert.That(progress.NextCheckpointIndex,Is.EqualTo(1));
            Assert.That(progress.CompletedCircuits,Is.Zero);
        }

        [Test]
        public void CircuitRequiresEveryCheckpointAndCanRepeat()
        {
            int events=0;progress.CircuitCompleted+=()=>events++;
            for(int lap=0;lap<2;lap++)
            {
                for(int i=0;i<route.CheckpointCount;i++)Assert.That(Cross(i),Is.True);
                Assert.That(progress.CompletedCircuits,Is.EqualTo(lap+1));
                Assert.That(progress.NextCheckpointIndex,Is.Zero);
            }
            Assert.That(events,Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator NearbyRecoveryPreservesSessionAndDoesNotPassGates()
        {
            kart.SetDriveInput(1f,0f);
            for(int i=0;i<40;i++)yield return new WaitForFixedUpdate();
            Assert.That(Cross(0),Is.True);
            float time=kart.DrivingTime, distance=kart.DistanceMetres, top=kart.TopSpeedKph;
            kart.RequestRecovery();
            route.Sample(progress.SafeDistance,out Vector3 safe,out Vector3 tangent);
            Assert.That(Vector3.Distance(kart.Body.position,safe+Vector3.up*0.7f),Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(kart.transform.forward,tangent),Is.GreaterThan(0.99f));
            Assert.That(kart.Body.linearVelocity.magnitude,Is.LessThan(0.01f));
            Assert.That(kart.DrivingTime,Is.EqualTo(time));
            Assert.That(kart.DistanceMetres,Is.EqualTo(distance));
            Assert.That(kart.TopSpeedKph,Is.EqualTo(top));
            Assert.That(progress.NextCheckpointIndex,Is.EqualTo(1));
            Assert.That(progress.RecoveryCount,Is.EqualTo(1));
            for(int i=0;i<10;i++)yield return new WaitForFixedUpdate();
            Assert.That(progress.NextCheckpointIndex,Is.EqualTo(1),"Rescue must not cross a checkpoint implicitly.");
            kart.ResetToSpawn();
            Assert.That(progress.NextCheckpointIndex,Is.Zero);
            Assert.That(progress.RecoveryCount,Is.Zero);
            Assert.That(progress.CompletedCircuits,Is.Zero);
            Assert.That(kart.DrivingTime,Is.Zero);
        }

        [UnityTest]
        public IEnumerator FallingAutomaticallyRecoversWithoutAdvancing()
        {
            Cross(0);
            kart.Body.position=new Vector3(550f,-15f,550f);
            for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
            Assert.That(progress.RecoveryCount,Is.EqualTo(1));
            Assert.That(progress.NextCheckpointIndex,Is.EqualTo(1));
            Assert.That(kart.Body.position.y,Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator OffRoadTimeoutRecoversAndWrongWayWarns()
        {
            kart.RecoverToPose(new Vector3(500f,0.6f,540f),Quaternion.Euler(0f,180f,0f));
            kart.Body.linearVelocity=Vector3.back*15f;
            kart.SetDriveInput(1f,0f);
            for(int i=0;i<70;i++)yield return new WaitForFixedUpdate();
            Assert.That(progress.IsWrongWay,Is.True,$"Sustained backwards travel must show a warning: pos={kart.Body.position} velocity={kart.Body.linearVelocity} heading={kart.transform.forward} speed={kart.SpeedKph}");
            kart.Body.position=new Vector3(550f,0.6f,550f);
            kart.Body.linearVelocity=Vector3.zero;kart.SetDriveInput(0f,0f);
            for(int i=0;i<72;i++)yield return new WaitForFixedUpdate();
            Assert.That(progress.RecoveryCount,Is.EqualTo(1));
            Assert.That(progress.IsWrongWay,Is.False);
            Assert.That(progress.NextCheckpointIndex,Is.Zero);
        }
    }
}
