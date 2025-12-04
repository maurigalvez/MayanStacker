using PlayFab;
using PlayFab.ClientModels;
using PlayFab.ProgressionModels;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using System.Collections.Generic;
#endif

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
/// DISPLAY NAME MANAGEMENT:
/// This manager automatically sets player display names for leaderboards:
/// - Google Play Games users: Display name is automatically fetched and synced on login
/// - Before each score submission, the Google Play display name is verified and refreshed
/// - Other users: A friendly "Player_XXXXXX" format is used instead of raw IDs
/// - Display names can be manually updated using UpdateDisplayName() method
/// - Leaderboards will show these display names instead of entity IDs
/// 
/// DEBUG LOGGING:
/// Most verbose Debug.Log statements are wrapped with #if DEBUG_MODE for production builds.
/// - To enable verbose logging: Add "DEBUG_MODE" to Project Settings > Player > Scripting Define Symbols
/// - Error and warning logs (Debug.LogError/LogWarning) are always shown for troubleshooting
/// - Without DEBUG_MODE defined, only critical errors and warnings will be logged
/// 
/// GOOGLE PLAY GAMES SETUP (Android):
/// Google Play Games authentication is now implemented and ready to use!
/// This ensures PlayFab accounts are consistently tied to Google accounts.
/// 
/// Setup Steps:
/// 1. ✅ Install Google Play Games Plugin (Already installed)
/// 2. Configure OAuth 2.0 credentials in Google Play Console
///    - Create an Android OAuth 2.0 Client ID
///    - Create a Web Application OAuth 2.0 Client ID (for server-side access)
/// 3. Set up Google Play Games in Unity:
///    - Go to Window > Google Play Games > Setup > Android Setup
///    - Enter your Web App Client ID from Google Play Console
///    - Click Setup
/// 4. In PlayFab Game Manager, link Google Play Games:
///    - Settings > Title Settings > Add-ons > Google
///    - Add your Web Application OAuth 2.0 Client ID and Client Secret
///    - Save and enable the add-on
/// 5. Verify in logs that Google Account ID is being logged correctly
/// 
/// GOOGLE PLAY GAMES POPUPS & NOTIFICATIONS:
/// The SDK is configured to show Google Play Games popups and notifications:
/// - Welcome back messages when signing in
/// - XP and achievement notifications
/// - If popups don't appear, check device settings: 
///   Settings > Google > Games > Show notifications
/// 
/// The system will automatically fallback to Android Device ID if Google Play Games fails.
/// When successfully logged in with Google, the logs will show:
/// - Google Player ID (from Google Play Games)
/// - Google Account ID (from PlayFab)
/// - Whether the account was newly created or already exists
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
    private IntegrityManager integrityManager;

    // State
    private bool isLoggedIn = false;
    private string playFabId = "";
    private string currentDisplayName = "";
    private bool isSyncing = false;
    private string lastIntegrityToken = null; // Store last integrity token for logging

    // Cloud save constants
    private const string PLAYER_PROGRESS_KEY = "PlayerProgress";

    // Events
    public System.Action<string> OnLoginSuccess;
    public System.Action<string> OnLoginFailure;
    public System.Action<int> OnScoreSubmitted;
    public System.Action<string> OnScoreSubmissionFailed;

    // Cloud sync events
    public System.Action OnLoginStarted;
    public System.Action OnSyncStarted;
    public System.Action<PlayerProgressData> OnProgressSynced;
    public System.Action<string> OnProgressSyncFailed;

    // Properties
    public bool IsLoggedIn => isLoggedIn;
    public string PlayFabId => playFabId;
    public string CurrentDisplayName => currentDisplayName;

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

        // make sure GPGS is initialized


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
#if DEBUG_MODE
        Debug.Log($"PlayFabManager: Scene '{scene.name}' loaded, refreshing dependencies");
#endif
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
        integrityManager = DependencyRegistry.Find<IntegrityManager>();

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
#if DEBUG_MODE
        Debug.Log("Logging into PlayFab...");
