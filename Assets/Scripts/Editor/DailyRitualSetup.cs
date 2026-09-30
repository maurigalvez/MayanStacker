#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the named Ritual of The Sun set: 14 <see cref="DailyRitual"/> assets (two weeks,
/// Monday first) and the <see cref="DailyRitualCalendar"/> that rotates them.
///
/// Each week climbs: Monday one gentle modifier, midweek a temple rule plus a modifier, the
/// weekend two or three omens at once, and Sunday the trial (two rules or a rule with the
/// harshest modifiers). Every number is an estimate to tune on device.
///
/// Non-destructive: a ritual asset that already exists is left exactly as it is (so hand
/// tuning survives a re-run); only missing ones are created, and the calendar is only
/// filled when it's new or empty.
///
/// Menu: TamalStacker ▸ Daily Challenge ▸ Create Ritual Calendar
///       TamalStacker ▸ Daily Challenge ▸ Delete Ritual Calendar (back to the one-modifier Daily)
/// </summary>
public static class DailyRitualSetup
{
    private const string Folder = "Assets/Resources/DailyChallenge";
    private const string RitualFolder = Folder + "/Rituals";
    private const string CalendarPath = Folder + "/DailyRitualCalendar.asset";

    [MenuItem("TamalStacker/Daily Challenge/Create Ritual Calendar")]
    public static void CreateCalendar()
    {
        DailyUIBuilder.EnsureFolder("Assets", "Resources");
        DailyUIBuilder.EnsureFolder("Assets/Resources", "DailyChallenge");
        DailyUIBuilder.EnsureFolder(Folder, "Rituals");

        var rituals = new List<DailyRitual>();
        int created = 0;

        foreach (RitualSpec spec in Specs())
        {
            string path = $"{RitualFolder}/Ritual_{spec.id}.asset";
            var ritual = AssetDatabase.LoadAssetAtPath<DailyRitual>(path);
            if (ritual == null)
            {
                ritual = ScriptableObject.CreateInstance<DailyRitual>();
                spec.Fill(ritual);
                AssetDatabase.CreateAsset(ritual, path);
                created++;
            }
            rituals.Add(ritual);
        }

        var calendar = AssetDatabase.LoadAssetAtPath<DailyRitualCalendar>(CalendarPath);
        bool newCalendar = calendar == null;
        if (newCalendar)
        {
            calendar = ScriptableObject.CreateInstance<DailyRitualCalendar>();
            AssetDatabase.CreateAsset(calendar, CalendarPath);
        }

        bool filled = false;
        if (newCalendar || !calendar.HasRituals)
        {
            calendar.rituals = rituals;
            EditorUtility.SetDirty(calendar);
            filled = true;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = calendar;
        EditorGUIUtility.PingObject(calendar);

        EditorUtility.DisplayDialog("Ritual Calendar",
            $"{created} ritual(s) created, {rituals.Count - created} already existed and were left untouched.\n\n" +
            (filled
                ? "The calendar now rotates them: two weeks, Monday (gentle) to Sunday (the trial), UTC days."
                : "The calendar already had rituals, so its order was kept.") +
            "\n\nForce a ritual for a day with PlayFab Title Data DailyChallenge_Override = its id " +
            "(e.g. wrath_of_cabracan), or in the editor with the Daily Challenge Manager's Editor Forced Ritual.",
            "OK");
    }

    [MenuItem("TamalStacker/Daily Challenge/Delete Ritual Calendar")]
    public static void DeleteCalendar()
    {
        if (AssetDatabase.LoadAssetAtPath<DailyRitualCalendar>(CalendarPath) == null)
        {
            EditorUtility.DisplayDialog("Ritual Calendar", "There is no calendar at " + CalendarPath + ".", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Ritual Calendar",
                "Delete " + CalendarPath + "?\n\nThe Daily goes back to the one-modifier rotation. " +
                "The ritual assets themselves are kept.", "Delete", "Cancel"))
        {
            return;
        }

        AssetDatabase.DeleteAsset(CalendarPath);
        AssetDatabase.Refresh();
    }

    // ─────────────────────────────────────────────────────────────────────
    // The set. Order = calendar order: week A Mon..Sun, then week B Mon..Sun.
    // ─────────────────────────────────────────────────────────────────────

    private static IEnumerable<RitualSpec> Specs()
    {
        // ── Week A ──
        yield return new RitualSpec("feather_of_dawn", 1, 25)
            .Mods(RunModifier.FeatherFall)
            .Tune(t => t.featherGravity = 0.45f);

        yield return new RitualSpec("spire_of_offerings", 2, 25)
            .Mods(RunModifier.ShrinkingOfferings)
            .Tune(t => { t.shrinkPerStone = 0.025f; t.minWidthScale = 0.5f; });

        yield return new RitualSpec("hunters_moon", 2, 30)
            .Mods(RunModifier.GiftOfTheGods)
            .Rules(LevelRule.Eclipse)
            .Eclipse(e => { e.firstAtHeight = 5; e.everyNStones = 7; e.totalitySeconds = 2.0f; });

        yield return new RitualSpec("coals_of_xibalba", 3, 30)
            .Mods(RunModifier.HotStone)
            .Tune(t => t.hotStoneSwings = 2.5f)
            .Rules(LevelRule.RainSlick);

        yield return new RitualSpec("storm_serpent", 3, 30)
            .Mods(RunModifier.SpeedRun, RunModifier.GiftOfTheGods)
            .Rules(LevelRule.JungleWind)
            .Wind(w => { w.swingPush = 0.4f; w.fallPush = 0.9f; w.stonesPerGust = 4; w.firstAtHeight = 3; w.leadSeconds = 0.8f; });

        yield return new RitualSpec("cenote_hunger", 4, 30)
            .Mods(RunModifier.ComboChain)
            .Power(PowerId.JaguarSlam)
            .Rules(LevelRule.RisingCenote)
            .Cenote(c => { c.stonesPerSecond = 0.18f; c.maxLagStones = 3f; });

        yield return new RitualSpec("wrath_of_cabracan", 5, 35)
            .Mods(RunModifier.ShrinkingOfferings, RunModifier.GiftOfTheGods)
            .Tune(t => { t.shrinkPerStone = 0.02f; t.minWidthScale = 0.55f; })
            .Rules(LevelRule.Earthquake, LevelRule.RainSlick)
            .Quake(q => { q.firstAtHeight = 6; q.everyNStones = 8; q.slidePerJolt = 0.085f; q.headShake = 0.3f; });

        // ── Week B ──
        yield return new RitualSpec("jade_morning", 1, 25)
            .Mods(RunModifier.ComboChain)
            .Power(PowerId.QuetzalFeather);

        yield return new RitualSpec("drifting_moon", 2, 25)
            .Mods(RunModifier.FeatherFall)
            .Rules(LevelRule.Eclipse)
            .Eclipse(e => { e.firstAtHeight = 5; e.everyNStones = 7; e.totalitySeconds = 2.0f; });

        yield return new RitualSpec("kiln_of_the_sun", 3, 30)
            .Mods(RunModifier.HotStone, RunModifier.ComboChain)
            .Tune(t => t.hotStoneSwings = 2f);

        yield return new RitualSpec("chaacs_trial", 3, 30)
            .Mods(RunModifier.NarrowWindow)
            .Power(PowerId.TzolkinRewind)
            .Rules(LevelRule.RainSlick);

        yield return new RitualSpec("needle_of_itzamna", 4, 30)
            .Mods(RunModifier.ShrinkingOfferings)
            .Rules(LevelRule.JungleWind)
            .Wind(w => { w.swingPush = 0.35f; w.fallPush = 0.8f; w.stonesPerGust = 5; w.firstAtHeight = 4; w.leadSeconds = 0.9f; w.steadyDirection = true; });

        yield return new RitualSpec("glass_pyramid", 4, 30)
            .Mods(RunModifier.FragileStack, RunModifier.GiftOfTheGods)
            .Rules(LevelRule.Eclipse)
            .Eclipse(e => { e.firstAtHeight = 6; e.everyNStones = 8; e.totalitySeconds = 2.0f; });

        yield return new RitualSpec("flood_of_chaac", 5, 35)
            .Mods(RunModifier.SpeedRun, RunModifier.GiftOfTheGods)
            .Rules(LevelRule.RisingCenote, LevelRule.JungleWind)
            .Cenote(c => { c.stonesPerSecond = 0.2f; c.maxLagStones = 2.5f; })
            .Wind(w => { w.swingPush = 0.45f; w.fallPush = 1.0f; w.stonesPerGust = 4; w.firstAtHeight = 3; w.leadSeconds = 0.8f; });
    }

    private class RitualSpec
    {
        public readonly string id;
        private readonly int difficulty;
        private readonly int stones;
        private RunModifier[] modifiers = new RunModifier[0];
        private LevelRule rule = LevelRule.None;
        private LevelRule secondRule = LevelRule.None;
        private bool grantsPower;
        private PowerId power;
        private readonly List<System.Action<DailyRitual>> edits = new List<System.Action<DailyRitual>>();

        public RitualSpec(string id, int difficulty, int stones)
        {
            this.id = id;
            this.difficulty = difficulty;
            this.stones = stones;
        }

        public RitualSpec Mods(params RunModifier[] m) { modifiers = m; return this; }
        public RitualSpec Rules(LevelRule first, LevelRule second = LevelRule.None) { rule = first; secondRule = second; return this; }
        public RitualSpec Power(PowerId p) { grantsPower = true; power = p; return this; }
        public RitualSpec Tune(System.Action<RunModifierTuning> f) { edits.Add(r => f(r.tuning)); return this; }
        public RitualSpec Wind(System.Action<JungleWindSettings> f) { edits.Add(r => f(r.windSettings)); return this; }
        public RitualSpec Quake(System.Action<EarthquakeSettings> f) { edits.Add(r => f(r.quakeSettings)); return this; }
        public RitualSpec Cenote(System.Action<RisingCenoteSettings> f) { edits.Add(r => f(r.cenoteSettings)); return this; }
        public RitualSpec Eclipse(System.Action<EclipseSettings> f) { edits.Add(r => f(r.eclipseSettings)); return this; }

        public void Fill(DailyRitual r)
        {
            r.id = id;
            r.nameKey = $"ritual_{id}_name";
            r.taglineKey = $"ritual_{id}_tagline";
            r.difficulty = difficulty;
            r.stonesToPlace = stones;
            r.modifiers = new List<RunModifier>(modifiers);
            r.rule = rule;
            r.secondRule = secondRule;
            r.grantsPower = grantsPower;
            r.grantedPower = power;
            foreach (var edit in edits) edit(r);
        }
    }
}
#endif
