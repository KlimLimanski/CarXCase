using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

[Serializable] public class ApartmentMaterialData { public string name,baseMap,normal; public float[] color; public float roughness,metallic; }
[Serializable] public class ApartmentInteractionData { public string name,kind,axis; public float angle,distance; }
[Serializable] public class ApartmentLightData { public string name; public float[] color; public float energy; }
[Serializable] public class ApartmentData { public ApartmentMaterialData[] materials; public ApartmentInteractionData[] interactions; public ApartmentLightData[] lights; }

public sealed class ApartmentImportSettings : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if(!assetPath.EndsWith("BrooklynApartment.fbx") && !assetPath.EndsWith("BrooklynApartment_Colliders.fbx"))return;
        var m=(ModelImporter)assetImporter;m.globalScale=1;m.useFileScale=true;
        m.generateSecondaryUV=false;m.importCameras=false;m.importLights=false;m.isReadable=true;
        m.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
    }
    void OnPreprocessTexture()
    {
        if(!assetPath.Contains("Apartaments/Textures/"))return;
        var t=(TextureImporter)assetImporter;t.maxTextureSize=1024;t.mipmapEnabled=true;
        t.anisoLevel=4;t.wrapMode=TextureWrapMode.Repeat;
        if(assetPath.EndsWith("_Normal.png")){t.textureType=TextureImporterType.NormalMap;t.sRGBTexture=false;}
    }
}

