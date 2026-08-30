/// <summary>
/// A rule change applied to a single run.
///
/// This is the general form of <see cref="DailyChallengeModifier"/>, which stays as-is
/// because its member NAMES are a serialized contract with PlayFab Title Data
/// (DailyChallenge_ActiveModifiers). The Daily maps into this enum when it applies a
/// modifier, so every system reads one source of truth
/// (<see cref="RunModifierService"/>) regardless of which mode set it.
///
/// Adding a member here is safe: unknown values fall back to <see cref="None"/>.
/// </summary>
public enum RunModifier
{
    /// <summary>No modifier — the game's baseline rules.</summary>
    None,

    /// <summary>Spawner swings noticeably faster.</summary>
    SpeedRun,

    /// <summary>A single sub-Good landing ends the run.</summary>
    FragileStack,

    /// <summary>Combo multiplier scales geometrically with a higher cap.</summary>
    ComboChain,

    /// <summary>Only Perfect landings hold the combo — a Good breaks it like a Poor would.</summary>
    Perfectionist,

    /// <summary>The Perfect window is tightened, making combos much harder to sustain.</summary>
    NarrowWindow,

    /// <summary>Everything scores double, but one Poor landing ends the run.</summary>
    DoubleOrNothing
}
