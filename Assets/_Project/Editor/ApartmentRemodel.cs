using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using CarXCase.Apartment;

/// Remodel of 01_FatherApartment: new shell (Apartment_Shell.fbx from ArtSource/Apartment/BrooklynApartment_v2.blend),
/// bigger bathroom with a clean corner, the side wing turned into one bedroom with a proper doorway,
/// worn wallpaper / stained ceiling, extra lights and the record player.
/// Menu: Tools/Brighton/Apartment: apply remodel   (or "apply" in HeroSetup/apartment_request.txt)
/// Old geometry is only switched off, never deleted. A scene backup is written on the first run.
[InitializeOnLoad]
public static class ApartmentRemodel
{
    const string ScenePath = "Assets/_Project/Scenes/01_FatherApartment.unity";
    const string ShellFbx = "Assets/_Project/Arts/Apartaments/Shell/Apartment_Shell.fbx";
    const string ShellTex = "Assets/_Project/Arts/Apartaments/Shell/Textures/";
    const string ShellMat = "Assets/_Project/Arts/Apartaments/Shell/Materials/";
    const string OldMat = "Assets/_Project/Arts/Apartaments/Materials/";
    const string RpFbx = "Assets/_Project/Arts/Apartaments/RecordPlayer/RecordPlayer.fbx";
    const string RpTex = "Assets/_Project/Arts/Apartaments/RecordPlayer/Textures/";
    const string RpMat = "Assets/_Project/Arts/Apartaments/RecordPlayer/Materials/";
    const string Io = "HeroSetup";
    static readonly StringBuilder log = new StringBuilder();

