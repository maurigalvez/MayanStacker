using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player owns and wears. PlayerPrefs-backed and synced through
/// <see cref="PlayerProgressData.ownedCosmetics"/> / <see cref="PlayerProgressData.equippedCosmetics"/>:
/// ownership is a union, so a sync can never take a purchase away.
///
/// Buying goes through <see cref="JadeWallet.TrySpend"/>, the only place jade leaves the wallet.
/// Free items (price 0) count as owned without a flag.
/// </summary>
public static class CosmeticStore
{
    private const string OwnedPrefix = "Cosmetic_Owned_";
    private const string EquippedPrefix = "Cosmetic_Equipped_";

    /// <summary>Raised after a purchase or an equip change.</summary>
    public static event Action OnChanged;

    public static bool IsOwned(CosmeticItem item)
    {
        if (item == null) return false;
        return item.price <= 0 || PlayerPrefs.GetInt(OwnedPrefix + item.id, 0) == 1;
    }

    /// <summary>Spends the jade and marks the item owned. False when it can't be afforded.</summary>
    public static bool TryBuy(CosmeticItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.id)) return false;
        if (IsOwned(item)) return true;
        if (!JadeWallet.TrySpend("cosmetic_" + item.id, item.price)) return false;

        PlayerPrefs.SetInt(OwnedPrefix + item.id, 1);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Wears an owned item. Ignored for an item that isn't owned.</summary>
    public static void Equip(CosmeticItem item)
    {
        if (item == null || !IsOwned(item)) return;
        PlayerPrefs.SetString(EquippedPrefix + item.kind, item.id);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// The item worn for <paramref name="kind"/>: the saved pick if still owned, otherwise the
    /// kind's free default, otherwise null (the game's own look).
    /// </summary>
    public static CosmeticItem Equipped(CosmeticKind kind)
    {
        CosmeticCatalog catalog = CosmeticCatalog.Active;
        if (catalog == null) return null;

        CosmeticItem picked = catalog.Find(PlayerPrefs.GetString(EquippedPrefix + kind, ""));
        if (picked != null && picked.kind == kind && IsOwned(picked)) return picked;
        return catalog.DefaultFor(kind);
    }

    // ---- Cloud sync ----

    public static void WriteTo(PlayerProgressData data)
    {
        CosmeticCatalog catalog = CosmeticCatalog.Active;
        if (data == null || catalog == null) return;

        data.ownedCosmetics = new List<string>();
        data.equippedCosmetics = new List<string>();
        foreach (CosmeticItem item in catalog.items)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;
            if (PlayerPrefs.GetInt(OwnedPrefix + item.id, 0) == 1) data.ownedCosmetics.Add(item.id);
        }
        foreach (CosmeticKind kind in Enum.GetValues(typeof(CosmeticKind)))
        {
            string id = PlayerPrefs.GetString(EquippedPrefix + kind, "");
            if (!string.IsNullOrEmpty(id)) data.equippedCosmetics.Add(kind + ":" + id);
        }
    }

    /// <summary>Adopts every purchase the cloud knows; adopts picks only where none is set here.</summary>
    public static void MergeFromCloud(PlayerProgressData data)
    {
        if (data == null) return;

        bool changed = false;
        if (data.ownedCosmetics != null)
        {
            foreach (string id in data.ownedCosmetics)
            {
                if (string.IsNullOrEmpty(id) || PlayerPrefs.GetInt(OwnedPrefix + id, 0) == 1) continue;
                PlayerPrefs.SetInt(OwnedPrefix + id, 1);
                changed = true;
            }
        }
        if (data.equippedCosmetics != null)
        {
            foreach (string entry in data.equippedCosmetics)
            {
                int colon = entry == null ? -1 : entry.IndexOf(':');
                if (colon <= 0) continue;
                if (!Enum.TryParse(entry.Substring(0, colon), out CosmeticKind kind)) continue;
                if (!string.IsNullOrEmpty(PlayerPrefs.GetString(EquippedPrefix + kind, ""))) continue;
                PlayerPrefs.SetString(EquippedPrefix + kind, entry.Substring(colon + 1));
                changed = true;
            }
        }

        if (!changed) return;
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>Forgets every purchase and pick. Full data resets only.</summary>
    public static void ResetAll()
    {
        CosmeticCatalog catalog = CosmeticCatalog.Active;
        if (catalog != null && catalog.items != null)
        {
            foreach (CosmeticItem item in catalog.items)
            {
                if (item != null && !string.IsNullOrEmpty(item.id)) PlayerPrefs.DeleteKey(OwnedPrefix + item.id);
            }
        }
        foreach (CosmeticKind kind in Enum.GetValues(typeof(CosmeticKind)))
            PlayerPrefs.DeleteKey(EquippedPrefix + kind);
    }
}
