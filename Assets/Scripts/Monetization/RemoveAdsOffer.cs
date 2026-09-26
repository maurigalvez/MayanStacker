using System;
using UnityEngine;

/// <summary>
/// The one way into the Remove Ads offer card, shared by every place that sells it:
/// the Settings button, the main-menu chip and the nudge that appears after an ad.
///
/// Every entry point opens the same <see cref="RemoveAdsOfferView"/>, so the price and the
/// terms are always on screen before Google Play can charge anything. Each one passes its
/// <c>source</c>, which the offer and purchase analytics carry, so conversions can be
/// compared per placement.
///
/// It also owns the pacing of the post-ad nudge. The nudge is only relevant to someone who has
/// actually sat through ads, and it must not nag, so it waits for
/// <see cref="AdsBeforeNudge"/> interstitials and then shows at most once per session.
/// </summary>
public static class RemoveAdsOffer
{
    public const string SourceSettings = "settings";
    public const string SourceMainMenu = "main_menu";
    public const string SourceResultCard = "result_card";

    /// <summary>Interstitials a player must have seen before the post-ad nudge first appears.</summary>
    public const int AdsBeforeNudge = 3;

    private const string AdsSeenKey = "RemoveAds_AdsSeen";
    private const string NudgeSessionKey = "RemoveAds_NudgeSession";

    /// <summary>
    /// Raised on the main thread when an interstitial closes and the nudge is due. The nudge
    /// view listens for it; the pacing has already been recorded when it fires.
    /// </summary>
    public static event Action NudgeDue;

    /// <summary>Interstitials this install has shown, lifetime.</summary>
    public static int AdsSeen => PlayerPrefs.GetInt(AdsSeenKey, 0);

    /// <summary>
    /// True when the offer can be opened right now: not owned, and Play has answered with a
    /// price. The card never opens without one, since stating the price is the point of it.
    /// </summary>
    public static bool Available
    {
        get
        {
            if (RemoveAds.Owned) return false;
            var purchases = PurchaseManager.Instance;
            return purchases != null && purchases.CanPurchase && !string.IsNullOrEmpty(purchases.RemoveAdsPrice);
        }
    }

    /// <summary>
    /// Opens the offer card for <paramref name="source"/>. Returns false when the store isn't
    /// reachable (it also nudges the store to reconnect), so the caller can say so.
    /// </summary>
    public static bool TryOpen(string source)
    {
        var purchases = PurchaseManager.Instance;
        if (!Available)
        {
            if (purchases != null) purchases.EnsureConnected();
            return false;
        }

        RemoveAdsOfferView.Show(purchases.RemoveAdsPrice, source, () =>
        {
            if (PurchaseManager.Instance != null) PurchaseManager.Instance.BuyRemoveAds(source);
        });
        return true;
    }

    /// <summary>
    /// AdManager calls this on the main thread when an interstitial closes. Counts the ad and,
    /// if the nudge is due, records it for this session and raises <see cref="NudgeDue"/>.
    /// </summary>
    public static void NotifyInterstitialClosed()
    {
        int seen = AdsSeen + 1;
        PlayerPrefs.SetInt(AdsSeenKey, seen);

        bool due = seen >= AdsBeforeNudge
                   && PlayerPrefs.GetInt(NudgeSessionKey, 0) != FtueState.SessionNumber
                   && Available;

        if (due) PlayerPrefs.SetInt(NudgeSessionKey, FtueState.SessionNumber);
        PlayerPrefs.Save();

        if (due) NudgeDue?.Invoke();
    }

    /// <summary>For PlayerDataReset: forget the ad count and the last nudge.</summary>
    public static void ResetPacing()
    {
        PlayerPrefs.DeleteKey(AdsSeenKey);
        PlayerPrefs.DeleteKey(NudgeSessionKey);
    }
}
