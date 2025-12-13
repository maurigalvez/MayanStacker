using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages game theme/skin selection and unlock status
/// Themes unlock based on level completion progress
/// </summary>
public enum GameTheme
{
    Day,      // Default, always unlocked
    Sunset,   // Unlocks at 7 completed levels
    Night     // Unlocks at 15 completed levels
}

public class ThemeManager : MonoBehaviour
{
    // Unlock requirements
    private const int SUNSET_UNLOCK_LEVELS = 7;
    private const int NIGHT_UNLOCK_LEVELS = 15;

    // PlayerPrefs keys
    private const string SELECTED_THEME_KEY = "SelectedTheme";
    private const string SUNSET_UNLOCKED_KEY = "SunsetThemeUnlocked";
    private const string NIGHT_UNLOCKED_KEY = "NightThemeUnlocked";

    // Events
    public System.Action<GameTheme> OnThemeChanged;
    public System.Action<GameTheme> OnThemeUnlocked;

    // State
    private GameTheme selectedTheme = GameTheme.Day;
    private bool sunsetUnlocked = false;
    private bool nightUnlocked = false;

    // References
    private LevelManager levelManager;

    // Properties
    public GameTheme SelectedTheme => selectedTheme;
    public bool IsSunsetUnlocked => sunsetUnlocked;
    public bool IsNightUnlocked => nightUnlocked;

    private void Awake()
    {
        // 1. Check if instance already exists
        var existingInstance = DependencyRegistry.Find<ThemeManager>();
        if (existingInstance != null && existingInstance != this)
        {
            Debug.LogWarning("ThemeManager instance already exists. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        // 2. Register with dependency registry
        DependencyRegistry.Register<ThemeManager>(this);

        // 3. Load saved state
        LoadUnlockStatus();
        LoadSelectedTheme();

        // 4. Persist across scenes
        DontDestroyOnLoad(gameObject);

        // 5. Subscribe to scene loading events to refresh dependencies
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Refresh dependencies and subscribe to events
        RefreshDependencies();

        // Check for unlocks on start (in case levels were completed before ThemeManager existed)
        CheckAndUnlockThemes();
    }

    /// <summary>
    /// Called when a new scene is loaded
    /// Refreshes dependencies since managers may have been recreated
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshDependencies();
    }

    /// <summary>
    /// Refreshes dependencies and re-subscribes to events
    /// Call this when switching scenes or if managers are recreated
    /// </summary>
    private void RefreshDependencies()
    {
        // Unsubscribe from old references if they exist
        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= OnLevelCompleted;
        }

        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null)
        {
            playFabManager.OnProgressSynced -= OnProgressSyncedFromCloud;
        }

        // Find new references
        levelManager = DependencyRegistry.Find<LevelManager>();

        // Subscribe to level completion events
        if (levelManager != null)
        {
            levelManager.OnLevelCompleted += OnLevelCompleted;
        }

