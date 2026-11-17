using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarXCase.Hero;
using CarXCase.Precinct;
using static PrecinctBuilder;
using IC = CarXCase.Precinct.Interrogation.Choice;
using IN = CarXCase.Precinct.Interrogation.Node;

/// Greybox locations reached from the evidence board: arrival cutscene → Detroit-style interrogation.
/// Command "locations" (HeroSetup/request.txt) or Tools/Precinct/Build locations.
public static class LocationBuilder
{
    [MenuItem("Tools/Precinct/Build locations (bar, pawnshop, Mikhail)")] static void Menu() => HeroSetup.RunCommand("locations");

    public static void BuildAll(StringBuilder log)
    {
        BuildBar(log);
        BuildPawnshop(log);
        BuildMikhail(log);
    }

    // ================================================================== shared
    class Ctx
    {
        public UnityEngine.SceneManagement.Scene scene;
        public Transform root, arch, props, lights, hero; public Camera cam;
        public NpcActor npc; public ScriptedCutscene cut; public Interrogation talk;
        public Transform door;
    }

    static Ctx Begin(string name, Color ambient)
    {
        var c = new Ctx();
        UnityEngine.Random.InitState(name.GetHashCode());
        c.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        c.root = new GameObject(name + "_Greybox").transform;
        c.arch = Group(c.root, "Architecture", Vector3.zero);
        c.props = Group(c.root, "Props", Vector3.zero);
        c.lights = Group(c.root, "Lighting", Vector3.zero);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        c.cam = camGo.AddComponent<Camera>(); c.cam.nearClipPlane = .03f; c.cam.fieldOfView = 50;
        c.cam.clearFlags = CameraClearFlags.SolidColor; c.cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();
        var heroRoot = new GameObject("Hero").transform;
        var anim = HeroSetup.CreateHero(heroRoot, "Alexey", "Hero_Locomotion.controller");
        var model = heroRoot.Find("Alexey");
        var loco = model.gameObject.AddComponent<HeroLocomotion>(); loco.animator = anim;
        c.hero = heroRoot;
        return c;
    }

