using System;
using UnityEngine;

/// <summary>
/// The player's jade: a soft currency earned by playing and spent on cosmetics.
///
/// Stored as two lifetime totals (earned, spent) rather than a balance. Two devices that both
/// earned and spent can then merge by taking the larger of each total, which never loses
/// progress and never duplicates a purchase's cost. The balance is earned minus spent.
///
/// PlayerPrefs-backed and synced through <see cref="PlayerProgressData"/>. It is deliberately
/// not touched by the leaderboard season reset: jade is not a score. Every gain goes through
/// <see cref="Earn"/>, so rewarded ads or other sources can plug in without a rewrite.
/// </summary>
public static class JadeWallet
{
    public const string EarnedKey = "Jade_LifetimeEarned";
    public const string SpentKey = "Jade_LifetimeSpent";

    public static int LifetimeEarned => PlayerPrefs.GetInt(EarnedKey, 0);
    public static int LifetimeSpent => PlayerPrefs.GetInt(SpentKey, 0);
    public static int Balance => Math.Max(0, LifetimeEarned - LifetimeSpent);

    /// <summary>Jade earned since the current run began. The result card reads this.</summary>
    public static int RunEarned { get; private set; }

    /// <summary>Raised after the balance changes, with the new balance.</summary>
    public static event Action<int> OnBalanceChanged;

    /// <summary>Clears the per-run tally. Called when a run starts or restarts.</summary>
    public static void BeginRun()
    {
        RunEarned = 0;
    }

    /// <summary>
    /// Adds jade. <paramref name="source"/> is a short analytics slug ("run", "stars", "daily").
    /// Returns the amount actually granted, 0 when jade is switched off.
    /// </summary>
    public static int Earn(string source, int amount)
    {
        if (amount <= 0 || JadeSettings.Active == null) return 0;

        PlayerPrefs.SetInt(EarnedKey, LifetimeEarned + amount);
        PlayerPrefs.Save();
        RunEarned += amount;

        GameAnalytics.JadeEarned(source, amount, Balance);
        OnBalanceChanged?.Invoke(Balance);
        return amount;
    }

    /// <summary>Pays for an item. False, and nothing changes, when the balance can't cover it.</summary>
    public static bool TrySpend(string item, int cost)
    {
        if (cost < 0 || cost > Balance) return false;

        PlayerPrefs.SetInt(SpentKey, LifetimeSpent + cost);
        PlayerPrefs.Save();

        GameAnalytics.JadeSpent(item, cost, Balance);
        OnBalanceChanged?.Invoke(Balance);
        return true;
    }

    /// <summary>Copies the local totals into outgoing cloud data.</summary>
    public static void WriteTo(PlayerProgressData data)
    {
        if (data == null) return;
        data.jadeEarned = LifetimeEarned;
        data.jadeSpent = LifetimeSpent;
    }

    /// <summary>
    /// Adopts the cloud totals where they are larger. Larger local totals are kept and reach the
    /// cloud on the next save, because <see cref="WriteTo"/> reads from here.
    /// </summary>
    public static void MergeFromCloud(PlayerProgressData data)
    {
        if (data == null) return;

        bool changed = false;
        if (data.jadeEarned > LifetimeEarned)
        {
            PlayerPrefs.SetInt(EarnedKey, data.jadeEarned);
            changed = true;
        }
        if (data.jadeSpent > LifetimeSpent)
        {
            PlayerPrefs.SetInt(SpentKey, data.jadeSpent);
            changed = true;
        }

        if (!changed) return;
        PlayerPrefs.Save();
        OnBalanceChanged?.Invoke(Balance);
    }

    /// <summary>Forgets all jade. Used by full data resets only, never by a season reset.</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(EarnedKey);
        PlayerPrefs.DeleteKey(SpentKey);
        JadeRunPayout.ResetAll();
        RunEarned = 0;
    }
}

/// <summary>
/// Works out and pays what a finished run earned. Pure arithmetic on the run's numbers plus the
/// once-a-day Daily flags; the amounts all come from <see cref="JadeSettings"/>.
/// </summary>
public static class JadeRunPayout
{
    private const string DailyPaidKey = "Jade_DailyPaidDay";
    private const string DailyCompleteKey = "Jade_DailyCompleteDay";

    public struct Tally
    {
        public int blocks;
        public int perfects;
        public int edgeLandings;
        public int surges;
    }

    /// <summary>What a run with this tally pays, after the per-run cap. 0 when it fell too early.</summary>
    public static int Compute(Tally t, JadeSettings s)
    {
        if (s == null || t.blocks < s.minStonesToPay) return 0;

        int total = t.perfects * s.perPerfect
                  + t.edgeLandings * s.perEdgeLanding
                  + (t.blocks / Mathf.Max(1, s.heightStepStones)) * s.perHeightStep
                  + t.surges * s.perSunSurge;
        return Mathf.Clamp(total, 0, s.maxPerRun);
    }

    /// <summary>Pays the run's jade. Returns the amount granted.</summary>
    public static int AwardRun(Tally t)
    {
        var s = JadeSettings.Active;
        if (s == null) return 0;
        return JadeWallet.Earn("run", Compute(t, s));
    }

    /// <summary>
    /// Daily participation: once per UTC day, plus the completion bonus once per day when the
    /// ritual finished (it can arrive on a later run of a day already paid for taking part).
    /// </summary>
    public static int AwardDaily(bool completed)
    {
        var s = JadeSettings.Active;
        if (s == null) return 0;

        int granted = 0;
        int today = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);

        if (PlayerPrefs.GetInt(DailyPaidKey, -1) < today)
        {
            granted += JadeWallet.Earn("daily", s.dailyParticipation);
            PlayerPrefs.SetInt(DailyPaidKey, today);
        }

        if (completed && PlayerPrefs.GetInt(DailyCompleteKey, -1) < today)
        {
            granted += JadeWallet.Earn("daily_complete", s.dailyComplete);
            PlayerPrefs.SetInt(DailyCompleteKey, today);
        }
        return granted;
    }

    /// <summary>Pays for stars gained for the first time on a temple.</summary>
    public static int AwardStars(int newStars)
    {
        var s = JadeSettings.Active;
        if (s == null || newStars <= 0) return 0;
        return JadeWallet.Earn("stars", newStars * s.perNewStar);
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(DailyPaidKey);
        PlayerPrefs.DeleteKey(DailyCompleteKey);
    }
}
