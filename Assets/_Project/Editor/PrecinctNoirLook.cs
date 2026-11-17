using System;
using System.Linq;
using CarXCase.Precinct;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class PrecinctNoirLook
{
    const string ScenePath = "Assets/_Project/Scenes/02_Precinct.unity";
    const string Art = "Assets/_Project/ArtDirection/PrecinctNoir/";
    const string RootName = "Precinct_Greybox";

    [MenuItem("Tools/Precinct/Refresh precinct lighting and materials")]
    public static void ApplyAndSave()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open 02_Precinct in Edit Mode.");
        Apply(scene);
        BakeReflections(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Unable to save the precinct.");
    }

    internal static void Apply(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var root = roots.Single(g => g.name == RootName).transform;
        var lights = root.Find("Lighting");
        var office = root.Find("Office_Alexey");
        var board = office.Find("DeductionBoard").GetComponent<DeductionBoard>();
        var camera = roots.Single(g => g.CompareTag("MainCamera")).GetComponent<Camera>();

        var vinyl = Lit("Institutional_Vinyl", new Color(.90f,.90f,.86f), .61f, Texture("Vinyl_Flecked_Composition_Tile"));
        vinyl.SetTexture("_MetallicGlossMap", Texture("Vinyl_Wear_MetallicSmoothness", linear: true));
        vinyl.EnableKeyword("_METALLICSPECGLOSSMAP");
        vinyl.mainTextureScale = new Vector2(16, 24);
        var entranceVinyl = Lit("Vestibule_Vinyl", new Color(.90f,.90f,.86f), .55f, Texture("Vinyl_Flecked_Composition_Tile"));
        entranceVinyl.mainTextureScale = new Vector2(2.6f, 1.9f);
        var vinylNormal = Texture("Vinyl_Tile_Seams_Normal", normal: true);
        SetNormal(vinyl, vinylNormal, .59f);
        SetNormal(entranceVinyl, vinylNormal, .55f);
        var metal = Lit("Grey_Powder_Coated_Steel", new Color(.83f,.87f,.88f), .38f, Texture("Desk_Powder_Coat"));
        var filing = Lit("Olive_Powder_Coated_Steel", new Color(.70f,.76f,.69f), .34f, Texture("Desk_Powder_Coat"));
        var abs = Lit("Aged_Ivory_ABS", Color.white, .28f, Texture("Yellowed_ABS"));
        var crt = Lit("Smoked_CRT_Glass", Color.white, .66f, Texture("CRT_Smoked_Phosphor"));
        SetNormal(crt, Texture("CRT_Convex_Glass_Normal", normal: true), 1.0f);
        Emission(crt, new Color(.014f,.025f,.019f));
        var frosted = Lit("Mottled_Frosted_Glass", Color.white, .37f, Texture("Frosted_Safety_Glass"));
        Emission(frosted, new Color(.035f,.046f,.054f));
        var exteriorWindow = Lit("Exterior_Frosted_Glass", new Color(.71f,.78f,.84f), .26f, Texture("Frosted_Safety_Glass"));
        Emission(exteriorWindow, new Color(.091f,.135f,.19f));
        var entryHaze = Lit("Street_Haze_Through_Door", new Color(.18f,.24f,.29f), .14f);
        Emission(entryHaze, new Color(.15f,.23f,.31f));
        var woodMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/ArtDirection/ApartmentWood/Quiet_Oak_Albedo.png");
        var desk = Lit("Worn_Oak_Laminate", new Color(.71f,.61f,.47f), .34f, woodMap);
        var frontDesk = Lit("Walnut_Counter_Panels", new Color(.51f,.43f,.34f), .28f, woodMap);
        var counter = Lit("Grey_Laminate_Countertop", new Color(.83f,.81f,.72f), .34f, Texture("Desk_Powder_Coat"));
        var forms = Lit("Typed_Desk_Forms", new Color(.92f,.90f,.83f), .21f, Texture("Typed_Desk_Forms"));
        var upperWalls = Lit("Institutional_Sage_Paint", new Color(.76f,.80f,.73f), .15f, Texture("Walls_Painted_Plaster"));
        var lowerWalls = Lit("Scuffed_Green_Wainscot", new Color(.56f,.62f,.54f), .24f, Texture("Walls_Painted_Plaster"));
        var plasterNormal = Texture("Painted_Plaster_Roller_Normal", normal: true);
        SetNormal(upperWalls, plasterNormal, .55f);
        SetNormal(lowerWalls, plasterNormal, .38f);
        upperWalls.mainTextureScale = new Vector2(4, 2);
        lowerWalls.mainTextureScale = new Vector2(4, 1);

        var ceiling = Lit("Fibre_Ceiling_Tiles", new Color(.82f,.83f,.79f), .12f, Texture("Ceiling_Fibre_Panels_Albedo"));
        ceiling.mainTextureScale = new Vector2(12, 18);
        SetNormal(ceiling, Texture("Ceiling_Fibre_Panels_Normal", normal: true), .65f);
        var fixture = Lit("Fluorescent_Steel_Housing", new Color(.44f,.46f,.45f), .31f);
        var diffuser = Diffuser("Cool_Fluorescent_Prismatic_Diffuser", new Color(2.7f, 2.72f, 2.59f));
        var officeDiffuser = Diffuser("Office_Fluorescent_Diffuser", new Color(1.55f, 1.61f, 1.62f));
        var shade = Lit("Green_Desk_Lamp_Enamel", new Color(.18f,.38f,.29f), .45f);
        Emission(shade, new Color(.040f,.092f,.053f));
        var brass = Lit("Desk_Lamp_Dulled_Brass", new Color(.60f,.48f,.27f), .42f, null, .57f);

        var cork = Lit("Pressed_Cork", new Color(.88f,.85f,.78f), .17f, Texture("Pressed_Cork_Granules"));
        cork.mainTextureScale = new Vector2(3,2);
        var frame = Lit("Evidence_Board_Walnut_Frame", new Color(.86f,.83f,.78f), .34f, Texture("Board_Walnut_Edge"));
        frame.mainTextureScale = new Vector2(2,1);
        SetNormal(cork, Texture("Pressed_Cork_Granules_Normal", normal: true), .84f);
        SetNormal(frame, Texture("Board_Walnut_Edge_Normal", normal: true), .52f);

        var replacements = new (string source, Material target)[] {
            ("Linoleum", vinyl), ("Desk_Metal", metal), ("Cabinet_Grey", filing),
            ("CRT_Plastic", abs), ("CRT_Glow", crt), ("Glass_Frosted", frosted), ("Glass_Dark", frosted),
            ("Frosted_Day", exteriorWindow), ("Window_Night", exteriorWindow), ("Daylight", entryHaze),
            ("Desk_Wood", desk), ("FrontDesk_Wood", frontDesk), ("Counter_Top", counter),
            ("Wall_Green", upperWalls), ("Wall_Wainscot", lowerWalls), ("Ceiling_Tile", ceiling),
            ("Fluorescent", fixture), ("Lamp_Green", shade), ("Cork", cork), ("Board_Frame", frame)
        };
        var bySource = replacements.ToDictionary(pair => pair.source, pair => pair.target);
        var byTarget = replacements.Select(pair => pair.target).Distinct().ToDictionary(material => material.name);
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var material = renderer.sharedMaterial;
            if (renderer.name == "Papers")
            {
                renderer.sharedMaterial = forms;
                continue;
            }
            if (!material || (!bySource.TryGetValue(material.name, out var replacement)
                              && !byTarget.TryGetValue(material.name, out replacement))) continue;
            renderer.sharedMaterial = replacement;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }
        var arch = root.Find("Architecture");
        Paint(arch.Find("Vestibule_Floor"), entranceVinyl);
        FitFluorescentPanel(lights.Find("Office_Ceiling_Fixture"), fixture, officeDiffuser);
        FitFluorescentPanel(lights.Find("Captain_Ceiling_Fixture"), fixture, officeDiffuser);
        InstallVinylReflections(root, arch, camera, vinylNormal, vinyl.GetTexture("_MetallicGlossMap"));
        Paint(office.Find("Office_Lamp_Base"), brass);
        if (!office.Find("Office_Lamp_Stem"))
            PrecinctBuilder.Prim(office, PrimitiveType.Cylinder, "Office_Lamp_Stem", new Vector3(3.95f,.921f,22.6f),
                new Vector3(.025f,.109f,.025f), brass);

        SetBoardLook(board, frame);
        PlacePolice(root.Find("NPCs"), "Desk_Sergeant", 1, 1.81f, 0);
        PlacePolice(root.Find("NPCs"), "Cop_Standing", 2, 1.82f, 0);
        PlacePolice(root.Find("NPCs"), "Detective_Talking", 3, 1.78f, 0);
        PlacePolice(root.Find("NPCs"), "Cop_AtDesk_1", 4, 1.76f, 10);
        PlacePolice(root.Find("NPCs"), "Cop_AtDesk_2", 5, 1.82f, -14);

        var kickoff = lights.Find("Light_Daylight").GetComponent<Light>();
        kickoff.color = new Color(.66f,.80f,1f);
        kickoff.intensity = 1.70f;
        kickoff.range = 5.0f;
        kickoff.shadows = LightShadows.Soft;
        var dusk = lights.Find("Dusk_Light").GetComponent<Light>();
        dusk.enabled = false;
        foreach (var light in lights.GetComponentsInChildren<Light>().Where(l => l.name.StartsWith("Fluo_", StringComparison.Ordinal)))
        {
            bool lobby = light.transform.position.z < 6f;
            bool center = Mathf.Abs(light.transform.position.x) < .1f;
            var tint = lobby ? new Color(.85f,.93f,1f) : new Color(.77f,.89f,.96f);
            SetSpot(light, light.transform.position, Vector3.down, tint, lobby ? 3.35f : 3.75f, 5.65f, 154f, 112f,
                center ? LightShadows.Soft : LightShadows.None);
            FitFluorescentPanel(lights.Find(light.name + "_Fixture"), fixture, diffuser);
            if (center)
            {
                var ceilingBounce = PartLight(lights, "Ceiling_Bounce_" + light.name);
                Vector3 bouncePosition = light.transform.position - Vector3.up * .36f;
                SetSpot(ceilingBounce, bouncePosition, Vector3.up, new Color(.86f, .89f, .91f),
                    .20f, 1.65f, 143f, 103f, LightShadows.None);
            }
        }
        Point(lights, "Lobby_Counter_Bounce", new Vector3(2.31f,1.62f,3.25f), new Color(.87f,.90f,.99f), .61f, 4.8f);
        Point(lights, "Reception_Floor_Bounce", new Vector3(-.70f,1.38f,6.72f), new Color(.88f,.92f,1f), .82f, 3.95f);
        Point(lights, "Bullpen_Desk_Bounce", new Vector3(-2.03f,1.59f,12.02f), new Color(.78f,.88f,1f), .55f, 4.95f);
        Point(lights, "Bullpen_Far_Wall_Bounce", new Vector3(2.15f,1.72f,13.2f), new Color(.79f,.86f,.95f), .34f, 4.1f);
        SetSpot(lights.Find("Office_Ceiling").GetComponent<Light>(), new Vector3(4f,3.055f,21f), new Vector3(-.26f,-1f,0),
            new Color(.79f,.89f,1f), 1.07f, 5.4f, 148f, 103f, LightShadows.Soft);
        SetSpot(lights.Find("Captain_Ceiling").GetComponent<Light>(), new Vector3(-4f,3.05f,21f), Vector3.down,
            new Color(.86f,.90f,1f), 1.4f, 5.25f, 147f, 102f, LightShadows.None);
        var lampPosition = office.Find("Office_Lamp_Shade").GetComponent<Renderer>().bounds.center;
        lampPosition.y = .982f;
        var workTop = office.Find("Office_Desk/Papers").position;
        SetSpot(lights.Find("Office_DeskLamp").GetComponent<Light>(), lampPosition, workTop - lampPosition,
            new Color(1f,.75f,.46f), .68f, 2.0f, 101f, 65f, LightShadows.Soft);
        Point(lights, "Desk_Paper_Bounce", new Vector3(4.27f,1.03f,22.35f), new Color(1f,.82f,.61f), .42f, 2.6f);
        SetSpot(PartLight(lights,"Evidence_Board_Bounce"), new Vector3(2.37f,2.29f,20.28f),
            board.transform.position - new Vector3(2.37f,2.29f,20.28f), new Color(1f,.91f,.79f), 3.78f, 3.85f, 77f, 49f,
            LightShadows.None);
        Point(lights, "Office_Wall_Bounce", new Vector3(3.19f,1.75f,19.16f), new Color(.88f,.88f,.94f), .58f, 3.35f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.29f,.31f,.34f);
        RenderSettings.ambientEquatorColor = new Color(.20f,.22f,.225f);
        RenderSettings.ambientGroundColor = new Color(.145f,.15f,.149f);
        RenderSettings.reflectionIntensity = .89f;

        var probes = Part(root, "Room_Reflections");
        Probe(probes,"Reception_Reflection",new Vector3(0,1.58f,3.1f),new Vector3(15.8f,3.17f,6.8f),1,.77f);
        Probe(probes,"Bullpen_Reflection",new Vector3(-.25f,1.30f,11.35f),new Vector3(15.7f,3.20f,11.8f),2,.84f);
        Probe(probes,"Alexey_Office_Reflection",new Vector3(4.10f,1.34f,21.0f),new Vector3(7.85f,3.18f,5.86f),4,.83f);
        SetGrade(root, camera);
    }

    static Material Diffuser(string name, Color outputColor)
    {
        var material = MaterialAsset(name, "Universal Render Pipeline/Unlit");
        material.SetTexture("_BaseMap", Texture("Fluorescent_Prismatic_Acrylic"));
        material.SetColor("_BaseColor", outputColor);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void FitFluorescentPanel(Transform housing, Material housingPaint, Material diffuser)
    {
        Paint(housing, housingPaint);
        var face = housing.Find("Acrylic_Diffuser");
        if (!face)
        {
            face = PrecinctBuilder.Box(housing, "Acrylic_Diffuser", new Vector3(0, -.55f, 0),
                new Vector3(.93f, .12f, .79f), diffuser, false).transform;
        }
        var renderer = face.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = diffuser;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static void InstallVinylReflections(Transform precinct, Transform architecture, Camera camera, Texture normal, Texture wear)
    {
        var material = MaterialAsset("Polished_Vinyl_Reflection_Layer", "CarXCase/Interiors/Precinct Vinyl Reflection");
        material.SetTexture("_DetailNormal", normal);
        material.SetTexture("_VinylWear", wear);
        material.SetFloat("_ReflectionStrength", 1.22f);
        material.SetFloat("_ReflectionBlur", 1.16f);
        material.SetFloat("_ReflectionDistortion", .023f);
        EditorUtility.SetDirty(material);

        var rig = Part(precinct, "Vinyl_Floor_Reflection");
        var reflection = rig.GetComponent<PrecinctFloorReflection>();
        if (!reflection) reflection = rig.gameObject.AddComponent<PrecinctFloorReflection>();

        var floor = architecture.Find("Floor").GetComponent<Renderer>().bounds;
        var vestibule = architecture.Find("Vestibule_Floor").GetComponent<Renderer>().bounds;
        reflection.Configure(camera, new[]
        {
            PlaceVinylOverlay(rig, "Main_Floor_Reflection", floor, material),
            PlaceVinylOverlay(rig, "Vestibule_Floor_Reflection", vestibule, material)
        }, floor.max.y);
    }

    static Renderer PlaceVinylOverlay(Transform rig, string name, Bounds bounds, Material material)
    {
        var surface = rig.Find(name);
        var center = new Vector3(bounds.center.x, bounds.max.y + .002f, bounds.center.z);
        var size = new Vector3(bounds.size.x, bounds.size.z, 1);
        if (!surface)
            surface = PrecinctBuilder.Prim(rig, PrimitiveType.Quad, name, center, size, material, new Vector3(90, 0, 0)).transform;
        surface.position = center;
        surface.localScale = size;
        var renderer = surface.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return renderer;
    }

    static void SetBoardLook(DeductionBoard board, Material frame)
    {
        board.cardMaterial = Lit("Archive_Paper", Color.white, .23f, Texture("Evidence_Paper_Fibers"));
        board.photoMaterial = Lit("Matte_Evidence_Photograph", new Color(.96f,.94f,.89f), .26f);
        Emission(board.photoMaterial, new Color(.37f,.34f,.30f));
        board.pinMaterial = Lit("Oxide_Red_Pushpin", new Color(.67f,.16f,.11f), .38f);
        var yarn = MaterialAsset("Crimson_Cotton_Thread", "Universal Render Pipeline/Unlit");
        yarn.SetColor("_BaseColor", new Color(.66f,.104f,.071f));
        EditorUtility.SetDirty(yarn);
        board.yarnMaterial = yarn;
        board.cluePhotos = AssetDatabase.FindAssets("t:Texture2D", new[] { Art.TrimEnd('/') + "/Evidence" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
            .OrderBy(texture => texture.name, StringComparer.Ordinal).ToArray();

        var frameSize = board.transform.Find("Frame").localScale;
        var corkSize = board.transform.Find("Cork").localScale;
        float woodWidth = (frameSize.x - corkSize.x) / 2f;
        float woodHeight = (frameSize.y - corkSize.y) / 2f;
        var trim = Part(board.transform, "Walnut_Front_Trim");
        if (trim.childCount > 0) return;
        foreach (int side in new[] {-1, 1})
        {
            PrecinctBuilder.Box(trim, side < 0 ? "Bottom_Rail" : "Top_Rail",
                new Vector3(0,side*(frameSize.y+corkSize.y)/4f,.011f),
                new Vector3(frameSize.x,woodHeight,.028f), frame, false);
            PrecinctBuilder.Box(trim, side < 0 ? "Left_Stile" : "Right_Stile",
                new Vector3(side*(frameSize.x+corkSize.x)/4f,0,.011f),
                new Vector3(woodWidth,corkSize.y,.028f), frame, false);
        }
    }

    static void PlacePolice(Transform actors, string slot, int index, float height, float yaw)
    {
        var target = actors.Find(slot);
        string path = $"Assets/_Project/Arts/Heroes/police npc/police npc{index}.blend";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!asset || asset.GetComponentsInChildren<Renderer>().Length == 0)
            throw new InvalidOperationException("Could not load the police model " + path);

        var model = target.Find("Police_Visual");
        if (!model)
        {
            model = ((GameObject)PrefabUtility.InstantiatePrefab(asset, target)).transform;
            model.name = "Police_Visual";
        }
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.Euler(0,yaw,0) * asset.transform.localRotation;
        model.localScale = asset.transform.localScale;
        Bounds bounds = ModelBounds(model);
        model.localScale *= height / bounds.size.y;
        bounds = ModelBounds(model);
        Vector3 feet = new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        model.position += target.position - feet;
        var uniform = Lit($"Police_Uniform_{index}", Color.white, .32f, Texture($"Police_{index}_Albedo"));
        SetNormal(uniform, Texture($"Police_{index}_Normal", normal: true), .76f);
        foreach (var renderer in target.GetComponentsInChildren<Renderer>())
        {
            bool visible = renderer.transform.IsChildOf(model);
            renderer.enabled = visible;
            if (!visible) continue;
            renderer.sharedMaterial = uniform;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }
    }

    static Bounds ModelBounds(Transform model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void SetGrade(Transform root, Camera camera)
    {
        const string path = Art + "Precinct_Practicals_Grade.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.AddObjectToAsset(profile.Add<ColorAdjustments>(), profile);
            AssetDatabase.AddObjectToAsset(profile.Add<Tonemapping>(), profile);
            AssetDatabase.AddObjectToAsset(profile.Add<Bloom>(), profile);
        }
        profile.TryGet<ColorAdjustments>(out var grade);
        grade.postExposure.Override(.27f);
        grade.contrast.Override(6f);
        grade.saturation.Override(-11f);
        profile.TryGet<Tonemapping>(out var tone);
        tone.mode.Override(TonemappingMode.Neutral);
        profile.TryGet<Bloom>(out var bloom);
        bloom.intensity.Override(.14f);
        bloom.threshold.Override(1.21f);
        bloom.scatter.Override(.62f);
        foreach (var overrideAsset in profile.components) EditorUtility.SetDirty(overrideAsset);
        EditorUtility.SetDirty(profile);
        var node = Part(root,"Precinct_Color_Grade");
        var volume = node.GetComponent<Volume>();
        if (!volume) volume = node.gameObject.AddComponent<Volume>();
        volume.sharedProfile = profile;
        volume.isGlobal = true;
        volume.priority = 1f;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask |= 1 << volume.gameObject.layer;
    }

    static Material MaterialAsset(string name, string shaderName)
    {
        string path = Art + name + ".mat";
        var shader = Shader.Find(shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        if (material.shader != shader) material.shader = shader;
        return material;
    }

    static Material Lit(string name, Color color, float smoothness, Texture2D map = null, float metallic = 0)
    {
        var material = MaterialAsset(name, "Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        material.SetTexture("_BaseMap", map);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Texture2D Texture(string name, bool normal = false, bool linear = false)
    {
        string path = Art + "Textures/" + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new InvalidOperationException("Missing precinct texture " + path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        bool colorSpace = !normal && !linear;
        if (importer.textureType != type || importer.sRGBTexture != colorSpace)
        {
            importer.textureType = type;
            importer.sRGBTexture = colorSpace;
            importer.SaveAndReimport();
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!texture) throw new InvalidOperationException("Unable to import " + path);
        return texture;
    }

    static void SetNormal(Material material, Texture2D normal, float scale)
    {
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", scale);
        material.EnableKeyword("_NORMALMAP");
    }

    static void Emission(Material material, Color color)
    {
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor",color);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }

    static Transform Part(Transform parent, string name)
    {
        var node = parent.Find(name);
        if (node) return node;
        node = new GameObject(name).transform;
        node.SetParent(parent, false);
        return node;
    }

    static void Paint(Transform node, Material material)
    {
        var renderer = node.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
    }

    static Light PartLight(Transform parent, string name)
    {
        var node = Part(parent,name);
        var light = node.GetComponent<Light>();
        if (!light) light = node.gameObject.AddComponent<Light>();
        return light;
    }

    static void Point(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
    {
        var light = PartLight(parent,name);
        light.transform.position = position;
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    static void SetSpot(Light light, Vector3 position, Vector3 direction, Color color, float intensity,
        float range, float angle, float inner, LightShadows shadows)
    {
        var up = Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.up)) > .993f ? Vector3.forward : Vector3.up;
        light.transform.SetPositionAndRotation(position,Quaternion.LookRotation(direction,up));
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = angle;
        light.innerSpotAngle = inner;
        light.shadows = shadows;
        light.shadowBias = .13f;
        light.shadowNormalBias = .23f;
    }

    static void Probe(Transform parent, string name, Vector3 position, Vector3 dimensions, int importance, float intensity)
    {
        var node = Part(parent,name);
        node.position = position;
        var probe = node.GetComponent<ReflectionProbe>();
        if (!probe) probe = node.gameObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.renderDynamicObjects = true;
        probe.boxProjection = true;
        probe.hdr = true;
        probe.resolution = 128;
        probe.size = dimensions;
        probe.center = Vector3.up * (1.6f - position.y);
        probe.nearClipPlane = .07f;
        probe.farClipPlane = 29f;
        probe.shadowDistance = 16f;
        probe.importance = importance;
        probe.blendDistance = .75f;
        probe.intensity = intensity;
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(Art + name + ".exr");
    }

    internal static void BakeReflections(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var root = roots.Single(g => g.name == RootName).transform;
        var people = roots.Single(g => g.name == "Player").GetComponentsInChildren<Renderer>()
            .Concat(root.Find("NPCs").GetComponentsInChildren<Renderer>())
            .Where(r => !r.forceRenderingOff).ToArray();
        try
        {
            foreach (var r in people) r.forceRenderingOff = true;
            foreach (var probe in root.Find("Room_Reflections").GetComponentsInChildren<ReflectionProbe>())
            {
                var previous = probe.customBakedTexture;
                string path = Art + probe.name + ".exr";
                try
                {
                    probe.customBakedTexture = null;
                    if (!Lightmapping.BakeReflectionProbe(probe,path)) throw new InvalidOperationException("Could not bake " + probe.name);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                    if (!probe.customBakedTexture) throw new InvalidOperationException("Cubemap import failed for " + path);
                }
                catch
                {
                    probe.customBakedTexture = previous;
                    throw;
                }
            }
        }
        finally
        {
            foreach (var r in people) r.forceRenderingOff = false;
        }
    }
}
