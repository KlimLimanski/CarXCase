using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Builds the "Brighton Star" coaster moored at the pier (after the art reference): a lofted hull with a raked
/// stem and a cruiser stern, a rust boot-top, rails, a long row of rusty hatch covers, a three-tier ochre
/// superstructure aft with lit windows, a funnel and radar mast, a foremast with a lamp and a yellow cargo boom,
/// stays, a lifeboat, anchors, bollards and the stencilled name on both sides. Procedural textures carry the
/// rust streaks and grime. The result replaces Moored_Cargo_Ship.prefab in place (same GUID, same berth).
/// Run: Tools/Harbor/Rebuild Brighton Star, <project>/Review/ship.request, or -executeMethod BrightonStarBuilder.Build
[InitializeOnLoad]
public static class BrightonStarBuilder
{
    const string PrefabPath = "Assets/_Project/ArtDirection/Harbor/Prefabs/Moored_Cargo_Ship.prefab";
    const string Dir = "Assets/_Project/ArtDirection/Harbor/BrightonStar";
    const string Harbor = "Assets/_Project/Scenes/05_BrightonHarbor.unity";

    // Ship frame: bow towards -Z, starboard (+X) against the pier, waterline at y = 0.
    const float L = 30.6f, B = 6.6f, Draft = 1.55f, Freeboard = 2.35f;

    static BrightonStarBuilder() { EditorApplication.update += Poll; }

