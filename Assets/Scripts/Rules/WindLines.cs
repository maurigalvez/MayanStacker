using UnityEngine;

/// <summary>
/// Hand-drawn-style wind streaks for <see cref="JungleWind"/>, drawn at runtime with pooled
/// <see cref="LineRenderer"/>s — no texture needed.
///
/// Each streak is a thin tapered stroke that draws itself on along a gently bowed path and
/// wipes off behind, while the whole path drifts downwind. About one in three ends in a curl
/// (a small inward spiral, the scroll Maya carvings use for breath and wind), always curling
/// up and back against the flow, so even a single still frame says which way the wind blows.
///
/// <see cref="Strength"/> (-1..1) sets direction and how many streaks are in the air;
/// <see cref="Burst"/> throws a row of long curled streaks across the view when a gust turns.
///
/// Presentation only — cosmetic randomness here never touches the seeded run stream.
/// </summary>
public class WindLines : MonoBehaviour
{
    private const int PoolSize = 14;
    private const int PathPoints = 40;    // dense path, sampled by arc length
    private const int DrawPoints = 18;    // points in the visible stroke
    private const float MaxWidth = 0.075f;
    private const float StreaksPerSecondAtFullGust = 5.5f;
    private const float DriftAtFullGust = 5.5f; // world units/s the path itself travels
    private const int BurstCount = 4;

    private static readonly Color StreakColor = new Color(0.96f, 0.93f, 0.84f, 0.85f); // parchment, not pure white; near-opaque over its outline

    private class Streak
    {
        public LineRenderer line;
        public LineRenderer outline;   // wider dark stroke under the line, see EffectOutline
        public readonly Gradient outlineGradient = new Gradient();
        public readonly Vector2[] path = new Vector2[PathPoints];      // local, flowing toward +x
        public readonly float[] lengthAt = new float[PathPoints];
        public readonly Vector3[] draw = new Vector3[DrawPoints];
        public float totalLength;
        public Vector2 origin;
        public float dir;          // +1 / -1
        public float age;
        public float life;
        public float tailShare;    // how much of the path shows at once
        public float drift;
        public bool alive;
        public readonly Gradient gradient = new Gradient();
    }

