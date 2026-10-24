using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Path of Kukulkan: a free mastery track filled by playing. Finished runs add XP; each tier
/// crossed pays its jade through <see cref="JadeWallet.Earn"/> and grants its cosmetic, with no
/// claim step so the menu stays quiet. XP is lifetime and local (PlayerPrefs), so tiers are
/// recomputed from it and a tuning change can't strand a player.
///
/// The Path only reads what a run already did; nothing here touches a score.
/// </summary>
public static class PathOfKukulkan
{
    private const string XpKey = "Path_Xp";
    private const string PaidKey = "Path_PaidTier";

    /// <summary>Set once the player has opened the Path and been shown how it works.</summary>
    public const string IntroSeenKey = "Path_IntroSeen";

    /// <summary>What a finished run did. Gathered by GameManager at game over.</summary>
    public struct RunStats
    {
        public int blocks;
        public int perfects;
        public int edgeLandings;
        public int surges;
        public bool dailyComplete;
    }

    /// <summary>Raised after a tier is reached and paid: tier number (1-based), jade paid.</summary>
    public static event Action<int, int> OnTierReached;

    public static bool Available => PathSettings.Active != null;

    public static int Xp => PlayerPrefs.GetInt(XpKey, 0);

    /// <summary>Highest tier whose reward has been paid.</summary>
    public static int PaidTier => PlayerPrefs.GetInt(PaidKey, 0);

    /// <summary>Tier reached by the current XP (0 before the first).</summary>
    public static int Tier
    {
        get
        {
            var s = PathSettings.Active;
            return s == null ? 0 : TierForXp(s, Xp, out _);
        }
    }

    public static bool Maxed => Available && Tier >= PathSettings.Active.TierCount;

    /// <summary>XP gained inside the current tier and the XP that tier needs (both 0 once maxed).</summary>
    public static void Progress(out int into, out int needed)
    {
        into = needed = 0;
        var s = PathSettings.Active;
        if (s == null) return;
        int tier = TierForXp(s, Xp, out int remainder);
        if (tier >= s.TierCount) return;
        into = remainder;
        needed = s.XpForTier(tier + 1);
    }

    private static int TierForXp(PathSettings s, int xp, out int remainder)
    {
        int tier = 0;
        remainder = xp;
        while (tier < s.TierCount)
        {
            int cost = s.XpForTier(tier + 1);
            if (remainder < cost) break;
            remainder -= cost;
            tier++;
        }
        if (tier >= s.TierCount) remainder = 0;
        return tier;
    }

    /// <summary>XP a finished run is worth, 0 when it was too short or the Path is off.</summary>
    public static int XpFor(RunStats run)
    {
        var s = PathSettings.Active;
        if (s == null || run.blocks < s.minStonesToCount) return 0;
        int xp = s.xpPerRun + run.blocks * s.xpPerStone + run.perfects * s.xpPerPerfect +
                 run.edgeLandings * s.xpPerEdgeLanding + run.surges * s.xpPerSunSurge;
        if (run.dailyComplete) xp += s.xpDailyComplete;
        return Mathf.Max(0, xp);
    }

    /// <summary>XP the run that just ended is worth, for the result card. 0 when it earned none.</summary>
    public static int LastRunXp { get; private set; }

    /// <summary>XP still missing for the next tier after that run, or 0 when it crossed one (or none is left).</summary>
    public static int LastRunXpToNext { get; private set; }

    /// <summary>The tier that run was heading for.</summary>
    public static int LastRunNextTier { get; private set; }

    /// <summary>
    /// Notes what a finished run is worth without recording it, so the result card can say "Path +46 XP"
    /// before <see cref="RecordRun"/> applies it. Call it before the card is built.
    /// </summary>
    public static void NoteRun(RunStats run)
    {
        LastRunXp = Available ? XpFor(run) : 0;
        LastRunXpToNext = 0;
        LastRunNextTier = Tier + 1;
        if (LastRunXp <= 0) return;

        Progress(out int into, out int needed);
        if (needed > 0) LastRunXpToNext = Mathf.Max(0, needed - into - LastRunXp);
    }

    /// <summary>The next tier to reach and what it pays, or false once the Path is finished.</summary>
    public static bool NextReward(out int tier, out int jade, out int xpToGo)
    {
        tier = jade = xpToGo = 0;
        var s = PathSettings.Active;
        if (s == null || Tier >= s.TierCount) return false;

        Progress(out int into, out int needed);
        tier = Tier + 1;
        jade = s.tiers[tier - 1].jade;
        xpToGo = Mathf.Max(0, needed - into);
        return true;
    }

    /// <summary>Adds the run's XP and pays any tiers it crosses. Returns the XP added.</summary>
    public static int RecordRun(RunStats run)
    {
        int xp = XpFor(run);
        if (xp <= 0) return 0;

        PlayerPrefs.SetInt(XpKey, Xp + xp);
        PlayerPrefs.Save();
        PayPendingTiers();
        return xp;
    }

    /// <summary>Pays every tier reached but not yet paid, in order, once each.</summary>
    public static void PayPendingTiers()
    {
        var s = PathSettings.Active;
        if (s == null) return;

        int reached = Tier;
        var rewarded = new List<KeyValuePair<int, int>>();
        for (int tier = PaidTier + 1; tier <= reached; tier++)
        {
            PathSettings.Tier reward = s.tiers[tier - 1];
            PlayerPrefs.SetInt(PaidKey, tier);
            int jade = JadeWallet.Earn("path", reward.jade);
            if (!string.IsNullOrEmpty(reward.cosmeticId)) CosmeticStore.Grant(reward.cosmeticId);
            rewarded.Add(new KeyValuePair<int, int>(tier, jade));
        }
        if (rewarded.Count == 0) return;
        PlayerPrefs.Save();

        foreach (var r in rewarded)
        {
            GameAnalytics.Track("path_tier", new Dictionary<string, object>
            {
                { "tier", r.Key },
                { "jade", r.Value }
            });
            OnTierReached?.Invoke(r.Key, r.Value);
        }

        var last = rewarded[rewarded.Count - 1];
        RunBanner.Show(LocalizationManager.Get("path_tier_up", last.Key),
            LocalizationManager.Get("path_tier_reward", last.Value),
            new Color(0.95f, 0.8f, 0.3f));
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(XpKey);
        PlayerPrefs.DeleteKey(PaidKey);
        PlayerPrefs.DeleteKey(IntroSeenKey);
        PlayerPrefs.Save();
    }
}
