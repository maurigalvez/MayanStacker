using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component that changes the map sprite based on the selected theme
/// Primarily designed for UI Image components (for UI maps)
/// </summary>
public class ThemeMapSprite : MonoBehaviour
{
    [Header("Map Sprites by Theme")]
    [Tooltip("Map sprite for Day theme")]
    [SerializeField] private Sprite dayMapSprite;

    [Tooltip("Map sprite for Sunset theme")]
    [SerializeField] private Sprite sunsetMapSprite;

    [Tooltip("Map sprite for Night theme")]
    [SerializeField] private Sprite nightMapSprite;

    [Header("Component References")]
    [Tooltip("Image component (for UI sprites). Auto-found if not assigned.")]
    [SerializeField] private Image image;

    // References
    private ThemeManager themeManager;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<ThemeMapSprite>(this);

        // Auto-find Image component if not assigned (primary component for UI maps)
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (image == null)
        {
            Debug.LogWarning($"ThemeMapSprite on {gameObject.name}: No Image component found! This component is designed for UI Image components.");
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
            Debug.LogWarning($"ThemeMapSprite on {gameObject.name}: ThemeManager not found via DependencyRegistry!");
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
            Debug.LogWarning($"ThemeMapSprite on {gameObject.name}: Image component is null, cannot apply theme!");
            return;
        }

        Sprite targetSprite = GetSpriteForTheme(theme);

        if (targetSprite == null)
        {
            Debug.LogWarning($"ThemeMapSprite on {gameObject.name}: No sprite assigned for theme {theme}");
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
                return dayMapSprite;
            case GameTheme.Sunset:
                return sunsetMapSprite;
            case GameTheme.Night:
                return nightMapSprite;
            default:
                return dayMapSprite;
        }
    }

    /// <summary>
    /// Refresh the map sprite to match the currently selected theme
    /// Call this when the level selection panel is shown to ensure the correct theme is displayed
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
            Debug.LogWarning($"ThemeMapSprite on {gameObject.name}: ThemeManager not found via DependencyRegistry when refreshing theme!");
        }
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<ThemeMapSprite>(this);

        // Unsubscribe from events
        if (themeManager != null)
        {
            themeManager.OnThemeChanged -= OnThemeChanged;
        }
    }
}
