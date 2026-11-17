using System.Collections.Generic;
using System.Linq;
using CarXCase.Precinct;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// Replaces the precinct greybox props with the Arts/Precint models (plus the generated doors,
/// glass partition, plants and wall details) and gives every NPC a rigged body driven by the
/// Mixamo clips. Greybox colliders stay in place for gameplay. Safe to run again.
public static class PrecinctDressing
{
    const string ScenePath = "Assets/_Project/Scenes/02_Precinct.unity";
    const string Props = "Assets/_Project/Arts/Precint/";
    const string Generated = Props + "Generated/";
    const string Npc = "Assets/_Project/Arts/Heroes/police npc/";
    const string Mats = "Assets/_Project/ArtDirection/PrecinctNoir/";
    const float Ceiling = 3.2f;
    const int ProbeLayer = 31;

    [MenuItem("Tools/Precinct/Dress precinct and animate NPCs")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        var greybox = GameObject.Find("Precinct_Greybox").transform;
        var old = greybox.Find("Precinct_Dressing");
        if (old) Object.DestroyImmediate(old.gameObject);
        foreach (var door in new[] { "FrontDoor_L_Pivot", "FrontDoor_R_Pivot", "OfficeDoor_Pivot", "CaptainDoor_Pivot" })
        {
            var pivot = greybox.Find("Architecture/" + door);
            var model = pivot ? pivot.Find("Door_Model") : null;
            if (model) Object.DestroyImmediate(model.gameObject);
        }
        var root = new GameObject("Precinct_Dressing").transform;
        root.SetParent(greybox, false);

        LoadMaterials();
        DressProps(greybox, root);
        DressArchitecture(greybox, root);
        DressWindows(greybox, root);
        Trim(greybox, root);
        Brighten(greybox.Find("Lighting"));
        ExtraFixtures(root);
        WorldUVOnCubes(root);
        AnimateNpcs(greybox.Find("NPCs"));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PrecinctDressing] Precinct dressed, NPCs animated.");
    }

    // ------------------------------------------------------------------ materials

    static Material Art(string name) => AssetDatabase.LoadAssetAtPath<Material>(Mats + name + ".mat");

    static Material Paint(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null, bool transparent = false)
    {
        string folder = Mats + "Props";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Mats.TrimEnd('/'), "Props");
        string path = folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material oak, walnut, olive, steel, ivory, bakelite, enamel, housing, flagCloth, red, binLiner;
    static Material screenOn, screenDim, screenOff;
    static Dictionary<string, Material> slots;

    static void LoadMaterials()
    {
        oak = Art("Worn_Oak_Laminate");
        walnut = Art("Walnut_Counter_Panels");
        olive = Art("Olive_Powder_Coated_Steel");
        steel = Art("Grey_Powder_Coated_Steel");
        ivory = Art("Aged_Ivory_ABS");
        enamel = Art("Green_Desk_Lamp_Enamel");
        housing = Art("Fluorescent_Steel_Housing");
        bakelite = Paint("Black_Bakelite_Plastic", new Color(.055f, .052f, .05f), .45f);
        flagCloth = Paint("Faded_Navy_Flag_Cloth", new Color(.13f, .16f, .24f), .18f);
        red = Paint("Fire_Red_Enamel", new Color(.42f, .06f, .05f), .55f, .1f);
        binLiner = Paint("Black_Bin_Liner", new Color(.03f, .03f, .035f), .62f);
        screenOn = Paint("CRT_Screen_On", new Color(.05f, .09f, .08f), .9f, 0f, new Color(.26f, .52f, .46f) * 1.8f);
        screenDim = Paint("CRT_Screen_Dim", new Color(.04f, .06f, .07f), .9f, 0f, new Color(.18f, .27f, .38f) * 1.1f);
        screenOff = Art("Smoked_CRT_Glass") ?? Paint("CRT_Screen_Off", new Color(.03f, .035f, .035f), .92f);

        slots = new Dictionary<string, Material>
        {
            ["Wood"] = walnut,
            ["Glass"] = Paint("Clear_Partition_Glass", new Color(.62f, .7f, .7f, .16f), .96f, 0f, null, true),
            ["Metal"] = steel,
            ["Paint"] = Art("Institutional_Sage_Paint") ?? steel,
            ["Terracotta"] = Paint("Terracotta_Pot", new Color(.42f, .22f, .14f), .25f),
            ["Soil"] = Paint("Potting_Soil", new Color(.2f, .14f, .09f), .08f),
            ["Bark"] = Paint("Ficus_Bark", new Color(.2f, .15f, .1f), .2f),
            ["Leaf"] = Paint("Dusty_Leaf_Green", new Color(.12f, .2f, .1f), .35f),
            ["Cork"] = Art("Pressed_Cork") ?? oak,
            ["Paper"] = Art("Archive_Paper") ?? ivory,
            ["Photo"] = Art("Matte_Evidence_Photograph") ?? ivory,
            ["Pin"] = Art("Oxide_Red_Pushpin") ?? red,
            ["Black"] = bakelite,
            ["Folder"] = Paint("Manila_Folder", new Color(.62f, .5f, .3f), .2f),
            ["Ceramic"] = Paint("Glazed_Mug_Ceramic", new Color(.66f, .64f, .58f), .7f),
            ["Coffee"] = Paint("Black_Coffee", new Color(.06f, .03f, .015f), .9f),
            ["Pot"] = Paint("Terracotta_Pot", new Color(.42f, .22f, .14f), .25f),
            ["LeafDark"] = Paint("Leaf_Deep_Green", new Color(.06f, .13f, .055f), .45f),
            ["LeafLight"] = Paint("Leaf_Fresh_Green", new Color(.15f, .26f, .09f), .4f),
            ["Frame"] = Paint("Window_Frame_Enamel", new Color(.5f, .52f, .48f), .4f, .15f),
            ["WindowGlass"] = Paint("Dusk_Window_Glass", new Color(.05f, .07f, .1f), .95f, 0f, new Color(.32f, .42f, .6f) * 1.3f),
            ["Blinds"] = Paint("Venetian_Blind_Slats", new Color(.66f, .62f, .53f), .35f),
            ["Sill"] = walnut,
        };
    }

    // ------------------------------------------------------------------ placement

    enum Fit { Height, Width, Depth }

    struct Placed
    {
        public Transform pivot, body;
        public Vector3 size;
        public float Top => pivot.position.y + size.y;
    }

    /// Places a model with its footprint centred on `position`, resting on it, turned by `yaw`
    /// (models face +Z) and uniformly scaled so the chosen dimension equals `size`.
    /// Without a material the model keeps its slot materials, remapped by slot name.
    static Placed Place(Transform parent, string asset, Vector3 position, float yaw, Fit fit, float size,
        Material material, string name = null, Mesh mesh = null, float stretchX = 1f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset.EndsWith(".fbx") ? asset : Props + asset + ".fbx");
        var pivot = new GameObject(name ?? prefab.name).transform;
        pivot.SetParent(parent, false);
        var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = prefab.transform.localRotation;
        body.transform.localScale = Vector3.one;
        if (mesh) foreach (var filter in body.GetComponentsInChildren<MeshFilter>()) filter.sharedMesh = mesh;
        var renderers = body.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        float dimension = fit == Fit.Height ? bounds.size.y : fit == Fit.Width ? bounds.size.x : bounds.size.z;
        float scale = size / Mathf.Max(dimension, 1e-4f);
        body.transform.localScale = new Vector3(scale * stretchX, scale, scale);
        body.transform.localPosition = new Vector3(-bounds.center.x * stretchX, -bounds.min.y, -bounds.center.z) * scale;
        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = material ? material : mats[i] && slots.TryGetValue(mats[i].name, out var m) && m ? m : mats[i];
            r.sharedMaterials = mats;
            r.renderingLayerMask |= 1;
        }
        pivot.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var size3 = new Vector3(bounds.size.x * scale * stretchX, bounds.size.y * scale, bounds.size.z * scale);
        // Everything standing on the floor blocks the player; desk clutter and wall pieces don't.
        if (position.y < .05f && size3.y > .35f)
        {
            var box = pivot.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, size3.y * .5f, 0);
            box.size = size3;
        }
        return new Placed { pivot = pivot, body = body.transform, size = size3 };
    }

    static Placed Gen(Transform parent, string model, Vector3 position, float yaw, Fit fit, float size, string name = null, float stretchX = 1f) =>
        Place(parent, Generated + model + ".fbx", position, yaw, fit, size, null, name ?? model, Prepared(model), stretchX);

    /// The Blender-made models come without UVs, so textured materials (walnut, plaster) showed
    /// as a flat fill. This copy gets box-projected UVs in metres; plant pots get their soil
    /// raised just under the rim so it reads from above.
    static UnityEngine.Mesh Prepared(string model)
    {
        string folder = Generated + "Prepared";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Generated.TrimEnd('/'), "Prepared");
        string path = folder + "/" + model + "_v2.asset";
        var cached = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (cached) return cached;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + model + ".fbx");
        var renderer = prefab.GetComponentInChildren<MeshRenderer>();
        var source = renderer.GetComponent<MeshFilter>().sharedMesh;
        var mesh = Object.Instantiate(source);
        mesh.name = model + "_Prepared";
        var vertices = mesh.vertices;

        // Soil: lift the soil cap to 1.2 cm under the pot's rim and let it reach the pot wall.
        var names = renderer.sharedMaterials.Select(m => m ? m.name : "").ToArray();
        int soil = System.Array.FindIndex(names, n => n.Contains("Soil"));
        int pot = System.Array.FindIndex(names, n => n.Contains("Pot") || n.Contains("Terracotta"));
        if (soil >= 0 && pot >= 0 && soil < mesh.subMeshCount && pot < mesh.subMeshCount)
        {
            var soilVerts = new HashSet<int>(mesh.GetTriangles(soil));
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (i != soil) foreach (var v in mesh.GetTriangles(i)) soilVerts.Remove(v);
            float rim = mesh.GetTriangles(pot).Max(v => vertices[v].z);
            if (soilVerts.Count > 0)
            {
                float top = soilVerts.Max(v => vertices[v].z);
                var centre = new Vector2(soilVerts.Average(v => vertices[v].x), soilVerts.Average(v => vertices[v].y));
                float lift = Mathf.Max(0f, rim - .012f - top);
                foreach (var v in soilVerts)
                {
                    var p = vertices[v];
                    var xy = centre + (new Vector2(p.x, p.y) - centre) * 1.04f;
                    vertices[v] = new Vector3(xy.x, xy.y, p.z + lift);
                }
                mesh.vertices = vertices;
            }
        }
        mesh.uv = BoxUV(vertices, mesh.normals.Length == vertices.Length ? mesh.normals : null, .6f);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Vector2[] BoxUV(Vector3[] vertices, Vector3[] normals, float tile)
    {
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i] / tile;
            var n = normals != null ? normals[i] : Vector3.up;
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            uv[i] = ax >= ay && ax >= az ? new Vector2(p.y, p.z) : ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
        }
        return uv;
    }

    /// Cube strips (skirting, casings, wall pieces) keep their texture at real-world scale
    /// instead of one stretched tile per strip.
    static void WorldUVOnCubes(Transform root)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (!mesh || mesh.name != "Cube") continue;
            var scale = filter.transform.lossyScale;
            var copy = Object.Instantiate(mesh);
            copy.name = "Cube_WorldUV";
            var vertices = copy.vertices;
            var scaled = vertices.Select(v => Vector3.Scale(v, scale)).ToArray();
            copy.uv = BoxUV(scaled, copy.normals, .8f);
            filter.sharedMesh = copy;
        }
    }

    static void Hide(Transform t)
    {
        if (!t) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
    }

    static void Hide(Transform scope, params string[] names)
    {
        foreach (var n in names) Hide(scope.Find(n));
    }

    // ---- probing the real model surfaces (baked props, curved CRT glass)

    static readonly List<(GameObject go, int layer, MeshCollider collider)> probes = new List<(GameObject, int, MeshCollider)>();

    static void Probe(Placed placed)
    {
        foreach (var filter in placed.body.GetComponentsInChildren<MeshFilter>())
        {
            var go = filter.gameObject;
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            probes.Add((go, go.layer, collider));
            go.layer = ProbeLayer;
        }
        Physics.SyncTransforms();
    }

    static void ClearProbes()
    {
        foreach (var (go, layer, collider) in probes)
        {
            if (collider) Object.DestroyImmediate(collider);
            if (go) go.layer = layer;
        }
        probes.Clear();
    }

    static float SurfaceAt(Vector3 point, float fallback)
    {
        return Physics.Raycast(new Vector3(point.x, 3f, point.z), Vector3.down, out var hit, 4f, 1 << ProbeLayer, QueryTriggerInteraction.Ignore)
            ? hit.point.y : fallback;
    }

    // ---- desks without the props sculpted into their tops

    static readonly Dictionary<string, (Mesh mesh, float surface)> cleaned = new Dictionary<string, (Mesh, float)>();

    /// Copies a model's mesh without everything resting on its main horizontal surface
    /// (the generator sculpted a phone, a tray and a monitor into the desk top).
    /// Returns the surface height as a share of the model height.
    static (Mesh mesh, float surface) CleanTop(string model)
    {
        if (cleaned.TryGetValue(model, out var known)) return known;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Props + model + ".fbx");
        var filter = prefab.GetComponentInChildren<MeshFilter>();
        var source = filter.sharedMesh;
        var upright = Matrix4x4.Rotate(prefab.transform.localRotation) * (filter.transform == prefab.transform ? Matrix4x4.identity : Matrix4x4.TRS(filter.transform.localPosition, filter.transform.localRotation, filter.transform.localScale));
        var vertices = source.vertices;
        var heights = vertices.Select(v => upright.MultiplyPoint3x4(v).y).ToArray();
        float min = heights.Min(), max = heights.Max(), height = max - min;

        // Largest upward-facing area in the upper half is the work surface.
        var area = new Dictionary<int, float>();
        float bin = height / 200f; // the meshes come in tiny file units
        for (int s = 0; s < source.subMeshCount; s++)
        {
            var tris = source.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = upright.MultiplyPoint3x4(vertices[tris[i]]), b = upright.MultiplyPoint3x4(vertices[tris[i + 1]]), c = upright.MultiplyPoint3x4(vertices[tris[i + 2]]);
                var cross = Vector3.Cross(b - a, c - a);
                float magnitude = cross.magnitude; // tiny: don't use .normalized, it zeroes small vectors
                if (magnitude <= 0f || cross.y / magnitude < .9f) continue;
                float y = (a.y + b.y + c.y) / 3f;
                if (y < min + height * .45f) continue;
                int key = Mathf.RoundToInt(y / bin);
                area.TryGetValue(key, out float sum);
                area[key] = sum + magnitude * .5f;
            }
        }
        float surface = area.Count > 0 ? area.OrderByDescending(p => p.Value).First().Key * bin : max;
        float cut = surface + .02f * height;

        var mesh = Object.Instantiate(source);
        mesh.name = source.name + "_Clean";
        float keptMax = min;
        for (int s = 0; s < source.subMeshCount; s++)
        {
            var tris = source.GetTriangles(s);
            var keep = new List<int>(tris.Length);
            for (int i = 0; i < tris.Length; i += 3)
                if (Mathf.Max(heights[tris[i]], heights[tris[i + 1]], heights[tris[i + 2]]) <= cut)
                {
                    keep.AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
                    keptMax = Mathf.Max(keptMax, heights[tris[i]], heights[tris[i + 1]], heights[tris[i + 2]]);
                }
            mesh.SetTriangles(keep, s);
        }
        mesh.RecalculateBounds();
        Debug.Log($"[PrecinctDressing] {model}: surface at {(surface - min) / height:P0} of the height, kept {mesh.triangles.Length / 3}/{source.triangles.Length / 3} triangles");
        string path = Generated + model + "_Clean.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        // Share of the cleaned model's height (that is what the placed bounds measure).
        // Unreferenced vertices stay in the mesh, so its bounds still span the original height.
        var result = (mesh, Mathf.Clamp01((surface - min) / height));
        cleaned[model] = result;
        return result;
    }

    // ---- CRT screens: an emissive pane on the real glass, a soft light for the lit ones

    static int screenIndex;

    static void Screen(Placed monitor, Transform parent, int mode)
    {
        Probe(monitor);
        var forward = monitor.pivot.forward;
        var center = monitor.pivot.position + Vector3.up * (monitor.size.y * .6f);
        if (!Physics.Raycast(center + forward * monitor.size.z, -forward, out var hit, monitor.size.z * 1.5f, 1 << ProbeLayer, QueryTriggerInteraction.Ignore))
        {
            ClearProbes();
            return;
        }
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.name = "Screen";
        quad.transform.SetParent(monitor.pivot, false);
        quad.transform.SetPositionAndRotation(hit.point + hit.normal * .004f, Quaternion.LookRotation(-hit.normal, Vector3.up));
        quad.transform.localScale = new Vector3(monitor.size.x * .6f, monitor.size.y * .46f, 1f);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mode == 2 ? screenOn : mode == 1 ? screenDim : screenOff;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (mode == 2)
        {
            var glow = new GameObject("Screen_Glow").AddComponent<Light>();
            glow.transform.SetParent(monitor.pivot, false);
            glow.transform.position = hit.point + hit.normal * .32f;
            glow.type = LightType.Point;
            glow.color = mode == 2 ? new Color(.55f, .85f, .78f) : new Color(.55f, .68f, .9f);
            glow.intensity = mode == 2 ? .9f : .55f;
            glow.range = 1.7f;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.Auto;
        }
        ClearProbes();
    }

    // ------------------------------------------------------------------ props

    static void DressProps(Transform greybox, Transform root)
    {
        var props = greybox.Find("Props");
        var office = greybox.Find("Office_Alexey");
        var lighting = greybox.Find("Lighting");
        screenIndex = 0;

        var bullpen = new GameObject("Bullpen_Desks").transform;
        bullpen.SetParent(root, false);
        // Screens: 2 = working, 1 = idle glow, 0 = switched off.
        int[] screens = { 2, 0, 1, 2, 0, 2, 2, 0, 1, 0, 2, 1 };
        for (int i = 0; i < 12; i++)
            DressDesk(props.Find("Desk_" + i), bullpen, lamp: i % 4 == 1 || i == 6, bin: i % 3 == 1, plant: i == 3 || i == 8 || i == 11,
                screen: screens[i], seed: i);
        var offices = new GameObject("Offices").transform;
        offices.SetParent(root, false);
        DressDesk(props.Find("Captain_Desk"), offices, lamp: true, bin: true, plant: false, screen: 1, seed: 20);
        float officeTop = DressDesk(office.Find("Office_Desk"), offices, lamp: false, bin: false, plant: false, screen: 2, seed: 21);

        // Alexey's office.
        Hide(office, "Office_Lamp_Base", "Office_Lamp_Shade", "Office_Lamp_Stem", "CoatRack_Pole", "Coat", "Chair_Visitor",
            "Office_Cabinet_0", "Office_Cabinet_1");
        Place(offices, "desk lamp from alex office", new Vector3(3.95f, officeTop, 22.62f), 25f, Fit.Height, .42f, enamel, "Alexey_Desk_Lamp");
        Place(offices, "hanger", new Vector3(.45f, 0, 18.5f), 20f, Fit.Height, 1.85f, walnut, "Alexey_Coat_Rack");
        Place(offices, "chair for visitors", new Vector3(4.2f, 0, 20.85f), 0f, Fit.Height, .92f, steel, "Alexey_Visitor_Chair");
        Place(offices, "Card Catalog Cabinet", new Vector3(7.7f, 0, 22.3f), -90f, Fit.Height, 1.32f, olive, "Alexey_Cabinet");
        Place(offices, "american flag", new Vector3(7.45f, 0, 23.45f), 225f, Fit.Height, 2.15f, flagCloth, "Alexey_Flag");
        Place(offices, "trash bag", new Vector3(.65f, 0, 23.45f), 40f, Fit.Height, .42f, binLiner, "Alexey_Bin");
        Gen(offices, "Plant_Ficus", new Vector3(7.3f, 0, 20.85f), 30f, Fit.Height, 1.55f, "Alexey_Ficus");
        Gen(offices, "Wall_Clock", new Vector3(7.87f, 2.25f, 20.6f), -90f, Fit.Height, .34f, "Alexey_Clock");
        // Captain's office.
        Place(offices, "chair for visitors", new Vector3(-4.45f, 0, 20.95f), 10f, Fit.Height, .92f, steel, "Captain_Visitor_Chair_L");
        Place(offices, "chair for visitors", new Vector3(-3.5f, 0, 20.95f), -12f, Fit.Height, .92f, steel, "Captain_Visitor_Chair_R");
        Place(offices, "Card Catalog Cabinet", new Vector3(-7.7f, 0, 22.3f), 90f, Fit.Height, 1.32f, olive, "Captain_Cabinet");
        Place(offices, "american flag", new Vector3(-7.45f, 0, 23.45f), 135f, Fit.Height, 2.15f, flagCloth, "Captain_Flag");
        Place(offices, "hanger", new Vector3(-1.4f, 0, 23.4f), -20f, Fit.Height, 1.85f, walnut, "Captain_Coat_Rack");
        Gen(offices, "Plant_Snake", new Vector3(-7.35f, 0, 18.7f), 0f, Fit.Height, 1.0f, "Captain_Plant");
        Gen(offices, "Bulletin_Board", new Vector3(-7.86f, 1.15f, 20.6f), 90f, Fit.Width, 1.3f, "Captain_Board");

        // Filing wall on the west side of the bullpen, with notice boards above.
        var files = new GameObject("Filing").transform;
        files.SetParent(root, false);
        foreach (var row in new[] { 8, 11, 14 })
        {
            for (int k = 0; k < 3; k++) Hide(props.Find($"FileCabinet_W{row}_{k}"));
            float center = row + .5f;
            Place(files, "Card Catalog Cabinet", new Vector3(-7.7f, 0, center - .47f), 90f, Fit.Height, 1.32f, olive, $"Cabinet_W{row}_A");
            Place(files, "Card Catalog Cabinet", new Vector3(-7.7f, 0, center + .47f), 90f, Fit.Height, 1.32f, olive, $"Cabinet_W{row}_B");
            Gen(files, "Paper_Stack", new Vector3(-7.72f, 1.32f, center - .3f), 80f, Fit.Width, .25f, $"Cabinet_Papers_{row}");
        }
        Gen(files, "Bulletin_Board", new Vector3(-7.86f, 1.55f, 9.9f), 90f, Fit.Width, 1.4f, "Bullpen_Board_W1");
        Gen(files, "Bulletin_Board", new Vector3(-7.86f, 1.55f, 13.1f), 90f, Fit.Width, 1.2f, "Bullpen_Board_W2");
        Gen(files, "Plant_Snake", new Vector3(-7.4f, 0, 10.05f), 20f, Fit.Height, 1.0f, "Bullpen_Plant_W1");
        Gen(files, "Plant_Snake", new Vector3(-7.4f, 0, 13.0f), 70f, Fit.Height, .9f, "Bullpen_Plant_W2");

        // Break corner on the east wall.
        var corner = new GameObject("Coffee_Corner").transform;
        corner.SetParent(root, false);
        Hide(props, "WaterCooler", "Coffee_Table", "Coffee_Machine");
        Cooler(corner, new Vector3(7.62f, 0, 8.2f), -90f, "Water_Cooler");
        var coffeeClean = CleanTop("coffie place table");
        var table = Place(corner, "coffie place table", new Vector3(7.45f, 0, 10.5f), -90f, Fit.Height, .9f, oak, "Coffee_Table", coffeeClean.mesh);
        float counter = table.size.y * coffeeClean.surface;
        Place(corner, "coffie maker", new Vector3(7.55f, counter, 10.25f), -90f, Fit.Height, .36f, steel, "Coffee_Maker");
        Gen(corner, "Coffee_Mug", new Vector3(7.4f, counter, 10.72f), 30f, Fit.Height, .1f, "Coffee_Mug_1");
        Gen(corner, "Coffee_Mug", new Vector3(7.5f, counter, 10.86f), 150f, Fit.Height, .1f, "Coffee_Mug_2");
        Place(corner, "trash bag", new Vector3(7.45f, 0, 11.75f), 15f, Fit.Height, .45f, binLiner, "Coffee_Bin");
        Place(corner, "Rustguard Extinguisher", new Vector3(7.75f, 0, 6.65f), -90f, Fit.Height, .62f, red, "Extinguisher_East");
        Gen(corner, "Plant_Ficus", new Vector3(7.3f, 0, 13.6f), 80f, Fit.Height, 1.6f, "Bullpen_Ficus_E");
        Gen(corner, "Bulletin_Board", new Vector3(7.86f, 1.55f, 15.0f), -90f, Fit.Width, 1.3f, "Bullpen_Board_E");

        // Lobby: reception counter, bench, visitor chairs, flag, coat rack, cooler, plants.
        var lobby = new GameObject("Lobby").transform;
        lobby.SetParent(root, false);
        Hide(props, "FrontDesk", "FrontDesk_Top", "FrontDesk_Side", "Bench_Lobby", "Bench_Lobby_Back", "Flag_Pole", "Wanted_Board");
        var receptionClean = CleanTop("reseption desk");
        var reception = Place(lobby, "reseption desk", new Vector3(3.45f, 0, 3.75f), 0f, Fit.Height, 1.12f, walnut, "Reception_Desk", receptionClean.mesh, 1.45f);
        float counterTop = reception.size.y * receptionClean.surface;
        Probe(reception);
        // The counter is L-shaped: the long front bar (z 3.2-3.7) and a short return on the left.
        // Everything goes on the front bar, facing the desk sergeant behind it.
        float monitorY = SurfaceAt(new Vector3(4.45f, 0, 3.42f), counterTop);
        float phoneY = SurfaceAt(new Vector3(2.7f, 0, 3.42f), counterTop);
        float stackY = SurfaceAt(new Vector3(3.6f, 0, 3.45f), counterTop);
        ClearProbes();
        var receptionMonitor = Place(lobby, "computer monitor", new Vector3(4.45f, monitorY, 3.42f), -12f, Fit.Height, .4f, ivory, "Reception_Monitor");
        Screen(receptionMonitor, lobby, 2);
        Place(lobby, "keyboard", new Vector3(3.98f, monitorY, 3.56f), 10f, Fit.Width, .42f, ivory, "Reception_Keyboard");
        Place(lobby, "office phone", new Vector3(2.7f, phoneY, 3.42f), 160f, Fit.Width, .22f, bakelite, "Reception_Phone");
        Gen(lobby, "Paper_Stack", new Vector3(3.6f, stackY, 3.45f), 10f, Fit.Width, .24f, "Reception_Papers");
        Place(lobby, "bench", new Vector3(-5.5f, 0, 2.15f), 0f, Fit.Width, 2.6f, walnut, "Lobby_Bench");
        for (int i = 0; i < 3; i++)
            Place(lobby, "chair for visitors", new Vector3(-7.45f, 0, 3.35f + i * .72f), 90f, Fit.Height, .92f, steel, "Lobby_Chair_" + i);
        Gen(lobby, "Bulletin_Board", new Vector3(-7.86f, 1.25f, 4.05f), 90f, Fit.Width, 1.5f, "Lobby_Wanted_Board");
        Place(lobby, "american flag", new Vector3(6.8f, 0, 1.2f), 200f, Fit.Height, 2.2f, flagCloth, "Lobby_Flag");
        Place(lobby, "hanger", new Vector3(-1.75f, 0, .45f), 0f, Fit.Height, 1.85f, walnut, "Lobby_Coat_Rack");
        Cooler(lobby, new Vector3(-7.55f, 0, 5.55f), 90f, "Lobby_Water_Cooler");
        Gen(lobby, "Plant_Palm", new Vector3(-7.05f, 0, .95f), 10f, Fit.Width, 1.4f, "Lobby_Palm_W");
        Gen(lobby, "Plant_Palm", new Vector3(7.05f, 0, 2.55f), 120f, Fit.Width, 1.3f, "Lobby_Palm_E");
        Gen(lobby, "Plant_Snake", new Vector3(5.6f, 0, 2.75f), 45f, Fit.Height, .95f, "Reception_Plant");
        Gen(lobby, "Wall_Clock", new Vector3(7.87f, 2.35f, 4.6f), -90f, Fit.Height, .36f, "Lobby_Clock");

        // Odds and ends around the bullpen.
        var misc = new GameObject("Bullpen_Details").transform;
        misc.SetParent(root, false);
        Place(misc, "Rustguard Extinguisher", new Vector3(-7.75f, 0, 16.4f), 90f, Fit.Height, .62f, red, "Extinguisher_West");
        Place(misc, "Rustguard Extinguisher", new Vector3(1.55f, 0, 17.78f), 180f, Fit.Height, .62f, red, "Extinguisher_Offices");
        Place(misc, "hanger", new Vector3(-7.5f, 0, 17.3f), 30f, Fit.Height, 1.85f, walnut, "Bullpen_Coat_Rack");
        Place(misc, "trash bag", new Vector3(7.45f, 0, 17.4f), 60f, Fit.Height, .48f, binLiner, "Bullpen_Bin_NE");
        Place(misc, "trash bag", new Vector3(-7.45f, 0, 6.65f), -30f, Fit.Height, .45f, binLiner, "Bullpen_Bin_NW");
        Gen(misc, "Wall_Clock", new Vector3(.9f, 2.45f, 17.92f), 180f, Fit.Height, .38f, "Bullpen_Clock");
        Gen(misc, "Bulletin_Board", new Vector3(-1.4f, 1.3f, 17.92f), 180f, Fit.Width, 1.3f, "Offices_Wall_Board");
        Gen(misc, "Plant_Ficus", new Vector3(2.45f, 0, 17.55f), 200f, Fit.Height, 1.5f, "Offices_Ficus");

        // Ceiling fluorescent fixtures (the lights themselves stay as they are).
        var diffuserMaterial = Art("Cool_Fluorescent_Prismatic_Diffuser") ?? Art("Office_Fluorescent_Diffuser");
        var lamps = new GameObject("Fluorescent_Fixtures").transform;
        lamps.SetParent(root, false);
        foreach (Transform fixture in lighting)
        {
            if (!fixture.name.EndsWith("_Fixture")) continue;
            Hide(fixture);
            Fixture(lamps, fixture.position, fixture.eulerAngles.y, fixture.name);
        }
    }

    /// Ceiling fluorescent: housing model with a glowing prismatic diffuser under it.
    static Placed Fixture(Transform parent, Vector3 p, float yaw, string name)
    {
        var diffuserMaterial = Art("Cool_Fluorescent_Prismatic_Diffuser") ?? Art("Office_Fluorescent_Diffuser");
        var lamp = Place(parent, "fluorescent lamp", new Vector3(p.x, 0, p.z), yaw, Fit.Width, 1.25f, housing, name);
        var box = lamp.pivot.GetComponent<BoxCollider>();
        if (box) Object.DestroyImmediate(box);
        lamp.pivot.position = new Vector3(p.x, Ceiling - lamp.size.y, p.z);
        var diffuser = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(diffuser.GetComponent<Collider>());
        diffuser.name = "Diffuser";
        diffuser.transform.SetParent(lamp.pivot, false);
        diffuser.transform.SetPositionAndRotation(new Vector3(p.x, Ceiling - lamp.size.y - .004f, p.z), Quaternion.LookRotation(Vector3.up, lamp.pivot.forward));
        diffuser.transform.localScale = new Vector3(lamp.size.x * .88f, lamp.size.z * .62f, 1f);
        var glow = diffuser.GetComponent<MeshRenderer>();
        glow.sharedMaterial = diffuserMaterial;
        glow.shadowCastingMode = ShadowCastingMode.Off;
        return lamp;
    }

    /// More ceiling fixtures between the original rows, each with its own soft spot light.
    static void ExtraFixtures(Transform root)
    {
        var parent = new GameObject("Extra_Fluorescents").transform;
        parent.SetParent(root, false);
        var spots = new[]
        {
            new Vector3(-4f, 0, 5.75f), new Vector3(0f, 0, 5.75f), new Vector3(4f, 0, 5.75f),
            new Vector3(-6.7f, 0, 10.25f), new Vector3(6.7f, 0, 10.25f),
            new Vector3(-6.7f, 0, 14f), new Vector3(6.7f, 0, 14f),
            new Vector3(-2f, 0, 17.1f), new Vector3(2f, 0, 17.1f),
            new Vector3(6.1f, 0, 22.6f), new Vector3(-6.1f, 0, 22.6f),
        };
        for (int i = 0; i < spots.Length; i++)
        {
            var lamp = Fixture(parent, spots[i], 0f, "Fluo_Extra_" + i);
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(lamp.pivot, false);
            light.transform.SetPositionAndRotation(new Vector3(spots[i].x, Ceiling - .15f, spots[i].z), Quaternion.Euler(90, 0, 0));
            light.type = LightType.Spot;
            light.spotAngle = 150f;
            light.innerSpotAngle = 70f;
            light.range = 6.5f;
            bool office = spots[i].z > 18f;
            light.intensity = office ? 2.2f : 4.6f;
            light.color = new Color(.93f, .97f, 1f);
            light.shadows = LightShadows.None;
        }
    }

    /// Swaps one greybox desk for the modelled set. Returns the height of the work surface.
    static float DressDesk(Transform desk, Transform parent, bool lamp, bool bin, bool plant, int screen, int seed)
    {
        if (!desk) return .76f;
        foreach (var r in desk.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        var set = new GameObject(desk.name + "_Set").transform;
        set.SetParent(parent, false);
        float yaw = desk.eulerAngles.y;
        var turn = Quaternion.Euler(0, yaw, 0);
        Vector3 At(float x, float y, float z) => desk.position + turn * new Vector3(x, 0, z) + Vector3.up * y;
        var random = new System.Random(seed * 7919 + 11);
        float Jitter(float range) => ((float)random.NextDouble() * 2f - 1f) * range;

        // The models face +Z; in the greybox the worker sits on the desk's -Z side.
        var clean = CleanTop("police table");
        var table = Place(set, "police table", At(0, 0, 0), yaw + 180f, Fit.Width, 1.5f, oak, "Desk", clean.mesh);
        float top = table.size.y * clean.surface + desk.position.y;
        var monitor = Place(set, "computer monitor", At(-.15f + Jitter(.05f), top, .12f), yaw + 180f + Jitter(6f), Fit.Height, .42f, ivory, "Monitor");
        Screen(monitor, set, screen);
        Place(set, "keyboard", At(-.15f + Jitter(.03f), top, -.2f), yaw + 180f + Jitter(5f), Fit.Width, .45f, ivory, "Keyboard");
        Place(set, "office phone", At(.55f, top, .18f), yaw + 200f + Jitter(10f), Fit.Width, .22f, bakelite, "Phone");
        Place(set, "computer tower", At(-.55f, 0, .12f), yaw + 180f, Fit.Height, .42f, ivory, "Tower");
        Gen(set, "Paper_Stack", At(.3f + Jitter(.04f), top, -.04f), yaw + Jitter(25f), Fit.Width, .24f, "Papers");
        if (seed % 2 == 0) Gen(set, "Coffee_Mug", At(.18f, top, -.26f), yaw + Jitter(180f), Fit.Height, .1f, "Mug");
        if (lamp) Place(set, "desk lamp from alex office", At(-.62f, top, .2f), yaw + 160f, Fit.Height, .4f, enamel, "Lamp");
        if (plant) Gen(set, "Plant_Desk", At(-.62f, top, .22f), yaw + Jitter(180f), Fit.Height, .22f, "Plant");
        if (bin) Place(set, "trash bag", At(.95f, 0, -.1f), yaw + 35f, Fit.Height, .4f, binLiner, "Bin");
        var chair = desk.Find("Chair");
        if (chair) Chair(set, chair.position, chair.eulerAngles.y);
        return top;
    }

    // ------------------------------------------------------------------ doors and the glass partition

    static void DressArchitecture(Transform greybox, Transform root)
    {
        var arch = greybox.Find("Architecture");
        Door(arch.Find("FrontDoor_L_Pivot"), "Door_Entrance", flip: false);
        Door(arch.Find("FrontDoor_R_Pivot"), "Door_Entrance", flip: true);
        Door(arch.Find("OfficeDoor_Pivot"), "Door_Office", flip: false);
        Door(arch.Find("CaptainDoor_Pivot"), "Door_Office", flip: false);

        // Lobby / bullpen divide: wood dado with glazing up to the ceiling, like the reference.
        var props = greybox.Find("Props");
        Hide(props, "Railing_L", "Railing_R");
        var glass = new GameObject("Glass_Partition").transform;
        glass.SetParent(root, false);
        foreach (var (from, to, count) in new[] { (-7.9f, -1.4f, 6), (2.2f, 7.9f, 5) })
        {
            float width = (to - from) / count;
            for (int i = 0; i < count; i++)
            {
                var module = Gen(glass, "Glass_Partition", new Vector3(from + width * (i + .5f), 0, 6.2f), 0f, Fit.Height, Ceiling,
                    $"Partition_{(from < 0 ? "W" : "E")}_{i}", width / 1f);
                // Neighbouring modules share their end posts; every other module is a hair deeper
                // so the coincident post faces don't flicker.
                if (i % 2 == 1) module.pivot.localScale = new Vector3(1f, 1f, 1.035f);
            }
        }
        // Solid steel posts framing the opening into the bullpen, wrapping the module ends.
        foreach (var x in new[] { -1.4f, 2.2f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Opening_Post_" + x;
            post.transform.SetParent(glass, false);
            post.transform.position = new Vector3(x, Ceiling * .5f, 6.2f);
            post.transform.localScale = new Vector3(.1f, Ceiling, .16f);
            post.GetComponent<MeshRenderer>().sharedMaterial = steel;
        }
    }

    static void Door(Transform pivot, string model, bool flip)
    {
        if (!pivot) return;
        foreach (var r in pivot.GetComponentsInChildren<Renderer>(true))
            if (r.name == "Leaf" || r.name == "Glass") r.enabled = false;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + model + ".fbx");
        var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
        body.name = "Door_Model";
        body.transform.localScale = Vector3.one;
        body.transform.localRotation = Quaternion.Euler(0, flip ? 180f : 0f, 0) * prefab.transform.localRotation;
        body.transform.localPosition = Vector3.zero;
        foreach (var filter in body.GetComponentsInChildren<MeshFilter>()) filter.sharedMesh = Prepared(model);
        foreach (var r in body.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] && slots.TryGetValue(mats[i].name, out var m) && m) mats[i] = model == "Door_Office" && mats[i].name == "Glass" ? (Art("Mottled_Frosted_Glass") ?? m) : m;
            r.sharedMaterials = mats;
        }
        // Hinge edge on the pivot, bottom on the floor, centred in the frame's thickness.
        var leaf = pivot.Find("Leaf");
        var renderers = body.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        float hingeX = flip ? bounds.max.x : bounds.min.x;
        float z = leaf ? leaf.position.z : pivot.position.z;
        body.transform.position += new Vector3(pivot.position.x - hingeX, pivot.position.y - bounds.min.y, z - bounds.center.z);
    }


    /// Water cooler: the generated model is one piece, so its upper part becomes the blue bottle.
    static void Cooler(Transform parent, Vector3 position, float yaw, string name)
    {
        var mesh = SplitByHeight("water cooler", .64f);
        var placed = Place(parent, "water cooler", position, yaw, Fit.Height, 1.3f, null, name, mesh);
        var bottleMat = Paint("Water_Bottle_Blue", new Color(.32f, .55f, .78f, .62f), .9f, 0f, null, true);
        foreach (var r in placed.body.GetComponentsInChildren<Renderer>()) r.sharedMaterials = new[] { ivory, bottleMat };
    }

    static void Chair(Transform parent, Vector3 position, float yaw)
    {
        var placed = Place(parent, "office chair", position, yaw, Fit.Height, 1.02f, null, "Chair", SplitByHeight("office chair", .4f));
        var fabric = Paint("Worn_Teal_Chair_Fabric", new Color(.13f, .2f, .22f), .12f);
        var frame = Paint("Dark_Chair_Frame_Steel", new Color(.07f, .07f, .075f), .45f, .6f);
        foreach (var r in placed.body.GetComponentsInChildren<Renderer>()) r.sharedMaterials = new[] { frame, fabric };
    }

    /// Two-submesh copy of a one-material model: submesh 0 below `fraction` of the height, 1 above.
    static Mesh SplitByHeight(string asset, float fraction)
    {
        string path = Generated + asset + "_Split" + Mathf.RoundToInt(fraction * 100) + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Props + asset + ".fbx");
            var source = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            var upright = Matrix4x4.Rotate(prefab.transform.localRotation);
            var vertices = source.vertices;
            var heights = vertices.Select(v => upright.MultiplyPoint3x4(v).y).ToArray();
            float min = heights.Min(), max = heights.Max(), split = min + (max - min) * fraction;
            var tris = source.triangles;
            var body = new List<int>(); var bottle = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                float y = (heights[tris[i]] + heights[tris[i + 1]] + heights[tris[i + 2]]) / 3f;
                (y > split ? bottle : body).AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
            }
            mesh = Object.Instantiate(source);
            mesh.name = asset + "_Split";
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(bottle, 1);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
        }
        return mesh;
    }

    // ------------------------------------------------------------------ windows

    static void DressWindows(Transform greybox, Transform root)
    {
        var arch = greybox.Find("Architecture");
        var office = greybox.Find("Office_Alexey");
        foreach (Transform t in arch)
            if (t.name.StartsWith("Window_") || t.name == "Captain_Glass") Hide(t);
        for (int i = 0; i < 10; i++) Hide(office.Find("Blind_" + i));
        Hide(office.Find("Office_Window"));

        var windows = new GameObject("Windows").transform;
        windows.SetParent(root, false);
        // Street windows: dusk glows through half-tilted blinds and throws slat shadows inside.
        Window(windows, new Vector3(-4.5f, 1.75f, 0f), 0f, 1.6f, 1.5f, true, "Window_Lobby_W");
        Window(windows, new Vector3(4.5f, 1.75f, 0f), 0f, 1.6f, 1.5f, true, "Window_Lobby_E");
        Window(windows, new Vector3(7.9f, 1.85f, 9.4f), -90f, 1.5f, 1.3f, true, "Window_Bullpen_E1");
        Window(windows, new Vector3(7.9f, 1.85f, 12.75f), -90f, 1.5f, 1.3f, true, "Window_Bullpen_E2");
        Window(windows, new Vector3(4.6f, 1.7f, 23.9f), 180f, 2.4f, 1.25f, true, "Window_Office_Alexey");
        Window(windows, new Vector3(-4.6f, 1.7f, 23.9f), 180f, 2.0f, 1.25f, true, "Window_Office_Captain");
        // Interior windows between the offices and the bullpen: real openings with clear glass.
        Opening(arch.Find("Wall_Offices_0"), -6.8f, -4.4f, 1.05f, 2.15f, windows);
        Window(windows, new Vector3(-5.6f, 1.6f, 17.94f), 180f, 2.4f, 1.1f, false, "Window_Captain_Interior");
        Opening(arch.Find("Wall_Offices_4"), 4.8f, 7.2f, 1.05f, 2.15f, windows);
        Window(windows, new Vector3(6.0f, 1.6f, 17.94f), 180f, 2.4f, 1.1f, false, "Window_Alexey_Interior");
    }

    /// Replaces a box wall (long along X) with four pieces around a window opening.
    static void Opening(Transform wall, float x0, float x1, float y0, float y1, Transform parent)
    {
        if (!wall) return;
        var renderer = wall.GetComponent<Renderer>();
        var material = renderer.sharedMaterial;
        renderer.enabled = false;
        var collider = wall.GetComponent<Collider>();
        if (collider) collider.enabled = false;
        var b = renderer.bounds;
        void Piece(string n, Vector3 min, Vector3 max)
        {
            if (max.x - min.x < .001f || max.y - min.y < .001f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = wall.name + "_" + n;
            go.transform.SetParent(parent, false);
            go.transform.position = (min + max) * .5f;
            go.transform.localScale = max - min;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        Piece("Left", new Vector3(b.min.x, b.min.y, b.min.z), new Vector3(x0, b.max.y, b.max.z));
        Piece("Right", new Vector3(x1, b.min.y, b.min.z), new Vector3(b.max.x, b.max.y, b.max.z));
        Piece("Below", new Vector3(x0, b.min.y, b.min.z), new Vector3(x1, y0, b.max.z));
        Piece("Above", new Vector3(x0, y1, b.min.z), new Vector3(x1, b.max.y, b.max.z));
    }

    static void Window(Transform parent, Vector3 wallPoint, float yaw, float width, float height, bool street, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "Window_Blinds.fbx");
        float baseHeight = 1.54f, baseWidth = 1.72f;
        float stretch = width / (baseWidth * height / baseHeight);
        var placed = Gen(parent, "Window_Blinds", Vector3.zero, yaw, Fit.Height, height, name, stretch);
        var forward = placed.pivot.forward;
        placed.pivot.position = new Vector3(wallPoint.x, wallPoint.y - placed.size.y * .5f, wallPoint.z) + forward * (placed.size.z * .5f);
        foreach (var r in placed.body.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (!street && mats[i] == slots["WindowGlass"]) mats[i] = slots["Glass"];
            r.sharedMaterials = mats;
            if (!street) continue;
        }
        if (!street) return;
        RainOnPane(placed, forward);
        var light = new GameObject("Window_Light").AddComponent<Light>();
        light.transform.SetParent(placed.pivot, true);
        light.transform.position = wallPoint + forward * .035f;
        light.transform.rotation = Quaternion.LookRotation((forward + Vector3.down * .4f).normalized, Vector3.up);
        light.type = LightType.Spot;
        light.spotAngle = 115f;
        light.innerSpotAngle = 55f;
        light.range = 7.5f;
        light.intensity = 2.4f;
        light.color = new Color(.6f, .7f, .9f);
        light.shadows = LightShadows.Soft;
        light.shadowStrength = .75f;
    }

    /// Drops running down the outside pane, same shader as the 00 car windows.
    static void RainOnPane(Placed placed, Vector3 forward)
    {
        var glass = slots["WindowGlass"];
        foreach (var r in placed.body.GetComponentsInChildren<MeshRenderer>())
        {
            var mats = r.sharedMaterials;
            var mesh = r.GetComponent<MeshFilter>().sharedMesh;
            for (int i = 0; i < mats.Length && i < mesh.subMeshCount; i++)
            {
                if (mats[i] != glass) continue;
                var local = mesh.GetSubMesh(i).bounds;
                var m = r.localToWorldMatrix;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                float minR = 1e9f, maxR = -1e9f, minY = 1e9f, maxY = -1e9f, minF = 1e9f, maxF = -1e9f;
                for (int c = 0; c < 8; c++)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var w = m.MultiplyPoint3x4(corner);
                    float pr = Vector3.Dot(w, right), pf = Vector3.Dot(w, forward);
                    minR = Mathf.Min(minR, pr); maxR = Mathf.Max(maxR, pr);
                    minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
                    minF = Mathf.Min(minF, pf); maxF = Mathf.Max(maxF, pf);
                }
                float width = maxR - minR, height = maxY - minY;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = "Rain_On_Glass";
                quad.transform.SetParent(placed.pivot, true);
                quad.transform.position = right * ((minR + maxR) * .5f) + Vector3.up * ((minY + maxY) * .5f) + forward * (maxF + .004f);
                quad.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
                quad.transform.localScale = new Vector3(width, height, 1f) * 0.98f;
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = RainMaterial(height / Mathf.Max(width, .01f));
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                return;
            }
        }
    }

    static Material RainMaterial(float aspect)
    {
        string path = Mats + "Props/Rain_Precinct_Window_" + Mathf.RoundToInt(aspect * 10) + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/ArtDirection/Rain/RainGlass_Side.mat"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_VScale", aspect);
        mat.SetFloat("_StaticAmount", .4f);
        mat.SetFloat("_RunningAmount", .45f);
        mat.SetFloat("_RunSpeed", .8f);
        mat.SetFloat("_Wipers", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// Brighter ceiling fixtures (absolute values, so re-running doesn't keep adding up).
    static void Brighten(Transform lighting)
    {
        foreach (var light in lighting.GetComponentsInChildren<Light>())
        {
            if (light.name.StartsWith("Fluo_")) { light.intensity = light.name.EndsWith("_3") ? 5.4f : 6f; light.range = 7f; }
            else if (light.name.StartsWith("Ceiling_Bounce")) { light.intensity = .45f; light.range = 2.4f; }
            else if (light.name == "Office_Ceiling") { light.intensity = 2.4f; light.range = 6f; }
            else if (light.name == "Captain_Ceiling") { light.intensity = 2.6f; light.range = 6f; }
            else if (light.name.EndsWith("_Bounce") && light.type == LightType.Point) light.intensity = Mathf.Max(light.intensity, .8f);
        }
        // More lights may touch one object (ceiling + screens + windows).
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline)
        {
            var so = new SerializedObject(pipeline);
            var limit = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (limit != null && limit.intValue < 8) { limit.intValue = 8; so.ApplyModifiedProperties(); AssetDatabase.SaveAssets(); }
        }
    }

    // ------------------------------------------------------------------ trim: skirting, door casings, facade paint

    static Material trimWood;

    static void Strip(Transform parent, string name, Vector3 min, Vector3 max, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = (min + max) * .5f;
        go.transform.localScale = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z));
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static void Casing(Transform parent, string name, float x0, float x1, float top, float faceZ, float outward)
    {
        const float w = .08f, t = .025f;
        float z0 = outward > 0 ? faceZ : faceZ - t, z1 = outward > 0 ? faceZ + t : faceZ;
        Strip(parent, name + "_JambL", new Vector3(x0 - w, 0, z0), new Vector3(x0, top + w, z1), trimWood);
        Strip(parent, name + "_JambR", new Vector3(x1, 0, z0), new Vector3(x1 + w, top + w, z1), trimWood);
        Strip(parent, name + "_Head", new Vector3(x0 - w, top, z0), new Vector3(x1 + w, top + w, z1), trimWood);
    }

    static void Trim(Transform greybox, Transform root)
    {
        trimWood = walnut;
        var sage = Art("Institutional_Sage_Paint");
        var wainscot = Art("Scuffed_Green_Wainscot");
        var arch = greybox.Find("Architecture");
        var trim = new GameObject("Trim").transform;
        trim.SetParent(root, false);

        // The lobby side of the brick facade gets the precinct's paint and wainscot.
        foreach (var n in new[] { "Vestibule_L", "Vestibule_R", "Vestibule_Top" })
        {
            var t = arch.Find(n);
            if (t) t.GetComponent<Renderer>().sharedMaterial = sage;
        }
        foreach (var (x0, x1) in new[] { (-7.9f, -1.1f), (1.1f, 7.9f) })
        {
            Strip(trim, "Facade_Paint", new Vector3(x0, 0, 0f), new Vector3(x1, Ceiling, .008f), sage);
            Strip(trim, "Facade_Wainscot", new Vector3(x0, 0, .008f), new Vector3(x1, 1f, .018f), wainscot);
            Strip(trim, "Facade_Skirting", new Vector3(x0, 0, .018f), new Vector3(x1, .1f, .036f), trimWood);
        }
        Strip(trim, "Facade_Header_Paint", new Vector3(-1.1f, 2.5f, 0f), new Vector3(1.1f, Ceiling, .008f), sage);

        // Skirting boards.
        Strip(trim, "Skirting_West", new Vector3(-7.86f, 0, 0f), new Vector3(-7.84f, .1f, 24f), trimWood);
        Strip(trim, "Skirting_East", new Vector3(7.84f, 0, 0f), new Vector3(7.86f, .1f, 24f), trimWood);
        Strip(trim, "Skirting_Back_W", new Vector3(-7.9f, 0, 23.88f), new Vector3(-.06f, .1f, 23.9f), trimWood);
        Strip(trim, "Skirting_Back_E", new Vector3(.06f, 0, 23.88f), new Vector3(7.9f, .1f, 23.9f), trimWood);
        foreach (var (x0, x1) in new[] { (-7.9f, -4.08f), (-2.92f, 2.92f), (4.08f, 7.9f) })
            Strip(trim, "Skirting_Offices_Bullpen", new Vector3(x0, 0, 17.92f), new Vector3(x1, .1f, 17.94f), trimWood);
        foreach (var (x0, x1) in new[] { (-7.9f, -4.08f), (-2.92f, -.06f), (.06f, 2.92f), (4.08f, 7.9f) })
            Strip(trim, "Skirting_Offices_Inside", new Vector3(x0, 0, 18.06f), new Vector3(x1, .1f, 18.08f), trimWood);
        Strip(trim, "Skirting_Divider_W", new Vector3(-.08f, 0, 18.06f), new Vector3(-.06f, .1f, 23.9f), trimWood);
        Strip(trim, "Skirting_Divider_E", new Vector3(.06f, 0, 18.06f), new Vector3(.08f, .1f, 23.9f), trimWood);

        // Door casings on both faces of every opening.
        Casing(trim, "Casing_Entrance_In", -1.1f, 1.1f, 2.5f, 0f, 1f);
        Casing(trim, "Casing_Entrance_Out", -1.1f, 1.1f, 2.5f, -.3f, -1f);
        Casing(trim, "Casing_Captain_Bullpen", -4f, -3f, 2.2f, 17.94f, -1f);
        Casing(trim, "Casing_Captain_Office", -4f, -3f, 2.2f, 18.06f, 1f);
        Casing(trim, "Casing_Alexey_Bullpen", 3f, 4f, 2.2f, 17.94f, -1f);
        Casing(trim, "Casing_Alexey_Office", 3f, 4f, 2.2f, 18.06f, 1f);
    }

    // ------------------------------------------------------------------ NPCs

    static AnimationClip Clip(string suffix) =>
        AssetDatabase.LoadAllAssetsAtPath(Npc + "Animations/police_npc4" + suffix + ".fbx").OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview"));

    static AnimatorController Controller(string name, params string[] clips)
    {
        string path = Npc + "Animations/" + name + ".controller";
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        var states = clips.Select((clip, i) =>
        {
            var state = machine.AddState(clip, new Vector3(260, 60 + 70 * i));
            state.motion = Clip(clip);
            return state;
        }).ToArray();
        // Several clips cycle into each other so the talkers don't loop one gesture.
        for (int i = 0; states.Length > 1 && i < states.Length; i++)
        {
            var transition = states[i].AddTransition(states[(i + 1) % states.Length]);
            transition.hasExitTime = true;
            transition.exitTime = .94f;
            transition.hasFixedDuration = true;
            transition.duration = .45f;
        }
        machine.defaultState = states[0];
        return controller;
    }

    static void AnimateNpcs(Transform npcs)
    {
        var standing = Controller("NPC_Standing_Talk", "Talking", "Talking_1", "Talking_2");
        var standingAlt = Controller("NPC_Standing_Talk_Alt", "Talking_2", "Talking", "Talking_1");
        var sittingIdle = Controller("NPC_Sitting_Idle", "Sitting_Idle");
        var sittingTalk = Controller("NPC_Sitting_Talk", "Sitting_Talking");

        Body(npcs.Find("Desk_Sergeant"), 1, standing, 1.9f);
        Body(npcs.Find("Cop_AtDesk_1"), 4, sittingIdle, 1.85f);
        Body(npcs.Find("Cop_AtDesk_2"), 5, sittingTalk, 1.91f);
        Body(npcs.Find("Cop_Standing"), 2, standing, 1.91f);
        Body(npcs.Find("Detective_Talking"), 3, standingAlt, 1.87f);
        Body(npcs.Find("Detective_Coffee"), 4, standingAlt, 1.88f);
        Body(npcs.Find("Captain"), 1, sittingTalk, 1.9f);
        // The visitor on the lobby bench was the last primitive figure.
        var visitor = npcs.Find("Civilian_Waiting");
        if (visitor)
        {
            visitor.position = new Vector3(visitor.position.x, 0f, visitor.position.z);
            Body(visitor, 2, sittingIdle, 1.86f);
        }
    }

    static void Body(Transform npc, int model, AnimatorController controller, float scale)
    {
        if (!npc) return;
        var previous = npc.Find("Rigged_Visual");
        if (previous) Object.DestroyImmediate(previous.gameObject);
        var reference = npc.Find("Police_Visual") ? npc.Find("Police_Visual").GetComponent<Renderer>() : null;
        foreach (var r in npc.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

        string path = Npc + "Rigged/police_npc" + model + "_Rigged.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, npc);
        body.name = "Rigged_Visual";
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one * scale;

        var animator = body.GetComponent<Animator>();
        if (!animator) animator = body.AddComponent<Animator>();
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().First();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        body.AddComponent<NpcAnimationVariety>();

        var uniform = Art("Police_Uniform_" + model);
        foreach (var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (uniform) skin.sharedMaterials = Enumerable.Repeat(uniform, skin.sharedMaterials.Length).ToArray();
            if (reference)
            {
                skin.renderingLayerMask = reference.renderingLayerMask;
                skin.shadowCastingMode = reference.shadowCastingMode;
            }
            skin.updateWhenOffscreen = true;
        }
    }
}
