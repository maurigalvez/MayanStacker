using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One mission line: its goal, a progress bar, the jade it pays and a done marker.
/// Lives in its own file because Unity only binds a MonoBehaviour whose class name matches the file.
/// </summary>
public class MissionRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI descriptionLabel;
    [SerializeField] private TextMeshProUGUI progressLabel;
    [SerializeField] private TextMeshProUGUI rewardLabel;
    [Tooltip("Stretched child of the bar track; its right anchor is moved to show progress.")]
    [SerializeField] private RectTransform barFill;
    [Tooltip("Shown when the mission is complete.")]
    [SerializeField] private GameObject doneMarker;

    [Tooltip("Optional: swaps this mission for another (once a day, behind a rewarded ad).")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private TextMeshProUGUI rerollLabel;

    public bool IsComplete => descriptionLabel != null && progressLabel != null && rewardLabel != null && barFill != null;

    public bool HasReroll => rerollButton != null && rerollLabel != null;

    /// <summary>Shows or hides the swap button; <paramref name="onTap"/> runs when it is pressed.</summary>
    public void BindReroll(bool visible, System.Action onTap)
    {
        if (!HasReroll) return;
        rerollButton.onClick.RemoveAllListeners();
        rerollButton.gameObject.SetActive(visible);
        if (!visible) return;

        rerollLabel.text = LocalizationManager.Get(RemoveAds.Owned ? "missions_reroll" : "missions_reroll_ad");
        rerollButton.interactable = true;
        rerollButton.onClick.AddListener(() =>
        {
            rerollButton.interactable = false;
            onTap?.Invoke();
        });
    }

    public void Bind(DailyMissions.Entry entry)
    {
        var t = entry.template;
        descriptionLabel.text = LocalizationManager.Get("mission_" + t.id, t.target);
        rewardLabel.text = LocalizationManager.Get("missions_reward", t.reward);

        float fraction = t.target > 0 ? Mathf.Clamp01(entry.progress / (float)t.target) : 1f;
        barFill.anchorMax = new Vector2(entry.done ? 1f : fraction, 1f);
        progressLabel.text = entry.done
            ? LocalizationManager.Get("missions_done")
            : entry.progress + "/" + t.target;
        if (doneMarker != null) doneMarker.SetActive(entry.done);
    }
}
