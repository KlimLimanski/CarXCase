using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class ApartmentTransitionCheck {
 const string P="Assets/_Project/Editor/";
 static ApartmentTransitionCheck(){EditorApplication.update+=Tick;}
 static void Tick(){
  if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  string file=P+"ApartmentCheck.request";if(!File.Exists(file))return;
  string cmd=File.ReadAllText(file).Trim();File.Delete(file);
  if(cmd=="play"){
   if(EditorApplication.isPlaying)return;
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/00_BrightonDrive_Models.unity");EditorApplication.isPlaying=true;
  }else if(cmd=="sample"){
   var scene=SceneManager.GetActiveScene();var player=GameObject.Find("Player");var cam=Camera.main;
   File.WriteAllText(P+"ApartmentPlayCheck.txt","scene="+scene.path+" play="+Application.isPlaying+" player="+(player?player.transform.position.ToString():"missing")+" camera="+(cam?cam.name:"missing")+" canLoad="+Application.CanStreamedLevelBeLoaded("01_FatherApartment"));
   if(cam){var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);var prev=cam.targetTexture;var active=RenderTexture.active;
    try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(P+"ApartmentPreview.png",tex.EncodeToPNG());}
    finally{cam.targetTexture=prev;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
   }
  }
 }
}
