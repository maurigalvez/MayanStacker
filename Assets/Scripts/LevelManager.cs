using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages level progression, level data, and player progress in Stacker Levels mode
/// </summary>
public class LevelManager : MonoBehaviour, ILevelManager
{
    [Header("Demo Settings")]
    [SerializeField] private bool isDemoVersion = false;
    [SerializeField] private int demoMaxLevel = 5;

#if UNITY_EDITOR
    [Header("Editor Only - Testing")]
    [SerializeField] private bool unlockAllLevelsInEditor = false;
    [Tooltip("When enabled in editor, all levels will be unlocked (given 1 star) on Start")]
#endif

    [Header("Level Configuration")]
    [SerializeField] private List<LevelData> levels = new List<LevelData>();

    [Header("Current Level")]
    [SerializeField] private int currentLevelIndex = -1;

    // Level state
    private bool isLevelComplete = false;
    private int earnedStars = 0;
    private Dictionary<int, int> levelStars = new Dictionary<int, int>(); // levelNumber -> stars earned
    private Dictionary<int, int> levelHighScores = new Dictionary<int, int>(); // levelNumber -> high score

    // Events
    public System.Action<LevelData> OnLevelLoaded;
    public System.Action<int, int, bool> OnLevelCompleted; // stars, score, isFirstCompletion
    public System.Action OnLevelFailed;
    public System.Action<int> OnStackHeightUpdated; // current height

    // Properties
    public LevelData CurrentLevel => currentLevelIndex >= 0 && currentLevelIndex < levels.Count ? levels[currentLevelIndex] : null;
    public int CurrentLevelIndex => currentLevelIndex;
    public bool IsLevelComplete => isLevelComplete;
    public int EarnedStars => earnedStars;
    public int TotalLevels => levels.Count;
    public bool IsDemoVersion => isDemoVersion;
    public int DemoMaxLevel => demoMaxLevel;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<LevelManager>(this);
        DependencyRegistry.Register<ILevelManager>(this as ILevelManager);

        // Load saved progress
        LoadProgress();
    }

    private void Start()
    {
        // Subscribe to game events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
        }

        // Subscribe to stack events
        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }

        // Subscribe to PlayFab sync events for cloud save
        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null)
        {
            playFabManager.OnProgressSynced += OnProgressSyncedFromCloud;
        }

#if UNITY_EDITOR
        // Editor-only: Unlock all levels if enabled
        if (unlockAllLevelsInEditor)
        {
            UnlockAllLevels();
        }
