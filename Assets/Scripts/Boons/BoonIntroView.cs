using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the one-time boon explainer.
///
/// The companion to <see cref="BoonPickerView"/>, for the screen that runs in front of a
/// player's first offer. Kept as its own prefab rather than a second panel on the picker so
/// either surface can be restyled — or deleted back to the code-built layout — without
/// touching the other.
///
/// Drop this on a prefab at Resources/UI/BoonIntro and <see cref="BoonSystem"/> uses it.
/// Unassigned fields are skipped.
///
/// Menu: TamalStacker ▸ Run Content ▸ Create Boon UI Prefabs
/// </summary>
public class BoonIntroView : MonoBehaviour
{
    [Header("Copy")]
    [Tooltip("Headline (\"The Blessings of Kukulkan\"). Filled from localization at runtime.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("The explanation itself. Filled from localization at runtime.")]
    [SerializeField] private TextMeshProUGUI bodyText;

    [Tooltip("Quieter line below the body, about the climb waiting and the warning. Optional.")]
    [SerializeField] private TextMeshProUGUI noteText;

    [Header("Continue")]
    [Tooltip("Dismisses the explainer and reveals the cards behind it. Required.")]
    [SerializeField] private Button continueButton;

    [Tooltip("Label on the continue button. Leave unassigned to use the button's first TMP child.")]
    [SerializeField] private TextMeshProUGUI continueLabel;

    /// <summary>
    /// Fills in the explainer. Returns the continue button so <see cref="BoonSystem"/> can
    /// hold it untappable for the same beat as the cards — otherwise the tap that placed
    /// the stone dismisses the explanation before it has been read.
    /// </summary>
    public Button Populate(string title, string body, string note, string continueText,
        Action onContinue)
    {
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;

        // A prefab that drops the note simply doesn't show it, rather than showing an
        // empty gap where a line used to be.
        if (noteText != null)
        {
            noteText.text = note;
            noteText.gameObject.SetActive(!string.IsNullOrEmpty(note));
        }

        if (continueButton == null)
        {
            Debug.LogWarning("[Boon] Intro prefab has no continue button - the explainer " +
                             "would trap the player with the game paused.");
            return null;
        }

        TextMeshProUGUI label = continueLabel != null
            ? continueLabel
            : continueButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = continueText;

        continueButton.onClick.AddListener(() => onContinue());

        return continueButton;
    }
}
