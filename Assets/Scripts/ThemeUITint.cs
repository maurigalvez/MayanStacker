using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies a per-theme color tint to any UI Graphic (Image, TMP_Text, RawImage, etc.)
/// Lightweight alternative to swapping full sprites — lets you recolor existing UI
/// elements (buttons, panels, text, icons) based on the selected GameTheme.
///
/// Attach to a UI GameObject, assign a color per theme in the Inspector, and it will
/// automatically update when ThemeManager.OnThemeChanged fires.
/// </summary>
[DisallowMultipleComponent]
public class ThemeUITint : MonoBehaviour
{
    [Header("Target Graphics")]
    [Tooltip("The Graphics to tint (Image/TMP_Text/RawImage). If empty, auto-finds a Graphic on this GameObject.")]
    [SerializeField] private Graphic[] targetGraphics;

    [Header("Colors by Theme")]
    [SerializeField] private Color dayColor = Color.white;
    [SerializeField] private Color sunsetColor = new Color(1f, 0.75f, 0.55f, 1f);
    [SerializeField] private Color nightColor = new Color(0.55f, 0.6f, 0.85f, 1f);

    [Header("Behavior")]
    [Tooltip("If true, multiplies the theme color with the graphic's original color instead of replacing it.")]
    [SerializeField] private bool multiplyWithOriginal = false;

    // References
    private ThemeManager themeManager;
    private Color[] originalColors;

    private void Awake()
    {
        if (targetGraphics == null || targetGraphics.Length == 0)
        {
            Graphic found = GetComponent<Graphic>();
            if (found != null)
            {
                targetGraphics = new Graphic[] { found };
            }
        }

        if (targetGraphics == null || targetGraphics.Length == 0)
        {
            Debug.LogWarning($"ThemeUITint on {gameObject.name}: No Graphic components assigned or found.");
            originalColors = new Color[0];
            return;
        }

        originalColors = new Color[targetGraphics.Length];
        for (int i = 0; i < targetGraphics.Length; i++)
        {
            if (targetGraphics[i] != null)
            {
                originalColors[i] = targetGraphics[i].color;
            }
        }
    }

    private void Start()
    {
        themeManager = DependencyRegistry.Find<ThemeManager>();

        if (themeManager != null)
        {
            themeManager.OnThemeChanged += OnThemeChanged;
            ApplyTheme(themeManager.GetSelectedTheme());
        }
    }

    private void OnThemeChanged(GameTheme theme)
    {
        ApplyTheme(theme);
    }

    private void ApplyTheme(GameTheme theme)
    {
        if (targetGraphics == null) return;

        Color themeColor = GetColorForTheme(theme);
        for (int i = 0; i < targetGraphics.Length; i++)
        {
            Graphic g = targetGraphics[i];
            if (g == null) continue;
            g.color = multiplyWithOriginal ? originalColors[i] * themeColor : themeColor;
        }
    }

    private Color GetColorForTheme(GameTheme theme)
    {
        switch (theme)
        {
            case GameTheme.Day: return dayColor;
            case GameTheme.Sunset: return sunsetColor;
            case GameTheme.Night: return nightColor;
            default: return dayColor;
        }
    }

    /// <summary>
    /// Re-apply the current theme color. Call after re-enabling the UI if needed.
    /// </summary>
    public void RefreshTheme()
    {
        if (themeManager == null)
        {
            themeManager = DependencyRegistry.Find<ThemeManager>();
        }

        if (themeManager != null)
        {
            themeManager.OnThemeChanged -= OnThemeChanged;
            themeManager.OnThemeChanged += OnThemeChanged;
            ApplyTheme(themeManager.GetSelectedTheme());
        }
    }

    private void OnDestroy()
    {
        if (themeManager != null)
        {
            themeManager.OnThemeChanged -= OnThemeChanged;
        }
    }
}
