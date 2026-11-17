using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CarXCase.Intro;

[InitializeOnLoad]
public static class BrightonIntroBuilder
{
    const string ScenePath = "Assets/Scenes/00_BrightonDrive.unity";
    const string ApartmentPath = "Assets/Scenes/FatherApartment.unity";
    static readonly string Request = Path.GetFullPath("Temp/BrightonIntro.request");
    static BrightonIntroBuilder() { EditorApplication.update += PollRequest; }
    static void PollRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Build(); }
        catch (Exception e) { File.WriteAllText("Logs/BrightonIntroBuild.txt", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Greybox/Build Brighton Driving Intro")]
    public static void Build()
    {
        if (!File.Exists(ApartmentPath)) throw new Exception("FatherApartment scene is missing.");
        if (File.Exists(ScenePath)) throw new Exception("Intro already exists. Open Assets/Scenes/00_BrightonDrive.unity to edit it.");
        // Preserve any current apartment edits before opening the new intro.
        if (!EditorSceneManager.SaveOpenScenes()) throw new Exception("Could not save open scenes.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory("Assets/Materials/BrightonIntro");
        var asphalt = Mat("Road", new Color(.17f,.18f,.19f));
        var pavement = Mat("Pavement", new Color(.51f,.49f,.44f));
        var steel = Mat("Steel", new Color(.22f,.28f,.27f));
        var cream = Mat("Cream", new Color(.7f,.65f,.53f));
        var brick = Mat("Brick", new Color(.46f,.29f,.22f));
        var redbrick = Mat("RedBrick", new Color(.57f,.37f,.29f));
        var glass = Mat("Window", new Color(.20f,.31f,.36f));
        var trim = Mat("Trim", new Color(.07f,.08f,.09f));
        var cloth = Mat("SeatCloth", new Color(.27f,.25f,.23f));
        var coat = Mat("Coat", new Color(.30f,.33f,.31f));
        var skin = Mat("Skin", new Color(.67f,.50f,.36f));
        var white = Mat("RoadPaint", new Color(.86f,.80f,.61f));
        var blue = Mat("CarPaint", new Color(.20f,.29f,.35f));
        var signMat = Mat("SignRed", new Color(.37f,.12f,.10f));
        var world = new GameObject("Brighton_Street").transform;
        Box(world,"Road",new Vector3(0,-.12f,330),new Vector3(9,.2f,760),asphalt);
        for (int side=-1;side<=1;side+=2)
        {
            Box(world,"Sidewalk_"+side,new Vector3(side*6.1f,-.02f,330),new Vector3(3.2f,.22f,760),pavement);
            Box(world,"Curb_"+side,new Vector3(side*4.55f,.06f,330),new Vector3(.18f,.24f,760),cream);
        }
        for(int z=-35;z<710;z+=10)
            Box(world,"Centerline_"+z,new Vector3(0,.005f,z),new Vector3(.1f,.01f,4),white);
        // A modest repeating street set, framed from inside the car.
        string[] shops = { "ПРОДУКТЫ", "BRIGHTON DELI", "АПТЕКА", "BAKERY", "ВИДЕО", "LAUNDRY" };
        for(int i=0;i<47;i++)
        {
            float z=-24+i*16;
            for(int side=-1;side<=1;side+=2)
            {
                float h=7+(i%3)*1.2f;
                var building=new GameObject("Building_"+side+"_"+i).transform; building.SetParent(world,false);
                Box(building,"Facade",new Vector3(side*10.8f,h/2,z),new Vector3(6.2f,h,15.3f), i%3==0?cream:(i%2==0?brick:redbrick));
                Box(building,"Cornice",new Vector3(side*7.65f,h-.2f,z),new Vector3(.35f,.35f,15.5f),cream);
                for(int row=0;row<2;row++) for(int col=0;col<4;col++)
                    Box(building,"Window_"+row+"_"+col,new Vector3(side*7.66f,4.5f+row*2.2f,z-5.4f+col*3.6f),new Vector3(.06f,1.35f,1.3f),glass);
                Box(building,"ShopWindows",new Vector3(side*7.64f,1.4f,z),new Vector3(.07f,2.1f,10),glass);
                Box(building,"ShopDoor",new Vector3(side*7.57f,1.1f,z+5.8f),new Vector3(.1f,2.2f,1.1f),trim);
                Box(building,"ShopSign",new Vector3(side*7.5f,2.95f,z),new Vector3(.2f,.65f,11.8f),signMat);
                Label(building,"ShopName",shops[(i+(side==1?1:0))%shops.Length],new Vector3(side*7.37f,2.95f,z),new Vector3(0,side==1?90:-90,0),.09f);
            }
        }
        // Elevated railway: columns and girders, open daylight between crossbeams.
        for(int z=-20;z<680;z+=18)
        {
            for(int side=-1;side<=1;side+=2)
                Box(world,"Rail_Column_"+side+"_"+z,new Vector3(side*4.05f,2.85f,z),new Vector3(.3f,5.7f,.4f),steel);
            Box(world,"Rail_Crossbeam_"+z,new Vector3(0,5.7f,z),new Vector3(8.5f,.45f,.4f),steel);
        }
        for(int side=-1;side<=1;side+=2)
        {
            Box(world,"Rail_Girder_"+side,new Vector3(side*2.7f,6.1f,330),new Vector3(.3f,.8f,740),steel);
            Box(world,"Rail_Track_"+side,new Vector3(side*1.1f,6.3f,330),new Vector3(.1f,.13f,740),trim);
        }
        for(int z=-20;z<690;z+=3) Box(world,"Rail_Sleeper_"+z,new Vector3(0,6.0f,z),new Vector3(6,.15f,.18f),steel);
        for(int i=0;i<32;i++)
        {
            float z=10+i*18;
            if(z>570) continue;
            int side=i%2==0?-1:1;
            Box(world,"ParkedCar_Body_"+i,new Vector3(side*3.25f,.65f,z),new Vector3(1.5f,.7f,3.7f),i%3==0?brick:blue);
            Box(world,"ParkedCar_Cabin_"+i,new Vector3(side*3.25f,1.25f,z-.2f),new Vector3(1.35f,.65f,1.9f),glass);
        }
        // Visible destination on the right as the car slows and pulls to the curb.
        Box(world,"Father_House_Entry",new Vector3(7.35f,1.25f,603),new Vector3(.25f,2.5f,1.6f),trim);
        Box(world,"Father_House_Canopy",new Vector3(6.7f,2.65f,603),new Vector3(1.6f,.18f,3),signMat);
        Label(world,"HouseNumber","3042",new Vector3(7.15f,2.18f,603),new Vector3(0,90,0),.18f);

        var car=new GameObject("Hero_Car").transform; car.position=new Vector3(1.7f,0,0);
        Box(car,"Car_Floor",new Vector3(0,.42f,0),new Vector3(1.9f,.18f,4.4f),blue);
        Box(car,"Car_Hood",new Vector3(0,.84f,1.65f),new Vector3(1.85f,.22f,1.3f),blue);
        Box(car,"Car_Trunk",new Vector3(0,.75f,-1.85f),new Vector3(1.85f,.4f,.7f),blue);
        Box(car,"Dashboard",new Vector3(0,.98f,.95f),new Vector3(1.72f,.25f,.48f),trim);
        Box(car,"Radio",new Vector3(.12f,.99f,.69f),new Vector3(.35f,.12f,.035f),cloth);
        Box(car,"Roof",new Vector3(0,2.14f,-.15f),new Vector3(1.88f,.1f,2.7f),cloth);
        for(int side=-1;side<=1;side+=2)
        {
            Box(car,"Door_"+side,new Vector3(side*.9f,.78f,-.1f),new Vector3(.12f,.6f,2.8f),cloth);
            Box(car,"A_Pillar_"+side,new Vector3(side*.84f,1.60f,1),new Vector3(.09f,1.09f,.1f),cloth,new Vector3(-20,0,0));
            Box(car,"B_Pillar_"+side,new Vector3(side*.87f,1.60f,-.65f),new Vector3(.09f,1.09f,.1f),cloth);
            Box(car,"FrontSeat_Base_"+side,new Vector3(side*.43f,.64f,-.1f),new Vector3(.68f,.2f,.7f),cloth);
            Box(car,"FrontSeat_Back_"+side,new Vector3(side*.43f,1.03f,-.42f),new Vector3(.68f,.78f,.15f),cloth,new Vector3(-8,0,0));
            Box(car,"Headrest_"+side,new Vector3(side*.43f,1.33f,-.46f),new Vector3(.32f,.18f,.12f),cloth);
        }
        Box(car,"RearSeat",new Vector3(0,.72f,-1.3f),new Vector3(1.6f,.25f,.6f),cloth);
        // A single seated driver, holding the phone with his right hand.
        var driver=new GameObject("Driver").transform; driver.SetParent(car,false);
        Box(driver,"Torso",new Vector3(-.43f,1.06f,-.12f),new Vector3(.42f,.55f,.30f),coat);
        Primitive(driver,"Head",PrimitiveType.Capsule,new Vector3(-.43f,1.5f,-.06f),new Vector3(.24f,.17f,.24f),skin);
        Box(driver,"Hair",new Vector3(-.43f,1.61f,-.09f),new Vector3(.24f,.12f,.23f),trim);
        for(int side=-1;side<=1;side+=2)
            Box(driver,"Leg_"+side,new Vector3(-.43f+side*.12f,.67f,.25f),new Vector3(.16f,.17f,.65f),coat);
        Segment(driver,"LeftArm",new Vector3(-.65f,1.23f,-.06f),new Vector3(-.65f,1.08f,.52f),.13f,coat);
        Segment(driver,"RightUpperArm",new Vector3(-.21f,1.22f,-.08f),new Vector3(-.08f,1.03f,.14f),.13f,coat);
        Segment(driver,"RightForearm",new Vector3(-.08f,1.03f,.14f),new Vector3(-.24f,1.48f,.02f),.11f,coat);
        Box(driver,"PhoneHand",new Vector3(-.24f,1.44f,.02f),new Vector3(.09f,.14f,.09f),skin);
        Box(driver,"Phone_1999",new Vector3(-.27f,1.48f,-.01f),new Vector3(.055f,.22f,.09f),trim);
        Box(driver,"Phone_Antenna",new Vector3(-.27f,1.63f,-.01f),new Vector3(.018f,.1f,.018f),trim);
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI/6;
            Box(car,"SteeringWheel_"+i,new Vector3(-.43f+Mathf.Cos(a)*.20f,1.12f+Mathf.Sin(a)*.20f,.58f),new Vector3(.11f,.04f,.04f),trim,new Vector3(0,0,i*30+90));
        }
        var wheelList=new System.Collections.Generic.List<Transform>();
        for(int side=-1;side<=1;side+=2) for(int axle=-1;axle<=1;axle+=2)
        {
            var w=Primitive(car,"Wheel_"+side+"_"+axle,PrimitiveType.Cylinder,new Vector3(side*.92f,.32f,axle*1.42f),new Vector3(.64f,.12f,.64f),trim);
            w.localRotation=Quaternion.Euler(0,0,90); wheelList.Add(w);
        }
        var cameraObject=new GameObject("Main Camera");
        cameraObject.tag="MainCamera";
        cameraObject.transform.SetParent(car,false);
        cameraObject.transform.localPosition=new Vector3(.23f,1.55f,-.85f);
        cameraObject.transform.localRotation=Quaternion.Euler(2,0,0);
        var camera=cameraObject.AddComponent<Camera>();
        camera.fieldOfView=64; camera.nearClipPlane=.04f; camera.farClipPlane=230;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.64f,.73f,.79f);
        cameraObject.AddComponent<AudioListener>();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var sun=new GameObject("Directional Light").AddComponent<Light>();
        sun.type=LightType.Directional; sun.intensity=1.35f; sun.color=new Color(1,.91f,.76f);
        sun.transform.rotation=Quaternion.Euler(42,-35,0); sun.shadows=LightShadows.Soft;
        RenderSettings.sun=sun; RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.66f,.72f,.78f);
        RenderSettings.ambientEquatorColor=new Color(.43f,.43f,.40f);
        RenderSettings.ambientGroundColor=new Color(.25f,.23f,.20f);
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear;
        RenderSettings.fogColor=camera.backgroundColor; RenderSettings.fogStartDistance=100; RenderSettings.fogEndDistance=220;
        var controller=new GameObject("Cutscene_Director").AddComponent<BrightonDriveCutscene>();
        controller.car=car; controller.cameraRig=camera.transform; controller.wheels=wheelList.ToArray();
        controller.dialogue=Dialogue();
        EditorSceneManager.SaveScene(scene,ScenePath);
        var previous=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath && s.path!=ApartmentPath).ToList();
        previous.Insert(0,new EditorBuildSettingsScene(ApartmentPath,true));
        previous.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
        EditorBuildSettings.scenes=previous.ToArray();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject=controller.gameObject;
        File.WriteAllText("Logs/BrightonIntroBuild.txt","SUCCESS: "+ScenePath+"; duration=150; distance=600; dialogue="+controller.dialogue.Length+"; FatherApartment registered.");
        Debug.Log("Brighton intro created and saved. Press Play. Space/Escape skips to FatherApartment.");
    }
    static BrightonDriveCutscene.DialogueLine[] Dialogue()
    {
        return new[] {
            Line(9,14,"АЛЕКСЕЙ","Миша? Я уже на Брайтоне. Что у тебя случилось?"),
            Line(16,22,"МИХАИЛ · ПО ТЕЛЕФОНУ","У меня ничего. Отец со вчерашнего вечера не отвечает."),
            Line(25,31,"АЛЕКСЕЙ","Он иногда выключает телефон. Ты же его знаешь."),
            Line(34,41,"МИХАИЛ · ПО ТЕЛЕФОНУ","Знаю. Но сегодня он не открыл магазин. Соседка звонила мне на работу."),
            Line(44,50,"АЛЕКСЕЙ","Может, ушёл по делам. Я заеду, посмотрю."),
            Line(53,60,"МИХАИЛ · ПО ТЕЛЕФОНУ","Вчера он просил найти тебя. Сказал, что нужно поговорить лично."),
            Line(63,69,"АЛЕКСЕЙ","О чём? Вы опять поссорились?"),
            Line(72,79,"МИХАИЛ · ПО ТЕЛЕФОНУ","Нет. В том-то и дело. Он был какой-то тихий. Всё спрашивал, один ли я."),
            Line(83,90,"АЛЕКСЕЙ","Ладно. До дома ещё пара кварталов. Ключ у меня остался."),
            Line(94,101,"МИХАИЛ · ПО ТЕЛЕФОНУ","Лёш… Если он дома, пусть просто перезвонит. Не начинай с ним спорить."),
            Line(105,111,"АЛЕКСЕЙ","Я еду проверить, как он. Больше ничего."),
            Line(115,122,"МИХАИЛ · ПО ТЕЛЕФОНУ","Хорошо. Я буду у телефона."),
            Line(129,135,"АЛЕКСЕЙ","Вижу дом. Сейчас припаркуюсь. Позвоню из квартиры."),
            Line(137,141,"МИХАИЛ · ПО ТЕЛЕФОНУ","Давай. И… спасибо.")
        };
    }
    static BrightonDriveCutscene.DialogueLine Line(float start,float end,string speaker,string text)
        => new BrightonDriveCutscene.DialogueLine{start=start,end=end,speaker=speaker,text=text};
    static Material Mat(string name,Color color)
    {
        string path="Assets/Materials/BrightonIntro/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat) {mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path);}
        mat.color=color; mat.SetFloat("_Smoothness",.15f); EditorUtility.SetDirty(mat); return mat;
    }
    static Transform Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
        go.transform.localPosition=position; go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=mat;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go.transform;
    }
    static Transform Box(Transform parent,string name,Vector3 position,Vector3 scale,Material mat,Vector3 rotation=default)
    {
        var t=Primitive(parent,name,PrimitiveType.Cube,position,scale,mat); t.localRotation=Quaternion.Euler(rotation); return t;
    }
    static void Segment(Transform parent,string name,Vector3 from,Vector3 to,float width,Material mat)
    {
        var t=Box(parent,name,(from+to)/2,new Vector3(width,(to-from).magnitude,width),mat);
        t.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);
    }
    static void Label(Transform parent,string name,string text,Vector3 position,Vector3 rotation,float size)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=position;
        go.transform.localRotation=Quaternion.Euler(rotation);
        var label=go.AddComponent<TextMesh>(); label.text=text; label.fontSize=64; label.characterSize=size;
        label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center; label.color=new Color(1,.89f,.65f);
    }
}
