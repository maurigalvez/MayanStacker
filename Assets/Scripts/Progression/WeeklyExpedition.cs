using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Weekly Expedition: five stages, one at a time, across the UTC week (Monday to Sunday).
/// Finished runs advance the current stage; a stage pays its jade once through
/// <see cref="JadeWallet.Earn"/>, and the fifth also pays the finale and counts the week's badge.
/// Progress is local (PlayerPrefs) and starts over each week; the badge count is lifetime.
///
/// The Expedition only reads what a run already did; nothing here touches a score.
/// </summary>
public static class WeeklyExpedition
{
    private const string WeekKey = "Expedition_Week";
    private const string StageKey = "Expedition_Stage";
    private const string ProgressKey = "Expedition_Progress";
    private const string BadgesKey = "Expedition_Badges";

    /// <summary>What a finished run did. Gathered by GameManager at game over.</summary>
    public struct RunStats
    {
        public GameMode mode;
        public int blocks;
        public int perfects;
        public int maxCombo;
        public int edgeLandings;
        public int surges;
    }

    /// <summary>Raised after a stage is paid: stage index (0-based), jade paid.</summary>
    public static event Action<int, int> OnStageCompleted;

    public static bool Available => ExpeditionSettings.Active != null;

    /// <summary>Weeks run Monday to Sunday UTC (day 0 of the Unix epoch was a Thursday).</summary>
    public static int CurrentWeek => (MissionDay + 3) / 7;

    private static int MissionDay => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);

    /// <summary>Weeks whose five stages were all finished, ever.</summary>
    public static int Badges => PlayerPrefs.GetInt(BadgesKey, 0);

    /// <summary>Index of the stage in play this week; equals the stage count once the week is done.</summary>
    public static int Stage
    {
        get
        {
            var s = ExpeditionSettings.Active;
            if (s == null) return 0;
            RollWeek();
            return Mathf.Clamp(PlayerPrefs.GetInt(StageKey, 0), 0, s.stages.Length);
        }
    }

    public static bool WeekComplete => Available && Stage >= ExpeditionSettings.Active.stages.Length;

    /// <summary>Progress on the stage in play (0 once the week is complete).</summary>
    public static int Progress
    {
        get
        {
            if (!Available) return 0;
            RollWeek();
            return PlayerPrefs.GetInt(ProgressKey, 0);
        }
    }

    /// <summary>Seconds until the next week's stages open.</summary>
    public static long SecondsUntilReset
    {
        get
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long weekEnd = ((long)(CurrentWeek + 1) * 7 - 3) * 86400;
            return Math.Max(0, weekEnd - now);
        }
    }

    /// <summary>Counts a finished run toward the stage in play and pays any stage it completes.</summary>
    public static void RecordRun(RunStats run)
    {
        var s = ExpeditionSettings.Active;
        if (s == null || run.blocks < s.minStonesToCount) return;

        RollWeek();
        int stage = Stage;
        if (stage >= s.stages.Length) return;

        var st = s.stages[stage];
        if (!ModeMatches(st.mode, run.mode)) return;

        int progress = PlayerPrefs.GetInt(ProgressKey, 0);
        switch (st.kind)
        {
            case MissionSettings.Kind.Runs: progress += 1; break;
            case MissionSettings.Kind.PerfectsTotal: progress += run.perfects; break;
            case MissionSettings.Kind.SunSurgesTotal: progress += run.surges; break;
            case MissionSettings.Kind.EdgeLandingsTotal: progress += run.edgeLandings; break;
            case MissionSettings.Kind.StonesInRun: progress = Mathf.Max(progress, run.blocks); break;
            case MissionSettings.Kind.PerfectsInRun: progress = Mathf.Max(progress, run.perfects); break;
            case MissionSettings.Kind.ComboInRun: progress = Mathf.Max(progress, run.maxCombo); break;
        }
        progress = Mathf.Min(progress, st.target);

        if (progress < st.target)
        {
            PlayerPrefs.SetInt(ProgressKey, progress);
            PlayerPrefs.Save();
            return;
        }

        // Stage done: advance first so a crash can't pay it twice.
        PlayerPrefs.SetInt(StageKey, stage + 1);
        PlayerPrefs.SetInt(ProgressKey, 0);
        bool finale = stage + 1 >= s.stages.Length;
        if (finale) PlayerPrefs.SetInt(BadgesKey, Badges + 1);
        PlayerPrefs.Save();

        int jade = JadeWallet.Earn("expedition", st.reward);
        if (finale) jade += JadeWallet.Earn("expedition", s.finaleReward);

        GameAnalytics.Track("expedition_stage", new Dictionary<string, object>
        {
            { "id", st.id },
            { "stage", stage + 1 },
            { "jade", jade },
            { "week_complete", finale }
        });
        RunBanner.Show(LocalizationManager.Get(finale ? "expedition_complete" : "expedition_stage_done", stage + 1),
            LocalizationManager.Get("expedition_reward", jade),
            new Color(0.4f, 0.8f, 0.9f));
        OnStageCompleted?.Invoke(stage, jade);
    }

    private static void RollWeek()
    {
        int week = CurrentWeek;
        if (PlayerPrefs.GetInt(WeekKey, -1) == week) return;
        PlayerPrefs.SetInt(WeekKey, week);
        PlayerPrefs.SetInt(StageKey, 0);
        PlayerPrefs.SetInt(ProgressKey, 0);
        PlayerPrefs.Save();
    }

    private static bool ModeMatches(MissionSettings.ModeFilter filter, GameMode mode)
    {
        switch (filter)
        {
            case MissionSettings.ModeFilter.Infinite: return mode == GameMode.InfiniteStacker;
            case MissionSettings.ModeFilter.Temples: return mode == GameMode.StackerLevels;
            case MissionSettings.ModeFilter.Daily: return mode == GameMode.DailyChallenge;
            default: return true;
        }
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(WeekKey);
        PlayerPrefs.DeleteKey(StageKey);
        PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.DeleteKey(BadgesKey);
        PlayerPrefs.Save();
    }
}
