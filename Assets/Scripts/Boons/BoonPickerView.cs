using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the boon picker.
///
/// <see cref="BoonSystem"/> builds its picker in code so the feature needs no scene wiring,
/// which also means it can't be restyled alongside the rest of the UI. Drop this component
/// on a prefab at Resources/UI/BoonPicker and the picker uses that instead — same
/// behaviour, same localization, but the art is editable like every other panel. Nothing
/// here is required: fields left unassigned are skipped, and with no prefab at all the
/// system falls back to its code-built layout.
///
/// The cards are cloned from <see cref="optionTemplate"/>, one per offer, so styling one
/// card styles them all. This is the <see cref="LanguageSelectView"/> pattern applied to a
/// second code-built surface.
///
/// Menu: TamalStacker ▸ Run Content ▸ Create Boon UI Prefabs generates a prefab matching
/// the current code-built layout, as a starting point to restyle.
/// </summary>
public class BoonPickerView : MonoBehaviour
{
    [Header("Copy")]
    [Tooltip("Headline (\"Kukulkan Offers\"). Filled from localization at runtime.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Line under the title (\"Choose one blessing.\"). Filled from localization at runtime.")]
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Cards")]
    [Tooltip("Parent the boon cards are cloned into. A LayoutGroup here is honoured.")]
    [SerializeField] private RectTransform optionsContainer;

    [Tooltip("Card cloned once per offer. Disabled at runtime; keep it enabled in the " +
             "prefab so it stays easy to edit.")]
    [SerializeField] private Button optionTemplate;

    [Tooltip("Child of the card holding the boon's name. Leave blank to use the card's first TMP label.")]
    [SerializeField] private string nameChild = "Name";

    [Tooltip("Child of the card holding the boon's description. Blank skips it.")]
    [SerializeField] private string descriptionChild = "Description";

    [Header("Accent")]
    [Tooltip("Tint the name label with the boon's accent colour, so the four blessings stay " +
             "colour-coded however the card is restyled.")]
    [SerializeField] private bool tintNameWithAccent = true;

    [Tooltip("Optional Graphic child (a stripe, an icon backing) tinted with the boon's accent colour.")]
    [SerializeField] private string accentGraphicChild = "";

    /// <summary>
    /// Fills in the picker: one card per offer, in the order given.
    ///
    /// Returns the cards it created so <see cref="BoonSystem"/> can hold them untappable
    /// for a beat — the arming delay is a rule about the offer, not about the art, so it
    /// has to survive whatever prefab is dropped in here.
    /// </summary>
    public List<Button> Populate(string title, string subtitle, IList<BoonId> offers,
        Action<BoonId> onChoose)
    {
        var created = new List<Button>();

        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;

        if (optionTemplate == null)
        {
            Debug.LogWarning("[Boon] Picker prefab has no card template - nothing to choose from.");
            return created;
        }

        Transform parent = optionsContainer != null ? optionsContainer : optionTemplate.transform.parent;
        optionTemplate.gameObject.SetActive(false);

        for (int i = 0; i < offers.Count; i++)
        {
            BoonId id = offers[i];
            BoonDefinition def = BoonDefinition.For(id);

            var card = Instantiate(optionTemplate, parent);
            card.name = $"Boon_{id}";
            card.gameObject.SetActive(true);

            ApplyLabel(card, nameChild, LocalizationManager.Get(def.nameKey),
                tintNameWithAccent ? def.accentColor : (Color?)null);

            ApplyLabel(card, descriptionChild, LocalizationManager.Get(def.descriptionKey), null);

            if (!string.IsNullOrEmpty(accentGraphicChild))
            {
                var accent = card.transform.Find(accentGraphicChild);
                var graphic = accent != null ? accent.GetComponent<Graphic>() : null;
                if (graphic != null) graphic.color = def.accentColor;
            }

            BoonId captured = id;
            card.onClick.AddListener(() => onChoose(captured));

            created.Add(card);
        }

        return created;
    }

    /// <summary>
    /// Writes text into a named child, tinting it when <paramref name="color"/> is given.
    /// A blank child name falls back to the card's own first label, so a minimal card that
    /// shows only the boon's name still works.
    /// </summary>
    private static void ApplyLabel(Component card, string childName, string text, Color? color)
    {
        TextMeshProUGUI label;

        if (string.IsNullOrEmpty(childName))
        {
            label = card.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        else
        {
            Transform child = card.transform.Find(childName);
            label = child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        if (label == null) return;

        label.text = text;
        if (color.HasValue) label.color = color.Value;
    }
}
