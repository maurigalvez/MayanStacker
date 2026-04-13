/// <summary>
/// Modifiers that can be applied to a Daily Challenge run.
/// The active set is controlled remotely via PlayFab Title Data
/// (DailyChallenge_ActiveModifiers / DailyChallenge_Override).
/// v1 ships three; more can be added without breaking existing players
/// because unknown IDs fall back to the first known modifier.
/// </summary>
public enum DailyChallengeModifier
{
    SpeedRun,      // Spawner swings noticeably faster.
    FragileStack,  // A single sub-Good landing ends the run early.
    ComboChain     // Combo multiplier scales geometrically with a higher cap.
    // Perfectionist, NarrowWindow, DoubleOrNothing — deferred to v2
}
