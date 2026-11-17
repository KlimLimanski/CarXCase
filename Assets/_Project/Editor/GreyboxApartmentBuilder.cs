using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GreyboxApartmentBuilder
{
    private const string MaterialFolder = "Assets/Materials";

    [MenuItem("Tools/Greybox/Build FatherApartment")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        var wall = Material("Mat_Wall_Beige", 0xB8, 0xA9, 0x8A);
        var floor = Material("Mat_Floor_DarkBrown", 0x3A, 0x2A, 0x1E);
        var furniture = Material("Mat_Furniture_DarkWood", 0x4A, 0x33, 0x20);
        var lamp = Material("Mat_Lamp_NearlyBlack", 0x1A, 0x1A, 0x1A);
        var briefcase = Material("Mat_Briefcase_DarkBrown", 0x2E, 0x1E, 0x12);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = new GameObject("Apartment_Root");
        root.transform.position = Vector3.zero;

        Cube(root.transform, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(5f, 0.1f, 4f), floor);
        Cube(root.transform, "Ceiling", new Vector3(0f, 2.75f, 0f), new Vector3(5f, 0.1f, 4f), wall);
        Cube(root.transform, "Wall_North", new Vector3(0f, 1.35f, 2f), new Vector3(5f, 2.7f, 0.1f), wall);
        Cube(root.transform, "Wall_South_Left", new Vector3(-1.7f, 1.35f, -2f), new Vector3(1.6f, 2.7f, 0.1f), wall);
        Cube(root.transform, "Wall_South_Right", new Vector3(1.7f, 1.35f, -2f), new Vector3(1.6f, 2.7f, 0.1f), wall);
        Cube(root.transform, "Wall_West", new Vector3(-2.5f, 1.35f, 0f), new Vector3(0.1f, 2.7f, 4f), wall);
        // East wall has a centered 1.6 m wide, 1.3 m high window opening.
        Cube(root.transform, "Wall_East_Sill", new Vector3(2.5f, 0.45f, 0f), new Vector3(0.1f, 0.9f, 4f), wall);
        Cube(root.transform, "Wall_East_Lintel", new Vector3(2.5f, 2.45f, 0f), new Vector3(0.1f, 0.5f, 4f), wall);
        Cube(root.transform, "Wall_East_WindowSide_North", new Vector3(2.5f, 1.55f, 1.4f), new Vector3(0.1f, 1.3f, 1.2f), wall);
        Cube(root.transform, "Wall_East_WindowSide_South", new Vector3(2.5f, 1.55f, -1.4f), new Vector3(0.1f, 1.3f, 1.2f), wall);

        var table = Cube(root.transform, "Table_Main", new Vector3(-0.3f, 0.75f, 0.5f), new Vector3(1.4f, 0.08f, 0.8f), furniture);
        for (int ix = 0; ix < 2; ix++)
            for (int iz = 0; iz < 2; iz++)
                Cube(table.transform, "Table_Leg_" + (ix == 0 ? "Left" : "Right") + "_" + (iz == 0 ? "Front" : "Back"),
                    new Vector3(ix == 0 ? -0.7f : 0.7f, -0.375f, iz == 0 ? -0.4f : 0.4f), new Vector3(0.08f, 0.75f, 0.08f), furniture);

        var fallen = Cube(root.transform, "Chair_Fallen", new Vector3(0.6f, 0.22f, -0.3f), new Vector3(0.45f, 0.45f, 0.45f), furniture);
        fallen.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Cube(root.transform, "Chair_Upright", new Vector3(-0.3f, 0.22f, 1.2f), new Vector3(0.45f, 0.45f, 0.45f), furniture);
        Cube(root.transform, "Bed", new Vector3(-1.5f, 0.22f, -1.2f), new Vector3(1.9f, 0.45f, 0.9f), furniture);
        Cube(root.transform, "Wardrobe", new Vector3(2f, 1f, 1.4f), new Vector3(1f, 2f, 0.6f), furniture);
        Cube(root.transform, "Nightstand", new Vector3(-1.9f, 0.25f, -1.6f), new Vector3(0.5f, 0.5f, 0.4f), furniture);
        var brokenLamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        brokenLamp.name = "FloorLamp_Broken";
        brokenLamp.transform.SetParent(root.transform, false);
        brokenLamp.transform.position = new Vector3(0.9f, 0.2f, 1.3f);
        brokenLamp.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
        brokenLamp.transform.rotation = Quaternion.Euler(0f, 0f, 70f);
        brokenLamp.GetComponent<Renderer>().sharedMaterial = lamp;
        Cube(root.transform, "Briefcase_Prop", new Vector3(-0.3f, 0.85f, 0.5f), new Vector3(0.4f, 0.3f, 0.12f), briefcase);

        AddLight(root.transform, "Light_Ceiling", LightType.Point, new Vector3(0f, 2.5f, 0f), new Color32(255, 217, 160, 255), 1.5f, 6f);
        AddLight(root.transform, "Light_Window_Neon", LightType.Point, new Vector3(3.5f, 1.5f, 0f), new Color32(139, 30, 30, 255), 2f, 5f);
        var rain = AddLight(root.transform, "Light_Window_Rain", LightType.Spot, new Vector3(3f, 2.2f, 0f), new Color32(106, 122, 138, 255), 1.2f, 7f);
        rain.spotAngle = 45f;
        rain.transform.rotation = Quaternion.Euler(80f, -90f, 0f);

        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1f, -1.5f);
        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Capsule";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(0.9f, 1.8f, 0.9f);
        Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.3f;

        var camera = GameObject.FindGameObjectWithTag("MainCamera");
        if (camera == null) camera = new GameObject("Main Camera");
        camera.name = "Main Camera";
        camera.tag = "MainCamera";
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        camera.transform.localRotation = Quaternion.identity;
        if (camera.GetComponent<Camera>() == null) camera.AddComponent<Camera>();
        if (camera.GetComponent<AudioListener>() == null) camera.AddComponent<AudioListener>();

        foreach (var directional in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (directional.type == LightType.Directional) directional.intensity = 0.2f;

        var scenePath = "Assets/Scenes/FatherApartment.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        var objectNames = System.Array.ConvertAll(Object.FindObjectsByType<GameObject>(), go => go.name);
        Debug.Log("FatherApartment base room created before bathroom, balcony, and kitchen expansion. Created objects: " +
                  string.Join(", ", objectNames));
        AddBathroomAndBalcony();
        ExpandApartmentAndAddKitchen();
        RefitBathroomAndRestoreWindow();
    }

    public static void AddBathroomAndBalcony()
    {
        const string scenePath = "Assets/Scenes/FatherApartment.unity";
        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var apartment = GameObject.Find("Apartment_Root");
        if (apartment == null)
        {
            Debug.LogError("Apartment_Root was not found in FatherApartment.");
            return;
        }
        if (apartment.transform.Find("Bathroom_Nook") != null || apartment.transform.Find("Balcony") != null)
        {
            Debug.LogWarning("Bathroom or balcony already exists; skipping duplicate additions.");
            return;
        }

        var bathroomWall = Material("Mat_Bathroom_Grey", 0x8C, 0x91, 0x91);
        var ceramic = Material("Mat_Bathroom_Ceramic", 0xD4, 0xD5, 0xD0);
        var showerMetal = Material("Mat_Shower_DarkMetal", 0x45, 0x4A, 0x4E);
        var balconyMaterial = Material("Mat_Balcony_Concrete", 0x66, 0x68, 0x65);

        // A compact 1.35 x 1.65 m bathroom nook in the north-west corner.
        var bathroom = new GameObject("Bathroom_Nook");
        bathroom.transform.SetParent(apartment.transform, false);
        Cube(bathroom.transform, "Bathroom_Floor", new Vector3(-1.775f, 0.025f, 1.125f), new Vector3(1.35f, 0.05f, 1.65f), bathroomWall);
        Cube(bathroom.transform, "Bathroom_Wall_South", new Vector3(-1.775f, 1.1f, 0.3f), new Vector3(1.35f, 2.2f, 0.1f), bathroomWall);
        // The east partition leaves a 0.65 m doorway near its south end.
        Cube(bathroom.transform, "Bathroom_Wall_East_North", new Vector3(-1.1f, 1.1f, 1.5f), new Vector3(0.1f, 2.2f, 0.9f), bathroomWall);
        Cube(bathroom.transform, "Bathroom_Wall_East_South", new Vector3(-1.1f, 1.1f, 0.375f), new Vector3(0.1f, 2.2f, 0.15f), bathroomWall);

        // Simple primitive toilet: tank, pedestal, bowl and seat.
        Cube(bathroom.transform, "Toilet_Base", new Vector3(-2.12f, 0.19f, 0.72f), new Vector3(0.38f, 0.38f, 0.48f), ceramic);
        Cube(bathroom.transform, "Toilet_Bowl", new Vector3(-2.12f, 0.39f, 0.65f), new Vector3(0.44f, 0.12f, 0.5f), ceramic);
        Cube(bathroom.transform, "Toilet_Seat", new Vector3(-2.12f, 0.46f, 0.62f), new Vector3(0.4f, 0.04f, 0.44f), showerMetal);
        Cube(bathroom.transform, "Toilet_Tank", new Vector3(-2.12f, 0.73f, 1.08f), new Vector3(0.4f, 0.55f, 0.18f), ceramic);

        // Shower pan and exposed pipe/head, all built from Unity primitives.
        Cube(bathroom.transform, "Shower_Tray", new Vector3(-1.65f, 0.08f, 1.48f), new Vector3(0.62f, 0.12f, 0.62f), ceramic);
        Cube(bathroom.transform, "Shower_BackPanel", new Vector3(-1.65f, 1.1f, 1.78f), new Vector3(0.62f, 2.0f, 0.04f), bathroomWall);
        var showerPipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        showerPipe.name = "Shower_Pipe";
        showerPipe.transform.SetParent(bathroom.transform, false);
        showerPipe.transform.localPosition = new Vector3(-1.4f, 0.95f, 1.72f);
        showerPipe.transform.localScale = new Vector3(0.025f, 0.85f, 0.025f);
        showerPipe.GetComponent<Renderer>().sharedMaterial = showerMetal;
        var showerHead = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        showerHead.name = "Shower_Head";
        showerHead.transform.SetParent(bathroom.transform, false);
        showerHead.transform.localPosition = new Vector3(-1.46f, 1.75f, 1.7f);
        showerHead.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        showerHead.transform.localScale = new Vector3(0.07f, 0.04f, 0.07f);
        showerHead.GetComponent<Renderer>().sharedMaterial = showerMetal;

        // Turn the east window into a low-key balcony doorway, then add the balcony slab and rails.
        foreach (var sideName in new[] { "Wall_East_WindowSide_North", "Wall_East_WindowSide_South" })
        {
            var side = apartment.transform.Find(sideName);
            if (side != null)
            {
                side.localPosition = new Vector3(side.localPosition.x, 1.1f, side.localPosition.z);
                side.localScale = new Vector3(side.localScale.x, 2.2f, side.localScale.z);
            }
        }
        var sill = apartment.transform.Find("Wall_East_Sill");
        if (sill != null) Object.DestroyImmediate(sill.gameObject);

        var balcony = new GameObject("Balcony");
        balcony.transform.SetParent(apartment.transform, false);
        Cube(balcony.transform, "Balcony_Floor", new Vector3(3.35f, -0.05f, 0f), new Vector3(1.7f, 0.1f, 1.8f), balconyMaterial);
        Cube(balcony.transform, "Balcony_Railing_Front", new Vector3(4.15f, 0.5f, 0f), new Vector3(0.1f, 1f, 1.8f), bathroomWall);
        Cube(balcony.transform, "Balcony_Railing_North", new Vector3(3.35f, 0.5f, 0.85f), new Vector3(1.7f, 1f, 0.1f), bathroomWall);
        Cube(balcony.transform, "Balcony_Railing_South", new Vector3(3.35f, 0.5f, -0.85f), new Vector3(1.7f, 1f, 0.1f), bathroomWall);

        var player = GameObject.Find("Player");
        Debug.Log("Bathroom and balcony added to FatherApartment. Player in scene: " + (player != null) +
                  (player != null ? "; position: " + player.transform.position : ""));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = bathroom;
    }

    [MenuItem("Tools/Greybox/Expand Apartment and Add Kitchen")]
    public static void ExpandApartmentAndAddKitchen()
    {
        const string scenePath = "Assets/Scenes/FatherApartment.unity";
        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var apartment = GameObject.Find("Apartment_Root");
        if (apartment == null)
        {
            Debug.LogError("Apartment_Root was not found in FatherApartment.");
            return;
        }
        var bathroomNook = apartment.transform.Find("Bathroom_Nook");
        if (bathroomNook != null && bathroomNook.Find("Bathroom_Wall_North") == null)
        {
            var bathroomWall = Material("Mat_Bathroom_Grey", 0x8C, 0x91, 0x91);
            Cube(bathroomNook, "Bathroom_Wall_North", new Vector3(-1.775f, 1.1f, 2f), new Vector3(1.35f, 2.2f, 0.1f), bathroomWall);
        }
        if (apartment.transform.Find("Kitchen_OpenPlan") != null)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("Kitchen already exists; ensured Bathroom_Wall_North and skipped duplicate expansion.");
            return;
        }

        // Expand the original 5 x 4 m room eastward to 8 x 5 m (40 m2).
        SetShape(apartment.transform, "Floor", new Vector3(1.5f, -0.05f, 0f), new Vector3(8f, 0.1f, 5f));
        SetShape(apartment.transform, "Ceiling", new Vector3(1.5f, 2.75f, 0f), new Vector3(8f, 0.1f, 5f));
        SetShape(apartment.transform, "Wall_North", new Vector3(1.5f, 1.35f, 2.5f), new Vector3(8f, 2.7f, 0.1f));
        SetShape(apartment.transform, "Wall_South_Left", new Vector3(-1.7f, 1.35f, -2.5f), new Vector3(1.6f, 2.7f, 0.1f));
        SetShape(apartment.transform, "Wall_South_Right", new Vector3(3.2f, 1.35f, -2.5f), new Vector3(4.6f, 2.7f, 0.1f));
        SetShape(apartment.transform, "Wall_West", new Vector3(-2.5f, 1.35f, 0f), new Vector3(0.1f, 2.7f, 5f));
        SetShape(apartment.transform, "Wall_East_Lintel", new Vector3(5.5f, 2.45f, 0f), new Vector3(0.1f, 0.5f, 5f));
        SetShape(apartment.transform, "Wall_East_WindowSide_North", new Vector3(5.5f, 1.1f, 1.65f), new Vector3(0.1f, 2.2f, 1.7f));
        SetShape(apartment.transform, "Wall_East_WindowSide_South", new Vector3(5.5f, 1.1f, -1.65f), new Vector3(0.1f, 2.2f, 1.7f));

        // Keep the balcony attached to the moved east wall and preserve the window lights outside it.
        var balcony = apartment.transform.Find("Balcony");
        if (balcony != null) balcony.localPosition = new Vector3(3f, 0f, 0f);
        SetLocalPosition(apartment.transform, "Light_Window_Neon", new Vector3(6.5f, 1.5f, 0f));
        SetLocalPosition(apartment.transform, "Light_Window_Rain", new Vector3(6f, 2.2f, 0f));

        var cabinet = Material("Mat_Kitchen_Cabinet_Grey", 0x63, 0x68, 0x65);
        var worktop = Material("Mat_Kitchen_Worktop", 0xB9, 0xB7, 0xAD);
        var appliance = Material("Mat_Kitchen_Appliance", 0x2B, 0x2D, 0x30);
        var basin = Material("Mat_Kitchen_Sink", 0x75, 0x80, 0x85);

        // The kitchen stays open to the living area; it occupies the new eastern part of the room.
        var kitchen = new GameObject("Kitchen_OpenPlan");
        kitchen.transform.SetParent(apartment.transform, false);
        Cube(kitchen.transform, "Kitchen_Counter_Base", new Vector3(3.65f, 0.45f, 1.9f), new Vector3(1.9f, 0.9f, 0.65f), cabinet);
        Cube(kitchen.transform, "Kitchen_Worktop", new Vector3(3.65f, 0.93f, 1.9f), new Vector3(1.95f, 0.06f, 0.68f), worktop);
        Cube(kitchen.transform, "Kitchen_UpperCabinet", new Vector3(3.65f, 1.8f, 2.2f), new Vector3(1.9f, 0.7f, 0.38f), cabinet);
        Cube(kitchen.transform, "Kitchen_Fridge", new Vector3(5f, 1f, 1.55f), new Vector3(0.75f, 2f, 0.72f), worktop);
        Cube(kitchen.transform, "Kitchen_Oven", new Vector3(3.1f, 0.5f, 1.55f), new Vector3(0.62f, 0.52f, 0.06f), appliance);
        Cube(kitchen.transform, "Kitchen_Cooktop", new Vector3(3.15f, 0.99f, 1.82f), new Vector3(0.62f, 0.06f, 0.48f), appliance);
        Cube(kitchen.transform, "Kitchen_Sink_Basin", new Vector3(4.1f, 0.99f, 1.9f), new Vector3(0.58f, 0.06f, 0.42f), basin);
        var faucet = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        faucet.name = "Kitchen_Faucet";
        faucet.transform.SetParent(kitchen.transform, false);
        faucet.transform.localPosition = new Vector3(4.1f, 1.1f, 2.12f);
        faucet.transform.localScale = new Vector3(0.025f, 0.12f, 0.025f);
        faucet.GetComponent<Renderer>().sharedMaterial = appliance;

        var player = GameObject.Find("Player");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = kitchen;
        Debug.Log("FatherApartment expanded to 8 x 5 m (40 m2) with open-plan kitchen. Player remains at " +
                  (player != null ? player.transform.position.ToString() : "not found"));
    }

    [MenuItem("Tools/Greybox/Refit Bathroom and Restore Window")]
    public static void RefitBathroomAndRestoreWindow()
    {
        const string scenePath = "Assets/Scenes/FatherApartment.unity";
        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var apartment = GameObject.Find("Apartment_Root");
        if (apartment == null)
        {
            Debug.LogError("Apartment_Root was not found in FatherApartment.");
            return;
        }

        var wall = Material("Mat_Wall_Beige", 0xB8, 0xA9, 0x8A);
        var floor = Material("Mat_Floor_DarkBrown", 0x3A, 0x2A, 0x1E);
        var ceramic = Material("Mat_Bathroom_Ceramic", 0xD4, 0xD5, 0xD0);
        var showerMetal = Material("Mat_Shower_DarkMetal", 0x45, 0x4A, 0x4E);

        // Remove the small cubicle and its detached fixtures; replace it with an attached annex.
        var oldBathroom = apartment.transform.Find("Bathroom_Nook");
        if (oldBathroom != null) Object.DestroyImmediate(oldBathroom.gameObject);
        var oldAnnex = apartment.transform.Find("Bathroom_Annex");
        if (oldAnnex == null)
        {
            // Connect through a 0.9 m doorway in the north wall, centered over the annex floor.
            var north = apartment.transform.Find("Wall_North");
            if (north != null)
            {
                north.name = "Wall_North_Right";
                north.localPosition = new Vector3(2.275f, 1.35f, 2.5f);
                north.localScale = new Vector3(6.45f, 2.7f, 0.1f);
                Cube(apartment.transform, "Wall_North_Left", new Vector3(-2.175f, 1.35f, 2.5f), new Vector3(0.65f, 2.7f, 0.1f), wall);
            }
            var annex = new GameObject("Bathroom_Annex");
            annex.transform.SetParent(apartment.transform, false);
            Cube(annex.transform, "Bathroom_Floor", new Vector3(-1.2f, -0.05f, 3.65f), new Vector3(2.4f, 0.1f, 2.3f), floor);
            Cube(annex.transform, "Bathroom_Ceiling", new Vector3(-1.2f, 2.75f, 3.65f), new Vector3(2.4f, 0.1f, 2.3f), wall);
            Cube(annex.transform, "Bathroom_Wall_West", new Vector3(-2.4f, 1.35f, 3.65f), new Vector3(0.1f, 2.7f, 2.3f), wall);
            Cube(annex.transform, "Bathroom_Wall_East", new Vector3(0f, 1.35f, 3.65f), new Vector3(0.1f, 2.7f, 2.3f), wall);
            Cube(annex.transform, "Bathroom_Wall_North", new Vector3(-1.2f, 1.35f, 4.8f), new Vector3(2.4f, 2.7f, 0.1f), wall);

            // Straight, north-aligned toilet with its tank against the north wall.
            Cube(annex.transform, "Toilet_Base", new Vector3(-1.95f, 0.19f, 4.03f), new Vector3(0.38f, 0.38f, 0.48f), ceramic);
            Cube(annex.transform, "Toilet_Bowl", new Vector3(-1.95f, 0.39f, 3.98f), new Vector3(0.44f, 0.12f, 0.5f), ceramic);
            Cube(annex.transform, "Toilet_Seat", new Vector3(-1.95f, 0.46f, 3.96f), new Vector3(0.4f, 0.04f, 0.44f), showerMetal);
            Cube(annex.transform, "Toilet_Tank", new Vector3(-1.95f, 0.73f, 4.39f), new Vector3(0.4f, 0.55f, 0.18f), ceramic);

            // Shower in the far-east corner and a simple sink on the west side.
            Cube(annex.transform, "Shower_Tray", new Vector3(-0.58f, 0.08f, 4.15f), new Vector3(0.76f, 0.12f, 0.76f), ceramic);
            Cube(annex.transform, "Shower_Screen", new Vector3(-0.12f, 1.1f, 4.15f), new Vector3(0.04f, 2.0f, 0.76f), wall);
            var showerPipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            showerPipe.name = "Shower_Pipe";
            showerPipe.transform.SetParent(annex.transform, false);
            showerPipe.transform.localPosition = new Vector3(-0.2f, 0.95f, 4.42f);
            showerPipe.transform.localScale = new Vector3(0.025f, 0.85f, 0.025f);
            showerPipe.GetComponent<Renderer>().sharedMaterial = showerMetal;
            var showerHead = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            showerHead.name = "Shower_Head";
            showerHead.transform.SetParent(annex.transform, false);
            showerHead.transform.localPosition = new Vector3(-0.25f, 1.75f, 4.4f);
            showerHead.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            showerHead.transform.localScale = new Vector3(0.07f, 0.04f, 0.07f);
            showerHead.GetComponent<Renderer>().sharedMaterial = showerMetal;
            Cube(annex.transform, "Bathroom_Sink_Pedestal", new Vector3(-1.95f, 0.42f, 2.95f), new Vector3(0.42f, 0.84f, 0.4f), ceramic);
            Cube(annex.transform, "Bathroom_Sink_Basin", new Vector3(-1.95f, 0.86f, 2.95f), new Vector3(0.62f, 0.12f, 0.5f), ceramic);
        }

        // Remove the balcony and restore a centered 1.6 m x 1.3 m window opening.
        var balcony = apartment.transform.Find("Balcony");
        if (balcony != null) Object.DestroyImmediate(balcony.gameObject);
        SetShape(apartment.transform, "Wall_East_Lintel", new Vector3(5.5f, 2.45f, 0f), new Vector3(0.1f, 0.5f, 5f));
        SetShape(apartment.transform, "Wall_East_WindowSide_North", new Vector3(5.5f, 1.55f, 1.65f), new Vector3(0.1f, 1.3f, 1.7f));
        var southWindowSide = apartment.transform.Find("Wall_East_WindowSide_South");
        if (southWindowSide == null)
        {
            foreach (Transform child in apartment.transform)
                if (child.name == "Player" && Mathf.Abs(child.localPosition.x - 5.5f) < 0.01f && child.localPosition.z < -1f && child.GetComponent<MeshRenderer>() != null)
                {
                    child.name = "Wall_East_WindowSide_South";
                    southWindowSide = child;
                    break;
                }
        }
        if (southWindowSide != null)
        {
            southWindowSide.localPosition = new Vector3(5.5f, 1.55f, -1.65f);
            southWindowSide.localScale = new Vector3(0.1f, 1.3f, 1.7f);
        }
        var sill = apartment.transform.Find("Wall_East_Sill");
        if (sill == null)
        {
            sill = Cube(apartment.transform, "Wall_East_Sill", new Vector3(5.5f, 0.45f, 0f), new Vector3(0.1f, 0.9f, 5f), wall).transform;
        }
        else
        {
            sill.localPosition = new Vector3(5.5f, 0.45f, 0f);
            sill.localScale = new Vector3(0.1f, 0.9f, 5f);
        }
        if (apartment.transform.Find("Window_Glass") == null)
        {
            var glass = Material("Mat_Window_Glass", 0x6A, 0x7A, 0x8A);
            glass.color = new Color(0.38f, 0.52f, 0.62f, 0.25f);
            glass.SetFloat("_Surface", 1f);
            glass.SetFloat("_Blend", 0f);
            glass.SetFloat("_ZWrite", 0f);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(glass);
            Cube(apartment.transform, "Window_Glass", new Vector3(5.49f, 1.55f, 0f), new Vector3(0.02f, 1.2f, 1.54f), glass);
            var frame = Material("Mat_Window_Frame", 0x4A, 0x33, 0x20);
            Cube(apartment.transform, "Window_Frame_North", new Vector3(5.44f, 1.55f, 0.77f), new Vector3(0.08f, 1.3f, 0.06f), frame);
            Cube(apartment.transform, "Window_Frame_South", new Vector3(5.44f, 1.55f, -0.77f), new Vector3(0.08f, 1.3f, 0.06f), frame);
            Cube(apartment.transform, "Window_Frame_Top", new Vector3(5.44f, 2.17f, 0f), new Vector3(0.08f, 0.06f, 1.6f), frame);
            Cube(apartment.transform, "Window_Frame_Bottom", new Vector3(5.44f, 0.93f, 0f), new Vector3(0.08f, 0.06f, 1.6f), frame);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = GameObject.Find("Bathroom_Annex");
        Debug.Log("Bathroom rebuilt as an attached 2.4 x 2.3 m north annex; toilet aligned; balcony removed and east window restored.");
    }

    private static void SetShape(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var item = parent.Find(name);
        if (item == null) return;
        item.localPosition = position;
        item.localScale = scale;
    }

    private static void SetLocalPosition(Transform parent, string name, Vector3 position)
    {
        var item = parent.Find(name);
        if (item != null) item.localPosition = position;
    }

    private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static Light AddLight(Transform parent, string name, LightType type, Vector3 position, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        return light;
    }

    private static Material Material(string name, byte r, byte g, byte b)
    {
        var path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.color = new Color32(r, g, b, 255);
        EditorUtility.SetDirty(material);
        return material;
    }
}
