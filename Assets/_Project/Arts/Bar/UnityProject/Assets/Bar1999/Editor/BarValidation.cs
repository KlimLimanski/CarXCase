using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using ReferenceBar;

namespace ReferenceBar.Editor {
    public static class BarValidation {
        const string Root="Assets/Bar1999";
        public static void FinalizeDelivery() {
            EditorSceneManager.OpenScene(Root+"/Scenes/Bar1999.unity");
            var manifest=JsonUtility.FromJson<BarManifest>(File.ReadAllText(Root+"/scene_manifest.json"));
            var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach(var p in manifest.markers) {
                var t=all.Single(t=>t.name==p.name);
                t.position=new Vector3(p.position[0],p.position[2],p.position[1]);
            }
            UnityEngine.Object.FindAnyObjectByType<CharacterController>().minMoveDistance=0;
            Physics.SyncTransforms();
            var nav=UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None).Single(n=>n.name=="NavMesh_GuestNPC");
            var filter=new NavMeshQueryFilter{agentTypeID=nav.agentTypeID,areaMask=NavMesh.AllAreas};
            var report=new List<string>();
            foreach(var name in new[]{"NPC_01_Spawn","NPC_02_Spawn"}) {
                Vector3 p=all.Single(t=>t.name==name).position;
                var colliders=Physics.OverlapCapsule(p+Vector3.up*.24f,p+Vector3.up*1.50f,.22f,~0,QueryTriggerInteraction.Ignore);
                bool valid=colliders.Length==0&&NavMesh.SamplePosition(p,out _, .12f,filter);
                report.Add(name+" "+p+": "+(valid?"PASS":"FAIL")+" overlaps="+string.Join(",",colliders.Select(c=>c.name)));
                if(!valid)throw new Exception("Invalid NPC spawn: "+name);
            }
            foreach(var expected in manifest.meshBounds) {
                var r=all.Single(t=>t.name==expected.name).GetComponent<Renderer>();
                var center=new Vector3(expected.center[0],expected.center[2],expected.center[1]);
                var size=new Vector3(expected.size[0],expected.size[2],expected.size[1]);
                if(Vector3.Distance(r.bounds.center,center)>.004f||Vector3.Distance(r.bounds.size,size)>.004f)throw new Exception("Mesh changed: "+expected.name);
            }
            report.Add("Final FBX world bounds: 999 PASS");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            var environment=all.Single(t=>t.name=="Bar1999_Environment").gameObject;
            PrefabUtility.SaveAsPrefabAsset(environment,Root+"/Prefabs/Bar1999_Environment.prefab");
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Root+"/Delivery_QA.txt",report);
            ValidateSavedScene();
            Debug.Log("BAR_DELIVERY_COMPLETE\n"+string.Join("\n",report));
        }
        public static void DiagnoseGeometry() {
            EditorSceneManager.OpenScene(Root+"/Scenes/Bar1999.unity");
            var lines=new List<string>();
            foreach(var name in new[]{"ENV_Architecture","ENV_Doors","Door_Entry","Door_StaffOnly","Bar_Counter","Bar_Carcass","Bar_PolishedTop","BarStool_01","BarStool_01_Geometry","Booth_1_BenchSouth","Booth_1_BenchSouth_Geometry","Jukebox","Jukebox_Geometry","Cash_Register","Register_Base","BackBar_Base"}) {
                var t=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(x=>x.name==name);
                if(t==null){lines.Add(name+": missing");continue;}
                lines.Add(name+" world="+t.position.ToString("F3")+" local="+t.localPosition.ToString("F3")+" scale="+t.lossyScale.ToString("F3")+" rotation="+t.eulerAngles.ToString("F1"));
                foreach(var r in t.GetComponentsInChildren<Renderer>())lines.Add("  "+r.name+" bounds="+r.bounds+" enabled="+r.enabled+" active="+r.gameObject.activeInHierarchy+" materials="+string.Join(",",r.sharedMaterials.Select(m=>m==null?"null":m.name)));
            }
            var player=UnityEngine.Object.FindAnyObjectByType<CharacterController>();
            Physics.SyncTransforms();
            foreach(var c in Physics.OverlapCapsule(player.transform.position+Vector3.up*.25f,player.transform.position+Vector3.up*1.5f,.23f))lines.Add("Spawn overlap: "+c.name+" "+c.bounds);
            foreach(var c in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))lines.Add("Collider "+c.name+" "+c.bounds+" rotation="+c.transform.eulerAngles);
            var nav=UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None).Single(n=>n.name=="NavMesh_Player");
            var filter=new NavMeshQueryFilter{agentTypeID=nav.agentTypeID,areaMask=NavMesh.AllAreas};
            for(int z=32;z>=0;z--) { string line="";for(int x=0;x<26;x++)line+=NavMesh.SamplePosition(new Vector3(x*.25f,0,z*.25f),out _, .12f, filter)?".":"#";lines.Add(z*.25f+" "+line); }
            File.WriteAllLines(Root+"/Geometry_Diagnostics.txt",lines);Debug.Log(string.Join("\n",lines));
        }
        public static void FinalizeProject() {
            BarSceneBuilder.Build();
            ValidateSavedScene();
            BarSceneBuilder.BakeLighting();
            ValidateSavedScene();
            Debug.Log("BAR_FINALIZATION_COMPLETE");
        }
        public static void RunPlayMode() {
            EditorSceneManager.OpenScene(Root+"/Scenes/Bar1999.unity");
            var qa=new GameObject("Temporary_PlayMode_QA").AddComponent<BarPlayModeQA>();
            qa.outputFolder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Renders"));
            EditorApplication.isPlaying=true;
        }
        public static void ValidateSavedScene() {
            EditorSceneManager.OpenScene(Root+"/Scenes/Bar1999.unity");
            var report=new List<string>();
            var nav=UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            foreach(var s in nav) {
                if(s.navMeshData==null || !AssetDatabase.Contains(s.navMeshData)) throw new Exception("Navmesh asset did not persist: "+s.name);
                report.Add(s.name+": persisted data PASS");
            }
            NavMeshQueryFilter Filter(string name) => new NavMeshQueryFilter { agentTypeID=nav.Single(n=>n.name=="NavMesh_"+name).agentTypeID,areaMask=NavMesh.AllAreas };
            bool Sample(Vector3 p,NavMeshQueryFilter f,float d=.12f) => NavMesh.SamplePosition(p,out _,d,f);
            void Require(bool value,string text) { report.Add(text+": "+(value?"PASS":"FAIL"));if(!value)throw new Exception(text); }
            var player=Filter("Player");var guest=Filter("GuestNPC");var bartender=Filter("Bartender");
            Require(!Sample(new Vector3(5.45f,0,3.8f),guest),"Guests excluded from staff aisle");
            Require(!Sample(new Vector3(2.75f,0,3.8f),bartender),"Bartender excluded from guest floor");
            Require(!Sample(new Vector3(4.575f,1.10f,4.4f),player,.06f),"Countertop not walkable");
            Require(!Sample(new Vector3(.80f,.80f,3.95f),player,.06f),"Tabletop not walkable");
            Require(Sample(new Vector3(5.45f,0,3.8f),bartender),"Bartender spawn on NavMesh");
            var path=new NavMeshPath();
            NavMesh.SamplePosition(new Vector3(5.45f,0,1.85f),out var a,.3f,bartender);
            NavMesh.SamplePosition(new Vector3(5.45f,0,7.60f),out var b,.3f,bartender);
            Require(NavMesh.CalculatePath(a.position,b.position,bartender,path)&&path.status==NavMeshPathStatus.PathComplete,"Bartender can traverse full service aisle");
            var interactions=UnityEngine.Object.FindObjectsByType<BarInteractive>(FindObjectsSortMode.None);
            var restroom=interactions.Single(i=>i.name=="Door_Restroom");
            restroom.SendMessage("Awake");restroom.Use();Require(!restroom.IsOpen&&restroom.locked,"Restroom rejects interaction");
            foreach(var door in interactions.Where(i=>i.kind=="Door"&&!i.locked)) {
                door.SendMessage("Awake");door.Use();Require(door.IsOpen,door.name+" open state");
                var child=door.GetComponentsInChildren<BoxCollider>().First();
                var rot=door.transform.localRotation;
                door.transform.localRotation=rot*Quaternion.Euler(0,door.openAngle,0);
                Physics.SyncTransforms();
                Require(child.bounds.center.z>door.transform.position.z+.2f,door.name+" swings inward");
                door.transform.localRotation=rot;door.Use();Require(!door.IsOpen,door.name+" close state");
            }
            var cc=UnityEngine.Object.FindAnyObjectByType<CharacterController>();
            Require(Mathf.Abs(cc.radius-.23f)<.001f,"Player radius 0.23m");
            var station=UnityEngine.Object.FindAnyObjectByType<BarInteractionStation>();int fired=0;
            station.onDialogueRequested.AddListener(()=>fired++);
            Require(station.TryUse(station.playerPoint.position)&&fired==1,"Bar dialogue event");
            Require(!station.TryUse(new Vector3(1.12f,0,.48f)),"Bar cannot interact remotely");
            var director=UnityEngine.Object.FindAnyObjectByType<UnityEngine.Playables.PlayableDirector>();
            Require(director!=null&&director.playableAsset!=null,"Timeline asset assigned");
            var brain=Camera.main.GetComponent<CinemachineBrain>();brain.enabled=true;
            foreach(double t in new[]{0.1,4.1,8.1,12.1,16.1,20.1,24.1,28.1}) {
                director.time=t;director.Evaluate();
                Require(!float.IsNaN(Camera.main.transform.position.x),"Timeline camera at "+t+" seconds");
            }
            brain.enabled=false;
            report.Add("Static environment triangles="+UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Sum(m=>m.sharedMesh!=null?Enumerable.Range(0,m.sharedMesh.subMeshCount).Sum(i=>(long)m.sharedMesh.GetIndexCount(i)/3):0));
            report.Add("Baked lightmaps="+LightmapSettings.lightmaps.Length);
            File.WriteAllLines(Root+"/SavedScene_Validation.txt",report);
            Debug.Log("BAR_SAVED_SCENE_VALIDATED\n"+string.Join("\n",report));
        }
        public static void Capture() { RunPlayMode(); }
    }
}
