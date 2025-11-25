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

