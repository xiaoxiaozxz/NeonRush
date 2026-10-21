using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LoongRacing.Editor
{
    public static class TrainingSceneBuilder
    {
        public const string ScenePath = "Assets/LoongRacing/Scenes/TrainingGround.unity";
        private const string Root = "Assets/LoongRacing";
        private const float Radius = 34f;
        private const float Straight = 90f;
        private const float HalfWidth = 8f;
        private const int Segments = 196;
        private static float Length => Straight * 2f + Mathf.PI * Radius * 2f;

        [MenuItem("NeonRush/Open Training Ground")]
        public static void OpenOrBuild()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before opening a scene.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Build();
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building the training scene.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Training scene already exists. Open it instead of overwriting edits.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before building a new one.");
            foreach (string folder in new[] { "Scenes", "Materials", "Meshes", "Prefabs", "Settings" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();

            var road = Material("Road", new Color(0.115f, 0.15f, 0.19f));
            var baseGround = Material("Ground", new Color(0.065f, 0.11f, 0.13f));
            var curb = Material("Curb_Cyan", new Color(0.07f, 0.66f, 0.65f));
            var white = Material("Marking_White", new Color(0.84f, 0.91f, 0.91f));
            var dark = Material("Barrier_Graphite", new Color(0.045f, 0.075f, 0.105f));
            var accent = Material("Accent_Cyan", new Color(0.13f, 0.95f, 0.83f), 0.6f);
            var yellow = Material("Kart_Yellow", new Color(1f, 0.68f, 0.08f));
            var black = Material("Tyre_Black", new Color(0.023f, 0.03f, 0.04f));
            var window = Material("Window_Blue", new Color(0.08f, 0.34f, 0.46f));
            var red = Material("Rear_Lights", new Color(1f, 0.16f, 0.14f), 1.2f);
            var headlight = Material("Headlights", new Color(0.82f, 0.98f, 1f), 0.8f);
            var buildings = Material("Buildings", new Color(0.11f, 0.2f, 0.25f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TrainingGround";
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.62f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.37f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.2f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.27f, 0.4f, 0.49f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 110f;
            RenderSettings.fogEndDistance = 300f;
            RenderSettings.skybox = null;

            var world = new GameObject("Training Ground");
            Cube("Foundation", world.transform, new Vector3(0f, -0.33f, 0f), new Vector3(280f, 0.5f, 300f), baseGround, true);
            BuildRoad(world.transform, road);
            BuildTrackEdges(world.transform, curb, white, dark, accent);
            BuildMarkings(world.transform, white, dark, yellow);
            BuildScenery(world.transform, buildings, dark, accent, white, yellow);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.91f, 0.77f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, 150f, 0f);
            RenderSettings.sun = sun;

            var tuning = AssetDatabase.LoadAssetAtPath<KartTuning>(Root + "/Settings/ArcadeKartTuning.asset");
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<KartTuning>();
                AssetDatabase.CreateAsset(tuning, Root + "/Settings/ArcadeKartTuning.asset");
            }
            var friction = new PhysicsMaterial("Kart_WallSlide")
            {
                dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(friction, Root + "/Settings/Kart_WallSlide.physicMaterial");

            var kart = BuildKart(tuning, friction, yellow, black, window, accent, red, headlight, white);
            PrefabUtility.SaveAsPrefabAssetAndConnect(kart.gameObject, Root + "/Prefabs/BlockKart.prefab", InteractionMode.AutomatedAction);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 400f;
            camera.fieldOfView = tuning.cameraBaseFieldOfView;
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<KartChaseCamera>();
            follow.Configure(kart);
            follow.SnapToTarget();

            var hud = new GameObject("Practice HUD").AddComponent<PracticeHud>();
            hud.Configure(kart);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save the generated scene.");

            // Put the playable scene first, retaining any scenes already in the project.
            var previous = EditorBuildSettings.scenes;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>
            { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var old in previous) if (old.path != ScenePath) list.Add(old);
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = kart.gameObject;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, new Vector3(125f, 20f, 205f)), false);
            Debug.Log("[NeonRush] Stage 1 training ground ready: " + ScenePath);
            return ScenePath;
        }

        private static void Sample(float distance, out Vector3 center, out Vector3 tangent)
        {
            distance = Mathf.Repeat(distance, Length);
            float arc = Mathf.PI * Radius;
            if (distance < Straight)
            {
                center = new Vector3(Radius, 0f, -Straight / 2f + distance);
                tangent = Vector3.forward;
            }
            else if (distance < Straight + arc)
            {
                float angle = (distance - Straight) / Radius;
                center = new Vector3(Radius * Mathf.Cos(angle), 0f, Straight / 2f + Radius * Mathf.Sin(angle));
                tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }
            else if (distance < Straight * 2f + arc)
            {
                center = new Vector3(-Radius, 0f, Straight / 2f - (distance - Straight - arc));
                tangent = Vector3.back;
            }
            else
            {
                float angle = Mathf.PI + (distance - Straight * 2f - arc) / Radius;
                center = new Vector3(Radius * Mathf.Cos(angle), 0f, -Straight / 2f + Radius * Mathf.Sin(angle));
                tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }
        }

        private static void BuildRoad(Transform parent, Material surface)
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                Sample(Length * i / Segments, out var point, out var forward);
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                vertices[i * 2] = point - right * HalfWidth;
                vertices[i * 2 + 1] = point + right * HalfWidth;
                uv[i * 2] = new Vector2(0f, i);
                uv[i * 2 + 1] = new Vector2(1f, i);
                if (i == Segments) continue;
                int index = i * 2;
                int tri = i * 6;
                triangles[tri] = index;
                triangles[tri + 1] = index + 2;
                triangles[tri + 2] = index + 1;
                triangles[tri + 3] = index + 1;
                triangles[tri + 4] = index + 2;
                triangles[tri + 5] = index + 3;
            }
            var mesh = new Mesh { name = "Training_Oval_Road", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Root + "/Meshes/TrainingRoad.asset");
            var roadObject = new GameObject("Road Surface");
            roadObject.transform.SetParent(parent, false);
            roadObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            roadObject.AddComponent<MeshRenderer>().sharedMaterial = surface;
            roadObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void BuildTrackEdges(Transform parent, Material cyan, Material white, Material barrier, Material glow)
        {
            var edges = new GameObject("Curbs and Safety Barriers").transform;
            edges.SetParent(parent, false);
            float spacing = Length / Segments;
            for (int i = 0; i < Segments; i++)
            {
                Sample((i + 0.5f) * spacing, out var point, out var forward);
                var rotation = Quaternion.LookRotation(forward);
                var right = Vector3.Cross(Vector3.up, forward);
                foreach (int side in new[] { -1, 1 })
                {
                    var curb = Cube("Curb", edges, point + right * (HalfWidth - 0.32f) * side + Vector3.up * 0.035f,
                        new Vector3(0.64f, 0.07f, spacing + 0.13f), i % 4 < 2 ? cyan : white);
                    curb.transform.rotation = rotation;
                    var wall = Cube("Safety Barrier", edges, point + right * (HalfWidth + 0.45f) * side + Vector3.up * 0.53f,
                        new Vector3(0.55f, 1.06f, spacing + 0.14f), barrier, true);
                    wall.transform.rotation = rotation;
                    var strip = Cube("Barrier Light", edges, wall.transform.position + Vector3.up * 0.46f,
                        new Vector3(0.58f, 0.07f, spacing + 0.16f), glow);
                    strip.transform.rotation = rotation;
                }
            }
        }

        private static void BuildMarkings(Transform parent, Material white, Material dark, Material yellow)
        {
            var markings = new GameObject("Road Markings").transform;
            markings.SetParent(parent, false);
            for (float distance = 0f; distance < Length; distance += 8f)
            {
                Sample(distance, out var point, out var direction);
                var dash = Cube("Center Dash", markings, point + Vector3.up * 0.014f, new Vector3(0.16f, 0.015f, 2.2f), white);
                dash.transform.rotation = Quaternion.LookRotation(direction);
            }
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 16; column++)
                    Cube("Start Checker", markings, new Vector3(Radius - 7.5f + column, 0.025f, -35f + row * 0.65f),
                        new Vector3(0.97f, 0.025f, 0.64f), (column + row) % 2 == 0 ? white : dark);
            foreach (float distance in new[] { 32f, 114f, 150f, 228f, 310f, 347f })
            {
                Sample(distance, out var point, out var direction);
                var arrow = new GameObject("Direction Arrow").transform;
                arrow.SetParent(markings, false);
                arrow.position = point + Vector3.up * 0.027f;
                arrow.rotation = Quaternion.LookRotation(direction);
                var left = Cube("Arrow Left", arrow, new Vector3(-0.6f, 0f, 0f), new Vector3(0.2f, 0.02f, 1.65f), yellow);
                left.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                var right = Cube("Arrow Right", arrow, new Vector3(0.6f, 0f, 0f), new Vector3(0.2f, 0.02f, 1.65f), yellow);
                right.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
            }
        }

        private static void BuildScenery(Transform parent, Material building, Material dark, Material accent, Material white, Material yellow)
        {
            var scenery = new GameObject("Geometric Scenery").transform;
            scenery.SetParent(parent, false);
            Cube("Infield Plinth", scenery, new Vector3(0f, 0.1f, 0f), new Vector3(30f, 0.4f, 70f), dark);
            for (int i = 0; i < 9; i++)
            {
                float height = 3f + (i * 7 % 11);
                var block = Cube("Infield Block", scenery, new Vector3((i % 3 - 1) * 8f, height / 2f, (i / 3 - 1) * 17f),
                    new Vector3(5f, height, 7f), building);
                Cube("Block Cap", block.transform, new Vector3(0f, 0.51f, 0f), new Vector3(1.03f, 0.02f, 1.03f), accent);
            }
            for (int i = 0; i < 28; i++)
            {
                float angle = i / 28f * Mathf.PI * 2f;
                float height = 8f + (i * 13 % 25);
                var pos = new Vector3(Mathf.Cos(angle) * 104f, height / 2f - 0.08f, Mathf.Sin(angle) * 135f);
                Cube("Skyline Block", scenery, pos, new Vector3(8f + i % 3 * 3f, height, 9f), building);
                Cube("Skyline Accent", scenery, pos + new Vector3(-3f, 0f, -4.52f), new Vector3(0.15f, height * 0.8f, 0.1f), accent);
            }
            // The opening gantry is behind the spawn, so the first view looks down an unobstructed straight.
            Cube("Gantry Left", scenery, new Vector3(Radius - 9.2f, 3f, -35f), new Vector3(1f, 6f, 1.3f), dark, true);
            Cube("Gantry Right", scenery, new Vector3(Radius + 9.2f, 3f, -35f), new Vector3(1f, 6f, 1.3f), dark, true);
            Cube("Gantry Header", scenery, new Vector3(Radius, 6.15f, -35f), new Vector3(19.4f, 1.5f, 1.3f), dark);
            Cube("Gantry Light", scenery, new Vector3(Radius, 5.39f, -34.31f), new Vector3(17f, 0.1f, 0.06f), accent);
            WorldText("START  /  NEONRUSH", scenery, new Vector3(Radius, 6.13f, -34.31f), Quaternion.Euler(0f, 180f, 0f), 0.22f, white.color);
            foreach (float z in new[] { -8f, 26f, 48f })
            {
                Cube("Distance Marker", scenery, new Vector3(Radius + 10.2f, 1.8f, z), new Vector3(0.45f, 3.6f, 0.5f), accent);
            }
        }

        private static ArcadeKartController BuildKart(KartTuning tuning, PhysicsMaterial friction, Material yellow,
            Material black, Material glass, Material accent, Material red, Material headlight, Material white)
        {
            var car = new GameObject("Player Kart");
            car.SetActive(false);
            car.transform.SetPositionAndRotation(new Vector3(Radius, 0.62f, -29f), Quaternion.identity);
            car.layer = 2; // Ignore Raycast: ground and camera casts must not hit the car itself.
            var collider = car.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.05f, 0f);
            collider.size = new Vector3(1.72f, 0.9f, 3.2f);
            collider.sharedMaterial = friction;
            car.AddComponent<Rigidbody>();
            var motor = car.AddComponent<ArcadeKartController>();
            car.AddComponent<KartKeyboardInput>();
            car.AddComponent<KartDriftEffects>().Configure(AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/TyreMarks.mat"));
            var plumeMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/BoostPlume.mat");
            if (plumeMaterial != null) car.AddComponent<KartBoostEffects>().Configure(plumeMaterial);
            var audioBank = AssetDatabase.LoadAssetAtPath<KartAudioBank>(KartAudioSetup.BankPath);
            if (audioBank != null) car.AddComponent<KartAudioController>().Configure(audioBank);
            var model = new GameObject("Body Visual").transform;
            model.SetParent(car.transform, false);
            Cube("Body", model, Vector3.zero, new Vector3(1.8f, 0.46f, 3.15f), yellow);
            Cube("Cabin", model, new Vector3(0f, 0.42f, -0.25f), new Vector3(1.12f, 0.48f, 1.2f), glass);
            Cube("Roof", model, new Vector3(0f, 0.68f, -0.29f), new Vector3(1.2f, 0.08f, 1.28f), yellow);
            Cube("Hood Stripe", model, new Vector3(0f, 0.239f, 1f), new Vector3(0.3f, 0.025f, 1.05f), accent);
            Cube("Front Bumper", model, new Vector3(0f, -0.09f, 1.62f), new Vector3(1.62f, 0.21f, 0.17f), black);
            Cube("Spoiler", model, new Vector3(0f, 0.44f, -1.48f), new Vector3(2f, 0.13f, 0.35f), black);
            var pivots = new Transform[2];
            for (int axle = 0; axle < 2; axle++)
            {
                float z = axle == 0 ? 1.04f : -1.03f;
                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? -0.96f : 0.96f;
                    var pivot = new GameObject(axle == 0 ? "Front Wheel Pivot" : "Rear Wheel Pivot").transform;
                    pivot.SetParent(car.transform, false);
                    pivot.localPosition = new Vector3(x, -0.2f, z);
                    if (axle == 0) pivots[side] = pivot;
                    var wheel = Primitive(PrimitiveType.Cylinder, "Wheel", pivot, Vector3.zero,
                        new Vector3(0.3f, 0.16f, 0.3f), black);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    var hub = Primitive(PrimitiveType.Cylinder, "Hub", pivot, new Vector3(Mathf.Sign(x) * 0.165f, 0f, 0f),
                        new Vector3(0.15f, 0.012f, 0.15f), white);
                    hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            foreach (float x in new[] { -0.58f, 0.58f })
            {
                Cube("Headlight", model, new Vector3(x, 0.05f, 1.59f), new Vector3(0.36f, 0.15f, 0.045f), headlight);
                Cube("Tail Light", model, new Vector3(x, 0.04f, -1.59f), new Vector3(0.38f, 0.14f, 0.045f), red);
            }
            foreach (Transform child in car.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
            motor.Configure(tuning, model, pivots);
            car.SetActive(true);
            return motor;
        }

        private static Material Material(string name, Color color, float emission = 0f)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The Universal Render Pipeline/Lit shader was not found.");
            var material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Smoothness", 0.26f);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = false)
            => Primitive(PrimitiveType.Cube, name, parent, position, scale, material, solid);

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position,
            Vector3 scale, Material material, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void WorldText(string value, Transform parent, Vector3 position, Quaternion rotation, float size, Color color)
        {
            var go = new GameObject(value);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var text = go.AddComponent<TextMesh>();
            text.text = value;
            text.fontSize = 80;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
        }
    }
}
