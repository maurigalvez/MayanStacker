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
    /// Load the game scene for the Daily Challenge mode.
    /// Convenience wrapper around LoadGameScene(GameMode.DailyChallenge).
    /// </summary>
    public static void LoadDailyChallenge()
    {
        LoadGameScene(GameMode.DailyChallenge);
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

        // Daily Challenge: fetch today's modifier from PlayFab Title Data, apply it, then start.
        if (pendingGameMode.Value == GameMode.DailyChallenge)
        {
            var dailyMgr = DependencyRegistry.Find<DailyChallengeManager>();
            if (dailyMgr != null)
            {
                // Initialize mode now so any UI subscribed to OnGameModeChanged sees Daily before StartGame fires.
                gameManager.InitializeGameMode(GameMode.DailyChallenge);

                dailyMgr.FetchTodaysConfig(cfg =>
                {
                    // Begin the run: apply the modifier (starts the SpeedRun timer) then start play.
                    System.Action beginRun = () =>
                    {
                        dailyMgr.ApplyModifier(cfg);
                        gameManager.StartGame();
                    };

                    // Gate the run behind the briefing screen so the player reads today's modifier.
                    // If no UIManager/briefing is available, fail open and start immediately.
                    var uiManager = DependencyRegistry.Find<UIManager>();
                    if (uiManager != null)
                    {
                        uiManager.ShowDailyBriefing(cfg, beginRun);
                    }
                    else
                    {
                        beginRun();
                    }
                });
            }
            else
            {
                Debug.LogWarning("DailyChallengeManager not found in scene — starting Daily Challenge without a modifier.");
                gameManager.InitializeGameMode(GameMode.DailyChallenge);
                gameManager.StartGame();
            }

            pendingGameMode = null;
            pendingLevelIndex = null;
            return;
        }

        // If a specific level was requested, load it first (this will also set the level number in GameManager)
        if (pendingLevelIndex.HasValue && pendingGameMode.Value == GameMode.StackerLevels)
        {
            LevelManager levelManager = DependencyRegistry.Find<LevelManager>();
            if (levelManager != null)
            {
                // Set game mode without starting (we'll start after level is loaded)
                gameManager.InitializeGameMode(pendingGameMode.Value);

                // Load the specific level (this calls gameManager.SetCurrentLevel internally)
                levelManager.LoadLevel(pendingLevelIndex.Value);
                Debug.Log($"Loaded level {pendingLevelIndex.Value + 1}");

                // Now start the game (SceneLoader is the authoritative source for starting after scene load)
                // This prevents duplicate StartGame() calls that can cause timing issues on Android
                gameManager.StartGame();
            }
            else
            {
                Debug.LogWarning("LevelManager not found, couldn't load specific level!");
                // Fallback: initialize mode and start explicitly
                gameManager.InitializeGameMode(pendingGameMode.Value);
                gameManager.StartGame();
            }
        }
        else
        {
            // For InfiniteStacker or when no specific level is requested
            // Initialize mode without auto-starting, then start explicitly
            // This ensures SceneLoader is the single source of StartGame() after scene load
            gameManager.InitializeGameMode(pendingGameMode.Value);
            gameManager.StartGame();
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

