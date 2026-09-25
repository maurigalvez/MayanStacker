using System.Collections;
using System.Linq;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.ProgressionModels;
using UnityEngine;
using UnityEngine.SceneManagement;

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
/// - Other users (no Google name): a temple name from MayanNameGenerator, e.g. "Jade Itzel".
///   Old "Player_XXXXXX" names are replaced on their next login; a later Google sign-in
///   replaces the temple name with the Google one.
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
    // Google Play Games is always used on Android (no toggle on purpose: a stray scene
    // override once turned it off and every new player got a "Player_XXXXXX" name).
    [Tooltip("If Google Play Games login fails, fallback to Android Device ID")]
    [SerializeField] private bool fallbackToDeviceID = true;

    // References (found via DependencyRegistry)
    private GameManager gameManager;
    private LevelManager levelManager;
    private IntegrityManager integrityManager;

    // State
    private bool isLoggedIn = false;
    private string playFabId = "";
    private string entityId = ""; // title_player_account id - leaderboard rankings are keyed by this, not playFabId
    private string currentDisplayName = "";
    private bool isSyncing = false;
    private string lastIntegrityToken = null; // Store last integrity token for logging
    private bool usedGoogleLogin = false; // Track if current session used Google login
    private bool usedFallbackLogin = false; // Track if fallback was used (important for debugging)
    private string lastGoogleLoginError = null; // Store error from failed Google login for event logging
    private string lastFailedGooglePlayerId = null; // Store Google Player ID from failed login
    private PlayerProgressData pendingMergedProgress = null; // From a Device ID account being merged into the Google one
    private bool loginInProgress = false; // Launch login or a "Sign in with Google Play" tap still running

    // Cloud save constants
    private const string PLAYER_PROGRESS_KEY = "PlayerProgress";
    private const string EXPECTED_PLAYFAB_ID_KEY = "ExpectedPlayFabId";
    private const string LAST_GOOGLE_PLAYER_ID_KEY = "LastGooglePlayerId";

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
    public string EntityId => entityId;
    public string CurrentDisplayName => currentDisplayName;
    public bool UsedFallbackLogin => usedFallbackLogin;
    public bool UsedGoogleLogin => usedGoogleLogin;
    public bool IsLoggingIn => loginInProgress;

    /// <summary>
    /// True when the player can still tap "Sign in with Google Play": on an Android device,
    /// logged in some other way (Device ID fallback) and not already signing in
    /// </summary>
    public bool CanSignInWithGoogle
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return !usedGoogleLogin && !loginInProgress;
#else
            return false;
#endif
        }
    }

    // Fired when who the player is could have changed: login, login failure, a new display
    // name, or a Google sign-in starting or ending. The identity UI refreshes on it.
    public System.Action OnAccountChanged;

    // Events for account mismatch detection
    public System.Action<string, string> OnAccountMismatchDetected; // (expectedId, actualId)

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
        loginInProgress = true;
        OnLoginStarted?.Invoke();
        OnAccountChanged?.Invoke();

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
    /// Login with Android - always uses Google Play Games (Device ID only as fallback)
    /// </summary>
    private void LoginWithAndroid()
    {
        StartCoroutine(LoginWithGooglePlayGames());
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
                // Without this the player never logs in at all (no leaderboard, no cloud save)
                FallBackToDeviceId($"gpgs_sign_in_{status}", null);
            }
        });
#endif
    }

    /// <summary>
    /// Player-initiated Google Play sign-in (Settings button) for someone whose launch sign-in
    /// failed. Runs the normal Google login, which links Google to this device's account or
    /// merges it into an existing Google account, so no progress or scores are lost.
    /// </summary>
    public void SignInWithGooglePlay()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!CanSignInWithGoogle) return;

        loginInProgress = true;
        OnAccountChanged?.Invoke();

        PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
        {
            if (status == SignInStatus.Success)
            {
                OnLoginStarted?.Invoke();
                OnGooglePlayGamesAuthenticationSuccess();
                return;
            }

            // Still logged into the device account, so there is nothing to fall back to
            Debug.LogWarning($"Google Play sign-in from Settings failed: {status}");
            loginInProgress = false;
            OnAccountChanged?.Invoke();
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

        // Look for an existing Google account first. Creating one straight away would strand
        // players who so far only had a Device ID account (and their scores) on it.
        RequestGoogleAuthCode(playerID,
            code => SubmitGooglePlayGamesLogin(code, playerID, displayName, createAccount: false));
#endif
    }

#if UNITY_ANDROID
    /// <summary>
    /// Gets a fresh server auth code (OPEN_ID scope, required by PlayFab). Codes are single-use,
    /// so every PlayFab Google call needs its own. Falls back to Device ID when none is issued.
    /// </summary>
    private void RequestGoogleAuthCode(string googlePlayerId, System.Action<string> onCode)
    {
        List<AuthScope> scopes = new List<AuthScope> { AuthScope.OPEN_ID };

        PlayGamesPlatform.Instance.RequestServerSideAccess(true, scopes, (authResponse) =>
        {
            string serverAuthCode = authResponse?.GetAuthCode();
            if (!string.IsNullOrEmpty(serverAuthCode))
            {
                onCode(serverAuthCode);
                return;
            }

            Debug.LogError("[ACCOUNT WARNING] No server auth code from Google Play Games");
            FallBackToDeviceId(authResponse == null ? "server_auth_response_null" : "server_auth_code_empty", googlePlayerId);
        });
    }
