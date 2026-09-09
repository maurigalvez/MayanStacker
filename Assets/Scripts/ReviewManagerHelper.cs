using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANDROID
using Google.Play.Review;
#endif

/// <summary>
/// Google Play In-App Review, on two routes.
///
/// Automatic, once ever, and only off the back of a genuinely good moment: a new personal
/// best in Infinite Stacker, or a three-star temple clear once the player has finished at
/// least <see cref="minCompletedLevelsForReview"/> of them. It deliberately does NOT fire on
/// a plain collapse — asking someone what they think of the game in the second after it beat
/// them is how a 5-star player leaves 2 stars.
///
/// Manual, any time, from the "Rate this game" button in Settings
/// (<see cref="RequestReviewFromPlayer"/>). That route ignores the once-ever latch, because
/// a player who taps a rate button has asked for something and must get it.
///
/// Google's in-app flow is quota-limited and returns silently when it declines to show
/// anything — sideloaded builds, an account that already reviewed, too many recent prompts.
/// The manual route therefore falls back to opening the store listing, so the button is
/// never a no-op.
/// </summary>
public class ReviewManagerHelper : MonoBehaviour
{
    [Header("Review Settings")]
    [SerializeField] private bool enableReviewPrompt = true;
    [SerializeField] private int minCompletedLevelsForReview = 2;
    [Tooltip("Stars required on a temple clear before the automatic prompt may fire. 3 = only a perfect clear counts as a good moment.")]
    [SerializeField] private int minStarsForReview = 3;

    // References
    private GameManager gameManager;
    private LevelManager levelManager;

    // State
    private bool isReviewShown = false;
    private const string REVIEW_PROMPT_SHOWN_KEY = "ReviewPromptShown";

#if UNITY_ANDROID
    private ReviewManager playReviewManager;
    private bool isReviewManagerInitialized = false;
    private bool isReviewManagerInitializing = false;
#endif

    private void Awake()
    {
        // Check if instance already exists
        var existingInstance = DependencyRegistry.Find<ReviewManagerHelper>();
        if (existingInstance != null && existingInstance != this)
        {
            Debug.LogWarning("ReviewManagerHelper instance already exists. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        // Register with dependency registry
        DependencyRegistry.Register<ReviewManagerHelper>(this);

        // Persist across scenes
        DontDestroyOnLoad(gameObject);

        // Load review shown status
        isReviewShown = PlayerPrefs.GetInt(REVIEW_PROMPT_SHOWN_KEY, 0) == 1;
#if DEBUG
        Debug.Log($"[ReviewManager] Loaded review status from PlayerPrefs: isReviewShown = {isReviewShown}");
#endif

        // Subscribe to scene loading events to refresh dependencies
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Refresh dependencies and subscribe to events
        RefreshDependencies();

#if UNITY_ANDROID
        // Try to initialize ReviewManager in Start() - gives native libraries more time to load
        // If it fails, we'll retry later when actually needed (lazy initialization fallback)
        InitializeReviewManager();
#endif
    }

    /// <summary>
    /// Called when a new scene is loaded
    /// Refreshes dependencies since managers may have been recreated
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshDependencies();
    }

    /// <summary>
    /// Refreshes dependencies and re-subscribes to events
    /// Call this when switching scenes or if managers are recreated
    /// </summary>
    private void RefreshDependencies()
    {
        // Unsubscribe from old references if they exist
        UnsubscribeFromEvents();

        // Find new references
        gameManager = DependencyRegistry.Find<GameManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();

#if DEBUG
        Debug.Log($"[ReviewManager] RefreshDependencies - GameManager: {(gameManager != null ? "Found" : "Not Found")}, LevelManager: {(levelManager != null ? "Found" : "Not Found")}");
#endif

        // Subscribe to new references
        SubscribeToEvents();
    }

    /// <summary>
    /// Subscribe to game events
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
    /// Unsubscribe from game events
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

    /// <summary>
    /// Called when game over event is triggered
    /// </summary>
    private void OnGameOver()
    {
#if DEBUG
        Debug.Log($"[ReviewManager] OnGameOver called - enableReviewPrompt: {enableReviewPrompt}, isReviewShown: {isReviewShown}");
#endif

        if (!enableReviewPrompt || isReviewShown)
        {
#if DEBUG
            Debug.Log($"[ReviewManager] OnGameOver - Not showing review: enableReviewPrompt={enableReviewPrompt}, isReviewShown={isReviewShown}");
#endif
            return;
        }

        // Only show review for Infinite Stacker mode, and only when the run that just ended
        // was a personal best. Every other game-over is a loss, and a loss is the worst
        // possible moment to ask someone to rate the game.
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.InfiniteStacker)
        {
            if (!IsNewPersonalBest())
            {
#if DEBUG
                Debug.Log($"[ReviewManager] OnGameOver - Infinite Stacker, but not a personal best ({gameManager.CurrentScore} vs {gameManager.HighScore}); staying quiet");
#endif
                return;
            }

#if DEBUG
            Debug.Log("[ReviewManager] OnGameOver - Infinite Stacker personal best, calling CheckAndShowReview");
#endif
            CheckAndShowReview("infinite_personal_best");
        }
        else
        {
#if DEBUG
            string mode = gameManager != null ? gameManager.CurrentGameMode.ToString() : "null";
            Debug.Log($"[ReviewManager] OnGameOver - Not Infinite Stacker mode (current mode: {mode})");
#endif
        }
    }

