using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad] public static class ApartmentLayoutProbe {
 static ApartmentLayoutProbe(){EditorApplication.delayCall+=Run;}
 static void Run(){var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Arts/Apartaments/FBX/BrooklynApartment.fbx");if(!model)return;var s=new StringBuilder();foreach(var t in model.GetComponentsInChildren<Transform>(true)){int d=0;for(var p=t.parent;p!=null;p=p.parent)d++;if(d<3)s.AppendLine(new string(' ',d)+t.name+" position="+t.position+" scale="+t.localScale);}File.WriteAllText("Assets/_Project/Editor/ApartmentLayout.txt",s.ToString());}
}