#endif

    /// <summary>
    /// Logs in with the Android Device ID after Google sign-in could not be used
    /// </summary>
    private void FallBackToDeviceId(string reason, string googlePlayerId)
    {
        if (!fallbackToDeviceID)
        {
            isLoggedIn = false;
            loginInProgress = false;
            Debug.LogError($"[ACCOUNT WARNING] Google login failed and Device ID fallback is off: {reason}");
            OnLoginFailure?.Invoke(reason);
            OnAccountChanged?.Invoke();
            return;
        }

        Debug.LogWarning($"[ACCOUNT WARNING] Falling back to Android Device ID ({reason}). " +
            "This may result in a DIFFERENT PlayFab account!");
        usedFallbackLogin = true;
        lastGoogleLoginError = reason;
        lastFailedGooglePlayerId = googlePlayerId;
        LoginWithAndroidDeviceID();
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
    /// <param name="createAccount">False on the first try, so a Device ID account can be adopted instead</param>
    private void SubmitGooglePlayGamesLogin(string serverAuthCode, string playerID, string displayName, bool createAccount)
    {
        var request = new LoginWithGoogleAccountRequest
        {
            CreateAccount = createAccount && createAccountIfNotExists,
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
                if (!createAccount && error.Error == PlayFabErrorCode.AccountNotFound)
                {
                    AdoptDeviceAccountForGoogle(playerID, displayName);
                    return;
                }

                string errorReport = error.GenerateErrorReport();
                Debug.LogError($"[ACCOUNT WARNING] PlayFab login with Google Play Games failed: {errorReport}");
                FallBackToDeviceId(errorReport, playerID);
            });
    }

    /// <summary>
    /// No PlayFab account is linked to this Google user yet. If this device already has a
    /// Device ID account, link Google to it so the player keeps their progress and scores and
    /// gets their Google name. Otherwise create a fresh Google account.
    /// </summary>
    private void AdoptDeviceAccountForGoogle(string playerID, string displayName)
    {
#if UNITY_ANDROID
        var request = new LoginWithAndroidDeviceIDRequest
        {
            AndroidDeviceId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = false,
            TitleId = PlayFabSettings.staticSettings.TitleId,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                GetUserAccountInfo = true
            }
        };

        PlayFabClientAPI.LoginWithAndroidDeviceID(request,
            deviceResult =>
            {
                // Linked to a different Google user (shared phone): leave it alone
                if (deviceResult.InfoResultPayload?.AccountInfo?.GoogleInfo != null)
                {
                    RequestGoogleAuthCode(playerID,
                        code => SubmitGooglePlayGamesLogin(code, playerID, displayName, createAccount: true));
                    return;
                }

                RequestGoogleAuthCode(playerID, code =>
                    PlayFabClientAPI.LinkGoogleAccount(
                        new LinkGoogleAccountRequest { ServerAuthCode = code, ForceLink = false },
                        _ =>
                        {
                            Debug.Log($"[Account Recovery] Linked Google to existing Device ID account {deviceResult.PlayFabId}");
                            OnGoogleLoginSuccessCallback(deviceResult, playerID, displayName);
                        },
                        linkError =>
                        {
                            // Still logged into the device account, so the player keeps their progress
                            Debug.LogWarning($"[Account Recovery] Could not link Google to device account: {linkError.GenerateErrorReport()}");
                            usedFallbackLogin = true;
                            lastGoogleLoginError = linkError.GenerateErrorReport();
                            lastFailedGooglePlayerId = playerID;
                            OnLoginSuccessCallback(deviceResult);
                        }));
            },
            error =>
            {
                if (error.Error == PlayFabErrorCode.AccountNotFound)
                {
                    // Brand-new player
                    RequestGoogleAuthCode(playerID,
                        code => SubmitGooglePlayGamesLogin(code, playerID, displayName, createAccount: true));
                }
                else
                {
                    OnLoginFailureCallback(error);
                }
            });
