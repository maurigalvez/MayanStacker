using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One named Ritual of The Sun: a hand-tuned mix of a goal, the temple rules (environment)
/// and run modifiers, with a name players can talk about ("did you survive Wrath of Cabracán?").
///
/// Rituals are picked by <see cref="DailyRitualCalendar"/> from the server-corrected UTC day,
/// so everyone plays the same one on the same day. Every random thing inside a ritual is
/// seeded by that day too (temple rule patterns, block variety, Gift of the Gods), so the
/// leaderboard compares like with like.
///
/// Created and tuned by TamalStacker ▸ Daily Challenge ▸ Create Ritual Calendar, then edited
/// by hand. All copy comes from localization keys.
/// </summary>
[CreateAssetMenu(fileName = "Ritual_", menuName = "TamalStacker/Daily Ritual", order = 2)]
public class DailyRitual : ScriptableObject
{
    [Tooltip("Stable id. PlayFab Title Data DailyChallenge_Override can name it to force this ritual, " +
             "and analytics records it. Never rename a shipped id.")]
    public string id = "ritual";

    [Tooltip("Localization key for the ritual's name, e.g. ritual_wrath_of_cabracan_name.")]
    public string nameKey = "";

    [Tooltip("Localization key for one line of flavour under the name.")]
    public string taglineKey = "";

    [Tooltip("1 = gentle (early week) … 5 = the Sunday trial. Shown as suns on the briefing.")]
    [Range(1, 5)]
    public int difficulty = 1;

    [Header("Goal")]
    [Tooltip("Stones to place to complete the ritual. Completing is the goal; score ranks you.")]
    [Min(5)]
    public int stonesToPlace = 30;

    [Header("Modifiers")]
    [Tooltip("Run modifiers, combined. Order matters only for which one names the set in logs.")]
    public List<RunModifier> modifiers = new List<RunModifier>();

    [Tooltip("Per-ritual overrides for the modifier numbers. 0 = the modifier's default.")]
    public RunModifierTuning tuning = new RunModifierTuning();

    [Header("Power")]
    [Tooltip("Gives everyone the same power this ritual, owned or not. Ignored when Gift of the " +
             "Gods is a modifier (that grants a random one on every full meter).")]
    public bool grantsPower;
    public PowerId grantedPower = PowerId.JaguarSlam;

    [Header("Temple rules (environment)")]
    public LevelRule rule = LevelRule.None;
    public LevelRule secondRule = LevelRule.None;

    [Tooltip("Used when either rule is Jungle Wind.")]
    public JungleWindSettings windSettings = new JungleWindSettings();

    [Tooltip("Used when either rule is Rain-slick Stones.")]
    public RainSlickSettings rainSettings = new RainSlickSettings();

    [Tooltip("Used when either rule is Cabracán.")]
    public EarthquakeSettings quakeSettings = new EarthquakeSettings();

    [Tooltip("Used when either rule is Rising Cenote.")]
    public RisingCenoteSettings cenoteSettings = new RisingCenoteSettings();

    [Tooltip("Used when either rule is Eclipse.")]
    public EclipseSettings eclipseSettings = new EclipseSettings();

    /// <summary>True when the meter runs this ritual (a granted power or Gift of the Gods).</summary>
    public bool UsesPowers => grantsPower || HasModifier(RunModifier.GiftOfTheGods);

    public bool HasModifier(RunModifier m) => modifiers != null && modifiers.Contains(m);

    public bool HasAnyRule => rule != LevelRule.None || secondRule != LevelRule.None;

    /// <summary>
    /// A throwaway <see cref="LevelData"/> the temple rules can read, exactly as they read a
    /// temple's asset: which rules run, their tuning, the ritual's length (so no quake or
    /// eclipse lands on the last stone) and a level number that seeds their patterns by day.
    /// </summary>
    public LevelData BuildRuleLevel(int dayNumberUtc)
    {
        var level = CreateInstance<LevelData>();
        level.hideFlags = HideFlags.DontSave;
        level.name = "DailyRitual_" + id;
        level.levelName = id;
        level.levelNumber = RuleSeedLevelNumber(dayNumberUtc);
        level.requiredStackHeight = stonesToPlace;
        level.rule = rule;
        level.secondRule = secondRule == rule ? LevelRule.None : secondRule;
        level.windSettings = windSettings;
        level.rainSettings = rainSettings;
        level.quakeSettings = quakeSettings;
        level.cenoteSettings = cenoteSettings;
        level.eclipseSettings = eclipseSettings;
        return level;
    }

    /// <summary>
    /// Rules seed their patterns (gust order, quake slide directions) from the level number.
    /// Kept well above the 27 temples, and different every day, so a ritual never replays a
    /// temple's pattern or yesterday's.
    /// </summary>
    public static int RuleSeedLevelNumber(int dayNumberUtc) => 1000 + ((dayNumberUtc % 9000) + 9000) % 9000;
}
