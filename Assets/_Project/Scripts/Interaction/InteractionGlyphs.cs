using System.Collections.Generic;
using System.Text.RegularExpressions;
using DetectiveGame.Core;
using UnityEngine;

namespace CarXCase.Interaction
{
    /// Lets an interactable choose where its key hangs (a lock, a handle, a button).
    public interface IGlyphAnchor { Vector3 GlyphPosition { get; } }

    /// Heavy Rain / Detroit key prompts: a solid 3D key (rounded frame, dark plate, raised letter)
    /// floats in the air next to the object. It spins in when Alexey comes close, drifts and sways
    /// so its depth reads, sinks in when pressed, and the camera leans gently towards it.
    /// Callers request a glyph every frame it should stay visible; it leaves on its own afterwards.
    [DefaultExecutionOrder(10000)]
    public sealed class InteractionGlyphs : MonoBehaviour
    {
        sealed class Glyph
        {
            public string key;
            public Vector3 anchor;
            public bool onScreen;
            public Vector2 viewport;
            public float screenHeight;
            public float progress = -1f;
            public bool urgent, attend;
            public int requestFrame;
            public float appear, pulse, fail, holdUntil = -1f, seed;
            public Vector3 smoothPosition;
            public bool placed;
        }

        sealed class KeyModel { public Mesh frame, plate, letter; public float width; }

        // Key proportions in local units (height of the key = 1).
        const float Depth = .1f, Corner = .19f, Rim = .045f;
        static readonly Regex PromptPattern = new Regex(@"^\s*\[?(?<k>[A-Za-z]|Tab|Esc|Space|Пробел|ПРОБЕЛ)\]?(?:\s*[—–:\-]\s*|\s+)");

        static InteractionGlyphs instance;
        static bool failed;
        static readonly Dictionary<Component, Vector3> anchorCache = new Dictionary<Component, Vector3>();

        readonly Dictionary<object, Glyph> glyphs = new Dictionary<object, Glyph>();
        readonly Dictionary<string, KeyModel> models = new Dictionary<string, KeyModel>();
        readonly List<object> finished = new List<object>();
        Material clearMat, solidMat;
        Mesh bar;
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        int visible;

        public static bool AnyVisible => instance && instance.visible > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; failed = false; anchorCache.Clear(); }

        static InteractionGlyphs Get()
        {
            if (instance || failed || !Application.isPlaying) return instance;
            var go = new GameObject("Interaction Glyphs") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<InteractionGlyphs>();
            if (!instance.Setup()) { failed = true; Destroy(go); instance = null; }
            return instance;
        }

        static Glyph Request(object owner, string key)
        {
            var self = Get();
            if (!self || owner == null || string.IsNullOrEmpty(key)) return null;
            if (!self.glyphs.TryGetValue(owner, out var g))
            {
                g = new Glyph { seed = Random.value * 10f };
                self.glyphs[owner] = g;
            }
            g.key = NormalizeKey(key);
            g.requestFrame = Time.frameCount;
            return g;
        }

        /// A key hanging in the scene at a world point. The camera leans towards it unless it is urgent (QTE).
        public static void Show(object owner, Vector3 position, string key, string label = null, float progress = -1f, bool urgent = false)
        {
            var g = Request(owner, key);
            if (g == null) return;
            if (g.onScreen) g.placed = false;
            g.onScreen = false;
            g.anchor = position;
            g.progress = progress;
            g.urgent = urgent;
            g.attend = !urgent;
        }

        /// A key pinned to the frame (viewport 0..1), height as a share of the screen. Used by the diary hint.
        public static void ShowOnScreen(object owner, Vector2 viewport, float height, string key)
        {
            var g = Request(owner, key);
            if (g == null) return;
            g.onScreen = true;
            g.viewport = viewport;
            g.screenHeight = height;
            g.progress = -1f;
            g.urgent = g.attend = false;
        }

        /// Accepts the game's prompt strings ("E  Осмотреть замок", "[E] Открыть"). Prompts without a key show nothing.
        public static void ShowPrompt(object owner, Vector3 position, string prompt)
        {
            var key = KeyOf(prompt);
            if (key != null) Show(owner, position, key);
        }

        public static string KeyOf(string prompt)
        {
            var m = PromptPattern.Match(prompt ?? "");
            return m.Success ? m.Groups["k"].Value : null;
        }

        static string NormalizeKey(string key) =>
            key == "ПРОБЕЛ" || key == "Пробел" || key == "Space" ? "Space" : key.Length == 1 ? key.ToUpperInvariant() : key;

