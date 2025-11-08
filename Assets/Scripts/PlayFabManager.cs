using PlayFab;
using PlayFab.ClientModels;
using PlayFab.ProgressionModels;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages PlayFab authentication and leaderboard submissions for TamalStacker
/// Handles automatic login and score submission for both game modes
/// 
/// STATISTICS V2 API:
/// This manager uses the NEW PlayFab Statistics V2 API (Progression namespace) instead of legacy statistics.
/// - Uses entity-based authentication (automatically available after login)
/// - Scores are stored as strings internally (converted to/from int)
/// - Leaderboard names are used directly as statistic names
/// - Requires statistics to be created in PlayFab Game Manager first
/// 
/// GOOGLE PLAY GAMES SETUP (Android):
/// To enable Google Play Games authentication:
/// 1. Install Google Play Games Plugin: https://github.com/playgameservices/play-games-plugin-for-unity
/// 2. Configure OAuth 2.0 credentials in Google Play Console
/// 3. In PlayFab Game Manager, link Google Play Games under Add-ons
/// 4. Uncomment the Google Play Games code in LoginWithGooglePlayGames() method
/// 5. Add the required using statements at the top of this file
/// 
/// Until configured, the system will fallback to Android Device ID authentication.
/// </summary>
public class PlayFabManager : MonoBehaviour
{
    [Header("PlayFab Settings")]
    [SerializeField] private string titleId = ""; // Set this in Unity Inspector or leave empty to use settings

    [Header("Leaderboard Names")]
    [SerializeField] private string infiniteStackerLeaderboard = "AllTime_InfiniteStackerHighScores";
    [SerializeField] private string stackerLevelsLeaderboardPrefix = "AllTime_StackerLevel_"; // Will be suffixed with level number

    [Header("Login Settings")]
    [SerializeField] private bool autoLoginOnStart = true;
    [SerializeField] private bool createAccountIfNotExists = true;

    [Header("Android Authentication")]
    [SerializeField] private bool useGooglePlayGames = true; // If false, uses Android Device ID
    [Tooltip("If Google Play Games login fails, fallback to Android Device ID")]
    [SerializeField] private bool fallbackToDeviceID = true;

    // References (found via DependencyRegistry)
    private GameManager gameManager;
    private LevelManager levelManager;

    // State
    private bool isLoggedIn = false;
    private string playFabId = "";

    // Events
    public System.Action<string> OnLoginSuccess;
    public System.Action<string> OnLoginFailure;
    public System.Action<int> OnScoreSubmitted;
    public System.Action<string> OnScoreSubmissionFailed;

    // Properties
    public bool IsLoggedIn => isLoggedIn;
    public string PlayFabId => playFabId;

    private void Awake()
    {
        // 1. Check if instance already exists
        var existingInstance = DependencyRegistry.Find<PlayFabManager>();
        if (existingInstance != null && existingInstance != this)
        {
            Debug.LogWarning("PlayFabManager instance already exists. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        // 2. Register with DependencyRegistry
        DependencyRegistry.Register<PlayFabManager>(this);

        // 3. Set PlayFab title ID if provided
        if (!string.IsNullOrEmpty(titleId))
        {
            PlayFabSettings.staticSettings.TitleId = titleId;
        }

        // 4. Persist across scenes
        DontDestroyOnLoad(gameObject);

        // 5. Subscribe to scene loading events to refresh dependencies
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // 1. Find initial dependencies and subscribe
        RefreshDependencies();

        // 2. Auto-login if enabled
        if (autoLoginOnStart)
        {
            LoginWithDeviceID();
        }
    }

    /// <summary>
    /// Called when a new scene is loaded
    /// Refreshes dependencies since managers may have been recreated
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"PlayFabManager: Scene '{scene.name}' loaded, refreshing dependencies");
        RefreshDependencies();
    }

    /// <summary>
    /// Refreshes dependencies and re-subscribes to events
    /// Call this when switching scenes or if managers are recreated
    /// </summary>
    public void RefreshDependencies()
    {
        // Unsubscribe from old references if they exist
        UnsubscribeFromEvents();

        // Find new references
        gameManager = DependencyRegistry.Find<GameManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();

        // Subscribe to new references
        SubscribeToEvents();
    }

    /// <summary>
    /// Subscribe to manager events
    /// </summary>
    private void SubscribeToEvents()
    {
        if (gameManager != null)
        {
            gameManager.OnGameOver += OnGameOver;
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted += OnLevelCompleted;
        }
    }

