using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Game Mode")]
    [SerializeField] private GameMode currentGameMode = GameMode.InfiniteStacker;
    [SerializeField] private bool waitForGameModeSelection = false;
    [SerializeField] private bool autoStartAfterModeSet = true;

    [Header("Game Settings")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int highScore = 0;
    [SerializeField] private bool isGameActive = false;
    [SerializeField] private bool isGameOver = false;

    [Header("Combo System")]
    [SerializeField] private int currentCombo = 0;
    [SerializeField] private int maxCombo = 0;
    [Tooltip("Minimum accuracy (0-1) required to maintain combo")]
    [SerializeField] private float comboMinAccuracy = 0.6f; // Good or better
    [Tooltip("Multiplier increase per consecutive landing (e.g., 1.0 = +1.0x per landing)")]
    [SerializeField] private float multiplierIncrement = 1.0f;
    [Tooltip("Maximum combo multiplier")]
    [SerializeField] private float maxComboMultiplier = 5f;
    [Tooltip("Time in seconds before combo decays (0 = no decay)")]
    [SerializeField] private float comboDecayTime = 3f;
    [Tooltip("Enable combo decay timer")]
    [SerializeField] private bool enableComboDecay = true;

    // Internal state
    private bool gameModeInitialized = false;
    private int currentLevelNumber = -1; // For StackerLevels mode
    private bool highScoreSaved = false; // Track if high score has been saved this session
    private float lastComboUpdateTime = 0f; // Track time since last combo update
    private bool comboDecayActive = false; // Track if decay timer is running
    private AccuracyLevel lastAccuracyLevel = AccuracyLevel.None; // Track last accuracy level for consistency check

    // Accuracy level enum for combo consistency
    private enum AccuracyLevel
    {
        None,
        Poor,
        Good,
        Perfect
    }

    [Header("Game Events")]
    public System.Action<int> OnScoreChanged;
    public System.Action<int> OnHighScoreChanged;
    public System.Action OnGameStart;
    public System.Action OnGameOver;
    public System.Action OnGameRestart;
    public System.Action<GameMode> OnGameModeChanged;
    public System.Action<int, float> OnComboChanged; // combo count, multiplier

    // Properties
    public GameMode CurrentGameMode => currentGameMode;
    public int CurrentScore => currentScore;
    public int HighScore => highScore;
    public bool IsGameActive => isGameActive;
    public bool IsGameOver => isGameOver;
    public bool IsGameModeInitialized => gameModeInitialized;
    public int CurrentCombo => currentCombo;
    public int MaxCombo => maxCombo;
    public float CurrentMultiplier => GetComboMultiplier();

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<GameManager>(this);

        // Don't load high score here - wait for game mode to be set
    }

    private void Start()
    {
        // Subscribe to PlayFab sync events for cloud save
        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null)
        {
            playFabManager.OnProgressSynced += OnProgressSyncedFromCloud;
        }

        // Only auto-start if not waiting for game mode selection
        if (!waitForGameModeSelection)
        {
            gameModeInitialized = true;
            StartGame();
        }
        else
        {
            Debug.Log("Waiting for game mode to be set before starting...");
        }
    }

    private void Update()
    {
        // Check for combo decay if enabled and game is active
        if (enableComboDecay && isGameActive && !isGameOver && comboDecayActive)
        {
            CheckComboDecay();
        }
    }

    public void StartGame()
    {
        isGameActive = true;
        isGameOver = false;
        currentScore = 0;
        currentCombo = 0;
        lastComboUpdateTime = 0f;
        comboDecayActive = false;
        lastAccuracyLevel = AccuracyLevel.None;
        highScoreSaved = false; // Reset save flag for new game session
        OnGameStart?.Invoke();
        OnScoreChanged?.Invoke(currentScore);
        OnComboChanged?.Invoke(currentCombo, GetComboMultiplier());
    }

    /// <summary>
    /// Adds score with combo multiplier based on landing accuracy
    /// </summary>
    /// <param name="basePoints">Base points before multiplier</param>
    /// <param name="accuracy">Landing accuracy (0-1)</param>
    /// <returns>Actual points awarded after multiplier</returns>
    public int AddScoreWithCombo(int basePoints, float accuracy)
    {
        if (!isGameActive || isGameOver) return 0;

        // Update combo based on accuracy
        UpdateCombo(accuracy);

        // Calculate points with multiplier
        float multiplier = GetComboMultiplier();
        int finalPoints = Mathf.RoundToInt(basePoints * multiplier);

        currentScore += finalPoints;
        OnScoreChanged?.Invoke(currentScore);

        // Check for new high score (but don't update UI or save yet)
        // High score UI will only update when a saved high score is loaded
        if (currentScore > highScore)
        {
            highScore = currentScore;
            // DON'T invoke OnHighScoreChanged here - only invoke when loading saved scores
        }

        return finalPoints;
    }

    /// <summary>
    /// Legacy method for adding score without combo (kept for backward compatibility)
    /// </summary>
    public void AddScore(int points)
    {
        if (!isGameActive || isGameOver) return;

        currentScore += points;
        OnScoreChanged?.Invoke(currentScore);

        // Check for new high score (but don't update UI or save yet)
        // High score UI will only update when a saved high score is loaded
        if (currentScore > highScore)
        {
            highScore = currentScore;
            // DON'T invoke OnHighScoreChanged here - only invoke when loading saved scores
        }
    }

    /// <summary>
    /// Updates combo count based on landing accuracy
    /// Combo requires CONSECUTIVE landings of Good or Perfect (can upgrade from Good to Perfect)
    /// </summary>
    private void UpdateCombo(float accuracy)
    {
        // Determine current accuracy level
        AccuracyLevel currentAccuracyLevel;
        if (accuracy >= 0.9f)
        {
            currentAccuracyLevel = AccuracyLevel.Perfect;
        }
        else if (accuracy >= comboMinAccuracy) // 0.6f by default
        {
            currentAccuracyLevel = AccuracyLevel.Good;
        }
        else
        {
            currentAccuracyLevel = AccuracyLevel.Poor;
        }

        // Check if we should maintain or break the combo
        bool shouldMaintainCombo = false;

        if (currentAccuracyLevel == AccuracyLevel.Poor)
        {
            // Poor landing always breaks combo
            shouldMaintainCombo = false;
        }
        else if (currentCombo == 0)
        {
            // Starting a new combo - accept either Good or Perfect
            shouldMaintainCombo = true;
        }
        else
        {
            // Combo is active - maintain if same level OR upgrading from Good to Perfect
            bool isSameLevel = (currentAccuracyLevel == lastAccuracyLevel);
            bool isUpgrade = (lastAccuracyLevel == AccuracyLevel.Good && currentAccuracyLevel == AccuracyLevel.Perfect);
            shouldMaintainCombo = isSameLevel || isUpgrade;
        }

        if (shouldMaintainCombo)
        {
            currentCombo++;

            // Track max combo
            if (currentCombo > maxCombo)
            {
                maxCombo = currentCombo;
            }

            // Reset decay timer
            lastComboUpdateTime = Time.time;
            comboDecayActive = true;

            // Update last accuracy level
            lastAccuracyLevel = currentAccuracyLevel;

            Debug.Log($"Combo maintained! Level: {currentAccuracyLevel}, Combo: {currentCombo}, Multiplier: {GetComboMultiplier()}x");
        }
        else
        {
            // Combo broken - reset
            if (currentCombo > 0)
            {
                Debug.Log($"Combo broken! Previous: {lastAccuracyLevel}, Current: {currentAccuracyLevel}");
            }

            currentCombo = 0;
            comboDecayActive = false;
            lastAccuracyLevel = AccuracyLevel.None;
        }

        // Notify listeners
        OnComboChanged?.Invoke(currentCombo, GetComboMultiplier());
    }

    /// <summary>
    /// Checks if combo should decay due to timeout
    /// </summary>
    private void CheckComboDecay()
    {
        if (currentCombo == 0)
        {
            comboDecayActive = false;
            return;
        }

        float timeSinceLastCombo = Time.time - lastComboUpdateTime;

        if (timeSinceLastCombo >= comboDecayTime)
        {
            // Time expired - reset combo
            Debug.Log($"Combo decayed! Time since last: {timeSinceLastCombo:F2}s");
            currentCombo = 0;
            comboDecayActive = false;
            lastAccuracyLevel = AccuracyLevel.None;
            OnComboChanged?.Invoke(currentCombo, GetComboMultiplier());
        }
    }

    /// <summary>
    /// Gets remaining time before combo decays
    /// </summary>
    public float GetComboTimeRemaining()
    {
        if (!enableComboDecay || currentCombo == 0 || !comboDecayActive)
        {
            return 0f;
        }

        float timeSinceLastCombo = Time.time - lastComboUpdateTime;
        float timeRemaining = comboDecayTime - timeSinceLastCombo;
        return Mathf.Max(0f, timeRemaining);
    }

    /// <summary>
    /// Calculates current combo multiplier based on combo count
    /// </summary>
    private float GetComboMultiplier()
    {
        if (currentCombo <= 1) return 1f;

        // Calculate multiplier: starts at 1x, increases by multiplierIncrement starting from 2nd landing
        // Example: with multiplierIncrement = 1.0:
        // Combo 1: 1x (first landing, no bonus yet), Combo 2: 2x, Combo 3: 3x, Combo 4: 4x, etc.
        float multiplier = 1f + ((currentCombo - 1) * multiplierIncrement);

        // Cap at max multiplier
        return Mathf.Min(multiplier, maxComboMultiplier);
    }

    /// <summary>
    /// Resets the combo (can be called externally if needed)
    /// </summary>
    public void ResetCombo()
    {
        currentCombo = 0;
        comboDecayActive = false;
        lastAccuracyLevel = AccuracyLevel.None;
        OnComboChanged?.Invoke(currentCombo, GetComboMultiplier());
    }

    public void GameOver()
    {
        if (isGameOver) return;

        isGameActive = false;
        isGameOver = true;

        // For InfiniteStacker mode, save high score on game over
        if (currentGameMode == GameMode.InfiniteStacker && currentScore > 0)
        {
            SaveHighScoreIfNeeded();
        }

        OnGameOver?.Invoke();
    }

    public void RestartGame()
    {
        isGameActive = false;
        isGameOver = false;
        currentScore = 0;
        currentCombo = 0;
        lastComboUpdateTime = 0f;
        comboDecayActive = false;
        lastAccuracyLevel = AccuracyLevel.None;
        OnGameRestart?.Invoke();
        OnScoreChanged?.Invoke(currentScore);
        OnComboChanged?.Invoke(currentCombo, GetComboMultiplier());

        // Restart the game after a brief delay
        Invoke(nameof(StartGame), 0.5f);
    }

    public void SetGameMode(GameMode newMode, bool startGameNow = true, int levelNumber = -1)
    {
        bool modeChanged = currentGameMode != newMode;

        currentGameMode = newMode;
        gameModeInitialized = true;
        currentLevelNumber = levelNumber;

        // Reload high score for the new mode/level
        LoadHighScore();

        if (modeChanged)
        {
            OnGameModeChanged?.Invoke(currentGameMode);
            Debug.Log($"Game Mode changed to: {currentGameMode}");
        }
        else
        {
            Debug.Log($"Game Mode set to: {currentGameMode}");
        }

        // Auto-start game if configured and not already active
        if (autoStartAfterModeSet && startGameNow && !isGameActive)
        {
            StartGame();
        }
    }

    /// <summary>
    /// Initialize game mode without starting the game
    /// </summary>
    public void InitializeGameMode(GameMode mode, int levelNumber = -1)
    {
        SetGameMode(mode, startGameNow: false, levelNumber: levelNumber);
    }

    /// <summary>
    /// Set the current level number for StackerLevels mode
    /// </summary>
    public void SetCurrentLevel(int levelNumber)
    {
        currentLevelNumber = levelNumber;
        LoadHighScore(); // Reload high score for this level
    }

    /// <summary>
    /// Load high score from PlayFab for the current mode/level
    /// This is called by PlayFabManager after retrieving the score
    /// </summary>
    public void LoadHighScore()
    {
        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null && playFabManager.IsLoggedIn)
        {
            // Request high score from PlayFab
            if (currentGameMode == GameMode.InfiniteStacker)
            {
                playFabManager.LoadHighScore("InfiniteStackerHighScores", OnHighScoreLoaded);
            }
            else if (currentGameMode == GameMode.StackerLevels && currentLevelNumber > 0)
            {
                string leaderboardName = $"StackerLevel_{currentLevelNumber}";
                playFabManager.LoadHighScore(leaderboardName, OnHighScoreLoaded);
            }
        }
        else
        {
            // Fallback to PlayerPrefs if PlayFab not ready
            LoadHighScoreFromPlayerPrefs();
        }
    }

    /// <summary>
    /// Fallback method to load high score from PlayerPrefs
    /// </summary>
    private void LoadHighScoreFromPlayerPrefs()
    {
        string key;
        if (currentGameMode == GameMode.InfiniteStacker)
        {
            key = "HighScore_InfiniteStacker";
        }
        else if (currentLevelNumber > 0)
        {
            key = $"Level_{currentLevelNumber}_HighScore";
        }
        else
        {
            key = "HighScore_Levels";
        }

        highScore = PlayerPrefs.GetInt(key, 0);
        OnHighScoreChanged?.Invoke(highScore);
        Debug.Log($"Loaded high score from PlayerPrefs: {highScore} (Key: {key})");
    }

    /// <summary>
    /// Called when high score is loaded from PlayFab
    /// </summary>
    private void OnHighScoreLoaded(int loadedHighScore)
    {
        highScore = loadedHighScore;
        OnHighScoreChanged?.Invoke(highScore);
        Debug.Log($"Loaded high score from PlayFab: {highScore}");
    }

    /// <summary>
    /// Save high score to PlayFab (only if it's a new high score and hasn't been saved yet)
    /// </summary>
    public void SaveHighScoreIfNeeded()
    {
        if (highScoreSaved)
        {
            Debug.Log("High score already saved this session");
            return;
        }

        // Only save if current score beats or equals the high score
        if (currentScore < highScore)
        {
            Debug.Log($"Current score ({currentScore}) is not a high score ({highScore}), not saving");
            return;
        }

        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null && playFabManager.IsLoggedIn)
        {
            if (currentGameMode == GameMode.InfiniteStacker)
            {
                playFabManager.SubmitScore("InfiniteStackerHighScores", currentScore);
                Debug.Log($"Saving InfiniteStacker high score to PlayFab: {currentScore}");
            }
            else if (currentGameMode == GameMode.StackerLevels && currentLevelNumber > 0)
            {
                string leaderboardName = $"StackerLevel_{currentLevelNumber}";
                playFabManager.SubmitScore(leaderboardName, currentScore);
                Debug.Log($"Saving StackerLevel {currentLevelNumber} high score to PlayFab: {currentScore}");
            }

            // Also save to PlayerPrefs as backup
            SaveHighScoreToPlayerPrefs();

            highScoreSaved = true;
        }
        else
        {
            Debug.LogWarning("PlayFab not ready, saving to PlayerPrefs only");
            SaveHighScoreToPlayerPrefs();
            highScoreSaved = true;
        }
    }

    /// <summary>
    /// Save high score to PlayerPrefs as backup
    /// </summary>
    private void SaveHighScoreToPlayerPrefs()
    {
        string key;
        if (currentGameMode == GameMode.InfiniteStacker)
        {
            key = "HighScore_InfiniteStacker";
        }
        else if (currentLevelNumber > 0)
        {
            key = $"Level_{currentLevelNumber}_HighScore";
        }
        else
        {
            key = "HighScore_Levels";
        }

        PlayerPrefs.SetInt(key, currentScore);
        PlayerPrefs.Save();
        Debug.Log($"Saved high score to PlayerPrefs: {currentScore} (Key: {key})");
        
        // Also save to cloud
        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null && playFabManager.IsLoggedIn)
        {
            playFabManager.SaveCurrentProgressToCloud();
        }
    }

    /// <summary>
    /// Called when progress is synced from cloud (after login)
    /// Updates local high score if cloud has a higher value (server authoritative)
    /// </summary>
    private void OnProgressSyncedFromCloud(PlayerProgressData data)
    {
        if (data == null) return;

        // Update Infinite Stacker high score from cloud
        if (currentGameMode == GameMode.InfiniteStacker)
        {
            if (data.infiniteStackerHighScore > highScore)
            {
                Debug.Log($"Updating Infinite Stacker high score from cloud: {data.infiniteStackerHighScore}");
                highScore = data.infiniteStackerHighScore;
                
                // Update PlayerPrefs cache
                PlayerPrefs.SetInt("HighScore_InfiniteStacker", highScore);
                PlayerPrefs.Save();
                
                // Notify UI
                OnHighScoreChanged?.Invoke(highScore);
            }
        }
        // Level mode high scores are handled by LevelManager
    }

    /// <summary>
    /// Updates high score from cloud data
    /// Called by LevelManager when level-specific high scores are synced
    /// </summary>
    public void UpdateHighScoreFromCloud(int cloudHighScore)
    {
        if (cloudHighScore > highScore)
        {
            Debug.Log($"Updating high score from cloud: {cloudHighScore}");
            highScore = cloudHighScore;
            OnHighScoreChanged?.Invoke(highScore);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from PlayFab events
        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null)
        {
            playFabManager.OnProgressSynced -= OnProgressSyncedFromCloud;
        }

        DependencyRegistry.Unregister<GameManager>(this);
    }
}

