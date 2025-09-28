using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int highScore = 0;
    [SerializeField] private bool isGameActive = false;
    [SerializeField] private bool isGameOver = false;

    [Header("Game Events")]
    public System.Action<int> OnScoreChanged;
    public System.Action<int> OnHighScoreChanged;
    public System.Action OnGameStart;
    public System.Action OnGameOver;
    public System.Action OnGameRestart;

    // Properties
    public int CurrentScore => currentScore;
    public int HighScore => highScore;
    public bool IsGameActive => isGameActive;
    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<GameManager>(this);

        // Ensure this GameManager persists across scenes
        DontDestroyOnLoad(gameObject);
        LoadHighScore();
    }

    private void Start()
    {
        // Start the game
        StartGame();
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

    private void LoadHighScore()
    {
        highScore = PlayerPrefs.GetInt("HighScore", 0);
        OnHighScoreChanged?.Invoke(highScore);
    }

    private void SaveHighScore()
    {
        PlayerPrefs.SetInt("HighScore", highScore);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        DependencyRegistry.Unregister<GameManager>(this);
    }
}