    static ApartmentRemodel() { EditorApplication.update += Poll; }
    static void Poll()
    {
        string req = Path.Combine(Io, "apartment_request.txt");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(req)) return;
        string cmd = File.ReadAllText(req).Trim(); File.Delete(req);
        Run(cmd.Length == 0 ? "apply capture" : cmd);
    }

    [MenuItem("Tools/Brighton/Apartment: apply remodel")] static void MenuApply() => Run("apply capture");
    [MenuItem("Tools/Brighton/Apartment: capture review shots")] static void MenuCapture() => Run("capture");

    static void Run(string cmd)
    {
        log.Clear(); log.AppendLine("APARTMENT " + cmd + " " + DateTime.Now);
        try
        {
            foreach (var c in cmd.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (c == "apply") Apply(); else if (c == "capture") Capture(); else throw new Exception("Unknown " + c);
            log.AppendLine("SUCCESS");
        }
        catch (Exception e) { log.AppendLine("ERROR " + e); Debug.LogException(e); }
        Directory.CreateDirectory(Io);
        File.WriteAllText(Path.Combine(Io, "apartment_result.txt"), log.ToString());
        Debug.Log("[Apartment] " + log);
    }

    // ------------------------------------------------------------------ helpers
    static IEnumerable<Transform> All() => EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true));
    static Transform Find(string n) => All().FirstOrDefault(t => t.name == n);
    static List<Transform> FindAll(Func<string, bool> pred) => All().Where(t => pred(t.name)).ToList();

    static Bounds BoundsOf(Transform t)
    {
        var rr = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rr.Length == 0) return new Bounds(t.position, Vector3.zero);
        var b = rr[0].bounds; foreach (var r in rr.Skip(1)) b.Encapsulate(r.bounds); return b;
    }

    static void Tex(string path, bool normal, bool clamp = false)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter; if (!ti) { log.AppendLine("missing texture " + path); return; }
        bool d = false;
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType != type) { ti.textureType = type; d = true; }
        if (ti.anisoLevel != 8) { ti.anisoLevel = 8; d = true; }
        if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; d = true; }
        var wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat; if (ti.wrapMode != wrap) { ti.wrapMode = wrap; d = true; }
        if (d) ti.SaveAndReimport();
    }

    static Material Lit(string dir, string name, string albedo, string normal, Color tint, float smooth, float metal, bool smoothFromAlpha = false, float bump = 1f)
    {
        Directory.CreateDirectory(dir);
        string path = dir + name + ".mat";
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
        m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        var a = albedo != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(albedo) : null;
        var n = normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normal) : null;
        m.SetTexture("_BaseMap", a);
        if (n) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); } else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        m.SetFloat("_SmoothnessTextureChannel", smoothFromAlpha ? 1 : 0);
        if (smoothFromAlpha) m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"); else m.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        EditorUtility.SetDirty(m); return m;
    }

    static void Transparent(Material m, float alpha)
    {
        var c = m.GetColor("_BaseColor"); c.a = alpha; m.SetColor("_BaseColor", c);
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
        m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("ShadowCaster", false);
    }

    static Light AddLight(Transform parent, string name, Vector3 pos, LightType type, Color c, float intensity, float range, LightShadows shadows, Quaternion? rot = null, float spot = 60)
    {
        var old = parent.Find(name); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.position = pos; if (rot.HasValue) g.transform.rotation = rot.Value;
        var l = g.AddComponent<Light>(); l.type = type; l.useColorTemperature = false; l.color = c; l.intensity = intensity; l.range = range; l.shadows = shadows;
        if (type == LightType.Spot) { l.spotAngle = spot; l.innerSpotAngle = spot * 0.55f; }
        return l;
    }

    // world position for a point given in the original Blender coordinates of the apartment
    static Transform apartment;
    static Vector3 B(float x, float y, float z) => apartment.TransformPoint(new Vector3(-x, z, -y));
    static Vector3 BDelta(float dx, float dy, float dz) => apartment.TransformVector(new Vector3(-dx, dz, -dy));

    static void Shift(string name, Vector3 world)
    {
        var t = Find(name); if (!t) { log.AppendLine("  not found: " + name); return; }
        t.position += world; log.AppendLine("  moved " + name);
    }

    // ------------------------------------------------------------------ apply
    static void Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode first");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var n in new[] { "Wallpaper_Peeled", "Ceiling_Stained", "BathPaint_Damp" }) { Tex(ShellTex + n + "_Albedo.png", false); Tex(ShellTex + n + "_Normal.png", true); }
        foreach (var n in new[] { "RP_Record", "RP_Walnut", "RP_Grille" }) { Tex(RpTex + n + "_Albedo.png", false, n == "RP_Record"); Tex(RpTex + n + "_Normal.png", true, n == "RP_Record"); }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        apartment = GameObject.Find("BrooklynApartment")?.transform ?? throw new Exception("BrooklynApartment not found");
        // The expansion had already hidden Wall_East before the remodel existed.
        // The light rig is only created after the remodel's initial layout is installed.
        bool first = !Find("Remodel_Lights");
        log.AppendLine("First run: " + first);
        if (first && !System.IO.Directory.GetFiles("Assets/_Project/Scenes", "BeforeRemodel_*.unity").Any())
        {
            string backup = "Assets/_Project/Scenes/BeforeRemodel_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            EditorSceneManager.SaveScene(scene, backup, true); log.AppendLine("Backup " + backup);
        }

        // ---------- shell
        var prev = Find("Apartment_Shell");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ShellFbx) ?? throw new Exception("Missing " + ShellFbx);
        var shell = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        shell.name = "Apartment_Shell"; shell.transform.SetParent(apartment, false);
        shell.transform.localPosition = Vector3.zero; shell.transform.localRotation = Quaternion.identity; shell.transform.localScale = Vector3.one;
        // Align against the unchanged entry jamb wall. The remodel extends WestFront
        // on the opposite end from the old corner, so it is not an alignment anchor.
        var oldWall = Find("Wall_EntryRight");
        var newWall = shell.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Shell_Wall_EntryRight");
        if (!oldWall || !newWall || (BoundsOf(oldWall).size - BoundsOf(newWall).size).sqrMagnitude > 0.0025f)
        {
            UnityEngine.Object.DestroyImmediate(shell);
            throw new Exception("Shell does not match the original entry wall; kept the previous shell.");
        }
        shell.transform.position += BoundsOf(oldWall).center - BoundsOf(newWall).center;
        if (prev) UnityEngine.Object.DestroyImmediate(prev.gameObject);

        // ---------- materials
        var wall = Lit(ShellMat, "Shell_Wallpaper", ShellTex + "Wallpaper_Peeled_Albedo.png", ShellTex + "Wallpaper_Peeled_Normal.png", Color.white, 0.16f, 0, false, 0.55f);
        wall.SetTextureScale("_BaseMap", Vector2.one * 0.25f);
        var bath = Lit(ShellMat, "Shell_BathPaint", ShellTex + "BathPaint_Damp_Albedo.png", ShellTex + "BathPaint_Damp_Normal.png", Color.white, 0.9f, 0, true, 0.7f);
        var ceil = Lit(ShellMat, "Shell_Ceiling", ShellTex + "Ceiling_Stained_Albedo.png", ShellTex + "Ceiling_Stained_Normal.png", Color.white, 0.9f, 0, true, 0.8f);
        var map = new Dictionary<string, Material> { { "Shell_Wallpaper", wall }, { "Shell_BathPaint", bath }, { "Shell_Ceiling", ceil } };
        foreach (var n in new[] { "RedOakFloor", "OldWalnut", "BathroomTile", "BathroomMosaic", "WindowGlass", "DullBrass", "LampGlass" })
            map[n] = AssetDatabase.LoadAssetAtPath<Material>(OldMat + n + ".mat");
        foreach (var r in shell.GetComponentsInChildren<Renderer>(true))
        {
            var mm = r.sharedMaterials;
            for (int i = 0; i < mm.Length; i++) if (mm[i] && map.TryGetValue(mm[i].name, out var m) && m) mm[i] = m;
            r.sharedMaterials = mm;
            bool glazing = r.name == "Shell_BedWin_Glazing";
            if (glazing) r.shadowCastingMode = ShadowCastingMode.Off;
            bool solid = r.name.StartsWith("Shell_Wall") || r.name.StartsWith("Shell_Bath_") || r.name.StartsWith("Shell_Floor") || r.name == "Shell_Ceiling" || r.name == "Shell_BedDoor_Leaf" || glazing;
            if (solid) AddBoxFromMesh(r.transform);
        }

        // ---------- switch off the old architecture (kept for reference)
        string[] oldArch = { "Wall_East", "Wall_North", "Wall_WestFront", "Wall_WestBack", "Wall_UnderWindow", "Wall_AboveWindow", "Wall_EntryLeft", "Wall_EntryRight", "Wall_EntryHeader",
            "Ceiling", "BathPartition_North", "BathPartition_EastRear", "BathPartition_EastFront", "BathPartition_Header", "Bath_WestTiles", "Bath_NorthTiles", "Bath_SouthTiles", "Floor_Bathroom",
            "BathroomDoor_Jamb", "BathroomDoor_Jamb.001", "BathroomDoor_Lintel", "Baseboard_Long", "Baseboard_Long.001", "Baseboard_Rear", "Crown_Long", "Crown_Long.001", "Crown_Rear",
            "COL_Wall_East", "COL_Wall_North", "COL_Wall_WestFront", "COL_Wall_WestBack", "COL_Wall_UnderWindow", "COL_Wall_AboveWindow", "COL_Wall_EntryLeft", "COL_Wall_EntryRight", "COL_Wall_EntryHeader",
            "COL_Ceiling", "COL_BathPartition_North", "COL_BathPartition_EastRear", "COL_BathPartition_EastFront", "COL_BathPartition_Header" };
        int off = 0;
        foreach (var n in oldArch) foreach (var t in FindAll(x => x == n)) if (t.IsChildOf(apartment) && !t.IsChildOf(shell.transform)) { t.gameObject.SetActive(false); off++; }
        var wing = Find("Apartment_AdditionalRooms");
        if (wing)
            foreach (var n in new[] { "Floor_Extension", "Ceiling_Extension", "Wall_OuterWest", "Wall_Front", "Wall_Back", "Wall_EntryFront", "Wall_EntryBack", "Wall_EntryLintel", "Wall_StudyDivider", "Light_Extension", "Wardrobe_Storage", "Chair_Study" })
                foreach (Transform t in wing) if (t.name == n) { t.gameObject.SetActive(false); off++; }
        log.AppendLine("Old pieces switched off: " + off);
        var studioWindow = Find("Window_Glazing");
        if (studioWindow)
        {
            studioWindow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            AddBoxFromMesh(studioWindow);
        }

        // ---------- moves (only on the first run: the deltas are relative to the original layout)
        if (first)
        {
            log.AppendLine("Bathroom wall +0.30 m:");
            foreach (var n in new[] { "INT_BathroomDoor", "HandTowel", "ToiletPaper", "PaperRoll_Holder", "TowelRail" }) Shift(n, BDelta(0.30f, 0, 0));
            log.AppendLine("Shelf away from the bedroom door:");
            foreach (var n in new[] { "WallShelf.001", "Shelf_Bracket.002", "Shelf_Bracket.003" }) Shift(n, BDelta(0, -0.85f, 0));
            log.AppendLine("Bedroom: outer wall moved 0.54 m out:");
            var outward = BDelta(0.54f, 0, 0);
            foreach (var n in new[] { "Bed_Single", "Nightstand_Lamp", "Wall_Shelf", "Bedroom_Lamp_Light", "Secret_Note" }) Shift(n, outward);
            Shift("Carpet", outward * 0.5f);
        }
        // Move the collision proxy and the envelope clue with the table. Using a
        // fixed position makes this work on scenes saved halfway through a rebuild.
        var diningTop = Find("DiningTable_Top");
        if (diningTop)
        {
            var across = BDelta(1, 0, 0).normalized;
            var delta = across * Vector3.Dot(B(1.20f, 0, 0) - diningTop.position, across);
            foreach (var t in FindAll(x => x.StartsWith("DiningTable_") || x == "DiningChair_Assembly" || x.StartsWith("INT_Envelope_") || x == "Envelope_Address" || x == "COL_DiningTable"))
                if (t.IsChildOf(apartment)) t.position += delta;
            log.AppendLine($"Dining set beside the window; shift {delta:F2}");
        }

        // ---------- reading corner at the back wall of the bedroom + record player
        var sofa = Find("Sofa_Study"); var table = Find("Table_Study");
        var cornerRoot = Find("Bedroom_ReadingCorner")?.gameObject ?? new GameObject("Bedroom_ReadingCorner");
        cornerRoot.transform.SetParent(wing ? wing : apartment, true);
        if (sofa)
        {
            if (first) { var bs0 = BoundsOf(sofa); sofa.RotateAround(bs0.center, Vector3.up, 90f); }
            var sofaCollider = sofa.GetComponent<BoxCollider>();
            var sofaMesh = sofa.GetComponent<MeshFilter>();
            if (sofaCollider && sofaMesh && sofaMesh.sharedMesh)
            {
                sofaCollider.center = sofaMesh.sharedMesh.bounds.center;
                sofaCollider.size = sofaMesh.sharedMesh.bounds.size;
            }
            var bs = BoundsOf(sofa);
            Vector3 back = B(5.55f, 6.6f, 0); Vector3 inward = (B(5.55f, 5.6f, 0) - back).normalized;
            Vector3 c = back + inward * (bs.extents.z + 0.06f);
            sofa.position += new Vector3(c.x - bs.center.x, -bs.min.y + apartment.position.y, c.z - bs.center.z);
            log.AppendLine("Sofa placed " + BoundsOf(sofa).center.ToString("F2"));
        }
        if (table)
        {
            var bt = BoundsOf(table); var bs = sofa ? BoundsOf(sofa) : bt;
            Vector3 front = new Vector3(bs.center.x, 0, bs.max.z + 0.42f + bt.extents.z);
            if (B(0, 6.6f, 0).z > B(0, 0, 0).z) front.z = bs.min.z - 0.42f - bt.extents.z;
            table.position += new Vector3(front.x - bt.center.x, -bt.min.y + apartment.position.y, front.z - bt.center.z);
            log.AppendLine("Table placed " + BoundsOf(table).center.ToString("F2"));
        }
        var lampSrc = Find("Nightstand_Lamp");
        var oldCopy = Find("ReadingCorner_Nightstand"); if (oldCopy) UnityEngine.Object.DestroyImmediate(oldCopy.gameObject);
        if (lampSrc && sofa)
        {
            var copy = UnityEngine.Object.Instantiate(lampSrc.gameObject, cornerRoot.transform); copy.name = "ReadingCorner_Nightstand";
            copy.transform.rotation = lampSrc.rotation; copy.transform.localScale = lampSrc.lossyScale;
            var bs = BoundsOf(sofa); var bc = BoundsOf(copy.transform);
            Vector3 side = new Vector3(bs.min.x - bc.extents.x - 0.08f, 0, bs.center.z);
            copy.transform.position += new Vector3(side.x - bc.center.x, bs.min.y - bc.min.y, side.z - bc.center.z);
            var shade = BoundsOf(copy.transform);
            AddLight(cornerRoot.transform, "ReadingLamp_Light", new Vector3(shade.center.x, shade.max.y - 0.12f, shade.center.z), LightType.Point, new Color(1f, 0.74f, 0.45f), 1.4f, 4.2f, LightShadows.Soft);
        }
        var rp = BuildRecordPlayer(cornerRoot.transform, table, sofa);

        // ---------- lights
        var lights = Find("Remodel_Lights")?.gameObject ?? new GameObject("Remodel_Lights");
        var warm = new Color(1f, 0.78f, 0.55f);
        AddLight(lights.transform, "Bedroom_Ceiling_Front", B(5.76f, 1.75f, 2.36f), LightType.Point, warm, 1.9f, 4.3f, LightShadows.Soft);
        AddLight(lights.transform, "Bedroom_Ceiling_Back", B(5.76f, 4.9f, 2.36f), LightType.Point, warm, 1.2f, 4.0f, LightShadows.None);
        AddLight(lights.transform, "Kitchen_UnderCabinet", B(1.55f, 6.28f, 1.55f), LightType.Spot, new Color(0.86f, 0.94f, 1f), 2.2f, 2.6f, LightShadows.None, Quaternion.LookRotation(Vector3.down, apartment.forward), 130);
        var street = B(8.9f, 3.35f, 3.1f); var into = B(6.6f, 3.35f, 0.4f);
        AddLight(lights.transform, "Street_Sodium_Bedroom", street, LightType.Spot, new Color(1f, 0.63f, 0.32f), 9f, 9f, LightShadows.Soft, Quaternion.LookRotation(into - street), 55);
        var streetW = B(-1.2f, 4.85f, 3.0f); var intoW = B(1.2f, 4.85f, 0.4f);
        AddLight(lights.transform, "Street_Sodium_Living", streetW, LightType.Spot, new Color(1f, 0.63f, 0.32f), 6f, 8f, LightShadows.Soft, Quaternion.LookRotation(intoW - streetW), 50);
        foreach (var light in All().Where(t => t.parent && t.parent.name == "GameLighting").Select(t => t.GetComponent<Light>()).Where(l => l))
        {
            if (light.name == "Window_Overcast") light.intensity = 1.1f;
            if (light.name.EndsWith("Bounce") || light.name == "Window_Overcast" || light.name == "Ceiling_Warm.001") light.shadows = LightShadows.None;
            else { light.shadowBias = 0.12f; light.shadowNormalBias = 0.30f; }
        }
        log.AppendLine("Lights added");

        ApartmentFurnishings.Apply(apartment);
        ApartmentFixtures.Apply(apartment);
        ApartmentEvidence.Apply(scene);
        ApartmentNoirLook.RefreshIfPresent(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        log.AppendLine("Saved " + scene.path);
    }

    static void AddBoxFromMesh(Transform part)
    {
        if (part.GetComponent<Collider>()) return;
        var filter = part.GetComponent<MeshFilter>();
        if (!filter || !filter.sharedMesh) throw new InvalidOperationException($"No structural mesh on {part.name}");
        var bounds = filter.sharedMesh.bounds;
        var box = part.gameObject.AddComponent<BoxCollider>();
        box.center = bounds.center;
        // FBX nodes can carry the centimetre-to-metre conversion separately from the mesh.
        var scale = part.lossyScale;
        var minimumSize = new Vector3(
            0.015f / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            0.015f / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
            0.015f / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
        box.size = Vector3.Max(bounds.size, minimumSize);
    }

    static Transform BuildRecordPlayer(Transform parent, Transform table, Transform sofa)
    {
        var prev = Find("RecordPlayer"); if (prev) UnityEngine.Object.DestroyImmediate(prev.gameObject);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(RpFbx) ?? throw new Exception("Missing " + RpFbx);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "RecordPlayer"; go.transform.SetParent(parent, true);

        var walnut = Lit(RpMat, "RP_Walnut", RpTex + "RP_Walnut_Albedo.png", RpTex + "RP_Walnut_Normal.png", Color.white, 0.55f, 0);
        var vinyl = Lit(RpMat, "RP_Vinyl", RpTex + "RP_Record_Albedo.png", RpTex + "RP_Record_Normal.png", Color.white, 1f, 0, true, 0.6f);
        var grille = Lit(RpMat, "RP_Grille", RpTex + "RP_Grille_Albedo.png", RpTex + "RP_Grille_Normal.png", Color.white, 0.05f, 0);
        var alu = Lit(RpMat, "RP_Aluminium", null, null, new Color(0.62f, 0.62f, 0.6f), 0.6f, 0.9f);
        var chrome = Lit(RpMat, "RP_Chrome", null, null, new Color(0.85f, 0.85f, 0.85f), 0.9f, 1f);
        var rubber = Lit(RpMat, "RP_Rubber", null, null, new Color(0.03f, 0.03f, 0.03f), 0.15f, 0);
        var knob = Lit(RpMat, "RP_KnobBlack", null, null, new Color(0.03f, 0.03f, 0.03f), 0.6f, 0);
        var smoked = Lit(RpMat, "RP_Smoked", null, null, new Color(0.05f, 0.045f, 0.04f), 0.95f, 0); Transparent(smoked, 0.38f);
        var lamp = Lit(RpMat, "RP_Lamp", null, null, new Color(0.6f, 0.05f, 0.02f), 0.6f, 0);
        lamp.SetColor("_EmissionColor", new Color(1f, 0.12f, 0.04f) * 3f); lamp.EnableKeyword("_EMISSION");
        var map = new Dictionary<string, Material> { { "RP_Walnut", walnut }, { "RP_Vinyl", vinyl }, { "RP_Grille", grille }, { "RP_Aluminium", alu }, { "RP_Chrome", chrome },
            { "RP_Rubber", rubber }, { "RP_KnobBlack", knob }, { "RP_Smoked", smoked }, { "RP_Lamp", lamp } };
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mm = r.sharedMaterials;
            for (int i = 0; i < mm.Length; i++) if (mm[i] && map.TryGetValue(mm[i].name, out var m)) mm[i] = m;
            r.sharedMaterials = mm;
            if (r.name == "RP_DustCover") r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // spinning platter: pivot on the spindle
        var spindle = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "RP_Spindle");
        var sb = BoundsOf(spindle);
        var pivot = new GameObject("Platter_Pivot").transform; pivot.SetParent(go.transform, false);
        pivot.position = new Vector3(sb.center.x, sb.min.y, sb.center.z); pivot.rotation = go.transform.rotation;
        foreach (var n in new[] { "RP_Platter", "RP_Mat", "RP_Record", "RP_Spindle" })
        {
            var t = go.GetComponentsInChildren<Transform>(true).First(x => x.name == n); t.SetParent(pivot, true);
        }
        pivot.gameObject.AddComponent<RecordSpin>();

        // place on the table top, facing the sofa
        if (table)
        {
            var top = table.GetComponentsInChildren<Renderer>(true).Where(r => r.name.Contains("Top")).Select(r => r.bounds).DefaultIfEmpty(BoundsOf(table)).First();
            if (sofa)
            {
                Vector3 toSofa = BoundsOf(sofa).center - top.center; toSofa.y = 0;
                go.transform.rotation = Quaternion.LookRotation(toSofa.normalized, Vector3.up);
            }
            var b = BoundsOf(go.transform);
            go.transform.position += new Vector3(top.center.x - b.center.x, top.max.y - b.min.y + 0.001f, top.center.z - b.center.z);
            log.AppendLine("Record player on the table at " + BoundsOf(go.transform).center.ToString("F2"));
        }
        var col = go.AddComponent<BoxCollider>(); var lb = BoundsOf(go.transform);
        col.center = go.transform.InverseTransformPoint(lb.center); col.size = new Vector3(0.44f, 0.12f, 0.36f);
        return go.transform;
    }

    // ------------------------------------------------------------------ capture
    static void Capture()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        apartment = GameObject.Find("BrooklynApartment").transform;
        Shot(B(2.1f, 3.2f, 1.6f), B(3.9f, 4.7f, 1.05f), "apt_bedroom_door.png", 62);
        Shot(B(4.35f, 0.35f, 1.75f), B(6.6f, 4.2f, 0.7f), "apt_bedroom_wide.png", 75);
        Shot(B(6.9f, 0.5f, 1.7f), B(4.6f, 3.6f, 0.9f), "apt_bedroom_reverse.png", 75);
        Shot(B(3.2f, 3.4f, 1.55f), B(2.0f, 2.05f, 1.1f), "apt_bath_corner.png", 60);
        Shot(B(1.0f, 0.3f, 1.55f), B(1.2f, 1.8f, 0.8f), "apt_bath_inside.png", 85);
        Shot(B(2.9f, 2.6f, 1.6f), B(1.4f, 5.0f, 0.9f), "apt_living_dining.png", 70);
        var rp = Find("RecordPlayer");
        if (rp)
        {
            var b = BoundsOf(rp); var fwd = rp.forward;
            Shot(b.center + fwd * 0.75f + Vector3.up * 0.45f + rp.right * 0.25f, b.center, "apt_record_player.png", 40);
            Shot(b.center + fwd * 1.9f + Vector3.up * 0.9f - rp.right * 0.6f, b.center + Vector3.up * 0.2f, "apt_reading_corner.png", 60);
        }
    }

    static void Shot(Vector3 from, Vector3 at, string file, float fov)
    {
        var go = new GameObject("__AptCaptureCam");
        try
        {
            var cam = go.AddComponent<Camera>(); cam.fieldOfView = fov; cam.nearClipPlane = 0.03f; cam.farClipPlane = 100;
            go.transform.position = from; go.transform.LookAt(at);
            if (Camera.main) { cam.clearFlags = Camera.main.clearFlags; cam.backgroundColor = Camera.main.backgroundColor; }
            HeroSetup.Shot(cam, file); log.AppendLine("Capture " + file);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
