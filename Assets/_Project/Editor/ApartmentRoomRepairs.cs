using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ApartmentRoomRepairs
{
    const string ScenePath = "Assets/_Project/Scenes/01_FatherApartment.unity";
    const string Art = "Assets/_Project/ArtDirection/ApartmentRepairs/";

    [MenuItem("Tools/Brighton/Apartment: refresh bedroom and bathroom repairs")]
    public static void ApplyAndSave()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open 01_FatherApartment in Edit Mode.");
        var apartment = scene.GetRootGameObjects().Single(g => g.name == "BrooklynApartment").transform;
        Apply(apartment);
        ApartmentNoirLook.RefreshIfPresent(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unable to save the apartment.");
    }

    internal static void Apply(Transform apartment)
    {
        var scene = apartment.gameObject.scene;
        var sceneNodes = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform Find(string name) => sceneNodes.First(x => x && x.name == name && x.gameObject.activeInHierarchy);
        var edits = Part(apartment, "Apartment_Room_Repairs");
        edits.localScale = Vector3.one / apartment.lossyScale.x;

        var caseRoot = Find("Cream_Fridge_Case");
        var dark = Lit("Fridge_Recessed_Seals", new Color(.127f, .111f, .092f), .18f);
        Trim(caseRoot, "Compartment_Divider", new Vector3(-3.910f, 1.459f, -6.766f),
            new Vector3(.791f, .094f, .053f), dark);
        foreach (float x in new[] { -4.298f, -3.522f })
            Trim(caseRoot, x < -4f ? "Left_Seal" : "Right_Seal", new Vector3(x, 1.067f, -6.766f),
                new Vector3(.039f, 1.953f, .053f), dark);
        Trim(caseRoot, "Top_Seal", new Vector3(-3.910f, 2.030f, -6.766f),
            new Vector3(.792f, .049f, .053f), dark);
        Trim(caseRoot, "Bottom_Seal", new Vector3(-3.910f, .092f, -6.766f),
            new Vector3(.792f, .060f, .053f), dark);

        var hallWardrobe = Find("Wardrobe_Assembly");
        var hallLeft = hallWardrobe.Find("INT_Wardrobe_Door00");
        var hallRight = hallWardrobe.Find("INT_Wardrobe_Door01");
        var hallFrame = hallWardrobe.Find("Wardrobe_Assembly_Visual").GetComponent<Renderer>().sharedMaterial;
        MoveHinge(hallLeft, -3.651f, -2.629f, 94f);
        MoveHinge(hallRight, -3.651f, -1.442f, -94f);
        Trim(edits, "Hall_Wardrobe_Door_Stop", new Vector3(-3.693f, 1.209f, -2.035f),
            new Vector3(.032f, 2.226f, .037f), hallFrame);

        ConfigureBedroomDoor(apartment);
        ConfigureBedroomWindow(apartment);

        var oldBedroomWardrobe = Find("Wardrobe_Open");
        Hide(oldBedroomWardrobe);
        foreach (var collider in oldBedroomWardrobe.GetComponents<Collider>()) collider.enabled = false;
        var bedWardrobe = Part(edits, "Bedroom_Wardrobe");
        bedWardrobe.position = new Vector3(-6.574f, .005f, -.413f);
        Model(bedWardrobe, "Bedroom_Wardrobe_Case", bedWardrobe.position, 180f);
        Hitbox(bedWardrobe, bedWardrobe.position + new Vector3(0f, .94f, .026f),
            new Vector3(1.073f, 1.88f, .588f));
        for (int side = -1; side <= 1; side += 2)
        {
            string suffix = side < 0 ? "L" : "R";
            var pivot = new Vector3(-6.574f+side*.495f, .102f, -.755f);
            var hinge = Hinge(bedWardrobe, "INT_Bedroom_Wardrobe_" + suffix, pivot, -side*98f);
            var center = new Vector3(-6.574f+side*.250f, .102f, -.755f);
            Model(hinge, "Bedroom_Wardrobe_Leaf_" + suffix, center, 180f);
            Hitbox(hinge, center + Vector3.up*.867f, new Vector3(.493f, 1.734f, .048f));
        }
        Trim(edits, "Bedroom_Wardrobe_Door_Stop", new Vector3(-6.574f, .970f, -.717f),
            new Vector3(.029f, 1.734f, .025f), hallFrame);

        Find("ReadingCorner_Nightstand").rotation = Quaternion.Euler(0f, 53f, 0f);
        PlaceSconce(edits, "Bedside_Sconce", new Vector3(-8.708f, 1.635f, -2.525f), .73f);
        PlaceSconce(edits, "Reading_Sconce", new Vector3(-8.708f, 1.582f, -6.920f), .64f);
        Find("Bedroom_Lamp_Light").GetComponent<Light>().enabled = false;
        Find("ReadingLamp_Light").GetComponent<Light>().enabled = false;
        var oldSconce = edits.Find("Bedroom_Opal_Sconce");
        if (oldSconce) UnityEngine.Object.DestroyImmediate(oldSconce.gameObject);

        Model(edits, "Vitreo_Toilet", new Vector3(-1.472f,.019f,-2.239f), 0f);
        foreach (var name in new[] { "Toilet_Bowl", "Toilet_Seat", "Toilet_Pedestal", "Toilet_Tank", "Toilet_TankLid", "Toilet_FlushHandle",
                     "Tub_Faucet", "Tub_Tap", "Tub_Tap.001", "Basin_BackLedge" })
            Hide(Find(name));
        Model(edits, "Wall_Tub_Mixer", new Vector3(-.471f, .835f, -2.231f), 0f);
        SetClearance(Find("Shower_Riser"), -2.212f);
        var shower = Find("Shower_Head");
        SetClearance(shower, -1.945f);
        var bathBounce = Find("Bath_CeilingBounce").GetComponent<Light>();
        bathBounce.intensity = .39f;

        RefreshTextiles(apartment);
        var blood = StainMaterial();
        var floor = Find("Shell_Floor_Bathroom").GetComponent<Renderer>().bounds.max.y;
        Stain(edits, "Bathroom_Dried_Drops", new Vector3(-1.075f, floor + .0018f, -1.461f), new Vector2(.265f,.230f), 28f, blood);
        var oldStain = edits.Find("BathMat_Dried_Drops");
        if (oldStain) UnityEngine.Object.DestroyImmediate(oldStain.gameObject);
        Stain(edits, "Bedside_Rug_Dried_Drops", new Vector3(-6.058f, .020f, -2.055f), new Vector2(.265f,.265f), -14f, blood);
        ApartmentWoodLook.Apply(apartment);
    }

    public static void ConfigureBedroomWindow(Transform apartment)
    {
        var architecture = apartment.Find("Architecture");
        var shell = apartment.Find("Apartment_Shell");
        var kitchenGlass = architecture.Find("Window_Glazing").GetComponent<Renderer>().bounds;
        var bedroomGlass = shell.Find("Shell_BedWin_Glazing").GetComponent<Renderer>().bounds;
        var window = apartment.Find("Bedroom_Kitchen_Window");
        if (!window)
        {
            window = Part(apartment, "Bedroom_Kitchen_Window");
            // Capture the parts in kitchen coordinates before turning the copy
            // to face into the bedroom from the opposite exterior wall.
            window.SetPositionAndRotation(kitchenGlass.center, Quaternion.identity);
            window.localScale = Vector3.one / apartment.lossyScale.x;
            foreach (string name in new[] { "Window_Glazing", "Window_Jamb", "Window_Jamb.001", "Window_Sash",
                         "Window_Sash.001", "Window_Sash.002", "Window_Sill", "Blind_Assembly", "Blinds_Headrail" })
            {
                var original = architecture.Find(name);
                var copy = UnityEngine.Object.Instantiate(original.gameObject, window, true);
                copy.name = name;
            }
        }

        window.SetPositionAndRotation(bedroomGlass.center, Quaternion.Euler(0f, 180f, 0f));
        window.localScale = new Vector3(1f, 1f, bedroomGlass.size.z / kitchenGlass.size.z) / apartment.lossyScale.x;
        foreach (Transform old in shell)
        {
            if (!old.name.StartsWith("Shell_BedWin_", StringComparison.Ordinal)) continue;
            Hide(old);
            foreach (var collider in old.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
    }

    public static void ConfigureBedroomDoor(Transform apartment)
    {
        var edits = Part(apartment, "Apartment_Room_Repairs");
        edits.localScale = Vector3.one / apartment.lossyScale.x;
        var nodes = apartment.GetComponentsInChildren<Transform>(true);
        Transform Find(string name) => nodes.First(x => x && x.name == name && x.gameObject.activeInHierarchy);

        foreach (var name in new[] { "Shell_BedDoor_Leaf", "Shell_BedDoor_Knob", "Shell_BedDoor_PanelBot", "Shell_BedDoor_PanelTop" })
        {
            var old = Find(name);
            Hide(old);
            foreach (var collider in old.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

        var nearJamb = Find("Shell_BedDoor_Jamb_A");
        var farJamb = Find("Shell_BedDoor_Jamb_B");
        nearJamb.position += Vector3.forward * (-4.895f - nearJamb.GetComponent<Renderer>().bounds.min.z);
        farJamb.position += Vector3.forward * (-5.916f - farJamb.GetComponent<Renderer>().bounds.max.z);
        foreach (var frame in new[] { nearJamb, farJamb, Find("Shell_BedDoor_Lintel") })
        {
            var bounds = frame.GetComponent<Renderer>().bounds;
            Hitbox(Part(edits, frame.name + "_Collision"), bounds.center, bounds.size);
        }

        const float doorX = -4.542f;
        var hinge = Hinge(edits, "INT_Bedroom_Door", new Vector3(doorX, .020f, -5.901f), -89f);
        float doorCenter = -5.405f;
        var leaf = Model(hinge, "Bedroom_Door", new Vector3(doorX, .020f, doorCenter), 90f);
        var knob = leaf.GetComponentsInChildren<MeshRenderer>().First(x => x.name.StartsWith("Brass_Knob"));
        if (knob.bounds.center.z < doorCenter)
            leaf.rotation = Quaternion.Euler(0f, -90f, 0f);
        Hitbox(hinge, new Vector3(doorX, 1.189f, doorCenter), new Vector3(.055f, 2.337f, .991f));

        var tv = Find("CRT_Television");
        var shift = Vector3.forward * (-3.802f - tv.position.z);
        foreach (string name in new[] { "CRT_Television", "TVStand_Assembly", "TV_PowerCord", "WallShelf.001", "Shelf_Bracket.002", "Shelf_Bracket.003" })
            Find(name).position += shift;
        var photo = Find("INT_PhotoFrame");
        var photoVisual = photo.Find("Evidence_Visual");
        if (photoVisual)
        {
            var renderers = photoVisual.GetComponentsInChildren<Renderer>();
            var photoBounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) photoBounds.Encapsulate(renderer.bounds);
            var shelf = Find("WallShelf.001").GetComponent<Renderer>().bounds;
            photo.position += Vector3.forward * (shelf.center.z + .24f - photoBounds.center.z);
        }
        var tvMesh = tv.Find("CRT_Television_Visual").GetComponent<MeshRenderer>();
        var screen = AssetDatabase.LoadAssetAtPath<Material>(ApartmentFixtures.CrtSignalPath);
        screen.SetVector("_CrtOrigin", screen.GetVector("_CrtOrigin") + (Vector4)shift);
        EditorUtility.SetDirty(screen);
        var housing = tvMesh.bounds;
        float baseY = .694f;
        Hitbox(Part(edits, "CRT_Housing_Collision"),
            new Vector3(housing.center.x, (housing.max.y + baseY) * .5f, housing.center.z),
            new Vector3(housing.size.x, housing.max.y - baseY, housing.size.z));
    }

    static void PlaceSconce(Transform parent, string name, Vector3 position, float intensity)
    {
        var mount = Part(parent, name);
        var model = Model(mount, "Bedroom_Opal_Sconce", position, 90f);
        var glass = Lit("Sconce_Warm_Opal", new Color(.84f,.67f,.46f), .39f);
        glass.EnableKeyword("_EMISSION");
        glass.SetColor("_EmissionColor", new Color(1.21f,.79f,.44f));
        glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        var diffuser = model.GetComponentsInChildren<MeshRenderer>().First(r => r.name.StartsWith("Opal_Glass_Diffuser"));
        diffuser.sharedMaterial = glass;
        var bulb = Part(mount, "Sconce_Bounce");
        bulb.position = position + new Vector3(.266f,.06f,0f);
        var light = bulb.GetComponent<Light>();
        if (!light) light = bulb.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f,.73f,.46f);
        light.range = 3.05f;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    internal static void RefreshTextiles(Transform apartment)
    {
        ApartmentRugLook.Apply(apartment);
        var sofa = apartment.gameObject.scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MeshRenderer>(true))
            .First(x=>x.name=="Sofa_Study");
        var materials = sofa.sharedMaterials;
        int i = Array.FindIndex(materials, m => m.name == "BrownPlaid" || m.name == "Study_Sofa_Twill");
        materials[i] = Textile("Study_Sofa_Twill", .25f, 1.16f);
        sofa.sharedMaterials = materials;
    }

    static void MoveHinge(Transform hinge, float x, float z, float angle)
    {
        var children = hinge.Cast<Transform>().ToArray();
        var positions = children.Select(child => child.position).ToArray();
        float moveX = x - hinge.position.x;
        hinge.position = new Vector3(x, hinge.position.y, z);
        for (int i = 0; i < children.Length; i++) children[i].position = positions[i] + Vector3.right*moveX;
        var interactive = hinge.GetComponent<ApartmentInteractable>();
        interactive.localAxis = hinge.InverseTransformDirection(Vector3.up);
        interactive.openAngle = angle;
    }

    static Transform Hinge(Transform parent, string name, Vector3 position, float angle)
    {
        var hinge = Part(parent, name);
        hinge.SetPositionAndRotation(position, Quaternion.identity);
        var interactive = hinge.GetComponent<ApartmentInteractable>();
        if (!interactive) interactive = hinge.gameObject.AddComponent<ApartmentInteractable>();
        interactive.kind = "hinge";
        interactive.localAxis = Vector3.up;
        interactive.openAngle = angle;
        return hinge;
    }

    static void Hitbox(Transform parent, Vector3 position, Vector3 size)
    {
        var hit = Part(parent, "Solid_Hitbox");
        hit.SetPositionAndRotation(position, Quaternion.identity);
        hit.localScale = Vector3.one;
        var collider = hit.GetComponent<BoxCollider>();
        if (!collider) collider = hit.gameObject.AddComponent<BoxCollider>();
        collider.size = size;
    }

    static void SetClearance(Transform item, float minZ)
    {
        var renderer = item.GetComponent<Renderer>();
        item.position += Vector3.forward*(minZ-renderer.bounds.min.z);
    }

    static void Hide(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
    }

    static Transform Part(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static Transform Model(Transform parent, string name, Vector3 position, float yaw)
    {
        var instance = parent.Find(name);
        if (!instance)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art+name+".glb");
            if (!asset) throw new InvalidOperationException("Missing model "+name);
            instance = ((GameObject)PrefabUtility.InstantiatePrefab(asset,parent)).transform;
        }
        instance.SetPositionAndRotation(position, Quaternion.Euler(0f,yaw,0f));
        instance.localScale = Vector3.one / parent.lossyScale.x;
        return instance;
    }

    static Transform Primitive(Transform parent, string name, PrimitiveType type)
    {
        var t = parent.Find(name);
        if (t) return t;
        var go = GameObject.CreatePrimitive(type);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        t = go.transform;
        t.SetParent(parent,false);
        return t;
    }

    static void Trim(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var t = Primitive(parent,name,PrimitiveType.Cube);
        t.SetPositionAndRotation(position,Quaternion.identity);
        t.localScale = size / parent.lossyScale.x;
        t.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void Stain(Transform parent, string name, Vector3 position, Vector2 size, float yaw, Material material)
    {
        var t = Primitive(parent,name,PrimitiveType.Quad);
        t.SetPositionAndRotation(position,Quaternion.Euler(0f,yaw,0f)*Quaternion.Euler(90f,0f,0f));
        t.localScale = new Vector3(size.x,size.y,1f);
        var renderer = t.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.sharedMaterial = material;
    }

    static Material StainMaterial()
    {
        var material = Lit("Dried_Blood_Transfer",Color.white,.21f);
        material.SetTexture("_BaseMap",Map("Dried_Blood_Transfer"));
        material.SetOverrideTag("RenderType","Transparent");
        material.SetFloat("_Surface",1f);
        material.SetFloat("_ZWrite",0f);
        material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_BlendModePreserveSpecular",0f);
        material.SetFloat("_SpecularHighlights",0f);
        material.SetFloat("_EnvironmentReflections",0f);
        material.SetFloat("_Cull",(float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetShaderPassEnabled("ShadowCaster",false);
        return material;
    }

    static Material Textile(string name, float smoothness, float strength)
    {
        var material = Lit(name,Color.white,smoothness);
        material.SetTexture("_BaseMap",Map(name+"_Albedo"));
        material.SetTexture("_BumpMap",Map(name+"_Normal",true));
        material.SetFloat("_BumpScale",strength);
        material.EnableKeyword("_NORMALMAP");
        return material;
    }

    static Material Lit(string name, Color color, float smoothness)
    {
        string path=Art+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material,path);
        }
        material.color=color;
        material.SetFloat("_Smoothness",smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Texture2D Map(string name, bool normal=false)
    {
        string path=Art+name+".png";
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new InvalidOperationException("Missing texture "+name);
        var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        if (importer.textureType!=type || importer.anisoLevel!=8)
        {
            importer.textureType=type;
            importer.anisoLevel=8;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
