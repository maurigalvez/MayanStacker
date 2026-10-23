using TMPro;
using UnityEngine;

/// <summary>
/// One tier of the Path of Kukulkan: its number, what it pays and whether it has been reached.
/// Lives in its own file because Unity only binds a MonoBehaviour whose class name matches the file.
/// </summary>
public class PathRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tierLabel;
    [SerializeField] private TextMeshProUGUI rewardLabel;
    [Tooltip("Shown once the tier has been reached.")]
    [SerializeField] private GameObject reachedMarker;
    [Tooltip("Optional: dimmed while the tier is still ahead.")]
    [SerializeField] private CanvasGroup group;

    public bool IsComplete => tierLabel != null && rewardLabel != null;

    public void Bind(int tier, PathSettings.Tier reward, bool reached)
    {
        tierLabel.text = LocalizationManager.Get("path_row_tier", tier);

        string text = LocalizationManager.Get("path_row_jade", reward.jade);
        var item = CosmeticCatalog.Active != null ? CosmeticCatalog.Active.Find(reward.cosmeticId) : null;
        if (item != null) text += "\n" + LocalizationManager.Get(item.nameKey);
        rewardLabel.text = text;

        if (reachedMarker != null) reachedMarker.SetActive(reached);
        if (group != null) group.alpha = reached ? 1f : 0.6f;
    }
}
