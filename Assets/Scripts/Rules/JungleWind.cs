using UnityEngine;

/// <summary>
/// Temple rule <see cref="LevelRule.JungleWind"/>: gusts lean the swing and push a falling
/// stone to one side. Wind streaks (<see cref="WindLines"/>) and leaves blowing across the
/// screen show which way and how hard.
///
/// The gusts follow a fixed pattern per temple (keyed on stack height, not time or chance),
/// so every attempt and every player faces the same wind. A new gust is announced first —
/// a row of curled streaks sweeps across, the gust sound plays and the leaves turn — and only
/// starts to push <see cref="JungleWindSettings.leadSeconds"/> later, building over
/// <see cref="JungleWindSettings.gustRampSeconds"/>.
/// </summary>
public class JungleWind : TempleRuleBehaviour
{
    private const int LeafCount = 22;
    private const float LeafSize = 0.45f;
    private const float LeafSpeedAtFullGust = 7f;
    private const int LeafSortingOrder = 12; // in front of the stones, behind the UI
    private const int LineSortingOrder = 10; // under the leaves and their outlines (11)

    private static AudioClip placeholderGust;

    public override LevelRule Rule => LevelRule.JungleWind;

    private JungleWindSettings settings;
    private AmbientSpriteField leaves;
    private WindLines lines;

    private float wind;         // -1..1, what pushes right now
    private float shownWind;    // -1..1, what the streaks and leaves show (leads the push)
    private float targetWind;   // -1..1, the gust for the current height
    private float pushTarget;   // targetWind, once its lead-in has passed
    private float announcedAt;
    private int lastGust = int.MinValue;

    private Rigidbody2D falling; // the stone in the air, pushed each physics step
    private StackableObject fallingStone;

    protected override void Build()
    {
        var go = new GameObject("Leaves");
        go.transform.SetParent(transform, false);
        leaves = go.AddComponent<AmbientSpriteField>();
        Sprite leaf = art.windLeaf != null ? art.windLeaf : RulePlaceholderArt.Leaf;
        leaves.Build(leaf, LeafCount, LeafSize, Color.white, LeafSortingOrder);
        leaves.Spin = true;

        var linesGo = new GameObject("WindLines");
        linesGo.transform.SetParent(transform, false);
        lines = linesGo.AddComponent<WindLines>();
        lines.Build(LineSortingOrder);
    }

    protected override void OnRunStart(LevelData level)
    {
        settings = RuleActive ? level.windSettings : null;
        wind = 0f;
        shownWind = 0f;
        targetWind = 0f;
        pushTarget = 0f;
        lastGust = int.MinValue;
        falling = null;
        fallingStone = null;
        SwingModifiers.LateralOffset = 0f;
        lines.Strength = 0f;

        if (RuleActive)
        {
            leaves.Density = 0f;
            leaves.Show();
            UpdateGust();
        }
        else
        {
            leaves.Density = 0f;
            FadeOutLoop();
        }
    }

    protected override void OnRunEnd(bool won)
    {
        if (!RuleActive) return;
        targetWind = 0f;
        pushTarget = 0f;
        falling = null;
        fallingStone = null;
        leaves.Density = 0f;
        lines.Strength = 0f;
        FadeOutLoop();
    }

    protected override void OnStoneLanded(StackableObject stone)
    {
        if (stone == fallingStone)
        {
            falling = null;
            fallingStone = null;
        }
        UpdateGust();
    }

    protected override void OnStoneDropped(GameObject stone)
    {
        fallingStone = stone.GetComponent<StackableObject>();
        falling = stone.GetComponent<Rigidbody2D>();
    }

