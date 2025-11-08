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

    // Internal state
    private bool gameModeInitialized = false;
    private int currentLevelNumber = -1; // For StackerLevels mode
    private bool highScoreSaved = false; // Track if high score has been saved this session

    [Header("Game Events")]
    public System.Action<int> OnScoreChanged;
    public System.Action<int> OnHighScoreChanged;
    public System.Action OnGameStart;
    public System.Action OnGameOver;
    public System.Action OnGameRestart;
    public System.Action<GameMode> OnGameModeChanged;

    // Properties
    public GameMode CurrentGameMode => currentGameMode;
    public int CurrentScore => currentScore;
    public int HighScore => highScore;
    public bool IsGameActive => isGameActive;
    public bool IsGameOver => isGameOver;
    public bool IsGameModeInitialized => gameModeInitialized;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<GameManager>(this);

        // Don't load high score here - wait for game mode to be set
    }

    private void Start()
    {
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

    public void StartGame()
    {
        isGameActive = true;
        isGameOver = false;
        currentScore = 0;
        highScoreSaved = false; // Reset save flag for new game session
        OnGameStart?.Invoke();
        OnScoreChanged?.Invoke(currentScore);
    }

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
        OnGameRestart?.Invoke();
        OnScoreChanged?.Invoke(currentScore);

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
    }

    private void OnDestroy()
    {
        DependencyRegistry.Unregister<GameManager>(this);
    }
}

