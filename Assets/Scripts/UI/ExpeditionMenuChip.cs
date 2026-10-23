using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The main-menu "EXPEDITION" chip: shows the stages cleared this week and opens the
/// <see cref="ExpeditionView"/>. Faded out (not deactivated, so it comes back) when the
/// Expedition is off.
///
/// Built into the Main Menu Canvas prefab by TamalStacker ▸ Economy ▸ Add Expedition Menu Chip.
/// </summary>
[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class ExpeditionMenuChip : MonoBehaviour
{
    [Tooltip("Shows \"cleared/total\" for the week.")]
    [SerializeField] private TextMeshProUGUI countLabel;

    private Button button;
    private CanvasGroup group;

    private void Awake()
    {
        button = GetComponent<Button>();
        group = GetComponent<CanvasGroup>();
        button.onClick.AddListener(OnPressed);
    }

    private void OnEnable()
    {
        WeeklyExpedition.OnStageCompleted += OnStage;
        Refresh();
    }

    private void OnDisable()
    {
        WeeklyExpedition.OnStageCompleted -= OnStage;
    }

    private void OnStage(int stage, int jade) => Refresh();

    private void Refresh()
    {
        bool show = ExpeditionView.Available;
        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
        if (!show || countLabel == null) return;

        countLabel.text = LocalizationManager.Get("expedition_row_progress",
            WeeklyExpedition.Stage, ExpeditionSettings.Active.stages.Length);
    }

    private void OnPressed()
    {
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        ExpeditionView.Show();
    }
}
