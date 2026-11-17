using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;

using CarXCase.Intro;
[InitializeOnLoad]
public static class BrightonRouteCapture {
 const string Key="Brighton.RouteCapture";
 static int shot,frames,lastFrame; static double last; static double sum,max; static string errors="";
 static BrightonRouteCapture(){EditorApplication.update+=Tick;Application.logMessageReceived+=(c,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception))errors+=c+"\n";};}
 public static void Start(string output){Directory.CreateDirectory(output);SessionState.SetString(Key+"out",output);SessionState.SetBool(Key,true);shot=frames=0;sum=max=last=0;errors="";EditorApplication.isPlaying=true;}
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
 var dir=SessionState.GetString(Key+"out","");var cut=Object.FindAnyObjectByType<BrightonDriveCutscene>();
 if(cut){float t=Time.timeSinceLevelLoad;
 float[] marks={5,10,20,40,65,90,115,135,146};
 if(Time.frameCount!=lastFrame){lastFrame=Time.frameCount;frames++;sum+=Time.unscaledDeltaTime;max=System.Math.Max(max,Time.unscaledDeltaTime);}
 if(shot<marks.Length&&t>=marks[shot]){ScreenCapture.CaptureScreenshot(Path.Combine(dir,"drive_"+marks[shot].ToString("000")+".png"));shot++;} last=EditorApplication.timeSinceStartup;
 }else if(SceneManager.GetActiveScene().name=="01_FatherApartment"&&EditorApplication.timeSinceStartup-last>4){ScreenCapture.CaptureScreenshot(Path.Combine(dir,"apartment_entry.png"));File.WriteAllText(Path.Combine(dir,"review.txt"),"Normal Play Mode entry; completed 150 second drive; transitioned to "+SceneManager.GetActiveScene().path+"\nCaptured checkpoints="+shot+"\nMean sampled frame ms="+(sum/System.Math.Max(frames,1)*1000)+" max="+(max*1000)+"\nErrors:\n"+errors);SessionState.SetBool(Key,false);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;}
 }
}