    // Shared key buffers; SetKeys copies them, so one set serves every streak.
    private static readonly GradientColorKey[] colorKeys =
    {
        new GradientColorKey(StreakColor, 0f), new GradientColorKey(StreakColor, 1f)
    };
    private static readonly GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];
    private static readonly GradientColorKey[] outlineColorKeys =
    {
        new GradientColorKey(EffectOutline.Color, 0f), new GradientColorKey(EffectOutline.Color, 1f)
    };
    private static readonly GradientAlphaKey[] outlineAlphaKeys = new GradientAlphaKey[3];

    private readonly Streak[] pool = new Streak[PoolSize];
    private Material material;
    private Camera cam;
    private float spawnAccumulator;
    private int next;

    /// <summary>-1..1: direction and strength of the wind. 0 lets the air go still.</summary>
    public float Strength { get; set; }

    public void Build(int sortingOrder)
    {
        cam = Camera.main;
        material = new Material(Shader.Find("Sprites/Default"));

        var width = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3.2f),
            new Keyframe(0.62f, 1f),
            new Keyframe(1f, 0.18f));

        // The same taper held open at the thin ends, so the dark edge doesn't pinch to nothing there.
        var outlineWidth = new AnimationCurve(
            new Keyframe(0f, 0.25f, 0f, 2.4f),
            new Keyframe(0.62f, 1f),
            new Keyframe(1f, 0.4f));

        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("WindLine");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = DrawPoints;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.material = material;
            lr.widthCurve = width;
            lr.widthMultiplier = MaxWidth;
            lr.sortingOrder = sortingOrder;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.enabled = false;

            var outlineGo = new GameObject("WindLineOutline");
            outlineGo.transform.SetParent(transform, false);
            var ol = outlineGo.AddComponent<LineRenderer>();
            ol.useWorldSpace = true;
            ol.positionCount = DrawPoints;
            ol.numCapVertices = 2;
            ol.numCornerVertices = 2;
            ol.material = material;
            ol.widthCurve = outlineWidth;
            ol.sortingOrder = sortingOrder - 1;
            ol.textureMode = LineTextureMode.Stretch;
            ol.alignment = LineAlignment.View;
            ol.enabled = false;

            pool[i] = new Streak { line = lr, outline = ol };
        }
    }

    private static void SetEnabled(Streak s, bool on)
    {
        s.line.enabled = on;
        s.outline.enabled = on;
    }

    /// <summary>A row of long, curled streaks across the view — the wind has turned.</summary>
    public void Burst(float direction)
    {
        if (pool[0] == null) return;
        float dir = direction >= 0f ? 1f : -1f;
        GetView(out Vector2 min, out Vector2 max);
        float height = max.y - min.y;

        for (int i = 0; i < BurstCount; i++)
        {
            // Spread down the upper two-thirds of the view, where stones swing and fall.
            float y = max.y - height * (0.12f + 0.2f * i) + Random.Range(-0.3f, 0.3f);
            float upwind = dir > 0f ? min.x : max.x;
            float x = upwind + dir * Random.Range(0.4f, 2.2f);
            Spawn(new Vector2(x, y), dir, isLong: true, curl: i % 2 == 0, delay: i * 0.07f);
        }
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    private void Update()
    {
        if (pool[0] == null) return;
        float dt = Time.deltaTime;
        float strength = Mathf.Clamp(Strength, -1f, 1f);
        float amount = Mathf.Abs(strength);

        // Nothing below a whisper of wind, so a calm stretch actually looks calm.
        if (amount > 0.08f)
        {
            spawnAccumulator += dt * StreaksPerSecondAtFullGust * amount;
            while (spawnAccumulator >= 1f)
            {
                spawnAccumulator -= 1f;
                GetView(out Vector2 min, out Vector2 max);
                var p = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y + (max.y - min.y) * 0.2f, max.y));
                Spawn(p, Mathf.Sign(strength), isLong: false, curl: Random.value < 0.33f, delay: 0f);
            }
        }
        else
        {
            spawnAccumulator = 0f;
        }

        for (int i = 0; i < PoolSize; i++)
        {
            Streak s = pool[i];
            if (!s.alive) continue;

            s.age += dt;
            if (s.age < 0f) continue; // waiting on a burst stagger
            if (s.age >= s.life)
            {
                s.alive = false;
                SetEnabled(s, false);
                continue;
            }

            s.origin.x += s.dir * s.drift * dt;
            Draw(s);
        }
    }

    private void Spawn(Vector2 at, float dir, bool isLong, bool curl, float delay)
    {
        // Take a free streak; if all are in the air, recycle the oldest in turn.
        Streak s = null;
        for (int k = 0; k < PoolSize; k++)
        {
            Streak candidate = pool[(next + k) % PoolSize];
            if (!candidate.alive) { s = candidate; next = (next + k + 1) % PoolSize; break; }
        }
        if (s == null)
        {
            s = pool[next];
            next = (next + 1) % PoolSize;
        }

        float length = isLong ? Random.Range(4.2f, 5.6f) : Random.Range(2.2f, 3.8f);
        float bow = Random.Range(0.08f, 0.22f) * (Random.value < 0.5f ? -1f : 1f);
        BuildPath(s, length, bow, curl, Random.Range(0.26f, 0.38f));

        s.origin = at;
        s.dir = dir;
        s.age = -delay;
        s.life = isLong ? Random.Range(1.1f, 1.35f) : Random.Range(1f, 1.35f);
        s.tailShare = isLong ? 0.5f : Random.Range(0.35f, 0.5f);
        s.drift = DriftAtFullGust * Random.Range(0.8f, 1.15f);
        s.line.widthMultiplier = MaxWidth * (isLong ? 1.15f : Random.Range(0.75f, 1f));
        s.outline.widthMultiplier = s.line.widthMultiplier + 2f * EffectOutline.Pad;
        s.alive = true;
        SetEnabled(s, false);
    }

    /// <summary>
    /// A bowed stroke along +x (sin² bump, so it leaves and meets the curl level), optionally
    /// finished with an inward spiral that turns up and back over the stroke.
    /// </summary>
    private static void BuildPath(Streak s, float length, float bow, bool curl, float curlRadius)
    {
        int straightPoints = curl ? PathPoints * 3 / 5 : PathPoints;
        float straightLength = curl ? length - curlRadius * 2.2f : length;

        for (int i = 0; i < straightPoints; i++)
        {
            float u = straightPoints > 1 ? (float)i / (straightPoints - 1) : 0f;
            float sn = Mathf.Sin(Mathf.PI * u);
            s.path[i] = new Vector2(u * straightLength, bow * sn * sn);
        }

        if (curl)
        {
            Vector2 end = s.path[straightPoints - 1];
            Vector2 centre = end + new Vector2(0f, curlRadius);
            const float turns = 1.15f;
            int curlPoints = PathPoints - straightPoints;
            for (int i = 0; i < curlPoints; i++)
            {
                float t = (i + 1f) / curlPoints;
                float angle = -Mathf.PI * 0.5f + t * turns * 2f * Mathf.PI; // starts heading +x, turns up
                float r = curlRadius * (1f - 0.6f * t);
                s.path[straightPoints + i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
        }

        s.lengthAt[0] = 0f;
        for (int i = 1; i < PathPoints; i++)
        {
            s.lengthAt[i] = s.lengthAt[i - 1] + Vector2.Distance(s.path[i - 1], s.path[i]);
        }
        s.totalLength = s.lengthAt[PathPoints - 1];
    }

    private void Draw(Streak s)
    {
        // The head runs from the path's start to past its end; the tail follows a fixed share
        // behind, so the stroke draws on, travels, then wipes off into its curl.
        float t = s.age / s.life;
        float eased = 1f - (1f - t) * (1f - t); // fast out, easing into the curl
        float span = s.tailShare * s.totalLength;
        float head = Mathf.Lerp(0f, s.totalLength + span, eased);
        float tail = head - span;
        float from = Mathf.Clamp(tail, 0f, s.totalLength);
        float to = Mathf.Clamp(head, 0f, s.totalLength);
        if (to - from < 0.02f)
        {
            SetEnabled(s, false);
            return;
        }

        int seg = 0;
        for (int i = 0; i < DrawPoints; i++)
        {
            float d = Mathf.Lerp(from, to, (float)i / (DrawPoints - 1));
            while (seg < PathPoints - 2 && s.lengthAt[seg + 1] < d) seg++;
            float segLen = s.lengthAt[seg + 1] - s.lengthAt[seg];
            float k = segLen > 0.0001f ? (d - s.lengthAt[seg]) / segLen : 0f;
            Vector2 local = Vector2.Lerp(s.path[seg], s.path[seg + 1], k);
            s.draw[i] = new Vector3(s.origin.x + local.x * s.dir, s.origin.y + local.y, 0f);
        }

        // Transparent tail, solid head; the whole stroke fades in and out at the ends of its life.
        float alpha = StreakColor.a * Mathf.Min(1f, t * 6f) * Mathf.Min(1f, (1f - t) * 4f);
        alphaKeys[0] = new GradientAlphaKey(0f, 0f);
        alphaKeys[1] = new GradientAlphaKey(alpha * 0.85f, 0.45f);
        alphaKeys[2] = new GradientAlphaKey(alpha, 1f);
        s.gradient.SetKeys(colorKeys, alphaKeys);
        s.line.colorGradient = s.gradient;
        s.line.SetPositions(s.draw);

        float edge = EffectOutline.Color.a / StreakColor.a; // outline alpha follows the fill's fade
        outlineAlphaKeys[0] = new GradientAlphaKey(0f, 0f);
        outlineAlphaKeys[1] = new GradientAlphaKey(Mathf.Clamp01(alpha * 0.85f * edge), 0.45f);
        outlineAlphaKeys[2] = new GradientAlphaKey(Mathf.Clamp01(alpha * edge), 1f);
        s.outlineGradient.SetKeys(outlineColorKeys, outlineAlphaKeys);
        s.outline.colorGradient = s.outlineGradient;
        s.outline.SetPositions(s.draw);
        SetEnabled(s, true);
    }

    private void GetView(out Vector2 min, out Vector2 max)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            min = new Vector2(-6f, -10f);
            max = new Vector2(6f, 10f);
            return;
        }

        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        min = new Vector2(c.x - w, c.y - h);
        max = new Vector2(c.x + w, c.y + h);
    }
}