#endif

        // Notify UI that login is starting
        OnLoginStarted?.Invoke();

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
            StartCoroutine(LoginWithGooglePlayGames());
        }
        else
        {
            LoginWithAndroidDeviceID();
        }
    }

    /// <summary>
    /// Attempts silent authentication with Google Play Games
    /// </summary>
    private void AttemptSilentAuthentication()
    {
#if UNITY_ANDROID
        Debug.Log("Attempting silent authentication...");

        PlayGamesPlatform.Instance.Authenticate((status) =>
        {
            Debug.Log($"Authenticate callback received with status: {status}");

            if (status == SignInStatus.Success)
            {
                Debug.Log("✓ Silent authentication successful!");
                OnGooglePlayGamesAuthenticationSuccess();
            }
            else
            {
                Debug.LogWarning($"Silent authentication failed with status: {status}");
                AttemptManualAuthentication();
            }
        });
#endif
    }

    /// <summary>
    /// Attempts manual authentication with Google Play Games (shows prompt to user)
    /// </summary>
    private void AttemptManualAuthentication()
    {
#if UNITY_ANDROID
        Debug.Log("Attempting manual authentication (will show prompt)...");

        PlayGamesPlatform.Instance.ManuallyAuthenticate((status) =>
        {
            Debug.Log($"Manual authentication callback received with status: {status}");

            if (status == SignInStatus.Success)
            {
                Debug.Log("✓ Manual authentication successful!");
                OnGooglePlayGamesAuthenticationSuccess();
            }
            else
            {
                Debug.LogError($"Manual authentication failed with status: {status}");
            }
        });
#endif
    }

    /// <summary>
    /// Called when Google Play Games authentication succeeds
    /// Handles getting server auth code and submitting to PlayFab
    /// </summary>
    private void OnGooglePlayGamesAuthenticationSuccess()
    {
#if UNITY_ANDROID
        // Get user info for logging
        string playerID = PlayGamesPlatform.Instance.GetUserId();
        string displayName = PlayGamesPlatform.Instance.GetUserDisplayName();

#if DEBUG_MODE
        Debug.Log($"Google Play Games Authentication Success:");
        Debug.Log($"  - Google Player ID: {playerID}");
        Debug.Log($"  - Display Name: {displayName ?? "[NULL - NOT AVAILABLE]"}");
#endif

        // Check if display name is null or looks like an ID (starts with 'g_' or is all numbers)
        if (string.IsNullOrEmpty(displayName) || displayName.StartsWith("g_") || displayName.All(char.IsDigit))
        {
            Debug.LogWarning($"Display name appears invalid or missing: '{displayName}'");
            Debug.LogWarning("This may cause leaderboards to show entity IDs instead of player names");
        }

        // Sync achievements with Google Play Games now that we're authenticated
        SyncAchievementsWithGooglePlay();

        // Get the server auth code for PlayFab with OPEN_ID scope (required for ID token)
#if DEBUG_MODE
        Debug.Log("Requesting server-side access token with OPEN_ID scope for PlayFab...");
#endif

        // Request OPEN_ID scope to ensure ID token is included (required by PlayFab)
        List<AuthScope> scopes = new List<AuthScope> { AuthScope.OPEN_ID };

        PlayGamesPlatform.Instance.RequestServerSideAccess(true, scopes, (authResponse) =>
        {
            if (authResponse != null)
            {
                string serverAuthCode = authResponse.GetAuthCode();
                List<AuthScope> grantedScopes = authResponse.GetGrantedScopes();

#if DEBUG_MODE
                Debug.Log($"Server auth response received:");
                Debug.Log($"  - Auth code length: {serverAuthCode?.Length ?? 0}");
                Debug.Log($"  - Granted scopes: {string.Join(", ", grantedScopes)}");
#endif

                if (!string.IsNullOrEmpty(serverAuthCode))
                {
#if DEBUG_MODE
                    Debug.Log("✓ Server auth code with OPEN_ID scope received, submitting to PlayFab...");
#endif
                    SubmitGooglePlayGamesLogin(serverAuthCode, playerID, displayName);
                }
                else
                {
                    Debug.LogError("Server auth code is null or empty");

                    if (fallbackToDeviceID)
                    {
#if DEBUG_MODE
                        Debug.Log("Falling back to Android Device ID...");
#endif
                        LoginWithAndroidDeviceID();
                    }
                }
            }
            else
            {
                Debug.LogError("Failed to get server auth response from Google Play Games (response is null)");

                if (fallbackToDeviceID)
                {
#if DEBUG_MODE
                    Debug.Log("Falling back to Android Device ID...");
#endif
                    LoginWithAndroidDeviceID();
                }
            }
        });
#endif
    }

    /// <summary>
    /// Login with Google Play Games (recommended for Android)
    /// Requires Google Play Games Plugin for Unity to be installed and configured
    /// Install from: https://github.com/playgameservices/play-games-plugin-for-unity
    /// </summary>
    private IEnumerator LoginWithGooglePlayGames()
    {
#if UNITY_ANDROID
#if DEBUG_MODE
        Debug.Log("Starting Google Play Games authentication...");
#endif

        // Initialize Google Play Games
        // Note: The plugin configuration should be done via Window > Google Play Games > Setup in Unity Editor
        // This ensures display name permissions are properly requested
        PlayGamesPlatform.DebugLogEnabled = true;
        PlayGamesPlatform.Activate();

#if DEBUG_MODE
        Debug.Log("Google Play Games platform activated");
#endif

        yield return new WaitForSeconds(0.5f);

        // Start the authentication flow (silent first, then manual if needed)
        AttemptSilentAuthentication();
#else
        Debug.LogError("Google Play Games is only available on Android!");
        LoginWithAndroidDeviceID();
        yield break;
#endif
    }

    /// <summary>
    /// Submit Google Play Games authentication to PlayFab
    /// This ensures the PlayFab account is tied to the Google Play Games account
    /// </summary>
    /// <param name="serverAuthCode">Server auth code from Google Play Games</param>
    /// <param name="playerID">Google Play Games Player ID for logging and verification</param>
    /// <param name="displayName">Google Play Games display name to set in PlayFab</param>
    private void SubmitGooglePlayGamesLogin(string serverAuthCode, string playerID, string displayName)
    {
        var request = new LoginWithGoogleAccountRequest
        {
            CreateAccount = createAccountIfNotExists,
            TitleId = PlayFabSettings.staticSettings.TitleId,
            ServerAuthCode = serverAuthCode,
            // InfoRequestParameters helps us get player info for verification AND current display name
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                GetUserAccountInfo = true,
                GetUserData = true
            }
        };

