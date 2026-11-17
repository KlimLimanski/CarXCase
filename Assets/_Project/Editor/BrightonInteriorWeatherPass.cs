using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.Linq;
using CarXCase.Intro;
public static class BrightonInteriorWeatherPass {
 const string Dir="Assets/_Project/ArtDirection/BrightonReference/";
 static Material M(string n,Color c,float smooth,float metal=0){var path=Dir+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=c;m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);EditorUtility.SetDirty(m);return m;}
 static Bounds BoundsOf(GameObject o){var rr=o.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
 static void FitLenses(Transform vehicle){
 var visual=vehicle.Find("ReferenceVisual");if(!visual)return;
 bool active=vehicle.gameObject.activeSelf;vehicle.gameObject.SetActive(true);
 var b=BoundsOf(visual.gameObject);var cols=visual.GetComponentsInChildren<MeshFilter>(true).Select(f=>{var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;return c;}).ToArray();Physics.SyncTransforms();
 int side=-1;foreach(var lens in vehicle.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Headlamp_Lens")){
 var vertices=new Vector3[45];var uv=new Vector2[45];var tris=new System.Collections.Generic.List<int>();
 for(int y=0;y<=4;y++)for(int x=0;x<=8;x++){int i=y*9+x;float u=x/8f,v=y/4f;var origin=new Vector3(b.center.x+side*b.size.x*.26f+(u-.5f)*.32f,b.min.y+b.size.y*.51f+(v-.5f)*.115f,b.min.z-1);var point=origin+Vector3.forward;float best=10;foreach(var c in cols)if(c.Raycast(new Ray(origin,Vector3.forward),out var hit,5)&&hit.distance<best){best=hit.distance;point=hit.point-Vector3.forward*.025f;}vertices[i]=lens.transform.InverseTransformPoint(point);uv[i]=new Vector2(u,v);if(x<8&&y<4){tris.AddRange(new[]{i,i+9,i+1,i+1,i+9,i+10});}}
 var mesh=new Mesh();mesh.name="Conforming_Halogen_Lens";mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();string path=Dir+vehicle.name+"_Lens_"+side+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}else{AssetDatabase.CreateAsset(mesh,path);saved=mesh;}lens.sharedMesh=saved;side=1;
 }
 foreach(var c in cols)Object.DestroyImmediate(c);vehicle.gameObject.SetActive(active);
 }
 [MenuItem("Tools/Brighton/Refine interior traffic and rain")]
 public static void Apply(){
 if(EditorApplication.isPlaying)throw new System.Exception("Stop Play Mode");
 var cut=Object.FindAnyObjectByType<BrightonDriveCutscene>();if(!cut)throw new System.Exception("Open Brighton intro");var car=cut.car;
 var old=car.Find("Sedan_2000_Visual");if(old)old.gameObject.SetActive(false);
 foreach(Transform t in car)if(t.name.EndsWith("_HeightFit"))t.gameObject.SetActive(false);
 var prior=car.Find("Sedan_Reference_Interior");if(prior)Object.DestroyImmediate(prior.gameObject);
 var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Arts/Car/ReferenceInterior/Sedan_1999_Reference_Interior.fbx");
 var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.name="Sedan_Reference_Interior";go.transform.SetParent(car,false);go.transform.localScale=new Vector3(1.35f,1.3f,1.72f);
 var rr=go.GetComponentsInChildren<Renderer>(true);var seat=rr.First(r=>r.name=="Driver_Seat_Cushion");var dash=rr.First(r=>r.name=="Dashboard_Lower");
 if(dash.bounds.center.z<seat.bounds.center.z)go.transform.localRotation=Quaternion.Euler(0,180,0);
 go.transform.localScale*=.64f/Mathf.Max(seat.bounds.size.x,seat.bounds.size.z);
 go.transform.position+=car.TransformPoint(new Vector3(-.43f,.64f,-.1f))-seat.bounds.center;
 var cloth=M("Interior_Charcoal_Weave",Color.white,.12f);cloth.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Arts/Car/ReferenceInterior/Charcoal_Seat_Weave.png"));
 var vinyl=M("Interior_Grained_Vinyl",Color.white,.22f);vinyl.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Arts/Car/ReferenceInterior/Dashboard_Vinyl_Grain.png"));
 var trim=M("Interior_Charcoal_Trim",new Color(.115f,.11f,.10f),.24f);
 var metal=M("Interior_Satin_Hardware",new Color(.24f,.25f,.24f),.46f,.7f);
 var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/BrightonModels/AutomotiveGlass.mat");
 foreach(var r in rr){var mm=r.sharedMaterials;for(int i=0;i<mm.Length;i++){string n=mm[i].name;if(n.Contains("Glass"))mm[i]=glass;else if(n.Contains("Woven")||n.Contains("Bolsters")||n.Contains("Headliner")||n.Contains("Carpet"))mm[i]=cloth;else if(n.Contains("Dashboard")||n.Contains("Plastic"))mm[i]=vinyl;else if(n.Contains("Gray_Trim"))mm[i]=trim;else if(n.Contains("Metal"))mm[i]=metal;}r.sharedMaterials=mm;if(mm.Contains(glass))r.shadowCastingMode=ShadowCastingMode.Off;if(r.name.StartsWith("Simple_Tire")||r.name.StartsWith("Simple_Wheel"))r.enabled=false;}
 // Preserve the traffic roots and timing; fix only their visual heading.
 var lens=M("Traffic_Warm_Halogen",new Color(1,.86f,.60f),.7f);lens.shader=Shader.Find("Universal Render Pipeline/Unlit");lens.SetColor("_BaseColor",new Color(5,3.9f,2.4f,1));lens.SetFloat("_Cull",0);EditorUtility.SetDirty(lens);
 foreach(var v in cut.oncomingVehicles){var visual=v.root.Find("ReferenceVisual");if(!visual)continue;
 foreach(var l in v.root.GetComponentsInChildren<Light>(true))l.enabled=false;
 var oldLights=v.root.Find("Reference_Headlights");if(oldLights)Object.DestroyImmediate(oldLights.gameObject);
 visual.localRotation=Quaternion.Euler(0,180,0);var b=BoundsOf(visual.gameObject);
 var lights=new GameObject("Reference_Headlights").transform;lights.SetParent(v.root,false);
 for(int side=-1;side<=1;side+=2){var p=new Vector3(b.center.x+side*b.size.x*.26f,b.min.y+b.size.y*.51f,b.min.z-.015f);
 var g=new GameObject("Halogen_Beam");g.transform.SetParent(lights,false);g.transform.position=p;g.transform.rotation=Quaternion.LookRotation(new Vector3(0,-.08f,-1));var l=g.AddComponent<Light>();l.type=LightType.Spot;l.color=new Color(1,.85f,.64f);l.intensity=220;l.range=38;l.spotAngle=58;l.innerSpotAngle=30;l.shadows=LightShadows.None;
 var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Headlamp_Lens";face.transform.SetParent(g.transform,false);face.transform.localRotation=Quaternion.Euler(0,180,0);face.transform.localScale=new Vector3(.30f,.13f,1);face.GetComponent<Renderer>().sharedMaterial=lens;Object.DestroyImmediate(face.GetComponent<Collider>());}
 FitLenses(v.root);
 }
 var wet=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Weather/WetRoad.mat");wet.SetColor("_BaseColor",new Color(.38f,.40f,.40f));wet.SetFloat("_PuddleAmount",.48f);wet.SetFloat("_RippleStrength",.035f);wet.SetFloat("_DetailStrength",.20f);EditorUtility.SetDirty(wet);
 foreach(var p in Object.FindObjectsByType<ReflectionProbe>()){p.intensity=.8f;p.clearFlags=ReflectionProbeClearFlags.SolidColor;p.backgroundColor=new Color(.018f,.022f,.025f);}
 var rain=GameObject.Find("Rain_Particles");if(rain){var ps=rain.GetComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.startSize=new ParticleSystem.MinMaxCurve(.005f,.009f);main.startColor=new Color(.50f,.55f,.58f,.24f);main.maxParticles=2400;main.startLifetime=1.2f;var em=ps.emission;em.rateOverTime=850;var shape=ps.shape;shape.scale=new Vector3(5,.1f,38);rain.transform.localPosition=new Vector3(-11,8,12);var render=rain.GetComponent<ParticleSystemRenderer>();render.lengthScale=1.4f;render.velocityScale=.018f;
 var tex=new Texture2D(16,64,TextureFormat.RGBA32,false);for(int y=0;y<64;y++)for(int x=0;x<16;x++){float a=Mathf.Exp(-Mathf.Pow((x-7.5f)/2.3f,2))*Mathf.Pow(Mathf.Sin(Mathf.PI*y/63f),1.5f);tex.SetPixel(x,y,new Color(.8f,.85f,.9f,a));}tex.Apply();var tp=Dir+"Rain_Streak.asset";var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(tp);if(!saved){AssetDatabase.CreateAsset(tex,tp);saved=tex;}else Object.DestroyImmediate(tex);render.sharedMaterial.SetTexture("_BaseMap",saved);EditorUtility.SetDirty(render.sharedMaterial);
 var right=car.Find("Rain_Right_Street");if(right)Object.DestroyImmediate(right.gameObject);var clone=Object.Instantiate(rain,car);clone.name="Rain_Right_Street";clone.transform.localPosition=new Vector3(11,8,12);}
 RenderSettings.ambientSkyColor=new Color(.32f,.33f,.34f);RenderSettings.ambientEquatorColor=new Color(.23f,.22f,.20f);
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
 }
}





