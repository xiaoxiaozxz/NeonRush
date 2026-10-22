using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LoongRacing.Editor
{
    public static class HarborCircuitBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/HarborCircuit.unity";
        private const string Root = "Assets/_Project/";

        [MenuItem("NeonRush/Open Harbor Circuit")]
        public static void OpenOrBuild()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Build();
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Circuit already exists. Open it to preserve edits.");
            var route = BuildRoute();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "HarborCircuit";
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.62f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.4f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.22f, 0.25f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.27f, 0.4f, 0.49f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 145f;
            RenderSettings.fogEndDistance = 420f;

            var world = new GameObject("Harbor Circuit - Greybox").transform;
            Cube("Harbor Water", world, new Vector3(80f, -12.4f, -35f), new Vector3(700f, 0.6f, 700f), Mat("Ground"));
            BuildSurface(route, world);
            BuildEdges(route, world);
            BuildMarkings(route, world);
            BuildJump(route, world);
            BuildScenery(route, world);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.92f, 0.8f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, 145f, 0f);
            RenderSettings.sun = sun;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/BlockKart.prefab");
            var vehicle = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var kart = vehicle.GetComponent<ArcadeKartController>();
            route.Sample(8f, out Vector3 start, out Vector3 forward);
            forward.y = 0f;
            vehicle.transform.SetPositionAndRotation(start + Vector3.up * 0.65f, Quaternion.LookRotation(forward));
            var progress = vehicle.AddComponent<KartRouteProgress>();
            progress.Configure(route);
            BuildGates(route, progress, world);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 550f;
            cameraObject.AddComponent<AudioListener>();
            var chase = cameraObject.AddComponent<KartChaseCamera>();
            chase.Configure(kart); chase.SnapToTarget();
            new GameObject("Practice HUD").AddComponent<PracticeHud>().Configure(kart);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save circuit.");
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var existing in EditorBuildSettings.scenes) if (existing.path != ScenePath) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = vehicle;
            StageFiveSetup.Apply();
            SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(80f, 0f, -35f), new Vector3(220f, 35f, 390f)), false);
            Debug.Log("[NeonRush] Harbor Circuit ready: " + route.Length.ToString("0") + "m / " + route.CheckpointCount + " gates.");
            return ScenePath;
        }

        private static TrackRoute BuildRoute()
        {
            const string path = Root + "Data/HarborRoute.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TrackRoute>(path);
            if (existing != null) return existing;
            var nodes = new[] {
                new Vector3(0,0,-90), new Vector3(0,0,-20), new Vector3(0,0,50), new Vector3(20,0,110),
                new Vector3(75,0,135), new Vector3(135,0,115), new Vector3(160,0,65), new Vector3(145,0,20),
                new Vector3(118,2,-15), new Vector3(122,6,-55), new Vector3(158,6,-95), new Vector3(175,3,-135),
                new Vector3(155,0,-185), new Vector3(105,0,-205), new Vector3(55,0,-190), new Vector3(10,0,-145) };
            var samples = new List<Vector3>();
            for (int i = 0; i < nodes.Length; i++)
            {
                Vector3 a = nodes[(i + nodes.Length - 1) % nodes.Length], b = nodes[i];
                Vector3 c = nodes[(i + 1) % nodes.Length], d = nodes[(i + 2) % nodes.Length];
                for (int j = 0; j < 32; j++)
                {
                    float t = j / 32f;
                    Vector3 p = 0.5f * ((2f * b) + (-a + c) * t + (2f*a - 5f*b + 4f*c - d)*t*t + (-a + 3f*b - 3f*c + d)*t*t*t);
                    p.y = Mathf.Max(0f, p.y);
                    samples.Add(p);
                }
            }
            samples.Add(samples[0]);
            float length = 0f;
            for (int i = 1; i < samples.Count; i++) length += Vector3.Distance(samples[i - 1], samples[i]);
            var gates = new float[12];
            for (int i = 0; i < gates.Length; i++) gates[i] = length * (i + 1f) / gates.Length;
            var route = ScriptableObject.CreateInstance<TrackRoute>();
            route.Configure("霓虹港湾", 9.5f, samples.ToArray(), gates);
            AssetDatabase.CreateAsset(route, path);
            return route;
        }

        private static void BuildSurface(TrackRoute route, Transform parent)
        {
            int count = Mathf.CeilToInt(route.Length / 1.8f);
            var vertices = new Vector3[(count + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[count * 6];
            for (int i = 0; i <= count; i++)
            {
                float distance = route.Length * i / count;
                route.Sample(distance, out Vector3 p, out Vector3 f);
                Vector3 side = Vector3.Cross(Vector3.up, f).normalized;
                vertices[i*2] = p - side * route.HalfWidth;
                vertices[i*2+1] = p + side * route.HalfWidth;
                uv[i*2] = new Vector2(0f, distance/8f); uv[i*2+1] = new Vector2(1f,distance/8f);
                if (i == count) continue;
                int k = i*6, v = i*2;
                triangles[k] = v; triangles[k+1] = v+2; triangles[k+2] = v+1;
                triangles[k+3] = v+1; triangles[k+4] = v+2; triangles[k+5] = v+3;
            }
            var mesh = new Mesh { name = "Harbor Continuous Road", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Root + "Art/Meshes/HarborRoad.asset");
            var road = new GameObject("Continuous Road Surface"); road.transform.SetParent(parent, false);
            road.AddComponent<MeshFilter>().sharedMesh = mesh;
            road.AddComponent<MeshRenderer>().sharedMaterial = Mat("Road");
            road.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void BuildEdges(TrackRoute route, Transform parent)
        {
            var edges = new GameObject("Continuous Guardrails").transform; edges.SetParent(parent, false);
            int count = Mathf.CeilToInt(route.Length / 3.5f);
            for (int i = 0; i < count; i++)
            {
                route.Sample(route.Length*i/count, out Vector3 a, out Vector3 fa);
                route.Sample(route.Length*(i+1)/count, out Vector3 b, out Vector3 fb);
                Vector3 sa = Vector3.Cross(Vector3.up, fa).normalized, sb = Vector3.Cross(Vector3.up, fb).normalized;
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 c = a + sa * side * (route.HalfWidth + 0.45f);
                    Vector3 d = b + sb * side * (route.HalfWidth + 0.45f);
                    Quaternion facing = Quaternion.LookRotation(d - c);
                    var barrier = Cube("Guardrail", edges, (c+d)*0.5f+Vector3.up*0.7f,
                        new Vector3(0.7f,1.4f,Vector3.Distance(c,d)+0.12f), Mat("Barrier_Graphite"), true);
                    barrier.transform.rotation = facing;
                    var strip = Cube("Guide Light", edges, (c+d)*0.5f+Vector3.up*1.44f,
                        new Vector3(0.74f,0.07f,Vector3.Distance(c,d)+0.1f), Mat("Accent_Cyan"));
                    strip.transform.rotation = facing;
                    var curb = Cube("Curb", edges, (a+b)*0.5f+(sa+sb).normalized*side*(route.HalfWidth-0.3f)+Vector3.up*0.025f,
                        new Vector3(0.65f,0.05f,Vector3.Distance(a,b)+0.08f), Mat(i%2 == 0 ? "Curb_Cyan" : "Marking_White"));
                    curb.transform.rotation = Quaternion.LookRotation(b-a);
                }
            }
        }

        private static void BuildMarkings(TrackRoute route, Transform parent)
        {
            var markings = new GameObject("Route Markings").transform; markings.SetParent(parent, false);
            for (float distance = 16f; distance < route.Length; distance += 16f)
            {
                route.Sample(distance, out Vector3 p, out Vector3 f);
                var dash = Cube("Centre Dash", markings, p+Vector3.up*0.025f, new Vector3(0.16f,0.035f,3f), Mat("Marking_White"));
                dash.transform.rotation = Quaternion.LookRotation(f);
                if (Mathf.RoundToInt(distance/16f)%2 != 0) continue;
                Vector3 side = Vector3.Cross(Vector3.up,f).normalized;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var arrow = Cube("Forward Chevron", markings, p+Vector3.up*0.04f+side*sign*0.6f,
                        new Vector3(0.22f,0.04f,1.8f), Mat("Kart_Yellow"));
                    arrow.transform.rotation = Quaternion.LookRotation(f)*Quaternion.Euler(0f,-sign*48f,0f);
                }
            }
            route.Sample(0f, out Vector3 start, out Vector3 heading);
            Vector3 right = Vector3.Cross(Vector3.up,heading).normalized;
            for (int row=0; row<2; row++) for (int col=0; col<18; col++)
            {
                var square=Cube("Start Checker",markings,start+right*(col-8.5f)+heading*(row-0.5f)+Vector3.up*0.04f,
                    new Vector3(1f,0.04f,1f),Mat((row+col)%2 == 0 ? "Marking_White" : "Barrier_Graphite"));
                square.transform.rotation=Quaternion.LookRotation(heading);
            }
        }

        private static void BuildGates(TrackRoute route, KartRouteProgress progress, Transform parent)
        {
            var gates = new GameObject("Sequential Checkpoint Gates").transform; gates.SetParent(parent,false);
            for (int i=0; i<route.CheckpointCount; i++)
            {
                route.Sample(route.CheckpointDistance(i),out Vector3 p,out Vector3 f);
                f.y=0f; f.Normalize(); Vector3 side=Vector3.Cross(Vector3.up,f);
                var gate=new GameObject("Checkpoint " + (i+1).ToString("00")); gate.transform.SetParent(gates,false);
                var lights=new List<Renderer>();
                foreach(int sign in new[] {-1,1})
                {
                    Cube("Gate Post",gate.transform,p+side*sign*(route.HalfWidth+1.3f)+Vector3.up*4f,new Vector3(0.8f,8f,0.8f),Mat("Barrier_Graphite"));
                    lights.Add(Cube("Checkpoint Light",gate.transform,p+side*sign*(route.HalfWidth+0.82f)+Vector3.up*4f,
                        new Vector3(0.2f,7.6f,0.2f),Mat("Accent_Cyan")).GetComponent<Renderer>());
                }
                var top=Cube("Gate Beam",gate.transform,p+Vector3.up*8f,new Vector3(route.HalfWidth*2f+3.4f,0.65f,0.8f),Mat("Barrier_Graphite"));
                top.transform.rotation=Quaternion.LookRotation(f);
                var glow=Cube("Gate Signal",gate.transform,p+Vector3.up*7.6f,new Vector3(route.HalfWidth*2f+1f,0.12f,0.9f),Mat("Accent_Cyan"));
                glow.transform.rotation=Quaternion.LookRotation(f);lights.Add(glow.GetComponent<Renderer>());
                Label(i==route.CheckpointCount-1 ? "HARBOR / START" : "CHECKPOINT " + (i+1).ToString("00"),gate.transform,
                    p+Vector3.up*8.7f,Quaternion.LookRotation(f),0.12f);
                gate.AddComponent<CheckpointGateView>().Configure(progress,i,lights.ToArray());
            }
        }

        private static void BuildJump(TrackRoute route, Transform parent)
        {
            route.Sample(48f,out Vector3 p,out Vector3 f); f.y=0f;f.Normalize();
            Vector3 side=Vector3.Cross(Vector3.up,f);
            var ramp = new GameObject("Optional Jump Lane"); ramp.transform.SetParent(parent,false);
            ramp.transform.SetPositionAndRotation(p-side*5f,Quaternion.LookRotation(f));
            var mesh=new Mesh { name="Harbor Optional Jump" };
            // Bury the toe to avoid a tiny vertical lip catching a flat chassis at low speed.
            mesh.vertices=new[] { new Vector3(-2.75f,-0.18f,0f),new Vector3(2.75f,-0.18f,0f),new Vector3(-2.75f,1.6f,14f),new Vector3(2.75f,1.6f,14f),new Vector3(-2.75f,0f,14f),new Vector3(2.75f,0f,14f) };
            mesh.triangles=new[] {0,2,1,1,2,3,2,4,3,3,4,5,0,1,5,0,5,4,0,4,2,1,3,5};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Root+"Art/Meshes/HarborJump.asset");
            ramp.AddComponent<MeshFilter>().sharedMesh=mesh;ramp.AddComponent<MeshRenderer>().sharedMaterial=Mat("Curb_Cyan");
            ramp.AddComponent<MeshCollider>().sharedMesh=mesh;
            Label("AIR / W",ramp.transform,ramp.transform.TransformPoint(new Vector3(0f,0.63f,4f)),ramp.transform.rotation*Quaternion.Euler(90f,0f,0f),0.075f);
            Label("JUMP  /  LEFT LANE",parent,p-f*9f+Vector3.up*0.05f,Quaternion.LookRotation(f)*Quaternion.Euler(90f,0f,0f),0.075f);
        }

        private static void BuildScenery(TrackRoute route, Transform parent)
        {
            var scenery=new GameObject("Harbor Greybox Skyline").transform;scenery.SetParent(parent,false);
            var random=new System.Random(41021);
            for(int i=0;i<72;i++)
            {
                Vector3 p=new Vector3(-65f+(float)random.NextDouble()*320f,0f,-260f+(float)random.NextDouble()*450f);
                float offset=route.FindNearest(p,out Vector3 nearest,out Vector3 tangent,out float along);
                if(offset<28f)continue;
                float h=8f+(float)random.NextDouble()*30f;
                Cube("Warehouse Block",scenery,p+Vector3.up*(h*0.5f-9f),new Vector3(9f,h,12f),Mat("Buildings"));
                Cube("Roof Beacon",scenery,p+Vector3.up*(h-8.95f),new Vector3(9.1f,0.12f,12.1f),Mat("Accent_Cyan"));
            }
            route.Sample(route.Length*0.58f,out Vector3 sign,out Vector3 forward);
            Label("S CURVES  /  E DRIFT",parent,sign+Vector3.up*0.06f,Quaternion.LookRotation(forward)*Quaternion.Euler(90f,0f,0f),0.1f);
            for(float d=route.Length*0.5f;d<route.Length*0.75f;d+=22f)
            {
                route.Sample(d,out Vector3 p,out Vector3 f);
                Cube("Elevated Road Support",parent,new Vector3(p.x,(p.y-12f)*0.5f,p.z),new Vector3(4f,p.y+12f,4f),Mat("Barrier_Graphite"));
            }
        }

        private static Material Mat(string name)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/"+name+".mat");
            if(material==null)throw new InvalidOperationException("Missing material: "+name);
            return material;
        }
        private static GameObject Cube(string name,Transform parent,Vector3 p,Vector3 scale,Material material,bool solid=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.position=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        private static void Label(string value,Transform parent,Vector3 position,Quaternion rotation,float size)
        {
            var go=new GameObject(value);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(position,rotation);
            var text=go.AddComponent<TextMesh>();text.text=value;text.fontSize=80;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
        }
    }
}
