using UnityEngine;

/// <summary>
/// Code-drawn stand-ins for temple-rule art, used whenever <see cref="TempleRuleArt"/> has no
/// sprite assigned. Each is drawn once and cached. Replace them by dropping real art into
/// Assets/Art/Rules/ and running TamalStacker ▸ Temples ▸ Wire Rule Art &amp; Audio.
/// </summary>
public static class RulePlaceholderArt
{
    private static Sprite leaf;
    private static Sprite streak;
    private static Sprite gradient;
    private static Sprite sun;

    /// <summary>A pointed jungle leaf, 1 world unit long.</summary>
    public static Sprite Leaf
    {
        get
        {
            if (leaf != null) return leaf;
            const int w = 64, h = 32;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;   // -1..1 along the leaf
                    float v = (y + 0.5f) / h * 2f - 1f;   // -1..1 across it
                    float halfWidth = (1f - u * u) * 0.9f;  // pointed at both ends
                    float d = Mathf.Abs(v) - halfWidth;
                    if (d > 0f) continue;
                    float a = Mathf.Clamp01(-d * 6f);
                    bool vein = Mathf.Abs(v) < 0.08f && u > -0.9f;
                    byte g = (byte)(vein ? 150 : 110 + 40 * (1f - Mathf.Abs(v)));
                    px[y * w + x] = new Color32((byte)(g * 0.35f), g, (byte)(g * 0.3f), (byte)(a * 255));
                }
            }
            leaf = Make(px, w, h, w);
            return leaf;
        }
    }

    /// <summary>A thin vertical rain streak, 1 world unit tall.</summary>
    public static Sprite Streak
    {
        get
        {
            if (streak != null) return streak;
            const int w = 4, h = 64;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Sin((y + 0.5f) / h * Mathf.PI); // soft at both ends
                for (int x = 0; x < w; x++)
                {
                    float edge = (x == 0 || x == w - 1) ? 0.4f : 1f;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * edge * 255));
                }
            }
            streak = Make(px, w, h, h);
            return streak;
        }
    }

    /// <summary>
    /// Vertical ramp for the eclipse: clear at the top, opaque at the bottom, 1 world unit
    /// square with its pivot on the ramp's middle.
    /// </summary>
    public static Sprite VerticalRamp
    {
        get
        {
            if (gradient != null) return gradient;
            const int w = 4, h = 128;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = 1f - (y + 0.5f) / h;           // 1 at the bottom, 0 at the top
                float a = t * t * (3f - 2f * t);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            gradient = Make(px, w, h, h, clamp: true);
            return gradient;
        }
    }

    /// <summary>An eclipsed sun: a dark disc with a soft gold corona, 1 world unit across.</summary>
    public static Sprite EclipsedSun
    {
        get
        {
            if (sun != null) return sun;
            const int s = 128;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float nx = (x + 0.5f) / s * 2f - 1f;
                    float ny = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    Color c;
                    if (r < 0.46f) c = new Color(0.04f, 0.03f, 0.03f, 1f);
                    else
                    {
                        float glow = Mathf.Clamp01(1f - (r - 0.46f) / 0.54f);
                        glow *= glow;
                        c = new Color(1f, 0.82f, 0.45f, glow);
                    }
                    px[y * s + x] = c;
                }
            }
            sun = Make(px, s, s, s);
            return sun;
        }
    }

    private static Sprite Make(Color32[] px, int w, int h, float pixelsPerUnit, bool clamp = true)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }
}
