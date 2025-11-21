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
    [Tooltip("Show ad every N game overs (1 = every game, 2 = every other game, etc.)")]
    [SerializeField] private int adFrequency = 2;
    [Tooltip("Delay in seconds before showing ad after game over/level complete")]
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

        // Initialize Google Mobile Ads SDK
        InitializeMobileAds();
    }

    /// <summary>
    /// Called when a new scene is loaded
    /// Re-finds dependencies since they might be new instances in the new scene
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DebugLog($"Scene loaded: {scene.name}, refreshing dependencies...");
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
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= OnLevelCompleted;
        }

        // Find new references
        gameManager = DependencyRegistry.Find<GameManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();

        // Subscribe to game events with new references
        if (gameManager != null)
        {
            gameManager.OnGameOver += OnGameOver;
            DebugLog("Subscribed to GameManager.OnGameOver");
        }
        else
        {
            DebugLog("GameManager not found (may not exist in this scene)");
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted += OnLevelCompleted;
            DebugLog("Subscribed to LevelManager.OnLevelCompleted");
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
        interstitialAd.Show();
    }

    /// <summary>
    /// Called when game over event is triggered (Infinite Mode)
    /// </summary>
    private void OnGameOver()
    {
        gameOverCount++;
        Debug.Log($"[AdManager] 🎮 Game Over #{gameOverCount} triggered");

        // Check if we should show an ad based on frequency
        if (gameOverCount % adFrequency == 0)
        {
            Debug.Log($"[AdManager] ✅ Ad frequency check passed! Will show ad after {adShowDelay}s delay");
            StartCoroutine(ShowAdAfterDelay());
        }
        else
        {
            int nextAdAt = gameOverCount + (adFrequency - (gameOverCount % adFrequency));
            Debug.Log($"[AdManager] ⏭️ Skipping ad (count: {gameOverCount}, frequency: {adFrequency}). Next ad at game over #{nextAdAt}");
        }
    }

    /// <summary>
    /// Called when level complete event is triggered (Level Mode)
    /// </summary>
    private void OnLevelCompleted(int stars, int score, bool showCodexPopup)
    {
        gameOverCount++;
        Debug.Log($"[AdManager] 🏆 Level Complete #{gameOverCount} triggered (stars: {stars}, score: {score})");

        // Check if we should show an ad based on frequency
        if (gameOverCount % adFrequency == 0)
        {
            Debug.Log($"[AdManager] ✅ Ad frequency check passed! Will show ad after {adShowDelay}s delay");
            StartCoroutine(ShowAdAfterDelay());
        }
        else
        {
            int nextAdAt = gameOverCount + (adFrequency - (gameOverCount % adFrequency));
            Debug.Log($"[AdManager] ⏭️ Skipping ad (count: {gameOverCount}, frequency: {adFrequency}). Next ad at level #{nextAdAt}");
        }
    }

    /// <summary>
    /// Show ad after a small delay to avoid interrupting UI animations
    /// </summary>
    private IEnumerator ShowAdAfterDelay()
    {
        Debug.Log($"[AdManager] ⏳ Waiting {adShowDelay} seconds before showing ad...");
        yield return new WaitForSeconds(adShowDelay);
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

        // Unsubscribe from game events
        if (gameManager != null)
        {
            gameManager.OnGameOver -= OnGameOver;
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= OnLevelCompleted;
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

