using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the leaderboard panel UI, displaying top scores and player position
/// Can be used for any leaderboard (InfiniteStacker or specific levels)
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Transform contentContainer; // ScrollView content container
    [SerializeField] private GameObject leaderboardEntryPrefab;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TextMeshProUGUI statusText; // Used for both "no data" and error messages
    [SerializeField] private Button closeButton;
    [SerializeField] private Button refreshButton;

    [Header("Mode Selection")]
    [SerializeField] private Button infiniteStackerModeButton;
    [SerializeField] private Button levelsModeButton;
    [SerializeField] private TextMeshProUGUI infiniteStackerModeButtonText;
    [SerializeField] private TextMeshProUGUI levelsModeButtonText;

    [Header("Mode Button Highlighting")]
    [SerializeField] private Color normalModeButtonColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color selectedModeButtonColor = new Color(1f, 0.9f, 0.6f, 1f);
    [SerializeField] private Color normalModeButtonTextColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color selectedModeButtonTextColor = new Color(1f, 0.8f, 0.4f, 1f);

    [Header("Navigation Buttons")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Settings")]
    [SerializeField] private int maxEntriesToShow = 20;
    [SerializeField] private bool showPlayerIfNotInTop = true;
    [SerializeField] private float leaderboardRefreshDelay = 0.5f; // Delay before refreshing when selecting a temple/level

    // State
    private List<GameObject> spawnedEntries = new List<GameObject>();
    private string currentLeaderboardName = "";
    private LeaderboardManager leaderboardManager;
    private LevelManager levelManager;

    // Navigation state
    private enum LeaderboardMode { InfiniteStacker, Levels }
    private LeaderboardMode currentMode = LeaderboardMode.InfiniteStacker;
    private int currentLevelNumber = 1; // For Levels mode
    private int totalLevels = 0;

    // Delay coroutine tracking
    private Coroutine pendingRefreshCoroutine = null;

    private void Awake()
    {
        // Set up button listeners
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshCurrentLeaderboard);
        }

        if (previousButton != null)
        {
            previousButton.onClick.AddListener(OnPreviousButtonClicked);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNextButtonClicked);
        }

        if (infiniteStackerModeButton != null)
        {
            infiniteStackerModeButton.onClick.AddListener(SwitchToInfiniteMode);
        }

        if (levelsModeButton != null)
        {
            levelsModeButton.onClick.AddListener(SwitchToLevelsMode);
        }
    }

    private void Start()
    {
        // Find managers
        leaderboardManager = DependencyRegistry.Find<LeaderboardManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();

        if (leaderboardManager == null)
        {
            Debug.LogError("LeaderboardPanel: LeaderboardManager not found!");
        }

        if (levelManager != null)
        {
            totalLevels = levelManager.TotalLevels;
        }

        // Note: Panel should start disabled in the scene hierarchy
        // Don't call SetActive(false) here to avoid timing issues
    }

    /// <summary>
    /// Ensure LeaderboardManager is available, attempt to find it if null
    /// </summary>
    /// <returns>True if LeaderboardManager is available, false otherwise</returns>
    private bool EnsureLeaderboardManager()
    {
        if (leaderboardManager == null)
        {
            leaderboardManager = DependencyRegistry.Find<LeaderboardManager>();

            if (leaderboardManager == null)
            {
                Debug.LogWarning("LeaderboardPanel: LeaderboardManager still not found, may not be initialized yet");
                return false;
            }

#if DEBUG_MODE
            Debug.Log("LeaderboardPanel: Successfully found LeaderboardManager");
#endif
        }

        return true;
    }

    /// <summary>
    /// Switch to Infinite Stacker mode
    /// </summary>
    private void SwitchToInfiniteMode()
    {
        currentMode = LeaderboardMode.InfiniteStacker;
        UpdateModeButtonHighlighting();
        UpdateNavigationVisibility();
        LoadInfiniteStackerLeaderboard();
    }

    /// <summary>
    /// Switch to Levels mode
    /// </summary>
    private void SwitchToLevelsMode()
    {
        currentMode = LeaderboardMode.Levels;
        currentLevelNumber = 1; // Start at level 1
        UpdateModeButtonHighlighting();
        UpdateNavigationVisibility();
        LoadCurrentLevelLeaderboardWithDelay();
    }

    /// <summary>
    /// Load the Infinite Stacker leaderboard
    /// </summary>
    private void LoadInfiniteStackerLeaderboard()
    {
        if (!EnsureLeaderboardManager())
        {
            ShowError("Leaderboard system not available");
            return;
        }

        string leaderboardName = leaderboardManager.GetInfiniteStackerLeaderboardName();
        LoadLeaderboard(leaderboardName, "Infinite Stacker");
    }

    /// <summary>
    /// Update navigation button visibility based on current mode
    /// </summary>
    private void UpdateNavigationVisibility()
    {
        bool showNavigation = (currentMode == LeaderboardMode.Levels);

        if (previousButton != null)
        {
            previousButton.gameObject.SetActive(showNavigation);
        }
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(showNavigation);
        }
    }

    /// <summary>
    /// Update mode button highlighting based on current mode
    /// </summary>
    private void UpdateModeButtonHighlighting()
    {
        bool isInfiniteSelected = (currentMode == LeaderboardMode.InfiniteStacker);
        bool isLevelsSelected = (currentMode == LeaderboardMode.Levels);

        // Update Infinite Stacker button
        if (infiniteStackerModeButton != null)
        {
            Image buttonImage = infiniteStackerModeButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isInfiniteSelected
                    ? selectedModeButtonColor
                    : normalModeButtonColor;
            }
        }

        // Update Infinite Stacker button text
        if (infiniteStackerModeButtonText != null)
        {
            infiniteStackerModeButtonText.color = isInfiniteSelected
                ? selectedModeButtonTextColor
                : normalModeButtonTextColor;
        }

        // Update Levels button
        if (levelsModeButton != null)
        {
            Image buttonImage = levelsModeButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isLevelsSelected
                    ? selectedModeButtonColor
                    : normalModeButtonColor;
            }
        }

        // Update Levels button text
        if (levelsModeButtonText != null)
        {
            levelsModeButtonText.color = isLevelsSelected
                ? selectedModeButtonTextColor
                : normalModeButtonTextColor;
        }
    }

    /// <summary>
    /// Navigate to previous level/leaderboard
    /// </summary>
    private void OnPreviousButtonClicked()
    {
        if (currentMode == LeaderboardMode.InfiniteStacker)
        {
            return; // No navigation in Infinite mode
        }

        // Loop backwards
        currentLevelNumber--;
        if (currentLevelNumber < 1)
        {
            // Loop to last level (respecting demo restrictions)
            currentLevelNumber = GetMaxAccessibleLevel();
        }

        LoadCurrentLevelLeaderboardWithDelay();
    }

    /// <summary>
    /// Navigate to next level/leaderboard
    /// </summary>
    private void OnNextButtonClicked()
    {
        if (currentMode == LeaderboardMode.InfiniteStacker)
        {
            return; // No navigation in Infinite mode
        }

        // Loop forwards
        currentLevelNumber++;
        int maxLevel = GetMaxAccessibleLevel();
        if (currentLevelNumber > maxLevel)
        {
            // Loop back to level 1
            currentLevelNumber = 1;
        }

        LoadCurrentLevelLeaderboardWithDelay();
    }

    /// <summary>
    /// Get the maximum accessible level (respecting demo mode)
    /// </summary>
    private int GetMaxAccessibleLevel()
    {
        if (EnsureLeaderboardManager())
        {
            return leaderboardManager.GetMaxAccessibleLevel();
        }
        return totalLevels;
    }

    /// <summary>
    /// Load the leaderboard for the current level number with delay
    /// </summary>
    private void LoadCurrentLevelLeaderboardWithDelay()
    {
        if (!EnsureLeaderboardManager())
        {
            ShowError("Leaderboard system not available");
            return;
        }

        // Get display name first (for title update)
        string displayName = $"Level {currentLevelNumber}";
        if (levelManager != null)
        {
            var levels = levelManager.GetAllLevels();
            var levelData = levels.Find(l => l.levelNumber == currentLevelNumber);
            if (levelData != null)
            {
                displayName = $"Level {levelData.levelNumber}\n{levelData.levelName}";
            }
        }

        // Update title immediately
        if (titleText != null)
        {
            titleText.text = displayName;
        }

        // Check if level is unlocked
        if (levelManager != null && !levelManager.IsLevelUnlocked(currentLevelNumber))
        {
            ClearEntries();
            ShowLoading(false);
            // Use ShowNoData pattern to display the message (same as "No scores yet")
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "Complete this level to unlock leaderboard";
            }
#if DEBUG_MODE
            Debug.Log($"Attempted to load leaderboard for locked level {currentLevelNumber}");
#endif
            return;
        }

        // Check if level is accessible
        if (!leaderboardManager.IsLevelAccessible(currentLevelNumber))
        {
            ShowError("This level is not available in demo mode");
            Debug.LogWarning($"Attempted to load leaderboard for inaccessible level {currentLevelNumber}");
            return;
        }

        string leaderboardName = leaderboardManager.GetStackerLevelLeaderboardName(currentLevelNumber);

        // Cancel any pending refresh
        if (pendingRefreshCoroutine != null)
        {
            StopCoroutine(pendingRefreshCoroutine);
            pendingRefreshCoroutine = null;
        }

        // Start delayed refresh
        pendingRefreshCoroutine = StartCoroutine(LoadLeaderboardWithDelay(leaderboardName, displayName, false));
    }

    /// <summary>
    /// Load the leaderboard for the current level number (immediate, used for initial load)
    /// </summary>
    private void LoadCurrentLevelLeaderboard()
    {
        if (!EnsureLeaderboardManager())
        {
            ShowError("Leaderboard system not available");
            return;
        }

        // Get display name first (for title update)
        string displayName = $"Level {currentLevelNumber}";
        if (levelManager != null)
        {
            var levels = levelManager.GetAllLevels();
            var levelData = levels.Find(l => l.levelNumber == currentLevelNumber);
            if (levelData != null)
            {
                displayName = $"Level {levelData.levelNumber}\n{levelData.levelName}";
            }
        }

        // Update title immediately
        if (titleText != null)
        {
            titleText.text = displayName;
        }

        // Check if level is unlocked
        if (levelManager != null && !levelManager.IsLevelUnlocked(currentLevelNumber))
        {
            ClearEntries();
            ShowLoading(false);
            // Use ShowNoData pattern to display the message (same as "No scores yet")
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "Complete this level to unlock leaderboard";
            }
#if DEBUG_MODE
            Debug.Log($"Attempted to load leaderboard for locked level {currentLevelNumber}");
#endif
            return;
        }

        // Check if level is accessible
        if (!leaderboardManager.IsLevelAccessible(currentLevelNumber))
        {
            ShowError("This level is not available in demo mode");
            Debug.LogWarning($"Attempted to load leaderboard for inaccessible level {currentLevelNumber}");
            return;
        }

        string leaderboardName = leaderboardManager.GetStackerLevelLeaderboardName(currentLevelNumber);
        LoadLeaderboard(leaderboardName, displayName);
    }

    /// <summary>
    /// Open the panel in Infinite Stacker mode
    /// </summary>
    public void OpenPanelInfiniteMode()
    {
        gameObject.SetActive(true);
        SwitchToInfiniteMode();
    }

    /// <summary>
    /// Open the panel in Levels mode
    /// </summary>
    public void OpenPanelLevelsMode()
    {
        gameObject.SetActive(true);
        SwitchToLevelsMode();
    }

    /// <summary>
    /// Open the panel and load a specific leaderboard (legacy method)
    /// </summary>
    public void OpenPanel(string leaderboardName, string displayTitle)
    {
        gameObject.SetActive(true);
        LoadLeaderboard(leaderboardName, displayTitle);
    }

    /// <summary>
    /// Close the panel
    /// </summary>
    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Load and display a specific leaderboard with optional delay
    /// </summary>
    /// <param name="leaderboardName">Name of the leaderboard</param>
    /// <param name="displayTitle">Title to display</param>
    /// <param name="forceRefresh">If true, bypasses cache and forces a refresh</param>
    public void LoadLeaderboard(string leaderboardName, string displayTitle, bool forceRefresh = false)
    {
        if (!EnsureLeaderboardManager())
        {
            ShowError("Leaderboard system not available");
            return;
        }

        currentLeaderboardName = leaderboardName;

        // Update title
        if (titleText != null)
        {
            titleText.text = displayTitle;
        }

        // Show loading state
        ShowLoading(true);
        ClearEntries();

        // Request leaderboard data (will use cache if available unless forceRefresh is true)
        leaderboardManager.GetLeaderboardWithPlayerPosition(
            leaderboardName,
            maxEntriesToShow,
            OnLeaderboardLoaded,
            OnLeaderboardError,
            forceRefresh
        );
    }

    /// <summary>
    /// Coroutine to load leaderboard with delay
    /// </summary>
    private IEnumerator LoadLeaderboardWithDelay(string leaderboardName, string displayTitle, bool forceRefresh)
    {
        // Update title immediately for better UX
        if (titleText != null)
        {
            titleText.text = displayTitle;
        }

        // Wait for the delay
        yield return new WaitForSeconds(leaderboardRefreshDelay);

        // Clear pending coroutine reference
        pendingRefreshCoroutine = null;

        // Now load the leaderboard
        LoadLeaderboard(leaderboardName, displayTitle, forceRefresh);
    }

    /// <summary>
    /// Refresh the current leaderboard (forces refresh, bypasses cache)
    /// </summary>
    private void RefreshCurrentLeaderboard()
    {
        if (!string.IsNullOrEmpty(currentLeaderboardName))
        {
            string displayTitle = titleText != null ? titleText.text.Replace(" Leaderboard", "") : "Leaderboard";
            // Force refresh when user explicitly clicks refresh button
            LoadLeaderboard(currentLeaderboardName, displayTitle, forceRefresh: true);
        }
    }

    /// <summary>
    /// Called when leaderboard data is successfully loaded
    /// </summary>
    private void OnLeaderboardLoaded(List<LeaderboardEntry> entries)
    {
        ShowLoading(false);

        if (entries == null || entries.Count == 0)
        {
            ShowNoData(true);
            return;
        }

        ShowNoData(false);
        DisplayEntries(entries);
    }

    /// <summary>
    /// Called when leaderboard loading fails
    /// </summary>
    private void OnLeaderboardError(string errorMessage)
    {
        ShowLoading(false);
        ShowError(errorMessage);
        Debug.LogError($"Leaderboard error: {errorMessage}");
    }

    /// <summary>
    /// Display the leaderboard entries in the UI
    /// </summary>
    private void DisplayEntries(List<LeaderboardEntry> entries)
    {
        ClearEntries();

        if (leaderboardEntryPrefab == null || contentContainer == null)
        {
            Debug.LogError("LeaderboardPanel: Missing prefab or container reference!");
            return;
        }

        foreach (var entry in entries)
        {
            GameObject entryObj = Instantiate(leaderboardEntryPrefab, contentContainer);
            LeaderboardEntryUI entryUI = entryObj.GetComponent<LeaderboardEntryUI>();

            if (entryUI != null)
            {
                entryUI.SetData(entry);
            }

            spawnedEntries.Add(entryObj);
        }
    }

    /// <summary>
    /// Clear all displayed entries
    /// </summary>
    private void ClearEntries()
    {
        foreach (var entry in spawnedEntries)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }
        spawnedEntries.Clear();
    }

    /// <summary>
    /// Show or hide the loading indicator
    /// </summary>
    private void ShowLoading(bool show)
    {
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(show);
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Show or hide the "no data" message
    /// </summary>
    private void ShowNoData(bool show)
    {
        if (statusText != null)
        {
            statusText.gameObject.SetActive(show);
            if (show)
            {
                statusText.text = "No scores yet. Be the first!";
            }
        }
    }

    /// <summary>
    /// Show an error message
    /// </summary>
    private void ShowError(string message)
    {
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = message;
        }
    }

    private void OnDestroy()
    {
        // Clean up button listeners
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveAllListeners();
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
        }

        if (infiniteStackerModeButton != null)
        {
            infiniteStackerModeButton.onClick.RemoveAllListeners();
        }

        if (levelsModeButton != null)
        {
            levelsModeButton.onClick.RemoveAllListeners();
        }

        ClearEntries();
    }
}

