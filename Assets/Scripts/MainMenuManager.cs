using System.Collections;
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
    [SerializeField] private GameObject achievementPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button infiniteModeButton;
    [SerializeField] private Button levelModeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button codexButton;
    [SerializeField] private Button leaderboardButton;
    [SerializeField] private Button achievementButton;

    [Header("Settings Panel Buttons")]
    [SerializeField] private Button backFromSettingsButton;

    [Header("Credits Panel Buttons")]
    [SerializeField] private Button backFromCreditsButton;

    [Header("Codex Panel Buttons")]
    [SerializeField] private Button backFromCodexButton;

    [Header("Leaderboard Panel Buttons")]
    [SerializeField] private Button backFromLeaderboardButton;

    [Header("Achievement Panel Buttons")]
    [SerializeField] private Button backFromAchievementButton;

    [Header("Level Selection")]
    [SerializeField] private Button backFromLevelSelectionButton;
    [SerializeField] private Button goToNextLevelButton;
    [SerializeField] private Button previousLevelButton;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button codexButtonFromLevelSelection;
    [SerializeField] private Button leaderboardButtonFromLevelSelection;
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private Transform[] levelButtonContainers;
    [SerializeField] private TextMeshProUGUI selectedLevelLabel;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI versionText;

    [Header("Cloud Sync UI")]
    [SerializeField] private GameObject syncPanel;
    [SerializeField] private TextMeshProUGUI syncStatusText;
    [SerializeField] private float syncCompleteDisplayDuration = 1f;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string gameVersion = "1.0.0";

    [Header("Theme Selection")]
    [SerializeField] private ThemeSelectionUI themeSelectionUI;

    // References
    private SettingsManager settingsManager;
    private LevelManager levelManager;
    private CodexManager codexManager;
    private MainMenuSoundManager soundManager;
    private LeaderboardPanel leaderboardPanelComponent;
    private TamalStacker.Achievements.AchievementPanelUI achievementPanelComponent;
    private PlayFabManager playFabManager;

    // Spawned UI elements
    private List<LevelButtonUI> spawnedLevelButtons = new List<LevelButtonUI>();

    // State
    private bool isFirstShow = true;
    private GameObject previousPanel = null; // Track previous panel for navigation
    private int currentSelectedLevelIndex = -1; // Track currently selected/focused level (0-based)

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

    /// <summary>
    /// Get the currently selected level index (0-based)
    /// </summary>
    /// <returns>The currently selected level index, or -1 if none selected</returns>
    public int GetCurrentSelectedLevelIndex()
    {
        return currentSelectedLevelIndex;
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

        // Get achievement panel component (it's on this GameObject, not the panel GameObject)
        achievementPanelComponent = GetComponent<TamalStacker.Achievements.AchievementPanelUI>();
        if (achievementPanelComponent == null)
        {
            Debug.LogWarning("MainMenuManager: AchievementPanelUI component not found on MainMenuManager GameObject!");
        }

        InitializeUI();
        SetupButtonListeners();
        ShowMainMenu();

        // Perform app startup integrity check
        PerformStartupIntegrityCheck();
    }

    /// <summary>
    /// Perform Standard Integrity check on app startup (one-time check per session)
    /// </summary>
    private void PerformStartupIntegrityCheck()
    {
        var integrityManager = DependencyRegistry.Find<IntegrityManager>();
        if (integrityManager != null && integrityManager.IsIntegrityChecksEnabled)
        {
            // Only perform if not already done
            if (!integrityManager.HasPerformedStartupCheck)
            {
                Debug.Log("[MainMenuManager] Performing app startup integrity check...");

                // Optionally show sync panel during check
                if (syncPanel != null && syncStatusText != null)
                {
                    syncPanel.SetActive(true);
                    syncStatusText.text = "Verifying app integrity...";
                }

                integrityManager.PerformStartupCheck((result) =>
                {
                    if (result.Success)
                    {
                        Debug.Log("[MainMenuManager] App startup integrity check passed.");

                        // Hide sync panel after short delay
                        if (syncPanel != null)
                        {
                            StartCoroutine(HideSyncPanelAfterDelay(syncCompleteDisplayDuration));
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[MainMenuManager] App startup integrity check failed: {result.ErrorMessage}");

                        // Hide sync panel on failure too (game continues)
                        if (syncPanel != null)
                        {
                            syncPanel.SetActive(false);
                        }
                    }
                });
            }
            else
            {
                Debug.Log("[MainMenuManager] Startup integrity check already performed this session.");
            }
        }
    }

    /// <summary>
    /// Coroutine to hide sync panel after a delay
    /// </summary>
    private IEnumerator HideSyncPanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (syncPanel != null)
        {
            syncPanel.SetActive(false);
        }
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
            codexButton.onClick.AddListener(OnCodexFromMainMenu);

        if (leaderboardButton != null)
            leaderboardButton.onClick.AddListener(OnLeaderboardFromMainMenu);

        if (achievementButton != null)
            achievementButton.onClick.AddListener(OnAchievementFromMainMenu);

        // Settings
        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.AddListener(ShowMainMenu);

        // Credits
        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.AddListener(ShowMainMenu);

        // Codex
        if (backFromCodexButton != null)
            backFromCodexButton.onClick.AddListener(OnBackFromCodex);

        // Leaderboard
        if (backFromLeaderboardButton != null)
            backFromLeaderboardButton.onClick.AddListener(OnBackFromLeaderboard);

        // Achievement
        if (backFromAchievementButton != null)
            backFromAchievementButton.onClick.AddListener(OnBackFromAchievement);

        // Level Selection
        if (backFromLevelSelectionButton != null)
            backFromLevelSelectionButton.onClick.AddListener(ShowMainMenu);

        // Level Selection - Codex and Leaderboard buttons
        if (codexButtonFromLevelSelection != null)
            codexButtonFromLevelSelection.onClick.AddListener(OnCodexFromLevelSelection);

        if (leaderboardButtonFromLevelSelection != null)
            leaderboardButtonFromLevelSelection.onClick.AddListener(OnLeaderboardFromLevelSelection);

        if (goToNextLevelButton != null)
            goToNextLevelButton.onClick.AddListener(OnGoToNextLevelClicked);

        // Level Navigation Buttons
        if (previousLevelButton != null)
            previousLevelButton.onClick.AddListener(OnPreviousLevelClicked);

        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
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
            // Set this as the currently selected level
            currentSelectedLevelIndex = nextPlayableLevelIndex;
        }
        else
        {
            // If no next playable level, find the last unlocked level
            int lastUnlockedIndex = GetLastUnlockedLevelIndex();
            if (lastUnlockedIndex >= 0)
            {
                currentSelectedLevelIndex = lastUnlockedIndex;
                if (lastUnlockedIndex < spawnedLevelButtons.Count)
                {
                    spawnedLevelButtons[lastUnlockedIndex].StartPulseAnimation();
                }
            }
            else
            {
                // Fallback to first level if none unlocked (shouldn't happen)
                currentSelectedLevelIndex = 0;
            }
        }

        // Update "Go to Next Level" button visibility/interactability
        if (goToNextLevelButton != null)
        {
            goToNextLevelButton.interactable = (nextPlayableLevelIndex >= 0);
        }

        // Update navigation buttons
        UpdateNavigationButtons();

        // Update level label
        UpdateSelectedLevelLabel();

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
    /// Get the last unlocked level index
    /// </summary>
    /// <returns>Level index of the last unlocked level, or -1 if none found</returns>
    private int GetLastUnlockedLevelIndex()
    {
        if (levelManager == null) return -1;

        int totalLevels = levelManager.TotalLevels;

        // Find the last unlocked level (highest level number that's unlocked)
        for (int i = totalLevels - 1; i >= 0; i--)
        {
            int levelNumber = i + 1; // Convert to 1-based
            bool isUnlocked = levelManager.IsLevelUnlocked(levelNumber);

            if (isUnlocked)
            {
                return i; // Return the index (0-based)
            }
        }

        return -1;
    }

    /// <summary>
    /// Get all unlocked level indices
    /// </summary>
    /// <returns>List of unlocked level indices (0-based), sorted by level number</returns>
    private List<int> GetUnlockedLevelIndices()
    {
        List<int> unlockedIndices = new List<int>();

        if (levelManager == null) return unlockedIndices;

        int totalLevels = levelManager.TotalLevels;

        for (int i = 0; i < totalLevels; i++)
        {
            int levelNumber = i + 1; // Convert to 1-based
            bool isUnlocked = levelManager.IsLevelUnlocked(levelNumber);

            if (isUnlocked)
            {
                unlockedIndices.Add(i);
            }
        }

        return unlockedIndices;
    }

    /// <summary>
    /// Get the previous unlocked level index (with circular navigation)
    /// </summary>
    /// <param name="currentIndex">Current level index (0-based)</param>
    /// <returns>Previous unlocked level index, or -1 if none found</returns>
    private int GetPreviousUnlockedLevelIndex(int currentIndex)
    {
        List<int> unlockedIndices = GetUnlockedLevelIndices();

        if (unlockedIndices.Count == 0) return -1;

        // Find current index in the unlocked list
        int currentPosition = unlockedIndices.IndexOf(currentIndex);

        if (currentPosition < 0)
        {
            // Current level is not in unlocked list, find the closest unlocked level
            // Find the first unlocked level that's less than currentIndex
            for (int i = unlockedIndices.Count - 1; i >= 0; i--)
            {
                if (unlockedIndices[i] < currentIndex)
                {
                    return unlockedIndices[i];
                }
            }
            // If none found before current, return the last unlocked (circular)
            return unlockedIndices[unlockedIndices.Count - 1];
        }

        // Circular navigation: if at first unlocked, go to last unlocked
        if (currentPosition == 0)
        {
            return unlockedIndices[unlockedIndices.Count - 1];
        }

        // Otherwise, return the previous unlocked level
        return unlockedIndices[currentPosition - 1];
    }

    /// <summary>
    /// Get the next unlocked level index (with circular navigation)
    /// </summary>
    /// <param name="currentIndex">Current level index (0-based)</param>
    /// <returns>Next unlocked level index, or -1 if none found</returns>
    private int GetNextUnlockedLevelIndex(int currentIndex)
    {
        List<int> unlockedIndices = GetUnlockedLevelIndices();

        if (unlockedIndices.Count == 0) return -1;

        // Find current index in the unlocked list
        int currentPosition = unlockedIndices.IndexOf(currentIndex);

        if (currentPosition < 0)
        {
            // Current level is not in unlocked list, find the closest unlocked level
            // Find the first unlocked level that's greater than currentIndex
            for (int i = 0; i < unlockedIndices.Count; i++)
            {
                if (unlockedIndices[i] > currentIndex)
                {
                    return unlockedIndices[i];
                }
            }
            // If none found after current, return the first unlocked (circular)
            return unlockedIndices[0];
        }

        // Circular navigation: if at last unlocked, go to first unlocked
        if (currentPosition == unlockedIndices.Count - 1)
        {
            return unlockedIndices[0];
        }

        // Otherwise, return the next unlocked level
        return unlockedIndices[currentPosition + 1];
    }

    /// <summary>
    /// Update navigation button states based on unlocked levels
    /// </summary>
    private void UpdateNavigationButtons()
    {
        if (previousLevelButton != null)
        {
            // Previous button should always be enabled if there are unlocked levels
            List<int> unlockedIndices = GetUnlockedLevelIndices();
            previousLevelButton.interactable = unlockedIndices.Count > 0;
        }

        if (nextLevelButton != null)
        {
            // Next button should always be enabled if there are unlocked levels
            List<int> unlockedIndices = GetUnlockedLevelIndices();
            nextLevelButton.interactable = unlockedIndices.Count > 0;
        }
    }

    /// <summary>
    /// Update the selected level label with level number and name
    /// </summary>
    private void UpdateSelectedLevelLabel()
    {
        if (selectedLevelLabel == null)
        {
            return;
        }

        if (currentSelectedLevelIndex < 0 || levelManager == null)
        {
            selectedLevelLabel.text = "";
            return;
        }

        // Get all levels from LevelManager
        List<LevelData> allLevels = levelManager.GetAllLevels();

        if (currentSelectedLevelIndex >= 0 && currentSelectedLevelIndex < allLevels.Count)
        {
            LevelData levelData = allLevels[currentSelectedLevelIndex];
            if (levelData != null)
            {
                int levelNumber = currentSelectedLevelIndex + 1; // Convert to 1-based
                selectedLevelLabel.text = $"Level {levelNumber}\n{levelData.levelName}";
            }
            else
            {
                selectedLevelLabel.text = "";
            }
        }
        else
        {
            selectedLevelLabel.text = "";
        }
    }

    /// <summary>
    /// Center map on a specific level index
    /// </summary>
    /// <param name="levelIndex">Level index to center on (0-based)</param>
    private void CenterMapOnLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= spawnedLevelButtons.Count)
        {
            Debug.LogWarning($"Cannot center map on invalid level index: {levelIndex}");
            return;
        }

        // Use LevelData position directly - no need to get the button rect
        StartCoroutine(CenterMapOnLevelDelayed(levelIndex));
    }

    /// <summary>
    /// Centers the map on a specific level after a brief delay to ensure UI is laid out
    /// Uses LevelData position for reliable centering
    /// </summary>
    private System.Collections.IEnumerator CenterMapOnLevelDelayed(int levelIndex)
    {
        // Wait for end of frame to ensure UI layout is complete
        yield return new WaitForEndOfFrame();

        // Force canvas update to ensure layout is calculated
        Canvas.ForceUpdateCanvases();

        // Wait a bit longer to ensure scaling calculations are complete
        yield return new WaitForSeconds(0.15f);

        // Force another canvas update after delay
        Canvas.ForceUpdateCanvases();

        // Try to find ScrollView_PinchScale with retries
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
            // Force canvas update one more time before centering
            Canvas.ForceUpdateCanvases();
            var levelButton = spawnedLevelButtons[levelIndex];
            // Use the new method with LevelData position (more reliable)
            mapScrollView.CenterOnRectTransform(levelButton.GetComponent<RectTransform>(), animate: true);
        }
        else
        {
            Debug.LogWarning($"ScrollView_PinchScale not found via DependencyRegistry after {maxRetries} retries.");
        }
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

        // Refresh main menu background theme to match current selection
        var themeBackground = DependencyRegistry.Find<ThemeMainMenuBackground>();
        if (themeBackground != null)
        {
            themeBackground.RefreshTheme();
        }
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

    private void ShowCodexPanel(GameObject returnToPanel = null)
    {
        soundManager?.PlayButtonClick();
        soundManager?.PlayPanelOpen();

        // Store the panel to return to (default to main menu if not specified)
        previousPanel = returnToPanel != null ? returnToPanel : mainMenuPanel;

        SetActivePanel(codexPanel);

        // Initialize codex
        if (codexManager != null)
        {
            codexManager.ShowCodex();
        }
    }

    private void ShowLeaderboardPanel(GameObject returnToPanel = null)
    {
        soundManager?.PlayButtonClick();
        soundManager?.PlayPanelOpen();

        // Store the panel to return to (default to main menu if not specified)
        previousPanel = returnToPanel != null ? returnToPanel : mainMenuPanel;

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

        // Enable theme selection UI
        if (themeSelectionUI != null)
        {
            themeSelectionUI.EnableThemeSelection();
        }

        // Refresh map theme sprite to match current selection
        var themeMapSprite = DependencyRegistry.Find<ThemeMapSprite>();
        if (themeMapSprite != null)
        {
            themeMapSprite.RefreshTheme();
        }

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
        // Check if we're switching away from codex
        bool isSwitchingFromCodex = codexPanel != null && codexPanel.activeSelf && panelToShow != codexPanel;

        // If switching from codex, show the target panel first (so there's no empty background)
        if (isSwitchingFromCodex && panelToShow != null)
        {
            panelToShow.SetActive(true);
        }

        // Disable theme selection UI if hiding level selection panel
        if (levelSelectionPanel != null && levelSelectionPanel != panelToShow && levelSelectionPanel.activeSelf)
        {
            if (themeSelectionUI != null)
            {
                themeSelectionUI.DisableThemeSelection();
            }
        }

        // Hide all panels (except codex if we're animating it, and the target panel if already shown)
        if (mainMenuPanel != null && mainMenuPanel != panelToShow) mainMenuPanel.SetActive(false);
        if (settingsPanel != null && settingsPanel != panelToShow) settingsPanel.SetActive(false);
        if (creditsPanel != null && creditsPanel != panelToShow) creditsPanel.SetActive(false);
        if (levelSelectionPanel != null && levelSelectionPanel != panelToShow) levelSelectionPanel.SetActive(false);
        if (leaderboardPanel != null && leaderboardPanel != panelToShow) leaderboardPanel.SetActive(false);
        if (achievementPanel != null && achievementPanel != panelToShow) achievementPanel.SetActive(false);

        // Hide codex if switching away from it (with animation)
        if (isSwitchingFromCodex && codexManager != null)
        {
            // Start scroll-down animation - codex panel will be hidden after animation completes
            codexManager.HideCodex();
        }
        else if (panelToShow != codexPanel)
        {
            // Not switching from codex and not switching to codex, hide it immediately
            if (codexPanel != null) codexPanel.SetActive(false);
        }

        // Show the requested panel (if not already shown)
        if (panelToShow != null && !panelToShow.activeSelf)
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

        // Update selected level index before loading
        currentSelectedLevelIndex = levelIndex;

        // Update level label
        UpdateSelectedLevelLabel();

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

    /// <summary>
    /// Handle previous level button click - navigates to previous unlocked level (circular)
    /// </summary>
    private void OnPreviousLevelClicked()
    {
        if (currentSelectedLevelIndex < 0)
        {
            Debug.LogWarning("No level currently selected!");
            return;
        }

        int previousIndex = GetPreviousUnlockedLevelIndex(currentSelectedLevelIndex);

        if (previousIndex >= 0 && previousIndex < spawnedLevelButtons.Count)
        {
            soundManager?.PlayLevelButtonClick();

            // Stop pulse animation on current level
            if (currentSelectedLevelIndex >= 0 && currentSelectedLevelIndex < spawnedLevelButtons.Count)
            {
                spawnedLevelButtons[currentSelectedLevelIndex].StopPulseAnimation();
            }

            // Update selected level
            currentSelectedLevelIndex = previousIndex;

            // Start pulse animation on new level
            spawnedLevelButtons[previousIndex].StartPulseAnimation();

            // Center map on the new level
            CenterMapOnLevel(previousIndex);

            // Update level label
            UpdateSelectedLevelLabel();

            Debug.Log($"Navigated to previous unlocked level: {previousIndex + 1}");
        }
        else
        {
            soundManager?.PlayButtonClick();
            Debug.LogWarning("No previous unlocked level found!");
        }
    }

    /// <summary>
    /// Handle next level button click - navigates to next unlocked level (circular)
    /// </summary>
    private void OnNextLevelClicked()
    {
        if (currentSelectedLevelIndex < 0)
        {
            Debug.LogWarning("No level currently selected!");
            return;
        }

        int nextIndex = GetNextUnlockedLevelIndex(currentSelectedLevelIndex);

        if (nextIndex >= 0 && nextIndex < spawnedLevelButtons.Count)
        {
            soundManager?.PlayLevelButtonClick();

            // Stop pulse animation on current level
            if (currentSelectedLevelIndex >= 0 && currentSelectedLevelIndex < spawnedLevelButtons.Count)
            {
                spawnedLevelButtons[currentSelectedLevelIndex].StopPulseAnimation();
            }

            // Update selected level
            currentSelectedLevelIndex = nextIndex;

            // Start pulse animation on new level
            spawnedLevelButtons[nextIndex].StartPulseAnimation();

            // Center map on the new level
            CenterMapOnLevel(nextIndex);

            // Update level label
            UpdateSelectedLevelLabel();

            Debug.Log($"Navigated to next unlocked level: {nextIndex + 1}");
        }
        else
        {
            soundManager?.PlayButtonClick();
            Debug.LogWarning("No next unlocked level found!");
        }
    }

    /// <summary>
    /// Handle codex button click from main menu
    /// </summary>
    private void OnCodexFromMainMenu()
    {
        ShowCodexPanel(mainMenuPanel);
    }

    /// <summary>
    /// Handle leaderboard button click from main menu
    /// </summary>
    private void OnLeaderboardFromMainMenu()
    {
        ShowLeaderboardPanel(mainMenuPanel);
    }

    /// <summary>
    /// Handle codex button click from level selection
    /// </summary>
    private void OnCodexFromLevelSelection()
    {
        ShowCodexPanel(levelSelectionPanel);
    }

    /// <summary>
    /// Handle leaderboard button click from level selection
    /// </summary>
    private void OnLeaderboardFromLevelSelection()
    {
        ShowLeaderboardPanel(levelSelectionPanel);
    }

    /// <summary>
    /// Handle back button from codex - returns to previous panel
    /// </summary>
    private void OnBackFromCodex()
    {
        soundManager?.PlayBackButton();
        soundManager?.PlayPanelClose();

        // Return to the previous panel (main menu or level selection)
        if (previousPanel != null)
        {
            if (previousPanel == levelSelectionPanel)
            {
                ShowLevelSelection();
            }
            else
            {
                ShowMainMenu();
            }
        }
        else
        {
            // Fallback to main menu if previous panel is not set
            ShowMainMenu();
        }
    }

    /// <summary>
    /// Handle back button from leaderboard - returns to previous panel
    /// </summary>
    private void OnBackFromLeaderboard()
    {
        soundManager?.PlayBackButton();
        soundManager?.PlayPanelClose();

        // Return to the previous panel (main menu or level selection)
        if (previousPanel != null)
        {
            if (previousPanel == levelSelectionPanel)
            {
                ShowLevelSelection();
            }
            else
            {
                ShowMainMenu();
            }
        }
        else
        {
            // Fallback to main menu if previous panel is not set
            ShowMainMenu();
        }
    }

    /// <summary>
    /// Handle achievement button click from main menu
    /// </summary>
    private void OnAchievementFromMainMenu()
    {
        ShowAchievementPanel(mainMenuPanel);
    }

    /// <summary>
    /// Show the achievement panel
    /// </summary>
    private void ShowAchievementPanel(GameObject returnToPanel = null)
    {
        soundManager?.PlayButtonClick();
        soundManager?.PlayPanelOpen();

        // Store the panel to return to (default to main menu if not specified)
        previousPanel = returnToPanel != null ? returnToPanel : mainMenuPanel;

        SetActivePanel(achievementPanel);

        // Initialize/refresh achievement panel
        if (achievementPanelComponent != null)
        {
            achievementPanelComponent.ShowPanel();
        }
    }

    /// <summary>
    /// Handle back button from achievement - returns to previous panel
    /// </summary>
    private void OnBackFromAchievement()
    {
        soundManager?.PlayBackButton();
        soundManager?.PlayPanelClose();

        // Return to the previous panel (main menu or level selection)
        if (previousPanel != null)
        {
            if (previousPanel == levelSelectionPanel)
            {
                ShowLevelSelection();
            }
            else
            {
                ShowMainMenu();
            }
        }
        else
        {
            // Fallback to main menu if previous panel is not set
            ShowMainMenu();
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

        if (achievementButton != null)
            achievementButton.onClick.RemoveAllListeners();

        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.RemoveAllListeners();

        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.RemoveAllListeners();

        if (backFromCodexButton != null)
            backFromCodexButton.onClick.RemoveAllListeners();

        if (backFromLeaderboardButton != null)
            backFromLeaderboardButton.onClick.RemoveAllListeners();

        if (backFromAchievementButton != null)
            backFromAchievementButton.onClick.RemoveAllListeners();

        if (backFromLevelSelectionButton != null)
            backFromLevelSelectionButton.onClick.RemoveAllListeners();

        if (codexButtonFromLevelSelection != null)
            codexButtonFromLevelSelection.onClick.RemoveAllListeners();

        if (leaderboardButtonFromLevelSelection != null)
            leaderboardButtonFromLevelSelection.onClick.RemoveAllListeners();

        if (goToNextLevelButton != null)
            goToNextLevelButton.onClick.RemoveAllListeners();

        if (previousLevelButton != null)
            previousLevelButton.onClick.RemoveAllListeners();

        if (nextLevelButton != null)
            nextLevelButton.onClick.RemoveAllListeners();

        // Clean up spawned level buttons
        ClearLevelButtons();
    }
}

