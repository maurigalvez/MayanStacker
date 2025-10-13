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

    [Header("Main Menu Buttons")]
    [SerializeField] private Button infiniteModeButton;
    [SerializeField] private Button levelModeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;

    [Header("Settings Panel Buttons")]
    [SerializeField] private Button backFromSettingsButton;

    [Header("Credits Panel Buttons")]
    [SerializeField] private Button backFromCreditsButton;

    [Header("Level Selection Buttons")]
    [SerializeField] private Button backFromLevelSelectionButton;
    [SerializeField] private Button[] levelButtons;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI versionText;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string gameVersion = "1.0.0";

    // References
    private SettingsManager settingsManager;
    private LevelManager levelManager;

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

        InitializeUI();
        SetupButtonListeners();
        ShowMainMenu();
    }

    private void InitializeUI()
    {
        // Set version text
        if (versionText != null)
        {
            versionText.text = $"v{gameVersion}";
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

        // Settings
        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.AddListener(ShowMainMenu);

        // Credits
        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.AddListener(ShowMainMenu);

        // Level Selection
        if (backFromLevelSelectionButton != null)
            backFromLevelSelectionButton.onClick.AddListener(ShowMainMenu);
    }

    private void InitializeLevelSelection()
    {
        if (levelButtons == null || levelButtons.Length == 0) return;

        // Use level manager reference from Start()
        // Set up level buttons
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;

            int levelIndex = i; // Capture for lambda
            levelButtons[i].onClick.AddListener(() => OnLevelButtonClicked(levelIndex));

            // Check if level is unlocked
            bool isUnlocked = levelManager != null && levelManager.IsLevelUnlocked(levelIndex);
            levelButtons[i].interactable = isUnlocked;

            // Update button appearance based on unlock status
            UpdateLevelButtonAppearance(levelButtons[i], levelIndex, isUnlocked);
        }
    }

    private void UpdateLevelButtonAppearance(Button levelButton, int levelIndex, bool isUnlocked)
    {
        // Get the button's text component
        TextMeshProUGUI buttonText = levelButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            if (isUnlocked)
            {
                buttonText.text = $"Level {levelIndex + 1}";

                // Show stars if level was completed
                if (levelManager != null)
                {
                    int stars = levelManager.GetLevelStars(levelIndex);
                    if (stars > 0)
                    {
                        string starString = new string('★', stars);
                        buttonText.text = $"Level {levelIndex + 1}\n{starString}";
                    }
                }
            }
            else
            {
                buttonText.text = "🔒";
            }
        }

        // Optional: Change button color based on status
        ColorBlock colors = levelButton.colors;
        if (!isUnlocked)
        {
            colors.normalColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.highlightedColor = colors.normalColor;
            colors.pressedColor = colors.normalColor;
            levelButton.colors = colors;
        }
    }

    // Panel Navigation Methods
    private void ShowMainMenu()
    {
        SetActivePanel(mainMenuPanel);
    }

    private void ShowSettingsPanel()
    {
        SetActivePanel(settingsPanel);

        // Initialize settings UI
        if (settingsManager != null)
        {
            settingsManager.InitializeSettingsUI();
        }
    }

    private void ShowCreditsPanel()
    {
        SetActivePanel(creditsPanel);
    }

    private void ShowLevelSelection()
    {
        SetActivePanel(levelSelectionPanel);
        InitializeLevelSelection(); // Refresh level states
    }

    private void SetActivePanel(GameObject panelToShow)
    {
        // Hide all panels
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);

        // Show the requested panel
        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }
    }

    // Button Click Handlers
    private void OnInfiniteModeSelected()
    {
        Debug.Log("Infinite Mode selected");
        SceneLoader.LoadGameScene(gameSceneName, GameMode.InfiniteStacker);
    }

    private void OnLevelModeSelected()
    {
        // Show level selection screen
        ShowLevelSelection();
    }

    private void OnLevelButtonClicked(int levelIndex)
    {
        Debug.Log($"Level {levelIndex + 1} selected");
        SceneLoader.LoadGameScene(gameSceneName, GameMode.StackerLevels, levelIndex);
    }

    private void OnDestroy()
    {
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

        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.RemoveAllListeners();

        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.RemoveAllListeners();

        if (backFromLevelSelectionButton != null)
            backFromLevelSelectionButton.onClick.RemoveAllListeners();

        if (levelButtons != null)
        {
            foreach (var button in levelButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
        }
    }
}

