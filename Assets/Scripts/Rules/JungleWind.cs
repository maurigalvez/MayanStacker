using UnityEngine;

/// <summary>
/// Temple rule <see cref="LevelRule.JungleWind"/>: gusts lean the swing and push a falling
/// stone to one side, and leaves blowing across the screen show which way and how hard.
///
/// The gusts follow a fixed pattern per temple (keyed on stack height, not time or chance),
/// so every attempt and every player faces the same wind. A change of wind builds over
/// <see cref="JungleWindSettings.gustRampSeconds"/>, so the leaves turn before it bites.
/// </summary>
public class JungleWind : TempleRuleBehaviour
{
    private const int LeafCount = 22;
    private const float LeafSize = 0.45f;
    private const float LeafSpeedAtFullGust = 7f;
    private const int LeafSortingOrder = 12; // in front of the stones, behind the UI

    public override LevelRule Rule => LevelRule.JungleWind;

    private JungleWindSettings settings;
    private AmbientSpriteField leaves;

    private float wind;        // -1..1, current
    private float targetWind;  // -1..1, the gust for the current height
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
    }

    protected override void OnRunStart(LevelData level)
    {
        settings = RuleActive ? level.windSettings : null;
        wind = 0f;
        targetWind = 0f;
        lastGust = int.MinValue;
        falling = null;
        fallingStone = null;
        SwingModifiers.LateralOffset = 0f;

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
        falling = null;
        fallingStone = null;
        leaves.Density = 0f;
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
            return;
        }

        int gust = (height - settings.firstAtHeight) / Mathf.Max(1, settings.stonesPerGust);
        if (gust == lastGust) return;

        int level = Level != null ? Level.levelNumber : 0;
        float dirWave = Mathf.Sin(gust * 2.3f + level * 1.7f + 0.4f);
        float direction = dirWave >= 0f ? 1f : -1f;
        float strength = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(gust * 1.3f + level));

        float previous = targetWind;
        targetWind = direction * strength;

        if (lastGust != int.MinValue && Mathf.Sign(previous) != Mathf.Sign(targetWind))
        {
            PlayOneShot(art.windGustSound, 0.8f);
        }

        lastGust = gust;
    }

    protected override void Update()
    {
        base.Update();
        if (!RuleActive || settings == null) return;

        float rate = 1f / Mathf.Max(0.05f, settings.gustRampSeconds);
        wind = Mathf.MoveTowards(wind, targetWind, rate * Time.deltaTime);

        SwingModifiers.LateralOffset = wind * settings.swingPush;

        leaves.Velocity = new Vector2(wind * LeafSpeedAtFullGust, -0.6f);
        leaves.Density = RunLive ? Mathf.Lerp(0.25f, 1f, Mathf.Abs(wind)) : 0f;

        if (RunLive) SetLoop(art.windLoop, Mathf.Lerp(0.25f, 0.8f, Mathf.Abs(wind)));
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
}