#endif
    }

    /// <summary>
    /// Load a specific level by index
    /// </summary>
    public void LoadLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levels.Count)
        {
            Debug.LogError($"Invalid level index: {levelIndex}. Valid range: 0-{levels.Count - 1}");
            return;
        }

        currentLevelIndex = levelIndex;
        isLevelComplete = false;
        earnedStars = 0;

        // Apply level settings
        ApplyLevelSettings(CurrentLevel);

        // Tell GameManager which level we're on for proper high score tracking
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null && CurrentLevel != null)
        {
            gameManager.SetCurrentLevel(CurrentLevel.levelNumber);
        }

        // Notify listeners
        int subscriberCount = OnLevelLoaded?.GetInvocationList()?.Length ?? 0;
        Debug.Log($"LevelManager: Invoking OnLevelLoaded event with {subscriberCount} subscribers");
        OnLevelLoaded?.Invoke(CurrentLevel);

        Debug.Log($"Loaded {CurrentLevel.levelName} - Required Height: {CurrentLevel.requiredStackHeight}");
    }

    /// <summary>
    /// Load the next level in sequence
    /// </summary>
    public void NextLevel()
    {
        int nextIndex = currentLevelIndex + 1;
        if (nextIndex < levels.Count)
        {
            LoadLevel(nextIndex);
        }
        else
        {
            Debug.Log("No more levels available! You've completed all levels!");
            // Could trigger a "game complete" event here
        }
    }

    /// <summary>
    /// Restart the current level
    /// </summary>
    public void RestartLevel()
    {
        if (CurrentLevel != null)
        {
            LoadLevel(currentLevelIndex);

            // Also restart the game
            var gameManager = DependencyRegistry.Find<GameManager>();
            if (gameManager != null)
            {
                gameManager.RestartGame();
            }
        }
    }

    /// <summary>
    /// Apply level-specific settings to game systems
    /// Note: Swing settings are now handled by SpawnerHolder subscribing to OnLevelLoaded event
    /// </summary>
    private void ApplyLevelSettings(LevelData level)
    {
        if (level == null) return;

        // Swing settings are now applied automatically by SpawnerHolder when it receives OnLevelLoaded event
        // No need to manually set them here to avoid race conditions or double-application
    }

    /// <summary>
    /// Check if level objective is complete when a new object is added to stack
    /// </summary>
    private void OnObjectAddedToStack(StackableObject stackableObject)
    {
        if (isLevelComplete) return;

        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager == null) return;

        int currentHeight = stackManager.GetStackCount();

        // Notify UI of height progress
        OnStackHeightUpdated?.Invoke(currentHeight);

        // Check if we've reached the required height
        if (CurrentLevel != null && currentHeight >= CurrentLevel.requiredStackHeight)
        {
            CompleteLevelSuccessfully();
        }
    }

    /// <summary>
    /// Called when level objective is achieved
    /// </summary>
    private void CompleteLevelSuccessfully()
    {
        if (isLevelComplete || CurrentLevel == null) return;

        isLevelComplete = true;

        // Get final score
        var gameManager = DependencyRegistry.Find<GameManager>();
        int finalScore = gameManager != null ? gameManager.CurrentScore : 0;

        // Calculate stars earned
        earnedStars = CurrentLevel.CalculateStars(finalScore);

        // Check if this is the first completion BEFORE saving
        int levelNumber = CurrentLevel.levelNumber;
        bool isFirstCompletion = !IsLevelCompletedBefore(levelNumber);
        bool codexNotShown = !IsCodexUnlocked(levelNumber);

        // Save progress
        SaveLevelProgress(levelNumber, earnedStars, finalScore);

        // Notify listeners with first completion flag
        OnLevelCompleted?.Invoke(earnedStars, finalScore, isFirstCompletion && codexNotShown && earnedStars > 0);

        Debug.Log($"Level Complete! Stars: {earnedStars}/3, Score: {finalScore}");
    }

    /// <summary>
    /// Called when the game is over (stack fell)
    /// </summary>
    private void OnGameOver()
    {
        if (isLevelComplete) return; // Already completed

        // Check if we at least reached minimum requirement
        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager == null) return;

        int currentHeight = stackManager.GetStackCount();

        if (CurrentLevel != null && currentHeight >= CurrentLevel.requiredStackHeight)
        {
            // Completed height requirement before falling
            CompleteLevelSuccessfully();
        }
        else
        {
            // Failed to complete level
            OnLevelFailed?.Invoke();
            Debug.Log($"Level Failed! Required: {CurrentLevel?.requiredStackHeight}, Reached: {currentHeight}");
        }
    }

    private void OnGameRestart()
    {
        // Reset level state but keep the same level loaded
        isLevelComplete = false;
        earnedStars = 0;
    }

    /// <summary>
    /// Save level progress to PlayerPrefs (stars only - high scores saved via PlayFab)
    /// </summary>
    private void SaveLevelProgress(int levelNumber, int stars, int score)
    {
        // Update stars in-memory and PlayerPrefs
        if (!levelStars.ContainsKey(levelNumber) || stars > levelStars[levelNumber])
        {
            levelStars[levelNumber] = stars;
            PlayerPrefs.SetInt($"Level_{levelNumber}_Stars", stars);
        }

        // Update in-memory high score (actual saving to PlayFab is done by GameManager)
        if (!levelHighScores.ContainsKey(levelNumber) || score > levelHighScores[levelNumber])
        {
            levelHighScores[levelNumber] = score;
        }

        PlayerPrefs.Save();
        Debug.Log($"Saved level {levelNumber} progress: {stars} stars");

        // Also save to cloud
        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null && playFabManager.IsLoggedIn)
        {
            playFabManager.SaveCurrentProgressToCloud();
        }
    }

    /// <summary>
    /// Mark codex as unlocked for a level (called from UI after showing popup)
    /// </summary>
    public void MarkCodexUnlockedForLevel(int levelNumber)
    {
        MarkCodexUnlocked(levelNumber);
    }

    /// <summary>
    /// Check if a level has been completed before (has at least 1 star)
    /// </summary>
    public bool IsLevelCompletedBefore(int levelNumber)
    {
        return GetLevelStars(levelNumber) > 0;
    }

    /// <summary>
    /// Check if codex has been unlocked for a level (shown to player)
    /// </summary>
    public bool IsCodexUnlocked(int levelNumber)
    {
        return PlayerPrefs.GetInt($"Level_{levelNumber}_CodexUnlocked", 0) == 1;
    }

    /// <summary>
    /// Mark codex as unlocked for a level
    /// </summary>
    private void MarkCodexUnlocked(int levelNumber)
    {
        PlayerPrefs.SetInt($"Level_{levelNumber}_CodexUnlocked", 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load saved progress from PlayerPrefs (stars) and prepare for PlayFab high score loading
    /// </summary>
    private void LoadProgress()
    {
        levelStars.Clear();
        levelHighScores.Clear();

        foreach (var level in levels)
        {
            int levelNumber = level.levelNumber;

            // Load stars from PlayerPrefs
            int savedStars = PlayerPrefs.GetInt($"Level_{levelNumber}_Stars", 0);
            if (savedStars > 0)
            {
                levelStars[levelNumber] = savedStars;
            }

            // Load high scores from PlayerPrefs as backup (PlayFab is primary source)
            int savedHighScore = PlayerPrefs.GetInt($"Level_{levelNumber}_HighScore", 0);
            if (savedHighScore > 0)
            {
                levelHighScores[levelNumber] = savedHighScore;
            }
        }

        // High scores will be loaded from PlayFab when each level is played
        Debug.Log($"Loaded level progress: {levelStars.Count} levels with stars");
    }

    /// <summary>
    /// Get the number of stars earned for a specific level
    /// </summary>
    public int GetLevelStars(int levelNumber)
    {
        return levelStars.ContainsKey(levelNumber) ? levelStars[levelNumber] : 0;
    }

    /// <summary>
    /// Get the high score for a specific level
    /// </summary>
    public int GetLevelHighScore(int levelNumber)
    {
        return levelHighScores.ContainsKey(levelNumber) ? levelHighScores[levelNumber] : 0;
    }

    /// <summary>
    /// Check if a level is unlocked (for sequential progression)
    /// </summary>
    public bool IsLevelUnlocked(int levelNumber)
    {
        // First level is always unlocked
        if (levelNumber == 1) return true;

        // Demo version restriction: levels beyond demoMaxLevel are locked
        if (isDemoVersion && levelNumber > demoMaxLevel)
        {
            return false;
        }

        // Check if previous level has at least 1 star
        return GetLevelStars(levelNumber - 1) > 0;
    }

    /// <summary>
    /// Get all level data
    /// </summary>
    public List<LevelData> GetAllLevels()
    {
        return new List<LevelData>(levels);
    }

    /// <summary>
    /// Reset all level progress (stars and high scores). Call this from Inspector for debugging.
    /// </summary>
    [ContextMenu("Reset Level Progress")]
    public void ResetLevelProgress()
    {
        Debug.LogWarning("Resetting all level progress...");

        // Clear in-memory dictionaries
        levelStars.Clear();
        levelHighScores.Clear();

        // Delete all level-related PlayerPrefs
        foreach (var level in levels)
        {
            int levelNumber = level.levelNumber;
            PlayerPrefs.DeleteKey($"Level_{levelNumber}_Stars");
            PlayerPrefs.DeleteKey($"Level_{levelNumber}_HighScore");
        }

        PlayerPrefs.Save();
        Debug.Log("Level progress reset complete!");
    }

    /// <summary>
    /// Reset ALL PlayerPrefs (including settings). Use with caution!
    /// </summary>
    [ContextMenu("Reset ALL PlayerPrefs (CAUTION)")]
    public void ResetAllPlayerPrefs()
    {
        Debug.LogWarning("Resetting ALL PlayerPrefs (including settings)...");
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // Reload progress (will be empty now)
        LoadProgress();

        Debug.Log("ALL PlayerPrefs reset complete!");
    }

    /// <summary>
    /// Called when progress is synced from cloud (after login)
    /// Updates local progress with cloud data (server authoritative)
    /// </summary>
    private void OnProgressSyncedFromCloud(PlayerProgressData data)
    {
        if (data == null) return;

        Debug.Log($"Syncing level progress from cloud... Levels with stars: {data.levelStars.Count}");

        // Update level stars from cloud (server authoritative - cloud always wins)
        foreach (var kvp in data.levelStars)
        {
            int levelNumber = kvp.Key;
            int cloudStars = kvp.Value;

            // Update in-memory
            if (!levelStars.ContainsKey(levelNumber) || cloudStars > levelStars[levelNumber])
            {
                levelStars[levelNumber] = cloudStars;

                // Update PlayerPrefs cache
                PlayerPrefs.SetInt($"Level_{levelNumber}_Stars", cloudStars);

                Debug.Log($"Updated level {levelNumber} stars from cloud: {cloudStars}");
            }
        }

        // Update level high scores from cloud
        foreach (var kvp in data.levelHighScores)
        {
            int levelNumber = kvp.Key;
            int cloudHighScore = kvp.Value;

            // Update in-memory
            if (!levelHighScores.ContainsKey(levelNumber) || cloudHighScore > levelHighScores[levelNumber])
            {
                levelHighScores[levelNumber] = cloudHighScore;

                // Update PlayerPrefs cache
                PlayerPrefs.SetInt($"Level_{levelNumber}_HighScore", cloudHighScore);

                Debug.Log($"Updated level {levelNumber} high score from cloud: {cloudHighScore}");

                // Update GameManager if this is the current level
                var gameManager = DependencyRegistry.Find<GameManager>();
                if (gameManager != null && CurrentLevel != null && CurrentLevel.levelNumber == levelNumber)
                {
                    gameManager.UpdateHighScoreFromCloud(cloudHighScore);
                }
            }
        }

        // Save updated cache to disk
        PlayerPrefs.Save();

        Debug.Log("Level progress sync complete!");
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<LevelManager>(this);
        DependencyRegistry.Unregister<ILevelManager>(this as ILevelManager);

        // Unsubscribe from events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnGameRestart -= OnGameRestart;
        }

        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }

        // Unsubscribe from PlayFab events
        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null)
        {
            playFabManager.OnProgressSynced -= OnProgressSyncedFromCloud;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only: Unlock all levels by giving them at least 1 star (for testing)
    /// </summary>
    [ContextMenu("Unlock All Levels (Editor Only)")]
    public void UnlockAllLevels()
    {
        if (!Application.isEditor)
        {
            Debug.LogWarning("UnlockAllLevels can only be called in the Unity Editor!");
            return;
        }

        int unlockedCount = 0;

        foreach (var level in levels)
        {
            if (level == null) continue;

            int levelNumber = level.levelNumber;

            // Give level at least 1 star if it doesn't have any
            int currentStars = GetLevelStars(levelNumber);
            if (currentStars == 0)
            {
                levelStars[levelNumber] = 1;
                PlayerPrefs.SetInt($"Level_{levelNumber}_Stars", 1);
                unlockedCount++;
            }
        }

        PlayerPrefs.Save();
        Debug.Log($"LevelManager (Editor): Unlocked {unlockedCount} levels! All levels are now playable.");
    }
#endif
}

