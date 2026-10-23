using System;
using UnityEngine;

/// <summary>
/// Tuning for the post-run Offering reveal: a tap-to-open bonus on top of the jade a run already
/// paid. The base payout is never at risk; the reveal only adds a small, occasionally generous,
/// extra. Jade is cosmetic currency, so the roll can't touch a ranked score.
///
/// An asset in Resources; if it is missing, or <see cref="enableReveal"/> is cleared, no reveal is
/// offered and the result card is exactly as before.
/// </summary>
[CreateAssetMenu(fileName = "RunRevealSettings", menuName = "TamalStacker/Run Reveal Settings", order = 10)]
public class RunRevealSettings : ScriptableObject
{
    public const string ResourcePath = "RunRevealSettings";

    [Serializable]
    public struct Tier
    {
        [Tooltip("Localization key for the tier's name.")]
        public string nameKey;
        [Min(0f)] public float weight;
        [Tooltip("Extra jade this tier adds on top of the run's payout.")]
        [Min(0)] public int bonusJade;
        [Tooltip("Heavy haptic for this tier.")]
        public bool grand;
    }

    private static RunRevealSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or the reveal is off.</summary>
    public static RunRevealSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<RunRevealSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableReveal ? cached : null;
        }
    }

    [Header("Master switch")]
    public bool enableReveal = true;

    [Header("Rules")]
    [Tooltip("Runs that paid less than this much jade don't offer a reveal.")]
    [Min(1)] public int minRunJade = 1;

    [Header("Double jade (opt-in rewarded ad)")]
    [Tooltip("After opening, offer to double the run's jade. Remove Ads owners get it free; everyone else watches a rewarded ad.")]
    public bool enableDoubleJade = true;
    [Tooltip("Share of the run's jade (run payout + opened bonus) the double adds. 1 = doubles it.")]
    [Range(0f, 1f)] public float doubleFraction = 1f;
    [Tooltip("The most one double can add, so a long run's cap-sized payout can't snowball.")]
    [Min(1)] public int maxDoubleBonus = 60;

    /// <summary>Jade the double would add for a run that paid <paramref name="runJade"/> plus <paramref name="bonus"/>.</summary>
    public int DoubleAmount(int runJade, int bonus)
    {
        return Mathf.Min(maxDoubleBonus, Mathf.RoundToInt((runJade + bonus) * doubleFraction));
    }

    [Header("Bonus table")]
    [Tooltip("Rolled once when the offering is opened, or silently when the card is dismissed unopened.")]
    public Tier[] tiers =
    {
        new Tier { nameKey = "reveal_tier_plain",   weight = 55f, bonusJade = 0,  grand = false },
        new Tier { nameKey = "reveal_tier_blessed", weight = 30f, bonusJade = 3,  grand = false },
        new Tier { nameKey = "reveal_tier_radiant", weight = 12f, bonusJade = 8,  grand = false },
        new Tier { nameKey = "reveal_tier_gift",    weight = 3f,  bonusJade = 20, grand = true  }
    };

    /// <summary>Picks a tier by weight. <paramref name="roll01"/> is a uniform number in [0,1).</summary>
    public Tier Roll(float roll01)
    {
        if (tiers == null || tiers.Length == 0) return default;

        float total = 0f;
        foreach (var t in tiers) total += Mathf.Max(0f, t.weight);
        if (total <= 0f) return tiers[0];

        float pick = Mathf.Clamp01(roll01) * total;
        foreach (var t in tiers)
        {
            pick -= Mathf.Max(0f, t.weight);
            if (pick < 0f) return t;
        }
        return tiers[tiers.Length - 1];
    }
}