    /// Room shell; the entrance is in the south wall (z = z0) at doorX, opening onto a coloured light curtain.
    static void Room(Ctx c, float x0, float x1, float z0, float z1, float h, Material floor, Material wall, Material ceil, float doorX, Color outside)
    {
        float w = x1 - x0, d = z1 - z0, cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
        Box(c.arch, "Floor", new Vector3(cx, -.05f, cz), new Vector3(w, .1f, d), floor);
        Box(c.arch, "Ceiling", new Vector3(cx, h + .05f, cz), new Vector3(w, .1f, d), ceil);
        Wall(c.arch, "Wall_South", new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), h, .15f, wall, new Vector3(doorX, 1.0f, 2.2f));
        Wall(c.arch, "Wall_North", new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), h, .15f, wall);
        Wall(c.arch, "Wall_West", new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), h, .15f, wall);
        Wall(c.arch, "Wall_East", new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), h, .15f, wall);
        // vestibule + light curtain so no street is ever visible
        Box(c.arch, "Vestibule_L", new Vector3(doorX - .7f, 1.2f, z0 - .7f), new Vector3(.08f, 2.4f, 1.3f), wall);
        Box(c.arch, "Vestibule_R", new Vector3(doorX + .7f, 1.2f, z0 - .7f), new Vector3(.08f, 2.4f, 1.3f), wall);
        Box(c.arch, "Vestibule_Top", new Vector3(doorX, 2.4f, z0 - .7f), new Vector3(1.5f, .08f, 1.3f), wall);
        Box(c.arch, "Vestibule_Floor", new Vector3(doorX, -.05f, z0 - .7f), new Vector3(1.5f, .1f, 1.3f), floor);
        Box(c.arch, "Outside_Light", new Vector3(doorX, 1.2f, z0 - 1.33f), new Vector3(1.4f, 2.4f, .04f), M("Outside_" + ColorUtility.ToHtmlStringRGB(outside), outside * .3f, 0, 0, outside), false);
        c.door = Group(c.arch, "Door_Pivot", new Vector3(doorX - .5f, 0, z0));
        Box(c.door, "Leaf", new Vector3(.5f, 1.1f, 0), new Vector3(.98f, 2.18f, .05f), M("Door_Dark", new Color(.22f, .15f, .1f), .4f));
        Box(c.door, "Handle", new Vector3(.88f, 1.0f, .05f), new Vector3(.03f, .12f, .03f), M("Brass", new Color(.7f, .58f, .3f), .6f, .8f), false);
    }

    static Transform Anchor(Ctx c, string name, Vector3 pos, Vector3 lookAt)
    {
        var t = Group(c.root, name, pos); t.rotation = Quaternion.LookRotation(lookAt - pos); return t;
    }

    static Transform[] Path(Ctx c, Vector3 lastForward, params Vector3[] pts)
    {
        var g = Group(c.root, "Cutscene_Path", Vector3.zero);
        var arr = pts.Select((p, i) => Group(g, "P" + i, p)).ToArray();
        arr[arr.Length - 1].rotation = Quaternion.LookRotation(lastForward);
        return arr;
    }

    static NpcActor Npc(Ctx c, string name, Vector3 pos, float yaw, Color shirt, bool sitting)
    {
        var t = Person(c.props, name, pos, yaw, shirt, sitting);
        var a = t.gameObject.AddComponent<NpcActor>();
        a.head = t.Find("Head"); a.torso = t.Find("Torso");
        return a;
    }

    static void Finish(Ctx c, string path, StringBuilder log, string tag)
    {
        c.hero.SetPositionAndRotation(c.cut.path[0].position, Quaternion.LookRotation(Vector3.forward));
        UnityEventTools.AddPersistentListener(c.cut.onFinished, c.talk.Begin);
        EditorSceneManager.SaveScene(c.scene, path);
        AddToBuild(path, log);
        log.AppendLine($"{tag}: saved {path}, cutscene {c.cut.Duration:F1}s, nodes {c.talk.nodes.Length}");
        Capture(c, tag);
        EditorSceneManager.SaveScene(c.scene);
    }

    static void Capture(Ctx c, string tag)
    {
        var anim = c.hero.GetComponentInChildren<Animator>();
        var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Arts/Heroes/Animations/Hero_Walk.anim");
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Arts/Heroes/Animations/Hero_Idle.anim");
        var closed = c.door.localRotation;
        float D = c.cut.Duration;
        var times = new[] { 0.8f, c.cut.walkStart + 1.8f, D - 1.0f };
        for (int i = 0; i < times.Length; i++)
        {
            c.door.localRotation = closed;
            SetPrivate(c.cut, "doorInit", false);
            c.cut.Evaluate(times[i]);
            if (walk && anim) walk.SampleAnimation(anim.gameObject, times[i] % walk.length);
            if (i == times.Length - 1 && idle && anim) idle.SampleAnimation(anim.gameObject, 0);
            HeroSetup.Shot(c.cam, $"{tag}_cut_{i}.png");
        }
        foreach (var a in new[] { c.talk.camOnSuspect, c.talk.camOnHero, c.talk.camWide })
        {
            c.cam.transform.SetPositionAndRotation(a.position, a.rotation); c.cam.fieldOfView = 40;
            HeroSetup.Shot(c.cam, $"{tag}_{a.name}.png");
        }
        c.door.localRotation = closed; SetPrivate(c.cut, "doorInit", false);
        c.cut.Evaluate(0);
    }

    static void SetPrivate(object o, string field, object v) =>
        o.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(o, v);

    static IN N(string id, string[] lines, params IC[] ch) => new IN { id = id, lines = lines, choices = ch };
    static IC C(int slot, string label, string line, int p, int s, string next, string reply = null, string req = null) =>
        new IC { slot = slot, label = label, heroLine = line, probability = p, stress = s, next = next, reply = reply, requires = req };

    static ScriptedCutscene.Shot Sh(float t, Transform anchor, Transform look, float fov) => new ScriptedCutscene.Shot { start = t, anchor = anchor, lookAt = look, fov = fov };
    static ScriptedCutscene.Shot Follow(float t, Vector3 off, float fov) => new ScriptedCutscene.Shot { start = t, followOffset = off, fov = fov };
    static ScriptedCutscene.Line L(float a, float b, string who, string text) => new ScriptedCutscene.Line { start = a, end = b, speaker = who, text = text };

    static Interrogation Talk(Ctx c, string suspectName, Vector3 heroPos, Vector3 npcHead, float heroFace = 1)
    {
        var g = new GameObject("Interrogation"); g.transform.SetParent(c.root, false);
        var t = g.AddComponent<Interrogation>();
        t.suspectName = suspectName; t.suspect = c.npc; t.cam = c.cam;
        Vector3 heroHead = heroPos + Vector3.up * 1.6f;
        Vector3 dir = (new Vector3(npcHead.x, 0, npcHead.z) - new Vector3(heroPos.x, 0, heroPos.z)).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        t.camOnSuspect = Anchor(c, "Cam_OnSuspect", heroHead - dir * .62f + right * .38f + Vector3.up * .07f, npcHead);
        t.camOnHero = Anchor(c, "Cam_OnHero", npcHead + dir * .95f - right * .6f + Vector3.up * .12f, heroHead - Vector3.up * .05f);
        Vector3 mid = (heroHead + npcHead) / 2;
        t.camWide = Anchor(c, "Cam_Wide", mid + right * 2.4f + Vector3.up * .35f - dir * .2f, mid - Vector3.up * .15f);
        return t;
    }

    // ================================================================== 03 BAR — Dmitry
    static void BuildBar(StringBuilder log)
    {
        var c = Begin("03_Bar", new Color(.10f, .07f, .07f));
        var floor = M("Bar_Floor", new Color(.2f, .13f, .09f), .45f);
        var wall = M("Bar_Wall", new Color(.32f, .1f, .09f), .25f);
        var ceil = M("Bar_Ceiling", new Color(.08f, .06f, .05f), .1f);
        var wood = M("Bar_Wood", new Color(.25f, .14f, .08f), .55f);
        var leather = M("Bar_Leather", new Color(.18f, .05f, .04f), .5f);
        Room(c, -5, 5, 0, 9, 3f, floor, wall, ceil, -3f, new Color(.25f, .35f, .7f));
        // counter + back bar
        Box(c.props, "Counter", new Vector3(0, .55f, 6.4f), new Vector3(6.4f, 1.1f, .6f), wood);
        Box(c.props, "Counter_Top", new Vector3(0, 1.12f, 6.35f), new Vector3(6.6f, .05f, .8f), M("Bar_Top", new Color(.12f, .07f, .04f), .8f));
        Box(c.props, "Counter_Glow", new Vector3(0, .25f, 6.08f), new Vector3(6.3f, .03f, .02f), M("Neon_Amber", new Color(1f, .5f, .1f), .5f, 0, new Color(2.5f, 1.1f, .2f)), false);
        Box(c.props, "BackBar_Mirror", new Vector3(0, 1.75f, 8.88f), new Vector3(5.6f, 1.3f, .02f), M("Mirror_Dark", new Color(.2f, .22f, .25f), .95f, .6f), false);
        string[] bottle = { "Bottle_Green", "Bottle_Amber", "Bottle_Clear", "Bottle_Red" };
        Color[] bc = { new Color(.1f, .4f, .15f), new Color(.6f, .3f, .05f), new Color(.7f, .75f, .75f), new Color(.5f, .05f, .05f) };
        for (int s = 0; s < 3; s++)
        {
            Box(c.props, "Shelf_" + s, new Vector3(0, 1.15f + s * .45f, 8.7f), new Vector3(5.6f, .04f, .35f), wood, false);
            for (int i = 0; i < 18; i++)
            {
                int k = UnityEngine.Random.Range(0, 4);
                Prim(c.props, PrimitiveType.Cylinder, "Bottle", new Vector3(-2.6f + i * .31f + UnityEngine.Random.Range(-.05f, .05f), 1.3f + s * .45f, 8.7f), new Vector3(.08f, .13f + UnityEngine.Random.Range(0, .05f), .08f),
                    M(bottle[k], bc[k], .9f, 0, bc[k] * .25f));
            }
        }
        Text(c.props, "Neon_Sign", "СТРЕЛА", new Vector3(0, 2.65f, 8.86f), 0, .09f, new Color(1f, .25f, .25f));
        Box(c.props, "Neon_Underline", new Vector3(0, 2.4f, 8.86f), new Vector3(2.0f, .04f, .02f), M("Neon_Red", new Color(1f, .1f, .1f), .5f, 0, new Color(3f, .2f, .2f)), false);
        foreach (float x in new[] { -2.4f, -1.2f, 1.2f, 2.4f })
        {
            Prim(c.props, PrimitiveType.Cylinder, "Stool_Seat", new Vector3(x, .75f, 5.75f), new Vector3(.38f, .04f, .38f), leather);
            Prim(c.props, PrimitiveType.Cylinder, "Stool_Leg", new Vector3(x, .37f, 5.75f), new Vector3(.06f, .37f, .06f), M("Chrome", new Color(.7f, .7f, .72f), .85f, 1));
        }
        // booths with two of Rezo's men
        foreach (float z in new[] { 2.2f, 4.4f })
        {
            Box(c.props, "Booth_Table_" + z, new Vector3(-4.2f, .74f, z), new Vector3(.8f, .05f, 1.0f), wood);
            Box(c.props, "Booth_SeatA_" + z, new Vector3(-4.2f, .45f, z - .8f), new Vector3(1.2f, .45f, .5f), leather);
            Box(c.props, "Booth_SeatB_" + z, new Vector3(-4.2f, .45f, z + .8f), new Vector3(1.2f, .45f, .5f), leather);
        }
        Person(c.props, "Thug_1", new Vector3(-4.2f, .1f, 2.95f), 180, new Color(.08f, .08f, .09f), true);
        Person(c.props, "Thug_2", new Vector3(-4.2f, .1f, 1.45f), 0, new Color(.15f, .12f, .1f), true);
        // pool table + jukebox
        Box(c.props, "Pool_Table", new Vector3(2.6f, .78f, 2.4f), new Vector3(1.3f, .12f, 2.4f), M("Felt_Green", new Color(.05f, .3f, .12f), .2f));
        Box(c.props, "Pool_Base", new Vector3(2.6f, .38f, 2.4f), new Vector3(1.0f, .7f, 2.0f), wood);
        Box(c.props, "Pool_Lamp", new Vector3(2.6f, 2.2f, 2.4f), new Vector3(.5f, .2f, 1.4f), M("Lamp_Green", new Color(.1f, .35f, .2f), .6f, 0, new Color(.2f, .5f, .25f)), false);
        Box(c.props, "Jukebox", new Vector3(4.55f, .75f, 6.5f), new Vector3(.7f, 1.5f, .6f), M("Jukebox", new Color(.6f, .2f, .5f), .6f, 0, new Color(.8f, .25f, .6f)));
        PrecinctBuilder.Light(c.lights, "Light_PoolTable", new Vector3(2.6f, 2.0f, 2.4f), new Color(1f, .9f, .6f), 2.2f, 4.5f, true, false);
        PrecinctBuilder.Light(c.lights, "Light_Neon", new Vector3(0, 2.4f, 8.2f), new Color(1f, .2f, .2f), 2.4f, 6f, false, false);
        PrecinctBuilder.Light(c.lights, "Light_Counter", new Vector3(0, 2.6f, 5.8f), new Color(1f, .72f, .45f), 2.2f, 5.5f, true, false);
        PrecinctBuilder.Light(c.lights, "Light_Booths", new Vector3(-3.8f, 2.4f, 3.3f), new Color(1f, .6f, .35f), 1.4f, 4.5f, false, false);
        PrecinctBuilder.Light(c.lights, "Light_Jukebox", new Vector3(4.2f, 1.3f, 6.2f), new Color(.9f, .3f, .7f), 1.0f, 3f, false, false);

        c.npc = Npc(c, "Dmitry", new Vector3(0, 0, 7.2f), 180, new Color(.2f, .2f, .22f), false);
        Vector3 heroEnd = new Vector3(0, 0, 5.4f);
        var cut = c.cut = new GameObject("Cutscene").AddComponent<ScriptedCutscene>();
        cut.hero = c.hero; cut.shotCamera = c.cam; cut.npc = c.npc; cut.door = c.door; cut.doorAngle = -95; cut.doorOpenAt = .2f;
        cut.path = Path(c, Vector3.forward, new Vector3(-3f, 0, -.6f), new Vector3(-2.2f, 0, 2.0f), heroEnd);
        cut.walkSpeed = 1.4f; cut.walkStart = 1.4f; cut.tail = 3.2f;
        var headTarget = Group(c.root, "Look_HeroEnd", heroEnd + Vector3.up * 1.55f);
        var dmHead = Group(c.root, "Look_Dmitry", new Vector3(0, 1.6f, 7.2f));
        cut.shots = new[]
        {
            Sh(0, Anchor(c, "Shot_Bar_Wide", new Vector3(3.9f, 2.3f, 6.0f), new Vector3(-3f, 1.2f, .5f)), null, 52),
            Follow(cut.walkStart + 1.6f, new Vector3(.4f, 1.7f, -1.6f), 50),
            Sh(cut.Duration - 3.2f, Anchor(c, "Shot_OverDmitry", new Vector3(.45f, 1.78f, 7.85f), heroEnd + Vector3.up * 1.5f), headTarget, 40),
        };
        cut.title = "БАР «СТРЕЛА»"; cut.subtitle = "Брайтон-Бич · 23:10";
        float D = cut.Duration;
        cut.dialogue = new[]
        {
            L(D - 3.0f, D - .1f, "ДМИТРИЙ", "Мы закрываемся. А, это ты… Какими судьбами, товарищ детектив?"),
        };

        c.talk = Talk(c, "ДМИТРИЙ", heroEnd, dmHead.position);
        var t = c.talk;
        t.probability = 40; t.stress = 35; t.successThreshold = 60;
        t.nodes = new[]
        {
            N("d1", new[] { "Лёха, ты в бар? В форме? Ты же у нас правильный, сюда не ходишь." },
                C(0, "Отец пропал", "Отец пропал, Дима. Со вчерашнего вечера.", 10, -5, "d2", "…Как пропал? Я его в субботу видел. Нормальный был."),
                C(1, "Наехать", "Не строй из себя удивлённого. Что ты знаешь?", -5, 15, "d2", "Ты пришёл в мой бар и орёшь на меня? Сбавь тон."),
                C(2, "Разрядить", "Расслабься. Я не по работе. Пока.", 0, -10, "d2", "«Пока». Ну-ну. Чего тебе?")),
            N("d2", new[] { "Говори прямо. Что тебе от меня надо?" },
                C(0, "Запись с автоответчика", "", 15, 15, "d3"),
                C(1, "Спички из твоего бара", "", 10, 5, "d3"),
                C(2, "Мы семья", "Ты его сын, как и я. Если он в беде — это и твоё дело.", 5, -10, "d3", "Не дави на жалость. …Ладно. Слушаю."),
                C(3, "Пригрозить", "Могу прислать сюда наряд и проверить твои лицензии. Хочешь?", -10, 25, "d3", "Вот так, значит. Родному брату.")),
            N("d3", new[] { "Отец позавчера был здесь. Спрашивал про Резо. Я сказал ему не лезть." },
                C(0, "Почему молчал?", "И ты мне не позвонил? Два дня молчал?", -5, 10, "d4", "А что бы ты сделал? Пришёл с жетоном? Его бы сразу и закопали."),
                C(1, "Где Резо?", "Где сейчас Резо? Мне нужен адрес.", 10, 10, "d4", "Ты не понимаешь, о чём просишь."),
                C(2, "Поблагодарить", "Спасибо, что сказал. Правда.", 10, -10, "d4", "Не благодари. Я тоже за него боюсь.")),
            N("d4", new[] { "Если я скажу, где они держат людей, меня завтра найдут в заливе." },
                C(0, "Обещать защиту", "Я дам тебе защиту. Слово.", 15, -5, "END"),
                C(1, "Бумаги с двенадцатого пирса", "", 20, 5, "END"),
                C(2, "Надавить", "Тогда тебя найдут в заливе вместе с ним. Решай.", -5, 25, "END"),
                C(3, "Уйти", "Ладно. Сам разберусь.", -20, -10, "END")),
        };
        t.successTitle = "ДМИТРИЙ ЗАГОВОРИЛ";
        t.successText = "Дмитрий наклоняется через стойку: «Старый склад рефрижераторов на Ван-Брант-стрит. Резо держит там должников. Я тебе ничего не говорил».\nТеперь ты знаешь, где искать отца.";
        t.failTitle = "ДМИТРИЙ ЗАКРЫЛСЯ";
        t.failText = "«Пей или уходи», — Дмитрий отворачивается к полкам. Зацепка в баре оборвалась, а люди Резо теперь знают, что ты их ищешь.";
        t.shutdownText = "Дмитрий бьёт ладонью по стойке: «Вон из моего бара!» Охрана выводит тебя на улицу.";
        t.endingTag = "ПРОДОЛЖЕНИЕ СЛЕДУЕТ: СКЛАД НА ВАН-БРАНТ-СТРИТ";
        t.grantId = "clue_warehouse"; t.grantTitle = "Адрес склада"; t.grantText = "Склад рефрижераторов на Ван-Брант-стрит: там люди Резо держат должников.";
        FatherApartmentScenario.ConfigureBar(t);
        Finish(c, "Assets/_Project/Scenes/03_Bar.unity", log, "bar");
    }

    // ================================================================== 04 PAWNSHOP — Sema
    static void BuildPawnshop(StringBuilder log)
    {
        var c = Begin("04_Pawnshop", new Color(.06f, .06f, .07f));
        var floor = M("Pawn_Floor", new Color(.38f, .35f, .3f), .35f);
        var wall = M("Pawn_Wall", new Color(.5f, .45f, .32f), .15f);
        var ceil = M("Pawn_Ceiling", new Color(.45f, .43f, .4f), .1f);
        var wood = M("Pawn_Wood", new Color(.3f, .2f, .12f), .45f);
        var metal = M("Pawn_Metal", new Color(.3f, .32f, .33f), .5f, .7f);
        Room(c, -4, 4, 0, 7, 3f, floor, wall, ceil, 0f, new Color(1f, .55f, .2f));
        Box(c.props, "Counter", new Vector3(0, .5f, 4.2f), new Vector3(5f, 1.0f, .6f), wood);
        Box(c.props, "Counter_Glass", new Vector3(0, 1.03f, 4.2f), new Vector3(5f, .04f, .62f), M("Glass_Display", new Color(.6f, .7f, .7f), .95f));
        for (int i = 0; i < 6; i++) Box(c.props, "Display_Item", new Vector3(-2.1f + i * .8f, .9f, 4.2f), new Vector3(.2f, .05f, .14f), M("Gold", new Color(.85f, .65f, .25f), .8f, 1), false);
        for (int i = 0; i < 24; i++)
        {
            float x = -2.5f + i * (5f / 23f);
            if (Mathf.Abs(x) < .65f) continue; // the service window
            Box(c.props, "Grille_Bar", new Vector3(x, 1.85f, 4.45f), new Vector3(.02f, 1.3f, .02f), metal, false);
        }
        Box(c.props, "Grille_Top", new Vector3(0, 2.5f, 4.45f), new Vector3(5f, .04f, .04f), metal, false);
        foreach (float x in new[] { -3.75f, 3.75f })
            for (int s = 0; s < 4; s++)
            {
                Box(c.props, "Shelf", new Vector3(x, .4f + s * .55f, 2.0f), new Vector3(.45f, .04f, 3.2f), wood, false);
                for (int i = 0; i < 4; i++)
                    Box(c.props, "Goods", new Vector3(x, .55f + s * .55f, .9f + i * .75f), new Vector3(.35f, UnityEngine.Random.Range(.15f, .3f), UnityEngine.Random.Range(.25f, .5f)),
                        M(i % 2 == 0 ? "CRT_Plastic" : "Goods_Dark", i % 2 == 0 ? new Color(.78f, .76f, .68f) : new Color(.15f, .15f, .16f), .3f), false);
            }
        for (int i = 0; i < 3; i++) Box(c.props, "Guitar_" + i, new Vector3(-1.5f + i * .7f, 1.6f, 6.88f), new Vector3(.35f, 1.0f, .08f), M("Guitar_Wood", new Color(.55f, .3f, .12f), .6f), false);
        for (int i = 0; i < 5; i++) Prim(c.props, PrimitiveType.Cylinder, "Clock_" + i, new Vector3(1.3f + i * .45f, 2.2f + (i % 2) * .25f, 6.9f), new Vector3(.3f, .02f, .3f), M("Clock_Face", new Color(.9f, .88f, .8f), .3f), new Vector3(90, 0, 0));
        // safe (door ajar) and back-room curtain
        Box(c.props, "Safe", new Vector3(2.6f, .6f, 6.4f), new Vector3(.9f, 1.2f, .8f), M("Safe_Green", new Color(.15f, .22f, .18f), .5f, .4f));
        var safeDoor = Group(c.props, "Safe_Door", new Vector3(2.15f, 0, 6.0f), -70);
        Box(safeDoor, "Door", new Vector3(.42f, .6f, 0), new Vector3(.82f, 1.1f, .08f), M("Safe_Green", new Color(.15f, .22f, .18f), .5f, .4f));
        Box(c.props, "Papers_OnFloor", new Vector3(2.0f, .01f, 5.6f), new Vector3(.6f, .01f, .4f), M("Paper", new Color(.9f, .88f, .8f), .1f), false);
        Box(c.props, "Curtain_Back", new Vector3(-2.4f, 1.1f, 6.9f), new Vector3(1.0f, 2.2f, .04f), M("Curtain_Red", new Color(.35f, .05f, .05f), .1f), false);
        Text(c.props, "Shop_Sign", "ЛОМБАРД · ВОЛКОВ И СЫНОВЬЯ", new Vector3(0, 2.7f, 6.88f), 0, .035f, new Color(.95f, .85f, .55f));
        PrecinctBuilder.Light(c.lights, "Light_Street", new Vector3(0, 2.0f, .6f), new Color(1f, .55f, .2f), 1.8f, 6f, true, false);
        PrecinctBuilder.Light(c.lights, "Light_SafeLamp", new Vector3(2.2f, 1.6f, 5.6f), new Color(1f, .78f, .5f), 1.6f, 3.5f, true, false);
        var torch = new GameObject("Sema_Flashlight").AddComponent<UnityEngine.Light>();
        torch.transform.SetParent(c.lights, false); torch.type = LightType.Spot; torch.spotAngle = 40; torch.range = 9; torch.intensity = 4;
        torch.color = new Color(1f, .95f, .85f); torch.transform.position = new Vector3(.3f, 1.3f, 4.85f);
        torch.transform.rotation = Quaternion.LookRotation(new Vector3(0, 1.2f, 0) - torch.transform.position); torch.shadows = LightShadows.Soft;

        c.npc = Npc(c, "Sema", new Vector3(0, 0, 5.0f), 180, new Color(.45f, .38f, .3f), false);
        Vector3 heroEnd = new Vector3(0, 0, 3.4f);
        var cut = c.cut = new GameObject("Cutscene").AddComponent<ScriptedCutscene>();
        cut.hero = c.hero; cut.shotCamera = c.cam; cut.npc = c.npc; cut.door = c.door; cut.doorAngle = -95; cut.doorOpenAt = .4f;
        cut.path = Path(c, Vector3.forward, new Vector3(0, 0, -.6f), heroEnd);
        cut.walkSpeed = 1.2f; cut.walkStart = 1.6f; cut.tail = 3.0f;
        var headTarget = Group(c.root, "Look_HeroEnd", heroEnd + Vector3.up * 1.55f);
        var semaHead = Group(c.root, "Look_Sema", new Vector3(0, 1.6f, 5.0f));
        cut.shots = new[]
        {
            Sh(0, Anchor(c, "Shot_SemaPOV", new Vector3(1.1f, 1.55f, 5.7f), new Vector3(0, 1.3f, 0)), null, 46),
            Follow(cut.walkStart + 1.4f, new Vector3(.4f, 1.68f, -1.5f), 48),
            Sh(cut.Duration - 2.8f, Anchor(c, "Shot_OverSema", new Vector3(.4f, 1.75f, 5.7f), heroEnd + Vector3.up * 1.5f), headTarget, 40),
        };
        cut.title = "ЛОМБАРД ОТЦА"; cut.subtitle = "Брайтон-Бич · 23:40";
        float D = cut.Duration;
        cut.dialogue = new[]
        {
            L(1.0f, 3.2f, "СЁМА", "Кто здесь?! У меня… у меня газовый пистолет!"),
            L(3.4f, 5.4f, "АЛЕКСЕЙ", "Это я, Сёма. Опусти. Что ты делаешь в закрытом ломбарде?"),
        };

        c.talk = Talk(c, "СЁМА", heroEnd, semaHead.position);
        var t = c.talk;
        t.probability = 45; t.stress = 50; t.successThreshold = 65;
        t.nodes = new[]
        {
            N("s1", new[] { "Алексей?! Ох… Я думал, это ваш отец вернулся." },
                C(0, "Что у сейфа?", "Магазин закрыт. Что ты делаешь у открытого сейфа в темноте?", 10, 15, "s2", "Я… проверял. Он просил, если что, проверить."),
                C(1, "Успокоить", "Тихо, Сёма. Я не за тобой. Я ищу отца.", 5, -15, "s2", "Слава богу… Я сам его ищу второй день."),
                C(2, "Руки на прилавок", "Руки на прилавок. Медленно.", -5, 25, "s2", "Вы что?! Я тут двенадцать лет работаю!")),
            N("s2", new[] { "Позавчера он закрыл кассу раньше. Сказал, что ему надо кое-что спрятать." },
                C(0, "Тайник", "Ключи от ломбарда остались дома, фото пропало из рамки. Он что-то спрятал здесь. Где?", 15, 5, "s3", "В сейфе есть второе дно. Я как раз… хотел посмотреть.", "concl_pawnshop"),
                C(1, "Что спрятать?", "Что именно он хотел спрятать?", 5, 0, "s3", "Не знаю! Папку какую-то. Бумаги."),
                C(2, "Фотография", "Из рамки пропало наше семейное фото. Ты его видел?", 10, 5, "s3", "Фото… Он клал его в сейф. Вместе с бумагами.", "clue_photo")),
            N("s3", new[] { "Там… расписки. Ваш брат закладывал часы отца. Три раза. Отец всё выкупал." },
                C(0, "Какой брат?", "Какой брат, Сёма? Дмитрий?", 10, 5, "s4", "Нет. Михаил. Он брал деньги у плохих людей."),
                C(1, "Почему молчал?", "Ты знал и молчал?", -5, 15, "s4", "Это не моё дело! Отец просил никому не говорить."),
                C(2, "Открывай сейф", "Открывай сейф. Сейчас.", 5, 10, "s4", "Хорошо, хорошо… Только не кричите.")),
            N("s4", new[] { "Вчера заходил человек. Со шрамом. Спрашивал отца. Сказал — от Резо." },
                C(0, "Опиши его", "Опиши его. Каждую мелочь.", 15, -5, "END"),
                C(1, "Ты что-то сказал?", "Ты сказал ему, где живёт отец?", 5, 20, "END"),
                C(2, "Уезжай", "Закрой ломбард и уезжай к сестре. Сегодня же.", 10, -15, "END")),
        };
        t.successTitle = "СЁМА ОТКРЫЛ СЕЙФ";
        t.successText = "Во втором дне сейфа — расписки Михаила: трижды закладывал отцовские часы, и каждый раз отец их выкупал. Рядом семейное фото и адрес склада на Ван-Брант-стрит.\nОтец собирал доказательства — и молчал, чтобы защитить сына.";
        t.failTitle = "СЁМА ПРОМОЛЧАЛ";
        t.failText = "Сёма запирается в подсобке и требует адвоката. Сейф остался закрытым — а вместе с ним и тайна ломбарда.";
        t.shutdownText = "Сёма в панике выбегает через чёрный ход. Ищи его теперь по всему Брайтону.";
        t.endingTag = "НА ДОСКЕ УЛИК ДОСТУПНА НОВАЯ СВЯЗКА";
        t.grantId = "clue_receipts"; t.grantTitle = "Расписки Михаила"; t.grantText = "В сейфе ломбарда: Михаил трижды закладывал отцовские часы. Отец выкупал их и молчал.";
        Finish(c, "Assets/_Project/Scenes/04_Pawnshop.unity", log, "pawnshop");
    }

    // ================================================================== 05 MIKHAIL — the brother
    static void BuildMikhail(StringBuilder log)
    {
        var c = Begin("05_Mikhail", new Color(.05f, .06f, .08f));
        var floor = M("Flat_Floor", new Color(.3f, .22f, .15f), .4f);
        var wall = M("Flat_Wall", new Color(.42f, .45f, .5f), .15f);
        var ceil = M("Flat_Ceiling", new Color(.6f, .6f, .6f), .1f);
        var fabric = M("Sofa_Fabric", new Color(.28f, .25f, .18f), .15f);
        Room(c, -4, 4, 0, 7, 2.7f, floor, wall, ceil, -2.6f, new Color(.9f, .85f, .6f));
        Box(c.props, "Sofa_Seat", new Vector3(0, .25f, 6.0f), new Vector3(2.3f, .45f, .9f), fabric);
        Box(c.props, "Sofa_Back", new Vector3(0, .65f, 6.45f), new Vector3(2.3f, .8f, .2f), fabric);
        Box(c.props, "Sofa_Arm_L", new Vector3(-1.2f, .45f, 6.0f), new Vector3(.2f, .5f, .9f), fabric);
        Box(c.props, "Sofa_Arm_R", new Vector3(1.2f, .45f, 6.0f), new Vector3(.2f, .5f, .9f), fabric);
        Box(c.props, "CoffeeTable", new Vector3(0, .38f, 4.7f), new Vector3(1.2f, .06f, .6f), M("Desk_Wood", new Color(.33f, .24f, .17f), .35f));
        for (int i = 0; i < 4; i++) Prim(c.props, PrimitiveType.Cylinder, "Beer_" + i, new Vector3(-.4f + i * .22f, .47f, 4.6f + (i % 2) * .15f), new Vector3(.07f, .07f, .07f), M("Bottle_Amber", new Color(.6f, .3f, .05f), .9f));
        Box(c.props, "Phone_Red", new Vector3(.4f, .45f, 4.8f), new Vector3(.2f, .08f, .22f), M("Phone_Red", new Color(.6f, .05f, .05f), .5f), false);
        Box(c.props, "Bills", new Vector3(-.3f, .415f, 4.85f), new Vector3(.3f, .01f, .2f), M("Paper", new Color(.9f, .88f, .8f), .1f), false);
        var tv = Group(c.props, "TV", new Vector3(2.9f, 0, 4.0f), -55);
        Box(tv, "Stand", new Vector3(0, .3f, 0), new Vector3(.9f, .6f, .5f), M("Desk_Wood", new Color(.33f, .24f, .17f), .35f));
        Box(tv, "Body", new Vector3(0, .9f, 0), new Vector3(.7f, .6f, .55f), M("Goods_Dark", new Color(.15f, .15f, .16f), .3f));
        Box(tv, "Screen", new Vector3(0, .92f, .28f), new Vector3(.56f, .44f, .01f), M("TV_Glow", new Color(.3f, .4f, .6f), .8f, 0, new Color(.6f, .9f, 1.4f)), false);
        Box(c.props, "Kitchen_Counter", new Vector3(-3.6f, .45f, 4.5f), new Vector3(.6f, .9f, 2.6f), M("Counter_Top", new Color(.5f, .48f, .42f), .5f));
        Box(c.props, "Fridge", new Vector3(-3.6f, .85f, 6.4f), new Vector3(.65f, 1.7f, .6f), M("CRT_Plastic", new Color(.78f, .76f, .68f), .3f));
        Box(c.props, "Poster", new Vector3(1.2f, 1.6f, 6.92f), new Vector3(.8f, 1.0f, .01f), M("Poster", new Color(.15f, .25f, .55f), .2f), false);
        Text(c.props, "Poster_Text", "ХОККЕЙ 1999", new Vector3(1.2f, 1.7f, 6.9f), 0, .02f, Color.white);
        Box(c.props, "Bag_Packed", new Vector3(-1.9f, .25f, 5.9f), new Vector3(.7f, .45f, .35f), M("NPC_Pants", new Color(.08f, .09f, .12f), .2f));
        var tvLight = new GameObject("Light_TV").AddComponent<UnityEngine.Light>();
        tvLight.transform.SetParent(c.lights, false); tvLight.transform.position = new Vector3(2.4f, 1.0f, 4.4f);
        tvLight.type = LightType.Point; tvLight.color = new Color(.5f, .75f, 1f); tvLight.intensity = 2.2f; tvLight.range = 5.5f; tvLight.shadows = LightShadows.Soft;
        PrecinctBuilder.Light(c.lights, "Light_Kitchen", new Vector3(-3.3f, 2.3f, 4.5f), new Color(1f, .85f, .6f), 1.0f, 4f, false, false);
        PrecinctBuilder.Light(c.lights, "Light_Door", new Vector3(-2.6f, 2.0f, .5f), new Color(.95f, .9f, .7f), 1.0f, 3.5f, false, false);

        c.npc = Npc(c, "Mikhail", new Vector3(0, .1f, 5.75f), 180, new Color(.5f, .1f, .1f), true);
        Vector3 heroEnd = new Vector3(0, 0, 3.5f);
        var cut = c.cut = new GameObject("Cutscene").AddComponent<ScriptedCutscene>();
        cut.hero = c.hero; cut.shotCamera = c.cam; cut.npc = c.npc; cut.door = c.door; cut.doorAngle = -95; cut.doorOpenAt = .3f;
        cut.path = Path(c, Vector3.forward, new Vector3(-2.6f, 0, -.6f), new Vector3(-1.8f, 0, 2.0f), heroEnd);
        cut.walkSpeed = 1.2f; cut.walkStart = 1.5f; cut.tail = 3.0f;
        var headTarget = Group(c.root, "Look_HeroEnd", heroEnd + Vector3.up * 1.55f);
        var mHead = Group(c.root, "Look_Mikhail", new Vector3(0, 1.43f, 5.75f));
        cut.shots = new[]
        {
            Sh(0, Anchor(c, "Shot_FromSofa", new Vector3(1.6f, 1.35f, 5.3f), new Vector3(-2.6f, 1.2f, .3f)), null, 48),
            Follow(cut.walkStart + 1.6f, new Vector3(.4f, 1.68f, -1.5f), 48),
            Sh(cut.Duration - 2.8f, Anchor(c, "Shot_OverMikhail", new Vector3(.45f, 1.5f, 6.25f), heroEnd + Vector3.up * 1.5f), headTarget, 42),
        };
        cut.title = "КВАРТИРА МИХАИЛА"; cut.subtitle = "Шипсхед-Бей · 00:40";
        float D = cut.Duration;
        cut.dialogue = new[]
        {
            L(1.2f, 3.4f, "МИХАИЛ", "Открыто… Лёша? Ты чего так поздно?"),
            L(D - 2.6f, D - .1f, "МИХАИЛ", "Есть новости про папу? Я всё звоню — никто не берёт."),
        };

        c.talk = Talk(c, "МИХАИЛ", heroEnd, mHead.position);
        var t = c.talk;
        t.probability = 35; t.stress = 45; t.successThreshold = 80;
        t.nodes = new[]
        {
            N("m1", new[] { "Садись. Будешь пиво? Ты как будто с допроса." },
                C(0, "Записка", "Я нашёл у отца под кроватью записку. Твой почерк: «Папа, не говори Лёше. Я всё верну до пятницы».", 20, 20, "m2", "…Ты рылся у него в спальне?", "clue_secret_note"),
                C(1, "Где ты был?", "Где ты был вчера вечером, Миша?", 5, 10, "m2", "Дома. Где ещё. Хоккей смотрел."),
                C(2, "Как ты?", "Как ты? Держишься?", 0, -15, "m2", "Как… Не спал. Всё время звоню ему.")),
            N("m2", new[] { "Я же тебе говорил — отец не отвечает со вчерашнего вечера. Я сразу тебе позвонил." },
                C(0, "Гостем был ты", "Две чашки на столе. Гость, которому он сам налил кофе. Это был ты, Миша.", 20, 20, "m3", "Я… просто зашёл. Он сам налил.", "concl_betrayal"),
                C(1, "Расписки", "Сёма показал расписки. Ты три раза закладывал папины часы.", 15, 15, "m3", "Сёма… Крыса.", "clue_receipts"),
                C(2, "Блефовать", "Соседка видела, как ты поднимался к нему вечером.", 5, 15, "m3", "Какая соседка? Она слепая на один глаз!"),
                C(3, "Деньги", "Ты опять играешь? Сколько ты должен?", 10, 10, "m3", "Это не твоё дело.")),
            N("m3", new[] { "Ладно. Да. Я был там. Я должен людям Резо. Много." },
                C(0, "Что ты им отдал?", "Что ты им отдал, Миша?", 10, 5, "m4", "Письмо было от них. Отец видел, кто поджёг склад. Они хотели, чтобы он молчал… А я сказал им, что он ходил в полицию."),
                C(1, "Ударить по столу", "Ты сдал отца бандитам?!", -10, 25, "m4", "Я не сдавал! Я думал, они просто припугнут!"),
                C(2, "Понять", "Я понимаю. Ты запутался. Говори — я помогу.", 10, -15, "m4", "Ты не поймёшь… Они сказали: или он, или я.")),
            N("m4", new[] { "Они увезли его. Я не знаю куда. Клянусь." },
                C(0, "Смотри в глаза", "Ты знаешь. Посмотри мне в глаза.", 10, 15, "END"),
                C(1, "Вытащим вместе", "Он наш отец. Мы вытащим его вместе. Где он?", 15, -10, "END"),
                C(2, "Склад на Ван-Брант", "Склад рефрижераторов на Ван-Брант-стрит. Я уже знаю. Мне нужно только время и вход.", 20, 5, "END", null, "clue_warehouse"),
                C(3, "Задержать", "Михаил Волков, вы задержаны.", -15, 20, "END")),
        };
        t.successTitle = "МИХАИЛ ПРИЗНАЛСЯ";
        t.successText = "Михаил ломается: «Склад рефрижераторов на Ван-Брант-стрит. Сегодня ночью его перевезут». Братья едут вместе — Михаил знает, где задний вход и сколько там людей.\nПод утро отец сидит на заднем сиденье — живой. Михаил даёт показания против Резо. Семья снова вместе — какой бы ценой это ни далось.";
        t.failTitle = "ПЛОХАЯ КОНЦОВКА: ТЫ ОПОЗДАЛ";
        t.failText = "Михаил выставляет тебя за дверь и тут же кому-то звонит. Через час склад на Ван-Брант-стрит пуст. Отца там уже нет.";
        t.shutdownTitle = "ПЛОХАЯ КОНЦОВКА: БРАТ СБЕЖАЛ";
        t.shutdownText = "Михаил хватает сумку и уходит по пожарной лестнице. Ты остаёшься один в пустой квартире с работающим телевизором.";
        t.endingTag = "ЛУЧШАЯ КОНЦОВКА: СЕМЬЯ";
        Finish(c, "Assets/_Project/Scenes/05_Mikhail.unity", log, "mikhail");
    }
}
