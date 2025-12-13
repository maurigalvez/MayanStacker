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
    [Tooltip("Minimum accuracy (0-1) for Good landing classification (combos only count for Perfect hits >= 0.9f)")]
    [SerializeField] private float comboMinAccuracy = 0.6f; // Used for Good vs Poor classification
    [Tooltip("Multiplier increase per consecutive landing (e.g., 1.0 = +1.0x per landing)")]
    [SerializeField] private float multiplierIncrement = 1.0f;
    [Tooltip("Maximum combo multiplier")]
    [SerializeField] private float maxComboMultiplier = 5f;
    [Tooltip("Time in seconds before combo decays (0 = no decay)")]
    [SerializeField] private float comboDecayTime = 3f;
    [Tooltip("Enable combo decay timer")]
    [SerializeField] private bool enableComboDecay = true;

    [Header("Perfect Hit System")]
    [Tooltip("Number of consecutive perfect hits required to straighten the stack")]
    [SerializeField] private int perfectHitsRequired = 4;

    // Internal state
    private bool gameModeInitialized = false;
    private int currentLevelNumber = -1; // For StackerLevels mode
    private bool highScoreSaved = false; // Track if high score has been saved this session
    private float lastComboUpdateTime = 0f; // Track time since last combo update
    private bool comboDecayActive = false; // Track if decay timer is running
    private AccuracyLevel lastAccuracyLevel = AccuracyLevel.None; // Track last accuracy level for consistency check
    private int consecutivePerfectHits = 0; // Track consecutive perfect hits for stack straightening

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
    public System.Action OnPerfectHitStreak; // Triggered when required perfect hits are achieved
    public System.Action<int> OnConsecutivePerfectHitsChanged; // Triggered when consecutive perfect hits count changes (current count)

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
    public int ConsecutivePerfectHits => consecutivePerfectHits;
    public int PerfectHitsRequired => perfectHitsRequired;

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

        // Find IntegrityManager for game session checks
        var integrityManager = DependencyRegistry.Find<IntegrityManager>();

        // Don't auto-start if waiting for game mode selection (SceneLoader will handle starting)
        // This prevents StartGame() from being called multiple times, which can cause timing issues on Android
        // where UIManager might not be ready when OnGameStart fires, preventing gameTitleText from displaying
        if (!waitForGameModeSelection)
        {
            // Only auto-start if game mode is already initialized (meaning this is not a scene load scenario)
            // When SceneLoader loads a scene, it will initialize game mode and start the game, so we don't start here
            // This prevents duplicate StartGame() calls
            if (gameModeInitialized)
            {
                Debug.Log("Game mode already initialized in Start(), starting game...");
                StartGame();
            }
            else
            {
                Debug.Log("Game mode not yet initialized, SceneLoader will handle starting the game...");
            }
        }
        else
        {
            Debug.Log("Waiting for game mode to be set before starting...");
        }
    }

    private void Update()
    {
        // Check for combo decay if enabled and game is active
        // Don't run combo decay if game is over or level is completed
        if (enableComboDecay && isGameActive && !isGameOver && comboDecayActive)
        {
            // Check if level is completed (for level mode)
            var levelManager = DependencyRegistry.Find<LevelManager>();
            if (levelManager != null && levelManager.IsLevelComplete)
            {
                // Stop combo decay when level is completed
                comboDecayActive = false;
                return;
            }

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
        consecutivePerfectHits = 0; // Reset perfect hit streak
        OnConsecutivePerfectHitsChanged?.Invoke(consecutivePerfectHits); // Notify of reset
        highScoreSaved = false; // Reset save flag for new game session

        // Perform Standard Integrity check at game session start
        var integrityManager = DependencyRegistry.Find<IntegrityManager>();
        if (integrityManager != null && integrityManager.IsIntegrityChecksEnabled)
        {
            integrityManager.PerformGameSessionCheck((result) =>
            {
                if (!result.Success)
                {
                    Debug.LogWarning($"[GameManager] Game session integrity check failed: {result.ErrorMessage}");
                }
            });
        }

        OnGameStart?.Invoke();
        OnScoreChanged?.Invoke(currentScore);
        OnComboChanged?.Invoke(currentCombo, GetComboMultiplier());
    }

    /// <summary>
    /// Adds score with combo multiplier based on landing accuracy
    /// Combo multiplier only applies for perfect hits (accuracy >= 0.9f)
    /// </summary>
    /// <param name="basePoints">Base points before multiplier</param>
    /// <param name="accuracy">Landing accuracy (0-1)</param>
    /// <returns>Actual points awarded after multiplier</returns>
    public int AddScoreWithCombo(int basePoints, float accuracy)
    {
        if (!isGameActive || isGameOver) return 0;

        // Update combo based on accuracy
        UpdateCombo(accuracy);

        // Calculate points - multiplier only applies for perfect hits
        bool isPerfectHit = accuracy >= 0.9f;
        float multiplier = isPerfectHit ? GetComboMultiplier() : 1f;
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
    /// Combo requires CONSECUTIVE Perfect landings only (accuracy >= 0.9f)
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

        // Track consecutive perfect hits for stack straightening
        int previousPerfectHits = consecutivePerfectHits;
        if (currentAccuracyLevel == AccuracyLevel.Perfect)
        {
            consecutivePerfectHits++;

            // Notify listeners of perfect hits change
            if (consecutivePerfectHits != previousPerfectHits)
            {
                OnConsecutivePerfectHitsChanged?.Invoke(consecutivePerfectHits);
            }

            // Check if we've reached the required number of perfect hits
            if (consecutivePerfectHits >= perfectHitsRequired)
            {
                Debug.Log($"Perfect hit streak achieved! {consecutivePerfectHits} consecutive perfect hits - straightening stack!");
                OnPerfectHitStreak?.Invoke();
                consecutivePerfectHits = 0; // Reset after triggering
                OnConsecutivePerfectHitsChanged?.Invoke(consecutivePerfectHits); // Notify of reset
            }
        }
        else
        {
            // Non-perfect hit breaks the perfect hit streak
            if (consecutivePerfectHits > 0)
            {
                consecutivePerfectHits = 0;
                OnConsecutivePerfectHitsChanged?.Invoke(consecutivePerfectHits); // Notify of reset
            }
        }

        // Check if we should maintain or break the combo
        // Combo only counts for Perfect hits (accuracy >= 0.9f)
        bool shouldMaintainCombo = false;

        if (currentAccuracyLevel == AccuracyLevel.Perfect)
        {
            // Perfect landing - always maintain or start combo
            shouldMaintainCombo = true;
        }
        else
        {
            // Good or Poor landing - always break combo
            shouldMaintainCombo = false;
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

        // Stop combo decay to prevent sound effects after game over
        comboDecayActive = false;

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
        consecutivePerfectHits = 0; // Reset perfect hit streak
        OnConsecutivePerfectHitsChanged?.Invoke(consecutivePerfectHits); // Notify of reset
        OnGameRestart?.Invoke();
        OnScoreChanged?.Invoke(currentScore);
        OnComboChanged?.Invoke(currentCombo, GetComboMultiplier());
        OnHighScoreChanged?.Invoke(highScore); // Refresh high score UI with current value

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
    /// Get the PlayerPrefs key for the current game mode's high score
    /// Centralized key generation to prevent duplication and ensure consistency
    /// </summary>
    private string GetHighScorePlayerPrefsKey()
    {
        if (currentGameMode == GameMode.InfiniteStacker)
        {
            return "HighScore_InfiniteStacker";
        }
        else if (currentLevelNumber > 0)
        {
            return $"Level_{currentLevelNumber}_HighScore";
        }
        else
        {
            return "HighScore_Levels";
        }
    }

    /// <summary>
    /// Fallback method to load high score from PlayerPrefs
    /// </summary>
    private void LoadHighScoreFromPlayerPrefs()
    {
        string key = GetHighScorePlayerPrefsKey();
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

        // Save to PlayerPrefs so it's available when offline
        SaveHighScoreValueToPlayerPrefs(loadedHighScore);
    }

    /// <summary>
    /// Save high score to PlayFab (only if it's a new high score and hasn't been saved yet)
    /// If offline, queues the score for later sync
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

        // Always save to PlayerPrefs as backup (works offline)
        SaveHighScoreToPlayerPrefs();

        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null && playFabManager.IsLoggedIn)
        {
            string leaderboardName = null;
            if (currentGameMode == GameMode.InfiniteStacker)
            {
                leaderboardName = "InfiniteStackerHighScores";
            }
            else if (currentGameMode == GameMode.StackerLevels && currentLevelNumber > 0)
            {
                leaderboardName = $"StackerLevel_{currentLevelNumber}";
            }

            if (!string.IsNullOrEmpty(leaderboardName))
            {
                // SubmitScore will handle offline queueing automatically
                playFabManager.SubmitScore(leaderboardName, currentScore);
                Debug.Log($"Saving {leaderboardName} high score: {currentScore}");
            }

            highScoreSaved = true;
        }
        else
        {
            // PlayFab not ready - queue score for later sync if we have network
            // If offline, the score is already saved to PlayerPrefs and will be synced when online
            if (currentGameMode == GameMode.InfiniteStacker)
            {
                OfflineScoreQueue.QueueScore("InfiniteStackerHighScores", currentScore);
                Debug.LogWarning("PlayFab not ready, queued InfiniteStacker score for later sync");
            }
            else if (currentGameMode == GameMode.StackerLevels && currentLevelNumber > 0)
            {
                string leaderboardName = $"StackerLevel_{currentLevelNumber}";
                OfflineScoreQueue.QueueScore(leaderboardName, currentScore);
                Debug.LogWarning($"PlayFab not ready, queued StackerLevel {currentLevelNumber} score for later sync");
            }
            else
            {
                Debug.LogWarning("PlayFab not ready, saving to PlayerPrefs only");
            }

            highScoreSaved = true;
        }
    }

    /// <summary>
    /// Save high score to PlayerPrefs as backup
    /// Used when player achieves a new high score during gameplay
    /// </summary>
    private void SaveHighScoreToPlayerPrefs()
    {
        string key = GetHighScorePlayerPrefsKey();
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
    /// Save a high score value to PlayerPrefs for offline access
    /// Used when loading/syncing scores from PlayFab to ensure they're cached locally
    /// Only saves if the score is higher than or equal to what's currently saved (prevents overwriting with lower values)
    /// </summary>
    /// <param name="score">The score value to save</param>
    /// <param name="forceSave">If true, saves even if score is 0 or lower than current (used for syncing)</param>
    private void SaveHighScoreValueToPlayerPrefs(int score, bool forceSave = false)
    {
        if (score <= 0 && !forceSave) return; // Don't save zero scores unless forced

        string key = GetHighScorePlayerPrefsKey();
        int currentSaved = PlayerPrefs.GetInt(key, 0);

        // Only update if the score is higher than or equal to what's currently saved
        // This ensures synced scores from PlayFab are available when offline
        // while preventing overwriting with lower values
        if (forceSave || score >= currentSaved)
        {
            PlayerPrefs.SetInt(key, score);
            PlayerPrefs.Save();
            Debug.Log($"Saved high score to PlayerPrefs: {score} (Key: {key}, previous: {currentSaved})");
        }
    }

    /// <summary>
    /// Called when progress is synced from cloud (after login)
    /// Updates local high score if cloud has a higher value (server authoritative)
    /// Always saves synced score to PlayerPrefs for offline access
    /// </summary>
    private void OnProgressSyncedFromCloud(PlayerProgressData data)
    {
        if (data == null) return;

        // Always save Infinite Stacker high score to PlayerPrefs (regardless of current game mode)
        // This ensures synced scores are available offline even if player is in a different mode
        if (data.infiniteStackerHighScore > 0)
        {
            // Save directly to PlayerPrefs using the correct key
            PlayerPrefs.SetInt("HighScore_InfiniteStacker", data.infiniteStackerHighScore);
            PlayerPrefs.Save();
            Debug.Log($"Saved synced Infinite Stacker high score to PlayerPrefs: {data.infiniteStackerHighScore}");
        }

        // Update in-memory high score if we're in Infinite Stacker mode and cloud has a higher value
        if (currentGameMode == GameMode.InfiniteStacker && data.infiniteStackerHighScore > highScore)
        {
            Debug.Log($"Updating Infinite Stacker high score from cloud: {data.infiniteStackerHighScore}");
            highScore = data.infiniteStackerHighScore;

            // Notify UI
            OnHighScoreChanged?.Invoke(highScore);
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

