using System.Collections.Generic;
using UnityEngine;

namespace Casebook
{
    // Cache one antialiased thread image per connection. Moving a card only rebuilds its threads.
    internal static class ThreadStroke
    {
        sealed class Stroke { public Vector2 delta; public float scale; public Texture2D texture; }
        static readonly Dictionary<string, Stroke> strokes = new Dictionary<string, Stroke>();
        public static void Draw(Vector2 a, Vector2 b, float scale, string id)
        {
            if (Event.current.type != EventType.Repaint) return;
            var delta = b - a;
            const int pad = 7;
            if (!strokes.TryGetValue(id, out var stroke)) strokes[id] = stroke = new Stroke();
            if (!stroke.texture || (stroke.delta - delta).sqrMagnitude > .1f || !Mathf.Approximately(stroke.scale, scale))
            {
                if (stroke.texture) Object.Destroy(stroke.texture);
                int w = Mathf.CeilToInt(Mathf.Abs(delta.x)) + pad * 2 + 1;
                int h = Mathf.CeilToInt(Mathf.Abs(delta.y)) + pad * 2 + 1;
                var pixels = new Color[w * h];
                var start = new Vector2(pad + Mathf.Max(0, -delta.x), pad + Mathf.Max(0, -delta.y));
                float sag = Mathf.Min(7 * scale, delta.magnitude * .016f);
                int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude * 2));
                void Dab(Vector2 p, float radius, Color color)
                {
                    for (int y = Mathf.Max(0, Mathf.FloorToInt(p.y - radius - 1)); y <= Mathf.Min(h-1, Mathf.CeilToInt(p.y + radius + 1)); y++)
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(p.x - radius - 1)); x <= Mathf.Min(w-1, Mathf.CeilToInt(p.x + radius + 1)); x++)
                    {
                        float alpha = Mathf.Clamp01(radius + .5f - Vector2.Distance(new Vector2(x,y), p)) * color.a;
                        int index = (h - 1 - y) * w + x;
                        if (alpha <= 0) continue;
                        var old = pixels[index];
                        float combined = alpha + old.a * (1 - alpha);
                        pixels[index] = new Color((color.r*alpha+old.r*old.a*(1-alpha))/combined,
                            (color.g*alpha+old.g*old.a*(1-alpha))/combined,
                            (color.b*alpha+old.b*old.a*(1-alpha))/combined, combined);
                    }
                }
                for(int pass=0;pass<2;pass++)
                    for(int i=0;i<=steps;i++)
                    {
                        float t=i/(float)steps;
                        var p=start+delta*t+Vector2.up*(4*t*(1-t)*sag);
                        Dab(p+(pass==0?Vector2.up*1.8f*scale:Vector2.zero), (pass==0?1.8f:1.25f)*scale,
                            pass==0?new Color(0,0,0,.12f):DeskInk.DisplayColor(DeskInk.DisplayColor(DeskInk.C("852c29"))));
                    }
                stroke.texture = new Texture2D(w,h,TextureFormat.RGBA32,false,true) { filterMode=FilterMode.Bilinear, wrapMode=TextureWrapMode.Clamp };
                stroke.texture.SetPixels(pixels); stroke.texture.Apply(false,true);
                stroke.delta=delta;stroke.scale=scale;
            }
            GUI.DrawTexture(new Rect(Mathf.Min(a.x,b.x)-pad,Mathf.Min(a.y,b.y)-pad,stroke.texture.width,stroke.texture.height),stroke.texture);
        }
    }
}