    /// <summary>
    /// Called when a level is completed
    /// </summary>
    private void OnLevelCompleted(int stars, int score, bool showCodexPopup)
    {
#if DEBUG
        Debug.Log($"[ReviewManager] OnLevelCompleted called - enableReviewPrompt: {enableReviewPrompt}, isReviewShown: {isReviewShown}");
#endif

        if (!enableReviewPrompt || isReviewShown)
        {
#if DEBUG
            Debug.Log($"[ReviewManager] OnLevelCompleted - Not showing review: enableReviewPrompt={enableReviewPrompt}, isReviewShown={isReviewShown}");
#endif
            return;
        }

        // Only show review for Stacker Levels mode
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.StackerLevels)
        {
            // A scraped-through one-star clear is not a good moment. Wait for a perfect one.
            if (stars < minStarsForReview)
            {
#if DEBUG
                Debug.Log($"[ReviewManager] OnLevelCompleted - {stars} star(s), need {minStarsForReview}; staying quiet");
#endif
                return;
            }

            // Check if at least 2 levels are completed
            int completedLevelCount = GetCompletedLevelCount();
#if DEBUG
            Debug.Log($"[ReviewManager] OnLevelCompleted - Stacker Levels mode, completed levels: {completedLevelCount}/{minCompletedLevelsForReview}");
#endif
            if (completedLevelCount >= minCompletedLevelsForReview)
            {
#if DEBUG
                Debug.Log("[ReviewManager] OnLevelCompleted - Enough levels completed, calling CheckAndShowReview");
#endif
                CheckAndShowReview("temple_three_star");
            }
            else
            {
#if DEBUG
                Debug.Log($"[ReviewManager] OnLevelCompleted - Not enough levels completed ({completedLevelCount} < {minCompletedLevelsForReview})");
#endif
            }
        }
        else
        {
#if DEBUG
            string mode = gameManager != null ? gameManager.CurrentGameMode.ToString() : "null";
            Debug.Log($"[ReviewManager] OnLevelCompleted - Not Stacker Levels mode (current mode: {mode})");
#endif
        }
    }

    /// <summary>
    /// Main method to check conditions and show review if appropriate
    /// </summary>
    public void CheckAndShowReview(string source = "auto")
    {
#if DEBUG
        Debug.Log($"[ReviewManager] CheckAndShowReview called (source: {source})");
#endif

        if (!ShouldShowReview())
        {
#if DEBUG
            Debug.Log("[ReviewManager] CheckAndShowReview - ShouldShowReview returned false, aborting");
#endif
            return;
        }

#if DEBUG
        Debug.Log("[ReviewManager] CheckAndShowReview - Proceeding to mark review as shown and request flow");
#endif

        // Mark as shown immediately to prevent multiple prompts
        MarkReviewShown();

        // Request and show review. The automatic route never falls back to the store page:
        // if Play declines to show the sheet, the player asked for nothing and gets nothing.
        StartCoroutine(RequestAndShowReview(source, allowStoreFallback: false));
    }

    /// <summary>
    /// The "Rate this game" button in Settings. Unlike the automatic prompt this ignores
    /// every gate — the once-ever latch, the mode, the score — because the player pressed a
    /// button and something has to happen. If Play's in-app sheet won't appear, the store
    /// listing opens instead.
    /// </summary>
    public void RequestReviewFromPlayer()
    {
        // A deliberate rating also retires the automatic prompt: having asked once, the game
        // shouldn't ambush them again mid-run.
        if (!isReviewShown) MarkReviewShown();

        StartCoroutine(RequestAndShowReview("settings_button", allowStoreFallback: true));
    }

    /// <summary>
    /// True when the run that just ended set a personal best. Matches the condition UIManager
    /// uses for its "NEW HIGH SCORE!" banner, so the prompt only ever follows a moment the
    /// player has actually been congratulated for.
    /// </summary>
    private bool IsNewPersonalBest()
    {
        if (gameManager == null) return false;
        return gameManager.CurrentScore > 0 && gameManager.CurrentScore >= gameManager.HighScore;
    }

    /// <summary>
    /// Opens the Play Store listing for this build. The https form is used rather than
    /// market://, because Play intercepts it on any device that has the store and it still
    /// resolves in a browser on any device that doesn't.
    /// </summary>
    private static void OpenStoreListing()
    {
        string url = $"https://play.google.com/store/apps/details?id={Application.identifier}";
        Debug.Log($"[ReviewManager] Falling back to the store listing: {url}");
        Application.OpenURL(url);
    }

    /// <summary>
    /// Check if review should be shown
    /// </summary>
    private bool ShouldShowReview()
    {
#if DEBUG
        Debug.Log($"[ReviewManager] ShouldShowReview - Checking conditions...");
#endif

        if (!enableReviewPrompt)
        {
#if DEBUG
            Debug.Log("[ReviewManager] ShouldShowReview - Review prompt is disabled");
#endif
            return false;
        }

        if (isReviewShown)
        {
#if DEBUG
            Debug.Log("[ReviewManager] ShouldShowReview - Review has already been shown");
#endif
            return false;
        }

        // Only works on Android
        if (Application.platform != RuntimePlatform.Android)
        {
#if DEBUG
            Debug.Log($"[ReviewManager] ShouldShowReview - Not on Android platform (current: {Application.platform})");
#endif
            return false;
        }

#if DEBUG
        Debug.Log("[ReviewManager] ShouldShowReview - All conditions met, returning true");
#endif
        return true;
    }

    /// <summary>
    /// Mark that review has been shown
    /// </summary>
    private void MarkReviewShown()
    {
#if DEBUG
        Debug.Log($"[ReviewManager] MarkReviewShown - Changing isReviewShown from {isReviewShown} to true");
#endif
        isReviewShown = true;
        PlayerPrefs.SetInt(REVIEW_PROMPT_SHOWN_KEY, 1);
        PlayerPrefs.Save();
#if DEBUG
        Debug.Log("[ReviewManager] MarkReviewShown - Review prompt marked as shown and saved to PlayerPrefs");
#else
        Debug.Log("Review prompt marked as shown");
#endif
    }

    /// <summary>
    /// Get the count of completed levels (levels with stars > 0)
    /// </summary>
    private int GetCompletedLevelCount()
    {
        if (levelManager == null)
        {
#if DEBUG
            Debug.Log("[ReviewManager] GetCompletedLevelCount - LevelManager is null, returning 0");
#endif
            return 0;
        }

        int completedCount = 0;
        var allLevels = levelManager.GetAllLevels();

        foreach (var level in allLevels)
        {
            int stars = levelManager.GetLevelStars(level.levelNumber);
            if (stars > 0)
            {
                completedCount++;
            }
        }

#if DEBUG
        Debug.Log($"[ReviewManager] GetCompletedLevelCount - Total levels: {allLevels.Count}, Completed: {completedCount}");
#endif

        return completedCount;
    }

    /// <summary>
    /// Initialize the ReviewManager if not already initialized
    /// Returns true if initialization was successful
    /// </summary>
