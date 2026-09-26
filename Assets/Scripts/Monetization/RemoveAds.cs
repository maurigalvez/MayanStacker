using System;
using UnityEngine;

/// <summary>
/// Whether this player owns the one-time "Remove Ads" purchase.
///
/// Google Play is the source of truth: <see cref="PurchaseManager"/> asks it on every launch
/// and calls <see cref="Grant"/> or <see cref="Revoke"/> with the answer. This class is only
/// the local cache of that answer, so ads stay off offline and before Play has replied. It is
/// deliberately not part of the cloud save or the leaderboard season reset: a purchase belongs
/// to the Google account, and Play restores it on a reinstall or a new device by itself.
/// </summary>
public static class RemoveAds
{
    public const string ProductId = "remove_ads";

    private const string OwnedKey = "RemoveAds_Owned";

    /// <summary>Raised with the new value whenever ownership changes.</summary>
    public static event Action<bool> Changed;

    public static bool Owned => PlayerPrefs.GetInt(OwnedKey, 0) == 1;

    /// <summary>
    /// Records ownership and saves immediately. PurchaseManager only confirms the purchase
    /// with Play after this returns, so a crash in between re-delivers it instead of losing it.
    /// </summary>
    public static void Grant()
    {
        if (Owned) return;
        PlayerPrefs.SetInt(OwnedKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[RemoveAds] Granted — interstitials are off.");
        Changed?.Invoke(true);
    }

    /// <summary>Play no longer reports the purchase (refunded, or a different Google account).</summary>
    public static void Revoke()
    {
        if (!Owned) return;
        PlayerPrefs.DeleteKey(OwnedKey);
        PlayerPrefs.Save();
        Debug.Log("[RemoveAds] Revoked — Play no longer reports the purchase.");
        Changed?.Invoke(false);
    }

    /// <summary>
    /// Clears the local cache only — for PlayerDataReset. On a Play build the purchase comes
    /// straight back on the next launch, which is the point.
    /// </summary>
    public static void ResetCache()
    {
        PlayerPrefs.DeleteKey(OwnedKey);
    }
}