#if DEBUG_MODE
        Debug.Log($"Logging into PlayFab with Google Play Games (Player ID: {playerID}, Display Name: {displayName})...");
#endif

        PlayFabClientAPI.LoginWithGoogleAccount(request,
            result => OnGoogleLoginSuccessCallback(result, playerID, displayName),
            (error) =>
            {
                Debug.LogWarning($"PlayFab login with Google Play Games failed: {error.GenerateErrorReport()}");

                // Fallback to Device ID if enabled
                if (fallbackToDeviceID)
                {
#if DEBUG_MODE
                    Debug.Log("Falling back to Android Device ID authentication...");
#endif
                    LoginWithAndroidDeviceID();
                }
                else
                {
                    OnLoginFailureCallback(error);
                }
            });
    }

    /// <summary>
    /// Called when Google Play Games login to PlayFab is successful
    /// Logs additional account information for verification and sets display name
    /// </summary>
    private void OnGoogleLoginSuccessCallback(LoginResult result, string googlePlayerID, string googleDisplayName)
    {
        isLoggedIn = true;
        playFabId = result.PlayFabId;

#if DEBUG_MODE
        Debug.Log($"✓ PlayFab login successful via Google Play Games!");
        Debug.Log($"  - PlayFab ID: {playFabId}");
        Debug.Log($"  - Google Player ID: {googlePlayerID}");
        Debug.Log($"  - Google Display Name: {googleDisplayName ?? "Not available"}");
        Debug.Log($"  - Account Created: {result.NewlyCreated}");
#endif

        // Check current PlayFab display name
        string currentPlayFabDisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName;
        currentDisplayName = currentPlayFabDisplayName ?? ""; // Store for public access
#if DEBUG_MODE
        Debug.Log($"  - Current PlayFab Display Name: {currentPlayFabDisplayName ?? "[Not Set]"}");
#endif

        // Log Google account info if available
        if (result.InfoResultPayload?.AccountInfo?.GoogleInfo != null)
        {
            var googleInfo = result.InfoResultPayload.AccountInfo.GoogleInfo;
#if DEBUG_MODE
            Debug.Log($"  - Google Account linked: Yes");
            Debug.Log($"  - Google Account ID: {googleInfo.GoogleId}");
            Debug.Log($"  - Google Email: {googleInfo.GoogleEmail ?? "Not provided"}");
#endif
        }
        else
        {
            Debug.LogWarning("  - Google Account Info: Not available in response");
        }

        // Check if this is a new account
        if (result.NewlyCreated)
        {
#if DEBUG_MODE
            Debug.Log("New PlayFab account created and linked to Google Play Games account");
#endif
        }
        else
        {
#if DEBUG_MODE
            Debug.Log("Logged into existing PlayFab account linked to this Google Play Games account");
#endif
        }

        // Only update display name if needed (not already set correctly)
        StartCoroutine(SetDisplayNameIfNeeded(googleDisplayName, currentPlayFabDisplayName));

        OnLoginSuccess?.Invoke(playFabId);

        // Sync progress from cloud after login
        SyncProgressOnLogin();
    }

    /// <summary>
    /// Sets the display name only if needed (not already set correctly)
    /// Checks if current PlayFab name matches Google name, and only updates if different
    /// </summary>
    private IEnumerator SetDisplayNameIfNeeded(string googleDisplayName, string currentPlayFabDisplayName)
    {
        string displayName = googleDisplayName;

        // Check if display name is valid
        bool isValidName = !string.IsNullOrEmpty(displayName) &&
                          !displayName.StartsWith("g_") &&
                          !displayName.All(char.IsDigit);

#if UNITY_ANDROID
        // If display name is invalid, retry getting it from Google Play Games
        if (!isValidName && PlayGamesPlatform.Instance != null)
        {
            Debug.LogWarning("Initial display name is invalid, attempting to retrieve again...");

            // Wait a bit for Google Play Games to fully initialize
            yield return new WaitForSeconds(1.0f);

            // Try to get display name again
            displayName = PlayGamesPlatform.Instance.GetUserDisplayName();
            Debug.Log($"Retry: Retrieved display name: {displayName ?? "[NULL]"}");

            // Check validity again
            isValidName = !string.IsNullOrEmpty(displayName) &&
                         !displayName.StartsWith("g_") &&
                         !displayName.All(char.IsDigit);

            // If still invalid, try one more time after another delay
            if (!isValidName)
            {
                Debug.LogWarning("Display name still invalid, trying one more time...");
                yield return new WaitForSeconds(2.0f);

                displayName = PlayGamesPlatform.Instance.GetUserDisplayName();
                Debug.Log($"Final retry: Retrieved display name: {displayName ?? "[NULL]"}");

                isValidName = !string.IsNullOrEmpty(displayName) &&
                             !displayName.StartsWith("g_") &&
                             !displayName.All(char.IsDigit);
            }
        }
#endif

        // Check if current PlayFab display name is already correct
        if (!string.IsNullOrEmpty(currentPlayFabDisplayName) && currentPlayFabDisplayName == displayName)
        {
#if DEBUG_MODE
            Debug.Log($"✓ PlayFab display name already set correctly to: {currentPlayFabDisplayName}");
#endif
            yield break; // No need to update
        }

        // Check if current name looks like an entity ID (needs updating)
        bool currentNameIsEntityId = string.IsNullOrEmpty(currentPlayFabDisplayName) ||
                                     currentPlayFabDisplayName.Length > 16 || // Entity IDs are long
                                     currentPlayFabDisplayName.All(c => char.IsLetterOrDigit(c) || c == '-'); // Entity IDs are alphanumeric with dashes

        // Set the display name if valid and needed
        if (isValidName)
        {
#if DEBUG_MODE
            Debug.Log($"Updating PlayFab display name from '{currentPlayFabDisplayName ?? "[Not Set]"}' to '{displayName}'");
#endif
            SetDisplayNameDirectly(displayName, onSuccess: () =>
            {
                currentDisplayName = displayName; // Update stored display name on success
#if DEBUG_MODE
                Debug.Log($"✓ Display name successfully updated to: {displayName}");
#endif
            },
            onFailure: (error) =>
            {
                Debug.LogWarning($"Could not update display name: {error}");
                if (error.Contains("Name not available"))
                {
                    Debug.LogWarning("The display name is already in use. This is OK - your scores are still saved correctly.");
#if DEBUG_MODE
                    Debug.Log($"Your leaderboard entries will show as: {currentPlayFabDisplayName ?? "[Entity ID]"}");
#endif
                }
            });
        }
        else if (currentNameIsEntityId)
        {
            // Current name looks bad and we couldn't get a good Google name
            Debug.LogError($"Failed to get valid Google Play display name. Display name: '{displayName}'");
            Debug.LogWarning("Leaderboards may show entity IDs instead of player names.");

            // Try to set a fallback display name
            string fallbackName = $"Player_{playFabId.Substring(0, System.Math.Min(6, playFabId.Length))}";
#if DEBUG_MODE
            Debug.Log($"Attempting to set fallback display name: {fallbackName}");
#endif
            SetDisplayNameDirectly(fallbackName,
            onSuccess: () =>
            {
                currentDisplayName = fallbackName; // Update stored display name on success
            },
            onFailure: (error) =>
            {
                Debug.LogWarning("Could not set fallback name either. Scores will still save correctly.");
            });
        }
        else
        {
#if DEBUG_MODE
            Debug.Log($"Display name '{currentPlayFabDisplayName}' is acceptable, keeping as-is");
#endif
        }
    }

    /// <summary>
    /// Helper method to set the display name directly in PlayFab
    /// </summary>
    private void SetDisplayNameDirectly(string displayName, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            Debug.LogWarning("Cannot set empty display name");
            onFailure?.Invoke("Display name is empty");
            return;
        }

        var request = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = displayName
        };

        PlayFabClientAPI.UpdateUserTitleDisplayName(request,
            result =>
            {
                currentDisplayName = result.DisplayName; // Update stored display name
#if DEBUG_MODE
                Debug.Log($"✓ PlayFab display name set to: {result.DisplayName}");
#endif
                onSuccess?.Invoke();
            },
            error =>
            {
                string errorMessage = error.GenerateErrorReport();
                Debug.LogWarning($"Failed to update PlayFab display name: {errorMessage}");
                onFailure?.Invoke(errorMessage);
            });
    }

    /// <summary>
    /// Updates the PlayFab display name to match the Google Play Games display name
    /// This ensures leaderboards show the player's Google Play name instead of IDs
    /// </summary>
    private void UpdateDisplayNameFromGooglePlayGames()
    {
#if UNITY_ANDROID
        // Get the display name from Google Play Games
        string googleDisplayName = PlayGamesPlatform.Instance.GetUserDisplayName();

        if (!string.IsNullOrEmpty(googleDisplayName))
        {
            Debug.Log($"Updating PlayFab display name to Google Play name: {googleDisplayName}");
            SetDisplayNameDirectly(googleDisplayName);
        }
        else
        {
            Debug.LogWarning("Could not get display name from Google Play Games");
        }
#else
        Debug.Log("Display name update from Google Play Games only available on Android");
#endif
    }

    /// <summary>
    /// Ensures the display name is set before performing an action (like submitting a score)
    /// For Google Play Games users, fetches and updates the current display name
    /// For other users, uses the existing display name or sets a default
    /// </summary>
    /// <param name="onComplete">Callback to invoke after display name is verified/set</param>
    private void EnsureDisplayNameIsSet(System.Action onComplete)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot ensure display name - not logged into PlayFab");
            onComplete?.Invoke();
            return;
        }

