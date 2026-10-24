using UnityEngine;

/// <summary>
/// The daily login chest. One claim per UTC day (the same server-corrected day number the Ritual of
/// the Sun uses). Consecutive claims climb a reward cycle; one missed day is forgiven per
/// <see cref="LoginChestSettings.forgiveCooldownDays"/>, anything longer restarts at day 1.
///
/// The chest pays through <see cref="JadeWallet.Earn"/> and never touches a score. State is local
/// (PlayerPrefs).
/// </summary>
public static class LoginChest
{
    private const string StreakKey = "LoginChest_Streak";
    private const string LastClaimKey = "LoginChest_LastClaim";
    private const string ForgiveKey = "LoginChest_ForgiveDay";

    // The chest pays jade, so it needs the jade economy on as well.
    public static bool Available => LoginChestSettings.Active != null && JadeSettings.Active != null;

    private static int Today => DailyChallengeManager.CurrentDayNumberUtc();
    private static int LastClaim => PlayerPrefs.GetInt(LastClaimKey, int.MinValue);

    public static bool CanClaim => Available && LastClaim != Today;

    /// <summary>The consecutive-day count the next claim will have (1 after a lapse).</summary>
    public static int NextStreak
    {
        get
        {
            int last = LastClaim;
            int streak = PlayerPrefs.GetInt(StreakKey, 0);
            if (last == int.MinValue || streak <= 0) return 1;
            int gap = Today - last;
            if (gap <= 0) return streak;
            if (gap == 1) return streak + 1;
            if (gap == 2 && ForgiveReady) return streak + 1;
            return 1;
        }
    }

    /// <summary>Streak shown for the chest: what is held today, or what the next claim will be.</summary>
    public static int DisplayStreak => CanClaim ? NextStreak : PlayerPrefs.GetInt(StreakKey, 0);

    public static int CycleLength
    {
        get
        {
            var s = LoginChestSettings.Active;
            return s != null && s.cycleRewards != null ? s.cycleRewards.Length : 0;
        }
    }

    /// <summary>1-based position in the reward cycle for a streak count.</summary>
    public static int CycleDay(int streak)
    {
        int len = CycleLength;
        return len == 0 ? 1 : (Mathf.Max(1, streak) - 1) % len + 1;
    }

    /// <summary>
    /// Jade the chest pays for a given streak count, Ritual streak bonus and the release's welcome-back
    /// multiplier included. <paramref name="dayOffset"/> asks about a chest opened that many days from today.
    /// </summary>
    public static int RewardFor(int streak, int dayOffset = 0)
    {
        var s = LoginChestSettings.Active;
        if (s == null) return 0;
        int bonus = Mathf.Min(DailyStreak.Current * Mathf.Max(0, s.bonusPerRitualStreakDay), Mathf.Max(0, s.ritualBonusCap));
        return (s.RewardForDay(CycleDay(streak)) + bonus) * ReleaseAnnouncement.ChestMultiplier(dayOffset);
    }

    private static bool ForgiveReady
    {
        get
        {
            var s = LoginChestSettings.Active;
            int cooldown = s != null ? Mathf.Max(1, s.forgiveCooldownDays) : 7;
            int last = PlayerPrefs.GetInt(ForgiveKey, int.MinValue);
            return last == int.MinValue || Today - last >= cooldown;
        }
    }

    /// <summary>Opens today's chest. Returns the jade paid, 0 when there is nothing to claim.</summary>
    public static int Claim()
    {
        if (!CanClaim) return 0;

        int last = LastClaim;
        int streak = NextStreak;
        if (last != int.MinValue && Today - last == 2 && streak > 1) PlayerPrefs.SetInt(ForgiveKey, Today);

        int amount = JadeWallet.Earn("login_chest", RewardFor(streak));
        PlayerPrefs.SetInt(StreakKey, streak);
        PlayerPrefs.SetInt(LastClaimKey, Today);
        PlayerPrefs.Save();
        return amount;
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(StreakKey);
        PlayerPrefs.DeleteKey(LastClaimKey);
        PlayerPrefs.DeleteKey(ForgiveKey);
        PlayerPrefs.Save();
    }
}