#if UNITY_ANDROID
    private bool InitializeReviewManager()
    {
        // If already initialized, return success
        if (isReviewManagerInitialized && playReviewManager != null)
        {
            return true;
        }

        // If currently initializing, wait
        if (isReviewManagerInitializing)
        {
            return false;
        }

        // Try to initialize
        try
        {
            isReviewManagerInitializing = true;
            playReviewManager = new ReviewManager();
            isReviewManagerInitialized = true;
            isReviewManagerInitializing = false;
            Debug.Log("Google Play Review Manager initialized successfully");
            return true;
        }
        catch (System.Exception e)
        {
            isReviewManagerInitializing = false;
            Debug.LogWarning($"Failed to initialize Google Play Review Manager: {e.Message}");
            playReviewManager = null;
            isReviewManagerInitialized = false;
            return false;
        }
    }
#endif

    /// <summary>
    /// Request and show the review flow asynchronously
    /// </summary>
    private IEnumerator RequestAndShowReview(string source, bool allowStoreFallback)
    {
#if UNITY_ANDROID
#if DEBUG
        Debug.Log($"[ReviewManager] RequestAndShowReview - Starting coroutine (isReviewManagerInitialized: {isReviewManagerInitialized})");
#endif

        // Initialize ReviewManager if needed (lazy initialization fallback)
        // If Start() initialization failed, try again with retries
        if (!isReviewManagerInitialized || playReviewManager == null)
        {
#if DEBUG
            Debug.Log("[ReviewManager] RequestAndShowReview - Review Manager not initialized, attempting initialization with retries");
#endif
            int maxRetries = 3;
            int retryCount = 0;
            bool initialized = false;

            while (retryCount < maxRetries && !initialized)
            {
                initialized = InitializeReviewManager();
                if (!initialized)
                {
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        Debug.Log($"Google Play Review Manager initialization failed, retrying in 0.5 seconds... (Attempt {retryCount + 1}/{maxRetries})");
                        yield return new WaitForSeconds(0.5f);
                    }
                }
            }

            if (!initialized || playReviewManager == null)
            {
                Debug.LogWarning("Google Play Review Manager could not be initialized after retries. Review prompt will not be shown.");
                GameAnalytics.ReviewPrompt(source, false);
                if (allowStoreFallback) OpenStoreListing();
                yield break;
            }
        }

        Debug.Log("Requesting Google Play Review flow...");

        // Request review flow
        var requestFlowOperation = playReviewManager.RequestReviewFlow();
        yield return requestFlowOperation;

        if (requestFlowOperation.Error != ReviewErrorCode.NoError)
        {
            Debug.LogWarning($"Error requesting review flow: {requestFlowOperation.Error}");
            GameAnalytics.ReviewPrompt(source, false);
            if (allowStoreFallback) OpenStoreListing();
            yield break;
        }

        var playReviewInfo = requestFlowOperation.GetResult();
        if (playReviewInfo == null)
        {
            Debug.LogWarning("Review flow request returned null");
            GameAnalytics.ReviewPrompt(source, false);
            if (allowStoreFallback) OpenStoreListing();
            yield break;
        }

