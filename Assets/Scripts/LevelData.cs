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

    [TextArea(3, 5)]
    [Tooltip("Optional description of the level")]
    public string levelDescription = "";

    [Header("Level Objectives")]
    [Tooltip("Required stack height to complete the level")]
    [Min(1)]
    public int requiredStackHeight = 10;

    [Header("Star Rating Thresholds")]
    [Tooltip("Score required for 1 star (minimum to pass)")]
    [Min(0)]
    public int oneStarScore = 100;

    [Tooltip("Score required for 2 stars")]
    [Min(0)]
    public int twoStarScore = 500;

    [Tooltip("Score required for 3 stars (perfect)")]
    [Min(0)]
    public int threeStarScore = 1000;

    [Header("Level Settings")]
    [Tooltip("Swing speed modifier for this level (1.0 = default)")]
    [Range(0.5f, 3.0f)]
    public float swingSpeedMultiplier = 1.0f;

    [Tooltip("Swing amplitude modifier for this level (1.0 = default)")]
    [Range(0.5f, 2.0f)]
    public float swingAmplitudeMultiplier = 1.0f;

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

    /// <summary>
    /// Validate the ScriptableObject data
    /// </summary>
    private void OnValidate()
    {
        // Ensure star thresholds are in ascending order
        if (twoStarScore < oneStarScore)
            twoStarScore = oneStarScore;

        if (threeStarScore < twoStarScore)
            threeStarScore = twoStarScore;

        // Ensure required stack height is at least 1
        if (requiredStackHeight < 1)
            requiredStackHeight = 1;
    }
}