#if UNITY_ANDROID
        // For Android with Google Play Games, refresh the display name
        if (useGooglePlayGames && PlayGamesPlatform.Instance != null)
        {
            string googleDisplayName = PlayGamesPlatform.Instance.GetUserDisplayName();

            if (!string.IsNullOrEmpty(googleDisplayName))
            {
                Debug.Log($"Ensuring PlayFab display name is set to Google Play name: {googleDisplayName}");
                SetDisplayNameDirectly(googleDisplayName);
            }
            else
            {
                Debug.LogWarning("Could not get display name from Google Play Games");
            }
        }
#endif
        // Always call onComplete regardless of display name update
        onComplete?.Invoke();
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
            TitleId = PlayFabSettings.staticSettings.TitleId,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true
            }
        };

#if DEBUG_MODE
        Debug.Log("Logging into PlayFab with Android Device ID...");
#endif
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
            TitleId = PlayFabSettings.staticSettings.TitleId,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true
            }
        };

#if DEBUG_MODE
        Debug.Log("Logging into PlayFab with iOS Device ID...");
#endif
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
            TitleId = PlayFabSettings.staticSettings.TitleId,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true
            }
        };

#if DEBUG_MODE
        Debug.LogWarning("Using CustomID fallback for PlayFab login (Editor/Unsupported platform)");
