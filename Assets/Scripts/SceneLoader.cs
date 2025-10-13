using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Static utility class for loading scenes and passing game configuration between scenes
/// Handles scene transitions and ensures GameManager is properly configured
/// </summary>
public static class SceneLoader
{
    // Scene names - configure these in your build settings
    private const string MAIN_MENU_SCENE = "MainMenu";
    private const string GAME_SCENE = "GameScene";

    // Temporary storage for scene transition data
    private static GameMode? pendingGameMode = null;
    private static int? pendingLevelIndex = null;

    /// <summary>
    /// Load the main menu scene
    /// </summary>
    public static void LoadMainMenu()
    {
        Debug.Log("Loading Main Menu...");
        SceneManager.LoadScene(MAIN_MENU_SCENE);
    }

    /// <summary>
    /// Load the game scene with specified game mode
    /// </summary>
    /// <param name="gameMode">The game mode to start</param>
    public static void LoadGameScene(GameMode gameMode)
    {
        LoadGameScene(GAME_SCENE, gameMode, null);
    }

    /// <summary>
    /// Load the game scene with specified game mode and level
    /// </summary>
    /// <param name="sceneName">Name of the game scene to load</param>
    /// <param name="gameMode">The game mode to start</param>
    /// <param name="levelIndex">Optional level index for StackerLevels mode</param>
    public static void LoadGameScene(string sceneName, GameMode gameMode, int? levelIndex = null)
    {
        Debug.Log($"Loading Game Scene: {sceneName}, Mode: {gameMode}, Level: {levelIndex?.ToString() ?? "N/A"}");

        // Store the pending configuration
        pendingGameMode = gameMode;
        pendingLevelIndex = levelIndex;

        // Subscribe to scene loaded event to configure the game
        SceneManager.sceneLoaded += OnGameSceneLoaded;

        // Load the scene
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Called when a scene is loaded - configures GameManager if it's the game scene
    /// </summary>
    private static void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Unsubscribe to prevent multiple calls
        SceneManager.sceneLoaded -= OnGameSceneLoaded;

        // Only configure if we have pending game mode
        if (!pendingGameMode.HasValue)
        {
            Debug.LogWarning("Game scene loaded but no game mode was set!");
            return;
        }

        // Find GameManager via DependencyRegistry
        GameManager gameManager = DependencyRegistry.Find<GameManager>();

        if (gameManager != null)
        {
            ConfigureGameManager(gameManager);
        }
        else
        {
            Debug.LogError("GameManager not found after loading game scene! Make sure GameManager registers itself in Awake.");
        }
    }

    /// <summary>
    /// Configure the GameManager with pending settings
    /// </summary>
    private static void ConfigureGameManager(GameManager gameManager)
    {
        if (!pendingGameMode.HasValue) return;

        Debug.Log($"Configuring GameManager: Mode={pendingGameMode.Value}, Level={pendingLevelIndex?.ToString() ?? "N/A"}");

        // Set the game mode (this will start the game if autoStartAfterModeSet is true)
        gameManager.SetGameMode(pendingGameMode.Value);

        // If a specific level was requested, load it
        if (pendingLevelIndex.HasValue && pendingGameMode.Value == GameMode.StackerLevels)
        {
            LevelManager levelManager = DependencyRegistry.Find<LevelManager>();
            if (levelManager != null)
            {
                levelManager.LoadLevel(pendingLevelIndex.Value);
                Debug.Log($"Loaded level {pendingLevelIndex.Value + 1}");
            }
            else
            {
                Debug.LogWarning("LevelManager not found, couldn't load specific level!");
            }
        }

        // Clear pending data
        pendingGameMode = null;
        pendingLevelIndex = null;
    }

    /// <summary>
    /// Reload the current scene
    /// </summary>
    public static void ReloadCurrentScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        Debug.Log($"Reloading scene: {currentScene.name}");
        SceneManager.LoadScene(currentScene.name);
    }

    /// <summary>
    /// Get the current scene name
    /// </summary>
    public static string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }

    /// <summary>
    /// Check if we're currently in the main menu
    /// </summary>
    public static bool IsInMainMenu()
    {
        return GetCurrentSceneName() == MAIN_MENU_SCENE;
    }

    /// <summary>
    /// Check if we're currently in the game scene
    /// </summary>
    public static bool IsInGameScene()
    {
        return GetCurrentSceneName() == GAME_SCENE;
    }
}

