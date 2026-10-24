using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The main-menu "MISSIONS" chip: shows how many of today's missions are done and opens the
/// <see cref="MissionsView"/>. Faded out (not deactivated, so it comes back) when missions are off.
///
/// Built into the Main Menu Canvas prefab by TamalStacker ▸ Economy ▸ Add Missions Menu Chip.
/// </summary>
[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class MissionsMenuChip : MonoBehaviour
{
    [Tooltip("Shows \"done/total\" for today.")]
    [SerializeField] private TextMeshProUGUI countLabel;

    private Button button;
    private const string SeenKey = "missions";

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
        DailyMissions.OnMissionCompleted += OnCompleted;
        Refresh();
    }

    // OnEnable can run before LocalizationManager.Awake on a fresh scene load, which leaves the raw
    // key on the label; Start runs after every Awake, and the language hook keeps it current.
    private void Start()
    {
        Refresh();
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged += Refresh;
        StartCoroutine(MenuRailPacing.AnnounceLater(MenuRailPacing.Feature.Missions));
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= Refresh;
    }

    private void OnDisable()
    {
        DailyMissions.OnMissionCompleted -= OnCompleted;
    }

    private void OnCompleted(MissionSettings.Template _) => Refresh();

    private void Refresh()
    {
        bool show = MissionsView.Available && MenuRailPacing.IsRevealed(MenuRailPacing.Feature.Missions);
        MenuRailItem.SetShown(group, show);
        if (!show) return;

        MissionsView.Count(out int done, out int total);
        if (cue != null) cue.Set((done > 0 && Stamp(done) != MenuRailCue.Seen(SeenKey)) || MenuRailPacing.IsFresh(MenuRailPacing.Feature.Missions));
        if (countLabel == null) return;
        countLabel.text = LocalizationManager.Get("missions_count", done, total);
    }

    private static int Stamp(int done) => DailyMissions.CurrentDay * 100 + done;

    private void OnPressed()
    {
        MissionsView.Count(out int done, out _);
        MenuRailCue.MarkSeen(SeenKey, Stamp(done));
        MenuRailPacing.Introduced(MenuRailPacing.Feature.Missions);
        if (cue != null) cue.Set(false);
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        MissionsView.Show();
    }
}
