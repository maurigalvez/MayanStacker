using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One rule on the pre-play screen's rule strip: its icon, name and one-line summary.
///
/// Lives on the entry template inside <see cref="PowerLoadoutView"/>'s rule strip and is cloned
/// once per rule (twice, side by side, on a paired temple), so styling the template styles both.
/// Every part is optional.
/// </summary>
public class TempleRuleBriefEntry : MonoBehaviour
{
    [Tooltip("The rule's badge (TempleRuleIcons).")]
    public Image icon;

    [Tooltip("The rule's name, e.g. \"Cabracán\".")]
    public TextMeshProUGUI nameText;

    [Tooltip("The rule's one-line summary, e.g. \"Hold BRACE when it shakes\".")]
    public TextMeshProUGUI shortText;

    public void Fill(LevelRule rule)
    {
        if (icon != null)
        {
            icon.sprite = TempleRuleIcons.For(rule);
            icon.enabled = icon.sprite != null;
        }
        if (nameText != null) nameText.text = LocalizationManager.Get(TempleRules.NameKey(rule));
        if (shortText != null) shortText.text = LocalizationManager.Get(TempleRules.ShortKey(rule));
    }
}
