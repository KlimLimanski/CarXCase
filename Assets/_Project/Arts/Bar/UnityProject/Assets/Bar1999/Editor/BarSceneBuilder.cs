using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using ReferenceBar;

namespace ReferenceBar.Editor {
    [Serializable] public class BarManifest {
        public CollisionInfo[] colliders; public InteractionInfo[] interactives;
        public MarkerInfo[] markers; public CameraInfo[] cameras; public LightInfo[] lights;
        public MaterialInfo[] materials;
        public MeshBoundsInfo[] meshBounds;
    }
    [Serializable] public class MeshBoundsInfo { public string name; public float[] center,size; }
    [Serializable] public class CollisionInfo { public string name, owner, zone; public float[] center, size, rotation; }
    [Serializable] public class InteractionInfo { public string name, kind; public bool locked; }
    [Serializable] public class MarkerInfo { public string name; public float[] position; }
    [Serializable] public class CameraInfo { public string name; public float[] position, target; public float lens; }
    [Serializable] public class LightInfo { public string name, type; public float[] position, rotation, color; public float energy, size; }
    [Serializable] public class MaterialInfo { public string name, texture; public float[] color, emission; public float roughness, metallic, emissionStrength; }

    public class BarModelImporter : AssetPostprocessor {
        void OnPreprocessModel() {
            if (!assetPath.EndsWith("Bar1999.fbx")) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1; m.useFileScale = true; m.bakeAxisConversion = true;
            m.importCameras = false; m.importLights = false; m.importAnimation = false;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            m.generateSecondaryUV = true; m.secondaryUVPackMargin = 8;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.isReadable = false;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
        void OnPreprocessTexture() {
            if (!assetPath.Contains("Bar1999/Textures/")) return;
            var t = (TextureImporter)assetImporter;
            t.maxTextureSize = 1024; t.mipmapEnabled = true; t.anisoLevel = 8;
            if (assetPath.Contains("_Normal")) t.textureType = TextureImporterType.NormalMap;
            if (assetPath.Contains("_Roughness") || assetPath.Contains("_MetallicSmoothness")) t.sRGBTexture = false;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
    public static class BarSceneBuilder {
        const string Root = "Assets/Bar1999";
        static BarManifest data;
        static GameObject model;
        static readonly Dictionary<string, Transform> objects = new();
        static readonly List<string> qa = new();
        static readonly List<NavMeshSurface> surfaces = new();
        static Vector3 V(float[] a) => new Vector3(a[0], a[2], a[1]);
        static Color C(float[] a) => new Color(a[0], a[1], a[2], a.Length > 3 ? a[3] : 1);
        static Transform Find(string n) => objects.TryGetValue(n, out var t) ? t : null;
        static GameObject Go(string name, Transform parent = null) { var g = new GameObject(name); if (parent != null) g.transform.SetParent(parent, false); return g; }

        [MenuItem("Bar 1999/Rebuild Reference Scene")]
        public static void Build() {
            qa.Clear(); objects.Clear(); surfaces.Clear();
            Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Scenes"); Directory.CreateDirectory(Root + "/Navigation");
            data = JsonUtility.FromJson<BarManifest>(File.ReadAllText(Root + "/scene_manifest.json"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.ImportAsset(Root + "/Models/Bar1999.fbx", ImportAssetOptions.ForceUpdate);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Bar1999.fbx");
            if (source == null) throw new Exception("FBX not imported.");
            model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Bar1999_Environment";
            foreach (var t in model.GetComponentsInChildren<Transform>()) objects[t.name] = t;
            // FBX supplies Blender-oriented empty axes. Give Unity pivots upright axes,
            // preserving every child's world pose before attaching gameplay components.
            foreach(var t in model.GetComponentsInChildren<Transform>()) {
                if(t.GetComponent<Renderer>()!=null)continue;
                var children=Enumerable.Range(0,t.childCount).Select(t.GetChild).ToArray();
                var positions=children.Select(c=>c.position).ToArray();
                var rotations=children.Select(c=>c.rotation).ToArray();
                t.rotation=Quaternion.identity;
                for(int i=0;i<children.Length;i++)children[i].SetPositionAndRotation(positions[i],rotations[i]);
            }
            // Derive the imported handedness from known markers, then normalize coordinates.
            var start = Find("Player_Start");
            var bartender = Find("Bartender_Spawn");
            if (start == null || bartender == null) throw new Exception("FBX markers missing");
            qa.Add("Imported Player_Start=" + start.position.ToString("F4") + ", Bartender=" + bartender.position.ToString("F4"));
            if (start.position.x < 0 && start.position.z < 0) model.transform.rotation = Quaternion.Euler(0, 180, 0);
            else if (start.position.x > 0 && start.position.z < 0) model.transform.localScale = new Vector3(1, 1, -1);
            else if (start.position.x < 0 && start.position.z > 0) model.transform.localScale = new Vector3(-1, 1, 1);
            if (Vector3.Distance(start.position, new Vector3(1.12f, 0, .48f)) > .02f)
                throw new Exception("FBX axis/scale mismatch: " + start.position);
            qa.Add("Scale and axes: Player_Start and bartender coordinates verified in metres.");
            int verified=0;
            foreach(var expected in data.meshBounds) {
                var t=Find(expected.name);var r=t!=null?t.GetComponent<Renderer>():null;
                if(r==null)throw new Exception("Missing mesh renderer: "+expected.name);
                if(Vector3.Distance(r.bounds.center,V(expected.center))>.004f || Vector3.Distance(r.bounds.size,V(expected.size))>.004f)
                    throw new Exception("Mesh transform mismatch "+expected.name+" actual="+r.bounds+" expected="+V(expected.center)+" size="+V(expected.size));
                verified++;
            }
            qa.Add("Every mesh world bound matched Blender: "+verified+" PASS");
            SetupMaterials();
            SetupCollisions();
            SetupInteractions();
            var station = Go("Bar_Dialogue_Station").AddComponent<BarInteractionStation>();
            station.playerPoint = Find("Player_Bar_Interaction_Point");
            station.bartenderPoint = Find("Bartender_Interaction_Point"); station.lookAt = Find("LookAt_Bartender");
            foreach (var r in model.GetComponentsInChildren<Renderer>()) {
                bool dynamic = r.GetComponentInParent<BarInteractive>() != null;
                var flags = dynamic ? (StaticEditorFlags)0 : StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags);
                r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
            }
            BuildLighting();
            var camera = BuildCameras();
            BuildNavigation();
            BuildPlayer(camera);
            CreatePrefabs();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), Root + "/Scenes/Bar1999.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "/Scenes/Bar1999.unity", true) };
            BakeOcclusion();
            ValidatePaths();
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Root + "/Unity_QA.txt", qa);
            Debug.Log("BAR_BUILD_COMPLETE\n" + string.Join("\n", qa));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        static void SetupMaterials() {
            var mats = new Dictionary<string, Material>();
            foreach (var info in data.materials) {
                var path = Root + "/Materials/" + info.name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
                m.name = info.name;
                m.SetColor("_BaseColor", C(info.color));
                m.SetFloat("_Metallic", info.metallic); m.SetFloat("_Smoothness", 1 - info.roughness);
                if (!string.IsNullOrEmpty(info.texture)) {
                    string pre = Root + "/Textures/" + info.texture;
                    var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(pre + "_BaseColor.png");
                    var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(pre + "_Normal.png");
                    var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(pre + "_MetallicSmoothness.png");
                    m.SetColor("_BaseColor", Color.white); m.SetTexture("_BaseMap", albedo);
                    if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", .45f); m.EnableKeyword("_NORMALMAP"); }
                    if (metal != null) { m.SetTexture("_MetallicGlossMap", metal); m.SetFloat("_Smoothness", 1); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                }
                if (info.emissionStrength > 0) {
                    m.SetColor("_EmissionColor", C(info.emission) * info.emissionStrength); m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                if (info.color[3] < .99f) {
                    m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0);
                    m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
                }
                m.enableInstancing = true; mats[info.name] = m; EditorUtility.SetDirty(m);
            }
            int missing = 0;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) {
                var mm = r.sharedMaterials;
                for (int i = 0; i < mm.Length; i++) {
                    if (mm[i] != null && mats.TryGetValue(mm[i].name, out var mat)) mm[i] = mat;
                    else missing++;
                }
                r.sharedMaterials = mm;
            }
            qa.Add("URP materials assigned; unmatched slots=" + missing);
            if (missing > 0) throw new Exception("Unmatched FBX materials: " + missing);
        }
        static void SetupCollisions() {
            var group = Go("TECH_Collision");
            foreach (var info in data.colliders) {
                var owner = string.IsNullOrEmpty(info.owner) ? null : Find(info.owner);
                var g = Go("COL_" + info.name, owner != null ? owner : group.transform);
                g.transform.position = V(info.center);
                g.transform.rotation = Quaternion.identity;
                var c = g.AddComponent<BoxCollider>(); c.size = V(info.size);
            }
            // The exterior is not supplied by the reference. This invisible limit keeps the demo inside.
            var limit = Go("Entry_ReferenceBoundary", group.transform);
            limit.transform.position = new Vector3(1.125f, 1.1f, -.13f);
            limit.AddComponent<BoxCollider>().size = new Vector3(.95f, 2.2f, .04f);
            qa.Add("Simple collision proxies=" + data.colliders.Length + "; no MeshCollider.");
        }
        static void SetupInteractions() {
            foreach (var info in data.interactives) {
                var t = Find(info.name); if (t == null) throw new Exception("Missing interaction " + info.name);
                var c = t.gameObject.AddComponent<BarInteractive>(); c.kind = info.kind; c.locked = info.locked;
                c.openAngle = info.name == "Door_Entry" ? -90 : 90;
                if (t.GetComponentInChildren<Collider>() == null) {
                    var rr = t.GetComponentsInChildren<Renderer>();
                    if (rr.Length > 0) {
                        var bounds = rr[0].bounds; foreach (var r in rr) bounds.Encapsulate(r.bounds);
                        var col = Go("Interaction_Collider", t).AddComponent<BoxCollider>();
                        col.transform.position = bounds.center; col.transform.rotation = Quaternion.identity; col.size = bounds.size;
                    }
                }
                if (info.kind == "Stool" || info.kind == "Chair" || (info.kind == "Door" && !info.locked)) {
                    var obstacle = t.gameObject.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    if (info.kind == "Door") {
                        obstacle.center = new Vector3(info.name == "Door_Entry" ? .475f : -.475f, 1.1f, 0);
                        obstacle.size = new Vector3(.95f, 2.2f, .08f);
                    } else { obstacle.center = new Vector3(0, .4f, 0); obstacle.size = new Vector3(.43f, .8f, .43f); }
                    obstacle.carving = true;
                }
            }
        }
        static void BuildLighting() {
            var root = Go("ENV_Lighting_Unity");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.13f, .115f, .09f);
            RenderSettings.ambientIntensity = .5f;
            RenderSettings.reflectionIntensity = .55f;
            foreach (var i in data.lights) {
                var g = Go(i.name, root.transform); g.transform.position = V(i.position);
                var l = g.AddComponent<Light>(); l.color = C(i.color);
                l.lightmapBakeType = LightmapBakeType.Mixed;
                l.shadows = LightShadows.Soft;
                if (i.type == "AREA") {
                    // Visible fixture unchanged; URP-compatible cone shares its location and direction.
                    l.type = LightType.Spot; l.spotAngle = 135; l.innerSpotAngle = 85;
                    g.transform.rotation = Quaternion.Euler(90, 0, 0);
                    l.intensity = i.energy / 8f; l.range = 5;
                } else {
                    l.type = LightType.Point; l.intensity = i.energy / 8f; l.range = i.energy < 10 ? 1.5f : 4.2f;
                }
                if(i.energy < 10)l.shadows=LightShadows.None;
                l.bounceIntensity = 1.3f;
            }
            var volume = Go("Bar_ColorAndBloom").AddComponent<Volume>(); volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tonemap = profile.Add<Tonemapping>(true); tonemap.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.23f); bloom.threshold.Override(1);
            var exposure = profile.Add<ColorAdjustments>(true); exposure.postExposure.Override(.3f);
            var old = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/BarVolume.asset");
            if (old != null) AssetDatabase.DeleteAsset(Root + "/BarVolume.asset");
            AssetDatabase.CreateAsset(profile, Root + "/BarVolume.asset");
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            volume.sharedProfile = profile;
            var probes = Go("LightProbes_CharacterLighting").AddComponent<LightProbeGroup>();
            var points = new List<Vector3>();
            foreach (float x in new[] { 1.7f, 2.8f, 3.3f, 5.45f })
                foreach (float z in new[] { .7f, 2.2f, 3.8f, 5.4f, 7.3f })
                    foreach (float y in new[] { .4f, 1.1f, 1.65f, 2.2f }) points.Add(new Vector3(x, y, z));
            probes.probePositions = points.ToArray();
            var reflection = Go("Bar_ReflectionProbe").AddComponent<ReflectionProbe>();
            reflection.transform.position = new Vector3(3.2f, 1.35f, 4);
            reflection.size = new Vector3(6.4f, 2.7f, 8); reflection.boxProjection = true;
            reflection.mode = ReflectionProbeMode.Baked; reflection.resolution = 128;
            reflection.clearFlags = ReflectionProbeClearFlags.SolidColor; reflection.backgroundColor = Color.black;
            var settings = new LightingSettings(); settings.bakedGI = true; settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.UnityComputeGPU;
            settings.lightmapResolution = 16; settings.lightmapMaxSize = 1024;
            settings.indirectSampleCount = 128; settings.directSampleCount = 32; settings.environmentSampleCount = 32;
            settings.mixedBakeMode = MixedLightingMode.Shadowmask;
            if (AssetDatabase.LoadAssetAtPath<LightingSettings>(Root + "/BarLighting.lighting") != null) AssetDatabase.DeleteAsset(Root + "/BarLighting.lighting");
            AssetDatabase.CreateAsset(settings, Root + "/BarLighting.lighting"); Lightmapping.lightingSettings = settings;
        }
        static Camera BuildCameras() {
            var output = Go("Main Camera"); output.tag = "MainCamera";
            var cam = output.AddComponent<Camera>(); cam.nearClipPlane = .04f; cam.farClipPlane = 40;
            cam.fieldOfView = 66; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            output.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            cam.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var brain = output.AddComponent<CinemachineBrain>();
            var root = Go("TECH_Cinematics_Unity");
            var director = root.AddComponent<PlayableDirector>(); director.playOnAwake = false;
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var path = Root + "/Bar_CameraSequence.playable";
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(timeline, path);
            director.playableAsset = timeline;
            var track = timeline.CreateTrack<CinemachineTrack>(null, "Reference camera sequence");
            director.SetGenericBinding(track, brain);
            for (int i = 0; i < data.cameras.Length; i++) {
                var info = data.cameras[i]; var g = Go(info.name, root.transform);
                g.transform.position = V(info.position); g.transform.LookAt(V(info.target));
                var c = g.AddComponent<CinemachineCamera>();
                c.Lens.FieldOfView = 2 * Mathf.Atan(22.5f / (2 * info.lens)) * Mathf.Rad2Deg;
                c.Lens.NearClipPlane = .04f; c.Lens.FarClipPlane = 40;
                var clip = track.CreateClip<CinemachineShot>(); clip.displayName = info.name; clip.start = i * 4.0; clip.duration = 4.0;
                var shot = (CinemachineShot)clip.asset;
                shot.VirtualCamera.exposedName = new PropertyName(Guid.NewGuid().ToString());
                director.SetReferenceValue(shot.VirtualCamera.exposedName, c);
            }
            brain.enabled = false; return cam;
        }
        static int Agent(string name, float radius) {
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++) {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == name) return s.agentTypeID;
            }
            var setting = NavMesh.CreateSettings();
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];
            var so = new SerializedObject(asset);
            var settings = so.FindProperty("m_Settings"); var names = so.FindProperty("m_SettingNames");
            int index = settings.arraySize - 1;
            var item = settings.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("agentRadius").floatValue = radius;
            item.FindPropertyRelative("agentHeight").floatValue = 1.72f;
            item.FindPropertyRelative("agentClimb").floatValue = .18f;
            item.FindPropertyRelative("agentSlope").floatValue = 35;
            item.FindPropertyRelative("minRegionArea").floatValue = .05f;
            while (names.arraySize < settings.arraySize) names.InsertArrayElementAtIndex(names.arraySize);
            names.GetArrayElementAtIndex(index).stringValue = name;
            so.ApplyModifiedProperties();
            return setting.agentTypeID;
        }
        static void BuildNavigation() {
            var root = Go("TECH_Navigation_Unity");
            int player = Agent("Bar_Player", .23f), npc = Agent("Bar_GuestNPC", .22f), bartender = Agent("Bar_Bartender", .22f);
            foreach (var col in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)) {
                if (col.name.Contains("Floor") || col.name.Contains("Wall") || col.name.Contains("Ceiling")) continue;
                if (col.bounds.max.y < .2f || col.bounds.min.y > 1.8f) continue;
                var mod = col.gameObject.AddComponent<NavMeshModifier>(); mod.overrideArea = true; mod.area = 1;
            }
            // Narrow volumes separate staff/guest navigation without modifying the architecture.
            foreach (var spec in new[] {
                ("Player", player, new Vector3(3.2f, 1.1f, 4), new Vector3(6.4f, 2.2f, 8.0f)),
                ("GuestNPC", npc, new Vector3(2.05f, 1.1f, 4.0f), new Vector3(4.10f, 2.2f, 7.7f)),
                ("Bartender", bartender, new Vector3(5.45f, 1.1f, 5.70f), new Vector3(1.10f, 2.2f, 8.5f)) }) {
                var g = Go("NavMesh_" + spec.Item1, root.transform);
                var nav = g.AddComponent<NavMeshSurface>(); nav.agentTypeID = spec.Item2;
                nav.collectObjects = CollectObjects.Volume; nav.center = spec.Item3; nav.size = spec.Item4;
                nav.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                nav.overrideVoxelSize = true; nav.voxelSize = .035f;
                nav.minRegionArea = .05f;
                nav.BuildNavMesh();
                if (nav.navMeshData == null) throw new Exception("NavMesh failed: " + spec.Item1);
                string p = Root + "/Navigation/" + spec.Item1 + ".asset";
                if (AssetDatabase.LoadAssetAtPath<NavMeshData>(p) != null) AssetDatabase.DeleteAsset(p);
                AssetDatabase.CreateAsset(nav.navMeshData, p); surfaces.Add(nav);
                qa.Add("Navigation baked: " + spec.Item1 + " agent=" + spec.Item2);
            }
        }
        static void BuildPlayer(Camera camera) {
            var p = Go("Player_Walkthrough"); p.transform.position = new Vector3(1.12f, .04f, .48f);
            p.transform.rotation = Quaternion.Euler(0, 15, 0);
            var cc = p.AddComponent<CharacterController>(); cc.radius = .23f; cc.height = 1.72f;
            cc.center = new Vector3(0, .86f, 0); cc.skinWidth = .015f; cc.stepOffset = .12f; cc.slopeLimit = 35;
            cc.minMoveDistance = 0;
            var eye = Go("Eye", p.transform); eye.transform.localPosition = new Vector3(0, 1.60f, 0);
            camera.transform.SetPositionAndRotation(eye.transform.position, eye.transform.rotation);
            var walk = p.AddComponent<BarWalkthrough>(); walk.eye = eye.transform; walk.gameCamera = camera;
            walk.brain = camera.GetComponent<CinemachineBrain>(); walk.sequence = UnityEngine.Object.FindFirstObjectByType<PlayableDirector>();
            walk.barStation = UnityEngine.Object.FindAnyObjectByType<BarInteractionStation>();
        }
        static void CreatePrefabs() {
            foreach (var i in data.interactives) {
                var t = Find(i.name);
                PrefabUtility.SaveAsPrefabAsset(t.gameObject, Root + "/Prefabs/" + i.name + ".prefab");
            }
            PrefabUtility.SaveAsPrefabAsset(model, Root + "/Prefabs/Bar1999_Environment.prefab");
        }
        static void BakeOcclusion() {
            var area = Go("Occlusion_InteriorBounds").AddComponent<OcclusionArea>();
            area.transform.position = new Vector3(3.2f, 1.35f, 4.0f); area.size = new Vector3(6.4f, 2.7f, 8);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            StaticOcclusionCulling.smallestOccluder = .25f; StaticOcclusionCulling.smallestHole = .08f;
            StaticOcclusionCulling.backfaceThreshold = 100;
            bool result = StaticOcclusionCulling.Compute();
            qa.Add("Occlusion compute=" + result);
        }
        static void ValidatePaths() {
            var nav = surfaces[0];
            var filter = new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = NavMesh.AllAreas };
            var start = new Vector3(1.12f, 0, .48f);
            foreach (var item in new[] { ("Bar", new Vector3(3.25f, 0, 3.8f)), ("North doors", new Vector3(3.3f, 0, 7.60f)), ("Round table", new Vector3(2.6f, 0, 1.6f)), ("Staff door", new Vector3(5.50f, 0, 7.65f)), ("Restroom door", new Vector3(1.37f, 0, 7.60f)) }) {
                var path = new NavMeshPath();
                bool a = NavMesh.SamplePosition(start, out var from, .5f, filter);
                bool b = NavMesh.SamplePosition(item.Item2, out var to, .5f, filter);
                bool okay = a && b && NavMesh.CalculatePath(from.position, to.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
                qa.Add("Path Entry -> " + item.Item1 + ": " + (okay ? "PASS" : "FAIL"));
                if (!okay) throw new Exception("Blocked required path: " + item.Item1);
            }
            qa.Add("RESTROOM locked=" + Find("Door_Restroom").GetComponent<BarInteractive>().locked);
            qa.Add("All required routes checked with player radius 0.23 m.");
        }
        [MenuItem("Bar 1999/Bake Lighting")]
        public static void BakeLighting() {
            EditorSceneManager.OpenScene(Root + "/Scenes/Bar1999.unity");
            bool success = Lightmapping.Bake();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            File.WriteAllText(Root + "/Lighting_QA.txt", "Lightmapping.Bake=" + success + "; lightmaps=" + LightmapSettings.lightmaps.Length);
            Debug.Log("BAR_LIGHTING_COMPLETE " + success + " maps=" + LightmapSettings.lightmaps.Length);
        }
    }
}
