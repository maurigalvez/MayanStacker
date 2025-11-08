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

    [Header("Navigation Buttons")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Settings")]
    [SerializeField] private int maxEntriesToShow = 20;
    [SerializeField] private bool showPlayerIfNotInTop = true;

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

            Debug.Log("LeaderboardPanel: Successfully found LeaderboardManager");
        }

        return true;
    }

    /// <summary>
    /// Switch to Infinite Stacker mode
    /// </summary>
    private void SwitchToInfiniteMode()
    {
        currentMode = LeaderboardMode.InfiniteStacker;
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
        UpdateNavigationVisibility();
        LoadCurrentLevelLeaderboard();
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

        LoadCurrentLevelLeaderboard();
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

        LoadCurrentLevelLeaderboard();
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
    /// Load the leaderboard for the current level number
    /// </summary>
    private void LoadCurrentLevelLeaderboard()
    {
        if (!EnsureLeaderboardManager())
        {
            ShowError("Leaderboard system not available");
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
        string displayName = $"Level {currentLevelNumber}";

        // Get the actual level name if available
        if (levelManager != null)
        {
            var levels = levelManager.GetAllLevels();
            var levelData = levels.Find(l => l.levelNumber == currentLevelNumber);
            if (levelData != null)
            {
                displayName = levelData.levelName;
            }
        }

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
    /// Load and display a specific leaderboard
    /// </summary>
    public void LoadLeaderboard(string leaderboardName, string displayTitle)
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

        // Request leaderboard data
        leaderboardManager.GetLeaderboardWithPlayerPosition(
            leaderboardName,
            maxEntriesToShow,
            OnLeaderboardLoaded,
            OnLeaderboardError
        );
    }

    /// <summary>
    /// Refresh the current leaderboard
    /// </summary>
    private void RefreshCurrentLeaderboard()
    {
        if (!string.IsNullOrEmpty(currentLeaderboardName))
        {
            string displayTitle = titleText != null ? titleText.text.Replace(" Leaderboard", "") : "Leaderboard";
            LoadLeaderboard(currentLeaderboardName, displayTitle);
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

