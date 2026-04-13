/// <summary>
/// The fully-resolved configuration for today's Daily Challenge run.
/// Cached for the session once fetched, so the run can't re-roll mid-play.
/// </summary>
public struct DailyChallengeConfig
{
    public DailyChallengeModifier modifier;
    public int blockCount;
    public int dayNumberUtc; // days since 1970-01-01 UTC, used for deterministic rotation
}
