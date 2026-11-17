using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CarXCase.Intro;
public static class BrightonReferencePass {
 const string Dir="Assets/_Project/ArtDirection/BrightonReference/";
 static Transform root;
 static Material Mat(string name,Color color,float metallic,float smooth){var p=Dir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;}
 static GameObject Box(string name,Vector3 pos,Vector3 size,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
 static Light Lamp(string n,Vector3 p,Color c,float intensity,float range,Transform parent=null){var g=new GameObject(n);g.transform.SetParent(parent?parent:root,false);g.transform.localPosition=p;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=c;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;return l;}
 static Bounds BoundsOf(GameObject g){var rr=g.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
 static void Car(Transform parent,string file,Vector3 location,float yaw){var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Arts/Car/"+file+".glb");if(!asset)throw new Exception(file);var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.name="ReferenceCar_"+file;go.transform.SetParent(parent,false);go.transform.localRotation=Quaternion.Euler(0,yaw+90,0);var b=BoundsOf(go);go.transform.localScale*=4.8f/Mathf.Max(b.size.x,b.size.z);b=BoundsOf(go);go.transform.position+=parent.TransformPoint(location)-new Vector3(b.center.x,b.min.y,b.center.z);}
 [MenuItem("Tools/Brighton/Apply reference art pass")]
 public static void Apply(){
 if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode");
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="00_BrightonDrive_Models")throw new Exception("Open Models scene");
 Directory.CreateDirectory(Dir);AssetDatabase.Refresh();
 if(!File.Exists("Assets/_Project/Scenes/BeforeReferencePass.unity"))EditorSceneManager.SaveScene(scene,"Assets/_Project/Scenes/BeforeReferencePass.unity",true);
 var previous=GameObject.Find("Brighton_Reference_Art");if(previous)UnityEngine.Object.DestroyImmediate(previous);
 root=new GameObject("Brighton_Reference_Art").transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Brighton reference pass");
 var steel=Mat("Elevated_Painted_Steel",new Color(.12f,.15f,.14f),.65f,.36f);
 var rust=Mat("Elevated_Foot_Oxidation",new Color(.18f,.10f,.055f),.15f,.22f);
 var dark=Mat("Elevated_Deck",new Color(.055f,.062f,.06f),.2f,.25f);
 var warm=Mat("Sodium_Diffuser",new Color(.65f,.4f,.15f),0,.4f);warm.EnableKeyword("_EMISSION");warm.SetColor("_EmissionColor",new Color(1,.62f,.28f)*3);
 var concrete=Mat("Curb_Aged_Concrete",new Color(.27f,.29f,.28f),0,.45f);
 var old=GameObject.Find("Brighton_Street");
 foreach(var r in old.GetComponentsInChildren<Renderer>())if(r.name.StartsWith("Rail_"))r.enabled=false;
 var bay=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Arts/Infrastructure/BrightonElevated/Brighton_Elevated_Bay.fbx");
 for(int z=-20;z<705;z+=18){var g=(GameObject)PrefabUtility.InstantiatePrefab(bay);g.transform.SetParent(root,false);g.transform.localPosition=new Vector3(0,0,z);g.name="Elevated_Bay_"+z;
 foreach(var r in g.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains("Oxidized")?rust:m.name.Contains("Underside")?dark:steel).ToArray();
 Box("Underdeck_Fixture",new Vector3(0,5.43f,z+7),new Vector3(.65f,.12f,.4f),steel);Box("Underdeck_Diffuser",new Vector3(0,5.35f,z+7),new Vector3(.48f,.035f,.27f),warm);
 Lamp("Sodium_Pool",new Vector3(0,5.05f,z+7),new Color(1,.64f,.32f),5.5f,16);
 }
 string[] cars={"car1","car2","car3","car4","car5","car6","taxi"};int ci=0;
 foreach(var r in old.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("ParkedCar_Body_")).ToArray()){
 r.enabled=false;var tail=r.name.Substring("ParkedCar_Body_".Length);var cabin=old.transform.Find("ParkedCar_Cabin_"+tail);if(cabin)cabin.GetComponent<Renderer>().enabled=false;
 Car(root,cars[ci++%cars.Length],new Vector3(r.transform.position.x,.04f,r.transform.position.z),r.transform.position.x<0?180:0);
 }
 var cut=UnityEngine.Object.FindAnyObjectByType<BrightonDriveCutscene>();ci=0;
 foreach(var v in cut.oncomingVehicles){foreach(var r in v.root.GetComponentsInChildren<Renderer>())r.enabled=false;var existing=v.root.Find("ReferenceVisual");if(existing)UnityEngine.Object.DestroyImmediate(existing.gameObject);var holder=new GameObject("ReferenceVisual").transform;holder.SetParent(v.root,false);Car(holder,v.root.name.Contains("Police")?"police car":v.root.name.Contains("Taxi")?"taxi":cars[ci++%6],Vector3.zero,0);}
 for(int side=-1;side<=1;side+=2){
 var curb=old.transform.Find("Curb_"+side);if(curb)curb.GetComponent<Renderer>().enabled=false;
 for(int z=-50;z<760;z+=3)Box("Jointed_Granite_Curb",new Vector3(side*8.06f,.13f,z),new Vector3(.23f,.28f,2.975f),concrete);
 for(int z=0;z<680;z+=32)Lamp("Shop_Window_Spill",new Vector3(side*11.1f,2.6f,z),side<0?new Color(1,.72f,.43f):new Color(.62f,.75f,.70f),3.5f,9);
 }
 foreach(var l in UnityEngine.Object.FindObjectsByType<Light>()){
 if(l.type==LightType.Rectangle)l.enabled=false;
 if(l.type==LightType.Directional){l.color=new Color(.55f,.65f,.78f);l.intensity=.32f;l.transform.rotation=Quaternion.Euler(24,-35,0);l.shadows=LightShadows.Soft;}}
 RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.15f,.18f,.22f);RenderSettings.ambientEquatorColor=new Color(.085f,.10f,.11f);RenderSettings.ambientGroundColor=new Color(.035f,.034f,.03f);
 RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.085f,.10f,.115f);RenderSettings.fogStartDistance=35;RenderSettings.fogEndDistance=190;
 var cam=Camera.main;cam.backgroundColor=RenderSettings.fogColor;cam.allowHDR=true;cam.allowMSAA=true;cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;cam.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
 var car=cut.car;var fill=car.Find("Cabin_Reflected_Fill");if(fill)UnityEngine.Object.DestroyImmediate(fill.gameObject);Lamp("Cabin_Reflected_Fill",new Vector3(.5f,1.8f,.9f),new Color(.7f,.75f,.85f),.32f,3.5f,car);
 var volume=root.gameObject.AddComponent<Volume>();volume.isGlobal=true;var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Dir+"NightGrade.asset");if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Dir+"NightGrade.asset");}volume.sharedProfile=profile;
 if(!profile.TryGet<Tonemapping>(out var tm))tm=profile.Add<Tonemapping>();tm.mode.Override(TonemappingMode.ACES);
 if(!profile.TryGet<ColorAdjustments>(out var ca))ca=profile.Add<ColorAdjustments>();ca.postExposure.Override(.5f);ca.saturation.Override(-12);ca.contrast.Override(12);
 if(!profile.TryGet<Bloom>(out var bloom))bloom=profile.Add<Bloom>();bloom.threshold.Override(1.2f);bloom.intensity.Override(.18f);
 foreach(var r in old.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Centerline_")))r.transform.position=new Vector3(r.transform.position.x,.029f,r.transform.position.z);
 var wet=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Weather/WetRoad.mat");if(wet){wet.SetFloat("_PuddleAmount",.58f);wet.SetFloat("_RippleStrength",.16f);wet.SetFloat("_DetailStrength",.35f);EditorUtility.SetDirty(wet);}
 FinalizeLook();BrightonInteriorWeatherPass.Apply();EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 }
 public static void FinalizeLook(){
 RenderSettings.skybox=null;RenderSettings.reflectionIntensity=.35f;
 RenderSettings.ambientSkyColor=new Color(.2f,.22f,.25f);RenderSettings.ambientEquatorColor=new Color(.13f,.13f,.12f);
 foreach(var p in UnityEngine.Object.FindObjectsByType<ReflectionProbe>()){p.clearFlags=ReflectionProbeClearFlags.SolidColor;p.backgroundColor=new Color(.045f,.05f,.055f);p.intensity=.55f;}
 foreach(var l in UnityEngine.Object.FindObjectsByType<Light>()){
 if(l.name=="Sodium_Pool"){l.type=LightType.Spot;l.transform.rotation=Quaternion.Euler(90,0,0);l.spotAngle=130;l.innerSpotAngle=75;l.intensity=170;l.shadows=LightShadows.Soft;l.shadowBias=.04f;l.shadowNormalBias=.15f;}
 if(l.name=="Shop_Window_Spill")l.intensity=28;
 if(l.name=="Cabin_Reflected_Fill"){l.intensity=.55f;l.color=new Color(.8f,.75f,.65f);}}
 var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/BrightonModels/AutomotiveGlass.mat");if(glass){glass.SetFloat("_SpecularHighlights",0);glass.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");glass.SetFloat("_Smoothness",.65f);EditorUtility.SetDirty(glass);}
 var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Dir+"NightGrade.asset");if(profile&&profile.TryGet<ColorAdjustments>(out var ca)){ca.postExposure.Override(.9f);EditorUtility.SetDirty(profile);}
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
 }
}