#endif
        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccessCallback, OnLoginFailureCallback);
    }

    /// <summary>
    /// Called when login is successful (non-Google methods)
    /// </summary>
    private void OnLoginSuccessCallback(LoginResult result)
    {
        isLoggedIn = true;
        playFabId = result.PlayFabId;

        // Get current display name if available
        if (result.InfoResultPayload?.PlayerProfile?.DisplayName != null)
        {
            currentDisplayName = result.InfoResultPayload.PlayerProfile.DisplayName;
        }

#if DEBUG_MODE
        Debug.Log($"PlayFab login successful! PlayFabId: {playFabId}");
#endif

        // Check if this is a new account
        if (result.NewlyCreated)
        {
#if DEBUG_MODE
            Debug.Log("New PlayFab account created for this device");
#endif
            // For new accounts without Google, set a default display name
            SetDefaultDisplayName();
        }

        OnLoginSuccess?.Invoke(playFabId);

        // Sync progress from cloud after login
        SyncProgressOnLogin();
    }

    /// <summary>
    /// Sets a default display name for accounts not using Google Play Games
    /// Uses a player-friendly format instead of showing raw IDs
    /// </summary>
    private void SetDefaultDisplayName()
    {
        // Create a more user-friendly display name
        string defaultName = $"Player_{playFabId.Substring(0, System.Math.Min(6, playFabId.Length))}";

        var request = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = defaultName
        };

        PlayFabClientAPI.UpdateUserTitleDisplayName(request,
            result =>
            {
                currentDisplayName = result.DisplayName; // Update stored display name
#if DEBUG_MODE
                Debug.Log($"Default display name set to: {result.DisplayName}");
#endif
            },
            error =>
            {
                Debug.LogWarning($"Failed to set default display name: {error.GenerateErrorReport()}");
            });
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
    private void OnLevelCompleted(int stars, int score, bool showCodexPopup)
    {
        if (gameManager == null) return;

        // Tell GameManager to save the high score for the completed level
        gameManager.SaveHighScoreIfNeeded();
#if DEBUG_MODE
        Debug.Log($"Level completed! Saving high score: {score} (Stars: {stars})");
#endif
    }

    /// <summary>
    /// Submit a score to a specific leaderboard using the new Statistics V2 API
    /// Ensures display name is set from Google Play Games before submission
    /// Performs Classic Integrity check before submission for security
    /// </summary>
    public void SubmitScore(string leaderboardName, int score)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot submit score - not logged into PlayFab");
            return;
        }

        // Perform Classic Integrity check before score submission
        if (integrityManager != null && integrityManager.IsIntegrityChecksEnabled)
        {
            // Generate a nonce for this score submission
            string nonce = IntegrityManager.GenerateNonce(32);
            
#if DEBUG_MODE
            Debug.Log($"[PlayFabManager] Requesting integrity check before submitting score {score} to {leaderboardName}");
#endif

            integrityManager.RequestClassicIntegrityToken(nonce, (integrityResult) =>
            {
                // Store the token for logging/debugging
                lastIntegrityToken = integrityResult.Token;

                if (!integrityResult.Success)
                {
                    Debug.LogWarning($"[PlayFabManager] Integrity check failed: {integrityResult.ErrorMessage}. Proceeding with submission (server-side verification recommended).");
                }
                else
                {
#if DEBUG_MODE
                    Debug.Log($"[PlayFabManager] Integrity check passed. Token length: {integrityResult.Token?.Length ?? 0}");
#endif
                }

                // Proceed with score submission regardless of integrity check result
                // (Integrity token should be sent to server for verification in production)
                SubmitScoreInternal(leaderboardName, score, integrityResult.Token);
            });
        }
        else
        {
            // No integrity manager or checks disabled, submit directly
            if (integrityManager == null)
            {
                Debug.LogWarning("[PlayFabManager] IntegrityManager not found. Score will be submitted without integrity check.");
            }
            SubmitScoreInternal(leaderboardName, score, null);
        }
    }

    /// <summary>
    /// Internal method to submit score after integrity check
    /// </summary>
    private void SubmitScoreInternal(string leaderboardName, int score, string integrityToken)
    {
        // Ensure display name is current from Google Play Games before submitting score
        EnsureDisplayNameIsSet(() =>
        {
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

            // Note: In production, the integrity token should be included in the request
            // and verified server-side. PlayFab CloudScript or Azure Functions can be used
            // to verify the token with Google's Integrity API verification endpoint.
            // For now, we log it for monitoring purposes.

#if DEBUG_MODE
            Debug.Log($"Submitting score {score} to leaderboard '{leaderboardName}' with current display name");
            if (!string.IsNullOrEmpty(integrityToken))
            {
                Debug.Log($"Integrity token available (length: {integrityToken.Length}) - should be verified server-side");
            }
#endif
            PlayFabProgressionAPI.UpdateStatistics(request, OnScoreSubmitSuccess, OnScoreSubmitFailure);
        });
    }

    /// <summary>
    /// Called when score submission is successful
    /// </summary>
    private void OnScoreSubmitSuccess(PlayFab.ProgressionModels.UpdateStatisticsResponse result)
    {
#if DEBUG_MODE
        Debug.Log("Score submitted to PlayFab successfully!");
#endif
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
#if DEBUG_MODE
                    Debug.Log($"Loaded high score from PlayFab: {highScore} for {leaderboardName}");
#endif
                }
            }
        }