#if DEBUG
        Debug.Log("[ReviewManager] RequestAndShowReview - Review request successful, launching flow");
#endif
        Debug.Log("Launching Google Play Review flow...");

        // Launch review flow
        var launchFlowOperation = playReviewManager.LaunchReviewFlow(playReviewInfo);
        yield return launchFlowOperation;

        if (launchFlowOperation.Error != ReviewErrorCode.NoError)
        {
            Debug.LogWarning($"Error launching review flow: {launchFlowOperation.Error}");
            GameAnalytics.ReviewPrompt(source, false);
            if (allowStoreFallback) OpenStoreListing();
            yield break;
        }

        // Note: Play returns success whether or not it actually drew the sheet, and never
        // tells us whether a review was left. "launched" here means "Play accepted the
        // request", which is the most this API will ever say.
        GameAnalytics.ReviewPrompt(source, true);
        Debug.Log("Google Play Review flow completed successfully");
#else
        Debug.Log("Google Play Review is only available on Android platform");
        GameAnalytics.ReviewPrompt(source, false);
        if (allowStoreFallback) OpenStoreListing();
        yield break;
#endif
    }

    private void OnDestroy()
    {
        // Unsubscribe from scene loading events
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Unregister from dependency registry
        DependencyRegistry.Unregister<ReviewManagerHelper>(this);

        // Unsubscribe from events
        UnsubscribeFromEvents();
    }
}

