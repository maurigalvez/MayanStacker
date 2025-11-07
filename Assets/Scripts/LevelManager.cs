using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages level progression, level data, and player progress in Stacker Levels mode
/// </summary>
public class LevelManager : MonoBehaviour, ILevelManager
{
    [Header("Demo Settings")]
    [SerializeField] private bool isDemoVersion = false;
    [SerializeField] private int demoMaxLevel = 5;

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
    public System.Action<int, int> OnLevelCompleted; // stars, score
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

        // Notify listeners
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
    /// </summary>
    private void ApplyLevelSettings(LevelData level)
    {
        if (level == null) return;

        // Apply swing settings to spawner holder
        var spawnerHolder = DependencyRegistry.Find<SpawnerHolder>();
        if (spawnerHolder != null)
        {
            float baseSpeed = spawnerHolder.SwingSpeed;
            float baseAmplitude = spawnerHolder.SwingAmplitude;

            spawnerHolder.SetSwingSpeed(baseSpeed * level.swingSpeedMultiplier);
            spawnerHolder.SetSwingAmplitude(baseAmplitude * level.swingAmplitudeMultiplier);
        }
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

        // Save progress
        SaveLevelProgress(CurrentLevel.levelNumber, earnedStars, finalScore);

        // Notify listeners
        OnLevelCompleted?.Invoke(earnedStars, finalScore);

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
    /// Save level progress to PlayerPrefs
    /// </summary>
    private void SaveLevelProgress(int levelNumber, int stars, int score)
    {
        // Update in-memory dictionaries
        if (!levelStars.ContainsKey(levelNumber) || stars > levelStars[levelNumber])
        {
            levelStars[levelNumber] = stars;
            PlayerPrefs.SetInt($"Level_{levelNumber}_Stars", stars);
        }

        if (!levelHighScores.ContainsKey(levelNumber) || score > levelHighScores[levelNumber])
        {
            levelHighScores[levelNumber] = score;
            PlayerPrefs.SetInt($"Level_{levelNumber}_HighScore", score);
        }

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load saved progress from PlayerPrefs
    /// </summary>
    private void LoadProgress()
    {
        levelStars.Clear();
        levelHighScores.Clear();

        foreach (var level in levels)
        {
            int levelNumber = level.levelNumber;

            int savedStars = PlayerPrefs.GetInt($"Level_{levelNumber}_Stars", 0);
            if (savedStars > 0)
            {
                levelStars[levelNumber] = savedStars;
            }

            int savedHighScore = PlayerPrefs.GetInt($"Level_{levelNumber}_HighScore", 0);
            if (savedHighScore > 0)
            {
                levelHighScores[levelNumber] = savedHighScore;
            }
        }
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
    }
}