public static class ApartmentSetup
{
    const string Root="Assets/_Project/Arts/Apartaments";
    [MenuItem("Tools/Brooklyn Apartment/Create exploration scene")]
    public static void Build()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/FBX/BrooklynApartment.fbx");
        if(!model)throw new Exception("Copy the package to Assets/BrooklynApartment first.");
        var data=JsonUtility.FromJson<ApartmentData>(File.ReadAllText(Root+"/UnityData.json"));
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Scenes");
        var materials=new Dictionary<string,Material>();
        Shader shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        foreach(var d in data.materials)
        {
            string path=Root+"/Materials/"+d.name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            mat.name=d.name;
            Color color=d.color!=null && d.color.Length>=3 ? new Color(d.color[0],d.color[1],d.color[2]):Color.white;
            if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);if(mat.HasProperty("_Color"))mat.SetColor("_Color",color);
            if(!string.IsNullOrEmpty(d.baseMap))
            {
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+d.baseMap);
                if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",tex);if(mat.HasProperty("_MainTex"))mat.SetTexture("_MainTex",tex);
            }
            if(!string.IsNullOrEmpty(d.normal))
            {
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+d.normal));mat.SetFloat("_BumpScale",.45f);mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Metallic",d.metallic);
            if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",1-d.roughness);
            if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",1-d.roughness);
            if(d.name=="LampGlass"){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.5f,.32f,.14f));}
            EditorUtility.SetDirty(mat);materials[d.name]=mat;
        }
        // Create a new scene; existing user scenes are not overwritten.
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var apartment=UnityEngine.Object.Instantiate(model);apartment.name="BrooklynApartment";
        apartment.transform.localScale *= 1.15f;
        var transforms=apartment.GetComponentsInChildren<Transform>(true);
        var byName=transforms.GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        int collisions=0;
        foreach(var t in transforms)
        {
            var renderer=t.GetComponent<MeshRenderer>();var filter=t.GetComponent<MeshFilter>();
            if(t.name.StartsWith("COL_") && filter && filter.sharedMesh)
            {
                var c=t.gameObject.AddComponent<BoxCollider>();c.center=filter.sharedMesh.bounds.center;c.size=filter.sharedMesh.bounds.size;
                if(renderer)UnityEngine.Object.DestroyImmediate(renderer);
                UnityEngine.Object.DestroyImmediate(filter);collisions++;continue;
            }
            if(renderer)
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m && materials.TryGetValue(m.name.Replace(" (Instance)",""),out var replacement)?replacement:m).ToArray();
                bool movable=false;for(var p=t;p!=null;p=p.parent)if(p.name.StartsWith("INT_"))movable=true;
                if(!movable)GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ContributeGI|StaticEditorFlags.OccludeeStatic);
            }
        }
        foreach(var d in data.interactions)
        {
            if(!byName.TryGetValue(d.name,out var t))continue;
            var interact=t.gameObject.AddComponent<ApartmentInteractable>();interact.kind=d.kind;interact.openAngle=-d.angle;interact.travel=d.distance;
            if(d.name=="INT_EntryDoor")interact.kind="locked";
            var marker=t.Cast<Transform>().FirstOrDefault(c=>c.name.StartsWith(d.kind=="drawer"?"DrawerAxisMarker_":"HingeAxisMarker_"));
            if(marker)interact.localAxis=marker.localPosition.normalized;
            if(d.kind=="pickup")
            {
                var rr=t.GetComponentsInChildren<Renderer>();if(rr.Length==0)continue;
                var bb=new Bounds(t.InverseTransformPoint(rr[0].bounds.center),Vector3.zero);
                foreach(var r in rr){var b=r.bounds;for(int i=0;i<8;i++)bb.Encapsulate(t.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));}
                var c=t.gameObject.AddComponent<BoxCollider>();c.center=bb.center;c.size=bb.size;c.isTrigger=true;
            }
        }
        var lights=new GameObject("GameLighting");
        foreach(var d in data.lights)
        {
            if(!byName.TryGetValue("Anchor_"+d.name,out var anchor))continue;
            var go=new GameObject(d.name);go.transform.SetParent(lights.transform);go.transform.position=anchor.position;
            var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(d.color[0],d.color[1],d.color[2]);
            l.intensity=d.energy/75f;l.range=d.name.Contains("Bath")||d.name.Contains("Vanity")?2.8f:5f;l.shadows=LightShadows.Soft;l.shadowBias=.025f;l.lightmapBakeType=LightmapBakeType.Mixed;
        }
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.11f,.105f,.09f);
        var player=new GameObject("Player");player.transform.position=byName["PlayerSpawn"].position;
        var direction=byName["PlayerLookTarget"].position-player.transform.position;direction.y=0;player.transform.rotation=Quaternion.LookRotation(direction);
        var cc=player.AddComponent<CharacterController>();cc.height=1.75f;cc.radius=.23f;cc.center=new Vector3(0,.875f,0);cc.skinWidth=.02f;cc.stepOffset=.15f;
        var eye=new GameObject("PlayerCamera");eye.transform.SetParent(player.transform,false);eye.transform.localPosition=new Vector3(0,1.62f,0);
        var camera=eye.AddComponent<Camera>();camera.nearClipPlane=.045f;camera.farClipPlane=80;camera.fieldOfView=70;camera.tag="MainCamera";eye.AddComponent<AudioListener>();
        player.AddComponent<ApartmentExplorer>().eye=camera;
        // A nearby probe helps period metal and CRT glass in the built-in pipeline.
        var probe=new GameObject("InteriorReflectionProbe").AddComponent<ReflectionProbe>();probe.transform.position=byName["PlayerLookTarget"].position;probe.size=new Vector3(8,4,10);probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.resolution=128;
        PrefabUtility.SaveAsPrefabAsset(apartment,Root+"/BrooklynApartment.prefab");
        Directory.CreateDirectory("Assets/_Project/Scenes");
        string scenePath="Assets/_Project/Scenes/01_FatherApartment.unity";
        EditorSceneManager.SaveScene(scene,scenePath);AssetDatabase.SaveAssets();
        var floor=transforms.FirstOrDefault(t=>t.name=="Floor_Wood");
        string report="Unity "+Application.unityVersion+"\nCollision proxies: "+collisions+"\nInteractions: "+data.interactions.Length+"\nRenderers: "+apartment.GetComponentsInChildren<Renderer>().Length+"\nFloor bounds: "+(floor?floor.GetComponent<Renderer>().bounds.size.ToString():"missing")+"\n";
        report+=Validate(apartment,byName,player,materials);
        File.WriteAllText(Root+"/UnityValidation.txt",report);Debug.Log(report);
    }
    static string Validate(GameObject apartment,Dictionary<string,Transform> names,GameObject player,Dictionary<string,Material> materials)
    {
        int missingUV=0,unmapped=0;
        foreach(var f in apartment.GetComponentsInChildren<MeshFilter>())
            if(f.sharedMesh.uv2.Length!=f.sharedMesh.vertexCount)missingUV++;
        foreach(var r in apartment.GetComponentsInChildren<Renderer>())
            foreach(var m in r.sharedMaterials)if(!m || !materials.ContainsValue(m))unmapped++;
        // A capsule occupancy flood fill in actual imported Unity coordinates.
        var door=names["INT_BathroomDoor"];var closed=door.localRotation;
        var interaction=door.GetComponent<ApartmentInteractable>();
        door.localRotation=closed*Quaternion.AngleAxis(interaction.openAngle,interaction.localAxis);
        var controller=player.GetComponent<CharacterController>();controller.enabled=false;Physics.SyncTransforms();
        var bounds=names["Floor_Wood"].GetComponent<Renderer>().bounds;
        float step=.07f;int nx=Mathf.CeilToInt(bounds.size.x/step),nz=Mathf.CeilToInt(bounds.size.z/step);
        var occupied=new bool[nx,nz];var reached=new bool[nx,nz];
        Vector3 origin=new Vector3(bounds.min.x,player.transform.position.y,bounds.min.z);
        for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
        {
            var p=origin+new Vector3(x*step,0,z*step);
            occupied[x,z]=Physics.CheckCapsule(p+Vector3.up*.23f,p+Vector3.up*1.52f,.23f,~0,QueryTriggerInteraction.Ignore);
        }
        Func<Vector3,Vector2Int> index=p=>new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.x-origin.x)/step),0,nx-1),Mathf.Clamp(Mathf.RoundToInt((p.z-origin.z)/step),0,nz-1));
        var start=index(player.transform.position);var queue=new Queue<Vector2Int>();
        if(!occupied[start.x,start.y]){queue.Enqueue(start);reached[start.x,start.y]=true;}
        int[] dx={1,-1,0,0},dz={0,0,1,-1};
        while(queue.Count>0)
        {var a=queue.Dequeue();for(int i=0;i<4;i++){int x=a.x+dx[i],z=a.y+dz[i];if(x>=0&&z>=0&&x<nx&&z<nz&&!occupied[x,z]&&!reached[x,z]){reached[x,z]=true;queue.Enqueue(new Vector2Int(x,z));}}}
        string result="Missing UV2 meshes: "+missingUV+"\nUnmapped material slots: "+unmapped+"\nPlayer start clear: "+(!occupied[start.x,start.y])+"\n";
        var sp=origin+new Vector3(start.x*step,0,start.y*step);
        result+="Start blockers: "+string.Join(", ",Physics.OverlapCapsule(sp+Vector3.up*.23f,sp+Vector3.up*1.52f,.23f,~0,QueryTriggerInteraction.Ignore).Select(c=>c.name))+"\n";
        foreach(string target in new[]{"Nav_Living","Nav_Kitchen","Nav_Bathroom","Nav_Dining"})
        {var p=index(names[target].position);result+=target+" reachable: "+reached[p.x,p.y]+"\n";}
        door.localRotation=closed;controller.enabled=true;Physics.SyncTransforms();return result;
    }
}
