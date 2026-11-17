using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Renders the hero's current Walk/Run/Idle clips from the side and front in a temporary scene
/// (nothing in the open scene changes). Output: HeroSetup/anim_*.png + anim_review.txt.
/// Trigger: Tools/Hero/Review walk and run, or HeroSetup/heroanim_request.txt.
[InitializeOnLoad]
public static class HeroAnimReview
{
    const string Model = "Assets/_Project/Arts/Heroes/Main Hero/MainHero_Rigged.glb";
    const string AnimDir = "Assets/_Project/Arts/Heroes/Animations/";
    static HeroAnimReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        string req = "HeroSetup/heroanim_request.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(req)) return;
        File.Delete(req); Review();
    }

    [MenuItem("Tools/Hero/Review walk and run")]
    public static void Review()
    {
        var log = new StringBuilder("ANIM REVIEW " + DateTime.Now + "\n");
        var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var origin = new Vector3(500, 0, 500);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var hero = (GameObject)UnityEngine.Object.Instantiate(prefab);
            SceneManager_Move(hero, temp);
            hero.transform.position = origin; hero.transform.rotation = Quaternion.identity;
            var light = new GameObject("key").AddComponent<Light>(); SceneManager_Move(light.gameObject, temp);
            light.type = LightType.Directional; light.intensity = 2.2f; light.transform.rotation = Quaternion.Euler(35, 140, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); SceneManager_Move(floor, temp); floor.transform.position = origin; floor.transform.localScale = Vector3.one * 0.6f;
            var rr = hero.GetComponentsInChildren<Renderer>();
            Bounds b = rr[0].bounds; foreach (var r in rr) b.Encapsulate(r.bounds);
            log.AppendLine($"rest bounds {b.center - origin:F3} size {b.size:F3}");
            foreach (var smr in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                log.AppendLine($"smr {smr.name} root {(smr.rootBone ? AnimationUtility.CalculateTransformPath(smr.rootBone, hero.transform) : "none")} bones {smr.bones.Length} first {(smr.bones.Length > 0 && smr.bones[0] ? AnimationUtility.CalculateTransformPath(smr.bones[0], hero.transform) : "null")} inHero {smr.bones.All(x => x && x.IsChildOf(hero.transform))} updateOffscreen {smr.updateWhenOffscreen}");
            foreach (var c in hero.GetComponentsInChildren<Component>(true).Where(c => !(c is Transform) && !(c is Renderer) && !(c is MeshFilter)))
                log.AppendLine("component " + c.GetType().Name + " on " + c.name);
            var allT = hero.GetComponentsInChildren<Transform>(true);
            Transform hand = allT.FirstOrDefault(x => x.name == "L_Hand"), foot = allT.FirstOrDefault(x => x.name == "L_Foot");
            foreach (var name in new[] { "Walk", "Run", "Idle" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "Hero_" + name + ".anim");
                if (!clip) { log.AppendLine("missing " + name); continue; }
                log.AppendLine($"{name}: length {clip.length:F2}s bindings {AnimationUtility.GetCurveBindings(clip).Length} path0 {AnimationUtility.GetCurveBindings(clip).FirstOrDefault().path}");
                for (int i = 0; i < 4; i++)
                {
                    float t = clip.length * i / 4f;
                    clip.SampleAnimation(hero, t);
                    Bounds pb = rr[0].bounds; foreach (var r in rr) pb.Encapsulate(r.bounds);
                    var bm = new Mesh(); var sm0 = hero.GetComponentsInChildren<SkinnedMeshRenderer>()[0]; sm0.BakeMesh(bm, true);
                    float lowY = bm.vertices.Min(v => sm0.transform.TransformPoint(v).y) - origin.y; UnityEngine.Object.DestroyImmediate(bm);
                    log.AppendLine($"  t={t:F2} hand {(hand ? hand.position - origin : Vector3.zero):F2} foot {(foot ? foot.position - origin : Vector3.zero):F2} mesh low {lowY:F3}");
                    log.AppendLine($"  t={t:F2} bounds min.y {pb.min.y - origin.y:F3} max.y {pb.max.y - origin.y:F3} center {pb.center - origin:F2}");
                    Vector3 c = origin + Vector3.up * 0.95f;
                    Shot(c + hero.transform.right * 4.2f, c, $"anim_{name}_{i}_side.png", 30);
                    Shot(c + hero.transform.forward * 4.2f, c, $"anim_{name}_{i}_front.png", 30);
                }
            }
        }
        catch (Exception e) { log.AppendLine("ERROR " + e); Debug.LogException(e); }
        finally { EditorSceneManager.CloseScene(temp, true); }
        File.WriteAllText("HeroSetup/anim_review.txt", log.ToString());
    }

    static void SceneManager_Move(GameObject g, UnityEngine.SceneManagement.Scene s) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, s);

    static void Shot(Vector3 from, Vector3 at, string file, float fov)
    {
        var go = new GameObject("__AnimCam");
        try
        {
            var cam = go.AddComponent<Camera>(); cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 50;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.22f, 0.22f, 0.24f);
            go.transform.position = from; go.transform.LookAt(at);
            HeroSetup.Shot(cam, file);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
