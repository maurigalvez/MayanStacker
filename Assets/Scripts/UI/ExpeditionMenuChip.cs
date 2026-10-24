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
    private const string SeenKey = "expedition";

    private CanvasGroup group;
    private MenuRailCue cue;

    private void Awake()
    {
        button = GetComponent<Button>();
        group = GetComponent<CanvasGroup>();
        cue = GetComponent<MenuRailCue>();
        button.onClick.AddListener(OnPressed);
    }

    private void OnEnable()
    {
        WeeklyExpedition.OnStageCompleted += OnStage;
        Refresh();
    }

    // OnEnable can run before LocalizationManager.Awake on a fresh scene load, which leaves the raw
    // key on the label; Start runs after every Awake, and the language hook keeps it current.
    private void Start()
    {
        Refresh();
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged += Refresh;
        StartCoroutine(MenuRailPacing.AnnounceLater(MenuRailPacing.Feature.Expedition));
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= Refresh;
    }

    private void OnDisable()
    {
        WeeklyExpedition.OnStageCompleted -= OnStage;
    }

    private void OnStage(int stage, int jade) => Refresh();

    private void Refresh()
    {
        bool show = ExpeditionView.Available && MenuRailPacing.IsRevealed(MenuRailPacing.Feature.Expedition);
        MenuRailItem.SetShown(group, show);
        if (!show) return;

        if (cue != null) cue.Set((WeeklyExpedition.Stage > 0 && Stamp() != MenuRailCue.Seen(SeenKey)) || MenuRailPacing.IsFresh(MenuRailPacing.Feature.Expedition));
        if (countLabel == null) return;

        countLabel.text = LocalizationManager.Get("expedition_row_progress",
            WeeklyExpedition.Stage, ExpeditionSettings.Active.stages.Length);
    }

    private static int Stamp() => WeeklyExpedition.CurrentWeek * 100 + WeeklyExpedition.Stage;

    private void OnPressed()
    {
        MenuRailCue.MarkSeen(SeenKey, Stamp());
        MenuRailPacing.Introduced(MenuRailPacing.Feature.Expedition);
        if (cue != null) cue.Set(false);
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        ExpeditionView.Show();
    }
}