        // Subscribe to PlayFab sync events to recheck unlocks after cloud sync
        playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null)
        {
            playFabManager.OnProgressSynced += OnProgressSyncedFromCloud;
        }
    }

    /// <summary>
    /// Called when a level is completed - check if themes should unlock
    /// </summary>
    private void OnLevelCompleted(int stars, int score, bool isFirstCompletion)
    {
        if (stars <= 0) return; // Only count levels with stars > 0

        CheckAndUnlockThemes();
    }

    /// <summary>
    /// Check completed level count and unlock themes if requirements are met
    /// </summary>
    private void CheckAndUnlockThemes()
    {
        int completedLevels = GetCompletedLevelCount();

        // Check Sunset unlock (7 levels)
        if (!sunsetUnlocked && completedLevels >= SUNSET_UNLOCK_LEVELS)
        {
            UnlockTheme(GameTheme.Sunset);
        }

        // Check Night unlock (15 levels)
        if (!nightUnlocked && completedLevels >= NIGHT_UNLOCK_LEVELS)
        {
            UnlockTheme(GameTheme.Night);
        }
    }

    /// <summary>
    /// Get the count of completed levels (levels with stars > 0)
    /// </summary>
    private int GetCompletedLevelCount()
    {
        if (levelManager == null) return 0;

        int completedCount = 0;
        var allLevels = levelManager.GetAllLevels();

        foreach (var level in allLevels)
        {
            int stars = levelManager.GetLevelStars(level.levelNumber);
            if (stars > 0)
            {
                completedCount++;
            }
        }

        return completedCount;
    }

    /// <summary>
    /// Check if a theme is unlocked
    /// </summary>
    public bool IsThemeUnlocked(GameTheme theme)
    {
        switch (theme)
        {
            case GameTheme.Day:
                return true; // Always unlocked
            case GameTheme.Sunset:
                return sunsetUnlocked;
            case GameTheme.Night:
                return nightUnlocked;
            default:
                return false;
        }
    }

    /// <summary>
    /// Unlock a theme (called when requirements are met)
    /// </summary>
    private void UnlockTheme(GameTheme theme)
    {
        switch (theme)
        {
            case GameTheme.Sunset:
                if (!sunsetUnlocked)
                {
                    sunsetUnlocked = true;
                    SaveUnlockStatus();
                    OnThemeUnlocked?.Invoke(GameTheme.Sunset);
                    Debug.Log("Sunset theme unlocked!");
                }
                break;
            case GameTheme.Night:
                if (!nightUnlocked)
                {
                    nightUnlocked = true;
                    SaveUnlockStatus();
                    OnThemeUnlocked?.Invoke(GameTheme.Night);
                    Debug.Log("Night theme unlocked!");
                }
                break;
        }
    }

    /// <summary>
    /// Set the unlock status for themes (used when syncing from cloud)
    /// </summary>
    public void SetThemeUnlockStatus(bool sunsetUnlocked, bool nightUnlocked)
    {
        bool sunsetChanged = this.sunsetUnlocked != sunsetUnlocked;
        bool nightChanged = this.nightUnlocked != nightUnlocked;

        this.sunsetUnlocked = sunsetUnlocked;
        this.nightUnlocked = nightUnlocked;

        // Save if changed
        if (sunsetChanged || nightChanged)
        {
            SaveUnlockStatus();
        }

        // Fire events for newly unlocked themes
        if (sunsetChanged && sunsetUnlocked)
        {
            OnThemeUnlocked?.Invoke(GameTheme.Sunset);
        }
        if (nightChanged && nightUnlocked)
        {
            OnThemeUnlocked?.Invoke(GameTheme.Night);
        }
    }

    /// <summary>
    /// Called when progress is synced from cloud - recheck unlocks based on synced level progress
    /// </summary>
    private void OnProgressSyncedFromCloud(PlayerProgressData data)
    {
        // Recheck unlocks after cloud sync (level progress may have changed)
        CheckAndUnlockThemes();
    }

    /// <summary>
    /// Set the selected theme (saved locally only)
    /// </summary>
    public void SetSelectedTheme(GameTheme theme)
    {
        if (!IsThemeUnlocked(theme))
        {
            Debug.LogWarning($"Cannot select theme {theme} - it is not unlocked!");
            return;
        }

        if (selectedTheme != theme)
        {
            selectedTheme = theme;
            SaveSelectedTheme();
            OnThemeChanged?.Invoke(theme);
            Debug.Log($"Theme changed to: {theme}");
        }
    }

    /// <summary>
    /// Get the currently selected theme
    /// </summary>
    public GameTheme GetSelectedTheme()
    {
        return selectedTheme;
    }

    /// <summary>
    /// Get the number of completed levels (public for UI display)
    /// </summary>
    public int GetCompletedLevelCountPublic()
    {
        return GetCompletedLevelCount();
    }

    /// <summary>
    /// Save unlock status to PlayerPrefs
    /// </summary>
    private void SaveUnlockStatus()
    {
        PlayerPrefs.SetInt(SUNSET_UNLOCKED_KEY, sunsetUnlocked ? 1 : 0);
        PlayerPrefs.SetInt(NIGHT_UNLOCKED_KEY, nightUnlocked ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load unlock status from PlayerPrefs
    /// </summary>
    private void LoadUnlockStatus()
    {
        sunsetUnlocked = PlayerPrefs.GetInt(SUNSET_UNLOCKED_KEY, 0) == 1;
        nightUnlocked = PlayerPrefs.GetInt(NIGHT_UNLOCKED_KEY, 0) == 1;
    }

    /// <summary>
    /// Save selected theme to PlayerPrefs (local only, not synced)
    /// </summary>
    private void SaveSelectedTheme()
    {
        PlayerPrefs.SetInt(SELECTED_THEME_KEY, (int)selectedTheme);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load selected theme from PlayerPrefs
    /// </summary>
    private void LoadSelectedTheme()
    {
        int savedTheme = PlayerPrefs.GetInt(SELECTED_THEME_KEY, (int)GameTheme.Day);
        selectedTheme = (GameTheme)savedTheme;

        // Validate that the saved theme is still unlocked (in case save data is corrupted)
        if (!IsThemeUnlocked(selectedTheme))
        {
            selectedTheme = GameTheme.Day;
            SaveSelectedTheme();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from scene loading events
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Unregister from dependency registry
        DependencyRegistry.Unregister<ThemeManager>(this);

        // Unsubscribe from events
        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= OnLevelCompleted;
        }

        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null)
        {
            playFabManager.OnProgressSynced -= OnProgressSyncedFromCloud;
        }
    }

#if UNITY_EDITOR
    [Header("Debug (Editor Only)")]
    [SerializeField] private bool showDebugOptions = true;

    /// <summary>
    /// Debug: Set theme to Day (Editor only)
    /// </summary>
    [ContextMenu("Debug: Set Theme to Day")]
    private void DebugSetThemeDay()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug theme selection only works in Play Mode!");
            return;
        }

        // Unlock all themes for testing
        DebugUnlockAllThemes();
        SetSelectedTheme(GameTheme.Day);
        Debug.Log($"[DEBUG] Theme set to: Day");
    }

    /// <summary>
    /// Debug: Set theme to Sunset (Editor only)
    /// </summary>
    [ContextMenu("Debug: Set Theme to Sunset")]
    private void DebugSetThemeSunset()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug theme selection only works in Play Mode!");
            return;
        }

        // Unlock all themes for testing
        DebugUnlockAllThemes();
        SetSelectedTheme(GameTheme.Sunset);
        Debug.Log($"[DEBUG] Theme set to: Sunset");
    }

    /// <summary>
    /// Debug: Set theme to Night (Editor only)
    /// </summary>
    [ContextMenu("Debug: Set Theme to Night")]
    private void DebugSetThemeNight()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug theme selection only works in Play Mode!");
            return;
        }

        // Unlock all themes for testing
        DebugUnlockAllThemes();
        SetSelectedTheme(GameTheme.Night);
        Debug.Log($"[DEBUG] Theme set to: Night");
    }

    /// <summary>
    /// Debug: Unlock all themes (Editor only)
    /// </summary>
    [ContextMenu("Debug: Unlock All Themes")]
    private void DebugUnlockAllThemes()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug theme selection only works in Play Mode!");
            return;
        }

        bool changed = false;
        if (!sunsetUnlocked)
        {
            sunsetUnlocked = true;
            changed = true;
            OnThemeUnlocked?.Invoke(GameTheme.Sunset);
        }
        if (!nightUnlocked)
        {
            nightUnlocked = true;
            changed = true;
            OnThemeUnlocked?.Invoke(GameTheme.Night);
        }

        if (changed)
        {
            SaveUnlockStatus();
            Debug.Log("[DEBUG] All themes unlocked!");
        }
        else
        {
            Debug.Log("[DEBUG] All themes already unlocked.");
        }
    }

    /// <summary>
    /// Debug: Lock all themes except Day (Editor only)
    /// </summary>
    [ContextMenu("Debug: Lock All Themes (Except Day)")]
    private void DebugLockAllThemes()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug theme selection only works in Play Mode!");
            return;
        }

        bool changed = false;
        if (sunsetUnlocked)
        {
            sunsetUnlocked = false;
            changed = true;
        }
        if (nightUnlocked)
        {
            nightUnlocked = false;
            changed = true;
        }

        // Reset to Day if a locked theme was selected
        if (!IsThemeUnlocked(selectedTheme))
        {
            selectedTheme = GameTheme.Day;
            SaveSelectedTheme();
            OnThemeChanged?.Invoke(GameTheme.Day);
        }

        if (changed)
        {
            SaveUnlockStatus();
            Debug.Log("[DEBUG] All themes locked (except Day).");
        }
        else
        {
            Debug.Log("[DEBUG] All themes already locked.");
        }
    }

    /// <summary>
    /// Debug: Cycle to next theme (Editor only)
    /// </summary>
    [ContextMenu("Debug: Cycle to Next Theme")]
    private void DebugCycleTheme()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug theme selection only works in Play Mode!");
            return;
        }

        // Unlock all themes for testing
        DebugUnlockAllThemes();

        GameTheme nextTheme = selectedTheme switch
        {
            GameTheme.Day => GameTheme.Sunset,
            GameTheme.Sunset => GameTheme.Night,
            GameTheme.Night => GameTheme.Day,
            _ => GameTheme.Day
        };

        SetSelectedTheme(nextTheme);
        Debug.Log($"[DEBUG] Theme cycled to: {nextTheme}");
    }

    /// <summary>
    /// Debug: Print current theme status (Editor only)
    /// </summary>
    [ContextMenu("Debug: Print Theme Status")]
    private void DebugPrintThemeStatus()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug theme selection only works in Play Mode!");
            return;
        }

        Debug.Log($"[DEBUG] Current Theme: {selectedTheme}");
        Debug.Log($"[DEBUG] Day Unlocked: True (always)");
        Debug.Log($"[DEBUG] Sunset Unlocked: {sunsetUnlocked}");
        Debug.Log($"[DEBUG] Night Unlocked: {nightUnlocked}");
        Debug.Log($"[DEBUG] Completed Levels: {GetCompletedLevelCountPublic()}");
    }
#endif
}

