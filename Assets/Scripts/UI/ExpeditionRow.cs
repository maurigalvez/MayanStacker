using TMPro;
using UnityEngine;

/// <summary>
/// One stage of the Weekly Expedition: its number, goal, progress and jade reward.
/// Lives in its own file because Unity only binds a MonoBehaviour whose class name matches the file.
/// </summary>
public class ExpeditionRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stageLabel;
    [Tooltip("Goal text, followed by the progress on the stage in play.")]
    [SerializeField] private TextMeshProUGUI goalLabel;
    [SerializeField] private TextMeshProUGUI rewardLabel;
    [Tooltip("Shown once the stage is cleared.")]
    [SerializeField] private GameObject doneMarker;
    [Tooltip("Optional: dimmed while the stage is still ahead.")]
    [SerializeField] private CanvasGroup group;

    public bool IsComplete => stageLabel != null && goalLabel != null && rewardLabel != null;

    /// <param name="state">Negative: ahead, zero: in play, positive: cleared.</param>
    public void Bind(int number, ExpeditionSettings.Stage stage, int state, int progress)
    {
        stageLabel.text = LocalizationManager.Get("expedition_row_stage", number);

        string goal = LocalizationManager.Get("expedition_" + stage.id, stage.target);
        if (state == 0) goal += "  " + LocalizationManager.Get("expedition_row_progress", progress, stage.target);
        goalLabel.text = goal;
        rewardLabel.text = LocalizationManager.Get("expedition_reward", stage.reward);

        if (doneMarker != null) doneMarker.SetActive(state > 0);
        if (group != null) group.alpha = state < 0 ? 0.6f : 1f;
    }
}
