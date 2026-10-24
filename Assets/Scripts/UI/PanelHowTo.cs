using TMPro;
using UnityEngine;

/// <summary>
/// The "how this works" card at the top of a feature panel (Market, Missions, Expedition, Path): what
/// the feature is and exactly how the player takes part, in the player's language. A feature the
/// player can't explain back is a feature they won't use, so every panel opens on one.
///
/// Texts are localization keys, set on enable and again on a language change. A panel's view may add a
/// live line under the explanation through <see cref="SetExtra"/> (the Path shows the next reward).
///
/// Built into the panels by TamalStacker ▸ UI ▸ Restyle Spins Panels (PanelContentFit).
/// </summary>
public class PanelHowTo : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI headlineLabel;
    [SerializeField] private TextMeshProUGUI bodyLabel;
    [Tooltip("Optional live line under the explanation. Hidden while empty.")]
    [SerializeField] private TextMeshProUGUI extraLabel;
    [SerializeField] private string headlineKey;
    [SerializeField] private string bodyKey;

    private void OnEnable()
    {
        Refresh();
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= Refresh;
    }

    private void Refresh()
    {
        if (headlineLabel != null && !string.IsNullOrEmpty(headlineKey)) headlineLabel.text = LocalizationManager.Get(headlineKey);
        if (bodyLabel != null && !string.IsNullOrEmpty(bodyKey)) bodyLabel.text = LocalizationManager.Get(bodyKey);
    }

    /// <summary>Shows a live line under the explanation, or hides it when empty.</summary>
    public void SetExtra(string text)
    {
        if (extraLabel == null) return;
        extraLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        extraLabel.text = text ?? "";
    }
}
