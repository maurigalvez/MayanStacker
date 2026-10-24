using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Show/hide for the medallions on the main-menu rail (Remove Ads, Chest, Missions, Expedition,
/// Path, Market). They fade with a CanvasGroup rather than deactivating, so their scripts keep
/// checking and bring them back; a hidden one also leaves the rail's layout so the rest close up
/// instead of leaving a hole.
///
/// The rail is built by TamalStacker ▸ UI ▸ Main Menu Rail ▸ Build Rail.
/// </summary>
public static class MenuRailItem
{
    public static void SetShown(CanvasGroup group, bool show)
    {
        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
        var layout = group.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = !show;
    }
}
