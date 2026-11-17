using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BrightonModelAssembly
{
    const string Base = "Assets/_Project/";
    static BrightonModelAssembly() { EditorApplication.delayCall += Inspect; EditorApplication.delayCall += RepairRequested; EditorApplication.update += Poll; }
    static void RepairRequested() {
        string request=Base+"Editor/BrightonRepair.request";
        if(!File.Exists(request) || EditorApplication.isPlaying) return;
        File.Delete(request);
        try {
            var scene=SceneManager.GetActiveScene();var root=GameObject.Find("Brighton_Models_Assembled");
            if(!root) throw new Exception("Open the Models scene first.");
            EditorSceneManager.SaveScene(scene,Base+"Scenes/BeforeCameraWalls_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Repair camera and left walls");
            var car=GameObject.Find("Hero_Car").transform;var rig=car.Find("Camera_Composition_Offset");
            if(rig) {Undo.RecordObject(rig,"Restore original camera alignment");rig.localPosition=Vector3.zero;}
            int count=0;
            foreach(Transform building in root.transform) {
                if(building.Find("Wall_Left_Closure"))continue;
                var rr=building.GetComponentsInChildren<MeshRenderer>();
                if(rr.Length==0)continue;
                Bounds b=default;bool first=true;
                foreach(var r in rr) {
                    var mesh=r.GetComponent<MeshFilter>().sharedMesh.bounds;
                    var matrix=building.worldToLocalMatrix*r.transform.localToWorldMatrix;
                    for(int i=0;i<8;i++) {
                        var p=matrix.MultiplyPoint3x4(mesh.center+Vector3.Scale(mesh.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                        if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);
                    }
                }
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(wall,"Close missing left wall");
                wall.name="Wall_Left_Closure";wall.transform.SetParent(building,false);
                // The GLB facade faces +Z; its plain left party wall is at +X.
                wall.transform.localPosition=new Vector3(b.max.x-.04f,(b.min.y+b.max.y)*.5f,b.min.z*.5f);
                wall.transform.localScale=new Vector3(.08f,b.size.y,-b.min.z);
                var mat=rr.SelectMany(r=>r.sharedMaterials).FirstOrDefault(m=>m && m.name.Contains("Simple_Party_Walls"));
                if(!mat) mat=rr[0].sharedMaterial;
                wall.GetComponent<Renderer>().sharedMaterial=mat;
                count++;
            }
            Undo.CollapseUndoOperations(group);EditorSceneManager.SaveScene(scene);Capture();
            File.WriteAllText(Base+"Editor/BrightonRepair.result","SUCCESS leftWalls="+count+" cameraOffset=0 scene="+scene.path);
        }catch(Exception e){File.WriteAllText(Base+"Editor/BrightonRepair.result",e.ToString());Debug.LogException(e);}
    }
    static void Poll() {
        string request=Base+"Editor/BrightonAssembly.request";
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request)) return;
        string command=File.ReadAllText(request).Trim(); File.Delete(request);
        try { if(command=="assemble") Assemble(); else if(command=="capture") Capture(); else if(command=="finish") Finish(); else if(command=="seats") FitSeats(); }
        catch(Exception e) {File.WriteAllText(Base+"Editor/BrightonAssembly.result",e.ToString()); Debug.LogException(e);}
    }
    static Bounds BoundsOf(GameObject obj) {
        var rr=obj.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if(rr.Length==0) throw new Exception("No geometry: "+obj.name);
        var b=rr[0].bounds; foreach(var r in rr.Skip(1)) b.Encapsulate(r.bounds); return b;
    }
    static void Finish() {
        var car=GameObject.Find("Hero_Car");var visual=car.transform.Find("Sedan_2000_Visual");
        if(!visual)throw new Exception("No sedan visual");
        Directory.CreateDirectory(Base+"Materials/BrightonModels");
        string path=Base+"Materials/BrightonModels/AutomotiveGlass.mat";
        var glass=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!glass){glass=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(glass,path);}
        glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",5);glass.SetFloat("_DstBlend",10);glass.SetFloat("_ZWrite",0);
        glass.SetColor("_BaseColor",new Color(.72f,.82f,.85f,.045f));glass.SetFloat("_Smoothness",.8f);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;
        glass.SetOverrideTag("RenderType","Transparent");EditorUtility.SetDirty(glass);
        foreach(var r in visual.GetComponentsInChildren<Renderer>()) {
            var materials=r.sharedMaterials;
            for(int i=0;i<materials.Length;i++)if(materials[i] && materials[i].name.Contains("Clear_Automotive_Glass"))materials[i]=glass;
            Undo.RecordObject(r,"Correct glass");r.sharedMaterials=materials;
            if(materials.Contains(glass)) r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach(var t in visual.GetComponentsInChildren<Transform>().Where(t=>t.name=="Driver_Seat_ASSEMBLY" || t.name=="Passenger_Seat_ASSEMBLY")) {
            var cushion=t.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.EndsWith("Seat_Cushion"));
            if(cushion){var pos=cushion.bounds.center;Undo.RecordObject(t,"Fit seat");t.localScale=new Vector3(t.localScale.x,t.localScale.y*.78f,t.localScale.z);t.position+=pos-cushion.bounds.center;}
        }
        var cam=Camera.main;
        if(!car.transform.Find("Camera_Composition_Offset")) {
            var rig=new GameObject("Camera_Composition_Offset");Undo.RegisterCreatedObjectUndo(rig,"Frame camera");rig.transform.SetParent(car.transform,false);
            var p=cam.transform.localPosition;var q=cam.transform.localRotation;
            Undo.SetTransformParent(cam.transform,rig.transform,"Frame camera");cam.transform.localPosition=p;cam.transform.localRotation=q;
            rig.transform.localPosition=new Vector3(-.18f,.08f,0);
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Capture();
        File.WriteAllText(Base+"Editor/BrightonAssembly.result","FINISHED glass, seats, camera; saved "+SceneManager.GetActiveScene().path);
    }
    static void Hide(Renderer r) {Undo.RecordObject(r,"Hide original greybox"); r.enabled=false;}
    static void FitSeats() {
        var car=GameObject.Find("Hero_Car").transform;
        var visual=car.Find("Sedan_2000_Visual");
        foreach(var seat in visual.GetComponentsInChildren<Transform>().Where(t=>t.name=="Driver_Seat_ASSEMBLY" || t.name=="Passenger_Seat_ASSEMBLY").ToArray()) {
            var cushion=seat.GetComponentsInChildren<Renderer>().First(r=>r.name.EndsWith("Seat_Cushion"));var pos=cushion.bounds.center;
            Undo.RecordObject(seat,"Fit seat height"); seat.localScale=new Vector3(seat.localScale.x,seat.localScale.y/.78f,seat.localScale.z);seat.position+=pos-cushion.bounds.center;
            var mount=new GameObject(seat.name+"_HeightFit");Undo.RegisterCreatedObjectUndo(mount,"Fit seat height");mount.transform.SetParent(car,false);
            Undo.SetTransformParent(seat,mount.transform,"Fit seat height");
            mount.transform.localScale=new Vector3(1,.72f,1);mount.transform.localPosition=new Vector3(0,.64f*(1-.72f),0);
        }
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Capture();
        File.WriteAllText(Base+"Editor/BrightonAssembly.result","SEATS FITTED; scene saved");
    }
    static void Assemble() {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetActiveScene();
        if(GameObject.Find("Brighton_Models_Assembled")) throw new Exception("Already assembled.");
        var car=GameObject.Find("Hero_Car"); if(!car) throw new Exception("Hero_Car missing.");
        string[] names={"01_Hudson","02_Mercer","03_Fulton","04_Rosette","05_Union"};
        var assets=names.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Arts/Buildings/Street_Buildings_1Scene/"+n+".glb")).ToArray();
        if(assets.Any(x=>!x)) throw new Exception("Building import missing.");
        var sedan=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Arts/Car/Sedan_2000/Sedan_2000_Rear_Passenger.blend");
        if(!sedan || !sedan.transform.Find("SEDAN_2000_MOVE_CAR")) throw new Exception("Sedan assembly missing.");
        Directory.CreateDirectory(Base+"Scenes");
        string backup=Base+"Scenes/00_BrightonDrive_BeforeModels_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
        if(!EditorSceneManager.SaveScene(scene,backup,true)) throw new Exception("Backup failed.");
        Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Assemble Brighton models");
        try {
            var marker=new GameObject("Brighton_Models_Assembled"); Undo.RegisterCreatedObjectUndo(marker,"Create models");
            var oldBuildings=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("Building_")).ToArray();
            int count=0;
            foreach(var old in oldBuildings) {
                var facade=old.Find("Facade"); if(!facade) continue;
                var b=facade.GetComponent<Renderer>().bounds;
                var item=(GameObject)PrefabUtility.InstantiatePrefab(assets[count%5],scene); Undo.RegisterCreatedObjectUndo(item,"Add building");
                item.name=old.name+"_"+names[count%5]; item.transform.SetParent(marker.transform,false);
                float side=Mathf.Sign(b.center.x);
                item.transform.rotation=Quaternion.Euler(0,side<0?90:-90,0);
                var nb=BoundsOf(item);
                float scale=Mathf.Clamp(b.size.z/nb.size.z,0.6f,1.5f);
                item.transform.localScale*=scale; nb=BoundsOf(item);
                float roadEdge=side<0?b.max.x:b.min.x;
                Vector3 dest=new Vector3(roadEdge+side*nb.extents.x,nb.extents.y,b.center.z);
                item.transform.position+=dest-nb.center;
                foreach(var r in old.GetComponentsInChildren<Renderer>(true)) Hide(r);
                count++;
            }
            var imported=(GameObject)PrefabUtility.InstantiatePrefab(sedan,scene);
            Undo.RegisterCreatedObjectUndo(imported,"Import sedan");
            PrefabUtility.UnpackPrefabInstance(imported,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var content=imported.transform.Find("SEDAN_2000_MOVE_CAR");
            content.SetParent(null,true);
            Undo.DestroyObjectImmediate(imported);
            var wrapper=new GameObject("Sedan_2000_Visual"); Undo.RegisterCreatedObjectUndo(wrapper,"Create sedan visual");
            wrapper.transform.SetParent(car.transform,false); content.SetParent(wrapper.transform,true);
            wrapper.transform.localRotation=Quaternion.Euler(0,90,0);
            wrapper.transform.localScale=new Vector3(1,1.45f,1.1f);
            var seat=content.GetComponentsInChildren<Renderer>().First(r=>r.name=="Driver_Seat_Cushion");
            var target=car.transform.Find("FrontSeat_Base_-1");
            wrapper.transform.position+=target.position-seat.bounds.center;
            foreach(var r in car.GetComponentsInChildren<Renderer>(true)) {
                if(r.transform.IsChildOf(wrapper.transform)) continue;
                Transform top=r.transform; while(top.parent && top.parent!=car.transform)top=top.parent;
                if(top.name=="Driver" || top.name.StartsWith("Wheel_")) continue;
                Hide(r);
            }
            foreach(var r in content.GetComponentsInChildren<Renderer>(true))
                if(r.name.StartsWith("Simple_Tire") || r.name.StartsWith("Simple_Wheel")) r.enabled=false;
            foreach(var c in content.GetComponentsInChildren<Component>(true))
                if(c is Camera || c is AudioListener || c is Light) Undo.DestroyObjectImmediate(c);
            AssetDatabase.SaveAssets();
            string output=Base+"Scenes/00_BrightonDrive_Models.unity";
            if(!EditorSceneManager.SaveScene(scene,output)) throw new Exception("Save failed.");
            Undo.CollapseUndoOperations(group);
            File.WriteAllText(Base+"Editor/BrightonAssembly.result","SUCCESS buildings="+count+" sedanMeshes="+content.GetComponentsInChildren<Renderer>().Length+" scene="+output+" backup="+backup);
            Capture();
        } catch { Undo.RevertAllDownToGroup(group); throw; }
    }
    static void Capture() {
        var camera=Camera.main; if(!camera)throw new Exception("Main Camera missing");
        var old=camera.targetTexture; var active=RenderTexture.active;
        var rt=new RenderTexture(1280,720,24); var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try {camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Base+"Editor/BrightonCamera.png",tex.EncodeToPNG());}
        finally {camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
    [MenuItem("Tools/Brighton/Inspect Models")]
    public static void Inspect()
    {
        try {
            var s = new StringBuilder();
            s.AppendLine("SCENE " + SceneManager.GetActiveScene().path);
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
                if (t.name.StartsWith("Building_") || t.name == "Hero_Car" || t.GetComponent<Camera>() || (t.parent && t.parent.name == "Hero_Car"))
                    s.AppendLine("NODE " + t.name + " pos=" + t.position + " local=" + t.localPosition + " rot=" + t.eulerAngles + " scale=" + t.localScale);
            }
            foreach (string p in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(Base+"Arts/") && (p.EndsWith(".blend") || p.EndsWith(".fbx") || p.EndsWith(".glb")))) {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                s.AppendLine("ASSET " + p + " imported=" + (model != null));
                if (!model) continue;
                if(p.Contains("Sedan")) foreach(var t in model.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==model.transform || t.name.Contains("Driver_Seat_Cushion"))) s.AppendLine("GROUP "+t.name+" parent="+(t.parent?t.parent.name:"none")+" pos="+t.position);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    s.AppendLine(" MESH " + r.name + " bounds=" + r.bounds + " mat=" + string.Join(",",r.sharedMaterials.Select(m => m ? m.name+":"+m.shader.name : "NULL")));
            }
            File.WriteAllText(Base+"Editor/BrightonInspection.txt",s.ToString());
        } catch(Exception e) { Debug.LogException(e); }
    }
}
