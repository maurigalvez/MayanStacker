using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the theme selection UI in the level selection panel
/// Handles theme buttons, unlock info panel, and confirmation panel
/// </summary>
public class ThemeSelectionUI : MonoBehaviour
{
    [Header("Theme Buttons")]
    [SerializeField] private Button dayThemeButton;
    [SerializeField] private Button sunsetThemeButton;
    [SerializeField] private Button nightThemeButton;

    [Header("Button Sprites")]
    [SerializeField] private Sprite sunsetThemeLockedSprite;
    [SerializeField] private Sprite sunsetThemeUnlockedSprite;
    [SerializeField] private Sprite nightThemeLockedSprite;
    [SerializeField] private Sprite nightThemeUnlockedSprite;

    [Header("Unlock Info Panel")]
    [SerializeField] private GameObject unlockInfoPanel;
    [SerializeField] private TextMeshProUGUI unlockInfoTitle;
    [SerializeField] private TextMeshProUGUI unlockInfoDescription;
    [SerializeField] private Button unlockInfoCloseButton;

    [Header("Confirmation Panel")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private TextMeshProUGUI confirmationTitle;
    [SerializeField] private TextMeshProUGUI confirmationMessage;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Button Visual States")]
    [SerializeField] private Color lockedButtonColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color unlockedButtonColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color selectedButtonColor = new Color(1f, 0.9f, 0.6f, 1f);

    // References
    private ThemeManager themeManager;
    private MainMenuSoundManager soundManager;

    // State
    private GameTheme pendingThemeSelection = GameTheme.Day;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<ThemeSelectionUI>(this);
    }

    private void Start()
    {
        // Find dependencies
        themeManager = DependencyRegistry.Find<ThemeManager>();
        soundManager = DependencyRegistry.Find<MainMenuSoundManager>();

        // Subscribe to theme events
        if (themeManager != null)
        {
            themeManager.OnThemeChanged += OnThemeChanged;
            themeManager.OnThemeUnlocked += OnThemeUnlocked;
        }

        // Setup button listeners
        SetupButtonListeners();

        // Initialize UI state
        UpdateThemeButtonStates();

        // Hide panels initially
        if (unlockInfoPanel != null)
            unlockInfoPanel.SetActive(false);
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
    }

