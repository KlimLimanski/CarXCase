using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DetectiveGame.Core;
using CarXCase.Hero;
using CarXCase.Precinct;

/// Builds the greybox precinct (02_Precinct) and wires the apartment exit.
/// Commands (HeroSetup/request.txt): "exit", "precinct", "capture_precinct".
public static class PrecinctBuilder
{
    const string Scene = "Assets/_Project/Scenes/02_Precinct.unity";
    const string FlatScene = "Assets/_Project/Scenes/01_FatherApartment.unity";
    const string MatDir = "Assets/_Project/Materials/Precinct";

    [MenuItem("Tools/Precinct/Build precinct scene")] static void MenuBuild() => HeroSetup.RunCommand("precinct");
    [MenuItem("Tools/Precinct/Wire apartment exit + clues")] static void MenuExit() => HeroSetup.RunCommand("exit");

    public static void SetupApartmentExit(StringBuilder log)
    {
        FatherApartmentScenario.Rebuild();
        log.AppendLine("Updated apartment threshold, exit and evidence.");
    }

    // ================================================================== precinct
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    internal static Material M(string name, Color c, float smooth = .25f, float metal = 0, Color? emission = null)
    {
        if (mats.TryGetValue(name, out var m) && m) return m;
        string path = MatDir + "/" + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Precinct");
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        EditorUtility.SetDirty(m);
        mats[name] = m; return m;
    }

