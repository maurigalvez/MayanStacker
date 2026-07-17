using TMPro;
using UnityEngine;

/// <summary>
/// Maps a locale code to the TMP font asset that should render it.
///
/// Only languages that need a script-specific font are listed. Latin languages
/// (en, es-419, pt-BR) return null, which tells <see cref="LocaleFontSwitcher"/>
/// to keep each label's original design-time (stylized Latin) font.
///
/// The three CJK locales get their own regional font so glyph SHAPES are correct
/// (Simplified vs Traditional vs Japanese share codepoints but want different
/// shapes — the "Han unification" problem a single shared fallback can't solve).
///
/// Lives at Resources/LocaleFontSet.asset and is loaded once at runtime.
/// Populate it via  TamalStacker ▸ Localization ▸ Setup Per-Language Fonts.
/// </summary>
[CreateAssetMenu(fileName = "LocaleFontSet", menuName = "TamalStacker/Locale Font Set")]
public class LocaleFontSet : ScriptableObject
{
    [Tooltip("Font for Simplified Chinese (zh-Hans). e.g. Source Han Sans SC / Noto Sans SC")]
    public TMP_FontAsset simplifiedChinese;

    [Tooltip("Font for Traditional Chinese (zh-Hant). e.g. Source Han Sans TC/HK / Noto Sans TC")]
    public TMP_FontAsset traditionalChinese;

    [Tooltip("Font for Japanese (ja). e.g. Source Han Sans JP / Noto Sans JP")]
    public TMP_FontAsset japanese;

    /// <summary>
    /// Returns the font for a locale, or null if the locale should keep its
    /// original Latin design font.
    /// </summary>
    public TMP_FontAsset GetFontForLocale(string localeCode)
    {
        switch (localeCode)
        {
            case "zh-Hans": return simplifiedChinese;
            case "zh-Hant": return traditionalChinese;
            case "ja": return japanese;
            default: return null; // en, es-419, pt-BR → keep original Latin font
        }
    }
}
