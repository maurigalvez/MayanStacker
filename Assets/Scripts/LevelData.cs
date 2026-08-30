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
    [Tooltip("Score required for 1 star (minimum to pass)")]
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
    /// Calculate the number of stars earned based on score
    /// </summary>
    public int CalculateStars(int score)
    {
        if (score < oneStarScore)
            return 0; // Failed to complete level
        else if (score < twoStarScore)
            return 1;
        else if (score < threeStarScore)
            return 2;
        else
            return 3; // Perfect score!
    }
}