#endif
    }

    /// <summary>
    /// Called when Google Play Games login to PlayFab is successful
    /// Logs additional account information for verification and sets display name
    /// Links Android Device ID to this account for fallback recovery
    /// </summary>
    private void OnGoogleLoginSuccessCallback(LoginResult result, string googlePlayerID, string googleDisplayName)
    {
        string linkedDeviceId = result.InfoResultPayload?.AccountInfo?.AndroidDeviceInfo?.AndroidDeviceId;
        if (linkedDeviceId == SystemInfo.deviceUniqueIdentifier)
        {
            FinishGoogleLogin(result, googlePlayerID, googleDisplayName);
            return;
        }

        LinkDeviceOrMergeSplitAccount(result, googlePlayerID, googleDisplayName);
    }

    private void FinishGoogleLogin(LoginResult result, string googlePlayerID, string googleDisplayName)
    {
        isLoggedIn = true;
        playFabId = result.PlayFabId;
        entityId = result.EntityToken?.Entity?.Id ?? "";
        usedGoogleLogin = true;
        usedFallbackLogin = false;
        loginInProgress = false;

#if DEBUG_MODE
        Debug.Log($"✓ PlayFab login successful via Google Play Games!");
        Debug.Log($"  - PlayFab ID: {playFabId}");
        Debug.Log($"  - Google Player ID: {googlePlayerID}");
        Debug.Log($"  - Google Display Name: {googleDisplayName ?? "Not available"}");
        Debug.Log($"  - Account Created: {result.NewlyCreated}");
#endif

        // Check current PlayFab display name
        string currentPlayFabDisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName;
        SetCurrentDisplayName(currentPlayFabDisplayName);
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

        // IMPORTANT: Store the expected PlayFab ID and Google Player ID locally
        // This helps detect account mismatches if fallback is used later
        StoreExpectedAccountInfo(playFabId, googlePlayerID);

        // Only update display name if needed (not already set correctly)
        StartCoroutine(SetDisplayNameIfNeeded(googleDisplayName, currentPlayFabDisplayName));

        OnLoginSuccess?.Invoke(playFabId);
        OnAccountChanged?.Invoke();

        // Sync progress from cloud after login
        SyncProgressOnLogin();

        // Sync any queued scores from offline play
        SyncQueuedScores();
    }

    /// <summary>
    /// Stores the expected PlayFab ID and Google Player ID locally
    /// Used to detect account mismatches when fallback login is used
    /// </summary>
    private void StoreExpectedAccountInfo(string expectedPlayFabId, string googlePlayerId)
    {
        PlayerPrefs.SetString(EXPECTED_PLAYFAB_ID_KEY, expectedPlayFabId);
        if (!string.IsNullOrEmpty(googlePlayerId))
        {
            PlayerPrefs.SetString(LAST_GOOGLE_PLAYER_ID_KEY, googlePlayerId);
        }
        PlayerPrefs.Save();
#if DEBUG_MODE
        Debug.Log($"[Account Recovery] Stored expected PlayFab ID: {expectedPlayFabId}");
#endif
    }

    /// <summary>
    /// Links this device to the Google account so a later Device ID fallback finds the same
    /// account. If the device already belongs to another, Google-less account (players who got
    /// a Device ID account while Google sign-in was off), that account's progress and scores
    /// are merged into this one and the device is moved over. Login finishes afterwards.
    /// </summary>
    private void LinkDeviceOrMergeSplitAccount(LoginResult googleResult, string googlePlayerID, string googleDisplayName)
    {
#if UNITY_ANDROID
        var request = new LinkAndroidDeviceIDRequest
        {
            AndroidDeviceId = SystemInfo.deviceUniqueIdentifier,
            ForceLink = false // Don't take the device from another account without merging it first
        };

        PlayFabClientAPI.LinkAndroidDeviceID(request,
            _ =>
            {
                LogDeviceLinkingEvent(true);
                FinishGoogleLogin(googleResult, googlePlayerID, googleDisplayName);
            },
            error =>
            {
                string mergeCheckedKey = $"DeviceMergeChecked_{googleResult.PlayFabId}";
                if (error.Error == PlayFabErrorCode.LinkedDeviceAlreadyClaimed && PlayerPrefs.GetInt(mergeCheckedKey, 0) == 0)
                {
                    // Only ever try once per account, so a failure can't cost extra logins every launch
                    PlayerPrefs.SetInt(mergeCheckedKey, 1);
                    PlayerPrefs.Save();
                    MergeDeviceAccountInto(googlePlayerID, googleDisplayName);
                    return;
                }

                Debug.LogWarning($"[Account Recovery] Could not link Android Device ID: {error.GenerateErrorReport()}");
                LogDeviceLinkingEvent(false, error.GenerateErrorReport());
                FinishGoogleLogin(googleResult, googlePlayerID, googleDisplayName);
            });
#else
        FinishGoogleLogin(googleResult, googlePlayerID, googleDisplayName);
#endif
    }

    /// <summary>
    /// Reads the progress off the account this device is linked to, logs back into the Google
    /// account, moves the device over and finishes login. The progress is merged in
    /// SyncProgressOnLogin, which also resubmits the merged best scores.
    /// </summary>
    private void MergeDeviceAccountInto(string googlePlayerID, string googleDisplayName)
    {
#if UNITY_ANDROID
        var request = new LoginWithAndroidDeviceIDRequest
        {
            AndroidDeviceId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = false,
            TitleId = PlayFabSettings.staticSettings.TitleId,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetUserAccountInfo = true,
                GetUserData = true,
                UserDataKeys = new List<string> { PLAYER_PROGRESS_KEY }
            }
        };

        PlayFabClientAPI.LoginWithAndroidDeviceID(request,
            deviceResult =>
            {
                // A device account linked to another Google user is someone else's: don't merge it
                bool ownedByOtherGoogleUser = deviceResult.InfoResultPayload?.AccountInfo?.GoogleInfo != null;
                if (!ownedByOtherGoogleUser)
                {
                    var userData = deviceResult.InfoResultPayload?.UserData;
                    if (userData != null && userData.TryGetValue(PLAYER_PROGRESS_KEY, out var record))
                    {
                        pendingMergedProgress = PlayerProgressData.FromJson(record.Value);
                    }
                    Debug.Log($"[Account Recovery] Merging Device ID account {deviceResult.PlayFabId} into the Google account");
                }

                LoginToGoogleAccountAgain(googlePlayerID, googleDisplayName, moveDevice: !ownedByOtherGoogleUser);
            },
            error =>
            {
                Debug.LogWarning($"[Account Recovery] Could not read the device's account: {error.GenerateErrorReport()}");
                LoginToGoogleAccountAgain(googlePlayerID, googleDisplayName, moveDevice: false);
            });