    static void Poll()
    {
        string request = Path.Combine(HeroReviewCapture.Folder, "ship.request");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request)) return;
        File.Delete(request);
        Build();
    }

    [MenuItem("Tools/Harbor/Rebuild Brighton Star")]
    public static void Build()
    {
        var log = new StringBuilder();
        try { Run(log); }
        catch (System.Exception e) { log.AppendLine("ERROR " + e); Debug.LogException(e); }
        Directory.CreateDirectory(HeroReviewCapture.Folder);
        File.WriteAllText(Path.Combine(HeroReviewCapture.Folder, "ship_build.txt"), log.ToString());
    }

    // ------------------------------------------------------------------ materials & textures

    static readonly Dictionary<string, Material> mats = new();

    static Material Mat(string name) => mats[name];

    static void MakeMaterials()
    {
        Directory.CreateDirectory(Dir);
        mats.Clear();
        mats["hull"] = Lit("Hull black", Tex("hull", HullTexture), new Color(1, 1, 1), .25f, .32f);
        mats["boot"] = Lit("Hull rust boot-top", Tex("boot", BootTexture), Color.white, .2f, .22f);
        mats["deck"] = Lit("Deck steel", Tex("deck", DeckTexture), Color.white, .3f, .2f);
        mats["paint"] = Lit("Superstructure ochre", Tex("paint", PaintTexture), Color.white, .05f, .28f);
        mats["hatch"] = Lit("Hatch covers rust", Tex("hatch", HatchTexture), Color.white, .25f, .2f);
        mats["steel"] = Lit("Fittings steel", Tex("steel", SteelTexture), new Color(.62f, .63f, .62f), .7f, .38f);
        mats["dark"] = Lit("Fittings black", Tex("steel", SteelTexture), new Color(.12f, .12f, .13f), .5f, .3f);
        mats["boom"] = Lit("Boom yellow", Tex("paint", PaintTexture), new Color(1.08f, 1f, .78f), .15f, .35f);
        mats["boat"] = Lit("Lifeboat orange", Tex("paint", PaintTexture), new Color(1.25f, .66f, .42f), .05f, .4f);
        mats["glass"] = Lit("Window glass", null, new Color(.05f, .06f, .07f), .1f, .85f);
        mats["lit"] = Emissive("Window lit", new Color(1f, .74f, .42f), 2.2f);
        mats["lamp"] = Emissive("Mast lamp", new Color(1f, .86f, .62f), 6f);
        mats["rope"] = Lit("Wire rope", null, new Color(.09f, .09f, .09f), .6f, .3f);
        var name = Lit("Painted name", Tex("name", NameTexture, true), new Color(.86f, .84f, .78f), 0f, .2f);
        name.SetFloat("_AlphaClip", 1f); name.EnableKeyword("_ALPHATEST_ON"); name.SetFloat("_Cutoff", .5f);
        name.renderQueue = (int)RenderQueue.AlphaTest;
        mats["name"] = name;
        foreach (var m in mats.Values)
        {
            string path = Dir + "/" + m.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { existing.CopyPropertiesFromMaterial(m); existing.shaderKeywords = m.shaderKeywords; existing.renderQueue = m.renderQueue; }
            else AssetDatabase.CreateAsset(m, path);
        }
        foreach (var key in mats.Keys.ToList()) mats[key] = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/" + mats[key].name + ".mat");
    }

    static Material Lit(string name, Texture2D tex, Color tint, float metallic, float smooth)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        if (tex) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        return m;
    }

    static Material Emissive(string name, Color c, float power)
    {
        var m = Lit(name, null, c * .6f, 0f, .6f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * power);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return m;
    }

    delegate Color Painter(float u, float v, System.Random rnd);

    static Texture2D Tex(string name, Painter paint, bool alpha = false, int w = 512, int h = 512)
    {
        if (name == "name") { w = 2048; h = 256; }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var px = new Color[w * h];
        var rnd = new System.Random(name.GetHashCode());
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = paint((float)x / w, (float)y / h, rnd);
        tex.SetPixels(px);
        tex.Apply();
        string path = Dir + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = name == "name" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.alphaIsTransparency = alpha;
        importer.sRGBTexture = true;
        importer.maxTextureSize = name == "name" ? 2048 : 1024;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float N(float x, float y) => Mathf.PerlinNoise(x, y);
    static float Fbm(float x, float y) => N(x, y) * .5f + N(x * 2.1f + 7, y * 2.1f + 3) * .3f + N(x * 4.3f + 11, y * 4.3f + 5) * .2f;
    static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));

    // Tiles are wrapped with seamless-ish noise by sampling on periodic coordinates.
    static float P(float u, float v, float freq, float seed)
    {
        float a = u * Mathf.PI * 2, b = v * Mathf.PI * 2;
        return Fbm(Mathf.Cos(a) * freq + seed, Mathf.Sin(a) * freq + Mathf.Cos(b) * freq * .97f + Mathf.Sin(b) * freq * .53f);
    }

    static Color HullTexture(float u, float v, System.Random r)
    {
        // Near-black blue paint, horizontal plate seams, rust bleeding down from rivets and the deck edge.
        var paint = new Color(.075f, .085f, .095f);
        float grime = P(u, v, 3f, 1);
        var c = Mix(paint, new Color(.13f, .12f, .11f), grime * .6f);
        float streakSeed = Mathf.Floor(u * 64f);
        float streak = Mathf.PerlinNoise(streakSeed * .37f, 3.1f);
        if (streak > .62f)
        {
            float run = Mathf.PerlinNoise(streakSeed, v * 6f) * (1f - Mathf.Abs(u * 64f - streakSeed - .5f) * 2f);
            c = Mix(c, new Color(.32f, .15f, .07f), run * (streak - .62f) * 3.2f * (.4f + v * .8f));
        }
        float seam = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - .5f);
        if (seam > .485f) c *= .72f;
        float rivetRow = Mathf.Abs(Mathf.Repeat(v * 4f + .5f, 1f) - .5f);
        if (rivetRow < .012f && Mathf.Repeat(u * 96f, 1f) < .25f) c = Mix(c, new Color(.28f, .14f, .07f), .5f);
        float chip = P(u, v, 14f, 9);
        if (chip > .7f) c = Mix(c, new Color(.36f, .17f, .08f), (chip - .7f) * 4f);
        return c;
    }

    static Color BootTexture(float u, float v, System.Random r)
    {
        var red = new Color(.29f, .1f, .065f);
        float n = P(u, v, 4f, 3);
        var c = Mix(red, new Color(.16f, .075f, .05f), n);
        float algae = P(u, v, 2f, 7) * (1f - v);
        c = Mix(c, new Color(.07f, .085f, .06f), algae * .7f);
        return c;
    }

    static Color DeckTexture(float u, float v, System.Random r)
    {
        var c = Mix(new Color(.2f, .19f, .17f), new Color(.31f, .2f, .13f), P(u, v, 5f, 4));
        if (Mathf.Abs(Mathf.Repeat(u * 8f, 1f) - .5f) > .49f || Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - .5f) > .49f) c *= .7f;
        return Mix(c, new Color(.08f, .08f, .08f), P(u, v, 12f, 2) * .5f);
    }

    static Color PaintTexture(float u, float v, System.Random r)
    {
        // Faded ochre paint, darker at the bottom, rust run-off under each seam.
        var ochre = new Color(.58f, .45f, .26f);
        var c = Mix(ochre, new Color(.42f, .34f, .22f), P(u, v, 3f, 5) * .8f);
        c = Mix(c, new Color(.25f, .2f, .14f), (1f - v) * .35f * P(u, v, 6f, 8));
        float col = Mathf.Floor(u * 40f);
        if (Mathf.PerlinNoise(col * .61f, 1.3f) > .66f)
            c = Mix(c, new Color(.36f, .2f, .1f), Mathf.PerlinNoise(col, v * 5f) * .55f * (1f - Mathf.Abs(u * 40f - col - .5f) * 2f));
        if (Mathf.Abs(Mathf.Repeat(v * 3f, 1f) - .5f) > .488f) c *= .8f;
        return c;
    }

    static Color HatchTexture(float u, float v, System.Random r)
    {
        var c = Mix(new Color(.36f, .14f, .08f), new Color(.22f, .09f, .055f), P(u, v, 4f, 6));
        c = Mix(c, new Color(.46f, .25f, .12f), Mathf.Max(0, P(u, v, 10f, 1) - .6f) * 2.5f);
        if (Mathf.Abs(Mathf.Repeat(u * 6f, 1f) - .5f) > .485f) c *= .62f;
        return c;
    }

    static Color SteelTexture(float u, float v, System.Random r)
    {
        float n = P(u, v, 6f, 12);
        var c = Mix(new Color(.6f, .6f, .58f), new Color(.38f, .3f, .24f), Mathf.Max(0, n - .45f) * 1.8f);
        return c;
    }

    // Stencil letters for "BRIGHTON STAR", 4 x 6 units each.
    static readonly Dictionary<char, float[][]> Glyph = new()
    {
        ['B'] = new[] { new[] { 0f, 0, 0, 6 }, new[] { 0f, 6, 2.8f, 6, 3.6f, 5.2f, 3.6f, 3.8f, 2.8f, 3, 0, 3 }, new[] { 2.8f, 3, 3.8f, 2.2f, 3.8f, .8f, 3, 0, 0, 0 } },
        ['R'] = new[] { new[] { 0f, 0, 0, 6 }, new[] { 0f, 6, 2.8f, 6, 3.6f, 5.2f, 3.6f, 3.8f, 2.8f, 3, 0, 3 }, new[] { 1.6f, 3, 3.8f, 0 } },
        ['I'] = new[] { new[] { 1.9f, 0, 1.9f, 6 }, new[] { .7f, 6, 3.1f, 6 }, new[] { .7f, 0, 3.1f, 0 } },
        ['G'] = new[] { new[] { 3.6f, 5, 2.8f, 6, 1, 6, .2f, 5, .2f, 1, 1, 0, 2.8f, 0, 3.6f, 1, 3.6f, 2.8f, 2.1f, 2.8f } },
        ['H'] = new[] { new[] { 0f, 0, 0, 6 }, new[] { 3.8f, 0, 3.8f, 6 }, new[] { 0f, 3, 3.8f, 3 } },
        ['T'] = new[] { new[] { 0f, 6, 3.8f, 6 }, new[] { 1.9f, 6, 1.9f, 0 } },
        ['O'] = new[] { new[] { 1f, 0, 2.8f, 0, 3.8f, 1, 3.8f, 5, 2.8f, 6, 1, 6, 0, 5, 0, 1, 1, 0 } },
        ['N'] = new[] { new[] { 0f, 0, 0, 6, 3.8f, 0, 3.8f, 6 } },
        ['S'] = new[] { new[] { 3.6f, 5.2f, 2.8f, 6, 1, 6, .2f, 5.2f, .2f, 3.8f, 1, 3, 2.8f, 3, 3.6f, 2.2f, 3.6f, .8f, 2.8f, 0, 1, 0, .2f, .8f } },
        ['A'] = new[] { new[] { 0f, 0, 1.9f, 6, 3.8f, 0 }, new[] { .7f, 2.2f, 3.1f, 2.2f } },
    };

    static List<(Vector2 a, Vector2 b)> nameSegments;

    static Color NameTexture(float u, float v, System.Random r)
    {
        if (nameSegments == null)
        {
            nameSegments = new List<(Vector2, Vector2)>();
            float x = 0;
            foreach (char ch in "BRIGHTON STAR")
            {
                if (ch == ' ') { x += 3f; continue; }
                foreach (var stroke in Glyph[ch])
                    for (int i = 0; i + 3 < stroke.Length; i += 2)
                        nameSegments.Add((new Vector2(x + stroke[i], stroke[i + 1]), new Vector2(x + stroke[i + 2], stroke[i + 3])));
                x += 5.2f;
            }
        }
        const float width = 66f;                    // text units across the texture
        var p = new Vector2(u * width - 1.2f, (v - .14f) / .72f * 6f);
        float d = float.MaxValue;
        foreach (var (a, b) in nameSegments)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
            d = Mathf.Min(d, (p - (a + ab * t)).magnitude);
        }
        float wear = Mathf.PerlinNoise(u * 140f, v * 18f) * .6f + Mathf.PerlinNoise(u * 40f + 9, v * 6f) * .4f;
        bool ink = d < .48f && wear > .3f;
        return new Color(1, 1, 1, ink ? 1f : 0f);
    }

    // ------------------------------------------------------------------ mesh building

    sealed class Part
    {
        public readonly List<Vector3> v = new(), n = new();
        public readonly List<Vector2> uv = new();
        public readonly List<int> t = new();
    }

    sealed class Builder
    {
        public readonly Dictionary<string, Part> parts = new();
        Part P(string mat) { if (!parts.TryGetValue(mat, out var p)) parts[mat] = p = new Part(); return p; }

        public void Tri(string mat, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            var p = P(mat); int i = p.v.Count;
            Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
            p.v.Add(a); p.v.Add(b); p.v.Add(c); p.n.Add(nrm); p.n.Add(nrm); p.n.Add(nrm);
            p.uv.Add(ua); p.uv.Add(ub); p.uv.Add(uc);
            p.t.Add(i); p.t.Add(i + 1); p.t.Add(i + 2);
        }

        public void Quad(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            var p = P(mat); int i = p.v.Count;
            Vector3 nrm = Vector3.Cross(b - a, d - a).normalized;
            p.v.AddRange(new[] { a, b, c, d }); for (int k = 0; k < 4; k++) p.n.Add(nrm);
            p.uv.AddRange(new[] { ua, ub, uc, ud });
            p.t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        /// Oriented box; UVs projected at real-world scale (tile metres per UV unit).
        public void Box(string mat, Vector3 center, Vector3 size, Quaternion rot, float tile = 2f)
        {
            Vector3 h = size * .5f;
            Vector3[] c =
            {
                new(-h.x, -h.y, -h.z), new(h.x, -h.y, -h.z), new(h.x, h.y, -h.z), new(-h.x, h.y, -h.z),
                new(-h.x, -h.y, h.z), new(h.x, -h.y, h.z), new(h.x, h.y, h.z), new(-h.x, h.y, h.z)
            };
            int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 5, 6, 7, 4 }, new[] { 4, 7, 3, 0 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 4, 0, 1, 5 } };
            foreach (var f in faces)
            {
                Vector3 a = c[f[0]], b = c[f[1]], cc = c[f[2]], d = c[f[3]];
                Vector3 nrm = Vector3.Cross(b - a, d - a).normalized;
                Vector2 Uv(Vector3 q)
                {
                    Vector3 w = q + center;
                    if (Mathf.Abs(nrm.x) > .5f) return new Vector2(w.z, w.y) / tile;
                    if (Mathf.Abs(nrm.y) > .5f) return new Vector2(w.x, w.z) / tile;
                    return new Vector2(w.x, w.y) / tile;
                }
                Quad(mat, center + rot * a, center + rot * b, center + rot * cc, center + rot * d, Uv(a), Uv(b), Uv(cc), Uv(d));
            }
        }

        /// Cylinder between two points (optionally tapering), smooth sides, flat caps.
        public void Rod(string mat, Vector3 a, Vector3 b, float ra, float rb = -1, int sides = 10, bool caps = true)
        {
            if (rb < 0) rb = ra;
            var p = P(mat);
            Vector3 axis = (b - a); float len = axis.magnitude; if (len < 1e-4f) return; axis /= len;
            Vector3 n1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
            Vector3 n2 = Vector3.Cross(axis, n1);
            int start = p.v.Count;
            for (int i = 0; i <= sides; i++)
            {
                float t = (float)i / sides * Mathf.PI * 2f;
                Vector3 d = n1 * Mathf.Cos(t) + n2 * Mathf.Sin(t);
                p.v.Add(a + d * ra); p.n.Add(d); p.uv.Add(new Vector2((float)i / sides, 0));
                p.v.Add(b + d * rb); p.n.Add(d); p.uv.Add(new Vector2((float)i / sides, len / 2f));
            }
            for (int i = 0; i < sides; i++)
            {
                int k = start + i * 2;
                p.t.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
            }
            if (!caps) return;
            for (int e = 0; e < 2; e++)
            {
                Vector3 c = e == 0 ? a : b; float r = e == 0 ? ra : rb; Vector3 nrm = e == 0 ? -axis : axis;
                int ci = p.v.Count; p.v.Add(c); p.n.Add(nrm); p.uv.Add(new Vector2(.5f, .5f));
                for (int i = 0; i <= sides; i++)
                {
                    float t = (float)i / sides * Mathf.PI * 2f;
                    p.v.Add(c + (n1 * Mathf.Cos(t) + n2 * Mathf.Sin(t)) * r); p.n.Add(nrm); p.uv.Add(new Vector2(.5f + Mathf.Cos(t) * .5f, .5f + Mathf.Sin(t) * .5f));
                }
                for (int i = 0; i < sides; i++)
                {
                    if (e == 0) p.t.AddRange(new[] { ci, ci + 2 + i, ci + 1 + i });
                    else p.t.AddRange(new[] { ci, ci + 1 + i, ci + 2 + i });
                }
            }
        }

        public void Sphere(string mat, Vector3 c, float r, int seg = 10)
        {
            var p = P(mat); int start = p.v.Count;
            for (int y = 0; y <= seg; y++)
                for (int x = 0; x <= seg * 2; x++)
                {
                    float th = Mathf.PI * y / seg, ph = Mathf.PI * x / seg;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    p.v.Add(c + d * r); p.n.Add(d); p.uv.Add(new Vector2((float)x / (seg * 2), (float)y / seg));
                }
            int row = seg * 2 + 1;
            for (int y = 0; y < seg; y++)
                for (int x = 0; x < seg * 2; x++)
                {
                    int i = start + y * row + x;
                    p.t.AddRange(new[] { i, i + 1, i + row + 1, i, i + row + 1, i + row });
                }
        }

        public Mesh Bake(string name, out Material[] materials)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var keys = parts.Keys.ToList();
            foreach (var k in keys) { v.AddRange(parts[k].v); n.AddRange(parts[k].n); uv.AddRange(parts[k].uv); }
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = keys.Count;
            int offset = 0;
            for (int s = 0; s < keys.Count; s++)
            {
                mesh.SetTriangles(parts[keys[s]].t.Select(i => i + offset).ToList(), s);
                offset += parts[keys[s]].v.Count;
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            materials = keys.Select(Mat).ToArray();
            return mesh;
        }
    }

    // ------------------------------------------------------------------ hull

    static float T(float z) => -z / (L * .5f);                       // +1 at the bow, -1 at the stern
    static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
    static float DeckY(float z) { float t = T(z); return Freeboard + (t > 0 ? .85f * t * t : .4f * t * t); }
    static float KeelY(float z)
    {
        float t = T(z);
        if (t > .8f) return Mathf.Lerp(-Draft, DeckY(z) - .2f, Mathf.Pow((t - .8f) / .2f, 2.2f));
        if (t < -.7f) return Mathf.Lerp(-Draft, .35f, Mathf.Pow((-t - .7f) / .3f, 1.5f));
        return -Draft;
    }

    /// Hull half-breadth factor (0..1) at station z for height fraction h (0 keel .. 1 deck).
    static float Width(float z, float h)
    {
        float t = T(z);
        if (t > .28f)
        {
            float f = Mathf.Clamp01((t - .28f) / .72f);
            float bottom = 1f - Mathf.Pow(f, 1.6f), top = 1f - Mathf.Pow(f, 2.5f);
            return Mathf.Max(0f, Mathf.Lerp(bottom, top, Mathf.Pow(h, .8f)));
        }
        if (t < -.45f)
        {
            float f = Mathf.Clamp01((-t - .45f) / .55f);
            float bottom = 1f - .94f * Mathf.Pow(f, 1.3f), top = 1f - .45f * Mathf.Pow(f, 2.2f);
            return Mathf.Lerp(bottom, top, Mathf.Pow(h, .55f));
        }
        return 1f;
    }

    /// A point on the hull surface: station z, s in [-1, 1] around the section (port deck edge .. keel .. starboard deck edge).
    static Vector3 HullPoint(float z, float s)
    {
        float keel = KeelY(z), deck = DeckY(z), depth = deck - keel;
        float side = Mathf.Sign(s); float a = Mathf.Abs(s);              // 0 keel centre .. 1 deck edge
        // Section: flat bottom (a < .25), bilge arc (.25 .. .5), side (.5 .. 1)
        float hb = B * .5f;
        float r = Mathf.Min(1.1f, depth * .45f);
        Vector2 q;
        if (a < .25f) q = new Vector2(Mathf.Lerp(0, hb - r, a / .25f), Mathf.Lerp(0, .12f, a / .25f));
        else if (a < .5f)
        {
            float ang = Mathf.Lerp(-Mathf.PI * .5f, 0, (a - .25f) / .25f);
            q = new Vector2(hb - r + Mathf.Cos(ang) * r, r + Mathf.Sin(ang) * (r - .12f) + 0f);
        }
        else q = new Vector2(hb, Mathf.Lerp(r, depth, (a - .5f) / .5f));
        float h = Mathf.Clamp01(q.y / depth);
        float w = Width(z, h);
        float t = T(z);
        float zOff = -h * 1.9f * Smooth(.72f, 1f, t) + h * 1.1f * Smooth(.68f, 1f, -t);   // raked stem, stern counter
        return new Vector3(side * q.x * w, keel + q.y, z + zOff);
    }

    static void BuildHull(Builder b)
    {
        const int stations = 72, ring = 40;
        var grid = new Vector3[stations + 1, ring + 1];
        for (int i = 0; i <= stations; i++)
        {
            float z = Mathf.Lerp(-L * .5f, L * .5f, (float)i / stations);
            for (int j = 0; j <= ring; j++) grid[i, j] = HullPoint(z, Mathf.Lerp(-1, 1, (float)j / ring));
        }
        // Smooth-shaded shell: shared vertices per material band so normals blend.
        var shell = new Dictionary<string, (List<Vector3> v, List<Vector2> uv, List<int> t)>();
        foreach (var key in new[] { "hull", "boot" }) shell[key] = (new List<Vector3>(), new List<Vector2>(), new List<int>());
        var index = new Dictionary<(string, int, int), int>();
        int V(string mat, int i, int j)
        {
            if (index.TryGetValue((mat, i, j), out int k)) return k;
            var s = shell[mat]; k = s.v.Count; s.v.Add(grid[i, j]);
            s.uv.Add(new Vector2(grid[i, j].z / 6f, grid[i, j].y / 3f));
            index[(mat, i, j)] = k; return k;
        }
        for (int i = 0; i < stations; i++)
            for (int j = 0; j < ring; j++)
            {
                float y = (grid[i, j].y + grid[i + 1, j].y + grid[i, j + 1].y + grid[i + 1, j + 1].y) * .25f;
                string mat = y < .38f ? "boot" : "hull";
                var s = shell[mat];
                int a = V(mat, i, j), bb = V(mat, i + 1, j), c = V(mat, i + 1, j + 1), d = V(mat, i, j + 1);
                s.t.AddRange(new[] { a, d, c, a, c, bb });
            }
        foreach (var kv in shell)
        {
            var m = new Mesh { indexFormat = IndexFormat.UInt32 };
            m.SetVertices(kv.Value.v); m.SetUVs(0, kv.Value.uv); m.SetTriangles(kv.Value.t, 0);
            m.RecalculateNormals();
            var part = new Part();
            part.v.AddRange(m.vertices); part.n.AddRange(m.normals); part.uv.AddRange(kv.Value.uv); part.t.AddRange(kv.Value.t);
            Object.DestroyImmediate(m);
            if (!b.parts.TryGetValue(kv.Key, out var existing)) b.parts[kv.Key] = part;
            else { int off = existing.v.Count; existing.v.AddRange(part.v); existing.n.AddRange(part.n); existing.uv.AddRange(part.uv); existing.t.AddRange(part.t.Select(x => x + off)); }
        }
        // Transom: close the stern section.
        var centre = new Vector3(0, (KeelY(L * .5f) + DeckY(L * .5f)) * .5f, L * .5f + .55f);
        for (int j = 0; j < ring; j++)
            b.Tri("hull", grid[stations, j], centre, grid[stations, j + 1], Vector2.zero, new Vector2(.5f, .5f), Vector2.one);
        // Deck plating between the deck edges.
        for (int i = 0; i < stations; i++)
        {
            Vector3 p0 = grid[i, 0], p1 = grid[i + 1, 0], s0 = grid[i, ring], s1 = grid[i + 1, ring];
            p0.y -= .05f; p1.y -= .05f; s0.y -= .05f; s1.y -= .05f;
            b.Quad("deck", p0, s0, s1, p1, new Vector2(p0.x, p0.z) / 3f, new Vector2(s0.x, s0.z) / 3f, new Vector2(s1.x, s1.z) / 3f, new Vector2(p1.x, p1.z) / 3f);
        }
        // Rubbing strake and bulwark top at the deck edge, solid bulwark on the forecastle.
        for (int side = 0; side < 2; side++)
            for (int i = 0; i < stations; i++)
            {
                int j = side == 0 ? 0 : ring;
                Vector3 a = grid[i, j], c = grid[i + 1, j];
                b.Rod("dark", a + Vector3.up * -.25f, c + Vector3.up * -.25f, .07f, -1, 6, false);
                float t = T((a.z + c.z) * .5f);
                if (t > .55f)
                {
                    Vector3 up = Vector3.up * .95f;
                    var n = new Vector3(Mathf.Sign(a.x), 0, 0);
                    b.Quad("hull", a, c, c + up, a + up, new Vector2(a.z / 6f, a.y / 3f), new Vector2(c.z / 6f, c.y / 3f), new Vector2(c.z / 6f, (c.y + .95f) / 3f), new Vector2(a.z / 6f, (a.y + .95f) / 3f));
                    b.Quad("hull", c, a, a + up, c + up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                    b.Box("dark", (a + c) * .5f + up, new Vector3(.12f, .06f, (c - a).magnitude + .02f), Quaternion.LookRotation(c - a), 1f);
                }
            }
    }

    static Vector3 DeckEdge(float z, float side, float inset = 0f)
    {
        var p = HullPoint(z, side);
        p.x -= Mathf.Sign(p.x) * inset;
        return p;
    }

    static void Railing(Builder b, IList<Vector3> path, float height = 1f, float spacing = 1.35f)
    {
        float carried = 0;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector3 a = path[i], c = path[i + 1];
            float len = (c - a).magnitude;
            for (float h = .38f; h <= height + .01f; h += (height - .38f) / 2f)
                b.Rod("steel", a + Vector3.up * h, c + Vector3.up * h, .022f, -1, 5, false);
            while (carried <= len)
            {
                Vector3 p = Vector3.Lerp(a, c, carried / len);
                b.Rod("steel", p, p + Vector3.up * height, .026f, -1, 5, true);
                carried += spacing;
            }
            carried -= len;
        }
    }

    // ------------------------------------------------------------------ superstructure & fittings

    static void Deckhouse(Builder b, float z0, float z1, float width, float y0, float height, int windows, bool lit, bool portholes)
    {
        b.Box("paint", new Vector3(0, y0 + height * .5f, (z0 + z1) * .5f), new Vector3(width, height, z1 - z0), Quaternion.identity, 3f);
        // Roof slab with a small overhang and a dark drip edge.
        b.Box("dark", new Vector3(0, y0 + height + .05f, (z0 + z1) * .5f), new Vector3(width + .28f, .1f, z1 - z0 + .28f), Quaternion.identity, 2f);
        var rnd = new System.Random((int)(z0 * 100 + y0 * 10));
        float wy = y0 + height * (portholes ? .58f : .6f);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int k = 0; k < windows; k++)
            {
                float z = Mathf.Lerp(z0 + .7f, z1 - .7f, windows == 1 ? .5f : (float)k / (windows - 1));
                bool on = lit && rnd.NextDouble() < .45;
                var face = new Vector3(side * (width * .5f + .012f), wy, z);
                if (portholes)
                {
                    b.Rod(on ? "lit" : "glass", face - new Vector3(side * .02f, 0, 0), face + new Vector3(side * .02f, 0, 0), .2f, -1, 14);
                    b.Rod("steel", face - new Vector3(side * .01f, 0, 0), face + new Vector3(side * .035f, 0, 0), .25f, -1, 14, false);
                }
                else
                {
                    b.Box(on ? "lit" : "glass", face, new Vector3(.03f, .75f, 1.05f), Quaternion.identity, 1f);
                    b.Box("dark", face + new Vector3(side * .01f, .42f, 0), new Vector3(.05f, .06f, 1.2f), Quaternion.identity, 1f);
                }
            }
        }
    }

    static void Superstructure(Builder b, List<Light> lights, Transform root)
    {
        float zf = 5.1f, ze = 14.2f;                        // forward and aft faces of the main deckhouse
        float deck = DeckY(zf) - .25f;
        // Tier 1: main deckhouse with portholes.
        Deckhouse(b, zf, ze, 5.9f, deck, 2.35f, 5, true, true);
        float y1 = deck + 2.45f;
        // Tier 2: accommodation with windows, open deck around it.
        Deckhouse(b, zf + .6f, ze - 1.4f, 5.2f, y1, 2.15f, 4, true, false);
        float y2 = y1 + 2.25f;
        // Tier 3: wheelhouse with wrap-around windows and bridge wings.
        float wz0 = zf + .8f, wz1 = zf + 4.6f;
        b.Box("paint", new Vector3(0, y2 + 1.05f, (wz0 + wz1) * .5f), new Vector3(4.6f, 2.1f, wz1 - wz0), Quaternion.identity, 3f);
        b.Box("dark", new Vector3(0, y2 + 2.18f, (wz0 + wz1) * .5f), new Vector3(5.1f, .12f, wz1 - wz0 + .5f), Quaternion.identity, 2f);
        for (int k = 0; k < 5; k++)        // front windows
        {
            float x = Mathf.Lerp(-1.8f, 1.8f, k / 4f);
            b.Box(k % 2 == 0 ? "lit" : "glass", new Vector3(x, y2 + 1.35f, wz0 - .012f), new Vector3(.72f, .78f, .03f), Quaternion.identity, 1f);
        }
        for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 3; k++)
                b.Box(k == 1 ? "lit" : "glass", new Vector3(side * 2.312f, y2 + 1.35f, Mathf.Lerp(wz0 + .6f, wz1 - .6f, k / 2f)), new Vector3(.03f, .78f, .8f), Quaternion.identity, 1f);
        b.Box("paint", new Vector3(0, y2 - .05f, wz0 + .6f), new Vector3(B * .98f, .14f, 1.4f), Quaternion.identity, 2f);   // bridge wings
        Railing(b, new[] { new Vector3(-B * .49f, y2 + .02f, wz0 - .1f), new Vector3(B * .49f, y2 + .02f, wz0 - .1f) }, .95f);
        // Rails around tier 1 and tier 2 roofs.
        foreach (var (w, y, z0, z1) in new[] { (5.9f, y1, zf, ze), (5.2f, y2, zf + .6f, ze - 1.4f) })
        {
            float hw = w * .5f + .08f;
            Railing(b, new[] { new Vector3(-hw, y, z0 - .1f), new Vector3(-hw, y, z1 + .1f), new Vector3(hw, y, z1 + .1f), new Vector3(hw, y, z0 - .1f) }, .9f);
        }
        // Funnel and its black top.
        float fz = ze - 2.6f;
        b.Box("steel", new Vector3(0, y2 + 1.4f, fz), new Vector3(1.3f, 2.8f, 1.8f), Quaternion.Euler(-6, 0, 0), 2f);
        b.Box("dark", new Vector3(0, y2 + 2.95f, fz + .16f), new Vector3(1.36f, .45f, 1.86f), Quaternion.Euler(-6, 0, 0), 2f);
        b.Rod("dark", new Vector3(.3f, y2 + 2.9f, fz + .1f), new Vector3(.3f, y2 + 3.5f, fz + .2f), .14f);
        b.Rod("dark", new Vector3(-.3f, y2 + 2.9f, fz + .1f), new Vector3(-.3f, y2 + 3.4f, fz + .2f), .11f);
        // Radar mast on the wheelhouse roof.
        float rz = (wz0 + wz1) * .5f + .4f, ry = y2 + 2.24f;
        b.Rod("steel", new Vector3(0, ry, rz), new Vector3(0, ry + 3.2f, rz), .07f);
        b.Rod("steel", new Vector3(-1.1f, ry + 2.2f, rz), new Vector3(1.1f, ry + 2.2f, rz), .04f);
        b.Box("dark", new Vector3(0, ry + 1.35f, rz), new Vector3(1.9f, .14f, .26f), Quaternion.identity, 1f);
        b.Sphere("lamp", new Vector3(0, ry + 3.28f, rz), .09f, 8);
        b.Sphere("lamp", new Vector3(-1.1f, ry + 2.28f, rz), .06f, 6);
        b.Sphere("lamp", new Vector3(1.1f, ry + 2.28f, rz), .06f, 6);
        b.Rod("steel", new Vector3(0, ry + 3.2f, rz), new Vector3(0, ry + 3.9f, rz), .025f);
        // Doors and a ladder on tier 1.
        for (int side = -1; side <= 1; side += 2)
        {
            b.Box("dark", new Vector3(side * 2.965f, deck + 1f, zf + .9f), new Vector3(.04f, 1.9f, .8f), Quaternion.identity, 1f);
            float lx = side * 2.6f;
            for (float y = deck + .3f; y < y1; y += .3f) b.Rod("steel", new Vector3(lx - .2f, y, ze + .12f), new Vector3(lx + .2f, y, ze + .12f), .02f, -1, 4, false);
            b.Rod("steel", new Vector3(lx - .2f, deck, ze + .12f), new Vector3(lx - .2f, y1 + .9f, ze + .12f), .025f);
            b.Rod("steel", new Vector3(lx + .2f, deck, ze + .12f), new Vector3(lx + .2f, y1 + .9f, ze + .12f), .025f);
        }
        // Lifeboat on davits, pier side and seaward side.
        for (int side = -1; side <= 1; side += 2)
        {
            float bx = side * 3.35f, by = y1 + .55f, bz = zf + 4.4f;
            var hull = new List<Vector3>();
            b.Rod("boat", new Vector3(bx, by, bz - 1.75f), new Vector3(bx, by, bz + 1.75f), .58f, .58f, 14);
            b.Sphere("boat", new Vector3(bx, by, bz - 1.75f), .58f, 10);
            b.Sphere("boat", new Vector3(bx, by, bz + 1.75f), .58f, 10);
            b.Box("paint", new Vector3(bx, by + .5f, bz), new Vector3(1.05f, .2f, 3.2f), Quaternion.identity, 1f);
            for (int k = -1; k <= 1; k += 2)
            {
                float dz = bz + k * 1.3f;
                b.Rod("dark", new Vector3(side * 2.7f, y1, dz), new Vector3(side * 2.7f, y1 + 1.6f, dz), .06f);
                b.Rod("dark", new Vector3(side * 2.7f, y1 + 1.6f, dz), new Vector3(bx, y1 + 1.7f, dz), .05f);
                b.Rod("rope", new Vector3(bx, y1 + 1.7f, dz), new Vector3(bx, by + .55f, dz), .012f, -1, 4, false);
            }
        }
        // Deck floodlights under the bridge wings.
        foreach (var pos in new[] { new Vector3(-2.8f, y2 - .25f, wz0 - .1f), new Vector3(2.8f, y2 - .25f, wz0 - .1f) })
        {
            b.Box("lamp", pos, new Vector3(.22f, .14f, .1f), Quaternion.identity, 1f);
            lights.Add(MakeLight(root, "Deck floodlight", pos + new Vector3(0, -.1f, -.2f), new Color(1f, .82f, .55f), 1.6f, 9f));
        }
    }

    static Light MakeLight(Transform root, string name, Vector3 pos, Color c, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    static void Hatches(Builder b)
    {
        float z0 = -9.6f, z1 = 4.2f, w = B * .64f;
        float deck = DeckY(-3f) - .05f;
        // Coaming
        b.Box("dark", new Vector3(0, deck + .42f, (z0 + z1) * .5f), new Vector3(w, .84f, z1 - z0), Quaternion.identity, 2f);
        int panels = 6;
        float pl = (z1 - z0) / panels;
        for (int i = 0; i < panels; i++)
        {
            float zc = z0 + pl * (i + .5f);
            // Slightly peaked cover, two halves meeting on the centre line.
            for (int s = -1; s <= 1; s += 2)
                b.Box("hatch", new Vector3(s * w * .25f, deck + .92f + .05f, zc), new Vector3(w * .5f + .04f, .16f, pl - .1f), Quaternion.Euler(0, 0, -s * 3f), 2f);
            for (int r = 0; r < 3; r++)       // stiffeners
            {
                float rz = zc + (r - 1) * pl * .3f;
                b.Box("hatch", new Vector3(0, deck + 1.06f, rz), new Vector3(w * .98f, .07f, .09f), Quaternion.identity, 1f);
            }
            b.Box("dark", new Vector3(0, deck + 1.0f, zc - pl * .5f + .02f), new Vector3(w + .06f, .1f, .06f), Quaternion.identity, 1f);
            for (int s = -1; s <= 1; s += 2) b.Box("steel", new Vector3(s * (w * .5f + .03f), deck + .86f, zc), new Vector3(.06f, .1f, .5f), Quaternion.identity, 1f);
        }
    }

    static void Foremast(Builder b, List<Light> lights, Transform root)
    {
        float mz = -11.4f, deck = DeckY(mz) - .05f, top = deck + 10.8f;
        var foot = new Vector3(0, deck, mz);
        var head = new Vector3(0, top, mz);
        b.Rod("steel", foot, head, .2f, .11f, 12);
        b.Box("dark", foot + Vector3.up * .3f, new Vector3(.9f, .6f, .9f), Quaternion.identity, 1f);
        b.Rod("steel", new Vector3(-1.15f, top - 2.6f, mz), new Vector3(1.15f, top - 2.6f, mz), .05f);     // crosstree
        b.Box("dark", head + Vector3.up * .08f, new Vector3(.34f, .16f, .34f), Quaternion.identity, 1f);
        b.Rod("lamp", head + Vector3.up * .16f, head + Vector3.up * .45f, .1f, .1f, 10);
        b.Box("dark", head + Vector3.up * .52f, new Vector3(.3f, .06f, .3f), Quaternion.identity, 1f);
        lights.Add(MakeLight(root, "Mast lamp", head + Vector3.up * .3f, new Color(1f, .84f, .58f), 3.2f, 16f));
        // Cargo boom: heel on the mast, head raised over the hatches.
        var heel = new Vector3(0, deck + 2.4f, mz + .25f);
        var boomHead = heel + Quaternion.Euler(-38f, 0, 0) * Vector3.forward * 9.2f;
        b.Rod("boom", heel, boomHead, .17f, .11f, 12);
        b.Box("dark", heel, new Vector3(.36f, .36f, .36f), Quaternion.identity, 1f);
        b.Rod("rope", head - Vector3.up * .3f, boomHead, .018f, -1, 5, false);                  // topping lift
        b.Rod("rope", boomHead, boomHead - Vector3.up * 3.2f, .015f, -1, 5, false);             // cargo runner
        b.Box("dark", boomHead - Vector3.up * 3.3f, new Vector3(.2f, .25f, .14f), Quaternion.identity, 1f);
        // Stays to the bow and the deck edges, shrouds from the crosstree.
        b.Rod("rope", head, new Vector3(0, DeckY(-15.2f) + .9f, -15.6f), .016f, -1, 5, false);
        foreach (float side in new[] { -1f, 1f })
        {
            b.Rod("rope", head - Vector3.up * .2f, DeckEdge(mz + 1.5f, side, .25f) + Vector3.up * .1f, .016f, -1, 5, false);
            b.Rod("rope", new Vector3(side * 1.15f, top - 2.6f, mz), DeckEdge(mz - .8f, side, .25f) + Vector3.up * .1f, .014f, -1, 5, false);
        }
        // Long stay from the masthead to the funnel top / radar mast.
        b.Rod("rope", head, new Vector3(0, DeckY(9f) + 9.1f, 10.4f), .014f, -1, 5, false);
    }

    static void Fittings(Builder b)
    {
        // Windlass and bollards on the forecastle, bollards aft, fairleads at the rails.
        float fz = -13.3f, fd = DeckY(fz) - .05f;
        b.Box("dark", new Vector3(0, fd + .4f, fz), new Vector3(1.6f, .8f, .9f), Quaternion.identity, 1f);
        b.Rod("steel", new Vector3(-1.05f, fd + .45f, fz), new Vector3(1.05f, fd + .45f, fz), .22f, -1, 12);
        foreach (var z in new[] { -12.6f, -1f, 12.3f, 14.6f })
            foreach (float side in new[] { -1f, 1f })
            {
                var p = DeckEdge(z, side, .55f); float d = DeckY(z) - .05f;
                b.Rod("dark", new Vector3(p.x, d, z - .22f), new Vector3(p.x, d + .55f, z - .22f), .13f);
                b.Rod("dark", new Vector3(p.x, d, z + .22f), new Vector3(p.x, d + .55f, z + .22f), .13f);
                b.Box("dark", new Vector3(p.x, d + .06f, z), new Vector3(.36f, .12f, .8f), Quaternion.identity, 1f);
            }
        // Hawse pipes and stocked anchors on both bows.
        foreach (float side in new[] { -1f, 1f })
        {
            float z = -13.9f; var edge = HullPoint(z, side * .82f);
            var outN = new Vector3(side, 0, -.35f).normalized;
            b.Rod("dark", edge + outN * .02f, edge + outN * .1f, .3f, .34f, 14);
            b.Box("dark", edge + outN * .16f + Vector3.down * .45f, new Vector3(.12f, .9f, .26f), Quaternion.LookRotation(outN), 1f);
            b.Box("dark", edge + outN * .2f + Vector3.down * .9f, new Vector3(.12f, .14f, .75f), Quaternion.LookRotation(outN) * Quaternion.Euler(0, 90, 0), 1f);
            for (int k = 0; k < 6; k++) b.Rod("rope", edge + outN * .1f + Vector3.down * (k * .1f), edge + outN * .13f + Vector3.down * (k * .1f + .1f), .03f, -1, 5);
        }
        // Mooring line fairleads on the pier side.
        foreach (var z in new[] { -12.6f, 12.3f })
        {
            var p = DeckEdge(z, 1f, .05f);
            b.Rod("steel", p + new Vector3(0, -.05f, -.2f), p + new Vector3(0, .25f, -.2f), .06f);
            b.Rod("steel", p + new Vector3(0, -.05f, .2f), p + new Vector3(0, .25f, .2f), .06f);
        }
    }

    static void Name(Builder b)
    {
        // Stencilled on both sides of the forward hull, reading bow to stern.
        float z0 = -9.2f, z1 = 1.3f, y0 = Freeboard - 1.55f, y1 = Freeboard - .55f;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (B * .5f + .015f);
            var a = new Vector3(x, y0, z0); var bb = new Vector3(x, y0, z1); var c = new Vector3(x, y1, z1); var d = new Vector3(x, y1, z0);
            if (side > 0) b.Quad("name", a, bb, c, d, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            else b.Quad("name", bb, a, d, c, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
        }
    }

    static List<Vector3> DeckRail(float side)
    {
        var path = new List<Vector3>();
        for (float z = -12.4f; z <= 14.9f; z += .8f) path.Add(DeckEdge(z, side, .12f) + Vector3.up * -.03f);
        return path;
    }

    // ------------------------------------------------------------------ assembly

    static void Run(StringBuilder log)
    {
        nameSegments = null;
        MakeMaterials();
        var root = new GameObject("Moored_Cargo_Ship");
        var lights = new List<Light>();
        var b = new Builder();
        BuildHull(b);
        Hatches(b);
        Superstructure(b, lights, root.transform);
        Foremast(b, lights, root.transform);
        Fittings(b);
        Railing(b, DeckRail(-1f), .95f);
        Railing(b, DeckRail(1f), .95f);
        // Stern rail across the poop.
        Railing(b, new[] { DeckEdge(14.9f, -1f, .12f), new Vector3(0, DeckY(15.3f) - .03f, 15.4f), DeckEdge(14.9f, 1f, .12f) }, .95f);
        Name(b);

        var mesh = b.Bake("Brighton Star", out var materials);
        string meshPath = Dir + "/Brighton Star.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (old) { old.Clear(); EditorUtility.CopySerialized(mesh, old); mesh = old; }
        else AssetDatabase.CreateAsset(mesh, meshPath);
        var body = new GameObject("Brighton Star");
        body.transform.SetParent(root.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = body.AddComponent<MeshRenderer>();
        r.sharedMaterials = materials;
        r.shadowCastingMode = ShadowCastingMode.On;
        log.AppendLine($"mesh verts {mesh.vertexCount} submeshes {mesh.subMeshCount} bounds {mesh.bounds.center} {mesh.bounds.size} lights {lights.Count}");

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        log.AppendLine("prefab saved " + PrefabPath);

        // Retire the old ship's separate deck lights and painted names: the new model carries its own.
        var active = SceneManager.GetActiveScene();
        bool opened = false;
        if (active.path != Harbor && !Application.isBatchMode)
        {
            if (active.isDirty) { log.AppendLine("scene left as is (open scene has unsaved changes)"); return; }
        }
        if (SceneManager.GetActiveScene().path != Harbor) { EditorSceneManager.OpenScene(Harbor, OpenSceneMode.Single); opened = true; }
        var scene = SceneManager.GetActiveScene();
        var group = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Deck lighting and painted names");
        if (group && group.gameObject.activeSelf)
        {
            group.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("old deck lighting and painted names hidden; scene saved");
        }
        log.AppendLine("opened harbour: " + opened);

        // Review shots from the pier and the water.
        var ship = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Moored Cargo Ship");
        if (ship)
        {
            var cam = Camera.main;
            Vector3 P(Vector3 local) => ship.TransformPoint(local);
            var shots = new List<Texture2D>
            {
                HeroReviewCapture.Render(cam, P(new Vector3(26f, 6f, 2f)), P(new Vector3(0, 3f, 0)), 50f, 800, 450),
                HeroReviewCapture.Render(cam, P(new Vector3(9f, 2.6f, -24f)), P(new Vector3(0, 3f, -4f)), 50f, 800, 450),
                HeroReviewCapture.Render(cam, P(new Vector3(7f, 5.5f, 22f)), P(new Vector3(0, 4.5f, 8f)), 50f, 800, 450),
                HeroReviewCapture.Render(cam, P(new Vector3(-30f, 7f, -6f)), P(new Vector3(0, 3f, 0)), 50f, 800, 450),
                HeroReviewCapture.Render(cam, P(new Vector3(6.5f, 4.5f, -5f)), P(new Vector3(0, 2.5f, -9f)), 55f, 800, 450),
                HeroReviewCapture.Render(cam, P(new Vector3(4.6f, 9.5f, 1f)), P(new Vector3(0, 6.5f, 9f)), 55f, 800, 450),
            };
            log.AppendLine("sheet " + HeroReviewCapture.Sheet("ship_new", shots, 2));
        }
    }

    public static void BatchBuild() => Build();
}
