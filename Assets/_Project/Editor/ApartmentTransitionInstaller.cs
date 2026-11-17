using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ApartmentTransitionInstaller
{
    const string P="Assets/_Project/";
    const string Intro=P+"Scenes/00_BrightonDrive_Models.unity";
    const string Destination=P+"Scenes/01_FatherApartment.unity";
    static ApartmentTransitionInstaller(){EditorApplication.delayCall+=Install;}
    static void Install()
    {
        string request=P+"Editor/ApartmentTransition.request";
        if(!File.Exists(request)||EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        File.Delete(request);
        try {
            if(File.Exists(Destination))throw new Exception("Destination already exists; refusing to overwrite.");
            var current=SceneManager.GetActiveScene();
            if(current.path!=Intro)throw new Exception("Open 00_BrightonDrive_Models first.");
            if(!EditorSceneManager.SaveScene(current,P+"Scenes/BeforeApartmentTransition_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true))throw new Exception("Backup failed.");
            if(!EditorSceneManager.SaveScene(current))throw new Exception("Intro save failed.");
            foreach(string p in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith(P+"Arts/Apartaments/Textures/")&&p.EndsWith(".png"))) {
                var ti=AssetImporter.GetAtPath(p) as TextureImporter;
                if(ti==null)continue;
                if(p.EndsWith("_Normal.png")){ti.textureType=TextureImporterType.NormalMap;ti.sRGBTexture=false;}
                ti.wrapMode=TextureWrapMode.Repeat;ti.maxTextureSize=2048;ti.SaveAndReimport();
            }
            ApartmentSetup.Build();
            if(SceneManager.GetActiveScene().path!=Destination)throw new Exception("Apartment was not saved.");
            var apartment=SceneManager.GetActiveScene();
            var player=GameObject.Find("Player");
            if(!player || !player.GetComponent<CharacterController>()||!player.GetComponent<ApartmentExplorer>())throw new Exception("Player incomplete.");
            var intro=EditorSceneManager.OpenScene(Intro,OpenSceneMode.Additive);
            int updated=0;
            foreach(var root in intro.GetRootGameObjects())foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) {
                if(!behaviour||behaviour.GetType().Name!="BrightonDriveCutscene")continue;
                var so=new SerializedObject(behaviour);var target=so.FindProperty("destinationScene");
                if(target==null)throw new Exception("destinationScene field missing.");
                target.stringValue="01_FatherApartment";so.ApplyModifiedProperties();updated++;
            }
            if(updated!=1)throw new Exception("Expected one cutscene director, found "+updated);
            var scenes=EditorBuildSettings.scenes.ToList();
            foreach(string path in new[]{Intro,Destination}){
                int i=scenes.FindIndex(s=>s.path==path);if(i<0)scenes.Add(new EditorBuildSettingsScene(path,true));else scenes[i]=new EditorBuildSettingsScene(path,true);
            }
            EditorBuildSettings.scenes=scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(intro);EditorSceneManager.SaveScene(intro);
            EditorSceneManager.CloseScene(intro,true);SceneManager.SetActiveScene(apartment);
            File.WriteAllText(P+"Editor/ApartmentTransition.result","SUCCESS destination="+Destination+" director="+updated+" buildEnabled="+EditorBuildSettings.scenes.Count(s=>s.enabled));
        }catch(Exception e){File.WriteAllText(P+"Editor/ApartmentTransition.result",e.ToString());Debug.LogException(e);}
    }
}
