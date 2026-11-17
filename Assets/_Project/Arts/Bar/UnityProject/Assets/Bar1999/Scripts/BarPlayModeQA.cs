#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using UnityEditor;

namespace ReferenceBar {
    public class BarPlayModeQA : MonoBehaviour {
        [Serializable] class Doc { public Shot[] cameras; }
        [Serializable] class Shot { public string name;public float[] position,target;public float lens; }
        public string outputFolder;
        List<string> report=new();int errorCount,editorDiagnostics;
        void Log(string condition,string trace,LogType type) {
            if(type!=LogType.Exception&&type!=LogType.Error)return;
            if(trace.Contains("UnityEditor.Search.SearchDatabase")) {
                editorDiagnostics++;report.Add("Unity Editor search diagnostic (outside game code): "+condition);return;
            }
            errorCount++;report.Add(type+": "+condition);
        }
        void Check(bool condition,string message) { report.Add(message+": "+(condition?"PASS":"FAIL"));if(!condition)errorCount++; }
        IEnumerator Start() {
            Application.logMessageReceived+=Log;
            Application.runInBackground=true;
            Time.timeScale=1;
            for(int i=0;i<20;i++)yield return null;
            var walk=FindAnyObjectByType<BarWalkthrough>();var cam=Camera.main;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var kb=InputSystem.AddDevice<Keyboard>("QA_Keyboard");
            if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
            var initial=walk.transform.position;
            Time.captureDeltaTime=1f/60f;
            InputSystem.QueueStateEvent(kb,new KeyboardState(Key.W));
            InputSystem.Update();
            report.Add("Synthetic W pressed="+kb.wKey.isPressed+" current="+Keyboard.current.name+" controller="+walk.enabled);
            for(int step=0;step<120;step++) {
                kb.MakeCurrent();InputSystem.QueueStateEvent(kb,new KeyboardState(Key.W));InputSystem.Update();
                yield return null;
                if(step%20==0)report.Add("Step "+step+" W="+kb.wKey.isPressed+" current="+Keyboard.current.name+" frames="+walk.inputFrames+" hit="+walk.lastCollision+" dt="+Time.deltaTime+" pos="+walk.transform.position.ToString("F3"));
            }
            InputSystem.QueueStateEvent(kb,new KeyboardState());
            Time.captureDeltaTime=0;
            Check(Vector3.Distance(initial,walk.transform.position)>.12f,"WASD movement in Play Mode");
            report.Add("Movement from "+initial.ToString("F3")+" to "+walk.transform.position.ToString("F3"));
            Check(walk.transform.position.y>-.1f,"Player remains above floor");
            var doors=FindObjectsByType<BarInteractive>(FindObjectsSortMode.None).Where(i=>i.kind=="Door").ToArray();
            foreach(var d in doors) d.Use();
            yield return new WaitForSeconds(1.1f);
            foreach(var d in doors) {
                Check(d.locked?!d.IsOpen:d.IsOpen,d.name+" interaction");
                if(!d.locked) {
                    Check(d.GetComponentInChildren<BoxCollider>().bounds.center.z>d.transform.position.z+.2f,d.name+" animated inward swing");d.Use();
                }
            }
            yield return new WaitForSeconds(1.1f);
            walk.enabled=false;cam.GetComponent<CinemachineBrain>().enabled=false;
            var doc=JsonUtility.FromJson<Doc>(File.ReadAllText(Application.dataPath+"/Bar1999/scene_manifest.json"));
            for(int i=0;i<4;i++) {
                var s=doc.cameras[i];Vector3 V(float[] v)=>new Vector3(v[0],v[2],v[1]);
                int w=1152,h=i==0?514:720;
                cam.transform.position=V(s.position);cam.transform.LookAt(V(s.target));cam.aspect=(float)w/h;
                cam.fieldOfView=2*Mathf.Atan(36f/(2*s.lens*cam.aspect))*Mathf.Rad2Deg;
                var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();cam.targetTexture=rt;
                report.Add("Capture color space="+QualitySettings.activeColorSpace+" target sRGB="+rt.sRGB);
                for(int j=0;j<8;j++)yield return null;
                RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                yield return null;
                RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
                var pix=tex.GetPixels32();double light=pix.Average(p=>(p.r+p.g+p.b)/3.0);
                Check(light>5,"Rendered image "+s.name+" mean="+light.ToString("F1"));
                File.WriteAllBytes(Path.Combine(outputFolder,"Unity_"+s.name+".png"),tex.EncodeToPNG());
                cam.targetTexture=null;RenderTexture.active=null;Destroy(tex);rt.Release();Destroy(rt);
                yield return null;
            }
            Check(LightmapSettings.lightmaps.Length>0,"Baked lighting loaded in Play Mode");
            report.Add("Total runtime errors="+errorCount);
            report.Add("Unity Editor search diagnostics="+editorDiagnostics);
            File.WriteAllLines(Application.dataPath+"/Bar1999/PlayMode_QA.txt",report);
            Debug.Log("BAR_PLAYMODE_QA_COMPLETE\n"+string.Join("\n",report));
            Application.logMessageReceived-=Log;
            EditorApplication.Exit(errorCount==0?0:1);
        }
    }
}
#endif
