using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class BuildBrightonMaterials
{
    const string Root="Assets/_Project/Materials/BrightonModels/";
    static BuildBrightonMaterials(){EditorApplication.delayCall+=Run;}
    static void Run(){
        string request=Root+"Build.request";if(!File.Exists(request))return;File.Delete(request);
        var brick=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"AgedBrick_BaseColor.png");
        var metal=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"WornSteel_BaseColor.png");
        var asphalt=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"WetAsphalt_BaseColor.png");
        AssetDatabase.Refresh();
        brick=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"AgedBrick_BaseColor.png");metal=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"WornSteel_BaseColor.png");asphalt=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"WetAsphalt_BaseColor.png");
        var mb=Make("AgedBrick",brick,.0f,.48f);var mm=Make("WornSteel",metal,.82f,.38f);var ma=Make("WetAsphalt",asphalt,.08f,.84f);
        var scene=SceneManager.GetActiveScene();var assembled=GameObject.Find("Brighton_Models_Assembled");
        if(assembled){
            foreach(var r in assembled.GetComponentsInChildren<Renderer>(true)){
                var n=r.name.ToLowerInvariant();
                if(n.Contains("fire_escape")||n.Contains("painted_iron")||n.Contains("metal")||n.Contains("railing")){r.sharedMaterial=mm;continue;}
                if(n.Contains("simple_shell")||n.Contains("right_side")){r.sharedMaterial=mb;continue;}
            }
        }
        var street=GameObject.Find("Brighton_Street");
        if(street)foreach(var r in street.GetComponentsInChildren<Renderer>(true))if(r.name=="Road"||r.name.ToLowerInvariant().Contains("road"))r.sharedMaterial=ma;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText(Root+"Build.result","Created URP materials AgedBrick, WornSteel, WetAsphalt; assigned scene objects.");
    }
    static Material Make(string name,Texture2D tex,float metallic,float smooth){
        var path=Root+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        if(tex){m.SetTexture("_BaseMap",tex);tex.wrapMode=TextureWrapMode.Repeat;}
        m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);m.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(m);return m;
    }
}