    private void SetupButtonListeners()
    {
        // Theme buttons
        if (dayThemeButton != null)
            dayThemeButton.onClick.AddListener(() => OnThemeButtonClicked(GameTheme.Day));

        if (sunsetThemeButton != null)
            sunsetThemeButton.onClick.AddListener(() => OnThemeButtonClicked(GameTheme.Sunset));

        if (nightThemeButton != null)
            nightThemeButton.onClick.AddListener(() => OnThemeButtonClicked(GameTheme.Night));

        // Unlock info panel
        if (unlockInfoCloseButton != null)
            unlockInfoCloseButton.onClick.AddListener(HideUnlockInfo);

        // Confirmation panel
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmThemeSelection);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(HideConfirmation);
    }

    /// <summary>
    /// Update button visual states based on unlock status and current selection
    /// </summary>
    private void UpdateThemeButtonStates()
    {
        if (themeManager == null) return;

        GameTheme selectedTheme = themeManager.GetSelectedTheme();

        // Update Day button (always unlocked, no sprite changes)
        if (dayThemeButton != null)
        {
            UpdateButtonVisualState(dayThemeButton, GameTheme.Day, selectedTheme, true, null, null);
        }

        // Update Sunset button
        if (sunsetThemeButton != null)
        {
            bool isUnlocked = themeManager.IsSunsetUnlocked;
            UpdateButtonVisualState(sunsetThemeButton, GameTheme.Sunset, selectedTheme, isUnlocked, sunsetThemeUnlockedSprite, sunsetThemeLockedSprite);
        }

        // Update Night button
        if (nightThemeButton != null)
        {
            bool isUnlocked = themeManager.IsNightUnlocked;
            UpdateButtonVisualState(nightThemeButton, GameTheme.Night, selectedTheme, isUnlocked, nightThemeUnlockedSprite, nightThemeLockedSprite);
        }
    }

    /// <summary>
    /// Update a button's visual state (sprite, color, interactability)
    /// </summary>
    private void UpdateButtonVisualState(Button button, GameTheme theme, GameTheme selectedTheme, bool isUnlocked = true, Sprite unlockedSprite = null, Sprite lockedSprite = null)
    {
        if (button == null) return;

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage == null) return;

        // Keep buttons always interactable so locked buttons can show unlock info
        button.interactable = true;

        // Set sprite based on unlock state
        if (isUnlocked && unlockedSprite != null)
        {
            buttonImage.sprite = unlockedSprite;
        }
        else if (!isUnlocked && lockedSprite != null)
        {
            buttonImage.sprite = lockedSprite;
        }

        // Set color based on state (preserve full alpha to prevent transparency issues)
        if (selectedTheme == theme)
        {
            Color color = selectedButtonColor;
            color.a = 1f; // Ensure full opacity
            buttonImage.color = color;
        }
        else if (isUnlocked)
        {
            Color color = unlockedButtonColor;
            color.a = 1f; // Ensure full opacity
            buttonImage.color = color;
        }
        else
        {
            Color color = lockedButtonColor;
            color.a = 1f; // Ensure full opacity
            buttonImage.color = color;
        }
    }

    /// <summary>
    /// Handle theme button click
    /// </summary>
    private void OnThemeButtonClicked(GameTheme theme)
    {
        if (themeManager == null) return;

        soundManager?.PlayButtonClick();

        // Check if theme is unlocked
        if (!themeManager.IsThemeUnlocked(theme))
        {
            // Show unlock info panel
            ShowUnlockInfo(theme);
        }
        else
        {
            // Check if already selected
            if (themeManager.GetSelectedTheme() == theme)
            {
                // Already selected, do nothing
                return;
            }

            // Show confirmation panel
            ShowConfirmation(theme);
        }
    }

    /// <summary>
    /// Show unlock info panel for a locked theme
    /// </summary>
    private void ShowUnlockInfo(GameTheme theme)
    {
        if (unlockInfoPanel == null) return;

        // Hide confirmation panel if open
        HideConfirmation();

        // Set unlock requirements text
        string title = "";
        string description = "";

        switch (theme)
        {
            case GameTheme.Sunset:
                title = LocalizationManager.Get("theme_sunset");
                int completedLevels = themeManager != null ? themeManager.GetCompletedLevelCountPublic() : 0;
                description = LocalizationManager.Get("theme_sunset_unlock_format", completedLevels);
                break;
            case GameTheme.Night:
                title = LocalizationManager.Get("theme_night");
                completedLevels = themeManager != null ? themeManager.GetCompletedLevelCountPublic() : 0;
                description = LocalizationManager.Get("theme_night_unlock_format", completedLevels);
                break;
        }

        if (unlockInfoTitle != null)
            unlockInfoTitle.text = title;

        if (unlockInfoDescription != null)
            unlockInfoDescription.text = description;

        // Show panel
        unlockInfoPanel.SetActive(true);
        soundManager?.PlayPanelOpen();
    }

    /// <summary>
    /// Hide unlock info panel
    /// </summary>
    private void HideUnlockInfo()
    {
        if (unlockInfoPanel != null)
        {
            unlockInfoPanel.SetActive(false);
            soundManager?.PlayPanelClose();
        }
    }

    /// <summary>
    /// Show confirmation panel for theme selection
    /// </summary>
    private void ShowConfirmation(GameTheme theme)
    {
        if (confirmationPanel == null) return;

        // Hide unlock info panel if open
        HideUnlockInfo();

        // Store pending selection
        pendingThemeSelection = theme;

        // Set confirmation text
        string themeName = theme.ToString();
        if (confirmationTitle != null)
            confirmationTitle.text = LocalizationManager.Get("theme_confirm_title_format", themeName);

        if (confirmationMessage != null)
            confirmationMessage.text = LocalizationManager.Get("theme_confirm_message_format", themeName);

        // Show panel
        confirmationPanel.SetActive(true);
        soundManager?.PlayPanelOpen();
    }

    /// <summary>
    /// Hide confirmation panel
    /// </summary>
    private void HideConfirmation()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
            soundManager?.PlayPanelClose();
        }
    }

    /// <summary>
    /// Confirm theme selection
    /// </summary>
    private void OnConfirmThemeSelection()
    {
        if (themeManager == null) return;

        // Set the selected theme
        themeManager.SetSelectedTheme(pendingThemeSelection);

        // Hide confirmation panel
        HideConfirmation();

        // Update button states to reflect new selection
        UpdateThemeButtonStates();

        soundManager?.PlayButtonClick();
    }

    /// <summary>
    /// Called when theme is changed
    /// </summary>
    private void OnThemeChanged(GameTheme theme)
    {
        UpdateThemeButtonStates();
    }

    /// <summary>
    /// Called when a theme is unlocked
    /// </summary>
    private void OnThemeUnlocked(GameTheme theme)
    {
        UpdateThemeButtonStates();
    }

    /// <summary>
    /// Enable this component (called by MainMenuManager when showing level selection)
    /// Re-checks theme unlocks in case progress was synced from cloud
    /// </summary>
    public void EnableThemeSelection()
    {
        // Re-check theme unlocks (important after cloud sync or when reopening UI)
        if (themeManager != null)
        {
            themeManager.CheckAndUnlockThemes();
        }

        UpdateThemeButtonStates();
    }

    /// <summary>
    /// Disable this component (called by MainMenuManager when hiding level selection)
    /// </summary>
    public void DisableThemeSelection()
    {
        // Hide any open panels
        HideUnlockInfo();
        HideConfirmation();
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<ThemeSelectionUI>(this);

        // Unsubscribe from events
        if (themeManager != null)
        {
            themeManager.OnThemeChanged -= OnThemeChanged;
            themeManager.OnThemeUnlocked -= OnThemeUnlocked;
        }

        // Clean up button listeners
        if (dayThemeButton != null)
            dayThemeButton.onClick.RemoveAllListeners();

        if (sunsetThemeButton != null)
            sunsetThemeButton.onClick.RemoveAllListeners();

        if (nightThemeButton != null)
            nightThemeButton.onClick.RemoveAllListeners();

        if (unlockInfoCloseButton != null)
            unlockInfoCloseButton.onClick.RemoveAllListeners();

        if (confirmButton != null)
            confirmButton.onClick.RemoveAllListeners();

        if (cancelButton != null)
            cancelButton.onClick.RemoveAllListeners();
    }
}

