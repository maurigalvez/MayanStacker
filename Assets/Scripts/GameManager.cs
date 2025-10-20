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

        LoadHighScore();
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
        OnGameStart?.Invoke();
        OnScoreChanged?.Invoke(currentScore);
    }

    public void AddScore(int points)
    {
        if (!isGameActive || isGameOver) return;

        currentScore += points;
        OnScoreChanged?.Invoke(currentScore);

        // Check for new high score
        if (currentScore > highScore)
        {
            highScore = currentScore;
            SaveHighScore();
            OnHighScoreChanged?.Invoke(highScore);
        }
    }

    public void GameOver()
    {
        if (isGameOver) return;

        isGameActive = false;
        isGameOver = true;
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

    public void SetGameMode(GameMode newMode, bool startGameNow = true)
    {
        bool modeChanged = currentGameMode != newMode;

        currentGameMode = newMode;
        gameModeInitialized = true;

        // Reload high score for the new mode
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
    public void InitializeGameMode(GameMode mode)
    {
        SetGameMode(mode, startGameNow: false);
    }

    private void LoadHighScore()
    {
        // Load high score based on game mode
        string key = currentGameMode == GameMode.InfiniteStacker ? "HighScore" : "HighScore_Levels";
        highScore = PlayerPrefs.GetInt(key, 0);
        OnHighScoreChanged?.Invoke(highScore);
    }

    private void SaveHighScore()
    {
        // Save high score based on game mode
        string key = currentGameMode == GameMode.InfiniteStacker ? "HighScore" : "HighScore_Levels";
        PlayerPrefs.SetInt(key, highScore);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        DependencyRegistry.Unregister<GameManager>(this);
    }
}

