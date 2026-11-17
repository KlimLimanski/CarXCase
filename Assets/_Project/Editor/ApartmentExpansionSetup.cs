using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using DetectiveGame.Core;

[InitializeOnLoad]
public static class ApartmentExpansionSetup
{
 const string P="Assets/_Project/";
 static ApartmentExpansionSetup(){EditorApplication.delayCall+=Run;EditorApplication.delayCall+=FinishLayout;}
 static void FinishLayout(){
  string request=P+"Editor/ApartmentLayoutFinish.request";
  if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
  File.Delete(request);
  try{
   var scene=SceneManager.GetActiveScene();
   if(scene.path!=P+"Scenes/01_FatherApartment.unity")return;
   var root=GameObject.Find("Apartment_AdditionalRooms").transform;
   var floor=root.Find("Floor_Extension").GetComponent<Renderer>().bounds;
   var front=root.Find("Wall_EntryFront");var back=root.Find("Wall_EntryBack");var lintel=root.Find("Wall_EntryLintel");
   float z=-.85f,a=z-.7f,c=z+.7f;
   front.position=new Vector3(front.position.x,front.position.y,(c+floor.max.z)/2);
   front.localScale=new Vector3(front.localScale.x,front.localScale.y,floor.max.z-c);
   back.position=new Vector3(back.position.x,back.position.y,(floor.min.z+a)/2);
   back.localScale=new Vector3(back.localScale.x,back.localScale.y,a-floor.min.z);
   lintel.position=new Vector3(lintel.position.x,lintel.position.y,z);
   var player=GameObject.Find("Player").transform;
   player.position=new Vector3(player.position.x,player.position.y,-2.05f);
   var camera=Camera.main;var follow=camera.GetComponent<ThirdPersonCamera>();
   var rotation=Quaternion.Euler(12,player.eulerAngles.y,0);
   camera.transform.SetPositionAndRotation(player.position+follow.offset-rotation*Vector3.forward*follow.distance,rotation);
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   File.WriteAllText(P+"Editor/ApartmentLayoutFinish.result","SAVED entry opening moved clear of existing wardrobe; no Play Mode tests run");
  }catch(Exception e){Debug.LogException(e);File.WriteAllText(P+"Editor/ApartmentLayoutFinish.result",e.ToString());}
 }
 static Transform[] nodes;
 static Transform Find(string name)=>nodes.First(t=>t.name==name);
 static void Run(){
  string request=P+"Editor/ApartmentExpansion.request";
  if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
  File.Delete(request);
  try{
   var current=SceneManager.GetActiveScene();if(current.isDirty)EditorSceneManager.SaveScene(current);
   var scene=EditorSceneManager.OpenScene(P+"Scenes/01_FatherApartment.unity");
   if(GameObject.Find("Apartment_AdditionalRooms"))throw new Exception("Expansion already exists.");
   EditorSceneManager.SaveScene(scene,P+"Scenes/BeforeExpansion_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
   var apartment=GameObject.Find("BrooklynApartment");nodes=apartment.GetComponentsInChildren<Transform>(true);
   var floor=Find("Floor_Wood").GetComponent<Renderer>();var b=floor.bounds;
   var wall=Find("Wall_East").GetComponent<Renderer>();float x=wall.bounds.center.x;
   float height=Find("Ceiling").GetComponent<Renderer>().bounds.min.y;
   var wm=wall.sharedMaterial;var fm=floor.sharedMaterial;var cm=Find("Ceiling").GetComponent<Renderer>().sharedMaterial;
   var added=new GameObject("Apartment_AdditionalRooms").transform;
   float width=3.6f,left=x-width,center=(left+x)*.5f,zmid=b.center.z;
   float entranceZ=-.85f;
   wall.enabled=false;Find("COL_Wall_East").gameObject.SetActive(false);
   // A side wing increases floor space while retaining the existing furniture scale.
   Cube(added,"Floor_Extension",new Vector3(center,-.075f,zmid),new Vector3(width+.15f,.15f,b.size.z),fm);
   Cube(added,"Ceiling_Extension",new Vector3(center,height+.075f,zmid),new Vector3(width+.15f,.15f,b.size.z),cm);
   Cube(added,"Wall_OuterWest",new Vector3(left-.075f,height/2,zmid),new Vector3(.15f,height,b.size.z+.3f),wm);
   Cube(added,"Wall_Front",new Vector3(center,height/2,b.max.z+.075f),new Vector3(width+.3f,height,.15f),wm);
   Cube(added,"Wall_Back",new Vector3(center,height/2,b.min.z-.075f),new Vector3(width+.3f,height,.15f),wm);
   float a=entranceZ-.7f,c=entranceZ+.7f;
   Cube(added,"Wall_EntryFront",new Vector3(x,height/2,(c+b.max.z)/2),new Vector3(.18f,height,b.max.z-c),wm);
   Cube(added,"Wall_EntryBack",new Vector3(x,height/2,(b.min.z+a)/2),new Vector3(.18f,height,a-b.min.z),wm);
   Cube(added,"Wall_EntryLintel",new Vector3(x,(height+2.25f)/2,entranceZ),new Vector3(.18f,height-2.25f,1.4f),wm);
   // Partial dividing wall creates a study and a sitting/storage area with an open passage.
   Cube(added,"Wall_StudyDivider",new Vector3(left+1.15f,height/2,zmid),new Vector3(2.3f,height,.15f),wm);
   Copy(added,"Sofa_Assembly","Sofa_Study",new Vector3(left+.6f,0,zmid-1.5f));
   Copy(added,"Wardrobe_Assembly","Wardrobe_Storage",new Vector3(left+.6f,0,-1.1f));
   Copy(added,"DiningChair_Assembly","Chair_Study",new Vector3(x-.9f,0,zmid-1.5f));
   var table=new GameObject("Table_Study").transform;table.SetParent(added,false);
   foreach(var source in nodes.Where(t=>t.name.StartsWith("CoffeeTable_"))) {var part=UnityEngine.Object.Instantiate(source.gameObject,table);part.transform.position=source.position;part.transform.rotation=source.rotation;part.transform.localScale=source.lossyScale;}
   PositionOnFloor(table,new Vector3(left+1.8f,0,zmid-1.5f));
   foreach(float z in new[]{-1.8f,zmid-1.8f}){var go=new GameObject("Light_Extension");go.transform.SetParent(added);go.transform.position=new Vector3(center,height-.3f,z);var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.84f,.65f);l.intensity=1.6f;l.range=5;l.shadows=LightShadows.Soft;}
   var player=GameObject.Find("Player");var cc=player.GetComponent<CharacterController>();cc.height=1.8f;cc.radius=.25f;cc.center=new Vector3(0,.9f,0);
   var camera=Camera.main;camera.transform.SetParent(null,true);camera.nearClipPlane=.04f;camera.fieldOfView=65;
   var follow=camera.GetComponent<ThirdPersonCamera>()??camera.gameObject.AddComponent<ThirdPersonCamera>();follow.target=player.transform;follow.offset=new Vector3(0,1.45f,0);follow.distance=1.4f;follow.minDistance=.15f;follow.maxDistance=2;
   var rotation=Quaternion.Euler(12,player.transform.eulerAngles.y,0);camera.transform.position=player.transform.position+follow.offset-rotation*Vector3.forward*follow.distance;camera.transform.rotation=rotation;
   var hud=new GameObject("Gameplay_HUD").AddComponent<SimpleUIHUD>();hud.gameObject.AddComponent<QteManager>();
   var officer=player.GetComponent<OfficerController>()??player.AddComponent<OfficerController>();officer.cameraTransform=camera.transform;officer.hud=hud;officer.walkSpeed=1.8f;officer.runSpeed=3;officer.interactionRadius=1.5f;
   var explorer=player.GetComponent<ApartmentExplorer>();explorer.externalMovement=true;explorer.eye=camera;
   var visual=new GameObject("Character_Visual").transform;visual.SetParent(player.transform,false);
   var coat=Mat("PlayerCoat",new Color(.2f,.24f,.22f));var skin=Mat("PlayerSkin",new Color(.56f,.4f,.29f));var dark=Mat("PlayerTrousers",new Color(.06f,.07f,.08f));
   Body(visual,"Torso",new Vector3(0,1.15f,0),new Vector3(.44f,.6f,.26f),coat);
   Body(visual,"Head",new Vector3(0,1.65f,0),new Vector3(.24f,.29f,.24f),skin);
   for(int side=-1;side<=1;side+=2){Body(visual,"Arm_"+side,new Vector3(side*.28f,1.08f,0),new Vector3(.12f,.62f,.17f),coat);Body(visual,"Leg_"+side,new Vector3(side*.12f,.46f,0),new Vector3(.18f,.8f,.22f),dark);}
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
   File.WriteAllText(P+"Editor/ApartmentExpansion.result","SAVED area="+(b.size.x*b.size.z+width*b.size.z)+" cameraDistance="+follow.distance+" scene="+scene.path+"; no Play Mode tests run");
  }catch(Exception e){File.WriteAllText(P+"Editor/ApartmentExpansion.result",e.ToString());Debug.LogException(e);}
 }
 static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material mat){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;return go;}
 static void Body(Transform parent,string name,Vector3 p,Vector3 scale,Material m){var go=Cube(parent,name,Vector3.zero,scale,m);go.transform.localPosition=p;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}
 static Material Mat(string name,Color c){string p=P+"Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=c;AssetDatabase.CreateAsset(m,p);}return m;}
 static void Copy(Transform parent,string source,string name,Vector3 dest){var t=Find(source);var go=UnityEngine.Object.Instantiate(t.gameObject,parent);go.name=name;go.transform.position=t.position;go.transform.rotation=t.rotation;go.transform.localScale=t.lossyScale;PositionOnFloor(go.transform,dest);}
 static void PositionOnFloor(Transform t,Vector3 dest){var rr=t.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);t.position+=new Vector3(dest.x-b.center.x,-b.min.y,dest.z-b.center.z);if(t.GetComponentsInChildren<Collider>().Length==0){var box=t.gameObject.AddComponent<BoxCollider>();box.center=t.InverseTransformPoint(new Vector3(dest.x,b.extents.y,dest.z));box.size=new Vector3(b.size.x/t.lossyScale.x,b.size.y/t.lossyScale.y,b.size.z/t.lossyScale.z);}}
}
