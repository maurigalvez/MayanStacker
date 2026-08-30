using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the first-launch language picker.
///
/// LanguageSelectScreen used to build its whole UI in code, which meant the one screen
/// every new player sees could not be restyled alongside the rest of the UI. Drop this
/// component on a prefab at Resources/UI/LanguageSelectScreen and the screen uses that
/// instead — same behaviour, but the art is editable in the scene view like every other
/// panel. Nothing here is required: fields left unassigned are simply skipped, and if the
/// prefab is missing entirely the screen falls back to its old code-built layout.
///
/// The option rows are cloned from <see cref="optionTemplate"/>, one per shipped locale,
/// so styling one button styles them all.
///
/// Menu: TamalStacker ▸ Localization ▸ Create Language Select Prefab generates a prefab
/// that matches the current code-built layout, as a starting point to restyle.
/// </summary>
public class LanguageSelectView : MonoBehaviour
{
    [Header("Copy")]
    [Tooltip("Headline. Left as authored in the prefab — it is deliberately not localized, " +
             "because the player has not picked a language yet.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Multi-script line under the title. Needs a font that can draw CJK.")]
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Tooltip("Shown only on first launch (\"You can change this later in Settings.\").")]
    [SerializeField] private GameObject firstLaunchHint;

    [Header("Options")]
    [Tooltip("Parent the per-language buttons are cloned into. A LayoutGroup here is honoured.")]
    [SerializeField] private RectTransform optionsContainer;

    [Tooltip("Button cloned once per language. Its TMP child is filled with the language name. " +
             "Disabled at runtime; keep it enabled in the prefab so it stays easy to edit.")]
    [SerializeField] private Button optionTemplate;

    [Header("Highlight")]
    [Tooltip("Tint applied to the row for the language that is already active.")]
    [SerializeField] private Color currentLocaleColor = new Color(0.09f, 0.42f, 0.34f, 1f);

    [Tooltip("Optional object inside the template shown only on the currently active language.")]
    [SerializeField] private string currentLocaleMarkerChild = "";

    /// <summary>
    /// Fills in the picker: one row per shipped locale, each drawn in its own script.
    /// <paramref name="onChoose"/> receives the locale code the player tapped.
    /// </summary>
    public void Populate(LocaleFontSet fontSet, string currentLocale, bool isFirstLaunch, Action<string> onChoose)
    {
        if (firstLaunchHint != null) firstLaunchHint.SetActive(isFirstLaunch);

        // The subtitle mixes Latin, Chinese and Japanese; the CJK font draws all three.
        if (subtitleText != null) LanguageSelectScreen.ApplyBestFontFor(subtitleText, "zh-Hans", fontSet);

        if (optionTemplate == null)
        {
            Debug.LogWarning("[LanguageSelect] Prefab has no option template — no languages to pick from.");
            return;
        }

        Transform parent = optionsContainer != null ? optionsContainer : optionTemplate.transform.parent;
        optionTemplate.gameObject.SetActive(false);

        string[] codes = SettingsManager.LocaleCodes;
        string[] names = SettingsManager.LanguageNames;

        for (int i = 0; i < codes.Length; i++)
        {
            string code = codes[i];

            var row = Instantiate(optionTemplate, parent);
            row.name = $"Option_{code}";
            row.gameObject.SetActive(true);

            var label = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = i < names.Length ? names[i] : code;

                // Render each language in its own script, or degrade to a Latin name
                // rather than showing a row of tofu boxes.
                if (!LanguageSelectScreen.ApplyBestFontFor(label, code, fontSet))
                {
                    label.text = LanguageSelectScreen.LatinFallbackName(i, code);
                }
            }

            bool isCurrent = code == currentLocale;

            // The active locale is highlighted, so the screen offers a default rather than
            // demanding a decision from someone who has no opinion.
            if (isCurrent && row.targetGraphic != null) row.targetGraphic.color = currentLocaleColor;

            if (!string.IsNullOrEmpty(currentLocaleMarkerChild))
            {
                var marker = row.transform.Find(currentLocaleMarkerChild);
                if (marker != null) marker.gameObject.SetActive(isCurrent);
            }

            string captured = code;
            row.onClick.AddListener(() => onChoose(captured));
        }
    }
}
