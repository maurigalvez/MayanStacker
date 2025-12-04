using TamalStacker.Achievements;
using UnityEngine;

/// <summary>
/// Manages resetting and deleting player data
/// Accessible via Context Menu in Inspector
/// </summary>
public class PlayerDataReset : MonoBehaviour
{
    [Header("Data Reset Settings")]
    [Tooltip("Maximum level number to check when clearing level data (adjust if you have more levels)")]
    [SerializeField] private int maxLevelNumberToCheck = 50;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<PlayerDataReset>(this);
    }

    /// <summary>
    /// Resets all high scores (keeps settings and level progress)
    /// Accessible via Context Menu: Right-click on PlayerDataReset component in Inspector
    /// </summary>
    [ContextMenu("Reset All Scores")]
    public void ResetAllScores()
    {
        Debug.LogWarning("[PlayerDataReset] Resetting all scores...");

        // Get GameManager to reload high score after clearing
        var gameManager = DependencyRegistry.Find<GameManager>();

        // Clear Infinite Stacker high score
        PlayerPrefs.DeleteKey("HighScore_InfiniteStacker");
        PlayerPrefs.DeleteKey("HighScore_Levels");

        // Clear all level high scores
        for (int i = 1; i <= maxLevelNumberToCheck; i++)
        {
            PlayerPrefs.DeleteKey($"Level_{i}_HighScore");
        }

        PlayerPrefs.Save();
        Debug.Log("[PlayerDataReset] All scores have been reset!");

        // Reload high score to update UI via GameManager
        if (gameManager != null)
        {
            gameManager.LoadHighScore();
        }
    }

    /// <summary>
    /// Resets all achievement progress
    /// Accessible via Context Menu: Right-click on PlayerDataReset component in Inspector
    /// </summary>
    [ContextMenu("Reset All Achievements")]
    public void ResetAllAchievements()
    {
        Debug.LogWarning("[PlayerDataReset] Resetting all achievement progress...");

        // Try to use AchievementManager if available
        var achievementManager = DependencyRegistry.Find<AchievementManager>();
        if (achievementManager != null)
        {
            achievementManager.ResetAllProgress();
        }
        else
        {
            // Fallback: directly delete from PlayerPrefs
            PlayerPrefs.DeleteKey("AchievementProgressData");
            PlayerPrefs.Save();
            Debug.Log("[PlayerDataReset] Achievement data cleared from PlayerPrefs");
        }

        Debug.Log("[PlayerDataReset] All achievements have been reset!");
    }

    /// <summary>
    /// Deletes all player data including scores, progress, achievements, and settings
    /// Accessible via Context Menu: Right-click on PlayerDataReset component in Inspector
    /// </summary>
    [ContextMenu("Delete All Player Data")]
    public void DeleteAllPlayerData()
    {
        Debug.LogWarning("[PlayerDataReset] DELETING ALL PLAYER DATA - This cannot be undone!");

        // Reset scores first
        ResetAllScores();

        // Clear level progress (stars and codex unlocks)
        for (int i = 1; i <= maxLevelNumberToCheck; i++)
        {
            PlayerPrefs.DeleteKey($"Level_{i}_Stars");
            PlayerPrefs.DeleteKey($"Level_{i}_CodexUnlocked");
        }

        // Clear achievement data
        ResetAllAchievements();

        // Clear settings
        PlayerPrefs.DeleteKey("MasterVolume");
        PlayerPrefs.DeleteKey("MusicVolume");
        PlayerPrefs.DeleteKey("SFXVolume");
        PlayerPrefs.DeleteKey("MasterMute");
        PlayerPrefs.DeleteKey("MusicMute");
        PlayerPrefs.DeleteKey("SFXMute");

        // Clear other player data
        PlayerPrefs.DeleteKey("InfiniteMode_InstructionsSeen");
        PlayerPrefs.DeleteKey("ReviewPromptShown");

        PlayerPrefs.Save();
        Debug.Log("[PlayerDataReset] All player data has been deleted!");

        // Reload settings (they'll use defaults now)
        var settingsManager = DependencyRegistry.Find<SettingsManager>();
        if (settingsManager != null)
        {
            // SettingsManager will reload defaults on next Awake/Start
            Debug.Log("[PlayerDataReset] Settings will be reset to defaults on next scene load");
        }
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<PlayerDataReset>(this);
    }
}

