using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component that changes the main menu background sprite based on the selected theme
/// Designed for UI Image components
/// </summary>
public class ThemeMainMenuBackground : MonoBehaviour
{
    [Header("Background Sprites by Theme")]
    [Tooltip("Background sprite for Day theme")]
    [SerializeField] private Sprite dayBackgroundSprite;

    [Tooltip("Background sprite for Sunset theme")]
    [SerializeField] private Sprite sunsetBackgroundSprite;

    [Tooltip("Background sprite for Night theme")]
    [SerializeField] private Sprite nightBackgroundSprite;

    [Header("Component References")]
    [Tooltip("Image component (for UI sprites). Auto-found if not assigned.")]
    [SerializeField] private Image image;

    // References
    private ThemeManager themeManager;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<ThemeMainMenuBackground>(this);

        // Auto-find Image component if not assigned (primary component for UI backgrounds)
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (image == null)
        {
            Debug.LogWarning($"ThemeMainMenuBackground on {gameObject.name}: No Image component found! This component is designed for UI Image components.");
        }
    }

    private void Start()
    {
        // Find dependencies
        themeManager = DependencyRegistry.Find<ThemeManager>();

        // Subscribe to theme events
        if (themeManager != null)
        {
            themeManager.OnThemeChanged += OnThemeChanged;

            // Apply initial theme
            ApplyTheme(themeManager.GetSelectedTheme());
        }
        else
        {
            Debug.LogWarning($"ThemeMainMenuBackground on {gameObject.name}: ThemeManager not found via DependencyRegistry!");
        }
    }

    /// <summary>
    /// Called when theme is changed
    /// </summary>
    private void OnThemeChanged(GameTheme theme)
    {
        ApplyTheme(theme);
    }

    /// <summary>
    /// Apply the sprite for the given theme
    /// </summary>
    private void ApplyTheme(GameTheme theme)
    {
        if (image == null)
        {
            Debug.LogWarning($"ThemeMainMenuBackground on {gameObject.name}: Image component is null, cannot apply theme!");
            return;
        }

        Sprite targetSprite = GetSpriteForTheme(theme);

        if (targetSprite == null)
        {
            Debug.LogWarning($"ThemeMainMenuBackground on {gameObject.name}: No sprite assigned for theme {theme}");
            return;
        }

        // Apply to Image component (UI)
        image.sprite = targetSprite;
    }

    /// <summary>
    /// Get the sprite for the given theme
    /// </summary>
    private Sprite GetSpriteForTheme(GameTheme theme)
    {
        switch (theme)
        {
            case GameTheme.Day:
                return dayBackgroundSprite;
            case GameTheme.Sunset:
                return sunsetBackgroundSprite;
            case GameTheme.Night:
                return nightBackgroundSprite;
            default:
                return dayBackgroundSprite;
        }
    }

    /// <summary>
    /// Refresh the background sprite to match the currently selected theme
    /// Call this when the main menu is shown to ensure the correct theme is displayed
    /// </summary>
    public void RefreshTheme()
    {
        // Try to find ThemeManager if not already found
        if (themeManager == null)
        {
            themeManager = DependencyRegistry.Find<ThemeManager>();
        }

        // Subscribe to events if not already subscribed
        if (themeManager != null)
        {
            // Re-subscribe in case we missed the initial subscription
            themeManager.OnThemeChanged -= OnThemeChanged; // Remove first to avoid duplicates
            themeManager.OnThemeChanged += OnThemeChanged;

            // Apply current theme
            ApplyTheme(themeManager.GetSelectedTheme());
        }
        else
        {
            Debug.LogWarning($"ThemeMainMenuBackground on {gameObject.name}: ThemeManager not found via DependencyRegistry when refreshing theme!");
        }
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<ThemeMainMenuBackground>(this);

        // Unsubscribe from events
        if (themeManager != null)
        {
            themeManager.OnThemeChanged -= OnThemeChanged;
        }
    }
}