    /// <summary>The gust for the current height: fixed per temple, never random.</summary>
    private void UpdateGust()
    {
        if (settings == null || stackManager == null) return;

        int height = stackManager.GetStackCount();
        if (height < settings.firstAtHeight)
        {
            targetWind = 0f;
            pushTarget = 0f;
            return;
        }

        int gust = (height - settings.firstAtHeight) / Mathf.Max(1, settings.stonesPerGust);
        if (gust == lastGust) return;

        int level = Level != null ? Level.levelNumber : 0;
        int dirGust = settings.steadyDirection ? 0 : gust;
        float dirWave = Mathf.Sin(dirGust * 2.3f + level * 1.7f + 0.4f);
        float direction = dirWave >= 0f ? 1f : -1f;
        float strength = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(gust * 1.3f + level));

        float previous = targetWind;
        targetWind = direction * strength;
        announcedAt = Time.time;

        // The first gust and every change of direction get the full announcement.
        if (previous == 0f || Mathf.Sign(previous) != Mathf.Sign(targetWind))
        {
            PlayOneShot(art.windGustSound != null ? art.windGustSound : PlaceholderGust(), 0.8f);
            lines.Burst(direction);
        }

        lastGust = gust;
    }

    protected override void Update()
    {
        base.Update();
        if (!RuleActive || settings == null) return;

        if (Time.time - announcedAt >= settings.leadSeconds) pushTarget = targetWind;

        float ramp = Mathf.Max(0.05f, settings.gustRampSeconds);
        wind = Mathf.MoveTowards(wind, pushTarget, Time.deltaTime / ramp);
        // The picture turns faster than the push, so it is always a step ahead.
        shownWind = Mathf.MoveTowards(shownWind, targetWind, Time.deltaTime / (ramp * 0.5f));

        SwingModifiers.LateralOffset = wind * settings.swingPush;

        float shown = Mathf.Abs(shownWind);
        leaves.Velocity = new Vector2(shownWind * LeafSpeedAtFullGust, -0.6f);
        leaves.Density = RunLive ? Mathf.Lerp(0.25f, 1f, shown) : 0f;
        lines.Strength = RunLive ? shownWind : 0f;

        if (RunLive) SetLoop(art.windLoop, Mathf.Lerp(0.25f, 0.8f, shown));
    }

    private void FixedUpdate()
    {
        if (!RunLive || settings == null || falling == null) return;
        if (fallingStone == null || fallingStone.HasLanded)
        {
            falling = null;
            return;
        }
        if (falling.bodyType != RigidbodyType2D.Dynamic) return;

        falling.AddForce(new Vector2(wind * settings.fallPush * falling.mass, 0f), ForceMode2D.Force);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (RuleActive) SwingModifiers.LateralOffset = 0f;
    }

    /// <summary>
    /// A gust through the trees: band-passed noise that swells and falls away with a slow
    /// flutter, brightening at the peak. Stands in until SFX_Wind_Gust is dropped into
    /// Assets/Art/Audio/Rules and wired.
    /// </summary>
    public static AudioClip PlaceholderGust()
    {
        if (placeholderGust != null) return placeholderGust;

        const int sampleRate = 22050;
        const float lengthSeconds = 1.3f;

        int samples = Mathf.CeilToInt(sampleRate * lengthSeconds);
        var data = new float[samples];
        var rng = new System.Random(7117);
        float low = 0f, band = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            // Asymmetric swell: a quicker rise, a long tail.
            float s = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f));
            float env = s * s;
            float cutoff = Mathf.Lerp(0.03f, 0.16f, env);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * cutoff;
            band += (low - band) * 0.02f;
            float flutter = 0.8f + 0.2f * Mathf.Sin(t * 2f * Mathf.PI * 5.5f + Mathf.Sin(t * 13f));
            data[i] = (low - band) * env * flutter;
        }

        float peak = 0f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0f)
        {
            float gain = 0.75f / peak;
            for (int i = 0; i < samples; i++) data[i] *= gain;
        }

        placeholderGust = AudioClip.Create("WindGustPlaceholder", samples, 1, sampleRate, false);
        placeholderGust.SetData(data, 0);
        return placeholderGust;
    }
}
