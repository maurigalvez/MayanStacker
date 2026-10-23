using System;
using UnityEngine;

/// <summary>
/// Tuning for the daily missions: three small goals a day (one easy, one medium, one hard) that
/// pay jade. The pool lives here so goals and rewards can change without code.
///
/// An asset in Resources; if it is missing, or <see cref="enableMissions"/> is cleared, no
/// missions are rolled or tracked and the game plays exactly as before.
/// </summary>
[CreateAssetMenu(fileName = "MissionSettings", menuName = "TamalStacker/Mission Settings", order = 12)]
public class MissionSettings : ScriptableObject
{
    public const string ResourcePath = "MissionSettings";

    private static MissionSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or missions are off.</summary>
    public static MissionSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<MissionSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableMissions ? cached : null;
        }
    }

    public enum Kind
    {
        /// <summary>Finish this many runs (each must reach minStonesToCount).</summary>
        Runs,
        /// <summary>Have this many stones in the tower in a single run.</summary>
        StonesInRun,
        /// <summary>Land this many Perfects in a single run.</summary>
        PerfectsInRun,
        /// <summary>Land this many Perfects across the day's runs.</summary>
        PerfectsTotal,
        /// <summary>Reach this combo in a single run.</summary>
        ComboInRun,
        /// <summary>Ignite this many Sun Surges across the day.</summary>
        SunSurgesTotal,
        /// <summary>Land this many Serpent's Edge stones across the day.</summary>
        EdgeLandingsTotal
    }

    public enum ModeFilter { Any, Infinite, Temples, Daily }

    public enum Tier { Easy, Medium, Hard }

    [Serializable]
    public class Template
    {
        [Tooltip("Stable id. The description is the loc key mission_<id>, with the target as {0}.")]
        public string id;
        public Tier tier;
        public Kind kind;
        public ModeFilter mode;
        [Min(1)] public int target = 1;
        [Min(0)] public int reward = 10;
    }

    [Header("Master switch")]
    public bool enableMissions = true;

    [Header("Rules")]
    [Tooltip("A run that falls before this many stones counts for nothing, so instant restarts can't tick missions.")]
    [Min(0)] public int minStonesToCount = 3;

    [Header("Pool (one is rolled per tier each UTC day)")]
    public Template[] pool = new Template[0];
}
