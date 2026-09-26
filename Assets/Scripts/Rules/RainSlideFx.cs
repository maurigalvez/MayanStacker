using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a rain slide looks and sounds like, so the player can't miss that the stone moved:
///  - a wet sheen washes over the sliding stone;
///  - a skid mark grows along the seam from where the stone landed to where it stopped;
///  - water sprays out from under the stone's leading edge;
/// The sound is <see cref="RainSlick"/>'s; <see cref="PlaceholderSlide"/> stands in until
/// SFX_Rain_Slide exists.
///
/// Everything runs on unscaled time so hit-stop doesn't freeze it, and uses pooled renderers:
/// one sheen, one skid mark, a fixed handful of droplets.
/// </summary>
public class RainSlideFx : MonoBehaviour
{
    private const int DropletCount = 14;
    private const float DropletLife = 0.55f;
    private const float DropletGravity = 16f;
    private static readonly Vector2 DropletSize = new Vector2(0.07f, 0.22f);
    private static readonly Color DropletColor = new Color(0.8f, 0.92f, 1f, 0.9f);

    private const float SheenSeconds = 0.6f;
    private static readonly Color SheenColor = new Color(0.62f, 0.85f, 1f, 0.6f);

    private const float TrailHoldSeconds = 0.35f;
    private const float TrailFadeSeconds = 0.6f;
    private const float TrailThickness = 0.09f;
    private static readonly Color TrailColor = new Color(0.72f, 0.9f, 1f, 0.85f);

    private static AudioClip placeholderSlide;

    private struct Droplet
    {
        public SpriteRenderer sr;
        public Vector2 velocity;
        public float age;
    }

    private readonly List<Droplet> droplets = new List<Droplet>(DropletCount);
    private int nextDroplet;

    private SpriteRenderer sheen;
    private float sheenAge = float.MaxValue;

    private SpriteRenderer trail;
    private float trailAge = float.MaxValue;
    private float trailStartX, trailY, trailOffset, trailGrowSeconds;

    public void Build(Sprite streak)
    {
        for (int i = 0; i < DropletCount; i++)
        {
            var sr = CreateRenderer("Droplet", streak, transform);
            droplets.Add(new Droplet { sr = sr, age = float.MaxValue });
        }

        trail = CreateRenderer("SkidMark", streak, transform);

        sheen = CreateRenderer("WetSheen", null, transform);
    }

    /// <summary>Plays the whole cue for a stone about to slide <paramref name="offsetX"/> over <paramref name="seconds"/>.</summary>
    public void Play(StackableObject stone, StackableObject below, float offsetX, float seconds)
    {
        if (stone == null || stone.Collider == null) return;

        Bounds b = stone.Collider.bounds;
        SpriteRenderer stoneSr = stone.SpriteRenderer;
        int layer = stoneSr != null ? stoneSr.sortingLayerID : 0;
        int order = stoneSr != null ? stoneSr.sortingOrder : 0;
        float dir = Mathf.Sign(offsetX);
        float seamY = below != null && below.Collider != null ? below.Collider.bounds.max.y : b.min.y;

        // Sheen rides on the stone's own renderer so it follows the slide.
        if (stoneSr != null && stoneSr.sprite != null)
        {
            if (sheen == null) sheen = CreateRenderer("WetSheen", null, transform); // went down with an earlier stone
            sheen.transform.SetParent(stoneSr.transform, false);
            sheen.transform.localPosition = Vector3.zero;
            sheen.transform.localRotation = Quaternion.identity;
            sheen.transform.localScale = Vector3.one;
            sheen.sprite = stoneSr.sprite;
            sheen.drawMode = stoneSr.drawMode;
            if (stoneSr.drawMode != SpriteDrawMode.Simple) sheen.size = stoneSr.size;
            sheen.flipX = stoneSr.flipX;
            sheen.flipY = stoneSr.flipY;
            sheen.sortingLayerID = layer;
            sheen.sortingOrder = order + 1;
            sheen.enabled = true;
            sheenAge = 0f;
        }

        // Skid mark along the seam, growing with the slide from the landing spot.
        trailStartX = dir > 0f ? b.center.x + b.extents.x * 0.2f : b.center.x - b.extents.x * 0.2f;
        trailY = seamY;
        trailOffset = offsetX + dir * b.extents.x * 0.6f;
        trailGrowSeconds = Mathf.Max(0.05f, seconds);
        trail.sortingLayerID = layer;
        trail.sortingOrder = order + 2;
        trail.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        trail.enabled = true;
        trailAge = 0f;
        UpdateTrail(0f);

        // Spray from under the leading edge, thrown forward and up; a few flick back off the trailing edge.
        float lead = dir > 0f ? b.max.x : b.min.x;
        float trailEdge = dir > 0f ? b.min.x : b.max.x;
        for (int i = 0; i < DropletCount; i++)
        {
            bool back = i % 4 == 3;
            float t = (i + 0.5f) / DropletCount;
            float x = back ? trailEdge : lead;
            float speed = Mathf.Lerp(2.2f, 4.4f, Mathf.Repeat(t * 7.3f, 1f));
            float angle = Mathf.Lerp(20f, 70f, Mathf.Repeat(t * 3.7f, 1f)) * Mathf.Deg2Rad;
            float side = back ? -dir * 0.6f : dir;
            var v = new Vector2(Mathf.Cos(angle) * speed * side, Mathf.Sin(angle) * speed);
            SpawnDroplet(new Vector2(x, seamY), v, layer, order + 3);
        }
    }

