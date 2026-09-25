using UnityEngine;

/// <summary>
/// ScriptableObject that defines level configuration and objectives
/// </summary>
[CreateAssetMenu(fileName = "Level_", menuName = "TamalStacker/Level Data", order = 1)]
public class LevelData : ScriptableObject
{
    [Header("Level Info")]
    [Tooltip("Unique level identifier")]
    public int levelNumber = 1;

    [Tooltip("Display name for the level")]
    public string levelName = "Level 1";

    [Tooltip("Location of the ruin (e.g., 'Chiapas, Mexico')")]
    public string location = "";
    [TextArea(3, 5)]
    [Tooltip("Optional description of the level")]
    public string levelDescription = "";
    [Tooltip("Image of the archaeological site")]
    public Sprite siteImage;

    [Header("Level Objectives")]
    [Tooltip("Required stack height to complete the level")]
    [Min(1)]
    public int requiredStackHeight = 10;

    [Tooltip("Extra condition on top of the height requirement. Reach Height is the original behaviour.")]
    public LevelObjective objective = LevelObjective.ReachHeight;

    [Tooltip("Perfect Chain: how many consecutive perfect landings are needed.")]
    [Min(2)]
    public int requiredPerfectChain = 4;

    [Tooltip("Swift Ascent: seconds allowed to reach the required height.")]
    [Min(5f)]
    public float timeLimitSeconds = 60f;

    [Header("Star Rating Thresholds")]
    [Tooltip("Score for 1 star. Cosmetic: reaching the height always earns at least 1 star.")]
    public int oneStarScore = 100;
    [Tooltip("Score required for 2 stars")]
    public int twoStarScore = 500;

    [Tooltip("Score required for 3 stars (perfect)")]
    public int threeStarScore = 1000;

    [Header("Level Settings")]
    [Tooltip("Swing speed modifier for this level (1.0 = default)")]
    [Range(0.5f, 3.0f)]
    public float swingSpeedMultiplier = 1.0f;

    [Tooltip("Swing amplitude modifier for this level (1.0 = default)")]
    [Range(0.5f, 2.0f)]
    public float swingAmplitudeMultiplier = 1.0f;

    [Header("Block Sequence")]
    [Tooltip("Optional hand-authored block order, from the bottom of the stack up. Leave empty to let the level's seed decide every block - it will still be the same order on every attempt.")]
    public LevelBlockSequence blockSequence = new LevelBlockSequence();

    [Header("Temple Rule")]
    [Tooltip("A visible rule that makes this temple play differently. None is the original behaviour.")]
    public LevelRule rule = LevelRule.None;

    [Tooltip("A second rule running alongside the first, for the late temples. None = one rule.")]
    public LevelRule secondRule = LevelRule.None;

    [Tooltip("Used when either rule is Jungle Wind.")]
    public JungleWindSettings windSettings = new JungleWindSettings();

    [Tooltip("Used when either rule is Rain-slick Stones.")]
    public RainSlickSettings rainSettings = new RainSlickSettings();

    [Tooltip("Used when either rule is Earthquake.")]
    public EarthquakeSettings quakeSettings = new EarthquakeSettings();

    [Tooltip("Used when either rule is Rising Cenote.")]
    public RisingCenoteSettings cenoteSettings = new RisingCenoteSettings();

    [Tooltip("Used when either rule is Eclipse.")]
    public EclipseSettings eclipseSettings = new EclipseSettings();

    /// <summary>True when this temple runs <paramref name="r"/>, as its first or second rule.</summary>
    public bool HasRule(LevelRule r) => r != LevelRule.None && (rule == r || secondRule == r);

    [Header("Capstone")]
    [Tooltip("Play the capstone beat when the final stone lands: the stack locks, the camera " +
             "punches in, a gold flash and the temple's silhouette, then the result panel.")]
    public bool capstonePayoff = false;

    [Tooltip("This temple's silhouette, drawn over the finished tower. Optional - without it " +
             "the capstone is the gold flash alone.")]
    public Sprite capstoneSilhouette;

    [Header("Audio")]
    [Tooltip("Music track to play for this level (optional - uses default if not set)")]
    public AudioClip gameMusic;

    /// <summary>
    /// True when this level asks for anything beyond reaching the height, i.e. when the
    /// objective is worth announcing to the player.
    /// </summary>
    public bool HasExtraObjective => objective != LevelObjective.ReachHeight;

    /// <summary>
    /// One localized line describing what this temple wants, ready to show to the player.
    /// Returns the plain height requirement for an ordinary level.
    /// </summary>
    public string GetObjectiveDescription()
    {
        switch (objective)
        {
            case LevelObjective.FlawlessAscent:
                return LocalizationManager.Get("objective_flawless_desc", requiredStackHeight);

            case LevelObjective.PerfectChain:
                return LocalizationManager.Get("objective_perfect_chain_desc", requiredPerfectChain);

            case LevelObjective.SwiftAscent:
                return LocalizationManager.Get("objective_swift_desc",
                    requiredStackHeight, Mathf.RoundToInt(timeLimitSeconds));

            case LevelObjective.ReachHeight:
            default:
                return LocalizationManager.Get("objective_reach_height_desc", requiredStackHeight);
        }
    }

    /// <summary>
    /// Stars for a completed climb. Only called once the height (and objective) is met, so
    /// completion is always worth at least 1 star - the score only decides 2 and 3. A
    /// score gate on 1 star used to block the next temple from unlocking even after the
    /// player reached the top.
    /// </summary>
    public int CalculateStars(int score)
    {
        if (score < twoStarScore)
            return 1;
        else if (score < threeStarScore)
            return 2;
        else
            return 3; // Perfect score!
    }
}

