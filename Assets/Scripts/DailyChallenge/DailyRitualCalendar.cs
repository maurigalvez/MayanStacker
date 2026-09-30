using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which ritual runs on which day.
///
/// The list is read in weeks, Monday first: entries 0-6 are week A (Mon…Sun), 7-13 week B,
/// and so on. The week cycles, so with two weeks every ritual comes back once a fortnight.
/// Difficulty climbs through each week — gentle on Monday, the two-rule trial on Sunday —
/// so the day of the week tells a returning player what they're in for.
///
/// Weekdays are UTC, like everything else the Daily keys on (see
/// <see cref="DailyChallengeManager.NowUtc"/>). PlayFab Title Data DailyChallenge_Override can
/// force any ritual by <see cref="DailyRitual.id"/> for an event.
///
/// Optional: with no asset at Resources/<see cref="ResourcePath"/> the Daily falls back to its
/// original one-modifier rotation. Created by TamalStacker ▸ Daily Challenge ▸ Create Ritual Calendar.
/// </summary>
[CreateAssetMenu(fileName = "DailyRitualCalendar", menuName = "TamalStacker/Daily Ritual Calendar", order = 3)]
public class DailyRitualCalendar : ScriptableObject
{
    public const string ResourcePath = "DailyChallenge/DailyRitualCalendar";

    [Tooltip("Rituals in weeks, Monday first (0-6 = week A Mon..Sun, 7-13 = week B...). " +
             "A length that isn't a multiple of 7 simply cycles one ritual per day.")]
    public List<DailyRitual> rituals = new List<DailyRitual>();

    [Tooltip("Editor-only: play this ritual instead of today's, in the menu briefing and the run. " +
             "Ignored in builds.")]
    public DailyRitual editorForcedRitual;

    public bool HasRituals
    {
        get
        {
            if (rituals == null) return false;
            for (int i = 0; i < rituals.Count; i++) if (rituals[i] != null) return true;
            return false;
        }
    }

    /// <summary>The ritual for <paramref name="dayNumberUtc"/> (days since 1970-01-01 UTC), or null.</summary>
    public DailyRitual ForDay(int dayNumberUtc)
    {
        if (!HasRituals) return null;

        int count = rituals.Count;
        int index;

        if (count % 7 == 0)
        {
            // 1970-01-01 was a Thursday, so +3 makes Monday weekday 0.
            int shifted = dayNumberUtc + 3;
            int weekday = Mod(shifted, 7);
            int week = FloorDiv(shifted, 7);
            index = Mod(week, count / 7) * 7 + weekday;
        }
        else
        {
            index = Mod(dayNumberUtc, count);
        }

        // A hole in the list shouldn't cost the day its ritual: walk to the next one set.
        for (int i = 0; i < count; i++)
        {
            DailyRitual r = rituals[Mod(index + i, count)];
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>The ritual with <paramref name="id"/> (case-insensitive), or null.</summary>
    public DailyRitual Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || rituals == null) return null;
        string wanted = id.Trim();
        foreach (DailyRitual r in rituals)
        {
            if (r != null && string.Equals(r.id, wanted, System.StringComparison.OrdinalIgnoreCase)) return r;
        }
        return null;
    }

    private static int Mod(int a, int m) => ((a % m) + m) % m;
    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    private static DailyRitualCalendar current;
    private static bool looked;

    /// <summary>The Resources asset, or null when rituals aren't set up.</summary>
    public static DailyRitualCalendar Current
    {
        get
        {
            if (!looked || current == null)
            {
                current = Resources.Load<DailyRitualCalendar>(ResourcePath);
                looked = true;
            }
            return current;
        }
    }
}