        public static void Press(object owner)
        {
            if (owner != null && instance && instance.glyphs.TryGetValue(owner, out var g)) g.pulse = 1f;
        }

        /// Holds the key a moment after a QTE: it sinks in on success, shakes red on a miss.
        public static void Finish(object owner, bool success)
        {
            if (owner == null || !instance || !instance.glyphs.TryGetValue(owner, out var g)) return;
            if (success) g.pulse = 1f; else g.fail = 1f;
            g.holdUntil = Time.unscaledTime + .55f;
        }

        /// A stable point to hang a key on: IGlyphAnchor, a GlyphAnchor child, or the visible bounds.
        public static Vector3 PointOf(Component target)
        {
            if (!target) return Vector3.zero;
            if (target is IGlyphAnchor custom) return custom.GlyphPosition;
            if (anchorCache.TryGetValue(target, out var local)) return target.transform.TransformPoint(local);

            Vector3 point;
            var pinned = target.GetComponentInChildren<GlyphAnchor>();
            if (pinned) point = pinned.transform.position;
            else
            {
                bool has = false;
                var bounds = new Bounds(target.transform.position, Vector3.zero);
                foreach (var r in target.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer || !r.enabled) continue;
                    if (!has) { bounds = r.bounds; has = true; } else bounds.Encapsulate(r.bounds);
                }
                if (!has)
                    foreach (var c in target.GetComponentsInChildren<Collider>())
                    {
                        if (!has) { bounds = c.bounds; has = true; } else bounds.Encapsulate(c.bounds);
                    }
                point = bounds.center;
                // Tall things (doors, people, boards): about chest height, not mid-air above them.
                if (bounds.size.y > 1.4f) point.y = Mathf.Min(bounds.max.y, bounds.min.y + 1.35f);
            }
            anchorCache[target] = target.transform.InverseTransformPoint(point);
            return point;
        }

        bool Setup()
        {
            var shader = Resources.Load<Shader>("CarXInteractionGlyph");
            if (!shader || !shader.isSupported) { Debug.LogError("[Glyphs] Missing shader Resources/CarXInteractionGlyph."); return false; }
            clearMat = new Material(shader) { renderQueue = 3400, hideFlags = HideFlags.DontSave };
            clearMat.SetFloat("_ClearDepth", 1f);
            clearMat.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
            clearMat.SetFloat("_ColorMaskMode", 0f);
            solidMat = new Material(shader) { renderQueue = 3401, hideFlags = HideFlags.DontSave };
            solidMat.SetFloat("_ClearDepth", 0f);
            solidMat.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            solidMat.SetFloat("_ColorMaskMode", 15f);
            var b = new MeshBuilder();
            b.Box(new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, .5f, .5f));
            bar = b.Build();
            block = new MaterialPropertyBlock();
            return true;
        }

        void OnDestroy()
        {
            foreach (var m in models.Values) { Destroy(m.frame); Destroy(m.plate); Destroy(m.letter); }
            if (bar) Destroy(bar);
            if (clearMat) Destroy(clearMat);
            if (solidMat) Destroy(solidMat);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            visible = 0;
            bool worldKeyWanted = false;
            foreach (var g in glyphs.Values)
                worldKeyWanted |= !g.onScreen && g.requestFrame >= Time.frameCount - 1;
            Glyph focus = null;
            float focusDistance = float.MaxValue;
            foreach (var pair in glyphs)
            {
                var g = pair.Value;
                bool wanted = g.requestFrame >= Time.frameCount - 1;
                bool shown = wanted || t < g.holdUntil;
                // A world key that lost its target leaves fast when another one takes over: one E at a time.
                float leave = !g.onScreen && worldKeyWanted ? 12f : 4f;
                g.appear = Mathf.MoveTowards(g.appear, shown ? 1f : 0f, dt * (shown ? 3.2f : leave));
                g.pulse = Mathf.Max(0f, g.pulse - dt * 2.6f);
                g.fail = Mathf.Max(0f, g.fail - dt * 2f);
                if (!shown && g.appear <= 0f) { finished.Add(pair.Key); continue; }
                if (wanted) visible++;
                if (!cam) continue;
                Draw(cam, g, t, dt);
                if (wanted && g.attend)
                {
                    float d = (g.anchor - cam.transform.position).sqrMagnitude;
                    if (d < focusDistance) { focusDistance = d; focus = g; }
                }
            }
            foreach (var key in finished) glyphs.Remove(key);
            finished.Clear();

            // The shot leans towards the thing Alexey can act on, the way Heavy Rain frames an action.
            if (focus != null && cam && cam.TryGetComponent<ThirdPersonCamera>(out var follow))
                follow.Attend(focus.anchor);
        }

        void Draw(Camera cam, Glyph g, float t, float dt)
        {
            var view = cam.transform;
            var model = Model(g.key);
            Vector3 position;
            float height;
            if (g.onScreen)
            {
                const float distance = .6f;
                position = cam.ViewportToWorldPoint(new Vector3(g.viewport.x, g.viewport.y, distance));
                height = 2f * distance * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * g.screenHeight;
            }
            else
            {
                // Hang a little in front of the object, towards the lens, so it floats in the air.
                Vector3 toCam = view.position - g.anchor;
                float distance = toCam.magnitude;
                if (Vector3.Dot(-toCam, view.forward) <= cam.nearClipPlane) return;
                Vector3 target = g.anchor + toCam / Mathf.Max(distance, .01f) * Mathf.Min(.22f, distance * .3f) + Vector3.up * .06f;
                target = BesideHero(view, target);
                g.smoothPosition = g.placed ? Vector3.Lerp(g.smoothPosition, target, 1f - Mathf.Exp(-dt * 14f)) : target;
                g.placed = true;
                position = g.smoothPosition;
                float scaleFov = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Tan(30f * Mathf.Deg2Rad);
                height = Mathf.Clamp(distance * .085f * scaleFov, .1f, .24f);
            }

            // Arrival: a quick spin-in with a slight overshoot; leaving: it shrinks away.
            float a = g.appear;
            float pop = a >= 1f ? 1f : 1f + 2.7f * Mathf.Pow(a - 1f, 3f) + 1.7f * Mathf.Pow(a - 1f, 2f);
            float spin = (1f - Mathf.SmoothStep(0f, 1f, a)) * -95f;
            float press = Mathf.Sin(Mathf.Clamp01(g.pulse) * Mathf.PI);

            // Idle drift: a slow bob and sway so the key's depth reads; QTE keys tremble.
            float s = g.seed;
            Vector3 drift = view.up * (Mathf.Sin(t * 1.5f + s) * .045f) + view.right * (Mathf.Sin(t * .9f + s * 1.7f) * .03f);
            if (g.fail > 0f) drift += view.right * (Mathf.Sin(t * 60f) * .14f * g.fail);
            float yaw = Mathf.Sin(t * .8f + s) * 18f + spin;
            float pitch = Mathf.Sin(t * 1.1f + s * 1.3f) * 11f;
            float roll = Mathf.Sin(t * .7f + s * .6f) * 3f + (g.urgent ? Mathf.Sin(t * 26f + s) * 2.4f : 0f);

            Vector3 facing = g.onScreen ? view.forward : (position - view.position).normalized;
            var rotation = Quaternion.LookRotation(facing, view.up) * Quaternion.Euler(pitch, yaw, roll);
            float size = height * Mathf.Max(pop, 0f) * (1f - .14f * press);
            var root = Matrix4x4.TRS(position + drift * height + facing * (press * .35f * height), rotation, Vector3.one * size);

            Color ink = Color.Lerp(new Color(.95f, .94f, .9f), new Color(1f, .38f, .33f), g.fail);
            ink = Color.Lerp(ink, Color.white, press * .6f);
            float alpha = Mathf.Clamp01(a * 1.6f);
            var plate = new Color(.05f, .05f, .05f, .28f * alpha);
            var face = new Color(ink.r, ink.g, ink.b, .72f * alpha);
            var glyphInk = new Color(ink.r, ink.g, ink.b, .92f * alpha);

            // World keys obey the scene's depth: walls, doors and Alexey himself hide them.
            // Only the screen-pinned diary key is drawn over everything.
            if (g.onScreen)
            {
                Paint(cam, model.frame, root, clearMat, face);
                Paint(cam, model.plate, root, clearMat, face);
                Paint(cam, model.letter, root, clearMat, face);
            }
            Paint(cam, model.plate, root, solidMat, plate);
            Paint(cam, model.frame, root, solidMat, face);
            Paint(cam, model.letter, root, solidMat, glyphInk);
            if (g.progress >= 0f)
            {
                float w = model.width * Mathf.Clamp01(g.progress);
                var timer = root * Matrix4x4.TRS(new Vector3(0f, -.68f, 0f), Quaternion.identity, new Vector3(w, .07f, .07f));
                if (g.onScreen) Paint(cam, bar, timer, clearMat, face);
                Paint(cam, bar, timer, solidMat, face);
            }
        }

        OfficerController hero;

        /// Walls hide a key, but Alexey must not: if the key sits behind his silhouette, it steps just past its edge.
        Vector3 BesideHero(Transform view, Vector3 target)
        {
            if (!hero || !hero.isActiveAndEnabled) hero = FindAnyObjectByType<OfficerController>();
            if (!hero) return target;
            Vector3 body = hero.transform.position + Vector3.up * 1.15f;
            float heroDepth = Vector3.Dot(body - view.position, view.forward);
            float keyDepth = Vector3.Dot(target - view.position, view.forward);
            if (heroDepth <= .2f || keyDepth < heroDepth - .15f) return target;
            Vector3 rel = target - body;
            if (Mathf.Abs(Vector3.Dot(rel, view.up)) > .95f) return target;
            float side = Vector3.Dot(rel, view.right);
            float clear = .42f * keyDepth / heroDepth;
            if (Mathf.Abs(side) >= clear) return target;
            return target + view.right * ((side >= 0f ? 1f : -1f) * clear - side);
        }

        void Paint(Camera cam, Mesh mesh, Matrix4x4 matrix, Material material, Color color)
        {
            if (!mesh) return;
            block.SetColor(ColorId, color);
            Graphics.DrawMesh(mesh, matrix, material, 0, cam, 0, block, false, false, false);
        }

        KeyModel Model(string key)
        {
            if (models.TryGetValue(key, out var model)) return model;
            float width = key == "Space" ? 2.4f : key.Length > 1 ? 1.7f : 1f;
            var frame = new MeshBuilder();
            frame.RoundedRing(width, 1f, Corner, Rim, -Depth * .5f, Depth * .5f);
            var plate = new MeshBuilder();
            plate.RoundedSlab(width - Rim * 1.6f, 1f - Rim * 1.6f, Mathf.Max(.02f, Corner - Rim * .8f), Depth * .1f, Depth * .2f);
            var letter = new MeshBuilder();
            Letter(letter, key, -Depth * .5f - .025f, -Depth * .15f);
            model = new KeyModel { frame = frame.Build(), plate = plate.Build(), letter = letter.Build(), width = width };
            models[key] = model;
            return model;
        }

        /// Raised letter built from beveled-looking blocks: clean shapes for E, J and Space, a 5x7 grid for the rest.
        static void Letter(MeshBuilder b, string key, float front, float back)
        {
            void Bar(float x0, float y0, float x1, float y1) => b.Box(new Vector3(x0, y0, front), new Vector3(x1, y1, back));
            const float h = .24f, w = .048f;
            switch (key)
            {
                case "E":
                    Bar(-.15f, -h, -.15f + w, h);
                    Bar(-.15f, h - w, .16f, h);
                    Bar(-.15f, -w * .5f, .11f, w * .5f);
                    Bar(-.15f, -h, .16f, -h + w);
                    return;
                case "J":
                    Bar(.06f, -h + w, .06f + w, h);
                    Bar(-.06f, h - w, .2f, h);
                    Bar(-.15f, -h, .06f + w, -h + w);
                    Bar(-.15f, -h, -.15f + w, -.07f);
                    return;
                case "Space":
                    Bar(-.62f, -.12f, .62f, -.12f + w);
                    Bar(-.62f, -.12f, -.62f + w, .1f);
                    Bar(.62f - w, -.12f, .62f, .1f);
                    return;
            }
            if (key.Length != 1 || !Font5x7.TryGetValue(key[0], out var rows)) { Bar(-.06f, -.06f, .06f, .06f); return; }
            float cell = .5f / 7f, x0 = -2.5f * cell;
            for (int row = 0; row < 7; row++)
            {
                string line = rows[row];
                for (int col = 0; col < 5; col++)
                {
                    if (line[col] != '#') continue;
                    int end = col;
                    while (end + 1 < 5 && line[end + 1] == '#') end++;
                    float inset = cell * .22f;
                    Bar(x0 + col * cell + inset, .25f - (row + 1) * cell + inset, x0 + (end + 1) * cell - inset, .25f - row * cell - inset);
                    col = end;
                }
            }
        }

        static readonly Dictionary<char, string[]> Font5x7 = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".####", "#....", "#....", "#.###", "#...#", "#...#", ".###." },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
        };

        /// Flat-shaded mesh assembly with outward normals and matching winding.
        sealed class MeshBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int> tris = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                var n = Vector3.Cross(b - a, d - a).normalized;
                if (Vector3.Dot(n, outward) < 0f) { (b, d) = (d, b); n = -n; }
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                for (int k = 0; k < 4; k++) normals.Add(n);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                var n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, outward) < 0f) { (b, c) = (c, b); n = -n; }
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                for (int k = 0; k < 3; k++) normals.Add(n);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            public void Box(Vector3 min, Vector3 max)
            {
                Vector3 c = (min + max) * .5f;
                Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
                Quad(P(min.x, min.y, min.z), P(min.x, max.y, min.z), P(max.x, max.y, min.z), P(max.x, min.y, min.z), Vector3.back);
                Quad(P(min.x, min.y, max.z), P(max.x, min.y, max.z), P(max.x, max.y, max.z), P(min.x, max.y, max.z), Vector3.forward);
                Quad(P(min.x, min.y, min.z), P(min.x, min.y, max.z), P(min.x, max.y, max.z), P(min.x, max.y, min.z), Vector3.left);
                Quad(P(max.x, min.y, min.z), P(max.x, max.y, min.z), P(max.x, max.y, max.z), P(max.x, min.y, max.z), Vector3.right);
                Quad(P(min.x, max.y, min.z), P(min.x, max.y, max.z), P(max.x, max.y, max.z), P(max.x, max.y, min.z), Vector3.up);
                Quad(P(min.x, min.y, min.z), P(max.x, min.y, min.z), P(max.x, min.y, max.z), P(min.x, min.y, max.z), Vector3.down);
            }

            static List<Vector2> Loop(float width, float height, float radius, int segments = 7)
            {
                var points = new List<Vector2>();
                float hx = width * .5f - radius, hy = height * .5f - radius;
                Vector2[] centres = { new Vector2(hx, hy), new Vector2(-hx, hy), new Vector2(-hx, -hy), new Vector2(hx, -hy) };
                for (int c = 0; c < 4; c++)
                    for (int s = 0; s <= segments; s++)
                    {
                        float angle = (c * 90f + s * 90f / segments) * Mathf.Deg2Rad;
                        points.Add(centres[c] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                    }
                return points;
            }

            /// Rounded-square frame: front and back rings, outer and inner walls.
            public void RoundedRing(float width, float height, float radius, float rim, float front, float back)
            {
                var outer = Loop(width, height, radius);
                var inner = Loop(width - 2f * rim, height - 2f * rim, Mathf.Max(.02f, radius - rim));
                for (int i = 0; i < outer.Count; i++)
                {
                    int j = (i + 1) % outer.Count;
                    Vector3 o0 = outer[i], o1 = outer[j], i0 = inner[i], i1 = inner[j];
                    Quad(V(o0, front), V(o1, front), V(i1, front), V(i0, front), Vector3.back);
                    Quad(V(o0, back), V(i0, back), V(i1, back), V(o1, back), Vector3.forward);
                    Vector2 mid = (outer[i] + outer[j]) * .5f, innerMid = (inner[i] + inner[j]) * .5f;
                    Quad(V(o0, front), V(o0, back), V(o1, back), V(o1, front), new Vector3(mid.x, mid.y, 0f));
                    Quad(V(i0, front), V(i1, front), V(i1, back), V(i0, back), new Vector3(-innerMid.x, -innerMid.y, 0f));
                }
            }

            /// Solid rounded slab (the dark plate inside the frame).
            public void RoundedSlab(float width, float height, float radius, float front, float back)
            {
                var loop = Loop(width, height, radius);
                for (int i = 0; i < loop.Count; i++)
                {
                    int j = (i + 1) % loop.Count;
                    Vector3 a = loop[i], b = loop[j];
                    Tri(V(Vector2.zero, front), V(a, front), V(b, front), Vector3.back);
                    Tri(V(Vector2.zero, back), V(b, back), V(a, back), Vector3.forward);
                    Vector2 mid = (loop[i] + loop[j]) * .5f;
                    Quad(V(a, front), V(a, back), V(b, back), V(b, front), new Vector3(mid.x, mid.y, 0f));
                }
            }

            static Vector3 V(Vector2 p, float z) => new Vector3(p.x, p.y, z);

            public Mesh Build()
            {
                var mesh = new Mesh { hideFlags = HideFlags.DontSave };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
