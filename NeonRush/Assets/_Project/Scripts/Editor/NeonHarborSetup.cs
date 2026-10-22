using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LoongRacing.Editor
{
    public static class NeonHarborSetup
    {
        public const string ScenePath="Assets/_Project/Scenes/NeonHarbor.unity";
        private const string Art="Assets/_Project/Art/NeonHarbor/";
        private const string PrefabPath="Assets/_Project/Prefabs/VelaRacer.prefab";
        private static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();

        [MenuItem("NeonRush/Open Neon Harbor")]
        public static void Open()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath);else Build();
        }

        public static string Build()
        {
            if(EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Stop Play and save the current scene first.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("Neon scene exists. Open it to preserve edits.");
            foreach(string folder in new[]{"Materials","Meshes","Probes"})Directory.CreateDirectory(Art+folder);
            AssetDatabase.Refresh();
            PrepareAssets();
            BuildVehiclePrefab();
            if(!AssetDatabase.CopyAsset(HarborCircuitBuilder.ScenePath,ScenePath))throw new IOException("Could not copy the original scene.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var original=UnityEngine.Object.FindFirstObjectByType<ArcadeKartController>();
            var previousProgress=original.GetComponent<KartRouteProgress>();
            const string routePath="Assets/_Project/Data/NeonHarborRoute.asset";
            if(!File.Exists(routePath))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(previousProgress.Route),routePath);
            var route=AssetDatabase.LoadAssetAtPath<TrackRoute>(routePath);
            var routeSerialized=new SerializedObject(route);routeSerialized.FindProperty("displayName").stringValue="霓虹港湾 · 夜航";routeSerialized.ApplyModifiedPropertiesWithoutUndo();
            var vehicle=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            vehicle.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);
            var kart=vehicle.GetComponent<ArcadeKartController>();
            var progress=vehicle.AddComponent<KartRouteProgress>();progress.Configure(route);progress.ConfigureStart(-6f);
            vehicle.AddComponent<KartRaceSession>();
            foreach(var gate in UnityEngine.Object.FindObjectsByType<CheckpointGateView>(FindObjectsSortMode.None))
            {var so=new SerializedObject(gate);so.FindProperty("progress").objectReferenceValue=progress;so.ApplyModifiedPropertiesWithoutUndo();}
            var hud=UnityEngine.Object.FindFirstObjectByType<PracticeHud>();hud.Configure(kart);
            var chase=UnityEngine.Object.FindFirstObjectByType<KartChaseCamera>();chase.Configure(kart);chase.ConfigureFraming(2.35f,6.6f,.40f);chase.SnapToTarget();
            UnityEngine.Object.DestroyImmediate(original.gameObject);
            StyleExistingGeometry();
            BuildCity(route);
            BuildSample(route);
            ConfigureLighting();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            var scenes=new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(ScenePath,true)};
            foreach(var old in EditorBuildSettings.scenes)if(old.path!=ScenePath)scenes.Add(old);
            EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();Selection.activeGameObject=vehicle;
            Debug.Log("[Neon Harbor] Vela coupe and 120m art sample ready.");
            return ScenePath;
        }

        private static void PrepareAssets()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Art+"Textures"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=4;
                if(path.Contains("Normal")){importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;}
                else if(path.Contains("MetalSmooth"))importer.sRGBTexture=false;
                importer.SaveAndReimport();
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Model",new[]{Art+"Models"}))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
            }
            materials.Clear();
            Make("Vela_Paint",new Color(1f,.66f,.02f),.52f,.76f);
            materials["Vela_Paint"].SetTexture("_BaseMap",Texture("VelaPaint_Albedo"));
            materials["Vela_Paint"].color=new Color(1f,1.35f,1f);
            Make("Vela_Trim",new Color(.018f,.025f,.045f),.35f,.55f);
            Make("Vela_Metal",new Color(.40f,.48f,.56f),.90f,.72f);
            Make("Vela_Rubber",new Color(.018f,.023f,.03f),0f,.20f);
            Make("Vela_Glass",new Color(.025f,.055f,.10f),.70f,.91f);
            Make("Vela_Tail",new Color(.07f,.001f,.003f),0f,.4f);
            materials["Vela_Tail"].SetColor("_EmissionColor",new Color(1f,.002f,.04f)*1.1f);
            materials["Vela_Tail"].globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;materials["Vela_Tail"].EnableKeyword("_EMISSION");
            Make("Vela_Head",new Color(.55f,.82f,1f),.1f,.7f,3.5f);
            Make("Neon_Cyan",new Color(.015f,.64f,1f),.25f,.6f,2.1f);
            Make("Neon_Pink",new Color(1f,.018f,.28f),.25f,.6f,2.1f);
            Make("Neon_Concrete",new Color(.035f,.045f,.075f),.25f,.55f);
            Make("Neon_Tower",new Color(.032f,.045f,.085f),.35f,.62f);
            Make("Neon_Poster",Color.white,.05f,.5f,1.3f);
            materials["Neon_Poster"].SetTexture("_BaseMap",Texture("NightRun_Poster"));
            materials["Neon_Poster"].SetTexture("_EmissionMap",Texture("NightRun_Poster"));
            Make("WetRoad",new Color(.6f,.6f,.7f),.06f,.88f);
            var road=materials["WetRoad"];
            road.SetTexture("_BaseMap",Texture("WetRoad_Albedo"));road.SetTexture("_MetallicGlossMap",Texture("WetRoad_MetalSmooth"));road.EnableKeyword("_METALLICSPECGLOSSMAP");
            road.SetTexture("_BumpMap",Texture("WetRoad_Normal"));road.SetFloat("_BumpScale",.025f);road.EnableKeyword("_NORMALMAP");
            road.SetTextureScale("_BaseMap",new Vector2(3.2f,1f));
            road.SetTextureScale("_MetallicGlossMap",new Vector2(3.2f,1f));road.SetTextureScale("_BumpMap",new Vector2(3.2f,1f));
            foreach(var material in materials.Values)EditorUtility.SetDirty(material);
        }

        private static Material Make(string name,Color color,float metal,float smooth,float emission=0f)
        {
            string path=Art+"Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.name=name;m.color=color;m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;
            if(emission>0f){m.SetColor("_EmissionColor",color*emission);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.EnableKeyword("_EMISSION");}
            materials[name]=m;return m;
        }
        private static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/"+name+".png");
        private static Transform Find(Transform parent,string name)
        {foreach(var t in parent.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;throw new InvalidOperationException("Missing model part: "+name);}
        private static void Remap(Transform root)
        {
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mapped=r.sharedMaterials;
                for(int i=0;i<mapped.Length;i++)if(mapped[i]!=null && materials.TryGetValue(mapped[i].name,out var m))mapped[i]=m;
                r.sharedMaterials=mapped;
            }
        }

        private static void BuildVehiclePrefab()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/BlockKart.prefab");
            try
            {
                root.name="Vela Racer";root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                for(int i=root.transform.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                var visual=new GameObject("Body Visual").transform;visual.SetParent(root.transform,false);
                var sourceRoot=new GameObject("Source Conversion").transform;sourceRoot.SetParent(root.transform,false);sourceRoot.localRotation=Quaternion.Euler(0,180,0);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Models/VelaCoupe.fbx"));
                model.transform.SetParent(sourceRoot,false);Remap(model.transform);
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float scale=3.2f/bounds.size.z;sourceRoot.localScale=Vector3.one*scale;sourceRoot.localPosition=Vector3.down*.5f;
                var body=Combine(Find(model.transform,"Body"),visual,"VelaBody",out Material[] bodyMats);
                var steering=new List<Transform>();var spins=new List<Transform>();
                foreach(string name in new[]{"Wheel_FL","Wheel_FR","Wheel_RL","Wheel_RR"})
                {
                    var source=Find(model.transform,name);
                    var pivot=new GameObject(name+" Steering").transform;pivot.SetParent(visual,false);pivot.position=source.position;
                    if(name.StartsWith("Wheel_F"))steering.Add(pivot);
                    var spin=new GameObject(name+" Spin").transform;spin.SetParent(pivot,false);spins.Add(spin);
                    Combine(source,spin,"Vela"+name,out Material[] unused);
                }
                var jets=new List<Transform>();var contacts=new List<Transform>();
                foreach(string name in new[]{"Exhaust_L","Exhaust_R","RearContact_L","RearContact_R"})
                {
                    var anchor=new GameObject(name).transform;anchor.SetParent(visual,false);anchor.position=Find(model.transform,name).position;
                    if(name.StartsWith("Exhaust"))jets.Add(anchor);else contacts.Add(anchor);
                }
                int tail=-1;for(int i=0;i<bodyMats.Length;i++)if(bodyMats[i].name=="Vela_Tail")tail=i;
                var rig=root.GetComponent<KartVisualRig>()??root.AddComponent<KartVisualRig>();
                rig.Configure(spins.ToArray(),jets.ToArray(),contacts.ToArray(),body,tail,.39f*scale);
                var kart=root.GetComponent<ArcadeKartController>();kart.Configure(kart.Tuning,visual,steering.ToArray());
                var collider=root.GetComponent<BoxCollider>();collider.size=new Vector3(1.24f,.85f,3.1f);collider.center=new Vector3(0,-.075f,0);
                for(int i=0;i<2;i++)
                {
                    var head=new GameObject("Headlight beam").AddComponent<Light>();head.transform.SetParent(visual,false);
                    head.transform.localPosition=new Vector3(i==0?-.38f:.38f,-.08f,1.5f);head.type=LightType.Spot;
                    head.color=new Color(.58f,.77f,1f);head.intensity=.7f;head.range=24f;head.spotAngle=65f;head.shadows=LightShadows.None;
                }
                UnityEngine.Object.DestroyImmediate(sourceRoot.gameObject);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        private static Renderer Combine(Transform source,Transform target,string assetName,out Material[] resultMaterials)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null || !renderer.enabled)continue;
                var mesh=filter.sharedMesh;var shared=renderer.sharedMaterials;
                for(int i=0;i<mesh.subMeshCount;i++)
                {
                    Material mat=shared[Mathf.Min(i,shared.Length-1)];if(!groups.ContainsKey(mat))groups[mat]=new List<CombineInstance>();
                    groups[mat].Add(new CombineInstance{mesh=mesh,subMeshIndex=i,transform=target.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
            }
            var submeshes=new List<Mesh>();var final=new List<CombineInstance>();var mats=new List<Material>();
            foreach(var pair in groups)
            {
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);
                submeshes.Add(mesh);final.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});mats.Add(pair.Key);
            }
            var combined=new Mesh{name=assetName,indexFormat=IndexFormat.UInt32};combined.CombineMeshes(final.ToArray(),false,true);combined.RecalculateBounds();
            string path=Art+"Meshes/"+assetName+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null)AssetDatabase.CreateAsset(combined,path);else{EditorUtility.CopySerialized(combined,existing);UnityEngine.Object.DestroyImmediate(combined);combined=existing;}
            foreach(var mesh in submeshes)UnityEngine.Object.DestroyImmediate(mesh);
            var go=new GameObject(assetName);go.transform.SetParent(target,false);go.AddComponent<MeshFilter>().sharedMesh=combined;
            var output=go.AddComponent<MeshRenderer>();resultMaterials=mats.ToArray();output.sharedMaterials=resultMaterials;
            return output;
        }

        private static void StyleExistingGeometry()
        {
            foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if(r.GetComponentInParent<ArcadeKartController>()!=null)continue;
                var old=r.sharedMaterial;if(old==null)continue;string n=old.name;
                if(n=="Road")r.sharedMaterial=materials["WetRoad"];
                else if(n=="Ground"||n=="Barrier_Graphite"||n=="Buildings")r.sharedMaterial=materials["Neon_Concrete"];
                else if(n=="Accent_Cyan"||n=="Curb_Cyan")r.sharedMaterial=materials["Neon_Cyan"];
                if(r.name=="Guide Light" && Mathf.RoundToInt(r.transform.position.z/12f)%2==0)r.sharedMaterial=materials["Neon_Pink"];
            }
            foreach(string name in new[]{"Continuous Guardrails","Route Markings"})
            {
                var group=GameObject.Find(name);if(group==null)continue;
                var original=group.GetComponentsInChildren<MeshRenderer>();
                Combine(group.transform,group.transform,"Static"+name.Replace(" ",""),out Material[] unused);
                foreach(var r in original)if(r.GetComponent<MeshFilter>()!=null)r.enabled=false;
            }
            var skyline=GameObject.Find("Harbor Greybox Skyline");if(skyline!=null)skyline.SetActive(false);
        }

        private static GameObject Module(string model,Transform parent,Vector3 position,Quaternion rotation,Vector3 scale)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Models/"+model+".fbx"));
            // Baked wrapper supplies Unity's +Y up / +Z forward; source FBX keeps its conversion rotation.
            var wrapper=new GameObject(model);wrapper.transform.SetParent(parent,false);wrapper.transform.SetPositionAndRotation(position,rotation*Quaternion.Euler(0,180,0));
            wrapper.transform.localScale=scale;go.transform.SetParent(wrapper.transform,false);Remap(go.transform);
            return wrapper;
        }

        private static void BuildCity(TrackRoute route)
        {
            var city=new GameObject("Modular Neon Skyline").transform;var rng=new System.Random(61021);
            for(int i=0;i<180;i++)
            {
                Vector3 p=new Vector3(-100f+(float)rng.NextDouble()*390f,-12f,-300f+(float)rng.NextDouble()*530f);
                if(route.FindNearest(new Vector3(p.x,0,p.z),out Vector3 nearest,out Vector3 f,out float d)<28f)continue;
                float s=.6f+(float)rng.NextDouble()*.95f;
                Module(i%3==0?"Module_TowerB":"Module_TowerA",city,p,Quaternion.Euler(0,(i%4)*90f,0),new Vector3(s,s,s));
            }
            for(float distance=0;distance<route.Length;distance+=32f)
            {
                route.Sample(distance,out Vector3 p,out Vector3 f);
                Module("Module_Pier",city,p,Quaternion.LookRotation(Vector3.ProjectOnPlane(f,Vector3.up)),Vector3.one);
            }
            CombineStaticGroup(city,"StaticNeonSkyline");
        }

        private static void BuildSample(TrackRoute route)
        {
            var sample=new GameObject("Art Sample - Route 125 to 245 metres").transform;
            for(float d=125f;d<=245f;d+=10f)
            {
                route.Sample(d,out Vector3 p,out Vector3 f);f.y=0;f.Normalize();Vector3 side=Vector3.Cross(Vector3.up,f);
                if(Mathf.RoundToInt((d-125)/10)%3==0)Module("Module_Gate",sample,p,Quaternion.LookRotation(f),Vector3.one);
                foreach(int sign in new[]{-1,1})
                {
                    Module("Module_Rail",sample,p+side*sign*10.2f,Quaternion.LookRotation(f),Vector3.one);
                    if(Mathf.RoundToInt(d)%20==5)
                    {
                        var light=new GameObject("Neon road wash").AddComponent<Light>();light.transform.SetParent(sample,false);
                        light.transform.position=p+side*sign*7.8f+Vector3.up*2.5f;light.type=LightType.Point;
                        light.color=sign<0?new Color(1,.025f,.25f):new Color(.025f,.55f,1);light.range=14;light.intensity=2.8f;light.shadows=LightShadows.None;
                    }
                }
            }
            foreach(float d in new[]{145f,205f,240f})
            {
                route.Sample(d,out Vector3 p,out Vector3 f);Vector3 side=Vector3.Cross(Vector3.up,f).normalized;
                Module("Module_Billboard",sample,p+side*17f+Vector3.up*9f,Quaternion.LookRotation(side),Vector3.one);
            }
            for(float d=170f;d<240f;d+=10f)
            {
                route.Sample(d,out Vector3 p,out Vector3 f);Vector3 side=Vector3.Cross(Vector3.up,f).normalized;
                var board=new GameObject("Corner direction arrows");board.transform.SetParent(sample,false);board.transform.SetPositionAndRotation(p+side*10.1f+Vector3.up*2.4f,Quaternion.LookRotation(side));
                var text=board.AddComponent<TextMesh>();text.text="<<<";text.fontSize=80;text.characterSize=.10f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(1,.12f,.5f);
            }
            CombineStaticGroup(sample,"StaticArtSample");
        }

        private static void CombineStaticGroup(Transform group,string name)
        {
            var previous=group.GetComponentsInChildren<MeshRenderer>();
            Combine(group,group,name,out Material[] unused);
            foreach(var r in previous)if(r.GetComponent<MeshFilter>()!=null)r.enabled=false;
        }

        private static void ConfigureLighting()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.24f,.28f,.48f);
            RenderSettings.ambientEquatorColor=new Color(.18f,.14f,.30f);RenderSettings.ambientGroundColor=new Color(.07f,.05f,.12f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.045f,.035f,.11f);RenderSettings.fogStartDistance=180;RenderSettings.fogEndDistance=480;
            var sky=new Material(Shader.Find("NeonRush/Neon Sky")){name="Neon Horizon"};AssetDatabase.CreateAsset(sky,Art+"Materials/NeonSky.mat");RenderSettings.skybox=sky;
            var sun=GameObject.Find("Sun").GetComponent<Light>();sun.color=new Color(.56f,.64f,1f);sun.intensity=.9f;sun.transform.rotation=Quaternion.Euler(30,145,0);
            var rim=new GameObject("Vehicle warm fill").AddComponent<Light>();rim.type=LightType.Directional;rim.color=new Color(1f,.95f,.78f);rim.intensity=.9f;
            rim.transform.rotation=Quaternion.Euler(35,0,0);rim.cullingMask=1<<2;rim.shadows=LightShadows.None;
            new GameObject("Stable Night Ambient").AddComponent<NeonEnvironment>();
            var camera=Camera.main;camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=RenderSettings.fogColor;camera.allowHDR=true;camera.farClipPlane=600;
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            var volume=new GameObject("Night Grade").AddComponent<Volume>();volume.isGlobal=true;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Art+"Materials/NeonGrade.asset");volume.sharedProfile=profile;
            var bloom=profile.Add<Bloom>();bloom.intensity.Override(.36f);bloom.threshold.Override(1.1f);bloom.scatter.Override(.6f);
            var grade=profile.Add<ColorAdjustments>();grade.postExposure.Override(.35f);grade.contrast.Override(12);grade.saturation.Override(6);
            var tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
            var vignette=profile.Add<Vignette>();vignette.intensity.Override(.14f);vignette.smoothness.Override(.4f);
            foreach(var component in profile.components){AssetDatabase.AddObjectToAsset(component,profile);EditorUtility.SetDirty(component);}
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            DynamicGI.UpdateEnvironment();
            foreach(Vector3 position in new[]{new Vector3(0,2,-20),new Vector3(20,3,90),new Vector3(80,5,-80)})
            {
                var probe=new GameObject("Local Wet Road Reflection").AddComponent<ReflectionProbe>();probe.transform.position=position;
                probe.mode=ReflectionProbeMode.Baked;probe.resolution=256;probe.size=new Vector3(180,80,210);probe.boxProjection=true;probe.blendDistance=35f;
                probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.cullingMask=~(1<<2);probe.farClipPlane=350;probe.hdr=true;
                string path=Art+"Probes/Probe"+position.z.ToString("0")+".exr";
                if(!Lightmapping.BakeReflectionProbe(probe,path))throw new InvalidOperationException("Could not bake reflection probe.");
            }
        }
    }
}
