using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Today's three missions and the player's progress on them.
///
/// The day's picks are a pure function of the UTC day number, so every device rolls the same
/// goals without a server. Progress is local (PlayerPrefs) and resets with the day. A mission
/// pays its jade once, the moment a finished run completes it, through <see cref="JadeWallet.Earn"/>.
///
/// Missions only read what a run already did; nothing here touches a score.
/// </summary>
public static class DailyMissions
{
    private const string DayKey = "Missions_Day";
    private const string ProgressKey = "Missions_Progress";
    private const string DoneKey = "Missions_Done";
    private const string SwapKey = "Missions_Swaps";
    private const string SwapDayKey = "Missions_SwapDay";

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

    public struct Entry
    {
        public MissionSettings.Template template;
        public int progress;
        public bool done;
    }

    /// <summary>Raised when a mission completes, after its jade is paid.</summary>
    public static event Action<MissionSettings.Template> OnMissionCompleted;

    private static int cachedDay = -1;
    private static MissionSettings.Template[] today = new MissionSettings.Template[0];

    public static int CurrentDay => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);

    /// <summary>Today's missions with progress; empty when missions are off.</summary>
    public static List<Entry> GetToday()
    {
        var list = new List<Entry>();
        var s = MissionSettings.Active;
        if (s == null) return list;

        Roll(s);
        int[] progress = ReadInts(ProgressKey, today.Length);
        int[] done = ReadInts(DoneKey, today.Length);
        for (int i = 0; i < today.Length; i++)
        {
            list.Add(new Entry { template = today[i], progress = progress[i], done = done[i] != 0 });
        }
        return list;
    }

    /// <summary>Counts a finished run toward today's missions and pays any it completes.</summary>
    public static void RecordRun(RunStats run)
    {
        var s = MissionSettings.Active;
        if (s == null || run.blocks < s.minStonesToCount) return;

        Roll(s);
        int[] progress = ReadInts(ProgressKey, today.Length);
        int[] done = ReadInts(DoneKey, today.Length);
        var completed = new List<MissionSettings.Template>();

        for (int i = 0; i < today.Length; i++)
        {
            var t = today[i];
            if (done[i] != 0 || !ModeMatches(t.mode, run.mode)) continue;

            switch (t.kind)
            {
                case MissionSettings.Kind.Runs: progress[i] += 1; break;
                case MissionSettings.Kind.PerfectsTotal: progress[i] += run.perfects; break;
                case MissionSettings.Kind.SunSurgesTotal: progress[i] += run.surges; break;
                case MissionSettings.Kind.EdgeLandingsTotal: progress[i] += run.edgeLandings; break;
                case MissionSettings.Kind.StonesInRun: progress[i] = Mathf.Max(progress[i], run.blocks); break;
                case MissionSettings.Kind.PerfectsInRun: progress[i] = Mathf.Max(progress[i], run.perfects); break;
                case MissionSettings.Kind.ComboInRun: progress[i] = Mathf.Max(progress[i], run.maxCombo); break;
            }

            progress[i] = Mathf.Min(progress[i], t.target);
            if (progress[i] >= t.target)
            {
                done[i] = 1;
                completed.Add(t);
            }
        }

        WriteInts(ProgressKey, progress);
        WriteInts(DoneKey, done);
        PlayerPrefs.Save();

        foreach (var t in completed)
        {
            JadeWallet.Earn("mission", t.reward);
            GameAnalytics.Track("mission_complete", new Dictionary<string, object>
            {
                { "id", t.id },
                { "reward", t.reward }
            });
            RunBanner.Show(LocalizationManager.Get("mission_complete"),
                LocalizationManager.Get("mission_complete_reward", t.reward),
                new Color(0.35f, 0.85f, 0.6f));
            OnMissionCompleted?.Invoke(t);
        }
    }

    /// <summary>True while today's swap is unused and rerolling is switched on.</summary>
    public static bool CanReroll
    {
        get
        {
            var s = MissionSettings.Active;
            if (s == null || !s.enableReroll) return false;
            Roll(s);
            return PlayerPrefs.GetInt(SwapDayKey, -1) != CurrentDay;
        }
    }

    /// <summary>
    /// Replaces the unfinished mission in <paramref name="slot"/> with another from its tier and
    /// clears its progress. Once per day; the caller has already earned the right (rewarded ad).
    /// </summary>
    public static bool Reroll(int slot)
    {
        if (!CanReroll || slot < 0 || slot >= today.Length) return false;

        var s = MissionSettings.Active;
        int[] done = ReadInts(DoneKey, today.Length);
        if (done[slot] != 0) return false;

        // Slots are tiers in order, but a tier with an empty pool is skipped, so map back to the tier.
        int tierIndex = (int)today[slot].tier;
        int[] swapped = ReadInts(SwapKey, Enum.GetValues(typeof(MissionSettings.Tier)).Length);
        swapped[tierIndex] += 1;
        WriteInts(SwapKey, swapped);

        int[] progress = ReadInts(ProgressKey, today.Length);
        progress[slot] = 0;
        WriteInts(ProgressKey, progress);

        PlayerPrefs.SetInt(SwapDayKey, CurrentDay);
        PlayerPrefs.Save();
        cachedDay = -1;
        Roll(s);

        GameAnalytics.Track("mission_reroll", new Dictionary<string, object> { { "id", today[slot].id } });
        return true;
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(SwapKey);
        PlayerPrefs.DeleteKey(SwapDayKey);
        PlayerPrefs.DeleteKey(DayKey);
        PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.DeleteKey(DoneKey);
        cachedDay = -1;
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

    /// <summary>Picks today's missions (one per tier) and clears progress when the day changed.</summary>
    private static void Roll(MissionSettings s)
    {
        int day = CurrentDay;
        if (cachedDay == day && today.Length > 0) return;

        if (PlayerPrefs.GetInt(DayKey, -1) != day)
        {
            PlayerPrefs.SetInt(DayKey, day);
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.DeleteKey(DoneKey);
            PlayerPrefs.DeleteKey(SwapKey);
            PlayerPrefs.Save();
        }

        int[] swapped = ReadInts(SwapKey, Enum.GetValues(typeof(MissionSettings.Tier)).Length);
        var picks = new List<MissionSettings.Template>();
        foreach (MissionSettings.Tier tier in Enum.GetValues(typeof(MissionSettings.Tier)))
        {
            var candidates = new List<MissionSettings.Template>();
            foreach (var t in s.pool)
            {
                if (t != null && t.tier == tier && !string.IsNullOrEmpty(t.id)) candidates.Add(t);
            }
            if (candidates.Count == 0) continue;

            // A fixed per-tier salt keeps the tiers from always landing on the same index.
            var rng = new System.Random(day * 31 + (int)tier * 7919 + 17);
            int index = rng.Next(candidates.Count);

            // A swapped slot moves to a different template than the day's original pick.
            int swaps = swapped[(int)tier];
            if (swaps > 0 && candidates.Count > 1)
            {
                var swapRng = new System.Random(day * 31 + (int)tier * 7919 + swaps * 104729);
                index = (index + 1 + swapRng.Next(candidates.Count - 1)) % candidates.Count;
            }
            picks.Add(candidates[index]);
        }

        today = picks.ToArray();
        cachedDay = day;
    }

    private static int[] ReadInts(string key, int length)
    {
        var result = new int[length];
        string raw = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(raw)) return result;

        string[] parts = raw.Split(',');
        for (int i = 0; i < length && i < parts.Length; i++)
        {
            int.TryParse(parts[i], out result[i]);
        }
        return result;
    }

    private static void WriteInts(string key, int[] values)
    {
        PlayerPrefs.SetString(key, string.Join(",", values));
    }
}
