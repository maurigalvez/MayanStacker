using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the main menu UI, including game mode selection and navigation to different menu panels
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject levelSelectionPanel;
    [SerializeField] private GameObject codexPanel;
    [SerializeField] private GameObject leaderboardPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button infiniteModeButton;
    [SerializeField] private Button levelModeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button codexButton;
    [SerializeField] private Button leaderboardButton;

    [Header("Settings Panel Buttons")]
    [SerializeField] private Button backFromSettingsButton;

    [Header("Credits Panel Buttons")]
    [SerializeField] private Button backFromCreditsButton;

    [Header("Codex Panel Buttons")]
    [SerializeField] private Button backFromCodexButton;

    [Header("Leaderboard Panel Buttons")]
    [SerializeField] private Button backFromLeaderboardButton;

    [Header("Level Selection")]
    [SerializeField] private Button backFromLevelSelectionButton;
    [SerializeField] private Button goToNextLevelButton;
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private Transform[] levelButtonContainers;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI versionText;

    [Header("Cloud Sync UI")]
    [SerializeField] private GameObject syncPanel;
    [SerializeField] private TextMeshProUGUI syncStatusText;
    [SerializeField] private float syncCompleteDisplayDuration = 1f;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string gameVersion = "1.0.0";

    // References
    private SettingsManager settingsManager;
    private LevelManager levelManager;
    private CodexManager codexManager;
    private MainMenuSoundManager soundManager;
    private LeaderboardPanel leaderboardPanelComponent;
    private PlayFabManager playFabManager;

    // Spawned UI elements
    private List<LevelButtonUI> spawnedLevelButtons = new List<LevelButtonUI>();

    // State
    private bool isFirstShow = true;

    /// <summary>
    /// Get a level button by index (for use by ScrollView_PinchScale)
    /// </summary>
    /// <param name="levelIndex">Zero-based level index</param>
    /// <returns>The LevelButtonUI component, or null if not found</returns>
    public LevelButtonUI GetLevelButton(int levelIndex)
    {
        if (levelIndex >= 0 && levelIndex < spawnedLevelButtons.Count)
        {
            return spawnedLevelButtons[levelIndex];
        }
        return null;
    }

    /// <summary>
    /// Get the count of spawned level buttons (for debugging)
    /// </summary>
    /// <returns>The number of spawned level buttons</returns>
    public int GetLevelButtonCount()
    {
        return spawnedLevelButtons.Count;
    }

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<MainMenuManager>(this);
    }

    private void Start()
    {
        // Find dependencies via DependencyRegistry
        settingsManager = DependencyRegistry.Find<SettingsManager>();
        if (settingsManager == null)
        {
            // Create settings manager if it doesn't exist
            GameObject settingsObj = new GameObject("SettingsManager");
            settingsManager = settingsObj.AddComponent<SettingsManager>();
        }

        levelManager = DependencyRegistry.Find<LevelManager>();
        codexManager = DependencyRegistry.Find<CodexManager>();
        soundManager = DependencyRegistry.Find<MainMenuSoundManager>();
        playFabManager = DependencyRegistry.Find<PlayFabManager>();

        // Subscribe to PlayFab sync events
        if (playFabManager != null)
        {
            playFabManager.OnLoginStarted += OnLoginStarted;
            playFabManager.OnSyncStarted += OnSyncStarted;
            playFabManager.OnProgressSynced += OnProgressSynced;
            playFabManager.OnProgressSyncFailed += OnProgressSyncFailed;
        }

        // Get leaderboard panel component
        if (leaderboardPanel != null)
        {
            leaderboardPanelComponent = leaderboardPanel.GetComponent<LeaderboardPanel>();
        }

        InitializeUI();
        SetupButtonListeners();
        ShowMainMenu();
    }

    private void InitializeUI()
    {
        // Hide sync panel initially
        if (syncPanel != null)
            syncPanel.SetActive(false);

        // Set version text
        if (versionText != null)
        {
            string versionDisplay = $"v{gameVersion}";

            // Add DEMO tag if in demo mode
            if (levelManager != null && levelManager.IsDemoVersion)
            {
                versionDisplay += " BETA";
            }

            versionText.text = versionDisplay;
        }

        // Initialize level selection buttons
        InitializeLevelSelection();
    }

    private void SetupButtonListeners()
    {
        // Main Menu Buttons
        if (infiniteModeButton != null)
            infiniteModeButton.onClick.AddListener(OnInfiniteModeSelected);

        if (levelModeButton != null)
            levelModeButton.onClick.AddListener(OnLevelModeSelected);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(ShowSettingsPanel);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(ShowCreditsPanel);

        if (codexButton != null)
            codexButton.onClick.AddListener(ShowCodexPanel);

        if (leaderboardButton != null)
            leaderboardButton.onClick.AddListener(ShowLeaderboardPanel);

        // Settings
        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.AddListener(ShowMainMenu);

        // Credits
        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.AddListener(ShowMainMenu);

        // Codex
        if (backFromCodexButton != null)
            backFromCodexButton.onClick.AddListener(ShowMainMenu);

        // Leaderboard
        if (backFromLeaderboardButton != null)
            backFromLeaderboardButton.onClick.AddListener(ShowMainMenu);

        // Level Selection
        if (backFromLevelSelectionButton != null)
            backFromLevelSelectionButton.onClick.AddListener(ShowMainMenu);

        if (goToNextLevelButton != null)
            goToNextLevelButton.onClick.AddListener(OnGoToNextLevelClicked);
    }

    private void InitializeLevelSelection()
    {
        // Clear any existing buttons
        ClearLevelButtons();

        // Validate requirements
        if (levelButtonPrefab == null)
        {
            Debug.LogError("Level button prefab is not assigned!");
            return;
        }

        if (levelButtonContainers == null || levelButtonContainers.Length == 0)
        {
            Debug.LogError("Level button container is not assigned!");
            return;
        }

        if (levelManager == null)
        {
            Debug.LogWarning("LevelManager not found. Cannot create level buttons.");
            return;
        }

        // Get total number of levels from LevelManager
        int totalLevels = levelManager.TotalLevels;

        // Spawn a button for each level
        for (int i = 0; i < totalLevels; i++)
        {
            SpawnLevelButton(i);
        }

        // Find and highlight the next playable level
        int nextPlayableLevelIndex = GetNextPlayableLevelIndex();
        if (nextPlayableLevelIndex >= 0 && nextPlayableLevelIndex < spawnedLevelButtons.Count)
        {
            // Apply pulse animation to the next playable level
            spawnedLevelButtons[nextPlayableLevelIndex].StartPulseAnimation();
        }

        // Update "Go to Next Level" button visibility/interactability
        if (goToNextLevelButton != null)
        {
            goToNextLevelButton.interactable = (nextPlayableLevelIndex >= 0);
        }

        Debug.Log($"Created {totalLevels} level buttons dynamically");
    }

    /// <summary>
    /// Spawn a single level button
    /// </summary>
    private void SpawnLevelButton(int levelIndex)
    {
        // Instantiate the button
        GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonContainers[levelIndex]);

        // Get the LevelButtonUI component
        LevelButtonUI levelButtonUI = buttonObj.GetComponent<LevelButtonUI>();
        if (levelButtonUI == null)
        {
            Debug.LogError($"Level button prefab is missing LevelButtonUI component!");
            Destroy(buttonObj);
            return;
        }

        // Get level data from LevelManager
        int levelNumber = levelIndex + 1; // Convert to 1-based

        // Note: LevelManager.IsLevelUnlocked uses levelNumber (1-based), not index
        bool isUnlocked = levelManager.IsLevelUnlocked(levelNumber);
        int stars = levelManager.GetLevelStars(levelNumber);

        // Initialize the button
        levelButtonUI.Initialize(levelIndex, levelNumber, isUnlocked, stars);

        // Add click listener
        levelButtonUI.AddClickListener(OnLevelButtonClicked);

        // Track the spawned button
        spawnedLevelButtons.Add(levelButtonUI);
    }

    /// <summary>
    /// Clear all spawned level buttons
    /// </summary>
    private void ClearLevelButtons()
    {
        foreach (var button in spawnedLevelButtons)
        {
            if (button != null)
            {
                button.StopPulseAnimation();
                button.ClearClickListeners();
                Destroy(button.gameObject);
            }
        }

        spawnedLevelButtons.Clear();
    }

    /// <summary>
    /// Find the next playable level (first unlocked level with 0 stars)
    /// </summary>
    /// <returns>Level index of the next playable level, or -1 if none found</returns>
    private int GetNextPlayableLevelIndex()
    {
        if (levelManager == null) return -1;

        int totalLevels = levelManager.TotalLevels;

        // Find the first unlocked level that hasn't been completed (0 stars)
        for (int i = 0; i < totalLevels; i++)
        {
            int levelNumber = i + 1; // Convert to 1-based
            bool isUnlocked = levelManager.IsLevelUnlocked(levelNumber);
            int stars = levelManager.GetLevelStars(levelNumber);

            if (isUnlocked && stars == 0)
            {
                return i; // Return the index (0-based)
            }
        }

        // If all levels are completed, return -1
        return -1;
    }


    /// <summary>
    /// Centers the map on the current level after a brief delay to ensure UI is laid out
    /// </summary>
    private System.Collections.IEnumerator CenterMapOnCurrentLevelDelayed()
    {
        // Wait for end of frame to ensure UI layout is complete
        yield return new WaitForSeconds(0.2f);

        // Try to find ScrollView_PinchScale with retries (in case it hasn't registered yet)
        ScrollView_PinchScale mapScrollView = null;
        int maxRetries = 10;
        int retryCount = 0;

        while (mapScrollView == null && retryCount < maxRetries)
        {
            mapScrollView = DependencyRegistry.Find<ScrollView_PinchScale>();
            if (mapScrollView == null)
            {
                retryCount++;
                yield return new WaitForEndOfFrame();
            }
        }

        if (mapScrollView != null)
        {
            Debug.Log($"MainMenuManager: Found ScrollView_PinchScale after {retryCount} retries");
            mapScrollView.CenterMapOnCurrentLevel();
        }
        else
        {
            Debug.LogWarning($"MainMenuManager: ScrollView_PinchScale not found via DependencyRegistry after {maxRetries} retries. Cannot center map on current level.");
        }
    }


    // Panel Navigation Methods
    private void ShowMainMenu()
    {
        // Don't play sounds on first initialization
        if (!isFirstShow)
        {
            soundManager?.PlayBackButton();
            soundManager?.PlayPanelClose();
        }
        else
        {
            isFirstShow = false;
        }

        SetActivePanel(mainMenuPanel);
    }

    private void ShowSettingsPanel()
    {
        soundManager?.PlayButtonClick();
        soundManager?.PlayPanelOpen();
        SetActivePanel(settingsPanel);

        // Initialize settings UI
        if (settingsManager != null)
        {
            settingsManager.InitializeSettingsUI();
        }
    }

    private void ShowCreditsPanel()
    {
        soundManager?.PlayButtonClick();
        soundManager?.PlayPanelOpen();
        SetActivePanel(creditsPanel);
    }

    private void ShowCodexPanel()
    {
        soundManager?.PlayButtonClick();
        soundManager?.PlayPanelOpen();
        SetActivePanel(codexPanel);

        // Initialize codex
        if (codexManager != null)
        {
            codexManager.ShowCodex();
        }
    }

    private void ShowLeaderboardPanel()
    {
        soundManager?.PlayButtonClick();
        soundManager?.PlayPanelOpen();
        SetActivePanel(leaderboardPanel);

        // Open the leaderboard panel with mode selection visible
        // Users can choose between Infinite Stacker or Levels mode
        if (leaderboardPanelComponent != null)
        {
            // Default to Infinite Stacker mode
            leaderboardPanelComponent.OpenPanelInfiniteMode();
        }
    }

    private void ShowLevelSelection()
    {
        soundManager?.PlayLevelModeSelect();
        soundManager?.PlayPanelOpen();
        SetActivePanel(levelSelectionPanel);
        InitializeLevelSelection(); // Refresh level states

        // Notify path renderer to update paths
        var pathRenderer = DependencyRegistry.Find<LevelPathRenderer>();
        if (pathRenderer != null)
        {
            pathRenderer.OnLevelSelectionShown();
        }

        // Center map on current level after a brief delay to ensure UI is laid out
        StartCoroutine(CenterMapOnCurrentLevelDelayed());
    }

    private void SetActivePanel(GameObject panelToShow)
    {
        // Hide all panels
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (codexPanel != null) codexPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);

        // Hide codex if switching away from it
        if (codexManager != null && panelToShow != codexPanel)
        {
            codexManager.HideCodex();
        }

        // Show the requested panel
        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }
    }

    // Button Click Handlers
    private void OnInfiniteModeSelected()
    {
        soundManager?.PlayInfiniteModeSelect();
        Debug.Log("Infinite Mode selected");
        SceneLoader.LoadGameScene(gameSceneName, GameMode.InfiniteStacker);
    }

    private void OnLevelModeSelected()
    {
        // Show level selection screen (sound is played in ShowLevelSelection)
        ShowLevelSelection();
    }

    private void OnLevelButtonClicked(int levelIndex)
    {
        soundManager?.PlayLevelButtonClick();
        Debug.Log($"Level {levelIndex + 1} selected");
        SceneLoader.LoadGameScene(gameSceneName, GameMode.StackerLevels, levelIndex);
    }

    private void OnGoToNextLevelClicked()
    {
        int nextLevelIndex = GetNextPlayableLevelIndex();

        if (nextLevelIndex >= 0)
        {
            soundManager?.PlayLevelButtonClick();
            Debug.Log($"Going to next playable level: {nextLevelIndex + 1}");
            SceneLoader.LoadGameScene(gameSceneName, GameMode.StackerLevels, nextLevelIndex);
        }
        else
        {
            soundManager?.PlayButtonClick();
            Debug.Log("All levels completed!");
        }
    }

    // Cloud Sync UI Methods

    /// <summary>
    /// Called when PlayFab login starts
    /// </summary>
    private void OnLoginStarted()
    {
        ShowSyncScreen("Authenticating...");
    }

    /// <summary>
    /// Called when cloud sync starts
    /// </summary>
    private void OnSyncStarted()
    {
        ShowSyncScreen("Syncing with cloud...");
    }

    /// <summary>
    /// Called when progress sync completes successfully
    /// </summary>
    private void OnProgressSynced(PlayerProgressData data)
    {
        // Get display name from PlayFabManager
        string displayName = playFabManager != null ? playFabManager.CurrentDisplayName : "";

        // Show success message with display name
        if (!string.IsNullOrEmpty(displayName))
        {
            ShowSyncScreen($"Sync complete!\nLogged in as: {displayName}");
        }
        else
        {
            ShowSyncScreen("Sync complete!");
        }

        StartCoroutine(HideSyncScreenAfterDelay());
    }

    /// <summary>
    /// Called when progress sync fails
    /// </summary>
    private void OnProgressSyncFailed(string error)
    {
        // Show offline message briefly, then hide
        ShowSyncScreen("Sync failed. Playing offline.");
        StartCoroutine(HideSyncScreenAfterDelay());
        Debug.LogWarning($"Cloud sync failed: {error}");
    }

    /// <summary>
    /// Shows the sync screen with a status message and blocks input
    /// </summary>
    private void ShowSyncScreen(string message)
    {
        if (syncPanel != null)
        {
            syncPanel.SetActive(true);
        }

        if (syncStatusText != null)
        {
            syncStatusText.text = message;
        }

        Debug.Log($"[Sync UI] {message}");
    }

    /// <summary>
    /// Hides the sync screen and re-enables input
    /// </summary>
    private void HideSyncScreen()
    {
        if (syncPanel != null)
        {
            syncPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Hides sync screen after a brief delay
    /// </summary>
    private System.Collections.IEnumerator HideSyncScreenAfterDelay()
    {
        yield return new WaitForSeconds(syncCompleteDisplayDuration);
        HideSyncScreen();
    }

    private void OnDestroy()
    {
        // Unsubscribe from PlayFab events
        if (playFabManager != null)
        {
            playFabManager.OnLoginStarted -= OnLoginStarted;
            playFabManager.OnSyncStarted -= OnSyncStarted;
            playFabManager.OnProgressSynced -= OnProgressSynced;
            playFabManager.OnProgressSyncFailed -= OnProgressSyncFailed;
        }

        // Unregister from dependency registry
        DependencyRegistry.Unregister<MainMenuManager>(this);

        // Clean up button listeners
        if (infiniteModeButton != null)
            infiniteModeButton.onClick.RemoveAllListeners();

        if (levelModeButton != null)
            levelModeButton.onClick.RemoveAllListeners();

        if (settingsButton != null)
            settingsButton.onClick.RemoveAllListeners();

        if (creditsButton != null)
            creditsButton.onClick.RemoveAllListeners();

        if (codexButton != null)
            codexButton.onClick.RemoveAllListeners();

        if (leaderboardButton != null)
            leaderboardButton.onClick.RemoveAllListeners();

        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.RemoveAllListeners();

        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.RemoveAllListeners();

        if (backFromCodexButton != null)
            backFromCodexButton.onClick.RemoveAllListeners();

        if (backFromLeaderboardButton != null)
            backFromLeaderboardButton.onClick.RemoveAllListeners();

        if (backFromLevelSelectionButton != null)
            backFromLevelSelectionButton.onClick.RemoveAllListeners();

        if (goToNextLevelButton != null)
            goToNextLevelButton.onClick.RemoveAllListeners();

        // Clean up spawned level buttons
        ClearLevelButtons();
    }
}

