using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class BrightonShotRepair {
 const string P="Assets/_Project/Editor/";
 static BrightonShotRepair(){EditorApplication.delayCall+=Diagnose;EditorApplication.delayCall+=Fix;}
 static void Fix(){
  if(EditorApplication.isPlaying || !File.Exists(P+"ShotFix.request"))return;File.Delete(P+"ShotFix.request");
  var scene=SceneManager.GetActiveScene();var root=GameObject.Find("Brighton_Models_Assembled");if(!root)return;
  EditorSceneManager.SaveScene(scene,"Assets/_Project/Scenes/BeforeShotFix_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
  var cameras=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>(true)).ToArray();
  var main=cameras.First(x=>x.name=="Main Camera");
  foreach(var c in cameras){Undo.RecordObject(c,"Select cutscene camera");c.enabled=c==main;}
  Undo.RecordObject(main.transform,"Set safe cabin camera");main.transform.localPosition=new Vector3(.05f,1.63f,-.85f);main.transform.localRotation=Quaternion.Euler(2,0,0);main.nearClipPlane=.03f;
  string dir="Assets/_Project/Materials/BrightonModels/";Directory.CreateDirectory(dir);
  int count=0;
  foreach(Transform building in root.transform){
   var wall=building.Find("Wall_Left_Closure");if(!wall)continue;
   var mats=building.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct();
   var brick=mats.FirstOrDefault(m=>new[]{"Hudson_Red","Mercer_Ochre","Fulton_Umber","Rosette_Russet","Union_Painted"}.Contains(m.name));if(!brick)continue;
   string path=dir+brick.name+"_Side.mat";var side=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!side){side=new Material(Shader.Find("Universal Render Pipeline/Lit"));side.SetTexture("_BaseMap",brick.GetTexture("baseColorTexture"));side.SetColor("_BaseColor",brick.HasProperty("baseColorFactor")?brick.GetColor("baseColorFactor"):Color.white);side.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(side,path);}
   var mf=wall.GetComponent<MeshFilter>();string meshPath=dir+building.name+"_Side.asset";
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
   if(!mesh){mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);var verts=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[verts.Length];var scale=wall.lossyScale;
    for(int i=0;i<uv.Length;i++){var v=Vector3.Scale(verts[i],scale);uv[i]=Mathf.Abs(normals[i].x)>.5f?new Vector2(v.z,v.y)/2f:new Vector2(v.x,v.y)/2f;}mesh.uv=uv;AssetDatabase.CreateAsset(mesh,meshPath);}
   Undo.RecordObject(mf,"Set wall UV");mf.sharedMesh=mesh;Undo.RecordObject(wall.GetComponent<Renderer>(),"Texture wall");wall.GetComponent<Renderer>().sharedMaterial=side;count++;
  }
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
  File.WriteAllText(P+"ShotFix.result","walls="+count+" cameras="+string.Join(",",cameras.Select(c=>c.name+":"+c.enabled)));
 }
 static void Diagnose(){
  var s=new StringBuilder();
  foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
  foreach(var mb in root.GetComponentsInChildren<MonoBehaviour>(true)){
   if(!mb)continue;var type=mb.GetType().Name;if(!type.Contains("Brighton")&&!type.Contains("Camera")&&!type.Contains("Cutscene"))continue;
   s.AppendLine("COMPONENT "+mb.name+" "+type);var so=new SerializedObject(mb);var p=so.GetIterator();
   while(p.NextVisible(true)){
    if(p.name=="m_Script")continue;
    if(p.propertyType==SerializedPropertyType.Vector3)s.AppendLine(p.propertyPath+"="+p.vector3Value);
    if(p.propertyType==SerializedPropertyType.Float)s.AppendLine(p.propertyPath+"="+p.floatValue);
    if(p.propertyType==SerializedPropertyType.Boolean)s.AppendLine(p.propertyPath+"="+p.boolValue);
    if(p.propertyType==SerializedPropertyType.ObjectReference)s.AppendLine(p.propertyPath+"="+(p.objectReferenceValue?p.objectReferenceValue.name:"null"));
   }
  }
  var model=GameObject.Find("Brighton_Models_Assembled");
  if(model){foreach(var mat in model.transform.GetChild(0).GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct()){
   s.AppendLine("MATERIAL "+mat.name);foreach(var tex in mat.GetTexturePropertyNames())s.AppendLine(tex+"="+AssetDatabase.GetAssetPath(mat.GetTexture(tex)));
  }}
  File.WriteAllText(P+"ShotDiagnosis.txt",s.ToString());
 }
}
