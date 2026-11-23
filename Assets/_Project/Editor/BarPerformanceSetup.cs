using System.Collections.Generic;
using System.Linq;
using CarXCase.Hero;
using CarXCase.Precinct;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// Sets up dialogue acting in 02_Bar: rigs Katya's seated mesh (bones, skin weights, mouth shapes),
/// gives Dmitry and Alexey procedural lips, conversation acting and the IK pass. Safe to run again.
public static class BarPerformanceSetup
{
    const string KatyaName = "Katya at the far end of the counter";
    const string KatyaSource = "Assets/_Project/Arts/Bar/Katya/Katya2_Seated.asset";      // katya2.fbx seated in Blender (Source~/katya2_seated_rig.blend)
    const string KatyaSkinned = "Assets/_Project/Arts/Bar/Katya/Katya2_Seated_Skinned.asset";
    const float SeatBack = -.02f;   // mesh-space z of the stool centre: she sits toward the back of the cushion
    const string KatyaMaterial = "Assets/_Project/Arts/Bar/Katya/Katya2.mat";
    const string DmitryController = "Assets/_Project/Arts/Heroes/Dmitry/Dmitry_Bar.controller";

    // Joints of the seated mesh, mesh space (the mesh faces -Z, her left arm is on +X).
    static readonly Vector3 HipsP = new Vector3(0f, .90f, 0f);
    static readonly Vector3 SpineP = new Vector3(0f, 1.03f, 0f);
    static readonly Vector3 ChestP = new Vector3(0f, 1.18f, 0f);
    static readonly Vector3 NeckP = new Vector3(0f, 1.36f, 0f);
    static readonly Vector3 HeadP = new Vector3(0f, 1.43f, 0f);
    static readonly Vector3 ClavicleP = new Vector3(.03f, 1.345f, 0f);
    static readonly Vector3 ShoulderP = new Vector3(.148f, 1.33f, .01f);
    static readonly Vector3 ElbowP = new Vector3(.243f, 1.092f, .02f);
    static readonly Vector3 TwistP = new Vector3(.282f, .99f, .01f);
    static readonly Vector3 WristP = new Vector3(.32f, .89f, 0f);
    static readonly Vector3 TipP = new Vector3(.375f, .745f, -.01f);

    [MenuItem("Tools/Bar/Set Up Character Performances")]
    public static void SetUpAll()
    {
        var scene = EditorSceneManager.GetActiveScene();
        SetUpKatya();
        SetUpDmitry();
        SetUpAlexey();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[BarPerformanceSetup] Done. Save the scene to keep the changes.");
    }

    static T GetOrAdd<T>(Transform t) where T : Component
    {
        var c = t.GetComponent<T>();
        return c ? c : Undo.AddComponent<T>(t.gameObject);
    }

