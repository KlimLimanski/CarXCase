using System;
using System.IO;
using System.Linq;
using CarXCase.Apartment;
using CarXCase.HeroCar;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ApartmentFixtures
{
    const string ScenePath = "Assets/_Project/Scenes/01_FatherApartment.unity";
    const string Art = "Assets/_Project/Arts/Apartaments/Furniture/ApartmentFixtures/";
    const string Materials = Art + "Materials/";
    internal const string CrtSignalPath = Materials + "Crt_White_Noise.mat";

    [MenuItem("Tools/Brighton/Apartment: refresh appliances and mirror")]
    public static void ApplyAndSave()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open 01_FatherApartment in Edit Mode.");
        Apply(GameObject.Find("BrooklynApartment").transform);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    internal static void Apply(Transform apartment)
    {
        Directory.CreateDirectory(Materials);
        AssetDatabase.Refresh();
        var nodes = apartment.GetComponentsInChildren<Transform>(true);
        Transform Find(string name) => nodes.First(t => t && t.name == name);

        var kitchen = Find("Kitchen");
        var bathroom = Find("Bathroom");
        var fridge = Find("INT_FridgeDoor");
        var freezer = Find("INT_FreezerDoor");
        var oldFridgeDoor = Find("FridgeDoor_Visual").GetComponent<Renderer>();
        var oldFreezerDoor = Find("FreezerDoor_Visual").GetComponent<Renderer>();
        var oldFridgeBack = Find("Refrigerator_Back").GetComponent<Renderer>();

        Install(kitchen, "Cream_Fridge_Case", "Case_Outer_Cabinet", "Compressor_Grille",
            .825f, new Vector3(oldFridgeBack.bounds.center.x, 1.046f, -7.162f), Vector3.forward);
        Install(fridge, "Cream_Fridge_Door", "Door_Panel", "Handle_Grip",
            .796f, new Vector3(oldFridgeDoor.bounds.center.x, .767f, -6.699f), Vector3.forward);
        Install(freezer, "Cream_Freezer_Door", "Door_Panel", "Handle_Grip",
            .796f, new Vector3(oldFreezerDoor.bounds.center.x, 1.754f, -6.699f), Vector3.forward);
        oldFridgeDoor.enabled = oldFreezerDoor.enabled = false;
        float wallFace = Find("Shell_Wall_Shared_B").GetComponent<Collider>().bounds.max.x;
        FitDoorSwing(fridge, "Cream_Fridge_Door", wallFace);
        FitDoorSwing(freezer, "Cream_Freezer_Door", wallFace);

        var oldKitchenSink = Find("Sink_BasinBottom").GetComponent<Renderer>();
        var oldBasin = Find("Bathroom_Sink").GetComponent<Renderer>();
        Install(kitchen, "Stamped_Kitchen_Sink", "Basin_Rim_and_Bowl", "Tap_Mounting_Deck",
            .851f, new Vector3(oldKitchenSink.bounds.center.x, 1.059f, oldKitchenSink.bounds.center.z), Vector3.back, alignTop: true);
        Install(bathroom, "Porcelain_Bathroom_Basin", "Basin_Rim_and_Bowl", "Tap_Mounting_Deck",
            .649f, new Vector3(oldBasin.bounds.center.x, .958f, oldBasin.bounds.center.z), Vector3.forward, alignTop: true);
        FitCounterOpening(Find);
        foreach (var node in nodes)
        {
            if (!node) continue;
            string name = node.name;
            if (name.StartsWith("Refrigerator_") || name.StartsWith("Fridge_Vent") || name.StartsWith("Sink_")
                || name.StartsWith("Faucet_Knob") || name == "Kitchen_Faucet" || name.StartsWith("Basin_Tap") || name == "Basin_Faucet"
                || name == "Bathroom_Sink" || name == "KitchenBase_SinkLeft_Shelf.001" || name == "KitchenBase_SinkRight_Shelf.001")
            {
                if (node.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
            }
        }

        InstallScreen(Find("CRT_Television_Visual").GetComponent<MeshRenderer>());
        InstallMirror(Find("Mirror_Glass"));
        ApartmentKitchenLook.Apply(apartment);
        ApartmentRoomRepairs.Apply(apartment);
        if (FatherApartmentScenario.IsInstalled(apartment.gameObject.scene))
            FatherApartmentScenario.ConfigureLighting(apartment.gameObject.scene);
    }

    static void FitDoorSwing(Transform door, string visualName, float wallFace)
    {
        var hinge = door.GetComponent<ApartmentInteractable>();
        // The replacement has deeper handles. Put the pivot at the outside edge
        // of the panel while keeping the closed position of every door part.
        var children = door.Cast<Transform>().ToArray();
        var positions = children.Select(child => child.position).ToArray();
        var position = door.position;
        position.z = -6.656f;
        door.position = position;
        for (int i = 0; i < children.Length; i++) children[i].position = positions[i];

        var visual = door.Find(visualName);
        var bounds = visual.GetComponentsInChildren<Renderer>().Select(r => r.bounds)
            .Concat(door.GetComponentsInChildren<BoxCollider>().Select(c => c.bounds));
        var corners = bounds.SelectMany(b => new[] {
            new Vector3(b.min.x, b.center.y, b.min.z), new Vector3(b.max.x, b.center.y, b.min.z),
            new Vector3(b.min.x, b.center.y, b.max.z), new Vector3(b.max.x, b.center.y, b.max.z)
        }).Select(p => p - position).ToArray();
        float sign = Mathf.Sign(hinge.openAngle);
        var axis = door.TransformDirection(hinge.localAxis);
        int safeDegrees = 0;
        for (int angle = 1; angle <= 92; angle++)
        {
            var rotation = Quaternion.AngleAxis(sign * angle, axis);
            if (corners.Any(p => (position + rotation * p).x < wallFace + .006f)) break;
            safeDegrees = angle;
        }
        hinge.openAngle = sign * safeDegrees;
    }

    static void FitCounterOpening(Func<string, Transform> find)
    {
        var front = find("Counter_Front");
        var back = find("Counter_Back");
        ResizeSpan(front, Vector3.forward, -6.910f, front.GetComponent<Renderer>().bounds.max.z);
        ResizeSpan(back, Vector3.forward, back.GetComponent<Renderer>().bounds.min.z, -7.508f);
        foreach (string name in new[] { "KitchenBase_SinkLeft_Side.001", "KitchenBase_SinkRight_Side" })
        {
            var divider = find(name);
            ResizeSpan(divider, Vector3.up, divider.GetComponent<Renderer>().bounds.min.y, .80f);
        }
    }

    static void ResizeSpan(Transform piece, Vector3 axis, float min, float max)
    {
        var renderer = piece.GetComponent<Renderer>();
        var bounds = renderer.bounds;
        var localAxis = piece.InverseTransformDirection(axis);
        int index = Mathf.Abs(localAxis.x) > .5f ? 0 : Mathf.Abs(localAxis.y) > .5f ? 1 : 2;
        var scale = piece.localScale;
        scale[index] *= (max - min) / Vector3.Dot(bounds.size, axis);
        piece.localScale = scale;
        piece.position += axis * ((min + max) * .5f - Vector3.Dot(renderer.bounds.center, axis));
    }

    static void Install(Transform parent, string name, string referenceName, string facingPartName,
        float width, Vector3 target, Vector3 facing, bool alignTop = false)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".fbx");
        if (!asset) throw new InvalidOperationException($"Missing {name}.fbx");
        var previous = parent.Find(name);
        var prop = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
        prop.transform.SetParent(parent, true);
        try
        {
            var renderers = prop.GetComponentsInChildren<MeshRenderer>();
            MeshRenderer FindPart(string part) => renderers.First(r => r.name == part || r.name.StartsWith(part + ".", StringComparison.Ordinal));
            var reference = FindPart(referenceName);
            var facingPart = FindPart(facingPartName);
            prop.transform.localScale *= width / reference.bounds.size.x;
            // The Blender meshes are aligned to cardinal axes. Ignore the lateral handle offset.
            var axis = facingPart.bounds.center.z < reference.bounds.center.z ? Vector3.back : Vector3.forward;
            prop.transform.Rotate(Vector3.up, Vector3.SignedAngle(axis, facing, Vector3.up), Space.World);
            var b = reference.bounds;
            prop.transform.position += target - new Vector3(b.center.x, alignTop ? b.max.y : b.center.y, b.center.z);
            foreach (var renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(FixtureMaterial).ToArray();
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(prop);
            throw;
        }
        if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
    }

    static void InstallScreen(MeshRenderer screen)
    {
        var materials = screen.sharedMaterials;
        int slot = Array.FindIndex(materials, m => m && m.name is "CRTGlass" or "Crt_White_Noise" or "Unpowered_CRT_Glass");
        if (slot < 0) throw new InvalidOperationException("Cannot find the CRT glass material slot.");

        var mesh = screen.GetComponent<MeshFilter>().sharedMesh;
        var vertices = mesh.vertices;
        var indices = mesh.GetIndices(slot);
        var bounds = new Bounds(screen.transform.TransformPoint(vertices[indices[0]]), Vector3.zero);
        foreach (int i in indices) bounds.Encapsulate(screen.transform.TransformPoint(vertices[i]));
        var signal = Material("Crt_White_Noise", "CarXCase/Apartment CRT Noise");
        signal.SetVector("_CrtOrigin", bounds.min);
        signal.SetVector("_CrtDimensions", new Vector4(bounds.size.z, bounds.size.y, 0, 0));
        signal.SetFloat("_Peak", 1.74f);
        EditorUtility.SetDirty(signal);
        materials[slot] = signal;
        screen.sharedMaterials = materials;

        var parent = screen.transform.parent;
        var old = parent.Find("CRT_White_Noise_Light");
        var source = old ? old.gameObject : new GameObject("CRT_White_Noise_Light");
        source.transform.SetParent(parent, false);
        source.transform.SetPositionAndRotation(new Vector3(bounds.max.x + .018f, bounds.min.y + .075f, bounds.center.z),
            Quaternion.LookRotation(new Vector3(1, -.12f, 0)));
        var light = source.GetComponent<Light>();
        if (!light) light = source.AddComponent<Light>();
        light.type = LightType.Spot;
        light.intensity = 9.8f;
        light.range = 4.6f;
        light.spotAngle = 133;
        light.innerSpotAngle = 73;
        light.color = new Color(.96f, .98f, 1f);
        light.shadows = LightShadows.None;
        if (!source.GetComponent<CrtScreenLight>()) source.AddComponent<CrtScreenLight>();

        // The cabinet top is behind the face of the glass. A short diffuse spill
        // keeps the under-screen trim and the shelf connected to the white glow.
        var shelfLight = parent.Find("CRT_Shelf_Spill");
        if (!shelfLight)
        {
            shelfLight = new GameObject("CRT_Shelf_Spill").transform;
            shelfLight.SetParent(parent, false);
        }
        shelfLight.position = new Vector3(bounds.max.x + .010f, bounds.min.y + .016f, bounds.center.z);
        var fill = shelfLight.GetComponent<Light>();
        if (!fill) fill = shelfLight.gameObject.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.intensity = .13f;
        fill.range = .65f;
        fill.color = light.color;
        fill.shadows = LightShadows.None;
        if (!shelfLight.GetComponent<CrtScreenLight>()) shelfLight.gameObject.AddComponent<CrtScreenLight>();
    }

    static void InstallMirror(Transform glass)
    {
        var material = Material("Bathroom_Planar_Mirror", "CarXCase/CarMirror");
        material.SetFloat("_EdgeDarken", .16f);
        material.SetFloat("_Grime", .07f);
        EditorUtility.SetDirty(material);
        var renderer = glass.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        var mirror = glass.GetComponent<CarMirror>();
        if (!mirror) mirror = glass.gameObject.AddComponent<CarMirror>();
        else mirror.enabled = false;
        mirror.mirrorMaterial = material;
        mirror.viewer = Camera.main;
        mirror.resolution = 640;
        mirror.farClip = 19;
        mirror.tint = new Color(.92f, .94f, .95f);
        mirror.clipAtGlass = true;
        mirror.visibleDistance = 4.5f;
        if (mirror.enabled) mirror.enabled = false;
        mirror.enabled = true;
    }

    static Material FixtureMaterial(Material input)
    {
        switch (input.name)
        {
            case "Fixture_Aged_Enamel": return Lit(input.name, new Color(.74f,.71f,.61f), .49f);
            case "Fixture_Porcelain": return Lit(input.name, new Color(.81f,.81f,.75f), .67f);
            case "Fixture_Liner": return Lit(input.name, new Color(.73f,.73f,.68f), .45f);
            case "Fixture_Rubber": return Lit(input.name, new Color(.086f,.081f,.067f), .18f);
            case "Fixture_Satin_Steel": return Lit(input.name, new Color(.51f,.52f,.51f), .39f, .61f);
            case "Fixture_Chrome": return Lit(input.name, new Color(.62f,.61f,.57f), .69f, .83f);
            case "Fixture_Charcoal": return Lit(input.name, new Color(.027f,.028f,.028f), .12f);
            case "Fixture_Badge": return Lit(input.name, new Color(.25f,.24f,.20f), .46f, .35f);
            case "Fixture_Plastic": return Lit(input.name, new Color(.58f,.53f,.42f), .46f);
            case "Fixture_Magnet": return Lit(input.name, new Color(.34f,.10f,.068f), .38f);
            default: throw new InvalidOperationException($"Unknown fixture finish {input.name}");
        }
    }

    static Material Lit(string name, Color color, float smoothness, float metallic = 0)
    {
        var material = Material(name, "Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material Material(string name, string shaderName)
    {
        var path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            var shader = Shader.Find(shaderName);
            if (!shader) throw new InvalidOperationException($"Missing shader {shaderName}");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }
}
