/// <summary>
/// The fully-resolved configuration for today's Daily Challenge run.
/// Cached for the session once fetched, so the run can't re-roll mid-play.
/// </summary>
public struct DailyChallengeConfig
{
    /// <summary>Legacy single modifier, used only when no ritual is set up (<see cref="ritual"/> is null).</summary>
    public DailyChallengeModifier modifier;
    public int blockCount;
    public int dayNumberUtc; // days since 1970-01-01 UTC, used for deterministic rotation

    /// <summary>Today's named ritual, or null for the original one-modifier Daily.</summary>
    public DailyRitual ritual;

    public bool HasRitual => ritual != null;
}
