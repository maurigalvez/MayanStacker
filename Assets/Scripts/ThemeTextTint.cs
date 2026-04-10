using TMPro;
using UnityEngine;

/// <summary>
/// Applies per-theme colors to TextMeshPro text components.
/// Unlike ThemeUITint (which only sets Graphic.color), this can tint the face color
/// and optionally the outline color of TMP_Text, which are controlled by material
/// properties rather than the base vertex color.
///
/// Attach to a GameObject with one or more TMP_Text components, assign colors per
/// theme, and it will update when ThemeManager.OnThemeChanged fires.
/// </summary>
[DisallowMultipleComponent]
public class ThemeTextTint : MonoBehaviour
{
    [Header("Target Texts")]
    [Tooltip("The TMP_Text components to tint. If empty, auto-finds one on this GameObject.")]
    [SerializeField] private TMP_Text[] targetTexts;

    [Header("Face Colors by Theme")]
    [SerializeField] private Color dayFaceColor = Color.white;
    [SerializeField] private Color sunsetFaceColor = new Color(1f, 0.9f, 0.7f, 1f);
    [SerializeField] private Color nightFaceColor = new Color(0.8f, 0.85f, 1f, 1f);

    [Header("Outline")]
    [Tooltip("If true, also tints the outline color per theme.")]
    [SerializeField] private bool tintOutline = false;
    [SerializeField] private Color dayOutlineColor = Color.black;
    [SerializeField] private Color sunsetOutlineColor = new Color(0.4f, 0.2f, 0.1f, 1f);
    [SerializeField] private Color nightOutlineColor = new Color(0.1f, 0.1f, 0.3f, 1f);

    // References
    private ThemeManager themeManager;

    private void Awake()
    {
        if (targetTexts == null || targetTexts.Length == 0)
        {
            TMP_Text found = GetComponent<TMP_Text>();
            if (found != null)
            {
                targetTexts = new TMP_Text[] { found };
            }
        }

        if (targetTexts == null || targetTexts.Length == 0)
        {
            Debug.LogWarning($"ThemeTextTint on {gameObject.name}: No TMP_Text components assigned or found.");
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
        if (targetTexts == null) return;

        Color faceColor = GetFaceColorForTheme(theme);
        Color outlineColor = GetOutlineColorForTheme(theme);

        for (int i = 0; i < targetTexts.Length; i++)
        {
            TMP_Text text = targetTexts[i];
            if (text == null) continue;

            // Face color (TMP uses .color for the vertex/face tint)
            text.color = faceColor;

            if (tintOutline)
            {
                text.outlineColor = outlineColor;
            }
        }
    }

    private Color GetFaceColorForTheme(GameTheme theme)
    {
        switch (theme)
        {
            case GameTheme.Day: return dayFaceColor;
            case GameTheme.Sunset: return sunsetFaceColor;
            case GameTheme.Night: return nightFaceColor;
            default: return dayFaceColor;
        }
    }

    private Color GetOutlineColorForTheme(GameTheme theme)
    {
        switch (theme)
        {
            case GameTheme.Day: return dayOutlineColor;
            case GameTheme.Sunset: return sunsetOutlineColor;
            case GameTheme.Night: return nightOutlineColor;
            default: return dayOutlineColor;
        }
    }

    /// <summary>
    /// Re-apply the current theme. Call after re-enabling the UI if needed.
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