#if DEBUG_MODE
        else
        {
            Debug.Log($"No high score found on PlayFab for leaderboard: {leaderboardName}");
        }
#endif

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
#if DEBUG_MODE
        Debug.Log($"Leaderboard retrieved! {result.Rankings.Count} entries");
#endif

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
#if DEBUG_MODE
                Debug.Log($"Player position: {playerEntry.Rank}, Score: {playerScore}");
#endif
            }

            onSuccess?.Invoke(entries);
        }
        else
        {
#if DEBUG_MODE
            // Player has no score on this leaderboard
            Debug.Log("Player has no score on this leaderboard");
#endif
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

    #region Manual Login and Display Name Management

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
#if DEBUG_MODE
            Debug.Log("Already logged into PlayFab");
#endif
        }
    }

    /// <summary>
    /// Manually update the player's display name in PlayFab
    /// Useful for allowing players to customize their leaderboard name
    /// </summary>
    /// <param name="displayName">The desired display name (3-25 characters)</param>
    /// <param name="onSuccess">Callback invoked on success with the new display name</param>
    /// <param name="onFailure">Callback invoked on failure with error message</param>
    public void UpdateDisplayName(string displayName, System.Action<string> onSuccess = null, System.Action<string> onFailure = null)
    {
        if (!isLoggedIn)
        {
            string error = "Cannot update display name - not logged into PlayFab";
            Debug.LogWarning(error);
            onFailure?.Invoke(error);
            return;
        }

        if (string.IsNullOrEmpty(displayName) || displayName.Length < 3 || displayName.Length > 25)
        {
            string error = "Display name must be between 3 and 25 characters";
            Debug.LogWarning(error);
            onFailure?.Invoke(error);
            return;
        }

        var request = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = displayName
        };

        PlayFabClientAPI.UpdateUserTitleDisplayName(request,
            result =>
            {
#if DEBUG_MODE
                Debug.Log($"Display name updated successfully to: {result.DisplayName}");
#endif
                onSuccess?.Invoke(result.DisplayName);
            },
            error =>
            {
                string errorMsg = $"Failed to update display name: {error.GenerateErrorReport()}";
                Debug.LogWarning(errorMsg);
                onFailure?.Invoke(errorMsg);
            });
    }

    /// <summary>
    /// Refresh the display name from Google Play Games
    /// Useful if the player changes their Google Play name or if it wasn't set correctly at login
    /// </summary>
    public void RefreshDisplayNameFromGoogle()
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot refresh display name - not logged into PlayFab");
            return;
        }