    /// <summary>
    /// Unsubscribe from manager events
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (gameManager != null)
        {
            gameManager.OnGameOver -= OnGameOver;
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= OnLevelCompleted;
        }
    }

    #region Authentication

    /// <summary>
    /// Logs in the player using platform-specific device authentication
    /// This creates an anonymous account tied to the device
    /// </summary>
    public void LoginWithDeviceID()
    {
        Debug.Log("Logging into PlayFab...");

#if UNITY_ANDROID
        LoginWithAndroid();
#elif UNITY_IOS
        LoginWithIOS();
#else
        // Fallback to CustomID for Editor/other platforms
        LoginWithCustomIDFallback();
#endif
    }

    /// <summary>
    /// Login with Android - uses Google Play Games or Device ID based on settings
    /// </summary>
    private void LoginWithAndroid()
    {
        if (useGooglePlayGames)
        {
            LoginWithGooglePlayGames();
        }
        else
        {
            LoginWithAndroidDeviceID();
        }
    }

    /// <summary>
    /// Login with Google Play Games (recommended for Android)
    /// Requires Google Play Games Plugin for Unity to be installed and configured
    /// Install from: https://github.com/playgameservices/play-games-plugin-for-unity
    /// </summary>
    private void LoginWithGooglePlayGames()
    {
#if UNITY_ANDROID
        // NOTE: To use Google Play Games, you need to:
        // 1. Install the Google Play Games Plugin for Unity
        // 2. Configure it with your Google Play Console credentials
        // 3. Uncomment the code below and import the namespace at the top of the file

        /* 
        // Example implementation with Google Play Games Plugin:
        // Add this to the top of the file: using GooglePlayGames;
        // Add this to the top of the file: using GooglePlayGames.BasicApi;
        
        PlayGamesPlatform.Activate();
        
        PlayGamesPlatform.Instance.Authenticate((success) =>
        {
            if (success == SignInStatus.Success)
            {
                Debug.Log("Google Play Games authentication successful!");
                
                // Get the server auth code
                PlayGamesPlatform.Instance.RequestServerSideAccess(true, (serverAuthCode) =>
                {
                    if (!string.IsNullOrEmpty(serverAuthCode))
                    {
                        SubmitGooglePlayGamesLogin(serverAuthCode);
                    }
                    else
                    {
                        Debug.LogWarning("Failed to get server auth code from Google Play Games");
                        if (fallbackToDeviceID)
                        {
                            Debug.Log("Falling back to Android Device ID authentication...");
                            LoginWithAndroidDeviceID();
                        }
                    }
                });
            }
            else
            {
                Debug.LogWarning($"Google Play Games authentication failed: {success}");
                if (fallbackToDeviceID)
                {
                    Debug.Log("Falling back to Android Device ID authentication...");
                    LoginWithAndroidDeviceID();
                }
            }
        });
        */

        // TEMPORARY: Until Google Play Games Plugin is configured, use Device ID
        Debug.LogWarning("Google Play Games Plugin not configured. Install from: https://github.com/playgameservices/play-games-plugin-for-unity");
        Debug.Log("Using Android Device ID authentication as fallback...");
        LoginWithAndroidDeviceID();
#else
        Debug.LogError("Google Play Games is only available on Android!");
        LoginWithAndroidDeviceID();
#endif
    }

    /// <summary>
    /// Submit Google Play Games authentication to PlayFab
    /// </summary>
    /// <param name="serverAuthCode">Server auth code from Google Play Games</param>
    private void SubmitGooglePlayGamesLogin(string serverAuthCode)
    {
        var request = new LoginWithGoogleAccountRequest
        {
            CreateAccount = createAccountIfNotExists,
            TitleId = PlayFabSettings.staticSettings.TitleId,
            ServerAuthCode = serverAuthCode
        };

        Debug.Log("Logging into PlayFab with Google Play Games...");
        PlayFabClientAPI.LoginWithGoogleAccount(request, OnLoginSuccessCallback, (error) =>
        {
            Debug.LogWarning($"PlayFab login with Google Play Games failed: {error.GenerateErrorReport()}");

            // Fallback to Device ID if enabled
            if (fallbackToDeviceID)
            {
                Debug.Log("Falling back to Android Device ID authentication...");
                LoginWithAndroidDeviceID();
            }
            else
            {
                OnLoginFailureCallback(error);
            }
        });
    }

    /// <summary>
    /// Login with Android Device ID (alternative to Google Play Games)
    /// </summary>
    private void LoginWithAndroidDeviceID()
    {
        var request = new LoginWithAndroidDeviceIDRequest
        {
            AndroidDeviceId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = createAccountIfNotExists,
            TitleId = PlayFabSettings.staticSettings.TitleId
        };

        Debug.Log("Logging into PlayFab with Android Device ID...");
        PlayFabClientAPI.LoginWithAndroidDeviceID(request, OnLoginSuccessCallback, OnLoginFailureCallback);
    }

    /// <summary>
    /// Login with iOS Device ID (recommended by PlayFab for iOS)
    /// </summary>
    private void LoginWithIOS()
    {
        var request = new LoginWithIOSDeviceIDRequest
        {
            DeviceId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = createAccountIfNotExists,
            TitleId = PlayFabSettings.staticSettings.TitleId
        };

        Debug.Log("Logging into PlayFab with iOS Device ID...");
        PlayFabClientAPI.LoginWithIOSDeviceID(request, OnLoginSuccessCallback, OnLoginFailureCallback);
    }

    /// <summary>
    /// Fallback login method for Editor and unsupported platforms
    /// Uses CustomID as a fallback (not recommended for production)
    /// </summary>
    private void LoginWithCustomIDFallback()
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = createAccountIfNotExists,
            TitleId = PlayFabSettings.staticSettings.TitleId
        };

        Debug.LogWarning("Using CustomID fallback for PlayFab login (Editor/Unsupported platform)");
        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccessCallback, OnLoginFailureCallback);
    }

    /// <summary>
    /// Called when login is successful
    /// </summary>
    private void OnLoginSuccessCallback(LoginResult result)
    {
        isLoggedIn = true;
        playFabId = result.PlayFabId;

        Debug.Log($"PlayFab login successful! PlayFabId: {playFabId}");

        // Check if this is a new account
        if (result.NewlyCreated)
        {
            Debug.Log("New PlayFab account created for this device");
        }

        OnLoginSuccess?.Invoke(playFabId);
    }

    /// <summary>
    /// Called when login fails
    /// </summary>
    private void OnLoginFailureCallback(PlayFabError error)
    {
        isLoggedIn = false;

        string errorMessage = $"PlayFab login failed: {error.GenerateErrorReport()}";
        Debug.LogError(errorMessage);

        OnLoginFailure?.Invoke(errorMessage);
    }

    #endregion

    #region Score Submission

    /// <summary>
    /// Called when game over occurs in Infinite Stacker mode
    /// High score is saved by GameManager, this is just for notification
    /// </summary>
    private void OnGameOver()
    {
        // GameManager handles saving for InfiniteStacker mode
        // Nothing to do here - high score is saved when game over screen is shown
    }

    /// <summary>
    /// Called when a level is completed in Stacker Levels mode
    /// Triggers high score saving in GameManager
    /// </summary>
    private void OnLevelCompleted(int stars, int score)
    {
        if (gameManager == null) return;

        // Tell GameManager to save the high score for the completed level
        gameManager.SaveHighScoreIfNeeded();
        Debug.Log($"Level completed! Saving high score: {score} (Stars: {stars})");
    }

    /// <summary>
    /// Submit a score to a specific leaderboard using the new Statistics V2 API
    /// </summary>
    public void SubmitScore(string leaderboardName, int score)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot submit score - not logged into PlayFab");
            return;
        }

        var request = new PlayFab.ProgressionModels.UpdateStatisticsRequest
        {
            Statistics = new System.Collections.Generic.List<PlayFab.ProgressionModels.StatisticUpdate>
            {
                new PlayFab.ProgressionModels.StatisticUpdate
                {
                    Name = leaderboardName,
                    Scores = new System.Collections.Generic.List<string> { score.ToString() }
                }
            }
        };

        PlayFabProgressionAPI.UpdateStatistics(request, OnScoreSubmitSuccess, OnScoreSubmitFailure);
    }

    /// <summary>
    /// Called when score submission is successful
    /// </summary>
    private void OnScoreSubmitSuccess(PlayFab.ProgressionModels.UpdateStatisticsResponse result)
    {
        Debug.Log("Score submitted to PlayFab successfully!");
        OnScoreSubmitted?.Invoke(0); // Could pass the score if needed
    }

    /// <summary>
    /// Called when score submission fails
    /// </summary>
    private void OnScoreSubmitFailure(PlayFabError error)
    {
        string errorMessage = $"Score submission failed: {error.GenerateErrorReport()}";
        Debug.LogWarning(errorMessage);
        OnScoreSubmissionFailed?.Invoke(errorMessage);
    }

    #endregion

    #region Leaderboard Retrieval

    /// <summary>
    /// Load the player's high score for a specific leaderboard using the new Statistics V2 API
    /// </summary>
    /// <param name="leaderboardName">Name of the leaderboard/statistic</param>
    /// <param name="onScoreLoaded">Callback with the loaded score (0 if no score found)</param>
    public void LoadHighScore(string leaderboardName, System.Action<int> onScoreLoaded)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot load high score - not logged into PlayFab");
            onScoreLoaded?.Invoke(0);
            return;
        }

        var request = new PlayFab.ProgressionModels.GetStatisticsRequest
        {
            StatisticNames = new System.Collections.Generic.List<string> { leaderboardName }
        };

        PlayFabProgressionAPI.GetStatistics(request,
            result => OnLoadHighScoreSuccess(result, leaderboardName, onScoreLoaded),
            error => OnLoadHighScoreFailure(error, onScoreLoaded));
    }

    /// <summary>
    /// Called when high score load is successful
    /// </summary>
    private void OnLoadHighScoreSuccess(PlayFab.ProgressionModels.GetStatisticsResponse result, string leaderboardName, System.Action<int> onScoreLoaded)
    {
        int highScore = 0;

        if (result.Statistics != null && result.Statistics.ContainsKey(leaderboardName))
        {
            var statistic = result.Statistics[leaderboardName];
            if (statistic.Scores != null && statistic.Scores.Count > 0)
            {
                if (int.TryParse(statistic.Scores[0], out int score))
                {
                    highScore = score;
                    Debug.Log($"Loaded high score from PlayFab: {highScore} for {leaderboardName}");
                }
            }
        }
        else
        {
            Debug.Log($"No high score found on PlayFab for leaderboard: {leaderboardName}");
        }

        onScoreLoaded?.Invoke(highScore);
    }

    /// <summary>
    /// Called when high score load fails
    /// </summary>
    private void OnLoadHighScoreFailure(PlayFabError error, System.Action<int> onScoreLoaded)
    {
        Debug.LogWarning($"Failed to load high score from PlayFab: {error.GenerateErrorReport()}");
        onScoreLoaded?.Invoke(0);
    }

    /// <summary>
    /// Get the top entries for a specific leaderboard using the new Statistics V2 API
    /// </summary>
    /// <param name="leaderboardName">Name of the leaderboard statistic</param>
    /// <param name="maxResults">Maximum number of results to retrieve (1-100)</param>
    /// <param name="onSuccess">Callback with list of leaderboard entries</param>
    /// <param name="onFailure">Callback with error message</param>
    public void GetLeaderboard(string leaderboardName, int maxResults, System.Action<System.Collections.Generic.List<LeaderboardEntry>> onSuccess, System.Action<string> onFailure)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot get leaderboard - not logged into PlayFab");
            onFailure?.Invoke("Not logged into PlayFab");
            return;
        }

        // Clamp maxResults to valid range (1-100)
        maxResults = System.Math.Max(1, System.Math.Min(100, maxResults));

        var request = new PlayFab.ProgressionModels.GetEntityLeaderboardRequest
        {
            LeaderboardName = leaderboardName,
            StartingPosition = 1, // 1-based in new API
            PageSize = (uint)maxResults
        };

        PlayFabProgressionAPI.GetLeaderboard(request,
            result => OnGetLeaderboardSuccess(result, onSuccess),
            error => OnGetLeaderboardFailure(error, onFailure));
    }

    /// <summary>
    /// Called when leaderboard retrieval is successful
    /// </summary>
    private void OnGetLeaderboardSuccess(PlayFab.ProgressionModels.GetEntityLeaderboardResponse result, System.Action<System.Collections.Generic.List<LeaderboardEntry>> onSuccess)
    {
        Debug.Log($"Leaderboard retrieved! {result.Rankings.Count} entries");

        var entries = new System.Collections.Generic.List<LeaderboardEntry>();

        foreach (var entry in result.Rankings)
        {
            // Check if this is the current player's entity
            bool isCurrentPlayer = entry.Entity != null && entry.Entity.Id == playFabId;

            // Parse the score from string to int
            int score = 0;
            if (entry.Scores != null && entry.Scores.Count > 0)
            {
                int.TryParse(entry.Scores[0], out score);
            }

            entries.Add(new LeaderboardEntry(
                entry.Rank, // Already 1-based in new API
                entry.DisplayName ?? (entry.Entity?.Id ?? "Unknown"),
                score,
                isCurrentPlayer
            ));
        }

        onSuccess?.Invoke(entries);
    }

    /// <summary>
    /// Called when leaderboard retrieval fails
    /// </summary>
    private void OnGetLeaderboardFailure(PlayFabError error, System.Action<string> onFailure)
    {
        string errorMessage = $"Failed to get leaderboard: {error.GenerateErrorReport()}";
        Debug.LogWarning(errorMessage);
        onFailure?.Invoke(errorMessage);
    }

    /// <summary>
    /// Get the player's position on a specific leaderboard with surrounding entries using the new Statistics V2 API
    /// </summary>
    /// <param name="leaderboardName">Name of the leaderboard statistic</param>
    /// <param name="maxResults">Number of entries around player to retrieve</param>
    /// <param name="onSuccess">Callback with list of leaderboard entries centered on player</param>
    /// <param name="onFailure">Callback with error message</param>
    public void GetPlayerLeaderboardPosition(string leaderboardName, int maxResults, System.Action<System.Collections.Generic.List<LeaderboardEntry>> onSuccess, System.Action<string> onFailure)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot get leaderboard position - not logged into PlayFab");
            onFailure?.Invoke("Not logged into PlayFab");
            return;
        }

        var request = new PlayFab.ProgressionModels.GetLeaderboardAroundEntityRequest
        {
            LeaderboardName = leaderboardName,
            MaxSurroundingEntries = (uint)maxResults
        };

        PlayFabProgressionAPI.GetLeaderboardAroundEntity(request,
            result => OnGetPlayerPositionSuccess(result, onSuccess),
            error => OnGetPlayerPositionFailure(error, onFailure));
    }

    /// <summary>
    /// Called when player position retrieval is successful
    /// </summary>
    private void OnGetPlayerPositionSuccess(PlayFab.ProgressionModels.GetEntityLeaderboardResponse result, System.Action<System.Collections.Generic.List<LeaderboardEntry>> onSuccess)
    {
        if (result.Rankings != null && result.Rankings.Count > 0)
        {
            var entries = new System.Collections.Generic.List<LeaderboardEntry>();

            foreach (var entry in result.Rankings)
            {
                // Check if this is the current player's entity
                bool isCurrentPlayer = entry.Entity != null && entry.Entity.Id == playFabId;

                // Parse the score from string to int
                int score = 0;
                if (entry.Scores != null && entry.Scores.Count > 0)
                {
                    int.TryParse(entry.Scores[0], out score);
                }

                entries.Add(new LeaderboardEntry(
                    entry.Rank, // Already 1-based in new API
                    entry.DisplayName ?? (entry.Entity?.Id ?? "Unknown"),
                    score,
                    isCurrentPlayer
                ));
            }

            // Find and log the player's entry
            var playerEntry = result.Rankings.Find(e => e.Entity != null && e.Entity.Id == playFabId);
            if (playerEntry != null)
            {
                int playerScore = 0;
                if (playerEntry.Scores != null && playerEntry.Scores.Count > 0)
                {
                    int.TryParse(playerEntry.Scores[0], out playerScore);
                }
                Debug.Log($"Player position: {playerEntry.Rank}, Score: {playerScore}");
            }

            onSuccess?.Invoke(entries);
        }
        else
        {
            // Player has no score on this leaderboard
            Debug.Log("Player has no score on this leaderboard");
            onSuccess?.Invoke(new System.Collections.Generic.List<LeaderboardEntry>());
        }
    }

    /// <summary>
    /// Called when player position retrieval fails
    /// </summary>
    private void OnGetPlayerPositionFailure(PlayFabError error, System.Action<string> onFailure)
    {
        string errorMessage = $"Failed to get player position: {error.GenerateErrorReport()}";
        Debug.LogWarning(errorMessage);
        onFailure?.Invoke(errorMessage);
    }

    #endregion

    #region Manual Login (Optional)

    /// <summary>
    /// Manually trigger login (useful for retry after failure)
    /// </summary>
    public void ManualLogin()
    {
        if (!isLoggedIn)
        {
            LoginWithDeviceID();
        }
        else
        {
            Debug.Log("Already logged into PlayFab");
        }
    }

    #endregion

    private void OnDestroy()
    {
        // 1. Unregister from DependencyRegistry
        DependencyRegistry.Unregister<PlayFabManager>(this);

        // 2. Unsubscribe from scene events
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // 3. Unsubscribe from manager events
        UnsubscribeFromEvents();
    }
}