#endif
    }

    /// <summary>
    /// Restores the Google account session after MergeDeviceAccountInto logged into the
    /// device account, optionally taking over the device link, then finishes login.
    /// </summary>
    private void LoginToGoogleAccountAgain(string googlePlayerID, string googleDisplayName, bool moveDevice)
    {
#if UNITY_ANDROID
        RequestGoogleAuthCode(googlePlayerID, code =>
        {
            var request = new LoginWithGoogleAccountRequest
            {
                CreateAccount = false,
                TitleId = PlayFabSettings.staticSettings.TitleId,
                ServerAuthCode = code,
                InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
                {
                    GetPlayerProfile = true,
                    GetUserAccountInfo = true,
                    GetUserData = true
                }
            };

            PlayFabClientAPI.LoginWithGoogleAccount(request,
                googleResult =>
                {
                    if (!moveDevice)
                    {
                        FinishGoogleLogin(googleResult, googlePlayerID, googleDisplayName);
                        return;
                    }

                    PlayFabClientAPI.LinkAndroidDeviceID(
                        new LinkAndroidDeviceIDRequest { AndroidDeviceId = SystemInfo.deviceUniqueIdentifier, ForceLink = true },
                        _ =>
                        {
                            LogDeviceLinkingEvent(true, "moved_from_device_account");
                            FinishGoogleLogin(googleResult, googlePlayerID, googleDisplayName);
                        },
                        error =>
                        {
                            LogDeviceLinkingEvent(false, error.GenerateErrorReport());
                            FinishGoogleLogin(googleResult, googlePlayerID, googleDisplayName);
                        });
                },
                error =>
                {
                    // Lands on the device account again, where the pending merge would be a no-op
                    pendingMergedProgress = null;
                    FallBackToDeviceId(error.GenerateErrorReport(), googlePlayerID);
                });
        });
#endif
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
        if (isValidName && IsGoogleNameApplied(currentPlayFabDisplayName, displayName))
        {
#if DEBUG_MODE
            Debug.Log($"✓ PlayFab display name already set correctly to: {currentPlayFabDisplayName}");
#endif
            yield break; // No need to update
        }

        // Set the display name if valid and needed
        if (isValidName)
        {
#if DEBUG_MODE
            Debug.Log($"Updating PlayFab display name from '{currentPlayFabDisplayName ?? "[Not Set]"}' to '{displayName}'");
#endif
            SetUniqueDisplayName(displayName);
        }
        else if (MayanNameGenerator.IsPlaceholder(currentPlayFabDisplayName))
        {
            // No usable Google name and nothing chosen yet (or the old "Player_XXXXXX")
            Debug.LogWarning($"Failed to get valid Google Play display name ('{displayName}'), using a temple name");
            AssignMayanName();
        }
        else
        {
#if DEBUG_MODE
            Debug.Log($"Display name '{currentPlayFabDisplayName}' is acceptable, keeping as-is");
#endif
        }
    }

    private const int NameTagBaseMaxLength = 20; // PlayFab display names are 3-25 chars; "#XXXX" takes 5

    /// <summary>
    /// The name with "#" and the last 4 characters of the PlayFab ID, e.g. "Carlos#1A2B".
    /// Always the same for a player, so it doesn't change between logins.
    /// </summary>
    private string WithNameTag(string name)
    {
        string baseName = name.Length > NameTagBaseMaxLength ? name.Substring(0, NameTagBaseMaxLength) : name;
        string tag = playFabId.Length > 4 ? playFabId.Substring(playFabId.Length - 4) : playFabId;
        return $"{baseName}#{tag}";
    }

    /// <summary>
    /// Sets the name, or the tagged name when another player already has it
    /// (display names are unique per title)
    /// </summary>
    private void SetUniqueDisplayName(string name)
    {
        SetDisplayNameDirectly(name, onFailure: error =>
        {
            if (!error.Contains("Name not available")) return;

            string taggedName = WithNameTag(name);
            Debug.LogWarning($"'{name}' is taken, using '{taggedName}'");
            SetDisplayNameDirectly(taggedName);
        });
    }

    /// <summary>
    /// Gives a player without a Google Play name a temple name instead of "Player_XXXXXX"
    /// </summary>
    private void AssignMayanName()
    {
        string name = MayanNameGenerator.For(playFabId);
#if DEBUG_MODE
        Debug.Log($"Assigning temple name: {name}");
#endif
        SetUniqueDisplayName(name);
    }

    private void SetCurrentDisplayName(string name)
    {
        name = name ?? "";
        if (name == currentDisplayName) return;

        currentDisplayName = name;
        OnAccountChanged?.Invoke();
    }

    /// <summary>
    /// True when the PlayFab name already is the Google name, plain or with its tag
    /// </summary>
    private bool IsGoogleNameApplied(string playFabName, string googleName)
    {
        if (string.IsNullOrEmpty(playFabName) || string.IsNullOrEmpty(googleName)) return false;
        return playFabName == googleName || playFabName == WithNameTag(googleName);
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
                SetCurrentDisplayName(result.DisplayName);
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
        // Only on the Google-linked account: a Device ID fallback account taking the name
        // would force the real account onto the tagged name
        if (usedGoogleLogin && PlayGamesPlatform.Instance != null)
        {
            string googleDisplayName = PlayGamesPlatform.Instance.GetUserDisplayName();

            if (!string.IsNullOrEmpty(googleDisplayName))
            {
                if (!IsGoogleNameApplied(currentDisplayName, googleDisplayName))
                {
                    StartCoroutine(SetDisplayNameIfNeeded(googleDisplayName, currentDisplayName));
                }
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
    /// Called when login is successful (non-Google methods - Device ID fallback)
    /// Checks for account mismatch to detect when fallback created/found a different account
    /// </summary>
    private void OnLoginSuccessCallback(LoginResult result)
    {
        isLoggedIn = true;
        playFabId = result.PlayFabId;
        entityId = result.EntityToken?.Entity?.Id ?? "";
        usedGoogleLogin = false;
        loginInProgress = false;

        // Not kept from an earlier login this session: that may have been another account
        SetCurrentDisplayName(result.InfoResultPayload?.PlayerProfile?.DisplayName);

#if DEBUG_MODE
        Debug.Log($"PlayFab login successful! PlayFabId: {playFabId}");
#endif

        // LOG PLAYFAB EVENTS for fallback scenarios (logged after login so we have a valid session)
        if (usedFallbackLogin)
        {
            // Log the Google login failure event (now that we're logged in)
            if (!string.IsNullOrEmpty(lastGoogleLoginError))
            {
                LogGoogleLoginFailureEvent(lastGoogleLoginError, lastFailedGooglePlayerId);
            }

            // Log that fallback was used
            LogAccountFallbackEvent(lastGoogleLoginError ?? "unknown_error", lastFailedGooglePlayerId);

            // Clear the stored error info
            lastGoogleLoginError = null;
            lastFailedGooglePlayerId = null;
        }

        // IMPORTANT: Check for account mismatch (especially after fallback from Google login failure)
        CheckForAccountMismatch(result);

        // New accounts, and old ones still on "Player_XXXXXX", get a temple name.
        // A Google-linked account reached through the fallback keeps its Google name.
        if (MayanNameGenerator.IsPlaceholder(currentDisplayName))
        {
            AssignMayanName();
        }

        // Check if this is a new account
        if (result.NewlyCreated)
        {
#if DEBUG_MODE
            Debug.Log("New PlayFab account created for this device");
#endif

            // If fallback was used and this is a NEW account, this is likely the bug scenario!
            if (usedFallbackLogin)
            {
                Debug.LogError("[ACCOUNT WARNING] ⚠️ A NEW PlayFab account was created via fallback! " +
                    "This likely means the player's data is now on a different account. " +
                    "Previous Google account data may be lost or inaccessible.");

                string expectedId = PlayerPrefs.GetString(EXPECTED_PLAYFAB_ID_KEY, "");
                string lastGoogleId = PlayerPrefs.GetString(LAST_GOOGLE_PLAYER_ID_KEY, "");
                if (!string.IsNullOrEmpty(expectedId))
                {
                    Debug.LogError($"[ACCOUNT WARNING] Expected PlayFab ID: {expectedId}");
                    Debug.LogError($"[ACCOUNT WARNING] Actual PlayFab ID: {playFabId}");
                    Debug.LogError($"[ACCOUNT WARNING] Last Google Player ID: {lastGoogleId}");
                }
            }
        }

        OnLoginSuccess?.Invoke(playFabId);
        OnAccountChanged?.Invoke();

        // Sync progress from cloud after login
        SyncProgressOnLogin();

        // Sync any queued scores from offline play
        SyncQueuedScores();
    }

    /// <summary>
    /// Checks if the logged-in account matches the expected account from previous Google login
    /// Logs warnings and fires event if a mismatch is detected
    /// </summary>
    private void CheckForAccountMismatch(LoginResult result)
    {
        string expectedPlayFabId = PlayerPrefs.GetString(EXPECTED_PLAYFAB_ID_KEY, "");
        string lastGooglePlayerId = PlayerPrefs.GetString(LAST_GOOGLE_PLAYER_ID_KEY, "");

        if (string.IsNullOrEmpty(expectedPlayFabId))
        {
            // No previous account stored - this is the first login
#if DEBUG_MODE
            Debug.Log("[Account Check] No previous account stored - this appears to be first login");
#endif
            return;
        }

        if (playFabId == expectedPlayFabId)
        {
            // Account matches - everything is good
            Debug.Log("[Account Check] ✓ Logged into expected account (account recovery successful if fallback was used)");
            return;
        }

        // MISMATCH DETECTED - Log to PlayFab for remote tracking
        LogAccountMismatchEvent(expectedPlayFabId, playFabId, result.NewlyCreated);

        // Fire event so UI can potentially warn the user
        OnAccountMismatchDetected?.Invoke(expectedPlayFabId, playFabId);
    }

    /// <summary>
    /// Called when login fails
    /// </summary>
    private void OnLoginFailureCallback(PlayFabError error)
    {
        isLoggedIn = false;
        loginInProgress = false;

        string errorMessage = $"PlayFab login failed: {error.GenerateErrorReport()}";
        Debug.LogError(errorMessage);

        OnLoginFailure?.Invoke(errorMessage);
        OnAccountChanged?.Invoke();
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
    /// If offline, queues the score for later sync
    /// </summary>
    public void SubmitScore(string leaderboardName, int score)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot submit score - not logged into PlayFab. Queueing for later sync.");
            OfflineScoreQueue.QueueScore(leaderboardName, score);
            return;
        }

        // Check network connectivity
        if (NetworkUtility.IsOffline())
        {
            Debug.LogWarning($"Cannot submit score - device is offline. Queueing score {score} for {leaderboardName} for later sync.");
            OfflineScoreQueue.QueueScore(leaderboardName, score);
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
            // Use closures to capture leaderboard name and score for error handling
            PlayFabProgressionAPI.UpdateStatistics(request,
                (result) => OnScoreSubmitSuccess(result, leaderboardName, score),
                (error) => OnScoreSubmitFailure(error, leaderboardName, score));
        });
    }

    /// <summary>
    /// Called when score submission is successful
    /// </summary>
    private void OnScoreSubmitSuccess(PlayFab.ProgressionModels.UpdateStatisticsResponse result, string leaderboardName, int score)
    {
#if DEBUG_MODE
        Debug.Log($"Score {score} submitted to PlayFab successfully for {leaderboardName}!");
#endif
        // Clear this score from queue if it was queued
        OfflineScoreQueue.ClearQueuedScore(leaderboardName, score);
        OnScoreSubmitted?.Invoke(score);
    }

    /// <summary>
    /// Called when score submission fails
    /// If it's a network error, queues the score for later sync
    /// </summary>
    private void OnScoreSubmitFailure(PlayFabError error, string leaderboardName, int score)
    {
        string errorMessage = $"Score submission failed: {error.GenerateErrorReport()}";
        Debug.LogWarning(errorMessage);

        // If it's a network error, queue the score for later sync
        if (IsNetworkError(error) || NetworkUtility.IsOffline())
        {
            Debug.LogWarning($"Score submission failed due to network error. Queueing score {score} for {leaderboardName} for later sync.");
            OfflineScoreQueue.QueueScore(leaderboardName, score);
        }

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
    /// True when a leaderboard ranking belongs to the signed-in player.
    /// Progression rankings are keyed by the title_player_account entity, so playFabId
    /// (the master account id) never matches - it is only kept as a fallback.
    /// </summary>
    private bool IsSelf(PlayFab.ProgressionModels.EntityKey entity)
    {
        if (entity == null || string.IsNullOrEmpty(entity.Id)) return false;
        if (!string.IsNullOrEmpty(entityId) && entity.Id == entityId) return true;
        return !string.IsNullOrEmpty(playFabId) && entity.Id == playFabId;
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

        // Check network connectivity
        if (NetworkUtility.IsOffline())
        {
            Debug.LogWarning("Cannot get leaderboard - device is offline");
            onFailure?.Invoke("Connect to a Network to See This Leaderboard");
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
            bool isCurrentPlayer = IsSelf(entry.Entity);

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
    /// Normalizes error messages for user-friendly display
    /// </summary>
    private void OnGetLeaderboardFailure(PlayFabError error, System.Action<string> onFailure)
    {
        string errorMessage;
        if (IsNetworkError(error) || NetworkUtility.IsOffline())
        {
            errorMessage = "Connect to a Network to See This Leaderboard";
        }
        else
        {
            errorMessage = "Something Went wrong when Retrieving Leaderboard, Try Again Later";
        }
        Debug.LogWarning($"Failed to get leaderboard: {error.GenerateErrorReport()}");
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

        // Check network connectivity
        if (NetworkUtility.IsOffline())
        {
            Debug.LogWarning("Cannot get leaderboard position - device is offline");
            onFailure?.Invoke("Connect to a Network to See This Leaderboard");
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
                bool isCurrentPlayer = IsSelf(entry.Entity);

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
            var playerEntry = result.Rankings.Find(e => IsSelf(e.Entity));
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
    /// Normalizes error messages for user-friendly display
    /// </summary>
    private void OnGetPlayerPositionFailure(PlayFabError error, System.Action<string> onFailure)
    {
        string errorMessage;
        if (IsNetworkError(error) || NetworkUtility.IsOffline())
        {
            errorMessage = "Connect to a Network to See This Leaderboard";
        }
        else
        {
            errorMessage = "Something Went wrong when Retrieving Leaderboard, Try Again Later";
        }
        Debug.LogWarning($"Failed to get player position: {error.GenerateErrorReport()}");
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
                SetCurrentDisplayName(result.DisplayName);
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
            // Passing the real current name: with a null one a missing Google name would replace it
            StartCoroutine(SetDisplayNameIfNeeded(googleDisplayName, currentDisplayName));
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

    /// <summary>
    /// Attempts to re-authenticate with Google Play Games to recover the correct account
    /// Call this if an account mismatch was detected
    /// </summary>
    /// <param name="onSuccess">Callback when re-authentication succeeds</param>
    /// <param name="onFailure">Callback when re-authentication fails</param>
    public void AttemptAccountRecovery(System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
#if UNITY_ANDROID
        Debug.Log("[Account Recovery] Attempting to re-authenticate with Google Play Games...");

        // Force manual authentication to allow user to pick the correct account
        PlayGamesPlatform.Instance.ManuallyAuthenticate((status) =>
        {
            if (status == SignInStatus.Success)
            {
                Debug.Log("[Account Recovery] Google authentication successful, now logging into PlayFab...");
                
                // Get the new credentials and login to PlayFab
                OnGooglePlayGamesAuthenticationSuccess();
                onSuccess?.Invoke();
            }
            else
            {
                string error = $"Google Play Games re-authentication failed: {status}";
                Debug.LogError($"[Account Recovery] {error}");
                onFailure?.Invoke(error);
            }
        });
#else
        onFailure?.Invoke("Account recovery is only available on Android");
#endif
    }

    /// <summary>
    /// Gets the expected PlayFab ID from local storage
    /// Returns null if no previous account was stored
    /// </summary>
    public string GetExpectedPlayFabId()
    {
        return PlayerPrefs.GetString(EXPECTED_PLAYFAB_ID_KEY, null);
    }

    /// <summary>
    /// Gets the last Google Player ID from local storage
    /// Returns null if no previous Google account was stored
    /// </summary>
    public string GetLastGooglePlayerId()
    {
        return PlayerPrefs.GetString(LAST_GOOGLE_PLAYER_ID_KEY, null);
    }

    /// <summary>
    /// Clears the stored expected account info
    /// Use this if the user explicitly wants to use the current account
    /// </summary>
    public void ClearExpectedAccountInfo()
    {
        PlayerPrefs.DeleteKey(EXPECTED_PLAYFAB_ID_KEY);
        PlayerPrefs.DeleteKey(LAST_GOOGLE_PLAYER_ID_KEY);
        PlayerPrefs.Save();
        Debug.Log("[Account Recovery] Cleared stored account info - current account will be used going forward");
    }

    /// <summary>
    /// Checks if there's a potential account mismatch (without requiring login)
    /// Compares stored expected ID with current logged-in ID
    /// </summary>
    /// <returns>True if mismatch detected, false if OK or no previous account</returns>
    public bool HasAccountMismatch()
    {
        if (!isLoggedIn) return false;

        string expectedId = GetExpectedPlayFabId();
        if (string.IsNullOrEmpty(expectedId)) return false;

        return playFabId != expectedId;
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

                bool merged = pendingMergedProgress != null;
                if (merged)
                {
                    data.MergeFrom(pendingMergedProgress);
                    pendingMergedProgress = null;
                }

                // Merged here rather than by a listener: the ghost line only lives in the game
                // scene, and login usually happens before one is loaded.
                InfiniteBest.MergeFromCloud(data);
                PowerUnlocks.MergeFromCloud(data);

                OnProgressSynced?.Invoke(data);

                if (merged)
                {
                    SaveProgressToCloud(data);
                }
                BackfillBestScores(data, force: merged);
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

        // Get infinite mode high score
        // IMPORTANT: Always include the infinite stacker high score, regardless of current game mode
        // This prevents data loss when saving from level mode (which would otherwise overwrite cloud data with 0)
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.InfiniteStacker)
        {
            // In infinite mode, use the in-memory high score (most up-to-date)
            data.infiniteStackerHighScore = gameManager.HighScore;
        }
        else
        {
            // Not in infinite mode - read from PlayerPrefs (cached from cloud sync)
            // This ensures we don't lose the infinite high score when saving from level mode
            data.infiniteStackerHighScore = PlayerPrefs.GetInt("HighScore_InfiniteStacker", 0);
        }

        // Tallest Infinite tower (drives the personal-best ghost line). Always local, so it
        // survives saves made from any mode.
        InfiniteBest.WriteTo(data);

        // Earned powers. Local flags only ever grow, so this never drops a cloud unlock.
        PowerUnlocks.WriteTo(data);

        // Get achievement progress from AchievementManager
        // Fallback to PlayerPrefs if manager isn't available or initialized
        var achievementManager = DependencyRegistry.Find<TamalStacker.Achievements.AchievementManager>();
        if (achievementManager != null && achievementManager.IsInitialized)
        {
            var achievementProgressData = achievementManager.GetProgressData();
            if (achievementProgressData != null)
            {
                data.achievementProgressJson = achievementProgressData.ToJson();
            }
        }
        else
        {
            // Fallback to PlayerPrefs to preserve existing achievement data
            data.achievementProgressJson = PlayerPrefs.GetString("AchievementProgressData", "");
        }

        // Get theme unlock status from ThemeManager
        // Fallback to PlayerPrefs if manager isn't available
        var themeManager = DependencyRegistry.Find<ThemeManager>();
        if (themeManager != null)
        {
            data.sunsetThemeUnlocked = themeManager.IsSunsetUnlocked;
            data.nightThemeUnlocked = themeManager.IsNightUnlocked;
        }
        else
        {
            // Fallback to PlayerPrefs to preserve existing theme unlocks
            data.sunsetThemeUnlocked = PlayerPrefs.GetInt("SunsetThemeUnlocked", 0) == 1;
            data.nightThemeUnlocked = PlayerPrefs.GetInt("NightThemeUnlocked", 0) == 1;
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

    #region Best Score Backfill

    /// <summary>
    /// Makes sure this account's leaderboard stats hold the player's saved bests. GameManager
    /// only submits a score that beats the saved best, so an account that never received one
    /// (a merged or newly linked account) would otherwise stay off the boards. Runs once per
    /// account per season, or again after a merge. Only raises stats, never lowers them.
    /// </summary>
    private void BackfillBestScores(PlayerProgressData data, bool force)
    {
        string doneKey = $"ScoresBackfilled_{playFabId}_S{LeaderboardSeason.Current}";
        if (!force && PlayerPrefs.GetInt(doneKey, 0) == 1) return;
        if (NetworkUtility.IsOffline()) return;

        var bests = new System.Collections.Generic.Dictionary<string, int>();
        int infinite = System.Math.Max(data.infiniteStackerHighScore, PlayerPrefs.GetInt("HighScore_InfiniteStacker", 0));
        if (infinite > 0)
        {
            bests["InfiniteStackerHighScores"] = infinite;
        }
        foreach (var kvp in data.levelHighScores)
        {
            int best = System.Math.Max(kvp.Value, PlayerPrefs.GetInt($"Level_{kvp.Key}_HighScore", 0));
            if (best > 0)
            {
                bests[$"StackerLevel_{kvp.Key}"] = best;
            }
        }

        if (bests.Count == 0)
        {
            PlayerPrefs.SetInt(doneKey, 1);
            PlayerPrefs.Save();
            return;
        }

        string accountId = playFabId;
        PlayFabProgressionAPI.GetStatistics(
            new PlayFab.ProgressionModels.GetStatisticsRequest { StatisticNames = new System.Collections.Generic.List<string>(bests.Keys) },
            result =>
            {
                if (accountId != playFabId) return; // Account changed mid-request

                var updates = new System.Collections.Generic.List<PlayFab.ProgressionModels.StatisticUpdate>();
                foreach (var kvp in bests)
                {
                    int onServer = 0;
                    if (result.Statistics != null && result.Statistics.TryGetValue(kvp.Key, out var stat) &&
                        stat.Scores != null && stat.Scores.Count > 0)
                    {
                        int.TryParse(stat.Scores[0], out onServer);
                    }
                    if (kvp.Value > onServer)
                    {
                        updates.Add(new PlayFab.ProgressionModels.StatisticUpdate
                        {
                            Name = kvp.Key,
                            Scores = new System.Collections.Generic.List<string> { kvp.Value.ToString() }
                        });
                    }
                }

                if (updates.Count == 0)
                {
                    PlayerPrefs.SetInt(doneKey, 1);
                    PlayerPrefs.Save();
                    return;
                }

                PlayFabProgressionAPI.UpdateStatistics(
                    new PlayFab.ProgressionModels.UpdateStatisticsRequest { Statistics = updates },
                    _ =>
                    {
                        Debug.Log($"[Score Backfill] Resubmitted {updates.Count} best score(s) to account {accountId}");
                        PlayerPrefs.SetInt(doneKey, 1);
                        PlayerPrefs.Save();
                    },
                    error => Debug.LogWarning($"[Score Backfill] Failed to submit bests: {error.GenerateErrorReport()}"));
            },
            error => Debug.LogWarning($"[Score Backfill] Failed to read stats: {error.GenerateErrorReport()}"));
    }

    #endregion

    #region Offline Score Sync

    /// <summary>
    /// Check if a PlayFab error is network-related
    /// </summary>
    /// <param name="error">PlayFab error to check</param>
    /// <returns>True if error is network-related, false otherwise</returns>
    private bool IsNetworkError(PlayFabError error)
    {
        if (error == null) return false;

        // Check for common network error codes
        // PlayFab error codes that indicate network issues
        string errorCode = error.Error.ToString();
        string errorMessage = error.ErrorMessage ?? "";
        string errorReport = error.GenerateErrorReport() ?? "";

        // Check for network-related error codes
        if (errorCode.Contains("ConnectionError") ||
            errorCode.Contains("Timeout") ||
            errorCode.Contains("NetworkError") ||
            errorCode.Contains("ServiceUnavailable") ||
            errorCode.Contains("HttpError"))
        {
            return true;
        }

        // Check error message for network-related keywords
        string lowerMessage = errorMessage.ToLower() + " " + errorReport.ToLower();
        if (lowerMessage.Contains("timeout") ||
            lowerMessage.Contains("connection") ||
            lowerMessage.Contains("network") ||
            lowerMessage.Contains("unreachable") ||
            lowerMessage.Contains("no internet") ||
            lowerMessage.Contains("offline"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sync queued scores from offline play to PlayFab
    /// Called after successful login when network is available
    /// </summary>
    private void SyncQueuedScores()
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Cannot sync queued scores - not logged into PlayFab");
            return;
        }

        if (NetworkUtility.IsOffline())
        {
            Debug.LogWarning("Cannot sync queued scores - device is offline");
            return;
        }

        var queuedScores = OfflineScoreQueue.GetQueuedScores();
        if (queuedScores == null || queuedScores.Count == 0)
        {
#if DEBUG_MODE
            Debug.Log("No queued scores to sync");
#endif
            return;
        }

        Debug.Log($"Syncing {queuedScores.Count} queued score(s) from offline play...");

        // Sync each queued score
        // Note: SubmitScore will handle integrity checks and display name
        // OnScoreSubmitSuccess will clear successfully synced scores from the queue
        // OnScoreSubmitFailure will re-queue scores if network error occurs
        foreach (var queuedScore in queuedScores)
        {
            SubmitScore(queuedScore.leaderboardName, queuedScore.score);
        }

        Debug.Log($"Initiated sync for {queuedScores.Count} queued score(s). Scores will be removed from queue on successful submission.");
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

    #region PlayFab Event Logging

    /// <summary>
    /// Logs a custom event to PlayFab for tracking account-related issues
    /// Events can be viewed in PlayFab Dashboard > Analytics > Event History
    /// </summary>
    /// <param name="eventName">Name of the event (e.g., "account_fallback_used")</param>
    /// <param name="eventData">Dictionary of event properties</param>
    private void LogPlayFabEvent(string eventName, System.Collections.Generic.Dictionary<string, object> eventData = null)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning($"[PlayFab Events] Cannot log event '{eventName}' - not logged in yet");
            return;
        }

        var request = new WriteClientPlayerEventRequest
        {
            EventName = eventName,
            Body = eventData ?? new System.Collections.Generic.Dictionary<string, object>()
        };

        PlayFabClientAPI.WritePlayerEvent(request,
            result =>
            {
#if DEBUG_MODE
                Debug.Log($"[PlayFab Events] Event '{eventName}' logged successfully");
#endif
            },
            error =>
            {
                Debug.LogWarning($"[PlayFab Events] Failed to log event '{eventName}': {error.GenerateErrorReport()}");
            });
    }

    /// <summary>
    /// Public entry point for gameplay/funnel analytics. Routed through the same
    /// WritePlayerEvent path as the account diagnostics above.
    ///
    /// Callers should use GameAnalytics rather than calling this directly — it queues
    /// events fired before login, which this method drops.
    /// </summary>
    public void LogAnalyticsEvent(string eventName, System.Collections.Generic.Dictionary<string, object> eventData = null)
    {
        LogPlayFabEvent(eventName, eventData);
    }

    /// <summary>
    /// Logs an account fallback event when Google login fails and Device ID is used
    /// </summary>
    private void LogAccountFallbackEvent(string reason, string googlePlayerId = null)
    {
        var eventData = new System.Collections.Generic.Dictionary<string, object>
        {
            { "reason", reason },
            { "device_id", SystemInfo.deviceUniqueIdentifier },
            { "timestamp", System.DateTime.UtcNow.ToString("o") }
        };

        if (!string.IsNullOrEmpty(googlePlayerId))
        {
            eventData["google_player_id"] = googlePlayerId;
        }

        string expectedId = PlayerPrefs.GetString(EXPECTED_PLAYFAB_ID_KEY, "");
        if (!string.IsNullOrEmpty(expectedId))
        {
            eventData["expected_playfab_id"] = expectedId;
        }

        LogPlayFabEvent("account_fallback_used", eventData);
    }

    /// <summary>
    /// Logs an account mismatch event when logged-in account differs from expected
    /// </summary>
    private void LogAccountMismatchEvent(string expectedId, string actualId, bool newlyCreated)
    {
        var eventData = new System.Collections.Generic.Dictionary<string, object>
        {
            { "expected_playfab_id", expectedId },
            { "actual_playfab_id", actualId },
            { "newly_created", newlyCreated },
            { "used_fallback", usedFallbackLogin },
            { "device_id", SystemInfo.deviceUniqueIdentifier },
            { "timestamp", System.DateTime.UtcNow.ToString("o") }
        };

        string lastGoogleId = PlayerPrefs.GetString(LAST_GOOGLE_PLAYER_ID_KEY, "");
        if (!string.IsNullOrEmpty(lastGoogleId))
        {
            eventData["last_google_player_id"] = lastGoogleId;
        }

        LogPlayFabEvent("account_mismatch_detected", eventData);
    }

    /// <summary>
    /// Logs a successful device linking event
    /// </summary>
    private void LogDeviceLinkingEvent(bool success, string errorMessage = null)
    {
        var eventData = new System.Collections.Generic.Dictionary<string, object>
        {
            { "success", success },
            { "device_id", SystemInfo.deviceUniqueIdentifier },
            { "timestamp", System.DateTime.UtcNow.ToString("o") }
        };

        if (!string.IsNullOrEmpty(errorMessage))
        {
            eventData["error"] = errorMessage;
        }

        LogPlayFabEvent("device_linking_attempt", eventData);
    }

    /// <summary>
    /// Logs a Google login failure event
    /// </summary>
    private void LogGoogleLoginFailureEvent(string errorMessage, string googlePlayerId = null)
    {
        var eventData = new System.Collections.Generic.Dictionary<string, object>
        {
            { "error", errorMessage },
            { "device_id", SystemInfo.deviceUniqueIdentifier },
            { "timestamp", System.DateTime.UtcNow.ToString("o") }
        };

        if (!string.IsNullOrEmpty(googlePlayerId))
        {
            eventData["google_player_id"] = googlePlayerId;
        }

        // Note: This event is logged after fallback login succeeds, 
        // so we can track what errors caused the fallback
        LogPlayFabEvent("google_login_failed", eventData);
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


