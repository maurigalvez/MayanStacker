using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One line of "Today's Omens" on the Ritual Briefing: a temple rule, a modifier or a granted
/// power. Lives on the row template in Resources/UI/RitualBriefing; the view clones it per omen.
/// </summary>
public class RitualOmenRow : MonoBehaviour
{
    [Tooltip("Coloured stripe that says what kind of omen this is (rule / modifier / power).")]
    [SerializeField] private Image accent;
    [Tooltip("Optional: hidden when the omen has no icon.")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI descriptionLabel;

    public bool IsUsable => nameLabel != null && descriptionLabel != null;

    public void Set(string title, string description, Sprite iconSprite, Color accentColor)
    {
        nameLabel.text = title;
        descriptionLabel.text = description;

        if (accent != null) accent.color = accentColor;
        nameLabel.color = accentColor;

        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.gameObject.SetActive(iconSprite != null);
        }
    }
}
