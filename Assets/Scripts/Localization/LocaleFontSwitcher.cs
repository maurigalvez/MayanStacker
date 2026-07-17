using TMPro;
using UnityEngine;

/// <summary>
/// Gives a TMP label the correct script-specific font ONLY when it is actually
/// displaying Chinese/Japanese text, and otherwise leaves its original stylized
/// (Latin) font untouched.
///
/// The decision is driven by the label's CURRENT TEXT, not just the active
/// language. This matters because:
///   • Many labels are hardcoded English with no translation (SETTINGS, MASTER,
///     DAILY, INFINITE, TEMPLES). Swapping their font to a plain CJK font while
///     Chinese/Japanese is active made English words look out of place. Since
///     their text is pure ASCII, they now keep the stylized font.
///   • Localized copy is set two ways in this project — via LocalizedText AND via
///     code (LocalizationManager.Get assigned to .text). Reading the text works
///     for both; a component-presence check would miss the code-driven ones.
///
/// Rule: if the active locale has a CJK font AND the displayed text contains a
/// CJK/kana character, use that font; otherwise restore the original font+material.
///
/// Attach to any TMP label (the TamalStacker ▸ Localization ▸ Attach… menus do it
/// in bulk). Safe on every label — non-CJK text is never restyled.
///
/// For MIXED-SCRIPT labels whose Latin part should stay stylized (e.g. the level
/// title "第 5 關\nUxbenka"), tick <c>keepOriginalFont</c>: the label keeps its
/// stylized font and the global fallback renders only the CJK characters.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocaleFontSwitcher : MonoBehaviour
{
    private const string FontSetResourcePath = "LocaleFontSet";

    [Tooltip("Keep this label's original stylized font and let the TMP global " +
             "fallback render any CJK characters instead of swapping the whole " +
             "label. Use for mixed-script labels where the Latin part (e.g. a " +
             "proper level name like 'Uxbenka') should stay in the stylized font.")]
    [SerializeField] private bool keepOriginalFont = false;

    private static LocaleFontSet fontSet;
    private static bool fontSetLoaded;
    private static bool missingWarningShown;

    private TMP_Text text;
    private TMP_FontAsset originalFont;
    private Material originalMaterial;
    private bool cached;
    private bool applying;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        CacheOriginal();
    }

    private void CacheOriginal()
    {
        if (cached || text == null) return;
        originalFont = text.font;
        originalMaterial = text.fontSharedMaterial;
        cached = true;
    }

    private void OnEnable()
    {
        var loc = LocalizationManager.Instance;
        if (loc != null)
            loc.OnLanguageChanged += Evaluate;

        // Re-decide whenever this label's text regenerates (covers code-driven
        // text set outside of language-change events).
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnAnyTextChanged);

        Evaluate();
    }

    private void OnDisable()
    {
        var loc = LocalizationManager.Instance;
        if (loc != null)
            loc.OnLanguageChanged -= Evaluate;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnAnyTextChanged);
    }

    private void Start()
    {
        // Re-apply after all Awakes, in case the LocalizationManager initialized
        // its locale after this component's OnEnable.
        Evaluate();
    }

    private void OnAnyTextChanged(Object changed)
    {
        if (!applying && changed == (Object)text)
            Evaluate();
    }

    private void Evaluate()
    {
        if (text == null || applying) return;
        CacheOriginal();

        TMP_FontAsset target = ResolveTargetFont();

        applying = true; // guard against re-entrancy via TEXT_CHANGED
        try
        {
            if (target != null)
            {
                if (text.font != target)
                    text.font = target;
            }
            else
            {
                if (text.font != originalFont)
                    text.font = originalFont;
                if (originalMaterial != null && text.fontSharedMaterial != originalMaterial)
                    text.fontSharedMaterial = originalMaterial;
            }
        }
        finally
        {
            applying = false;
        }
    }

    /// <summary>
    /// The CJK font to use, or null to keep the original Latin font.
    /// Null unless the locale has a CJK font AND the text actually shows CJK.
    /// </summary>
    private TMP_FontAsset ResolveTargetFont()
    {
        // Opt-out: keep the stylized font; the global fallback covers CJK glyphs.
        if (keepOriginalFont) return null;

        var set = GetFontSet();
        if (set == null) return null;

        string locale = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.CurrentLocale
            : "en";

        TMP_FontAsset localeFont = set.GetFontForLocale(locale);
        if (localeFont == null) return null;         // Latin locale → keep original
        if (!ContainsCjk(text.text)) return null;    // ASCII/untranslated → keep original

        return localeFont;
    }

    /// <summary>True if the string contains any Chinese/Japanese character.</summary>
    private static bool ContainsCjk(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s)
        {
            if ((c >= 0x3000 && c <= 0x30FF) ||  // CJK symbols/punct, Hiragana, Katakana
                (c >= 0x3400 && c <= 0x9FFF) ||  // CJK Unified Ideographs (incl. Ext A)
                (c >= 0xF900 && c <= 0xFAFF) ||  // CJK Compatibility Ideographs
                (c >= 0xFF00 && c <= 0xFFEF))    // Halfwidth/Fullwidth forms
                return true;
        }
        return false;
    }

    private static LocaleFontSet GetFontSet()
    {
        if (fontSetLoaded) return fontSet;
        fontSet = Resources.Load<LocaleFontSet>(FontSetResourcePath);
        fontSetLoaded = true;

        if (fontSet == null && !missingWarningShown)
        {
            missingWarningShown = true;
            Debug.LogWarning(
                "LocaleFontSwitcher: No LocaleFontSet found at Resources/LocaleFontSet. " +
                "Run TamalStacker ▸ Localization ▸ Setup Per-Language Fonts. " +
                "CJK labels will keep their Latin font (may show boxes) until then.");
        }
        return fontSet;
    }
}
