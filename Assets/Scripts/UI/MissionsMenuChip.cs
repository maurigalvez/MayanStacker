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
    private CanvasGroup group;

    private void Awake()
    {
        button = GetComponent<Button>();
        group = GetComponent<CanvasGroup>();
        button.onClick.AddListener(OnPressed);
    }

    private void OnEnable()
    {
        DailyMissions.OnMissionCompleted += OnCompleted;
        Refresh();
    }

    private void OnDisable()
    {
        DailyMissions.OnMissionCompleted -= OnCompleted;
    }

    private void OnCompleted(MissionSettings.Template _) => Refresh();

    private void Refresh()
    {
        bool show = MissionsView.Available;
        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
        if (!show || countLabel == null) return;

        MissionsView.Count(out int done, out int total);
        countLabel.text = LocalizationManager.Get("missions_count", done, total);
    }

    private void OnPressed()
    {
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        MissionsView.Show();
    }
}
