using UnityEngine;

/// <summary>
/// The tuning behind one <see cref="RunModifier"/>, plus the localization keys that
/// describe it to the player.
///
/// Every field is expressed as a delta from the game's baseline, and the baseline itself
/// is a valid definition (<see cref="For"/> of <see cref="RunModifier.None"/>). That means
/// callers never branch on "is a modifier active?" — they just read the current definition
/// through <see cref="RunModifierService"/> and get default behaviour when nothing is set.
///
/// Localization keys keep the existing "daily_modifier_*" prefix even for modifiers the
/// Daily never rolls. The three original keys already ship translated in six locales, and
/// renaming them would orphan that work for no player-visible gain.
/// </summary>
public struct RunModifierDefinition
{
    /// <summary>Landing accuracy (0-1) at or above which a landing counts as Perfect.</summary>
    public const float BaselinePerfectThreshold = 0.9f;

    /// <summary>Landing accuracy (0-1) at or above which a landing counts as Good.</summary>
    public const float BaselineGoodThreshold = 0.6f;

    public RunModifier modifier;

    /// <summary>Multiplier applied to the spawner's swing speed for the run. 1 = untouched.</summary>
    public float swingSpeedMultiplier;

    /// <summary>Accuracy needed for a Perfect landing. Raising it shrinks the window.</summary>
    public float perfectThreshold;

    /// <summary>Accuracy needed for a Good landing.</summary>
    public float goodThreshold;

    /// <summary>Flat multiplier on every point awarded during the run.</summary>
    public float scoreMultiplier;

    /// <summary>When true, any sub-Good landing ends the run immediately.</summary>
    public bool endRunOnPoorLanding;

    /// <summary>
    /// When true (the baseline), a Good landing holds the combo instead of breaking it.
    /// Perfectionist turns this off.
    /// </summary>
    public bool goodHoldsCombo;

    /// <summary>When true, the combo multiplier compounds instead of adding.</summary>
    public bool geometricCombo;

    /// <summary>Base of the geometric progression. Ignored unless <see cref="geometricCombo"/>.</summary>
    public float geometricComboBase;

    /// <summary>
    /// Ceiling on the combo multiplier. Zero or less means "use GameManager's own cap",
    /// so the baseline never overrides the value tuned on the prefab.
    /// </summary>
    public float comboMultiplierCap;

    public string nameKey;
    public string descriptionKey;

    /// <summary>True for everything except the baseline.</summary>
    public bool IsSomething => modifier != RunModifier.None;

    /// <summary>
    /// The tuning table. Unknown values fall through to the baseline rather than throwing,
    /// so a modifier rolled by a newer server build can't break an older client.
    /// </summary>
    public static RunModifierDefinition For(RunModifier modifier)
    {
        RunModifierDefinition def = Baseline();
        def.modifier = modifier;

        switch (modifier)
        {
            case RunModifier.SpeedRun:
                def.swingSpeedMultiplier = 1.6f;
                def.nameKey = "daily_modifier_speedrun";
                def.descriptionKey = "daily_modifier_speedrun_desc";
                break;

            case RunModifier.FragileStack:
                def.endRunOnPoorLanding = true;
                def.nameKey = "daily_modifier_fragilestack";
                def.descriptionKey = "daily_modifier_fragilestack_desc";
                break;

            case RunModifier.ComboChain:
                def.geometricCombo = true;
                def.geometricComboBase = 1.5f;
                def.comboMultiplierCap = 10f;
                def.nameKey = "daily_modifier_combochain";
                def.descriptionKey = "daily_modifier_combochain_desc";
                break;

            case RunModifier.Perfectionist:
                // The forgiving "Good holds the combo" rule is the thing being taken away,
                // so this modifier is felt entirely through the combo meter.
                def.goodHoldsCombo = false;
                def.scoreMultiplier = 1.5f;
                def.nameKey = "daily_modifier_perfectionist";
                def.descriptionKey = "daily_modifier_perfectionist_desc";
                break;

            case RunModifier.NarrowWindow:
                def.perfectThreshold = 0.96f;
                def.scoreMultiplier = 1.75f;
                def.nameKey = "daily_modifier_narrowwindow";
                def.descriptionKey = "daily_modifier_narrowwindow_desc";
                break;

            case RunModifier.DoubleOrNothing:
                def.scoreMultiplier = 2f;
                def.endRunOnPoorLanding = true;
                def.nameKey = "daily_modifier_doubleornothing";
                def.descriptionKey = "daily_modifier_doubleornothing_desc";
                break;

            case RunModifier.None:
            default:
                def.modifier = RunModifier.None;
                break;
        }

        return def;
    }

    /// <summary>The game's untouched rules, expressed as a definition.</summary>
    private static RunModifierDefinition Baseline()
    {
        return new RunModifierDefinition
        {
            modifier = RunModifier.None,
            swingSpeedMultiplier = 1f,
            perfectThreshold = BaselinePerfectThreshold,
            goodThreshold = BaselineGoodThreshold,
            scoreMultiplier = 1f,
            endRunOnPoorLanding = false,
            goodHoldsCombo = true,
            geometricCombo = false,
            geometricComboBase = 1.5f,
            comboMultiplierCap = 0f,
            nameKey = string.Empty,
            descriptionKey = string.Empty
        };
    }

    /// <summary>
    /// Maps the Daily's PlayFab-facing enum onto the general one. Kept here rather than on
    /// DailyChallengeModifier so that enum stays a pure serialization contract.
    /// </summary>
    public static RunModifier FromDaily(DailyChallengeModifier daily)
    {
        switch (daily)
        {
            case DailyChallengeModifier.SpeedRun: return RunModifier.SpeedRun;
            case DailyChallengeModifier.FragileStack: return RunModifier.FragileStack;
            case DailyChallengeModifier.ComboChain: return RunModifier.ComboChain;
            default:
                Debug.LogWarning($"[RunModifier] Unmapped daily modifier '{daily}' - running unmodified.");
                return RunModifier.None;
        }
    }
}
