using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds UI in code on a dedicated overlay canvas.
///
/// The game's existing screens live in hand-authored prefabs (Game Scene UI, Main Menu
/// Canvas) that are wired field-by-field on UIManager. Anything added there has to be
/// laid out by hand in every scene that uses it. Run-scoped surfaces added on top —
/// altitude banners, the boon picker — instead build themselves at runtime on their own
/// canvas, exactly as LanguageSelectScreen already does.
///
/// The payoff is that no existing prefab, scene or serialized field is touched: turn a
/// feature off and its UI simply never appears.
///
/// Palette matches DailyChallengeUISetup so code-built surfaces and hand-authored ones
/// look like the same game.
/// </summary>
public static class RunOverlayUI
{
    public static readonly Color Backdrop = new Color(0.06f, 0.05f, 0.04f, 0.92f);
    public static readonly Color Stone = new Color(0.13f, 0.16f, 0.15f, 1f);
    public static readonly Color Jade = new Color(0.09f, 0.42f, 0.34f, 1f);
    public static readonly Color Clay = new Color(0.66f, 0.25f, 0.16f, 1f);
    public static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    public static readonly Color Parchment = new Color(0.93f, 0.90f, 0.82f, 1f);
    public static readonly Color Muted = new Color(0.72f, 0.68f, 0.58f, 1f);

    /// <summary>Reference resolution shared with the project's authored canvases.</summary>
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    private static LocaleFontSet fontSet;
    private static bool fontSetLoaded;

    /// <summary>
    /// The game's stylized Latin font, from Resources/LocaleFontSet.
    ///
    /// A label created in code gets TMP's LiberationSans default, because there is no prefab
    /// carrying a design-time font — which is why banners and pickers used to render in a
    /// plain sans while the rest of the game did not. Null when the font set is missing or
    /// its Latin font is unassigned; callers then keep the TMP default rather than fail.
    /// </summary>
    public static TMP_FontAsset DisplayFont
    {
        get
        {
            if (!fontSetLoaded)
            {
                fontSet = Resources.Load<LocaleFontSet>("LocaleFontSet");
                fontSetLoaded = true;
            }

            return fontSet != null ? fontSet.latinDisplay : null;
        }
    }

    /// <summary>
    /// Turns <paramref name="host"/> into a screen-space overlay canvas.
    ///
    /// <paramref name="sortingOrder"/> orders it against the authored UI, which sits at the
    /// default 0. Banners should sit above the HUD but below anything that blocks input;
    /// the picker sits above everything.
    /// </summary>
    public static Canvas CreateCanvas(GameObject host, int sortingOrder, bool interactive)
    {
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // Only add a raycaster when the surface actually takes input. A banner that eats
        // taps would swallow the drop input and make the game feel broken.
        if (interactive)
        {
            host.AddComponent<GraphicRaycaster>();
        }

        return canvas;
    }

    /// <summary>Creates an empty RectTransform child.</summary>
    public static RectTransform CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Creates a centred text label.</summary>
    public static TextMeshProUGUI CreateLabel(string name, Transform parent, string text,
        float fontSize, Color color)
    {
        var rt = CreateChild(name, parent);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;

        // Labels are decoration; never let them intercept a tap meant for the game.
        label.raycastTarget = false;

        // The game's font, then the CJK switcher — in that order, because the switcher
        // caches whatever font it finds as the label's "original" when it wakes up.
        if (DisplayFont != null) label.font = DisplayFont;
        rt.gameObject.AddComponent<LocaleFontSwitcher>();

        return label;
    }

    /// <summary>Creates a stone-slab button with a centred label.</summary>
    public static Button CreateButton(string name, Transform parent, string text,
        Color background, out TextMeshProUGUI label)
    {
        var rt = CreateChild(name, parent);

        var image = rt.gameObject.AddComponent<Image>();
        image.color = background;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        label = CreateLabel("Label", rt, text, 34f, Parchment);
        Stretch(label.rectTransform);

        return button;
    }

    /// <summary>Anchors a rect to a point and gives it a fixed size.</summary>
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>Makes a rect fill its parent.</summary>
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
