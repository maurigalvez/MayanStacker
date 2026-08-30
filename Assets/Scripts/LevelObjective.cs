/// <summary>
/// What a temple asks of the player beyond simply getting tall.
///
/// Every level has always been the same request — reach a height, with the only variation
/// being how fast the spawner swings. Objectives make the existing sites feel like
/// different puzzles rather than one puzzle at different speeds, without needing new art,
/// new levels or new mechanics.
///
/// All objectives still require <c>requiredStackHeight</c>: the extra condition is an
/// additional constraint, never a replacement. That means every existing LevelData asset
/// keeps working unchanged, because the default is <see cref="ReachHeight"/>.
/// </summary>
public enum LevelObjective
{
    /// <summary>Reach the required height. The original behaviour, and the default.</summary>
    ReachHeight,

    /// <summary>Reach the required height without a single poor landing.</summary>
    FlawlessAscent,

    /// <summary>
    /// Reach the required height, and land a run of consecutive perfect drops along the
    /// way. Reaching the height early simply lets the player keep stacking until they do.
    /// </summary>
    PerfectChain,

    /// <summary>Reach the required height before the clock runs out.</summary>
    SwiftAscent
}
