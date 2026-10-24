using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The main-menu "PATH" chip: shows the current tier and opens the <see cref="PathView"/>. Faded
/// out (not deactivated, so it comes back) when the Path is off.
///
/// Built into the Main Menu Canvas prefab by TamalStacker ▸ Economy ▸ Add Path Menu Chip.
/// </summary>
[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class PathMenuChip : MonoBehaviour
{
    [Tooltip("Shows \"Tier n/total\".")]
    [SerializeField] private TextMeshProUGUI countLabel;

    private Button button;
    private const string SeenKey = "path";

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
        PathOfKukulkan.OnTierReached += OnTier;
        Refresh();
    }

    // OnEnable can run before LocalizationManager.Awake on a fresh scene load, which leaves the raw
    // key on the label; Start runs after every Awake, and the language hook keeps it current.
    private void Start()
    {
        Refresh();
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged += Refresh;
        StartCoroutine(MenuRailPacing.AnnounceLater(MenuRailPacing.Feature.Path));
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= Refresh;
    }

    private void OnDisable()
    {
        PathOfKukulkan.OnTierReached -= OnTier;
    }

    private void OnTier(int tier, int jade) => Refresh();

    private void Refresh()
    {
        bool show = PathView.Available && MenuRailPacing.IsRevealed(MenuRailPacing.Feature.Path);
        MenuRailItem.SetShown(group, show);
        if (!show) return;

        if (cue != null) cue.Set(PathOfKukulkan.Tier > MenuRailCue.Seen(SeenKey) || MenuRailPacing.IsFresh(MenuRailPacing.Feature.Path));
        if (countLabel == null) return;

        countLabel.text = LocalizationManager.Get("path_tier_count", PathOfKukulkan.Tier, PathSettings.Active.TierCount);
    }

    private void OnPressed()
    {
        MenuRailCue.MarkSeen(SeenKey, PathOfKukulkan.Tier);
        MenuRailPacing.Introduced(MenuRailPacing.Feature.Path);
        if (cue != null) cue.Set(false);
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        PathView.Show();
    }
}
