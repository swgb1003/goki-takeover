using System.Collections.Generic;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// The "things a cockroach found" look of the redesigned menus (canvas 案2b, 2026-10-26): torn paper tape,
    /// a cardboard scrap, a round sticker, a red-pen loop, a small drawn cockroach and a screen grade (warm tint,
    /// vignette, grain) that lays the same light over the scene and the UI. Every texture is painted once in code
    /// at twice the 1280x720 canvas size and cached; nothing here is an imported asset.
    /// </summary>
    public static class PaperKit
    {
        const int Res = 2;   // texture pixels per canvas pixel
        public static readonly Color Paper = Hex(0xDCCFB4), Tape = new Color(0.84f, 0.77f, 0.55f), Ink = Hex(0x2A211A), InkSoft = Hex(0x2E241A);
        public static readonly Color Sticker = Hex(0x9DB23A), StickerRim = Hex(0xD6CBB0), StickerInk = Hex(0x1A1F06), RedPen = Hex(0xB5362A);
        public static readonly Color Card = Hex(0x9C7A4C), Orange = Hex(0xDD7D2C), Light = Hex(0xE2D6BE), Key = Hex(0xD8CCB2), KeyEdge = Hex(0x7C6E5C);
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        // ---------------- Drawing helpers (canvas coordinates; the angle turns about the centre) ----------------
        public static void Rotated(Rect r, float degrees, System.Action draw)
        {
            Matrix4x4 saved = GUI.matrix;
            // Rotate inside the canvas matrix (GUIUtility.RotateAroundPivot would pivot in screen space).
            if (degrees != 0) GUI.matrix = saved * Matrix4x4.Translate(r.center) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, degrees)) * Matrix4x4.Translate(-r.center);
            draw();
            GUI.matrix = saved;
        }
        public static void Scaled(Vector2 pivot, float scale, System.Action draw)
        {
            Matrix4x4 saved = GUI.matrix;
            if (scale != 1) GUI.matrix = saved * Matrix4x4.Translate(pivot) * Matrix4x4.Scale(new Vector3(scale, scale, 1)) * Matrix4x4.Translate(-pivot);
            draw();
            GUI.matrix = saved;
        }
        public static void Image(Rect r, Texture2D tex, float pad, Color tint)
        {
            GUI.color = tint;
            GUI.DrawTexture(new Rect(r.x - pad, r.y - pad, r.width + pad * 2, r.height + pad * 2), tex);
            GUI.color = Color.white;
        }

        /// <summary>A strip of paper tape with notched ends; the scene shows through it a little.</summary>
        public static void TapeStrip(Rect r, float degrees, float alpha, string label, GUIStyle style, Color ink)
        {
            var tex = Get("tape" + (int)r.width + "x" + (int)r.height, () => TapeTexture(r.width, r.height));
            Rotated(r, degrees, () =>
            {
                Image(r, tex, 0, new Color(Tape.r, Tape.g, Tape.b, alpha));
                if (label != null) HudKit.Ink(style, ink, () => GUI.Label(r, label, style));
            });
        }
        /// <summary>Paper tape with a hint of torn edges along the top and bottom (the subtitle and warnings).</summary>
        public static void TornTape(Rect r, float degrees, Color color)
        {
            var tex = Get("torn" + (int)r.width + "x" + (int)r.height, () => TornTexture(r.width, r.height));
            Rotated(r, degrees, () => Image(r, tex, 0, color));
        }
        /// <summary>A cardboard scrap with ragged top and left edges and a soft shadow below it.</summary>
        public static void Cardboard(Rect r, float degrees)
        {
            const float pad = 24;
            var tex = Get("card" + (int)r.width + "x" + (int)r.height, () => CardTexture(r.width, r.height, pad));
            Rotated(r, degrees, () => Image(r, tex, pad, Color.white));
        }
        /// <summary>The round sticker with a pale rim; <paramref name="lift"/> brightens it for hover / focus.</summary>
        public static void StickerDisc(Rect r, float degrees, float lift, bool pressed)
        {
            const float pad = 16;
            var tex = Get("sticker" + (int)r.width, () => StickerTexture(r.width, pad));
            if (pressed) r.y += 2;
            Rotated(r, degrees, () => Image(r, tex, pad, Color.Lerp(Color.white, new Color(1.12f, 1.12f, 1.06f), lift)));
        }
        /// <summary>The red-pen loop that marks the focused item, drawn around <paramref name="r"/>.</summary>
        public static void PenLoop(Rect r, Vector2 size, float alpha = 1)
        {
            var tex = Get("loop" + (int)size.x + "x" + (int)size.y, () => LoopTexture(size.x, size.y));
            Image(r, tex, 0, new Color(1, 1, 1, alpha));
        }
        public static void Roach(Rect r, float degrees)
        {
            var tex = Get("roach", RoachTexture);
            Rotated(r, degrees, () => Image(r, tex, 0, Color.white));
        }
        /// <summary>A key cap and its label, like 「Enter 決定」.</summary>
        public static float KeyHint(float x, float y, string key, string label, GUIStyle style)
        {
            float kw = style.CalcSize(new GUIContent(key)).x + 12, lw = style.CalcSize(new GUIContent(label)).x;
            HudKit.Fill(new Rect(x, y, kw, 20), Key); HudKit.Fill(new Rect(x, y + 18, kw, 2), KeyEdge);
            HudKit.Ink(style, Ink, () => GUI.Label(new Rect(x + 6, y + 1, kw, 20), key, style));
            Lit(new Rect(x + kw + 6, y + 1, lw + 4, 20), label, style, Light);
            return x + kw + 6 + lw + 14;
        }
        /// <summary>Light text straight on the scene, with a soft dark halo instead of a box.</summary>
        public static void Lit(Rect r, string label, GUIStyle style, Color color)
        {
            Color before = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.45f);
            for (int i = 0; i < 4; i++) GUI.Label(new Rect(r.x + (i == 0 ? -1 : i == 1 ? 1 : 0), r.y + (i == 2 ? -1 : i == 3 ? 2 : 1), r.width, r.height), label, style);
            style.normal.textColor = color; GUI.Label(r, label, style);
            style.normal.textColor = before;
        }
        /// <summary>The same light over the scene and the UI: warm tint, vignette and film grain.</summary>
        public static void Grade()
        {
            var full = new Rect(0, 0, 1280, 720);
            HudKit.Fill(full, new Color(0.55f, 0.4f, 0.15f, 0.06f));   // close to the mock-up's 16% multiply of #F0D3A4: warm, barely darker
            GUI.DrawTexture(full, Get("vignette", VignetteTexture));
            GUI.DrawTextureWithTexCoords(full, Get("grain", GrainTexture), new Rect(0, 0, 1280f / 256f, 720f / 256f));
        }

        // ---------------- Painting ----------------
        static Texture2D Get(string key, System.Func<Texture2D> make)
        {
            if (cache.TryGetValue(key, out var tex) && tex) return tex;
            tex = make();
            tex.hideFlags = HideFlags.DontSave;
            cache[key] = tex;
            return tex;
        }
        static Texture2D New(int w, int h, bool mips = true)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, mips) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 1 };
        }
        // Pixel (x, y) in canvas units from the top left; GetPixels rows run bottom-up.
        static Texture2D Paint(float w, float h, System.Func<float, float, Color> shade)
        {
            int pw = Mathf.CeilToInt(w * Res), ph = Mathf.CeilToInt(h * Res);
            var px = new Color[pw * ph];
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                    px[(ph - 1 - y) * pw + x] = shade((x + 0.5f) / Res, (y + 0.5f) / Res);
            var tex = New(pw, ph);
            tex.SetPixels(px); tex.Apply(true, true);
            return tex;
        }
        // GLSL-style smoothstep: 0 below a, 1 above b (Mathf.SmoothStep interpolates between its first two arguments instead).
        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float Cover(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance * Res);   // anti-aliased edge
        static Color Over(Color top, Color under)
        {
            float a = top.a + under.a * (1 - top.a);
            if (a <= 0) return new Color(0, 0, 0, 0);
            Color c = (top * top.a + under * under.a * (1 - top.a)) / a;
            c.a = a; return c;
        }
        // Signed distance to a convex-or-not polygon (negative inside).
        static float Polygon(Vector2[] p, float x, float y)
        {
            var q = new Vector2(x, y);
            float d = (q - p[0]).sqrMagnitude, s = 1;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i, i++)
            {
                Vector2 e = p[j] - p[i], w = q - p[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                d = Mathf.Min(d, b.sqrMagnitude);
                bool c1 = q.y >= p[i].y, c2 = q.y < p[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
        // Light from the top left: a little brighter there, a little darker at the bottom right.
        static Color Lit(Color c, float u, float v, float top = 0.12f, float bottom = 0.16f)
        {
            float t = Mathf.Clamp01((u * 0.17f + v) / 1.17f);
            return t < 0.4f ? Color.Lerp(c, new Color(1, 0.97f, 0.9f), top * (1 - t / 0.4f)) : Color.Lerp(c, new Color(0.24f, 0.14f, 0.04f), bottom * (t - 0.4f) / 0.6f);
        }
        static float Noise(int x, int y) { uint n = (uint)(x * 374761393 + y * 668265263); n = (n ^ (n >> 13)) * 1274126177u; return (n & 0xffff) / 65535f; }

        static Texture2D TapeTexture(float w, float h)
        {
            float n = Mathf.Min(h * 0.5f, w * 0.03f + 3);
            var shape = new[] { new Vector2(n, 0), new Vector2(w - n, 0), new Vector2(w, h * 0.5f), new Vector2(w - n, h), new Vector2(n, h), new Vector2(0, h * 0.5f) };
            return Paint(w, h, (x, y) =>
            {
                float a = Cover(Polygon(shape, x, y));
                Color c = Lit(Color.white, x / w, y / h, 0.1f, 0.12f);
                float fibre = 0.96f + 0.04f * Noise((int)(x * 0.5f), (int)(y * 3));
                return new Color(c.r * fibre, c.g * fibre, c.b * fibre, a);
            });
        }
        static Texture2D TornTexture(float w, float h)
        {
            var pts = new List<Vector2>();
            int steps = Mathf.Max(8, (int)(w / 14));
            pts.Add(new Vector2(w * 0.02f, 0));
            for (int i = 1; i < steps; i++) pts.Add(new Vector2(w * i / steps, (i % 2) * 1.6f));
            pts.Add(new Vector2(w, 0)); pts.Add(new Vector2(w * 0.985f, h * 0.32f)); pts.Add(new Vector2(w, h * 0.68f)); pts.Add(new Vector2(w * 0.98f, h));
            for (int i = steps - 1; i > 0; i--) pts.Add(new Vector2(w * i / steps, h - ((i + 1) % 2) * 1.6f));
            pts.Add(new Vector2(0, h)); pts.Add(new Vector2(w * 0.015f, h * 0.64f)); pts.Add(new Vector2(0, h * 0.3f));
            var shape = pts.ToArray();
            return Paint(w, h, (x, y) =>
            {
                Color c = Lit(Color.white, x / w, y / h, 0.12f, 0.14f);
                c.a = Cover(Polygon(shape, x, y));
                return c;
            });
        }
        static Texture2D CardTexture(float w, float h, float pad)
        {
            var shape = new[] { new Vector2(w * 0.03f, h * 0.02f), new Vector2(w * 0.34f, 0), new Vector2(w * 0.7f, h * 0.03f), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h), new Vector2(w * 0.02f, h * 0.52f) };
            float tw = w + pad * 2, th = h + pad * 2;
            return Paint(tw, th, (x, y) =>
            {
                float cx = x - pad, cy = y - pad;
                float d = Polygon(shape, cx, cy);
                // Soft shadow: the same shape 6 px lower, faded over 10 px.
                float sd = Polygon(shape, cx, cy - 6);
                var shadow = new Color(0.08f, 0.04f, 0, 0.55f * (1 - Smooth(-6, 10, sd)));
                if (d > 1) return shadow;
                Color c = Lit(Card, cx / w, cy / h, 0.16f, 0.24f);
                if (((int)cx % 9 + 9) % 9 < 2) c *= 0.88f;                 // corrugation lines
                float speck = Noise((int)(cx * 0.7f), (int)(cy * 0.7f));
                c *= 0.97f + speck * 0.05f; c.a = Cover(d);
                return Over(c, shadow);
            });
        }
        static Texture2D StickerTexture(float size, float pad)
        {
            float t = size + pad * 2, r = size * 0.5f, cx = t * 0.5f, rim = size * 6 / 186f;
            return Paint(t, t, (x, y) =>
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cx) * (y - cx)) - r;
                float sd = Mathf.Sqrt((x - cx) * (x - cx) + (y - cx - 4) * (y - cx - 4)) - r;
                var shadow = new Color(0.08f, 0.04f, 0, 0.5f * (1 - Smooth(-4, 7, sd)));
                if (d > 1) return shadow;
                float u = (x - pad) / size, v = (y - pad) / size;
                Color c = d > -rim ? Lit(StickerRim, u, v, 0.1f, 0.18f) : Lit(Sticker, u, v, 0.18f, 0.2f);
                if (d > -rim - 1 && d <= -rim) c = Color.Lerp(c, StickerRim * 0.8f, 0.5f);
                c.a = Cover(d);
                return Over(c, shadow);
            });
        }
        // The loop is a hand-drawn ellipse that overshoots its start, stamped with a round pen.
        static Texture2D LoopTexture(float w, float h)
        {
            int pw = Mathf.CeilToInt(w * Res), ph = Mathf.CeilToInt(h * Res);
            var alpha = new float[pw * ph];
            float rx = w * 0.5f - 4, ry = h * 0.5f - 4, cx = w * 0.5f, cy = h * 0.5f;
            const float pen = 2.1f;
            for (float a = -2.1f; a <= 2.1f + Mathf.PI * 2 + 0.35f; a += 0.004f)
            {
                float k = (a - -2.1f) / (Mathf.PI * 2 + 0.35f);                    // 0..1 along the stroke
                float wobble = 1 + 0.035f * Mathf.Sin(a * 3 + 0.7f) - 0.05f * k;     // the end lands a little inside
                float x = cx + Mathf.Cos(a) * rx * wobble, y = cy + Mathf.Sin(a) * ry * wobble;
                Stamp(alpha, pw, ph, x * Res, y * Res, pen * Res);
            }
            return FromAlpha(alpha, pw, ph, RedPen);
        }
        static Texture2D RoachTexture()
        {
            const float w = 70, h = 56;
            int pw = (int)(w * Res), ph = (int)(h * Res);
            var legs = new float[pw * ph];
            void Line(float x0, float y0, float x1, float y1, float r) { for (float t = 0; t <= 1; t += 0.01f) Stamp(legs, pw, ph, Mathf.Lerp(x0, x1, t) * Res, Mathf.Lerp(y0, y1, t) * Res, r * Res); }
            Line(24, 16, 16, 4, 1.2f); Line(34, 15, 32, 2, 1.2f); Line(24, 40, 16, 52, 1.2f); Line(34, 41, 32, 54, 1.2f); Line(42, 18, 48, 8, 1.2f); Line(42, 38, 48, 48, 1.2f);
            for (float t = 0; t <= 1; t += 0.01f)   // antennae
            {
                Stamp(legs, pw, ph, Mathf.Lerp(52, 69, t) * Res, (22 - 20 * t + 6 * t * (1 - t)) * Res, Res);
                Stamp(legs, pw, ph, Mathf.Lerp(52, 69, t) * Res, (32 + 22 * t - 6 * t * (1 - t)) * Res, Res);
            }
            var px = new Color[pw * ph];
            Color body = Hex(0x6A3A20), head = Hex(0x4A2814), edge = Hex(0x2A160B), leg = Hex(0x3A2416);
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                {
                    float u = (x + 0.5f) / Res, v = (y + 0.5f) / Res;
                    float db = Ellipse(u, v, 30, 28, 22, 12), dh = Ellipse(u, v, 51, 28, 6, 7);
                    Color c = new Color(leg.r, leg.g, leg.b, legs[y * pw + x]);
                    if (dh < 1) { var hc = dh > -2 ? edge : head; hc.a = Cover(dh); c = Over(hc, c); }
                    if (db < 1)
                    {
                        var bc = db > -2 ? edge : Mathf.Abs(v - 28) < 0.8f && u > 10 && u < 48 ? Hex(0x3E2010) : Lit(body, (u - 8) / 44, (v - 16) / 24, 0.2f, 0.2f);
                        bc.a = Cover(db); c = Over(bc, c);
                    }
                    px[(ph - 1 - y) * pw + x] = c;
                }
            var tex = New(pw, ph); tex.SetPixels(px); tex.Apply(true, true);
            return tex;
        }
        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float k = Mathf.Sqrt((x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry));
            return (k - 1) * Mathf.Min(rx, ry);
        }
        static void Stamp(float[] alpha, int pw, int ph, float x, float y, float r)
        {
            int x0 = Mathf.Max(0, (int)(x - r - 2)), x1 = Mathf.Min(pw - 1, (int)(x + r + 2)), y0 = Mathf.Max(0, (int)(y - r - 2)), y1 = Mathf.Min(ph - 1, (int)(y + r + 2));
            for (int j = y0; j <= y1; j++)
                for (int i = x0; i <= x1; i++)
                {
                    float d = Mathf.Sqrt((i + 0.5f - x) * (i + 0.5f - x) + (j + 0.5f - y) * (j + 0.5f - y)) - r;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (a > alpha[j * pw + i]) alpha[j * pw + i] = a;
                }
        }
        static Texture2D FromAlpha(float[] alpha, int pw, int ph, Color color)
        {
            var px = new Color[pw * ph];
            for (int y = 0; y < ph; y++) for (int x = 0; x < pw; x++) px[(ph - 1 - y) * pw + x] = new Color(color.r, color.g, color.b, alpha[y * pw + x]);
            var tex = New(pw, ph); tex.SetPixels(px); tex.Apply(true, true);
            return tex;
        }
        static Texture2D VignetteTexture()
        {
            const int w = 256, h = 144;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = ((x + 0.5f) / w - 0.52f) / 0.75f, v = ((y + 0.5f) / h - 0.54f) / 0.7f;   // ellipse 75% x 70%, centre a bit high
                    float k = Mathf.Sqrt(u * u + v * v);   // 1 at the ellipse edge
                    px[y * w + x] = new Color(0.08f, 0.04f, 0, 0.42f * Smooth(0.55f, 1.0f, k));
                }
            var tex = New(w, h, false); tex.filterMode = FilterMode.Bilinear; tex.SetPixels(px); tex.Apply(false, true);
            return tex;
        }
        static Texture2D GrainTexture()
        {
            const int s = 256;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float n = Noise(x, y);
                    px[y * s + x] = n > 0.5f ? new Color(1, 0.96f, 0.88f, (n - 0.5f) * 0.12f) : new Color(0.1f, 0.06f, 0, (0.5f - n) * 0.14f);
                }
            var tex = New(s, s, false); tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Point; tex.SetPixels(px); tex.Apply(false, true);
            return tex;
        }
    }
}
