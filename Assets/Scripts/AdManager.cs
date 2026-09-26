using System.Collections;
using GoogleMobileAds.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages Google Mobile Ads integration for the game
/// Handles loading and showing interstitial ads after gameplay sessions
/// </summary>
public class AdManager : MonoBehaviour
{
    [Header("Ad Configuration")]
    [SerializeField] private bool useTestAds = true;
    [Tooltip("Your AdMob App ID (set in AndroidManifest.xml)")]
    [SerializeField] private string admobAppId = "ca-app-pub-3940256099942544~3347511713"; // Test App ID

    [Header("Interstitial Ad Units")]
    [Tooltip("Android Interstitial Ad Unit ID")]
    [SerializeField] private string androidAdUnitId = "ca-app-pub-3940256099942544/1033173712"; // Test Ad Unit
    [Tooltip("Production Android Ad Unit ID (used when useTestAds is false)")]
    [SerializeField] private string productionAndroidAdUnitId = "";

    [Header("Ad Frequency")]
    [Tooltip("Show ad every N losses (1 = every loss, 2 = every other loss, etc.). Temple wins never count.")]
    [SerializeField] private int adFrequency = 2;
    [Tooltip("Delay in seconds before showing ad after a loss")]
    [SerializeField] private float adShowDelay = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Private state
    private InterstitialAd interstitialAd;
    private bool isAdLoaded = false;
    private bool isAdLoading = false;
    private bool isMobileAdsInitialized = false;
    private int gameOverCount = 0;
    private string currentAdUnitId;

    // The ad waits adShowDelay after a loss so the result card can land. If the player starts
    // the next run inside that window, the pending ad is cancelled (it must never open over a
    // live run) and owed to the next loss instead, so tapping fast doesn't skip it.
    private Coroutine pendingAdRoutine;
    private bool adOwed = false;

    // References
    private GameManager gameManager;
    private LevelManager levelManager;

    private void Awake()
    {
        var existingAdManager = DependencyRegistry.Find<AdManager>();
        if (existingAdManager != null)
        {
            Destroy(gameObject);
            return;
        }

        // Register with dependency registry
        DependencyRegistry.Register<AdManager>(this);

        // Persist across scenes
        DontDestroyOnLoad(gameObject);

        // Determine which ad unit to use
        currentAdUnitId = useTestAds ? androidAdUnitId : productionAndroidAdUnitId;

        if (string.IsNullOrEmpty(productionAndroidAdUnitId) && !useTestAds)
        {
            Debug.LogWarning("AdManager: Production Ad Unit ID is not set! Using test ads instead.");
            currentAdUnitId = androidAdUnitId;
        }
    }

    private void Start()
    {
        // Subscribe to scene loaded events to refresh dependencies when scenes change
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Find initial dependencies
        FindDependencies();

        RemoveAds.Changed += OnRemoveAdsChanged;

        // A Remove Ads owner never starts the ads SDK at all — no ad requests, and none of
        // its memory or network cost.
        if (RemoveAds.Owned)
        {
            DebugLog("Remove Ads owned — skipping Mobile Ads initialization.");
            return;
        }

        // Initialize Google Mobile Ads SDK
        InitializeMobileAds();
    }

    /// <summary>
    /// Ownership can arrive mid-session: a purchase, or Play restoring one shortly after a
    /// reinstall. Drop the loaded ad on grant; start the SDK if a refund takes it away.
    /// </summary>
    private void OnRemoveAdsChanged(bool owned)
    {
        if (owned)
        {
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }
            isAdLoaded = false;
            StopAllCoroutines();
            pendingAdRoutine = null;
            adOwed = false;
            return;
        }