#if UNITY_ANDROID
        if (PlayGamesPlatform.Instance != null)
        {
#if DEBUG_MODE
            Debug.Log("Force refreshing display name from Google Play Games...");
#endif
            // Get current display name from Google Play Games and force update
            string googleDisplayName = PlayGamesPlatform.Instance.GetUserDisplayName();
            StartCoroutine(SetDisplayNameIfNeeded(googleDisplayName, null)); // null current name to force update
        }
        else
        {
            Debug.LogWarning("Google Play Games not available");
        }
#else
#if DEBUG_MODE
        Debug.Log("Display name refresh from Google Play Games only available on Android");
#endif
#endif
    }

    #endregion

    #region Cloud Save/Load

    /// <summary>
    /// Syncs progress from cloud after successful login
    /// Automatically called after login success
    /// </summary>
    private void SyncProgressOnLogin()
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot sync - not logged into PlayFab");
            return;
        }

        Debug.Log("Starting cloud progress sync...");
        OnSyncStarted?.Invoke();

        LoadProgressFromCloud(
            onSuccess: (data) =>
            {
                Debug.Log("Cloud progress loaded successfully");
                OnProgressSynced?.Invoke(data);
            },
            onFailure: (error) =>
            {
                Debug.LogWarning($"Failed to load cloud progress: {error}");
                OnProgressSyncFailed?.Invoke(error);
            }
        );
    }

    /// <summary>
    /// Saves player progress data to PlayFab cloud storage
    /// </summary>
    /// <param name="data">Progress data to save</param>
    /// <param name="onSuccess">Callback on successful save</param>
    /// <param name="onFailure">Callback on failed save with error message</param>
    public void SaveProgressToCloud(PlayerProgressData data, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        if (!isLoggedIn)
        {
            string error = "Cannot save to cloud - not logged into PlayFab";
            Debug.LogWarning(error);
            onFailure?.Invoke(error);
            return;
        }

        if (isSyncing)
        {
            Debug.LogWarning("Already syncing, skipping save request");
            return;
        }

        isSyncing = true;

        // Update sync timestamp
        data.UpdateSyncTimestamp();

        // Convert to JSON
        string json = data.ToJson();

        var request = new UpdateUserDataRequest
        {
            Data = new System.Collections.Generic.Dictionary<string, string>
            {
                { PLAYER_PROGRESS_KEY, json }
            }
        };

#if DEBUG_MODE
        Debug.Log($"Saving progress to cloud... (Data size: {json.Length} chars)");
#endif

        PlayFabClientAPI.UpdateUserData(request,
            result =>
            {
                isSyncing = false;
#if DEBUG_MODE
                Debug.Log("Progress saved to cloud successfully!");
#endif
                onSuccess?.Invoke();
            },
            error =>
            {
                isSyncing = false;
                string errorMsg = $"Failed to save progress to cloud: {error.GenerateErrorReport()}";
                Debug.LogWarning(errorMsg);
                onFailure?.Invoke(errorMsg);
            });
    }

    /// <summary>
    /// Loads player progress data from PlayFab cloud storage
    /// </summary>
    /// <param name="onSuccess">Callback with loaded progress data</param>
    /// <param name="onFailure">Callback on failed load with error message</param>
    public void LoadProgressFromCloud(System.Action<PlayerProgressData> onSuccess, System.Action<string> onFailure = null)
    {
        if (!isLoggedIn)
        {
            string error = "Cannot load from cloud - not logged into PlayFab";
            Debug.LogWarning(error);
            onFailure?.Invoke(error);
            return;
        }

        if (isSyncing)
        {
            Debug.LogWarning("Already syncing, skipping load request");
            return;
        }

        isSyncing = true;

        var request = new GetUserDataRequest();

#if DEBUG_MODE
        Debug.Log("Loading progress from cloud...");
#endif

        PlayFabClientAPI.GetUserData(request,
            result =>
            {
                isSyncing = false;

                // Check if progress data exists
                if (result.Data != null && result.Data.ContainsKey(PLAYER_PROGRESS_KEY))
                {
                    string json = result.Data[PLAYER_PROGRESS_KEY].Value;
                    PlayerProgressData data = PlayerProgressData.FromJson(json);

#if DEBUG_MODE
                    Debug.Log($"Progress loaded from cloud! Levels with stars: {data.levelStars.Count}, Infinite high score: {data.infiniteStackerHighScore}");
#endif

                    onSuccess?.Invoke(data);
                }
                else
                {
                    // No cloud data exists yet - this is normal for new accounts
#if DEBUG_MODE
                    Debug.Log("No cloud progress found - new account or first sync");
#endif
                    onSuccess?.Invoke(new PlayerProgressData());
                }
            },
            error =>
            {
                isSyncing = false;
                string errorMsg = $"Failed to load progress from cloud: {error.GenerateErrorReport()}";
                Debug.LogWarning(errorMsg);
                onFailure?.Invoke(errorMsg);
            });
    }

    /// <summary>
    /// Gets the current player progress from local managers and saves to cloud
    /// Call this when player completes a level or sets a new high score
    /// </summary>
    public void SaveCurrentProgressToCloud()
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot save - not logged into PlayFab");
            return;
        }

        // Collect progress from managers
        PlayerProgressData data = new PlayerProgressData();

        // Get level progress from LevelManager
        var levelManager = DependencyRegistry.Find<LevelManager>();
        if (levelManager != null)
        {
            var levels = levelManager.GetAllLevels();
            foreach (var level in levels)
            {
                int levelNumber = level.levelNumber;
                int stars = levelManager.GetLevelStars(levelNumber);
                int highScore = levelManager.GetLevelHighScore(levelNumber);

                if (stars > 0)
                {
                    data.levelStars[levelNumber] = stars;
                }

                if (highScore > 0)
                {
                    data.levelHighScores[levelNumber] = highScore;
                }
            }
        }

        // Get infinite mode high score from GameManager
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.InfiniteStacker)
        {
            data.infiniteStackerHighScore = gameManager.HighScore;
        }

        // Get achievement progress from AchievementManager
        var achievementManager = DependencyRegistry.Find<TamalStacker.Achievements.AchievementManager>();
        if (achievementManager != null && achievementManager.IsInitialized)
        {
            var achievementProgressData = achievementManager.GetProgressData();
            if (achievementProgressData != null)
            {
                data.achievementProgressJson = achievementProgressData.ToJson();
            }
        }

        // Save to cloud (fire-and-forget - don't block gameplay)
        SaveProgressToCloud(data,
            onSuccess: () =>
            {
#if DEBUG_MODE
                Debug.Log("Current progress saved to cloud");
#endif
            },
            onFailure: (error) =>
            {
                Debug.LogWarning($"Failed to save current progress: {error}");
            });
    }

    #endregion

    #region Achievement Sync

    /// <summary>
    /// Sync achievements with Google Play Games after authentication
    /// This will upload any locally unlocked achievements to Google Play Games
    /// Adds a delay to ensure Google Play Games SDK is fully ready
    /// </summary>
    private void SyncAchievementsWithGooglePlay()
    {
        StartCoroutine(SyncAchievementsWithDelay());
    }

    /// <summary>
    /// Coroutine to sync achievements with a delay
    /// Google Play Games needs time to fully initialize after authentication
    /// </summary>
    private IEnumerator SyncAchievementsWithDelay()
    {
        // Wait 1.5 seconds for Google Play Games to fully initialize
        Debug.Log("[PlayFabManager] Waiting for Google Play Games to fully initialize before syncing achievements...");
        yield return new WaitForSeconds(1.5f);

        var achievementManager = DependencyRegistry.Find<TamalStacker.Achievements.AchievementManager>();
        if (achievementManager == null)
        {
            Debug.LogWarning("[PlayFabManager] AchievementManager not found, cannot sync achievements");
            yield break;
        }

        if (!achievementManager.IsInitialized)
        {
            Debug.LogWarning("[PlayFabManager] AchievementManager not initialized yet, will sync later");
            yield break;
        }

        Debug.Log("[PlayFabManager] Now syncing achievements with Google Play Games...");
        achievementManager.SyncWithGooglePlay();
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


