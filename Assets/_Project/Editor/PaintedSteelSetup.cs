using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PaintedSteelSetup
{
    const string Folder="Assets/_Project/Materials/BrightonModels/";
    static PaintedSteelSetup(){EditorApplication.delayCall+=Run;}
    [MenuItem("Tools/Brighton/Apply Painted Steel")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)return;
        try{
            string source=Folder+"PaintedSteel_BaseColor.png";
            AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(source);
            importer.wrapMode=TextureWrapMode.Repeat;importer.sRGBTexture=true;importer.maxTextureSize=2048;importer.SaveAndReimport();
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(source);
            if(!tex)throw new Exception("Steel texture not imported");
            string path=Folder+"PaintedSteel.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat)return; // one-time setup; do not overwrite later user edits
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name="PaintedSteel";
            mat.SetTexture("_BaseMap",tex);mat.SetColor("_BaseColor",Color.white);
            mat.SetFloat("_Metallic",.3f);mat.SetFloat("_Smoothness",.45f);
            AssetDatabase.CreateAsset(mat,path);
            int slots=0;
            var scene=SceneManager.GetActiveScene();
            if(scene.name=="00_BrightonDrive_Models"){
                Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply painted steel material");
                foreach(var root in scene.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
                    var materials=r.sharedMaterials;bool changed=false;
                    for(int i=0;i<materials.Length;i++){
                        if(!materials[i])continue;string n=materials[i].name;
                        if(n=="Painted_Iron"||n=="WornSteel"){materials[i]=mat;changed=true;slots++;}
                    }
                    if(changed){Undo.RecordObject(r,"Apply steel");r.sharedMaterials=materials;}
                }
                Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Folder+"PaintedSteel.result","SUCCESS material="+path+" shader="+mat.shader.name+" texture="+AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap"))+" slots="+slots);
        }catch(Exception e){File.WriteAllText(Folder+"PaintedSteel.result",e.ToString());Debug.LogException(e);}
    }
}
