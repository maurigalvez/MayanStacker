using UnityEngine;

/// <summary>
/// Temple rule <see cref="LevelRule.RainSlick"/>: rain falls, and a stone that doesn't land
/// Perfect slides a little further the way it was already leaning. "Good" stops being safe.
///
/// The slide is a short tween of the one stone (the same careful move Jaguar Slam uses), capped
/// at a share of the stone's width so rain alone never throws a stone off. The landing keeps
/// the score and tier it earned — only the tower's shape pays.
/// </summary>
public class RainSlick : TempleRuleBehaviour
{
    private const int StreakCount = 40;
    private const float StreakLength = 0.7f;
    private const int StreakSortingOrder = 12;
    private static readonly Color StreakColor = new Color(0.78f, 0.86f, 0.9f, 0.45f);
    private const float MinSlide = 0.02f;

    public override LevelRule Rule => LevelRule.RainSlick;

    private RainSlickSettings settings;
    private AmbientSpriteField rain;

    public static int SlidesThisRun { get; private set; }

    protected override void Build()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        rain = go.AddComponent<AmbientSpriteField>();
        Sprite streak = art.rainStreak != null ? art.rainStreak : RulePlaceholderArt.Streak;
        rain.Build(streak, StreakCount, StreakLength, StreakColor, StreakSortingOrder);
        rain.AlignToVelocity = true;
    }

    protected override void OnRunStart(LevelData level)
    {
        settings = RuleActive ? level.rainSettings : null;
        SlidesThisRun = 0;

        if (RuleActive)
        {
            rain.Density = 1f;
            rain.Show();
            SetLoop(art.rainLoop, 0.6f);
        }
        else
        {
            rain.Density = 0f;
            FadeOutLoop();
        }
    }

    protected override void OnRunEnd(bool won)
    {
        if (!RuleActive) return;
        rain.Density = won ? 0f : 0.5f;
        FadeOutLoop();
    }

    protected override void OnStoneLanded(StackableObject stone)
    {
        if (settings == null || stackManager == null || gameManager == null) return;
        if (stackManager.GetTopObject() != stone) return;

        int height = stackManager.GetStackCount();
        if (height < 2) return;

        // The final stone belongs to the capstone, which locks the tower.
        LevelData level = Level;
        if (level != null && height >= level.requiredStackHeight) return;

        float accuracy = stone.LandingAccuracy;
        if (accuracy >= gameManager.PerfectThreshold) return;
        if (accuracy < gameManager.GoodThreshold && !settings.poorSlides) return;

        var list = stackManager.StackObjects;
        StackableObject below = list[list.Count - 2];
        if (below == null || stone.Collider == null) return;

        float offset = stone.ColliderCenterX - below.ColliderCenterX;
        if (Mathf.Abs(offset) < 0.001f) return;

        float width = stone.Collider.bounds.size.x;
        float slide = Mathf.Min(Mathf.Abs(offset) * settings.slideFraction, settings.maxSlideOfWidth * width);
        if (slide < MinSlide) return;

        if (stackManager.NudgeTopStone(Mathf.Sign(offset) * slide, settings.slideSeconds))
        {
            SlidesThisRun++;
            PlayOneShot(art.rainSlideSound, 0.9f);
            HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!RuleActive) return;

        // Falls with the jungle wind when both rules run.
        rain.Velocity = new Vector2(SwingModifiers.LateralOffset * 4f, -14f);
    }
}