    /// <summary>Clears everything at once (run restart).</summary>
    public void HideAll()
    {
        for (int i = 0; i < droplets.Count; i++)
        {
            Droplet d = droplets[i];
            d.age = float.MaxValue;
            d.sr.enabled = false;
            droplets[i] = d;
        }
        trailAge = float.MaxValue;
        trail.enabled = false;
        sheenAge = float.MaxValue;
        ParkSheen();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < droplets.Count; i++)
        {
            Droplet d = droplets[i];
            if (d.age >= DropletLife) continue;

            d.age += dt;
            if (d.age >= DropletLife)
            {
                d.sr.enabled = false;
                droplets[i] = d;
                continue;
            }

            d.velocity.y -= DropletGravity * dt;
            Transform tr = d.sr.transform;
            tr.position += (Vector3)(d.velocity * dt);
            tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.velocity.y, d.velocity.x) * Mathf.Rad2Deg - 90f);
            Color c = DropletColor;
            c.a *= 1f - d.age / DropletLife;
            d.sr.color = c;
            droplets[i] = d;
        }

        if (trailAge < trailGrowSeconds + TrailHoldSeconds + TrailFadeSeconds)
        {
            trailAge += dt;
            UpdateTrail(trailAge);
        }
        else if (trail.enabled)
        {
            trail.enabled = false;
        }

        if (sheenAge < SheenSeconds && sheen != null)
        {
            sheenAge += dt;
            float k = Mathf.Clamp01(sheenAge / SheenSeconds);
            // Quick wash in, slow drain out.
            float a = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
            Color c = SheenColor;
            c.a *= a * a;
            sheen.color = c;
            if (sheenAge >= SheenSeconds) ParkSheen();
        }
    }

    private void UpdateTrail(float age)
    {
        float grow = Mathf.Clamp01(age / trailGrowSeconds);
        grow = grow * grow * (3f - 2f * grow);
        float length = Mathf.Max(0.05f, Mathf.Abs(trailOffset) * grow);
        float centreX = trailStartX + Mathf.Sign(trailOffset) * length * 0.5f;

        trail.transform.position = new Vector3(centreX, trailY, 0f);
        trail.transform.localScale = new Vector3(TrailThickness / StreakWidth(trail.sprite), length / StreakHeight(trail.sprite), 1f);

        float fade = age <= trailGrowSeconds + TrailHoldSeconds
            ? 1f
            : 1f - Mathf.Clamp01((age - trailGrowSeconds - TrailHoldSeconds) / TrailFadeSeconds);
        Color c = TrailColor;
        c.a *= fade;
        trail.color = c;
    }

    private void SpawnDroplet(Vector2 at, Vector2 velocity, int layer, int order)
    {
        int index = nextDroplet;
        nextDroplet = (nextDroplet + 1) % droplets.Count;
        Droplet d = droplets[index];

        d.age = 0f;
        d.velocity = velocity;
        d.sr.sortingLayerID = layer;
        d.sr.sortingOrder = order;
        d.sr.color = DropletColor;
        d.sr.transform.position = new Vector3(at.x, at.y, 0f);
        d.sr.transform.localScale = new Vector3(DropletSize.x / StreakWidth(d.sr.sprite), DropletSize.y / StreakHeight(d.sr.sprite), 1f);
        d.sr.enabled = true;
        droplets[index] = d;
    }

    private void ParkSheen()
    {
        if (sheen == null) return;
        sheen.enabled = false;
        sheen.transform.SetParent(transform, false);
    }

    private static SpriteRenderer CreateRenderer(string name, Sprite sprite, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.enabled = false;
        return sr;
    }

    private static float StreakWidth(Sprite s) => s != null ? Mathf.Max(0.001f, s.bounds.size.x) : 1f;
    private static float StreakHeight(Sprite s) => s != null ? Mathf.Max(0.001f, s.bounds.size.y) : 1f;

    /// <summary>
    /// A wet stone-on-stone squelch (a low-passed noise swoosh that brightens then drops away)
    /// with two water drips after it. Stands in until SFX_Rain_Slide is dropped into
    /// Assets/Art/Audio/Rules and wired.
    /// </summary>
    public static AudioClip PlaceholderSlide()
    {
        if (placeholderSlide != null) return placeholderSlide;

        const int sampleRate = 22050;
        const float lengthSeconds = 0.62f;
        const float swooshSeconds = 0.34f;

        int samples = Mathf.CeilToInt(sampleRate * lengthSeconds);
        int swoosh = Mathf.CeilToInt(sampleRate * swooshSeconds);
        var data = new float[samples];
        var rng = new System.Random(4242);
        float low = 0f, band = 0f;

        // Swoosh: noise through a filter that opens then closes, with a gritty flutter.
        for (int i = 0; i < swoosh; i++)
        {
            float t = (float)i / swoosh;
            float cutoff = Mathf.Lerp(0.06f, 0.35f, Mathf.Sin(t * Mathf.PI));
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * cutoff;
            band += (low - band) * 0.08f;
            float env = Mathf.Min(1f, t * 14f) * (1f - t) * (1f - t);
            float flutter = 0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * 22f);
            data[i] = (low - band) * env * flutter * 1.6f;
        }

        // Two drips: a short sine whose pitch falls fast, like a drop hitting a puddle.
        AddDrip(data, sampleRate, 0.30f, 1400f, 0.35f);
        AddDrip(data, sampleRate, 0.44f, 1100f, 0.25f);

        float peak = 0f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0f)
        {
            float gain = 0.8f / peak;
            for (int i = 0; i < samples; i++) data[i] *= gain;
        }

        placeholderSlide = AudioClip.Create("RainSlidePlaceholder", samples, 1, sampleRate, false);
        placeholderSlide.SetData(data, 0);
        return placeholderSlide;
    }

    private static void AddDrip(float[] data, int sampleRate, float atSeconds, float startHz, float amplitude)
    {
        int start = Mathf.RoundToInt(atSeconds * sampleRate);
        int length = Mathf.RoundToInt(0.07f * sampleRate);
        float phase = 0f;
        for (int i = 0; i < length && start + i < data.Length; i++)
        {
            float t = (float)i / length;
            float hz = startHz * Mathf.Lerp(1f, 0.45f, Mathf.Sqrt(t));
            phase += 2f * Mathf.PI * hz / sampleRate;
            float env = Mathf.Min(1f, i / (0.002f * sampleRate)) * Mathf.Exp(-5f * t);
            data[start + i] += Mathf.Sin(phase) * env * amplitude;
        }
    }
}
