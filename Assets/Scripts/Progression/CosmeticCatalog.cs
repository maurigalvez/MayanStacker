using System;
using UnityEngine;

/// <summary>What a cosmetic changes. One item per kind can be equipped at a time.</summary>
public enum CosmeticKind
{
    /// <summary>Colour of the full-screen flash when Kukulkan shifts the sky.</summary>
    KukulkanShift = 0,
    /// <summary>Colour of the dust burst when a stone lands.</summary>
    LandingBurst = 1,
}

[Serializable]
public class CosmeticItem
{
    [Tooltip("Stable id saved to the player's data. Never rename once shipped.")]
    public string id;
    public CosmeticKind kind;
    [Tooltip("Localization key for the display name.")]
    public string nameKey;
    [Tooltip("Jade price. 0 = owned from the start (the default look of its kind).")]
    [Min(0)] public int price;
    [Tooltip("Main colour. Kukulkan shift: flash colour (alpha is ignored, the flash keeps its own). Landing burst: first colour of the range.")]
    public Color color = Color.white;
    [Tooltip("Landing burst only: second colour; each puff picks between the two.")]
    public Color color2 = Color.white;
    [Tooltip("The game's own look (free default). Equipping it applies no override, so themes keep their tint.")]
    public bool keepGameLook;
    [Tooltip("Optional picture for the market card. Empty until art exists.")]
    public Sprite icon;
}

/// <summary>
/// Everything the jade market can sell. Cosmetic only: no item changes a score, a window or a
/// speed, so ranked boards stay fair. Prices here are proposals until the user signs off.
///
/// An asset in Resources; missing or switched off, every cosmetic is ignored and the game looks
/// as it did before. Owned items and picks are kept by <see cref="CosmeticStore"/>.
/// </summary>
[CreateAssetMenu(fileName = "CosmeticCatalog", menuName = "TamalStacker/Cosmetic Catalog", order = 10)]
public class CosmeticCatalog : ScriptableObject
{
    public const string ResourcePath = "CosmeticCatalog";

    private static CosmeticCatalog cached;
    private static bool loaded;

    /// <summary>The active catalog, or null when the asset is missing or cosmetics are off.</summary>
    public static CosmeticCatalog Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<CosmeticCatalog>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableCosmetics ? cached : null;
        }
    }

    [Header("Master switch")]
    public bool enableCosmetics = true;

    public CosmeticItem[] items = new CosmeticItem[0];

    public CosmeticItem Find(string id)
    {
        if (string.IsNullOrEmpty(id) || items == null) return null;
        foreach (CosmeticItem item in items)
        {
            if (item != null && item.id == id) return item;
        }
        return null;
    }

    /// <summary>The free item of a kind, used when nothing else is equipped.</summary>
    public CosmeticItem DefaultFor(CosmeticKind kind)
    {
        if (items == null) return null;
        foreach (CosmeticItem item in items)
        {
            if (item != null && item.kind == kind && item.price <= 0) return item;
        }
        return null;
    }
}