    static Transform Find(string name) =>
        EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);

    static float Smooth(float from, float to, float x)
    {
        float t = Mathf.Clamp01((x - from) / (to - from));
        return t * t * (3f - 2f * t);
    }

    static Vector3 Mirror(Vector3 p, float side) => new Vector3(p.x * side, p.y, p.z);

    // ====================================================================== Katya

    enum B { Hips, Spine, Chest, Neck, Head, LClav, LUpper, LLower, LTwist, LHand, RClav, RUpper, RLower, RTwist, RHand, Count }

    public static void SetUpKatya()
    {
        var root = Find(KatyaName);
        if (!root) { Debug.LogError("[BarPerformanceSetup] Katya not found"); return; }
        var source = AssetDatabase.LoadAssetAtPath<Mesh>(KatyaSource);
        var material = AssetDatabase.LoadAssetAtPath<Material>(KatyaMaterial);

        // first run: the static mesh is still on the root
        var oldRenderer = root.GetComponent<MeshRenderer>();
        var shadowMode = ShadowCastingMode.On;
        bool receive = true;
        if (oldRenderer)
        {
            shadowMode = oldRenderer.shadowCastingMode;
            receive = oldRenderer.receiveShadows;
            Undo.DestroyObjectImmediate(oldRenderer);
            var filter = root.GetComponent<MeshFilter>();
            if (filter) Undo.DestroyObjectImmediate(filter);
        }
        {   // seat the pelvis on the stool cushion
            var stool = Find("BarStool_07");
            var vertices = source.vertices;
            float seatBottom = vertices.Where(p => Mathf.Abs(p.x) < .12f && p.z > -.16f && p.z < .08f && p.y < .9f).Min(p => p.y);
            if (stool)
            {
                var seat = stool.GetComponentInChildren<Renderer>().bounds;
                // pelvis a little behind the cushion centre, so the knees and the long skirt clear the counter panel
                var pelvis = new Vector3(0f, 0f, SeatBack);
                var centre = seat.center - root.rotation * pelvis;
                Undo.RecordObject(root, "Seat Katya");
                root.position = new Vector3(centre.x, seat.max.y - .006f - seatBottom, centre.z);
            }
        }
        GameObjectUtility.SetStaticEditorFlags(root.gameObject, 0);
        foreach (var name in new[] { "Katya_Rig", "Katya_Body", "Katya idle look" })
        {
            var old = root.Find(name);
            if (old) Undo.DestroyObjectImmediate(old.gameObject);
        }

        // ---- skeleton (rest = the current seated mesh pose, every bone aligned with the root)
        var bones = new Transform[(int)B.Count];
        var rig = new GameObject("Katya_Rig").transform;
        Undo.RegisterCreatedObjectUndo(rig.gameObject, "Katya rig");
        rig.SetParent(root, false);
        Transform Bone(B id, string name, Transform parent, Vector3 meshPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = root.TransformPoint(meshPos);
            t.rotation = root.rotation;
            bones[(int)id] = t;
            return t;
        }
        var hips = Bone(B.Hips, "Hips", rig, HipsP);
        var spine = Bone(B.Spine, "Spine", hips, SpineP);
        var chest = Bone(B.Chest, "Chest", spine, ChestP);
        var neck = Bone(B.Neck, "Neck", chest, NeckP);
        Bone(B.Head, "Head", neck, HeadP);
        foreach (float side in new[] { 1f, -1f })
        {
            string p = side > 0 ? "L_" : "R_";
            var clav = Bone(side > 0 ? B.LClav : B.RClav, p + "Shoulder", chest, Mirror(ClavicleP, side));
            var upper = Bone(side > 0 ? B.LUpper : B.RUpper, p + "UpperArm", clav, Mirror(ShoulderP, side));
            var lower = Bone(side > 0 ? B.LLower : B.RLower, p + "LowerArm", upper, Mirror(ElbowP, side));
            Bone(side > 0 ? B.LTwist : B.RTwist, p + "ForeArmTwist", lower, Mirror(TwistP, side));
            Bone(side > 0 ? B.LHand : B.RHand, p + "Hand", lower, Mirror(WristP, side));
        }

        // ---- skinned mesh
        var mesh = Object.Instantiate(source);
        mesh.name = "Katya2_Seated_Skinned";
        mesh.boneWeights = ComputeWeights(source);
        var body = new GameObject("Katya_Body").transform;
        Undo.RegisterCreatedObjectUndo(body.gameObject, "Katya body");
        body.SetParent(root, false);
        mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * body.localToWorldMatrix).ToArray();
        var anchors = FaceVisemes.Anchors.Default;
        anchors.mouth = new Vector3(0f, 1.469f, -.106f);
        anchors.forward = Vector3.back;
        anchors.up = Vector3.up;
        anchors.halfWidth = .024f;
        anchors.wide = .0036f;
        anchors.round = .0042f;
        anchors.protrude = .003f;
        anchors.buildJaw = true;
        anchors.jawPivot = new Vector3(0f, 1.495f, -.01f);
        anchors.jawAngle = 5f;
        anchors.chinDepth = .043f;
        FaceVisemes.AddShapes(mesh, anchors);
        mesh.RecalculateBounds();
        if (AssetDatabase.LoadAssetAtPath<Mesh>(KatyaSkinned)) AssetDatabase.DeleteAsset(KatyaSkinned);
        AssetDatabase.CreateAsset(mesh, KatyaSkinned);

        var smr = body.gameObject.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.sharedMaterial = material;
        smr.bones = bones;
        smr.rootBone = hips;
        smr.shadowCastingMode = shadowMode;
        smr.receiveShadows = receive;
        smr.quality = SkinQuality.Bone4;
        var center = hips.InverseTransformPoint(body.TransformPoint(mesh.bounds.center));
        smr.localBounds = new Bounds(center, mesh.bounds.size + Vector3.one * .4f);

        // ---- face shapes (already baked into the asset)
        var visemes = GetOrAdd<FaceVisemes>(root);
        visemes.body = smr;
        visemes.anchors = anchors;
        visemes.jawShape = "";
        visemes.maxJaw = 1f;

        // ---- acting
        var actor = GetOrAdd<BarWitnessActor>(root);
        actor.hips = hips;
        actor.spine = spine;
        actor.chest = chest;
        actor.neck = neck;
        actor.head = bones[(int)B.Head];
        actor.visemes = visemes;
        SetUpArm(actor.left = new BarWitnessActor.Arm(), bones, 1f, source);
        SetUpArm(actor.right = new BarWitnessActor.Arm(), bones, -1f, source);
        actor.idleLook = FindDrink(root);

        var witness = Object.FindAnyObjectByType<BarWitness>(FindObjectsInactive.Include);
        if (witness)
        {
            Undo.RecordObject(witness, "Katya actor");
            witness.witness = root;
            witness.actor = actor;
            EditorUtility.SetDirty(witness);
        }
        EditorUtility.SetDirty(actor);
        EditorUtility.SetDirty(visemes);
    }

    static void SetUpArm(BarWitnessActor.Arm arm, Transform[] bones, float side, Mesh source)
    {
        bool l = side > 0;
        arm.upper = bones[(int)(l ? B.LUpper : B.RUpper)];
        arm.lower = bones[(int)(l ? B.LLower : B.RLower)];
        arm.twist = bones[(int)(l ? B.LTwist : B.RTwist)];
        arm.hand = bones[(int)(l ? B.LHand : B.RHand)];
        arm.restFinger = (Mirror(TipP, side) - Mirror(WristP, side)).normalized;
        arm.restPalm = new Vector3(-side, 0f, 0f);

        // hands rest on the tops of the thighs, fingers forward and a little inward
        float wx = .08f * side, wz = -.2f, tx = .055f * side, tz = -.36f;
        var wrist = new Vector3(wx, LapTop(source, wx, wz) + .023f, wz);
        var tip = new Vector3(tx, LapTop(source, tx, tz) + .012f, tz);
        arm.wristTarget = wrist;
        arm.fingerDir = (tip - wrist).normalized;
        arm.palmNormal = Vector3.ProjectOnPlane(new Vector3(-.22f * side, -1f, 0f), arm.fingerDir).normalized;
        arm.pole = Mirror(new Vector3(.36f, 1.12f, .24f), side);
    }

    /// Height of the dress over the thighs at (x, z): a ray straight down against the mesh triangles.
    static float LapTop(Mesh mesh, float x, float z)
    {
        var v = mesh.vertices;
        var tris = mesh.triangles;
        float best = float.NegativeInfinity;
        var o = new Vector2(x, z);
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            if (a.y > 1.05f || a.y < .6f || Mathf.Abs(a.x) > .16f) continue;
            // barycentric test in the XZ plane, then interpolate the height
            Vector2 pa = new Vector2(a.x, a.z), pb = new Vector2(b.x, b.z), pc = new Vector2(c.x, c.z);
            float den = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
            if (Mathf.Abs(den) < 1e-9f) continue;
            float l1 = ((pb.y - pc.y) * (o.x - pc.x) + (pc.x - pb.x) * (o.y - pc.y)) / den;
            float l2 = ((pc.y - pa.y) * (o.x - pc.x) + (pa.x - pc.x) * (o.y - pc.y)) / den;
            float l3 = 1f - l1 - l2;
            if (l1 < 0f || l2 < 0f || l3 < 0f) continue;
            best = Mathf.Max(best, l1 * a.y + l2 * b.y + l3 * c.y);
        }
        return float.IsNegativeInfinity(best) ? .95f : best;
    }

    static Transform FindDrink(Transform root)
    {
        Vector3 front = root.TransformPoint(new Vector3(0f, 1.12f, -.5f));
        Transform best = null;
        float bestD = .7f;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            string n = r.name.ToLowerInvariant();
            if (!(n.Contains("tumbler") || n.Contains("glass"))) continue;
            var c = r.bounds.center;
            if (c.y < 1.02f || c.y > 1.35f) continue;
            float d = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(front.x, front.z));
            if (d < bestD) { bestD = d; best = r.transform; }
        }
        var look = new GameObject("Katya idle look").transform;
        Undo.RegisterCreatedObjectUndo(look.gameObject, "Katya look");
        look.SetParent(root, false);
        var ahead = new Vector3(.02f, 1.3f, -.6f);
        if (best)
        {
            // mesh pivots sit at the scene origin: use the visible centre of the glass
            var drink = root.InverseTransformPoint(best.GetComponent<Renderer>().bounds.center);
            ahead = Vector3.Lerp(ahead, new Vector3(drink.x, ahead.y, drink.z), .35f);
        }
        look.localPosition = ahead;
        return look;
    }

    static BoneWeight[] ComputeWeights(Mesh mesh)
    {
        var v = mesh.vertices;
        var tris = mesh.triangles;
        int n = v.Length;

        // islands (welded by position): the body is the largest; hair, eyes and shoes are separate
        var parent = Enumerable.Range(0, n).ToArray();
        int FindSet(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        void Union(int a, int b) { a = FindSet(a); b = FindSet(b); if (a != b) parent[b] = a; }
        for (int i = 0; i < tris.Length; i += 3) { Union(tris[i], tris[i + 1]); Union(tris[i], tris[i + 2]); }
        foreach (var g in Enumerable.Range(0, n).GroupBy(i => new Vector3Int(Mathf.RoundToInt(v[i].x * 20000), Mathf.RoundToInt(v[i].y * 20000), Mathf.RoundToInt(v[i].z * 20000))))
        {
            int first = g.First();
            foreach (var i in g) Union(first, i);
        }
        int body = Enumerable.Range(0, n).GroupBy(FindSet).OrderByDescending(g => g.Count()).First().Key;

        var result = new BoneWeight[n];
        var w = new float[(int)B.Count];
        for (int i = 0; i < n; i++)
        {
            System.Array.Clear(w, 0, w.Length);
            var p = v[i];
            bool main = FindSet(i) == body;
            Chain(p, w, 1f);

            if (main)
            {   // collarbones carry a little of the shoulder line
                float clav = Smooth(.06f, .12f, Mathf.Abs(p.x)) * Smooth(1.24f, 1.31f, p.y) * Smooth(1.42f, 1.37f, p.y) * .6f;
                if (clav > 0f)
                {
                    Scale(w, 1f - clav);
                    w[(int)(p.x > 0 ? B.LClav : B.RClav)] += clav;
                }
                // arms
                float side = p.x >= 0 ? 1f : -1f;
                float mask = Smooth(.108f, .13f, Mathf.Abs(p.x));
                if (mask > 0f)
                {
                    var arm = new float[(int)B.Count];
                    float dist = Arm(p, side, arm);
                    // below the hips line and outside the thighs only hands live (the thumb sticks out forward)
                    bool handZone = Mathf.Abs(p.x) > .21f && p.y < .98f;
                    mask *= handZone ? 1f : Smooth(.07f, .045f, dist);
                    if (mask > 0f)
                    {
                        Scale(w, 1f - mask);
                        for (int b = 0; b < w.Length; b++) w[b] += arm[b] * mask;
                    }
                }
            }
            result[i] = Pack(w);
        }
        return result;
    }

    static void Scale(float[] w, float k) { for (int b = 0; b < w.Length; b++) w[b] *= k; }

    /// Spine chain by height; thighs and legs stay on the pelvis.
    static void Chain(Vector3 p, float[] w, float amount)
    {
        float y = p.y;
        void Mix(B a, B b, float t) { w[(int)a] += (1f - t) * amount; w[(int)b] += t * amount; }
        if (y < .95f) w[(int)B.Hips] += amount;
        else if (y < 1.06f) Mix(B.Hips, B.Spine, Smooth(.95f, 1.06f, y));
        else if (y < 1.12f) w[(int)B.Spine] += amount;
        else if (y < 1.22f) Mix(B.Spine, B.Chest, Smooth(1.12f, 1.22f, y));
        else if (y < 1.30f) w[(int)B.Chest] += amount;
        else if (y < 1.355f) Mix(B.Chest, B.Neck, Smooth(1.30f, 1.355f, y));
        else if (y < 1.44f) Mix(B.Neck, B.Head, Smooth(1.39f, 1.44f, y));
        else w[(int)B.Head] += amount;
        float legs = Smooth(-.08f, -.14f, p.z) * Smooth(1.03f, .97f, y);
        if (legs > 0f)
        {
            float keep = 1f - legs;
            for (int b = 0; b < w.Length; b++) w[b] *= keep;
            w[(int)B.Hips] += legs * amount;
        }
    }

    /// Weights along the arm polyline; returns the distance to it.
    static float Arm(Vector3 p, float side, float[] w)
    {
        Vector3 s = Mirror(ShoulderP, side), e = Mirror(ElbowP, side), wr = Mirror(WristP, side), tip = Mirror(TipP, side);
        float Param(Vector3 a, Vector3 b, out float d)
        {
            Vector3 ab = b - a;
            float t = Vector3.Dot(p - a, ab) / ab.sqrMagnitude;
            d = Vector3.Distance(p, a + ab * Mathf.Clamp01(t));
            return t;
        }
        float t0 = Param(s, e, out float d0), t1 = Param(e, wr, out float d1), t2 = Param(wr, tip, out float d2);
        bool l = side > 0;
        int clav = (int)(l ? B.LClav : B.RClav), up = (int)(l ? B.LUpper : B.RUpper), lo = (int)(l ? B.LLower : B.RLower),
            tw = (int)(l ? B.LTwist : B.RTwist), hand = (int)(l ? B.LHand : B.RHand);
        if (d0 <= d1 && d0 <= d2)
        {
            float shoulder = Smooth(.12f, -.05f, t0) * .55f;
            float elbow = Smooth(.85f, 1.12f, t0) * .5f;
            w[clav] += shoulder;
            w[lo] += elbow;
            w[up] += 1f - shoulder - elbow;
            return d0;
        }
        if (d1 <= d2)
        {
            float elbow = Smooth(.15f, -.12f, t1) * .5f;
            float wrist = Smooth(.9f, 1.1f, t1) * .5f;
            float twist = Smooth(.15f, .9f, t1);
            float rest = 1f - elbow - wrist;
            w[up] += elbow;
            w[hand] += wrist;
            w[tw] += rest * twist;
            w[lo] += rest * (1f - twist);
            return d1;
        }
        float back = Smooth(.12f, -.05f, t2) * .5f;
        w[tw] += back;
        w[hand] += 1f - back;
        return d2;
    }

    static BoneWeight Pack(float[] w)
    {
        var top = Enumerable.Range(0, w.Length).Where(i => w[i] > 1e-4f).OrderByDescending(i => w[i]).Take(4).ToArray();
        float sum = top.Sum(i => w[i]);
        var bw = new BoneWeight();
        if (top.Length == 0 || sum <= 0f) { bw.boneIndex0 = 0; bw.weight0 = 1f; return bw; }
        float W(int k) => k < top.Length ? w[top[k]] / sum : 0f;
        int I(int k) => k < top.Length ? top[k] : 0;
        bw.boneIndex0 = I(0); bw.weight0 = W(0);
        bw.boneIndex1 = I(1); bw.weight1 = W(1);
        bw.boneIndex2 = I(2); bw.weight2 = W(2);
        bw.boneIndex3 = I(3); bw.weight3 = W(3);
        return bw;
    }

    // ====================================================================== Dmitry

    public static void SetUpDmitry()
    {
        var humanoid = Find("Dmitry_Humanoid");
        if (!humanoid) { Debug.LogError("[BarPerformanceSetup] Dmitry not found"); return; }
        var smr = humanoid.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == "Dmitry_Body");
        var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(smr.sharedMesh));
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

        // mouth from the JawOpen shape: the lip line is the top of the region it moves
        var mesh = smr.sharedMesh;
        var v = mesh.vertices;
        var delta = new Vector3[mesh.vertexCount];
        int shape = mesh.GetBlendShapeIndex("JawOpen");
        mesh.GetBlendShapeFrameVertices(shape, mesh.GetBlendShapeFrameCount(shape) - 1, delta, null, null);
        float max = delta.Max(d => d.magnitude);
        var moved = Enumerable.Range(0, v.Length).Where(i => delta[i].magnitude > max * .3f && Mathf.Abs(v[i].x) < .00012f).ToArray();
        float lipZ = moved.Max(i => v[i].z);
        float frontY = moved.Where(i => v[i].z > lipZ - .00008f).Min(i => v[i].y);

        var visemes = GetOrAdd<FaceVisemes>(humanoid);
        visemes.body = smr;
        var a = FaceVisemes.Anchors.Default;
        a.mouth = new Vector3(0f, frontY, lipZ + .00020f);
        a.forward = Vector3.down;      // this mesh is Z-up, facing -Y, in centimetres-as-metres/100
        a.up = Vector3.forward;
        a.unitsPerMetre = .01f;
        a.halfWidth = .025f;
        a.wide = .0045f;
        a.round = .0052f;
        a.protrude = .0038f;
        visemes.anchors = a;
        visemes.mouthLift = .01f;
        visemes.jawShape = "JawOpen";
        visemes.maxJaw = .68f;
        EditorUtility.SetDirty(visemes);

        var face = humanoid.GetComponent<HeroFace>();
        var so = new SerializedObject(face);
        so.FindProperty("visemes").objectReferenceValue = visemes;
        so.FindProperty("armGestures").boolValue = false;
        so.FindProperty("intensity").floatValue = .8f;
        so.FindProperty("lipAmount").floatValue = 1f;
        so.ApplyModifiedProperties();

        var acting = GetOrAdd<ConversationActing>(humanoid);
        acting.face = face;
        EditorUtility.SetDirty(acting);

        var performance = humanoid.GetComponentInParent<BarDmitryPerformance>(true);
        if (performance)
        {
            var ps = new SerializedObject(performance);
            ps.FindProperty("acting").objectReferenceValue = acting;
            ps.ApplyModifiedProperties();
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DmitryController);
        if (controller)
        {
            var layers = controller.layers;
            if (!layers[0].iKPass)
            {
                layers[0].iKPass = true;
                controller.layers = layers;
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);
            }
        }
    }

    // ====================================================================== Alexey

    public static void SetUpAlexey()
    {
        var alexey = Find("Alexey");
        if (!alexey) { Debug.LogError("[BarPerformanceSetup] Alexey not found"); return; }
        var smr = alexey.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == "Body");
        var visemes = GetOrAdd<FaceVisemes>(alexey);
        visemes.body = smr;
        var a = FaceVisemes.Anchors.Default;
        a.mouth = new Vector3(0f, 1.693f, .1075f);
        a.forward = Vector3.forward;
        a.up = Vector3.up;
        a.halfWidth = .026f;
        a.wide = .0046f;
        a.round = .0054f;
        a.protrude = .004f;
        visemes.anchors = a;
        visemes.jawShape = "";
        EditorUtility.SetDirty(visemes);

        var face = alexey.GetComponent<HeroFace>();
        var so = new SerializedObject(face);
        so.FindProperty("visemes").objectReferenceValue = visemes;
        // the welded lips get a mouth interior (MouthInterior), which hides the stretch of a wider jaw drop
        so.FindProperty("maxJawAngle").floatValue = 4.4f;
        // the procedural beat gesture of the right arm reads as unnatural in dialogue: the hands stay with the animation
        so.FindProperty("armGestures").boolValue = false;
        so.FindProperty("lipAmount").floatValue = 1f;
        so.ApplyModifiedProperties();

        const string mouthMaterialPath = "Assets/_Project/Arts/Bar/Hero_MouthInterior.mat";
        var mouthMaterial = AssetDatabase.LoadAssetAtPath<Material>(mouthMaterialPath);
        if (!mouthMaterial)
        {
            mouthMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Hero_MouthInterior" };
            AssetDatabase.CreateAsset(mouthMaterial, mouthMaterialPath);
        }
        mouthMaterial.SetColor("_BaseColor", Color.white);   // vertex colours carry teeth / throat
        EditorUtility.SetDirty(mouthMaterial);
        var mouth = GetOrAdd<MouthInterior>(alexey);
        mouth.face = face;
        mouth.visemes = visemes;
        mouth.material = mouthMaterial;
        EditorUtility.SetDirty(mouth);
    }
}
