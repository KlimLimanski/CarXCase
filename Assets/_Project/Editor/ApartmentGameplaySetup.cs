using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DetectiveGame.Core;

[InitializeOnLoad]
public static class ApartmentGameplaySetup
{
    static ApartmentGameplaySetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/ApartmentGameplay.request")) return;
        File.Delete("Temp/ApartmentGameplay.request");
        try{Setup(); File.WriteAllText("Logs/ApartmentGameplay.txt","SETUP SUCCESS\n");}
        catch(Exception e){File.WriteAllText("Logs/ApartmentGameplay.txt",e.ToString());Debug.LogException(e);}
    }
    [MenuItem("Tools/Greybox/Connect Apartment Gameplay")]
    public static void Setup()
    {
        EditorSceneManager.SaveOpenScenes();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/FatherApartment.unity");
        var player=GameObject.Find("Player");
        if(!player) throw new Exception("Missing Player.");
        var cc=player.GetComponent<CharacterController>();
        cc.enabled=false;
        player.transform.position=new Vector3(0,.05f,-1.5f);
        player.transform.rotation=Quaternion.identity;
        cc.height=1.8f; cc.radius=.28f; cc.center=new Vector3(0,.9f,0); cc.stepOffset=.22f;
        cc.enabled=true;
        var oldCapsule=player.transform.Find("Capsule");
        if(oldCapsule) UnityEngine.Object.DestroyImmediate(oldCapsule.gameObject);
        if(!player.transform.Find("Character_Visual"))
        {
            var visual=new GameObject("Character_Visual").transform;visual.SetParent(player.transform,false);
            var coat=Mat("Mat_Character_Coat",new Color(.25f,.29f,.27f));
            var skin=Mat("Mat_Character_Skin",new Color(.67f,.50f,.36f));
            var dark=Mat("Mat_Character_Dark",new Color(.12f,.13f,.15f));
            Part(visual,"Torso",new Vector3(0,1.15f,0),new Vector3(.48f,.65f,.28f),coat);
            Part(visual,"Head",new Vector3(0,1.65f,0),new Vector3(.25f,.3f,.25f),skin);
            for(int side=-1;side<=1;side+=2)
            {
                Part(visual,"Arm_"+side,new Vector3(side*.31f,1.09f,0),new Vector3(.13f,.65f,.18f),coat);
                Part(visual,"Leg_"+side,new Vector3(side*.13f,.46f,0),new Vector3(.19f,.7f,.22f),dark);
                Part(visual,"Shoe_"+side,new Vector3(side*.13f,.09f,.06f),new Vector3(.21f,.16f,.34f),dark);
            }
        }
        var camera=Camera.main;
        camera.transform.SetParent(null,true);
        camera.transform.position=player.transform.position+new Vector3(0,1.85f,-1.55f);
        camera.transform.rotation=Quaternion.Euler(12,0,0);
        var follow=camera.GetComponent<ThirdPersonCamera>() ?? camera.gameObject.AddComponent<ThirdPersonCamera>();
        follow.target=player.transform;follow.offset=new Vector3(0,1.45f,0);
        follow.distance=1.65f; follow.minDistance=.3f;follow.maxDistance=2.5f;camera.nearClipPlane=.06f;
        var hud=UnityEngine.Object.FindAnyObjectByType<SimpleUIHUD>();
        if(!hud) hud=new GameObject("Gameplay_HUD").AddComponent<SimpleUIHUD>();
        if(!UnityEngine.Object.FindAnyObjectByType<QteManager>()) hud.gameObject.AddComponent<QteManager>();
        var officer=player.GetComponent<OfficerController>() ?? player.AddComponent<OfficerController>();
        officer.cameraTransform=camera.transform;officer.hud=hud;
        officer.walkSpeed=2f;officer.runSpeed=3.5f;officer.interactionRadius=1.8f;
        var torch=player.transform.Find("Player_Flashlight");
        if(!torch)
        {
            torch=new GameObject("Player_Flashlight").transform;torch.SetParent(player.transform,false);
            torch.localPosition=new Vector3(.2f,1.3f,.15f);
            var light=torch.gameObject.AddComponent<Light>();light.type=LightType.Spot;
            light.intensity=2;light.range=8;light.spotAngle=48;light.enabled=false;
        }
        officer.flashlight=torch.GetComponent<Light>();
        Clue("Briefcase_Prop","clue_briefcase","Портфель отца","Портфель оставлен на столе. Обычно отец берёт его с собой. Замки закрыты, рядом никого.");
        Clue("Chair_Fallen","clue_fallen_chair","Опрокинутый стул","Стул лежит посреди комнаты. Похоже, его резко отодвинули или задели в спешке.");
        Clue("FloorLamp_Broken","clue_broken_lamp","Упавшая лампа","Лампа повалена. На её корпусе свежая вмятина. Что здесь произошло?");
        var wardrobe=GameObject.Find("Wardrobe");
        var qte=wardrobe.GetComponent<QteTrigger>() ?? wardrobe.AddComponent<QteTrigger>();
        qte.actionName="Открыть заклинивший шкаф";qte.qteType=QteType.SinglePress;
        qte.targetKey=UnityEngine.InputSystem.Key.Space;qte.keyDisplayName="ПРОБЕЛ";qte.duration=3;
        qte.targetRenderer=wardrobe.GetComponent<Renderer>();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();Selection.activeGameObject=player;
        Debug.Log("Apartment gameplay connected: WASD, mouse, E clues/QTE, F flashlight.");
    }
    static Material Mat(string name,Color color)
    {
        string path="Assets/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;AssetDatabase.CreateAsset(mat,path);}return mat;
    }
    static void Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Clue(string name,string id,string title,string description)
    {
        var go=GameObject.Find(name);var clue=go.GetComponent<ClueObject>() ?? go.AddComponent<ClueObject>();
        clue.clueId=id;clue.clueTitle=title;clue.clueDescription=description;clue.targetRenderer=go.GetComponent<Renderer>();
    }
}
