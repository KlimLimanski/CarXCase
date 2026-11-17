using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarXCase.Precinct;

// Local, explicit review commands. Never runs a scene builder or changes story data.
[InitializeOnLoad]
public static class VisualPassReview
{
    const string Request="Temp/VisualPass/request.txt", Key="CarXCase.VisualReview";
    static int shot, frames; static double start; static float sum, max; static StringBuilder errors=new StringBuilder();
    static VisualPassReview(){EditorApplication.update+=Tick; Application.logMessageReceived+=Log;}
    static void Log(string c,string s,LogType t){if(EditorPrefs.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Warning))errors.AppendLine(t+": "+c);}
    public static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.Invoke(o,args);
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorPrefs.GetBool(Key,false)&&EditorApplication.isPlaying){
            if(start==0){start=EditorApplication.timeSinceStartup;shot=frames=0;sum=max=0;errors.Clear();}
            float t=(float)(EditorApplication.timeSinceStartup-start);frames++;sum+=Time.unscaledDeltaTime;max=Mathf.Max(max,Time.unscaledDeltaTime);
            float[] times={1.9f,4.8f,7.0f,9.5f,12.0f,16.0f,20.0f};
            if(shot<times.Length&&t>=times[shot]){ScreenCapture.CaptureScreenshot(Path.Combine(EditorPrefs.GetString(Key+"out"),"play_"+shot+".png"));shot++;}
            if(t>22){File.WriteAllText(Path.Combine(EditorPrefs.GetString(Key+"out"),"play_metrics.txt"),$"Play Mode normal entry; samples={frames}; mean delta ms={sum/frames*1000:F2}; max delta ms={max*1000:F2}; triangles={UnityStats.triangles}; drawCalls={UnityStats.drawCalls};\n"+errors);EditorPrefs.SetBool(Key,false);EditorApplication.isPlaying=false;start=0;}
            return;
        }
        if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        var lines=File.ReadAllLines(Request);File.Delete(Request);
        string cmd=lines[0], output=lines.Length>1?lines[1]:"Temp/VisualPass";Directory.CreateDirectory(output);
        try{
            if(cmd=="before"||cmd=="after"){CaptureAll(output);}
            else if(cmd=="bar_play"){EditorSceneManager.OpenScene("Assets/_Project/Scenes/03_Bar.unity");EditorPrefs.SetString(Key+"out",output);EditorPrefs.SetBool(Key,true);start=0;EditorApplication.isPlaying=true;}
            else if(cmd=="inspect"){Inspect(output);}
            else if(cmd=="apply_bar"){StrelaVisualPass.ApplyBar();}
            else if(cmd=="apply_secondary"){StrelaVisualPass.ApplySecondary();}
            File.WriteAllText(Path.Combine(output,"command_result.txt"),cmd+" OK "+DateTime.Now);
        }catch(Exception e){File.WriteAllText(Path.Combine(output,"command_result.txt"),cmd+" ERROR "+e);Debug.LogException(e);}
    }
    public static void Shot(Camera c,string file){
        var old=c.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{c.targetTexture=rt;c.aspect=1280f/720;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());}
        finally{c.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    static void CaptureAll(string output){
        var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/03_Bar.unity");var cut=UnityEngine.Object.FindAnyObjectByType<ScriptedCutscene>();var talk=UnityEngine.Object.FindAnyObjectByType<Interrogation>();
        float[] times={1.9f,4.8f,7f,cut.Duration-.4f};string[] names={"bar_entry","bar_walk","bar_approach","bar_counter"};
        var anim=cut.hero.GetComponentInChildren<Animator>();var walk=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Arts/Heroes/Animations/Hero_Walk.anim");var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Arts/Heroes/Animations/Hero_Idle.anim");
        for(int i=0;i<times.Length;i++){cut.Evaluate(times[i]);var clip=i==3?idle:walk;if(clip&&anim)clip.SampleAnimation(anim.gameObject,times[i]%clip.length);Shot(cut.shotCamera,Path.Combine(output,names[i]+".png"));}
        foreach(var a in new[]{talk.camOnSuspect,talk.camOnHero,talk.camWide}){talk.cam.transform.SetPositionAndRotation(a.position,a.rotation);talk.cam.fieldOfView=40;Shot(talk.cam,Path.Combine(output,"bar_"+a.name+".png"));}
        EditorSceneManager.OpenScene(scene.path);Inspect(output);
        foreach(string n in new[]{"01_FatherApartment","02_Precinct","00_BrightonDrive_Models"}){
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+n+".unity");var c=Camera.main;if(c)Shot(c,Path.Combine(output,n+".png"));
            if(n=="02_Precinct"){var b=UnityEngine.Object.FindAnyObjectByType<DeductionBoard>();if(b){Call(b,"BuildCards");c.transform.SetPositionAndRotation(b.viewPoint.position,b.viewPoint.rotation);Shot(c,Path.Combine(output,"precinct_board.png"));}}
        }
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/03_Bar.unity");
    }
    static void Inspect(string output){
        var sb=new StringBuilder();foreach(var r in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var t in r.GetComponentsInChildren<Transform>(true)){
            var rr=t.GetComponent<Renderer>();sb.AppendLine(t.name+" pos="+t.position+" scale="+t.lossyScale+(rr?" bounds="+rr.bounds+" materials="+string.Join(",",rr.sharedMaterials.Select(m=>m?m.name:"NULL")):""));
        }File.WriteAllText(Path.Combine(output,"scene_inventory.txt"),sb.ToString());
    }
}
