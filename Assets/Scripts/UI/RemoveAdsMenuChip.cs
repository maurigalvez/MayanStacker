using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The small "No Ads" chip on the main menu: the always-findable way into the Remove Ads
/// offer, next to the louder moment after an ad (<see cref="RemoveAdsNudgeView"/>). Tapping it
/// opens the same offer card as Settings.
///
/// It is only visible when the offer makes sense: not owned, the store has a price, and the
/// FTUE ad grace period is over (offering to remove ads a new player hasn't seen yet only
/// tells them ads are coming). It fades itself out with a CanvasGroup rather than
/// deactivating, so it keeps checking and comes back when those change.
///
/// Built into the Main Menu Canvas prefab by TamalStacker ▸ Monetization ▸ Add Remove Ads
/// Menu Chip.
/// </summary>
[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class RemoveAdsMenuChip : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    private Button button;
    private CanvasGroup group;
    private float nextRefresh;
    private bool visible = true;

    private void Awake()
    {
        button = GetComponent<Button>();
        group = GetComponent<CanvasGroup>();
        button.onClick.AddListener(OnPressed);
    }

    private void OnEnable()
    {
        nextRefresh = 0f;
        Refresh();
    }

    // Ownership, the store's price and the grace period can all change while the menu is
    // open; a cheap twice-a-second check covers them without subscribing to each.
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        Refresh();
    }

    private void Refresh()
    {
        nextRefresh = Time.unscaledTime + RefreshInterval;
        SetVisible(FtueState.AdsAllowed && RemoveAdsOffer.Available);
    }

    private void SetVisible(bool show)
    {
        if (show == visible) return;
        visible = show;
        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
    }

    private void OnPressed()
    {
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();

        // Unavailable means the store dropped since the last check: hide until it's back.
        if (!RemoveAdsOffer.TryOpen(RemoveAdsOffer.SourceMainMenu)) Refresh();
    }
}
