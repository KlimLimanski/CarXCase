using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class ElevatedSteelSetup {
 const string P="Assets/_Project/Materials/BrightonModels/";
 static ElevatedSteelSetup(){EditorApplication.delayCall+=Apply;}
 static void Apply(){
 if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(P+"ElevatedSteel.request"))return;
 File.Delete(P+"ElevatedSteel.request");
 try{
 AssetDatabase.ImportAsset(P+"ElevatedSteel_BaseColor.png",ImportAssetOptions.ForceSynchronousImport);
 var ti=(TextureImporter)AssetImporter.GetAtPath(P+"ElevatedSteel_BaseColor.png");ti.wrapMode=TextureWrapMode.Repeat;ti.sRGBTexture=true;ti.mipmapEnabled=true;ti.anisoLevel=8;ti.SaveAndReimport();
 var shader=Shader.Find("Project/Elevated Steel Rust");if(!shader)throw new Exception("Missing steel shader");
 var mat=AssetDatabase.LoadAssetAtPath<Material>(P+"ElevatedSteel.mat");if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,P+"ElevatedSteel.mat");}
 mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(P+"ElevatedSteel_BaseColor.png"));mat.SetFloat("_RustAmount",0.42f);EditorUtility.SetDirty(mat);
 var previous=SceneManager.GetActiveScene();if(previous.isDirty)EditorSceneManager.SaveScene(previous);
 var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/00_BrightonDrive_Models.unity");
 EditorSceneManager.SaveScene(scene,"Assets/_Project/Scenes/BeforeElevatedSteel_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
 int count=0;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply elevated railway steel");
 foreach(var root in scene.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
 if(!(r.name.StartsWith("Rail_Column_")||r.name.StartsWith("Rail_Crossbeam_")||r.name.StartsWith("Rail_Girder_")))continue;
 Undo.RecordObject(r,"Assign elevated steel");var mm=r.sharedMaterials;for(int i=0;i<mm.Length;i++)mm[i]=mat;r.sharedMaterials=mm;count++;
 }
 Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 File.WriteAllText(P+"ElevatedSteel.result","SAVED renderers="+count+"; no Play Mode or tests run");
 }catch(Exception e){File.WriteAllText(P+"ElevatedSteel.result",e.ToString());Debug.LogException(e);}
 }
}
