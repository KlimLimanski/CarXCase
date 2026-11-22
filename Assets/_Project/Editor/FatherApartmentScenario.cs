using System;
using System.Linq;
using CarXCase.Apartment;
using CarXCase.Precinct;
using DetectiveGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class FatherApartmentScenario
{
    const string Scenes = "Assets/_Project/Scenes/";
    const string Apartment = Scenes + "01_FatherApartment.unity";
    const string RootName = "Father_Apartment_Scenario";
    const string Materials = "Assets/_Project/ArtDirection/ApartmentStory";

    public static bool IsInstalled(Scene scene) =>
        scene.path == Apartment && scene.GetRootGameObjects().Any(root => root.name == RootName);

    [MenuItem("Tools/Brighton/Apartment: update story and evidence")]
    public static void Rebuild()
    {
        var scene = Open(Apartment);
        ApplyApartment(scene);
        Save(scene);
    }

    // Refresh the authored dialogue and deduction data in the story scenes.
    public static void ApplyDestinations()
    {
        var scene = Open(Scenes + "04_Precinct.unity");
        ConfigureBoard(Components<DeductionBoard>(scene).Single());
        foreach (var hud in Components<SimpleUIHUD>(scene)) hud.enableJournal = true;
        Save(scene);

        scene = Open(Scenes + "02_Bar.unity");
        ConfigureBar(Components<Interrogation>(scene).Single());
        Save(scene);
        scene = Open(Scenes + "03_Pawnshop.unity");
        ConfigurePawnshop(Components<Interrogation>(scene).Single());
        Save(scene);
        Open(Apartment);
        var buildScenes = EditorBuildSettings.scenes;
        var existingScenes = buildScenes.Where(entry => AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path)).ToArray();
        if (existingScenes.Length != buildScenes.Length) EditorBuildSettings.scenes = existingScenes;
    }

    static Scene Open(string path)
    {
        var active = SceneManager.GetActiveScene();
        if (active.isDirty && active.path != path)
            throw new InvalidOperationException("Save the open scene before updating this route.");
        return active.path == path ? active : EditorSceneManager.OpenScene(path);
    }

    static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static T[] Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    static void ApplyApartment(Scene scene)
    {
        if (IsInstalled(scene))
        {
            RefinePropsAndLighting(scene);
            return;
        }

        var nodes = Components<Transform>(scene);
        Transform Node(string name) => nodes.First(t => t.name == name);
        var officer = Components<OfficerController>(scene).Single();
        var door = Node("INT_EntryDoor");
        var oldExit = Components<SceneExit>(scene).SingleOrDefault();
        if (oldExit) Object.DestroyImmediate(oldExit);
        foreach (var prior in Components<ClueObject>(scene))
        {
            if (prior.gameObject.name == "RP_Record") continue;
            Object.DestroyImmediate(prior);
        }

        var root = Group(null, RootName, Vector3.zero);
        var walnut = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/ArtDirection/ApartmentWood/Walnut_Furniture_Aged.mat");
        var oak = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/ArtDirection/ApartmentWood/Quiet_Matte_Oak.mat");
        var brick = Mat("Landing_Oil_Paint", new Color(.24f, .23f, .19f), .34f);
        var plaster = Mat("Landing_Smoke_Stained_Plaster", new Color(.37f, .35f, .29f));
        var floor = Mat("Landing_Worn_Terrazzo", new Color(.25f, .238f, .217f), .24f);
        var metal = Mat("Keys_And_Latch_Nickel", new Color(.62f, .60f, .53f), .46f, .68f);
        var brass = Mat("Stairwell_Aged_Brass", new Color(.48f, .345f, .184f), .41f, .54f);
        var dark = Mat("Black_Bakelite", new Color(.078f, .076f, .068f), .29f);
        var ivory = Mat("Yellowed_ABS", new Color(.63f, .60f, .515f), .31f);

        BuildLanding(root, oak, plaster, brick, floor, brass, ivory);
        door.rotation = Quaternion.AngleAxis(-1.6f, Vector3.up) * door.rotation;
        Label(Group(door, "Apartment_2B_Marker", door.InverseTransformPoint(new Vector3(-3.22f, 1.84f, .034f))),
            "2B", Vector3.zero, Quaternion.Inverse(door.rotation) * Quaternion.Euler(0, 180, 0), .028f, new Color(.73f, .57f, .33f));

        var entry = Group(root, "Enter_Father_Apartment", new Vector3(-3.22f, .75f, .47f));
        Trigger(entry, new Vector3(.93f, 1.5f, .25f));
        var threshold = entry.gameObject.AddComponent<ApartmentThreshold>();
        var exitRoot = Group(root, "Choose_Next_Destination", new Vector3(-3.23f, .81f, -.16f));
        Trigger(exitRoot, new Vector3(.7f, 1.35f, .16f));
        var exit = exitRoot.gameObject.AddComponent<SceneExit>();
        exit.targetScene = "02_Bar";
        exit.alternateScene = "03_Pawnshop";
        exit.actionText = "Выбрать, куда поехать";
        exit.requiredClueIds = FatherCase.RequiredClues;
        exit.requiredClues = FatherCase.RequiredClues.Length;
        exit.blockedComment = "";
        exit.enabled = false;

        officer.transform.SetPositionAndRotation(new Vector3(-3.22f, -.47f, 5.99f), Quaternion.Euler(0, 180, 0));
        officer.cameraTransform.SetPositionAndRotation(new Vector3(-3.52f, 1.26f, 6.46f), Quaternion.Euler(6, 180, 0));
        officer.hud.enableJournal = true;
        officer.hud.showInspectionHud = true;
        var settings = new SerializedObject(threshold);
        settings.FindProperty("door").objectReferenceValue = door;
        settings.FindProperty("officer").objectReferenceValue = officer;
        settings.FindProperty("exit").objectReferenceValue = exit;

        var switchRoot = Group(root, "Entry_Bakelite_Light_Switch", new Vector3(-4.35f, 1.28f, -.76f));
        switchRoot.localRotation = Quaternion.Euler(0, 90, 0);
        Box(switchRoot, "Ivory plate", Vector3.zero, new Vector3(.078f, .115f, .016f), ivory);
        var rocker = Box(switchRoot, "Rocker", new Vector3(0, 0, .012f), new Vector3(.035f, .057f, .022f), ivory);
        var switchZone = Group(switchRoot, "Switch_Reach", new Vector3(0, -.45f, .09f));
        Trigger(switchZone, new Vector3(.28f, 1.15f, .30f));
        var lightSwitch = switchRoot.gameObject.AddComponent<ApartmentLightSwitch>();
        lightSwitch.enabled = false;
        var switchSettings = new SerializedObject(lightSwitch);
        switchSettings.FindProperty("rocker").objectReferenceValue = rocker;
        switchSettings.ApplyModifiedPropertiesWithoutUndo();
        settings.FindProperty("lightSwitch").objectReferenceValue = lightSwitch;
        settings.ApplyModifiedPropertiesWithoutUndo();

        BuildKeys(root, door, walnut, metal, brass, dark);
        BuildValuables(root, walnut, metal, ivory, dark);
        BuildMatches(root, ivory, dark);
        BuildAnsweringMachine(root, walnut, ivory, dark);
        var record = Node("RP_Record");
        var recordAim = Group(record, "Record_Aim", record.InverseTransformPoint(new Vector3(-6.42f, .694f, -5.33f)));
        recordAim.rotation = Quaternion.identity;
        var scale = recordAim.lossyScale;
        Trigger(recordAim, new Vector3(.34f / scale.x, .105f / scale.y, .34f / scale.z));
        SetClue(record, 4, new[] { record.GetComponent<Renderer>() });

        // Start with the apartment lights off until the player uses the switch.
        RefinePropsAndLighting(scene);
    }

    static void BuildLanding(Transform parent, Material trim, Material plaster,
        Material wainscot, Material floor, Material metal, Material ivory)
    {
        var p = Group(parent, "Second_Floor_Landing", Vector3.zero);
        Box(p, "Terrazzo landing", new Vector3(-3.22f, -.08f, 1.91f), new Vector3(2.67f, .16f, 3.98f), floor, true);
        for (int i = 0; i < 4; i++)
        {
            float top = -.12f * (i + 1);
            float z = 4.07f + .34f * i;
            Box(p, "Terrazzo stair " + (i + 1), new Vector3(-3.22f, top - .075f, z),
                new Vector3(2.65f, .15f, .342f), floor, true);
            Box(p, "Worn stair nosing", new Vector3(-3.22f, top + .001f, z + .153f),
                new Vector3(2.63f, .012f, .030f), metal);
        }
        Box(p, "Lower landing", new Vector3(-3.22f, -.56f, 6.10f), new Vector3(2.67f, .16f, 1.93f), floor, true);
        Box(p, "Stairwell ceiling", new Vector3(-3.22f, 2.82f, 3.47f), new Vector3(2.93f, .14f, 7.10f), plaster, true);

        for (int side = -1; side <= 1; side += 2)
        {
            float x = -3.22f + side * 1.39f;
            Box(p, "Stairwell wall", new Vector3(x, 1.14f, 3.48f), new Vector3(.14f, 3.34f, 7.04f), plaster, true);
            Box(p, "Landing wainscot", new Vector3(x - side * .073f, .43f, 2.0f), new Vector3(.018f, .86f, 4f), wainscot);
            Box(p, "Chair rail", new Vector3(x - side * .08f, .87f, 2.0f), new Vector3(.040f, .043f, 4f), trim);
            Box(p, "Baseboard", new Vector3(x - side * .080f, .064f, 2.0f), new Vector3(.035f, .127f, 4f), trim);
            for (int i = 0; i < 2; i++)
            {
                var d = Group(p, "Neighbor_Door_" + (i * 2 + (side < 0 ? 1 : 3)), new Vector3(x - side * .095f, 0, 1.33f + 1.72f * i));
                d.localRotation = Quaternion.Euler(0, -side * 90, 0);
            }

            var rail = Box(p, "Stair handrail", new Vector3(x - side * .13f, .69f, 4.56f),
                new Vector3(.054f, .059f, 1.55f), trim);
            rail.localRotation = Quaternion.Euler(20, 0, 0);
        }

        Box(p, "Scuffed threshold", new Vector3(-3.22f, .003f, .10f), new Vector3(1.06f, .014f, .22f), metal);
        var frosted = Mat("Landing_Opal_Glass", ivory.color, .27f, 0, new Color(.54f, .37f, .20f));
        Shape(p, PrimitiveType.Cylinder, "Landing fixture metal base", new Vector3(-3.21f, 2.731f, .94f),
            new Vector3(.43f, .025f, .43f), metal);
        Shape(p, PrimitiveType.Sphere, "Landing fixture opal bowl", new Vector3(-3.21f, 2.69f, .94f),
            new Vector3(.36f, .14f, .36f), frosted);
        LightSource(p, "Light fading down the staircase", new Vector3(-3.20f, 2.32f, 3.22f),
            new Color(.72f, .77f, .88f), .70f, 4.8f, new Vector3(.07f, -1, .56f), 111f);
    }

    static void BuildKeys(Transform root, Transform door, Material wood, Material metal, Material brass, Material dark)
    {
        var lockProp = Group(root, "Evidence_Lock_And_Keys", new Vector3(-3.96f, 1.14f, -.22f));
        Box(lockProp, "Hall key ledge", new Vector3(0, -.044f, .012f), new Vector3(.35f, .064f, .29f), wood);
        Box(lockProp, "Shelf lip", new Vector3(0, -.005f, -.119f), new Vector3(.35f, .022f, .026f), wood);

        var ring = Group(lockProp, "Key ring", new Vector3(.015f, .004f, -.010f));
        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.PI * .2f;
            var link = Box(ring, "Worn wire", new Vector3(Mathf.Sin(angle) * .022f, 0, Mathf.Cos(angle) * .022f),
                new Vector3(.014f, .004f, .004f), metal);
            link.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
        }

        for (int i = 0; i < 3; i++)
        {
            var key = Group(ring, "Notched key", new Vector3((i - 1) * .011f, (i + 1) * .002f, -.026f));
            key.localRotation = Quaternion.Euler(0, (i - 1) * 42, 0);
            Shape(key, PrimitiveType.Cylinder, "Bow", Vector3.zero, new Vector3(.022f, .003f, .024f), i == 2 ? brass : metal);
            Box(key, "Blade", new Vector3(0, 0, -.027f), new Vector3(.008f, .003f, .045f), metal);
            Box(key, "Bit", new Vector3(.005f, 0, -.043f), new Vector3(.008f, .003f, .011f), metal);
        }

        var anchor = Group(ring, "Worn anchor key fob", new Vector3(.064f, 0, .015f));
        anchor.localRotation = Quaternion.Euler(0, -71, 0);
        Shape(anchor, PrimitiveType.Cylinder, "Eye", new Vector3(0, 0, .036f), new Vector3(.015f, .002f, .015f), brass);
        Box(anchor, "Shank", Vector3.zero, new Vector3(.010f, .005f, .074f), brass);
        Box(anchor, "Stock", new Vector3(0, 0, .017f), new Vector3(.046f, .005f, .008f), brass);
        for (int i = -1; i <= 1; i += 2)
        {
            var fluke = Box(anchor, "Fluke", new Vector3(i * .017f, 0, -.030f), new Vector3(.033f, .006f, .009f), brass);
            fluke.localRotation = Quaternion.Euler(0, -i * 23, 0);
            Box(anchor, "Fluke tip", new Vector3(i * .032f, 0, -.019f), new Vector3(.011f, .006f, .017f), brass);
        }

        Trigger(lockProp, new Vector3(.32f, .19f, .29f));
        var plate = Group(lockProp, "Intact strike plate", new Vector3(.187f, -.066f, .173f));
        Box(plate, "Strike face", Vector3.zero, new Vector3(.022f, .125f, .057f), metal);
        Box(plate, "Latch recess", new Vector3(.012f, .007f, 0), new Vector3(.006f, .034f, .026f), dark);
        Trigger(plate, new Vector3(.12f, .25f, .105f));
        var cylinder = Group(door, "Intact lock cylinder", door.InverseTransformPoint(new Vector3(-3.66f, .984f, -.061f)));
        cylinder.rotation = Quaternion.Euler(90, 0, 0);
        var keyway = Shape(cylinder, PrimitiveType.Cylinder, "Clean nickel lock", Vector3.zero,
            new Vector3(.046f, .008f, .046f), metal);
        Box(cylinder, "Keyway", new Vector3(0, -.010f, 0), new Vector3(.005f, .003f, .020f), dark);
        SetClue(lockProp, 0, Renderers(ring).Concat(Renderers(plate)).Append(keyway.GetComponent<Renderer>()).ToArray());
        var originalClue = lockProp.GetComponent<ClueObject>();
        var aim = Group(cylinder, "Cylinder_Aim", Vector3.zero);
        aim.gameObject.AddComponent<ClueAimTarget>().clue = originalClue;
        Trigger(aim, new Vector3(.16f, .15f, .20f));
    }

    static void BuildValuables(Transform root, Material wood, Material metal, Material ivory, Material dark)
    {
        var p = Group(root, "Evidence_Valuables_By_Sony_CRT", new Vector3(-3.99f, .75f, -3.10f));
        Box(p, "Open side cabinet top", new Vector3(-.156f, -.030f, 0), new Vector3(.48f, .037f, .45f), wood);
        for (int side = -1; side <= 1; side += 2)
            Box(p, "Cabinet side", new Vector3(-.156f, -.378f, side * .21f), new Vector3(.45f, .71f, .035f), wood);
        Box(p, "Cabinet back", new Vector3(-.361f, -.38f, 0), new Vector3(.036f, .72f, .42f), wood);
        Box(p, "Cabinet shadow", new Vector3(-.334f, -.36f, 0), new Vector3(.018f, .66f, .36f), dark);
        for (int i = 0; i < 2; i++)
        {
            var drawer = Group(p, "Drawn out drawer", new Vector3(.146f + i * .06f, -.49f + i * .23f, -.02f - i * .02f));
            drawer.localRotation = Quaternion.Euler(-6 + i * 9, i * 12 - 8, i * 3);
            Box(drawer, "Drawer face", new Vector3(.14f, .019f, 0), new Vector3(.026f, .154f, .392f), wood);
            Box(drawer, "Drawer bottom", new Vector3(-.025f, -.043f, 0), new Vector3(.32f, .024f, .367f), wood);
            for (int side = -1; side <= 1; side += 2)
                Box(drawer, "Drawer edge", new Vector3(-.025f, .015f, side * .174f), new Vector3(.32f, .085f, .025f), wood);
            Box(drawer, "Brass drawer pull", new Vector3(.159f, .028f, 0), new Vector3(.026f, .020f, .081f), metal);
            var paper = Box(drawer, "Discarded paper", new Vector3(-.034f, -.026f, .032f), new Vector3(.14f, .006f, .107f), ivory);
            paper.localRotation = Quaternion.Euler(0, -20 + i * 47, 0);
        }

        var watch = Group(p, "Pobeda wristwatch", new Vector3(-.068f, -.012f, -.087f));
        watch.localRotation = Quaternion.Euler(0, 16, 0);
        Box(watch, "Leather strap", new Vector3(0, .005f, 0), new Vector3(.026f, .010f, .144f), dark);
        Shape(watch, PrimitiveType.Cylinder, "Watch case", new Vector3(0, .013f, 0),
            new Vector3(.049f, .006f, .049f), metal);
        Shape(watch, PrimitiveType.Cylinder, "Cream watch dial", new Vector3(0, .020f, 0),
            new Vector3(.041f, .001f, .041f), ivory);
        Box(watch, "Minute hand", new Vector3(.004f, .022f, .004f), new Vector3(.002f, .001f, .020f), dark)
            .localRotation = Quaternion.Euler(0, 54, 0);
        Box(watch, "Hour hand", new Vector3(-.004f, .022f, -.002f), new Vector3(.012f, .001f, .003f), dark);
        var caseProp = Group(p, "Silver cigarette case", new Vector3(-.162f, -.008f, .093f));
        caseProp.localRotation = Quaternion.Euler(0, -17, 0);
        Box(caseProp, "Silver bottom", new Vector3(0, .004f, 0), new Vector3(.091f, .018f, .118f), metal);
        Box(caseProp, "Case seam", new Vector3(0, .006f, 0), new Vector3(.093f, .003f, .120f), dark);
        for (int i = -4; i <= 4; i++)
            Box(caseProp, "Guilloche", new Vector3(i * .008f, .014f, 0), new Vector3(.0017f, .0015f, .10f), ivory);
        var focus = Group(p, "Valuables_Aim", new Vector3(-.050f, .068f, 0));
        Trigger(focus, new Vector3(.36f, .20f, .44f));
        SetClue(p, 1, Renderers(watch).Concat(Renderers(caseProp)).ToArray());
    }

    static void BuildMatches(Transform root, Material ivory, Material dark)
    {
        var p = Group(root, "Evidence_Sterlyad_Matchbox", new Vector3(-8.31f, .587f, -2.803f));
        p.localRotation = Quaternion.Euler(0, 17, 0);
        var paper = Mat("Sterlyad_Matchbox_Worn_Card", new Color(.585f, .47f, .315f), .19f);
        var ink = Mat("Sterlyad_Matchbox_Indigo_Ink", new Color(.095f, .147f, .158f), .16f);
        Box(p, "Printed carton sleeve", Vector3.zero, new Vector3(.108f, .023f, .066f), paper);
        Box(p, "Inner drawer", new Vector3(.016f, 0, 0), new Vector3(.101f, .018f, .056f), ivory);
        Box(p, "Striker strip", new Vector3(-.004f, 0, .033f), new Vector3(.087f, .012f, .002f), dark);
        var fish = Shape(p, PrimitiveType.Sphere, "Sturgeon body", new Vector3(-.003f, .012f, .013f),
            new Vector3(.045f, .002f, .012f), ink);
        Box(fish.parent, "Sturgeon pointed nose", new Vector3(-.030f, .012f, .014f), new Vector3(.017f, .002f, .003f), ink);
        for (int i = -1; i <= 1; i += 2)
            Box(p, "Tail fin", new Vector3(.023f, .012f, .013f + i * .003f), new Vector3(.014f, .002f, .004f), ink)
                .localRotation = Quaternion.Euler(0, i * 36, 0);
        Label(p, "СТЕРЛЯДЬ", new Vector3(0, .014f, -.009f), Quaternion.Euler(90, 0, 0),
            .0105f, new Color(.083f, .121f, .13f));
        Trigger(p, new Vector3(.24f, .20f, .21f));
        SetClue(p, 2, Renderers(p));
    }

    static void BuildAnsweringMachine(Transform root, Material wood, Material ivory, Material dark)
    {
        var p = Group(root, "Evidence_Answering_Machine", new Vector3(-.32f, 1.31f, -2.79f));
        p.localRotation = Quaternion.Euler(0, -90, 0);
        Box(p, "Walnut telephone shelf", new Vector3(0, -.049f, -.055f), new Vector3(.59f, .049f, .51f), wood, true);
        for (int side = -1; side <= 1; side += 2)
            Box(p, "Shelf support", new Vector3(side * .21f, -.135f, -.222f), new Vector3(.042f, .16f, .14f), wood);
        Box(p, "Answering machine shell", Vector3.zero, new Vector3(.325f, .079f, .222f), ivory);
        Box(p, "Lid seam", new Vector3(0, -.006f, 0), new Vector3(.327f, .005f, .224f), dark);
        Box(p, "Microcassette window", new Vector3(-.046f, .041f, -.041f), new Vector3(.164f, .009f, .089f), dark);
        for (int i = 0; i < 7; i++)
            Box(p, "Speaker grille", new Vector3(.077f + i * .008f, .041f, -.055f), new Vector3(.0033f, .004f, .064f), dark);
        var play = Mat("Answering_Machine_Play_Key", new Color(.30f, .43f, .35f), .28f);
        Box(p, "PLAY key", new Vector3(.076f, .046f, .067f), new Vector3(.074f, .024f, .048f), play);
        Label(p, "PLAY", new Vector3(.076f, .059f, .067f), Quaternion.Euler(90, 0, 0), .012f, Color.white);
        Box(p, "STOP key", new Vector3(-.011f, .044f, .067f), new Vector3(.050f, .019f, .044f), dark);
        Label(p, "STOP", new Vector3(-.011f, .055f, .065f), Quaternion.Euler(90, 0, 0), .010f, Color.white);
        var led = Mat("Answering_Machine_Amber_Segment", new Color(.4f, .17f, .05f), .5f, 0, new Color(.65f, .24f, .042f));
        Box(p, "Message display", new Vector3(-.108f, .013f, .113f), new Vector3(.083f, .040f, .010f), dark);
        Label(p, "02", new Vector3(-.108f, .014f, .120f), Quaternion.Euler(0, 180, 0), .024f, new Color(1f, .42f, .115f));
        Shape(p, PrimitiveType.Sphere, "Amber new message LED", new Vector3(-.107f, .043f, .064f),
            new Vector3(.011f, .009f, .011f), led);
        var indicator = LightSource(p, "Blinking message indicator", new Vector3(-.107f, .056f, .064f),
            new Color(1f, .36f, .075f), .17f, .21f, Vector3.zero, 0);
        var aim = Group(p, "Play_Aim", new Vector3(0, .062f, .058f));
        Trigger(aim, new Vector3(.39f, .18f, .26f));
        SetClue(p, 3, Renderers(p).Where(r => r.transform.name != "Walnut telephone shelf" && r is MeshRenderer).ToArray());
        var machine = p.gameObject.AddComponent<ApartmentAnsweringMachine>();
        var audio = p.GetComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = .65f;
        audio.minDistance = .7f;
        audio.maxDistance = 5f;
        var serialized = new SerializedObject(machine);
        serialized.FindProperty("messageIndicator").objectReferenceValue = indicator;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void RefinePropsAndLighting(Scene scene)
    {
        var scenario = scene.GetRootGameObjects().First(g => g.name == RootName).transform;
        var nodes = Components<Transform>(scene);
        var lighting = scenario.Find("Window_Reflected_Light");
        if (!lighting)
        {
            lighting = Group(scenario, "Window_Reflected_Light", Vector3.zero);
            var cool = new Color(.59f, .74f, .92f);
            LightSource(lighting, "Street bounce on the telephone shelf", new Vector3(-1.36f, 1.82f, -3.44f),
                cool, 1.55f, 2.98f, new Vector3(1f, -.39f, .48f), 97f).shadows = LightShadows.None;
            LightSource(lighting, "Bedroom window spill toward the nightstand", new Vector3(-8.35f, 1.51f, -3.47f),
                cool, 2.25f, 2.61f, new Vector3(.045f, -.8f, .76f), 91f).shadows = LightShadows.None;
        }

        ApartmentStoryArt.Apply(scene, scenario, nodes);
        ConfigureLighting(scene);
    }

    public static void ConfigureLighting(Scene scene)
    {
        var allLights = Components<Light>(scene);
        var window = allLights.First(l => l.name == "Window_Overcast");
        var street = allLights.First(l => l.name == "Street_Sodium_Bedroom");
        var scenario = scene.GetRootGameObjects().First(g => g.name == RootName).transform;
        foreach (var light in allLights)
        {
            if (light == window || light == street || light.transform.IsChildOf(scenario)) continue;
            light.enabled = false;
        }

        window.enabled = true;
        window.intensity = 3.2f;
        window.range = 6.5f;
        window.spotAngle = 114f;
        window.innerSpotAngle = 76f;
        window.transform.rotation = Quaternion.LookRotation(new Vector3(-1, -.22f, .28f));
        street.enabled = true;
        street.intensity = 10.6f;
        street.color = new Color(.82f, .88f, 1f);
        var officer = Components<OfficerController>(scene).Single();
        HeroSetup.ConfigureApartmentFill(officer.transform);
        ApartmentStoryArt.ConfigureSwitchLighting(scenario.Find("Entry_Bakelite_Light_Switch"),
            allLights, Components<Renderer>(scene));
    }

    public static void ConfigureBoard(DeductionBoard board)
    {
        board.fallbackClues = FatherCase.Evidence.Select(e => new DeductionBoard.ClueDef
        {
            id = e.id, title = e.title, description = e.id == FatherCase.Messages
                ? "19:15 — портовый гудок. 21:40 — угроза Дмитрию и пропавшие бумаги с двенадцатого пирса."
                : e.observation
        }).ToArray();
        board.requiredConclusions = new[] { FatherCase.Bar, FatherCase.Pawnshop };
        board.conclusionsForDecision = 2;
        board.deductions = new[]
        {
            new DeductionBoard.Deduction { id = FatherCase.Bar, clueA = FatherCase.Matches, clueB = FatherCase.Messages,
                title = "Бар Дмитрия", text = "Спички из «Стерляди», и в записи назвали Дмитрия. Надо спросить Митьку, что он знает об отце." },
            new DeductionBoard.Deduction { id = FatherCase.Pawnshop, clueA = FatherCase.Valuables, clueB = FatherCase.Messages,
                title = "Ломбард отца", text = "Часы и телевизор оставили. В сообщении говорили о товаре и бумагах. Борис мог видеть, что отец принял в ломбарде." },
            new DeductionBoard.Deduction { id = FatherCase.Guest, clueA = FatherCase.Door, clueB = FatherCase.Valuables,
                title = "Отец мог знать гостя", text = "Замок цел. Возможно, отец впустил кого-то знакомого, а обыск начался уже после." },
        };
        board.options = new[]
        {
            new DeductionBoard.Option { label = "Бар «Стерлядь»", requiresConclusion = FatherCase.Bar, targetScene = "02_Bar",
                outcome = "Спросить Дмитрия, когда он видел отца и что скрывает." },
            new DeductionBoard.Option { label = "Ломбард «Золотой Якорь»", requiresConclusion = FatherCase.Pawnshop, targetScene = "03_Pawnshop",
                outcome = "Найти Бориса Марковича и узнать, какую сделку заключил отец." },
        };
    }

    public static void ConfigureBar(Interrogation talk)
    {
        ConfigureTrust(talk, 40, 25, PortLead.BarFinished, PortLead.AskWitness);
        talk.interruptAtFailureThreshold = true;
        talk.suspectName = "ДМИТРИЙ";
        talk.openingLine = "Хватит юлить, Дим. Я был у отца. Замок цел, телек и часы на месте, а на тумбочке — твои спички.\nПо телефону ты слишком торопился меня слить. Что вы с отцом не поделили?";
        talk.successText = "У отца дома меня не было, клянусь. Пять дней назад он зашёл белый как мел.\nХотел сбыть старую ювелирку без бумаг через свой ломбард, а товар оказался краденым общаком. На него повесили пятнадцать тысяч.\nЗа этим стоят люди с причалов Шипсхед-Бей. Главный — из наших, брайтоновских. Если отец не отдал деньги, ищи его в старом ангаре. Номер четыре.";
        talk.partialReply = "Я не был у него, клянусь! Отца подставили на сделке с краденым товаром и повесили огромный долг.\nВчера сюда приходили двое, искали его. Куда увезли — не знаю. Уходи, Лёш, пока нас вместе не засветили.";
        talk.shutdownText = "Пошёл ты к чёрту, Лёша! Я тебе не подозреваемый. Разговор окончен.";
        talk.followUpThought = "Девушка в конце стойки видела, кто приходил. С ней и поговорю.";
        talk.failureThought = "Она слышала скандал и со мной говорить не станет. Надо проверить, что Дмитрий оставил у кассы.";
        talk.grantId = PortLead.Dmitry;
        talk.grantTitle = "Наводка Дмитрия";
        talk.grantText = "Отца подставили с краденым товаром и потребовали пятнадцать тысяч. Дмитрий указал на порт Шипсхед-Бей, старый ангар №4.";
        talk.revealHarborCode = true;
        talk.successText += "\nНа воротах замок. Набери " + CarXCase.Harbor.HarborGate.AccessCode + ".";
        talk.nodes = new[]
        {
            Round("family", new[] {"И что с того? Он заходил три дня назад. У меня этих коробков целая ваза, бери кто хочет. Не делай из меня подозреваемого."},
                Answer(0, "Напомнить о семье", "Это же наш отец. Если он влип — это и моя проблема. Никому не скажу. Просто дай зацепку.", 20, "evidence",
                    "Чёрт с тобой, Лёш… Не хотел тебя втягивать. Думал, само рассосётся."),
                Answer(1, "Давить должностью", "Если с отцом беда, твой бар первым пойдёт под проверку убойного.", 5, "evidence",
                    "Тише ты. Мне тут копы не нужны, проблем и так по горло."),
                Answer(2, "Пригрозить", "Ты сын ему или кто? Говори, где он, или я тебе бар разнесу!", -15, "END")),
            Round("evidence", new[] {"Он правда заходил. Просил денег, нервничал. Я дал полтинник, он ушёл. Может, просто уехал проветриться?"},
                Answer(0, "Надавить на совесть", "А если его сейчас где-то прессуют, а ты промолчал, прикрывая свою шкуру? Сможешь с этим жить?", 25, "last",
                    "Думаешь, мне плевать? Я вчера ему набирал, хотел предупредить, чтоб никому не открывал, а он уже трубку не брал."),
                Answer(1, "Дверь и кассета", "У него погром, замок цел, ценности на месте. А на кассете грохот и угрозы. Кто к нему ходит?", 20, "last",
                    "Значит, всё-таки пришли. Я же говорил ему не связываться с этими людьми…", FatherCase.Messages),
                Answer(2, "Блефовать о свидетелях", "Вчера видели твою машину у его дома. Не усугубляй.", -10, "last",
                    "Моя тачка весь вечер у чёрного хода стояла, полрайона видело. Не бери меня на понт.")),
            Round("last", new[] {"На Брайтоне теперь другие правила. Если я рот открою — завтра нас обоих в заливе найдут."},
                Answer(0, "Напомнить о прошлом", "В девяносто пятом я тебя от ребят с Оушен-Парквей отмазал. Теперь моя очередь за отцом идти. Не бросай меня одного.", 20, "END"),
                Answer(1, "Пообещать защиту", "Никто не узнает, откуда наводка. Скажи, кому он задолжал, и я сам всё решу.", 15, "END"),
                Answer(2, "Пригрозить арестом", "Либо говоришь, либо оформляю тебя как соучастника похищения.", -20, "END"))
        };
    }

    public static void ConfigurePawnshop(Interrogation talk)
    {
        ConfigureTrust(talk, 45, 30, PortLead.PawnshopFinished, PortLead.CheckSafe);
        talk.interruptAtFailureThreshold = false;
        talk.suspectName = "БОРИС МАРКОВИЧ";
        talk.openingLine = "";
        talk.successText = "Позавчера они ворвались перед моей сменой. Сказали, Фёдор должен пятнадцать тысяч и уже у них «в гостях».\nСтарший был коренастый, со шрамом на щеке. Он велел гнать машину обратно на причал, пока охрана не сменилась.\nЭто Шипсхед-Бей, Лёша. Портовая мафия держит там старый ангар. Номер четыре.";
        talk.partialReply = "Куда увезли — не знаю. Но позавчера Фёдору принесли конверт. Он побелел, когда прочёл записку, и спрятал её в сейф. Проверь там.";
        talk.shutdownText = "Ничего не знаю и знать не хочу! У меня внуки. Уходи отсюда!";
        talk.followUpThought = "Нужно найти сейф в глубине ломбарда. Посмотрим, что отец там оставил.";
        talk.failureThought = "Придётся искать самому. Если остались бумаги по сделке, отец мог спрятать их в сейфе.";
        talk.grantId = PortLead.Boris;
        talk.grantTitle = "Прямая наводка Бориса";
        talk.grantText = "Отец у людей со старых причалов в Шипсхед-Бей. Борис указал на ангар №4 и описал коренастого главаря со шрамом.";
        talk.revealHarborCode = false;
        talk.nodes = new[]
        {
            Round("approach", new[] {"Лёша? Господи, ты чего в такой час? Я думал, опять те ребята вернулись. Если Фёдора ищешь — его сегодня не было. Я вторую смену подряд стою."},
                Answer(0, "Напомнить о прошлом", "Вы на отца двадцать лет работаете, он вас как брата держал. Скажите честно, что за ребята приходили?", 20, "evidence",
                    "Эх, Фёдор Павлович… Говорил я ему не связываться. Слишком доверчивый стал под старость."),
                Answer(1, "Поговорить спокойно", "Я без шума, дядь Борь. Отец со вчерашнего утра не берёт трубку. Расскажите, что произошло.", 15, "evidence",
                    "Страшно говорить-то… Ладно, только тихо. Стены тонкие."),
                Answer(2, "Пригрозить проверкой", "Через лавку левый товар прошёл. Говорите всё, или я опечатаю ломбард!", -15, "evidence",
                    "Не ори на меня, Лёша! Я простой приёмщик, ни в чём не виноват!")),
            Round("evidence", new[] {"Налоги и аренда задушили. Фёдор взял у одного человека партию старых часов и ювелирки без бумаг.",
                    "Через два дня приехали амбалы. Товар, говорят, краденый из общака. И знали все входы и выходы, как будто кто-то из своих навёл."},
                Answer(0, "Молчать опаснее", "Они уже были здесь и от вас не отстанут. Помогите мне найти их первым, ради отца и ломбарда.", 20, "END",
                    "Я вчера всю ночь не спал, боялся, что за мной придут…"),
                Answer(1, "Предъявить улики", "У него дома всё перевёрнуто. На кассете угрозы и разговор о бумагах. Что отец мог спрятать?", 15, "END",
                    "Так вот зачем они вчера сюда врывались! Искали бумаги по сделке в кассе или в сейфе.", FatherCase.Messages),
                Answer(2, "Обвинить Бориса", "Отец влип, а вы тут сидите целый. Это вы его сдали?", -10, "END",
                    "Постыдился бы, Лёша. Я отцу двадцать лет верой и правдой служил."))
        };
    }

    static void ConfigureTrust(Interrogation talk, int initialTrust, int failureThreshold, string completed, string followUp)
    {
        talk.useTrust = true;
        talk.probability = initialTrust;
        talk.successThreshold = 70;
        talk.failureThreshold = failureThreshold;
        talk.choiceTime = 14f;
        talk.completedFlag = completed;
        talk.followUpFlag = followUp;
        talk.returnScene = "05_BrightonHarbor";
        talk.autoTravelDelay = 0f;
        talk.travelLabel = "Поехать в Шипсхед-Бей";
        talk.recoveryClueId = "";
    }

    static Interrogation.Node Round(string id, string[] lines, params Interrogation.Choice[] choices) =>
        new Interrogation.Node { id = id, lines = lines, choices = choices };

    static Interrogation.Choice Answer(int slot, string label, string line, int trust, string next, string reply = "", string requires = "") =>
        new Interrogation.Choice { slot = slot, label = label, heroLine = line, probability = trust, next = next, reply = reply, requires = requires };



    static ClueObject SetClue(Transform parent, int i, Renderer[] renderers)
    {
        var data = FatherCase.Evidence[i];
        var clue = parent.GetComponent<ClueObject>();
        if (!clue) clue = parent.gameObject.AddComponent<ClueObject>();
        clue.enabled = true;
        clue.addToJournal = true;
        clue.clueId = data.id;
        clue.clueTitle = data.title;
        clue.clueDescription = data.observation;
        clue.journalThought = data.thought;
        clue.inspectionComment = data.thought;
        clue.targetRenderer = renderers.FirstOrDefault();
        clue.highlightRenderers = renderers;
        return clue;
    }

    static Renderer[] Renderers(Transform root) =>
        root.GetComponentsInChildren<Renderer>().Where(renderer => !renderer.GetComponent<TextMesh>()).ToArray();

    static Transform Group(Transform parent, string name, Vector3 position)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = position;
        return t;
    }

    static Transform Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool solid = false) =>
        Shape(parent, PrimitiveType.Cube, name, center, size, material, solid);

    static Transform Shape(Transform parent, PrimitiveType type, string name, Vector3 center, Vector3 size, Material material, bool solid = false)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = center;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj.transform;
    }

    static void Trigger(Transform root, Vector3 size)
    {
        var collider = root.gameObject.AddComponent<BoxCollider>();
        collider.size = size;
        collider.isTrigger = true;
    }

    static Material Mat(string name, Color color, float smooth = .24f, float metallic = 0, Color? emission = null)
    {
        string path = Materials + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        if (!AssetDatabase.IsValidFolder(Materials))
            AssetDatabase.CreateFolder("Assets/_Project/ArtDirection", "ApartmentStory");
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.SetColor("_EmissionColor", emission.Value);
            m.EnableKeyword("_EMISSION");
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void Label(Transform parent, string text, Vector3 position, Quaternion rotation, float size, Color color)
    {
        var label = Group(parent, text, position).gameObject.AddComponent<TextMesh>();
        label.transform.localRotation = rotation;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.fontSize = 64;
        label.characterSize = size;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontStyle = FontStyle.Bold;
        label.color = color;
        label.text = text;
    }

    static Light LightSource(Transform parent, string name, Vector3 position, Color color, float intensity,
        float range, Vector3 direction, float angle)
    {
        var light = Group(parent, name, position).gameObject.AddComponent<Light>();
        light.type = angle > 0 ? LightType.Spot : LightType.Point;
        if (angle > 0)
        {
            light.transform.rotation = Quaternion.LookRotation(direction);
            light.spotAngle = angle;
            light.innerSpotAngle = angle * .63f;
            light.shadows = LightShadows.Soft;
        }
        light.intensity = intensity;
        light.range = range;
        light.color = color;
        return light;
    }
}
