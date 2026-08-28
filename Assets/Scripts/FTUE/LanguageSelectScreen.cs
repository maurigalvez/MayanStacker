using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The first thing a new player sees: pick a language.
///
/// It runs before the FTUE routes into the first temple, because every word of the
/// tutorial after this point assumes the player can read it. Device auto-detection only
/// covers the six locales the game ships, so anyone on an unmapped device (Korean, French,
/// German, ...) would otherwise be silently dropped into English with no way out until
/// they left the run.
///
/// Each option is written in its OWN script, using the per-locale font from LocaleFontSet
/// — a picker that renders 简体中文 as boxes is worse than no picker at all. Options whose
/// font can't render their own name fall back to a Latin label rather than showing tofu.
///
/// Builds its entire UI in code on a dedicated overlay canvas, so it needs no scene wiring
/// and works identically from the menu and from inside a run.
/// </summary>
public class LanguageSelectScreen : MonoBehaviour
{
    // Latin fallbacks, used only when a locale's font is missing or can't render its own name.
    private static readonly string[] LatinFallbackNames =
    {
        "English", "Espanol (Latinoamerica)", "Chinese (Simplified)",
        "Chinese (Traditional)", "Portugues (Brasil)", "Japanese"
    };

    // Mayan temple palette, matching DailyChallengeUISetup's placeholder styling.
    private static readonly Color Backdrop = new Color(0.06f, 0.05f, 0.04f, 0.97f);
    private static readonly Color Stone = new Color(0.13f, 0.16f, 0.15f, 1f);
    private static readonly Color StoneHighlight = new Color(0.09f, 0.42f, 0.34f, 1f); // jade
    private static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    private static readonly Color Parchment = new Color(0.93f, 0.90f, 0.82f, 1f);

    private static LanguageSelectScreen instance;

    private Action onChosen;
    private LocaleFontSet fontSet;

    /// <summary>True while the picker is on screen, so callers can avoid double-showing it.</summary>
    public static bool IsShowing => instance != null;

    /// <summary>
    /// Shows the picker. <paramref name="onChosen"/> fires after the language is applied
    /// and the screen has torn itself down — pass the "continue into the game" step there.
    /// </summary>
    public static void Show(Action onChosen, bool isFirstLaunch)
    {
        if (instance != null)
        {
            // Already up; don't stack a second copy, just adopt the newer continuation.
            instance.onChosen = onChosen;
            return;
        }

        var go = new GameObject("LanguageSelectScreen");
        instance = go.AddComponent<LanguageSelectScreen>();
        instance.onChosen = onChosen;
        instance.Build(isFirstLaunch);
    }

    private void Build(bool isFirstLaunch)
    {
        fontSet = Resources.Load<LocaleFontSet>("LocaleFontSet");

        // Own canvas, drawn above everything else in the scene. Screen-space overlay with
        // the project's 1080x1920 reference so it scales like the rest of the UI.
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Full-bleed backdrop, which also eats taps so nothing behind can be hit.
        var backdrop = CreateChild("Backdrop", transform);
        Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = Backdrop;

        // Title. Deliberately bilingual-neutral: the player can't be assumed to read
        // whatever locale we guessed, so the word "Language" carries the meaning and the
        // globe glyph carries it for anyone who can't read that either.
        var title = CreateLabel("Title", transform, "Language", 64, Gold);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(900f, 90f));

        var subtitle = CreateLabel("Subtitle", transform, "Idioma / 语言 / 言語", 34, Parchment);
        Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(900f, 60f));
        ApplyBestFontFor(subtitle, "zh-Hans"); // the subtitle mixes scripts; CJK font renders all of it

        BuildOptions();

        if (isFirstLaunch)
        {
            var hint = CreateLabel("Hint", transform, "You can change this later in Settings.", 26,
                new Color(0.72f, 0.68f, 0.58f, 1f));
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(900f, 50f));
        }

        GameAnalytics.Track("language_picker_shown", new System.Collections.Generic.Dictionary<string, object>
        {
            { "first_launch", isFirstLaunch },
            { "detected_locale", LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLocale : "unknown" }
        });
    }

    private void BuildOptions()
    {
        string[] codes = SettingsManager.LocaleCodes;
        string[] names = SettingsManager.LanguageNames;

        string current = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLocale : "en";

        const float buttonHeight = 130f;
        const float gap = 22f;
        float startY = -420f;

        for (int i = 0; i < codes.Length; i++)
        {
            string code = codes[i];
            string nativeName = i < names.Length ? names[i] : code;
            string fallbackName = i < LatinFallbackNames.Length ? LatinFallbackNames[i] : code;

            var button = CreateChild($"Option_{code}", transform);
            Place(button, new Vector2(0.5f, 1f), new Vector2(0f, startY - i * (buttonHeight + gap)),
                new Vector2(760f, buttonHeight));

            var image = button.gameObject.AddComponent<Image>();
            // The locale we're already using is highlighted, so the screen shows a default
            // rather than demanding a decision from someone who has no opinion.
            image.color = code == current ? StoneHighlight : Stone;

            var label = CreateLabel("Label", button, nativeName, 44, Parchment);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;

            // Render each language in its own script, or degrade to Latin rather than tofu.
            if (!ApplyBestFontFor(label, code))
            {
                label.text = fallbackName;
            }

            var btn = button.gameObject.AddComponent<Button>();
            btn.targetGraphic = image;

            string captured = code;
            btn.onClick.AddListener(() => Choose(captured));
        }
    }

    private void Choose(string localeCode)
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.SetLanguage(localeCode);
        }
        else
        {
            Debug.LogWarning("[LanguageSelect] No LocalizationManager - selection could not be applied.");
        }

        GameAnalytics.Track("language_selected", new System.Collections.Generic.Dictionary<string, object>
        {
            { "locale", localeCode }
        });

        Action continuation = onChosen;
        onChosen = null;

        if (instance == this) instance = null;
        Destroy(gameObject);

        continuation?.Invoke();
    }

    #region UI construction helpers

    /// <summary>
    /// Points a label at the font that can actually draw its text: the locale's font from
    /// LocaleFontSet for CJK, or the label's existing Latin font otherwise. Returns false
    /// when nothing available can render the string, so the caller can fall back.
    /// </summary>
    private bool ApplyBestFontFor(TextMeshProUGUI label, string localeCode)
    {
        TMP_FontAsset font = fontSet != null ? fontSet.GetFontForLocale(localeCode) : null;

        if (font != null)
        {
            label.font = font;
            return true;
        }

        // Latin locales keep the default font; that's expected, not a failure.
        if (localeCode == "en" || localeCode == "es-419" || localeCode == "pt-BR") return true;

        // A CJK locale with no font configured would render as boxes.
        return false;
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float size, Color color)
    {
        var rt = CreateChild(name, parent);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        return label;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;
    }

    #endregion

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
