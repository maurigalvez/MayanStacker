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
    private CanvasGroup group;

    private void Awake()
    {
        button = GetComponent<Button>();
        group = GetComponent<CanvasGroup>();
        button.onClick.AddListener(OnPressed);
    }

    private void OnEnable()
    {
        PathOfKukulkan.OnTierReached += OnTier;
        Refresh();
    }

    private void OnDisable()
    {
        PathOfKukulkan.OnTierReached -= OnTier;
    }

    private void OnTier(int tier, int jade) => Refresh();

    private void Refresh()
    {
        bool show = PathView.Available;
        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
        if (!show || countLabel == null) return;

        countLabel.text = LocalizationManager.Get("path_tier_count", PathOfKukulkan.Tier, PathSettings.Active.TierCount);
    }

    private void OnPressed()
    {
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        PathView.Show();
    }
}