    internal static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = center; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    internal static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 center, Vector3 size, Material mat, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = center; go.transform.localScale = size;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    internal static Transform Group(Transform parent, string name, Vector3 pos, float yaw = 0)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false);
        t.localPosition = pos; t.localEulerAngles = new Vector3(0, yaw, 0); return t;
    }

    // wall along X or Z with optional door openings: openings given as (center along wall, width, height)
    internal static void Wall(Transform p, string name, Vector3 a, Vector3 b, float h, float thick, Material mat, params Vector3[] openings)
    {
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float start = alongX ? Mathf.Min(a.x, b.x) : Mathf.Min(a.z, b.z), end = alongX ? Mathf.Max(a.x, b.x) : Mathf.Max(a.z, b.z);
        float fixedC = alongX ? a.z : a.x;
        var cuts = openings.OrderBy(o => o.x).ToList();
        float cur = start; int i = 0;
        void Seg(float s0, float s1, float y0, float y1, string n)
        {
            if (s1 - s0 < 0.01f || y1 - y0 < 0.01f) return;
            float mid = (s0 + s1) / 2, len = s1 - s0;
            var c = alongX ? new Vector3(mid, (y0 + y1) / 2, fixedC) : new Vector3(fixedC, (y0 + y1) / 2, mid);
            var s = alongX ? new Vector3(len, y1 - y0, thick) : new Vector3(thick, y1 - y0, len);
            Box(p, n, c, s, mat);
        }
        foreach (var o in cuts)
        {
            float o0 = o.x - o.y / 2, o1 = o.x + o.y / 2;
            Seg(cur, o0, 0, h, name + "_" + i++);
            Seg(o0, o1, o.z, h, name + "_Header" + i++);
            cur = o1;
        }
        Seg(cur, end, 0, h, name + "_" + i);
    }

    internal static Transform Desk(Transform p, string name, Vector3 pos, float yaw, bool crt = true, bool chair = true)
    {
        var d = Group(p, name, pos, yaw);
        var wood = M("Desk_Wood", new Color(.33f, .24f, .17f), .35f);
        var metal = M("Desk_Metal", new Color(.42f, .45f, .44f), .45f, .3f);
        Box(d, "Top", new Vector3(0, .74f, 0), new Vector3(1.5f, .05f, .75f), wood);
        Box(d, "Pedestal", new Vector3(.5f, .36f, 0), new Vector3(.42f, .72f, .7f), metal, false);
        Box(d, "Leg", new Vector3(-.7f, .36f, 0), new Vector3(.05f, .72f, .68f), metal, false);
        Box(d, "Modesty", new Vector3(-.1f, .45f, -.33f), new Vector3(1.2f, .5f, .02f), metal, false);
        var plastic = M("CRT_Plastic", new Color(.78f, .76f, .68f), .3f);
        if (crt)
        {
            Box(d, "CRT", new Vector3(-.25f, .96f, .05f), new Vector3(.4f, .36f, .38f), plastic, false);
            Box(d, "CRT_Screen", new Vector3(-.25f, .97f, -.145f), new Vector3(.32f, .26f, .01f), M("CRT_Glow", new Color(.05f, .09f, .07f), .8f, 0, new Color(.05f, .18f, .12f)), false);
            Box(d, "Keyboard", new Vector3(-.25f, .78f, -.25f), new Vector3(.42f, .03f, .15f), plastic, false);
        }
        var paper = M("Paper", new Color(.9f, .88f, .8f), .1f);
        Box(d, "Papers", new Vector3(.35f, .775f, -.05f), new Vector3(.3f, .02f, .24f), paper, false);
        Box(d, "Phone", new Vector3(.6f, .79f, .2f), new Vector3(.18f, .06f, .2f), M("Phone_Black", new Color(.05f, .05f, .05f), .5f), false);
        if (chair)
        {
            var c = Group(d, "Chair", new Vector3(-.1f, 0, -.65f), UnityEngine.Random.Range(-25f, 25f));
            var fabric = M("Chair_Fabric", new Color(.12f, .14f, .16f), .15f);
            Box(c, "Seat", new Vector3(0, .47f, 0), new Vector3(.48f, .08f, .46f), fabric, false);
            Box(c, "Back", new Vector3(0, .82f, -.22f), new Vector3(.46f, .55f, .06f), fabric, false);
            Box(c, "Post", new Vector3(0, .24f, 0), new Vector3(.05f, .45f, .05f), metal, false);
            Box(c, "Base", new Vector3(0, .04f, 0), new Vector3(.55f, .04f, .55f), metal, false);
        }
        return d;
    }

    internal static Transform Person(Transform p, string name, Vector3 pos, float yaw, Color shirt, bool sitting = false, bool cap = false)
    {
        var g = Group(p, name, pos, yaw);
        var skin = M("NPC_Skin", new Color(.62f, .47f, .36f), .3f);
        var cloth = M("NPC_" + ColorUtility.ToHtmlStringRGB(shirt), shirt, .2f);
        var pants = M("NPC_Pants", new Color(.08f, .09f, .12f), .2f);
        float y0 = sitting ? -.35f : 0;
        if (!sitting) { Box(g, "Leg_L", new Vector3(-.1f, .45f, 0), new Vector3(.15f, .9f, .18f), pants, false); Box(g, "Leg_R", new Vector3(.1f, .45f, 0), new Vector3(.15f, .9f, .18f), pants, false); }
        else { Box(g, "Thighs", new Vector3(0, .52f, .2f), new Vector3(.36f, .15f, .45f), pants, false); Box(g, "Shins", new Vector3(0, .25f, .42f), new Vector3(.34f, .5f, .14f), pants, false); }
        Box(g, "Torso", new Vector3(0, 1.22f + y0, 0), new Vector3(.44f, .62f, .25f), cloth, false);
        Box(g, "Arm_L", new Vector3(-.28f, 1.15f + y0, sitting ? .12f : 0), new Vector3(.11f, .58f, .13f), cloth, false);
        Box(g, "Arm_R", new Vector3(.28f, 1.15f + y0, sitting ? .12f : 0), new Vector3(.11f, .58f, .13f), cloth, false);
        Prim(g, PrimitiveType.Sphere, "Head", new Vector3(0, 1.68f + y0, 0), new Vector3(.22f, .26f, .23f), skin);
        if (cap) Prim(g, PrimitiveType.Cylinder, "Cap", new Vector3(0, 1.83f + y0, .01f), new Vector3(.25f, .04f, .26f), M("NPC_Navy", new Color(.07f, .1f, .2f), .3f));
        var col = g.gameObject.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, .9f, 0); col.height = 1.8f; col.radius = .25f;
        return g;
    }

    internal static void Light(Transform p, string name, Vector3 pos, Color c, float intensity, float range, bool shadows = false, bool fixture = true)
    {
        var t = new GameObject(name).transform; t.SetParent(p, false); t.localPosition = pos;
        var l = t.gameObject.AddComponent<UnityEngine.Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        if (fixture)
            Box(p, name + "_Fixture", pos + Vector3.up * .12f, new Vector3(1.2f, .05f, .3f), M("Fluorescent", new Color(.95f, .97f, .92f), .5f, 0, new Color(1.6f, 1.7f, 1.55f)), false);
    }

    internal static TextMesh Text(Transform p, string name, string text, Vector3 pos, float yaw, float size, Color c)
    {
        var tm = new GameObject(name).AddComponent<TextMesh>();
        tm.transform.SetParent(p, false); tm.transform.localPosition = pos; tm.transform.localEulerAngles = new Vector3(0, yaw, 0);
        tm.text = text; tm.characterSize = size; tm.fontSize = 64; tm.anchor = TextAnchor.MiddleCenter; tm.color = c;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.GetComponent<MeshRenderer>().sharedMaterial = TextMat() ?? tm.font.material;
        return tm;
    }

    internal static Material TextMat() => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BrightonIntro/TextDepth_LegacyRuntime.mat");

    public static void BuildPrecinct(StringBuilder log)
    {
        mats.Clear();
        UnityEngine.Random.InitState(1999);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Precinct_Greybox").transform;
        var arch = Group(root, "Architecture", Vector3.zero);
        var props = Group(root, "Props", Vector3.zero);
        var npcs = Group(root, "NPCs", Vector3.zero);
        var lights = Group(root, "Lighting", Vector3.zero);

        var floorM = M("Linoleum", new Color(.36f, .37f, .33f), .45f);
        var wallM = M("Wall_Green", new Color(.55f, .6f, .52f), .2f);
        var wallLow = M("Wall_Wainscot", new Color(.3f, .34f, .3f), .3f);
        var ceilM = M("Ceiling_Tile", new Color(.82f, .82f, .78f), .1f);
        var brick = M("Facade_Brick", new Color(.42f, .22f, .16f), .15f);
        var concrete = M("Sidewalk", new Color(.48f, .47f, .45f), .1f);
        var asphalt = M("Asphalt", new Color(.12f, .12f, .13f), .3f);
        var glass = M("Glass_Dark", new Color(.15f, .2f, .22f), .9f);
        var doorM = M("Door_Wood", new Color(.36f, .25f, .16f), .4f);
        const float H = 3.2f;

        // ---------------- entrance (no street: the doorway opens onto bright daylight)
        Box(arch, "Floor", new Vector3(0, -.05f, 12), new Vector3(16, .1f, 24), floorM);
        Box(arch, "Ceiling", new Vector3(0, H + .05f, 12), new Vector3(16, .1f, 24), ceilM);
        Wall(arch, "Facade", new Vector3(-8, 0, -.15f), new Vector3(8, 0, -.15f), H, .3f, brick, new Vector3(0, 2.2f, 2.5f));
        var daylight = M("Daylight", new Color(1f, .97f, .9f), 0, 0, new Color(3.2f, 3.0f, 2.6f));
        Box(arch, "Vestibule_Floor", new Vector3(0, -.05f, -1.1f), new Vector3(2.6f, .1f, 1.9f), floorM);
        Box(arch, "Vestibule_L", new Vector3(-1.25f, 1.4f, -1.1f), new Vector3(.1f, 2.8f, 1.9f), brick);
        Box(arch, "Vestibule_R", new Vector3(1.25f, 1.4f, -1.1f), new Vector3(.1f, 2.8f, 1.9f), brick);
        Box(arch, "Vestibule_Top", new Vector3(0, 2.8f, -1.1f), new Vector3(2.6f, .1f, 1.9f), brick);
        Box(arch, "Daylight_Curtain", new Vector3(0, 1.4f, -2.05f), new Vector3(2.4f, 2.8f, .05f), daylight, false);
        foreach (float wx in new[] { -4.5f, 4.5f })
        {
            Box(arch, "Window_Frosted_" + wx, new Vector3(wx, 1.75f, .01f), new Vector3(1.6f, 1.5f, .02f), M("Frosted_Day", new Color(.85f, .88f, .9f), .2f, 0, new Color(1.2f, 1.2f, 1.15f)), false);
            for (int i = 0; i < 9; i++) Box(arch, "Window_Blind_" + wx + "_" + i, new Vector3(wx, 1.1f + i * .16f, .04f), new Vector3(1.6f, .035f, .02f), M("Blinds", new Color(.75f, .72f, .62f), .3f), false);
        }
        Light(lights, "Light_Daylight", new Vector3(0, 1.8f, -.8f), new Color(1f, .93f, .8f), 3.0f, 7, true, false);
        Text(arch, "Lobby_Sign", "13-Й УЧАСТОК", new Vector3(3.6f, 2.6f, 3.2f), 180, .045f, new Color(.95f, .9f, .7f));
        var establishing = Group(root, "Shot_Establishing", new Vector3(2.7f, 1.55f, 5.2f));
        establishing.rotation = Quaternion.LookRotation(new Vector3(0, 1.4f, 0) - establishing.position);

        // front doors (pivots at the hinges)
        var doorL = Group(arch, "FrontDoor_L_Pivot", new Vector3(-1.0f, 0, 0));
        Box(doorL, "Leaf", new Vector3(.5f, 1.1f, 0), new Vector3(.98f, 2.18f, .06f), doorM);
        Box(doorL, "Glass", new Vector3(.5f, 1.45f, -.035f), new Vector3(.6f, .9f, .01f), glass, false);
        var doorR = Group(arch, "FrontDoor_R_Pivot", new Vector3(1.0f, 0, 0));
        Box(doorR, "Leaf", new Vector3(-.5f, 1.1f, 0), new Vector3(.98f, 2.18f, .06f), doorM);
        Box(doorR, "Glass", new Vector3(-.5f, 1.45f, -.035f), new Vector3(.6f, .9f, .01f), glass, false);

        // ---------------- shell
        Wall(arch, "Wall_West", new Vector3(-8, 0, 0), new Vector3(-8, 0, 24), H, .2f, wallM);
        Wall(arch, "Wall_East", new Vector3(8, 0, 0), new Vector3(8, 0, 24), H, .2f, wallM);
        Wall(arch, "Wall_Back", new Vector3(-8, 0, 24), new Vector3(8, 0, 24), H, .2f, wallM);
        Box(arch, "Wainscot_West", new Vector3(-7.88f, .5f, 12), new Vector3(.04f, 1f, 24), wallLow, false);
        Box(arch, "Wainscot_East", new Vector3(7.88f, .5f, 12), new Vector3(.04f, 1f, 24), wallLow, false);

        // lobby: front desk + bench + counter to the bullpen
        var deskWood = M("FrontDesk_Wood", new Color(.28f, .19f, .12f), .45f);
        Box(props, "FrontDesk", new Vector3(3.6f, .6f, 3.6f), new Vector3(3.2f, 1.2f, .7f), deskWood);
        Box(props, "FrontDesk_Top", new Vector3(3.6f, 1.22f, 3.55f), new Vector3(3.3f, .05f, .85f), M("Counter_Top", new Color(.5f, .48f, .42f), .5f), false);
        Box(props, "FrontDesk_Side", new Vector3(2.05f, .6f, 4.5f), new Vector3(.7f, 1.2f, 1.8f), deskWood);
        Box(props, "Bench_Lobby", new Vector3(-5.5f, .45f, 2.2f), new Vector3(2.6f, .1f, .5f), doorM);
        Box(props, "Bench_Lobby_Back", new Vector3(-5.5f, .85f, 1.98f), new Vector3(2.6f, .7f, .06f), doorM);
        Box(props, "Railing_L", new Vector3(-4.6f, .5f, 6.2f), new Vector3(6.4f, 1f, .1f), deskWood);
        Box(props, "Railing_R", new Vector3(5.0f, .5f, 6.2f), new Vector3(5.6f, 1f, .1f), deskWood);
        Box(props, "Wanted_Board", new Vector3(-7.86f, 1.6f, 4.0f), new Vector3(.04f, 1f, 1.6f), M("Cork", new Color(.6f, .45f, .28f), .1f), false);
        Box(props, "Flag_Pole", new Vector3(6.8f, 1.1f, 1.2f), new Vector3(.05f, 2.2f, .05f), M("Brass", new Color(.7f, .58f, .3f), .6f, .8f), false);
        Person(npcs, "Desk_Sergeant", new Vector3(3.6f, 0, 4.4f), 180, new Color(.12f, .18f, .38f), false, true);
        Person(npcs, "Civilian_Waiting", new Vector3(-6.0f, .1f, 2.3f), 0, new Color(.45f, .3f, .2f), true);

        // bullpen desks (aisle at x = 0 stays clear)
        int n = 0;
        foreach (float z in new[] { 9f, 12f, 15f })
            foreach (float x in new[] { -5.4f, -2.4f, 2.4f, 5.4f })
                Desk(props, "Desk_" + n++, new Vector3(x, 0, z), 0);
        foreach (float z in new[] { 8f, 11f, 14f })
            for (int i = 0; i < 3; i++)
                Box(props, "FileCabinet_W" + z + "_" + i, new Vector3(-7.6f, .66f, z + i * .5f), new Vector3(.5f, 1.32f, .48f), M("Cabinet_Grey", new Color(.45f, .47f, .45f), .4f, .2f));
        Box(props, "WaterCooler", new Vector3(7.6f, .7f, 8.2f), new Vector3(.4f, 1.4f, .4f), M("CRT_Plastic", new Color(.78f, .76f, .68f), .3f));
        Box(props, "Coffee_Table", new Vector3(7.4f, .45f, 10.5f), new Vector3(.8f, .9f, 1.6f), deskWood);
        Box(props, "Coffee_Machine", new Vector3(7.4f, 1.1f, 10.2f), new Vector3(.4f, .4f, .35f), M("Phone_Black", new Color(.05f, .05f, .05f), .5f), false);
        Person(npcs, "Cop_AtDesk_1", new Vector3(-5.5f, 0, 8.25f), 0, new Color(.75f, .72f, .62f), true);
        Person(npcs, "Cop_AtDesk_2", new Vector3(2.3f, 0, 11.25f), 0, new Color(.6f, .65f, .72f), true);
        Person(npcs, "Detective_Coffee", new Vector3(6.6f, 0, 10.6f), -90, new Color(.35f, .3f, .26f));
        Person(npcs, "Cop_Standing", new Vector3(-2.6f, 0, 13.3f), 140, new Color(.12f, .18f, .38f), false, true);
        Person(npcs, "Detective_Talking", new Vector3(-1.9f, 0, 14.1f), -60, new Color(.5f, .48f, .45f));

        // offices along the back (z 18..24): captain (west), Alexey (east)
        Wall(arch, "Wall_Offices", new Vector3(-8, 0, 18), new Vector3(8, 0, 18), H, .12f, wallM, new Vector3(-3.5f, 1.0f, 2.2f), new Vector3(3.5f, 1.0f, 2.2f));
        Box(arch, "Captain_Glass", new Vector3(-5.6f, 1.6f, 18), new Vector3(2.4f, 1.1f, .04f), glass, false);
        Wall(arch, "Wall_Office_Divider", new Vector3(0, 0, 18), new Vector3(0, 0, 24), H, .12f, wallM);
        var officeDoor = Group(arch, "OfficeDoor_Pivot", new Vector3(3.0f, 0, 18));
        Box(officeDoor, "Leaf", new Vector3(.5f, 1.1f, 0), new Vector3(.98f, 2.18f, .05f), doorM);
        Box(officeDoor, "Glass", new Vector3(.5f, 1.5f, -.03f), new Vector3(.6f, .5f, .01f), M("Glass_Frosted", new Color(.75f, .8f, .8f), .7f), false);
        Text(officeDoor, "DoorName", "ДЕТ. А. ВОЛКОВ", new Vector3(.5f, 1.5f, -.04f), 0, .025f, new Color(.1f, .1f, .1f));
        var capDoor = Group(arch, "CaptainDoor_Pivot", new Vector3(-4.0f, 0, 18));
        Box(capDoor, "Leaf", new Vector3(.5f, 1.1f, 0), new Vector3(.98f, 2.18f, .05f), doorM);
        Text(capDoor, "DoorName", "КАПИТАН", new Vector3(.5f, 1.5f, -.04f), 0, .025f, new Color(.1f, .1f, .1f));
        Desk(props, "Captain_Desk", new Vector3(-4.0f, 0, 22.0f), 180);
        Person(npcs, "Captain", new Vector3(-4.0f, 0, 22.75f), 180, new Color(.82f, .82f, .8f), true);

        // ---------------- Alexey's office (x 0..8, z 18..24)
        var office = Group(root, "Office_Alexey", Vector3.zero);
        Desk(office, "Office_Desk", new Vector3(4.6f, 0, 22.4f), 180, true, true);
        Box(office, "Office_Lamp_Base", new Vector3(3.95f, .8f, 22.6f), new Vector3(.12f, .06f, .12f), M("Brass", new Color(.7f, .58f, .3f), .6f, .8f), false);
        Prim(office, PrimitiveType.Cylinder, "Office_Lamp_Shade", new Vector3(3.95f, 1.08f, 22.6f), new Vector3(.24f, .08f, .24f), M("Lamp_Green", new Color(.1f, .35f, .2f), .6f, 0, new Color(.2f, .5f, .25f)));
        for (int i = 0; i < 2; i++) Box(office, "Office_Cabinet_" + i, new Vector3(7.6f, .66f, 22.6f - i * .55f), new Vector3(.5f, 1.32f, .5f), M("Cabinet_Grey", new Color(.45f, .47f, .45f), .4f, .2f));
        Box(office, "CoatRack_Pole", new Vector3(0.45f, .9f, 18.5f), new Vector3(.05f, 1.8f, .05f), doorM, false);
        Box(office, "Coat", new Vector3(0.45f, 1.35f, 18.55f), new Vector3(.35f, .8f, .18f), M("NPC_Pants", new Color(.08f, .09f, .12f), .2f), false);
        Box(office, "Office_Window", new Vector3(4.6f, 1.7f, 23.9f), new Vector3(2.4f, 1.2f, .05f), M("Window_Night", new Color(.05f, .07f, .12f), .9f, 0, new Color(.05f, .08f, .15f)), false);
        for (int i = 0; i < 10; i++) Box(office, "Blind_" + i, new Vector3(4.6f, 1.15f + i * .12f, 23.84f), new Vector3(2.4f, .03f, .02f), M("Blinds", new Color(.75f, .72f, .62f), .3f), false);
        Box(office, "Chair_Visitor", new Vector3(4.2f, .45f, 20.9f), new Vector3(.45f, .08f, .45f), M("Chair_Fabric", new Color(.12f, .14f, .16f), .15f), false);
        Box(office, "Office_Rug", new Vector3(4.2f, .005f, 20.8f), new Vector3(2.4f, .01f, 1.8f), M("Rug_Red", new Color(.35f, .1f, .08f), .1f), false);

        // evidence board on the divider wall, facing +X (into the office)
        var board = Group(office, "DeductionBoard", new Vector3(0.1f, 1.55f, 20.6f), 90);
        Box(board, "Frame", new Vector3(0, 0, -.03f), new Vector3(1.95f, 1.25f, .05f), M("Board_Frame", new Color(.2f, .14f, .1f), .4f));
        Box(board, "Cork", new Vector3(0, 0, 0), new Vector3(1.85f, 1.15f, .02f), M("Cork", new Color(.6f, .45f, .28f), .1f), false);
        var surface = Group(board, "Surface", new Vector3(0, 0, .012f));
        var view = Group(office, "Board_ViewPoint", new Vector3(1.75f, 1.6f, 20.6f));
        view.rotation = Quaternion.LookRotation(new Vector3(0.1f, 1.55f, 20.6f) - view.position, Vector3.up);
        var db = board.gameObject.AddComponent<DeductionBoard>();
        db.viewPoint = view; db.surface = surface; db.surfaceSize = new Vector2(1.75f, 1.05f);
        db.cardMaterial = M("Card_Paper", new Color(.86f, .83f, .75f), .05f);
        db.pinMaterial = M("Pin_Red", new Color(.8f, .08f, .06f), .6f);
        db.textMaterial = TextMat();
        db.yarnMaterial = M("Yarn_Red", new Color(.75f, .05f, .05f), .2f, 0, new Color(.25f, .01f, .01f));
        var trigger = board.gameObject.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0, -.3f, .8f); trigger.size = new Vector3(1.9f, 2.0f, 1.6f); trigger.isTrigger = true;
        FillBoardContent(db);

        // ---------------- lighting
        var sun = new GameObject("Dusk_Light").AddComponent<UnityEngine.Light>();
        sun.transform.SetParent(lights, false); sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(18, 35, 0); sun.color = new Color(1f, .62f, .42f); sun.intensity = .6f; sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.32f, .34f, .4f); RenderSettings.ambientEquatorColor = new Color(.22f, .22f, .22f); RenderSettings.ambientGroundColor = new Color(.1f, .09f, .08f);
        foreach (float z in new[] { 3f, 8.5f, 12f, 15.5f })
            foreach (float x in new[] { -4f, 0f, 4f })
                Light(lights, $"Fluo_{x}_{z}", new Vector3(x, H - .15f, z), new Color(.92f, 1f, .9f), 1.6f, 6.5f, z == 12f && x == 0f);
        Light(lights, "Office_Ceiling", new Vector3(4f, H - .15f, 21f), new Color(1f, .95f, .85f), 1.2f, 6, true);
        Light(lights, "Office_DeskLamp", new Vector3(3.95f, 1.0f, 22.6f), new Color(1f, .8f, .55f), 1.4f, 3.5f, false, false);
        Light(lights, "Captain_Ceiling", new Vector3(-4f, H - .15f, 21f), new Color(1f, .95f, .85f), 1.0f, 5.5f);

        // ---------------- player + cameras
        var player = new GameObject("Player");
        var cc = player.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .28f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .22f;
        var visual = new GameObject("Character_Visual").transform; visual.SetParent(player.transform, false);
        var anim = HeroSetup.CreateHero(visual, "Alexey", "Hero_Locomotion.controller");
        var hero = visual.Find("Alexey");
        var loco = hero.gameObject.AddComponent<HeroLocomotion>(); loco.animator = anim; loco.controller = cc;
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = .04f; cam.fieldOfView = 60; camGo.AddComponent<AudioListener>();
        var follow = camGo.AddComponent<ThirdPersonCamera>(); follow.target = player.transform; follow.offset = new Vector3(0, 1.45f, 0);
        follow.distance = 1.5f; follow.minDistance = .2f; follow.maxDistance = 2.2f; follow.enabled = false;
        var hudGo = new GameObject("Gameplay_HUD"); var hud = hudGo.AddComponent<SimpleUIHUD>(); hud.enableJournal = true; hudGo.AddComponent<QteManager>();
        var officer = player.AddComponent<OfficerController>(); officer.cameraTransform = camGo.transform; officer.hud = hud;
        officer.walkSpeed = 1.8f; officer.runSpeed = 3f; officer.interactionRadius = 1.5f; officer.enabled = false;

        // ---------------- cutscene
        var path = Group(root, "Cutscene_Path", Vector3.zero);
        Transform P(string name, Vector3 pos, Vector3 fwd) { var t = Group(path, name, pos); t.rotation = Quaternion.LookRotation(fwd); return t; }
        var p0 = P("P0_Doorway", new Vector3(0, 0, -1.0f), Vector3.forward);
        var p1 = P("P1_BullpenEnd", new Vector3(0, 0, 16.7f), Vector3.forward);
        var p2 = P("P2_OfficeFront", new Vector3(3.5f, 0, 16.7f), Vector3.forward);
        var p3 = P("P3_InOffice", new Vector3(3.5f, 0, 19.7f), Vector3.forward);
        var director = new GameObject("Cutscene_Director").AddComponent<PrecinctIntroCutscene>();
        director.player = player.transform; director.shotCamera = cam; director.path = new[] { p0, p1, p2, p3 };
        director.frontDoorLeft = doorL; director.frontDoorRight = doorR; director.officeDoor = officeDoor; director.establishingShot = establishing;
        director.dialogue = new[]
        {
            new PrecinctIntroCutscene.Line { start = 5.0f, end = 8.6f, speaker = "ДЕЖУРНЫЙ", text = "Волков? Ты же сегодня выходной. Капитан про тебя спрашивал." },
            new PrecinctIntroCutscene.Line { start = 8.8f, end = 11.6f, speaker = "АЛЕКСЕЙ", text = "Потом. Мне нужно кое-что разложить по полочкам." },
            new PrecinctIntroCutscene.Line { start = 12.6f, end = 15.6f, speaker = "ДЕТЕКТИВ", text = "Слышал про твоего старика. Если что — мы рядом, Лёха." },
            new PrecinctIntroCutscene.Line { start = 15.8f, end = 17.2f, speaker = "АЛЕКСЕЙ", text = "Спасибо." },
        };
        player.transform.SetPositionAndRotation(p0.position, p0.rotation);
        PrecinctNoirLook.Apply(scene);

        Directory("Assets/_Project/Scenes");
        EditorSceneManager.SaveScene(scene, Scene);
        AddToBuild(Scene, log);
        AddToBuild(FlatScene, log);
        log.AppendLine("Precinct saved: " + Scene + " cutscene " + director.Duration.ToString("F1") + "s");
        CapturePrecinct(log);
        EditorSceneManager.SaveScene(scene);
    }

    internal const string SecretTitle = "Смятая записка";
    internal const string SecretText = "Под кроватью, у самой стены: «Папа, не говори Лёше. Я всё верну до пятницы. М.» Почерк Михаила.";

    static void FillBoardContent(DeductionBoard board) => FatherApartmentScenario.ConfigureBoard(board);

    static void Directory(string path) { if (!AssetDatabase.IsValidFolder(path)) System.IO.Directory.CreateDirectory(path); }

    internal static void AddToBuild(string path, StringBuilder log)
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
        log.AppendLine("Added to build: " + path);
    }

    public static void CapturePrecinct(StringBuilder log)
    {
        var director = UnityEngine.Object.FindAnyObjectByType<PrecinctIntroCutscene>();
        var cam = director.shotCamera;
        var hero = director.player.GetComponentInChildren<Animator>();
        var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Arts/Heroes/Animations/Hero_Walk.anim");
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Arts/Heroes/Animations/Hero_Idle.anim");
        var doors = new[] { director.frontDoorLeft, director.frontDoorRight, director.officeDoor };
        var closed = doors.Select(d => d.localRotation).ToArray();
        float D = director.Duration;
        var times = new[] { 1.0f, 3.2f, 6f, 11f, D - 4.5f, D - 1.3f };
        for (int i = 0; i < times.Length; i++)
        {
            for (int d = 0; d < doors.Length; d++) doors[d].localRotation = closed[d];
            ReflectAwake(director);
            director.Evaluate(times[i]);
            if (walk && hero) walk.SampleAnimation(hero.gameObject, times[i] % walk.length);
            HeroSetup.Shot(cam, $"precinct_cut_{i}.png");
            log.AppendLine($"  cut {i} t={times[i]:F1} cam={cam.transform.position} fwd={cam.transform.forward} hero={director.player.position}");
        }
        if (idle && hero) idle.SampleAnimation(hero.gameObject, 0);
        // gameplay view and the board
        var board = UnityEngine.Object.FindAnyObjectByType<DeductionBoard>();
        HeroSetup.DebugShot(new Vector3(-3f, 1.7f, 5f), new Vector3(0, 1.4f, 0), "precinct_lobby.png", 60);
        HeroSetup.DebugShot(board.viewPoint.position, board.transform.position, "precinct_board.png", 60);
        HeroSetup.DebugShot(new Vector3(7.3f, 2.3f, 19.0f), new Vector3(3f, .8f, 22f), "precinct_office.png", 75);
        HeroSetup.DebugShot(new Vector3(-6.5f, 2.6f, 7f), new Vector3(2f, .8f, 15f), "precinct_bullpen.png", 70);
        for (int d = 0; d < doors.Length; d++) doors[d].localRotation = closed[d];
        director.Evaluate(0);
    }

    // Awake() is not called in edit mode: copy closed rotations through the same code path
    static void ReflectAwake(PrecinctIntroCutscene d)
    {
        var m = typeof(PrecinctIntroCutscene).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        m?.Invoke(d, null);
    }
}