        if (isMobileAdsInitialized) LoadInterstitialAd();
        else InitializeMobileAds();
    }

    /// <summary>
    /// Called when a new scene is loaded
    /// Re-finds dependencies since they might be new instances in the new scene
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DebugLog($"Scene loaded: {scene.name}, refreshing dependencies...");
        // Next Level / Try Again can reload the scene; an ad queued by the last run must not
        // open on top of the new one.
        CancelPendingAd();
        FindDependencies();
    }

    /// <summary>
    /// Find or refresh manager dependencies
    /// This is called on Start and whenever a new scene loads
    /// </summary>
    private void FindDependencies()
    {
        // Unsubscribe from old references if they exist
        if (gameManager != null)
        {
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnGameStart -= CancelPendingAd;
            gameManager.OnGameRestart -= CancelPendingAd;
        }

        if (levelManager != null)
        {
            levelManager.OnLevelFailed -= OnLevelFailed;
        }

        // Find new references
        gameManager = DependencyRegistry.Find<GameManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();

        // Subscribe to game events with new references
        if (gameManager != null)
        {
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameStart += CancelPendingAd;
            gameManager.OnGameRestart += CancelPendingAd;
            DebugLog("Subscribed to GameManager.OnGameOver");
        }
        else
        {
            DebugLog("GameManager not found (may not exist in this scene)");
        }

        if (levelManager != null)
        {
            levelManager.OnLevelFailed += OnLevelFailed;
            DebugLog("Subscribed to LevelManager.OnLevelFailed");
        }
        else
        {
            DebugLog("LevelManager not found (may not exist in this scene)");
        }
    }

    /// <summary>
    /// Initialize the Google Mobile Ads SDK
    /// </summary>
    private void InitializeMobileAds()
    {
        if (isMobileAdsInitialized)
        {
            DebugLog("Mobile Ads already initialized");
            return;
        }

        DebugLog("Initializing Google Mobile Ads SDK...");

        // The SDK raises its callbacks on a Java thread by default, where StartCoroutine and
        // the UI the Remove Ads nudge opens are not allowed. Have it post them to Unity's.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        // Initialize the SDK
        MobileAds.Initialize(initStatus =>
        {
            isMobileAdsInitialized = true;
            DebugLog($"Mobile Ads SDK initialized. Status: {initStatus}");

            // Load the first interstitial ad
            LoadInterstitialAd();
        });
    }

    /// <summary>
    /// Load an interstitial ad
    /// </summary>
    private void LoadInterstitialAd()
    {
        if (RemoveAds.Owned) return;

        if (!isMobileAdsInitialized)
        {
            Debug.LogWarning("AdManager: Cannot load ad - SDK not initialized yet");
            return;
        }

        if (isAdLoading)
        {
            DebugLog("Ad is already loading, skipping...");
            return;
        }

        if (isAdLoaded)
        {
            DebugLog("Ad is already loaded, skipping...");
            return;
        }

        // Clean up any existing ad
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        isAdLoading = true;
        DebugLog($"Loading interstitial ad with unit ID: {currentAdUnitId}");

        // Create ad request
        AdRequest adRequest = new AdRequest();

        // Load the ad
        InterstitialAd.Load(currentAdUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            isAdLoading = false;

            if (error != null || ad == null)
            {
                Debug.LogError($"AdManager: Failed to load interstitial ad. Error: {error}");
                isAdLoaded = false;

                // Retry loading after a delay
                StartCoroutine(RetryLoadAdAfterDelay(5f));
                return;
            }

            DebugLog("Interstitial ad loaded successfully!");
            interstitialAd = ad;
            isAdLoaded = true;

            // Register ad event callbacks
            RegisterAdCallbacks();
        });
    }

    /// <summary>
    /// Register callbacks for ad events
    /// </summary>
    private void RegisterAdCallbacks()
    {
        if (interstitialAd == null) return;

        // Raised when the ad is shown
        interstitialAd.OnAdFullScreenContentOpened += () =>
        {
            DebugLog("Interstitial ad opened (full screen)");
        };

        // Raised when the ad closed
        interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            DebugLog("Interstitial ad closed");

            // Clean up
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }

            isAdLoaded = false;

            // Counts the ad and, when due, shows the Remove Ads nudge on the result card
            // the player is returning to.
            RemoveAdsOffer.NotifyInterstitialClosed();

            // Load the next ad
            LoadInterstitialAd();
        };

        // Raised when the ad failed to open
        interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"AdManager: Interstitial ad failed to show. Error: {error}");

            // Clean up
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }

            isAdLoaded = false;

            // Retry loading
            LoadInterstitialAd();
        };
    }

    /// <summary>
    /// Retry loading ad after a delay
    /// </summary>
    private IEnumerator RetryLoadAdAfterDelay(float delay)
    {
        DebugLog($"Retrying ad load in {delay} seconds...");
        yield return new WaitForSeconds(delay);
        LoadInterstitialAd();
    }

    /// <summary>
    /// Show the interstitial ad if loaded
    /// </summary>
    private void ShowInterstitialAd()
    {
        if (RemoveAds.Owned)
        {
            DebugLog("Remove Ads owned — not showing an interstitial.");
            return;
        }

        // FTUE grace period. A new player has no reason to tolerate a full-screen ad
        // before the game has shown them anything worth staying for, so interstitials
        // wait until they've finished a temple, played a few runs, and come back.
        if (!FtueState.AdsAllowed)
        {
            string reason = FtueState.AdSuppressionReason;
            Debug.Log($"[AdManager] 🛡️ Ad suppressed by FTUE grace period (reason: {reason}, " +
                      $"runs: {FtueState.LifetimeRuns}, session: {FtueState.SessionNumber})");
            GameAnalytics.AdSuppressed(reason, FtueState.LifetimeRuns);
            return;
        }

        if (!isAdLoaded || interstitialAd == null)
        {
            Debug.LogWarning("[AdManager] ⚠️ Cannot show ad - Ad not loaded yet!");

            // Try loading again if not already loading
            if (!isAdLoading)
            {
                DebugLog("Attempting to load ad now...");
                LoadInterstitialAd();
            }
            return;
        }

        Debug.Log("[AdManager] 🎬 Showing interstitial ad now!");
        GameAnalytics.AdShown("interstitial_game_over", FtueState.LifetimeRuns);
        interstitialAd.Show();
    }

    /// <summary>
    /// Game over in Infinite / Daily. Level mode goes through OnLevelFailed instead, because
    /// there GameOver also fires when a temple is won by falling after reaching its goal.
    /// </summary>
    private void OnGameOver()
    {
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.StackerLevels) return;
        RegisterLoss("Game Over");
    }

    /// <summary>
    /// A temple attempt that ended short of its goal. Temple wins never show or count toward
    /// an ad: the win is the moment the player should be left to enjoy.
    /// </summary>
    private void OnLevelFailed()
    {
        if (gameManager == null || gameManager.CurrentGameMode != GameMode.StackerLevels) return;
        RegisterLoss("Level Failed");
    }

    private void RegisterLoss(string label)
    {
        gameOverCount++;
        Debug.Log($"[AdManager] 🎮 {label} #{gameOverCount} triggered");

        if (adOwed)
        {
            Debug.Log($"[AdManager] ✅ Showing the ad owed from a cancelled slot after {adShowDelay}s delay");
            ScheduleAd();
            return;
        }

        // Check if we should show an ad based on frequency
        if (gameOverCount % adFrequency == 0)
        {
            Debug.Log($"[AdManager] ✅ Ad frequency check passed! Will show ad after {adShowDelay}s delay");
            ScheduleAd();
        }
        else
        {
            int nextAdAt = gameOverCount + (adFrequency - (gameOverCount % adFrequency));
            Debug.Log($"[AdManager] ⏭️ Skipping ad (count: {gameOverCount}, frequency: {adFrequency}). Next ad at loss #{nextAdAt}");
        }
    }

    private void ScheduleAd()
    {
        if (pendingAdRoutine != null) StopCoroutine(pendingAdRoutine);
        adOwed = true; // cleared only once the delay runs out with no run in progress
        pendingAdRoutine = StartCoroutine(ShowAdAfterDelay());
    }

    /// <summary>
    /// A new run started (or the scene changed) while an ad was still waiting out its delay.
    /// </summary>
    private void CancelPendingAd()
    {
        if (pendingAdRoutine == null) return;

        StopCoroutine(pendingAdRoutine);
        pendingAdRoutine = null;
        Debug.Log("[AdManager] ⏹️ New run started before the ad delay ended — ad moved to the next loss.");
    }

    /// <summary>
    /// Show ad after a small delay to avoid interrupting UI animations
    /// </summary>
    private IEnumerator ShowAdAfterDelay()
    {
        Debug.Log($"[AdManager] ⏳ Waiting {adShowDelay} seconds before showing ad...");
        yield return new WaitForSeconds(adShowDelay);
        pendingAdRoutine = null;

        // A run that began without raising OnGameStart must not get an ad over it either.
        if (gameManager != null && gameManager.IsGameActive)
        {
            Debug.Log("[AdManager] ⏹️ A run is live — ad moved to the next loss.");
            yield break;
        }

        adOwed = false;
        ShowInterstitialAd();
    }

    /// <summary>
    /// Debug logging helper
    /// </summary>
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AdManager] {message}");
        }
    }

    /// <summary>
    /// Public method to manually trigger ad loading (useful for testing)
    /// </summary>
    public void LoadAd()
    {
        LoadInterstitialAd();
    }

    /// <summary>
    /// Public method to manually show ad (useful for testing)
    /// </summary>
    public void ShowAd()
    {
        ShowInterstitialAd();
    }

    /// <summary>
    /// Check if an ad is ready to show
    /// </summary>
    public bool IsAdReady()
    {
        return isAdLoaded && interstitialAd != null;
    }

    private void OnDestroy()
    {
        // Unsubscribe from scene loaded events
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RemoveAds.Changed -= OnRemoveAdsChanged;

        // Unsubscribe from game events
        if (gameManager != null)
        {
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnGameStart -= CancelPendingAd;
            gameManager.OnGameRestart -= CancelPendingAd;
        }

        if (levelManager != null)
        {
            levelManager.OnLevelFailed -= OnLevelFailed;
        }

        // Clean up ad
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        // Unregister from dependency registry
        DependencyRegistry.Unregister<AdManager>(this);
    }
}

