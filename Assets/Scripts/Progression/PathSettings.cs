using UnityEngine;

/// <summary>
/// Tuning for the Path of Kukulkan: a free mastery track. Every finished run earns XP; each tier
/// reached pays jade (and, on a few tiers, a cosmetic) automatically. Free lane only, no purchases.
///
/// An asset in Resources; if it is missing, or <see cref="enablePath"/> is cleared, no XP is
/// tracked and the game plays exactly as before.
/// </summary>
[CreateAssetMenu(fileName = "PathSettings", menuName = "TamalStacker/Path Settings", order = 14)]
public class PathSettings : ScriptableObject
{
    public const string ResourcePath = "PathSettings";

    [System.Serializable]
    public struct Tier
    {
        [Tooltip("Jade paid when this tier is reached.")]
        public int jade;
        [Tooltip("Optional CosmeticCatalog item id granted free on this tier. Empty for none.")]
        public string cosmeticId;
    }

    private static PathSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or the Path is off.</summary>
    public static PathSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<PathSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enablePath ? cached : null;
        }
    }

    [Tooltip("Master switch. Off: no XP, no tiers, no menu chip.")]
    public bool enablePath = true;

    [Header("XP per run")]
    [Tooltip("A run shorter than this many stones earns nothing (no XP for instant fails).")]
    public int minStonesToCount = 3;
    public int xpPerRun = 10;
    public int xpPerStone = 2;
    public int xpPerPerfect = 3;
    public int xpPerEdgeLanding = 2;
    public int xpPerSunSurge = 10;
    [Tooltip("Extra XP for finishing a Ritual of the Sun target.")]
    public int xpDailyComplete = 30;

    [Header("Tier curve")]
    [Tooltip("XP to climb from tier 0 to tier 1.")]
    public int firstTierXp = 60;
    [Tooltip("Each later tier costs this much more than the one before.")]
    public int xpStepPerTier = 10;

    [Header("Rewards (index 0 = tier 1)")]
    public Tier[] tiers = new Tier[0];

    public int TierCount => tiers != null ? tiers.Length : 0;

    /// <summary>XP needed to go from tier (tier-1) to <paramref name="tier"/> (1-based).</summary>
    public int XpForTier(int tier)
    {
        return Mathf.Max(1, firstTierXp + Mathf.Max(0, tier - 1) * xpStepPerTier);
    }
}
