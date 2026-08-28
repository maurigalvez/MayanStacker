using UnityEngine;

/// <summary>
/// Consecutive-day streak for the Ritual of The Sun.
///
/// Keyed to the same server-corrected UTC day number the rest of the Daily system uses
/// (DailyChallengeManager.CurrentDayNumberUtc), so the streak can never disagree with the
/// reset countdown or the leaderboard the player is competing on.
///
/// A single missed day per week repairs itself instead of resetting the streak to zero.
/// That rule exists because the streak only works paired with a notification, and a streak
/// that dies on one busy day turns that notification into a source of guilt.
/// </summary>
public static class DailyStreak
{
    private const string PP_STREAK = "DailyStreak_Count";
    private const string PP_LAST_DAY = "DailyStreak_LastDay";
    private const string PP_BEST_STREAK = "DailyStreak_Best";
    private const string PP_LAST_REPAIR_DAY = "DailyStreak_LastRepairDay";

    /// <summary>Days between repairs. One forgiven miss per week.</summary>
    private const int RepairCooldownDays = 7;

    /// <summary>The player's current run of consecutive days.</summary>
    public static int Current => PlayerPrefs.GetInt(PP_STREAK, 0);

    /// <summary>The longest streak they've ever held.</summary>
    public static int Best => PlayerPrefs.GetInt(PP_BEST_STREAK, 0);

    private static int LastDay => PlayerPrefs.GetInt(PP_LAST_DAY, -1);

    /// <summary>
    /// True when the player has a live streak they haven't defended today — the state the
    /// reminder notification exists to address.
    /// </summary>
    public static bool IsAtRisk
    {
        get
        {
            if (Current <= 0) return false;
            return LastDay != DailyChallengeManager.CurrentDayNumberUtc();
        }
    }

    /// <summary>Whether a forgiven miss is currently available.</summary>
    public static bool RepairAvailable
    {
        get
        {
            int lastRepair = PlayerPrefs.GetInt(PP_LAST_REPAIR_DAY, int.MinValue);
            if (lastRepair == int.MinValue) return true;
            return DailyChallengeManager.CurrentDayNumberUtc() - lastRepair >= RepairCooldownDays;
        }
    }

    /// <summary>
    /// Record a completed Daily run against today. Idempotent within a day — replaying the
    /// ritual doesn't inflate the streak, matching the rule that the countdown is a window
    /// to achieve the objective rather than a one-shot lockout.
    /// </summary>
    /// <returns>The streak value after recording.</returns>
    public static int RecordCompletedRun()
    {
        int today = DailyChallengeManager.CurrentDayNumberUtc();
        int lastDay = LastDay;

        // Already counted today.
        if (lastDay == today) return Current;

        int streak;

        if (lastDay < 0)
        {
            streak = 1;
        }
        else
        {
            int gap = today - lastDay;

            if (gap == 1)
            {
                streak = Current + 1;
            }
            else if (gap == 2 && RepairAvailable)
            {
                // Exactly one missed day, and they have a repair to spend.
                streak = Current + 1;
                PlayerPrefs.SetInt(PP_LAST_REPAIR_DAY, today);
                Debug.Log("[DailyStreak] One missed day forgiven - streak preserved.");
            }
            else
            {
                streak = 1;
            }
        }

        PlayerPrefs.SetInt(PP_STREAK, streak);
        PlayerPrefs.SetInt(PP_LAST_DAY, today);

        if (streak > Best) PlayerPrefs.SetInt(PP_BEST_STREAK, streak);

        PlayerPrefs.Save();

        Debug.Log($"[DailyStreak] Streak is now {streak} (best {Best}).");
        return streak;
    }

    /// <summary>
    /// Recomputes whether the streak has lapsed, without recording a run. Call on app start
    /// so a returning player sees the truth rather than a stale number.
    /// </summary>
    public static void RefreshLapsedState()
    {
        int lastDay = LastDay;
        if (lastDay < 0 || Current <= 0) return;

        int gap = DailyChallengeManager.CurrentDayNumberUtc() - lastDay;

        // Still current, or inside the forgiven window.
        if (gap <= 1) return;
        if (gap == 2 && RepairAvailable) return;

        if (gap > 1)
        {
            Debug.Log($"[DailyStreak] Streak of {Current} lapsed after {gap} days.");
            PlayerPrefs.SetInt(PP_STREAK, 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Clears streak state. Used by PlayerDataReset.</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(PP_STREAK);
        PlayerPrefs.DeleteKey(PP_LAST_DAY);
        PlayerPrefs.DeleteKey(PP_BEST_STREAK);
        PlayerPrefs.DeleteKey(PP_LAST_REPAIR_DAY);
        PlayerPrefs.Save();
    }
}
