using UnityEngine;

/// <summary>
/// A temple's physical rule: something visible that changes how the run plays and looks.
///
/// Additive to <see cref="LevelObjective"/> (which is about what counts as winning) and
/// defaults to <see cref="None"/>, so every existing LevelData asset is unchanged. Temples are
/// grouped into regions of about three, each with one rule; the last region pairs two
/// (<see cref="LevelData.secondRule"/>).
///
/// The names are recorded by analytics, so rename with care.
/// </summary>
public enum LevelRule
{
    None = 0,

    // 1 was BallCourtRing, removed 2026-09-25. Don't reuse the number: level assets and
    // analytics store rules by value.

    /// <summary>Gusts push the swing and the falling stone to one side; leaves show which way.</summary>
    JungleWind = 2,

    /// <summary>A landing that isn't Perfect slides a little further off-centre.</summary>
    RainSlick = 3,

    /// <summary>At set heights the ground shakes; press and hold to brace the tower.</summary>
    Earthquake = 4,

    /// <summary>Water climbs behind the tower; if it reaches the top stone the run ends.</summary>
    RisingCenote = 5,

    /// <summary>Only the top of the tower is lit; the lean below has to be remembered.</summary>
    Eclipse = 6
}

/// <summary>Per-level tuning for <see cref="LevelRule.JungleWind"/>. Estimates; tune on device.</summary>
[System.Serializable]
public class JungleWindSettings
{
    [Tooltip("How far a full gust pushes the swing's centre, in world units.")]
    [Range(0f, 2f)]
    public float swingPush = 0.7f;

    [Tooltip("Sideways acceleration on a falling stone at full gust, in world units/s².")]
    [Range(0f, 6f)]
    public float fallPush = 1.6f;

    [Tooltip("Stones between gust changes. The gust pattern is fixed per temple, so every " +
             "attempt and every player faces the same wind.")]
    [Min(1)]
    public int stonesPerGust = 3;

    [Tooltip("Seconds a change of wind takes to build, so a shift is seen before it bites.")]
    [Range(0.2f, 4f)]
    public float gustRampSeconds = 1.4f;

    [Tooltip("No wind before the stack is this tall.")]
    [Min(0)]
    public int firstAtHeight = 2;
}

/// <summary>Per-level tuning for <see cref="LevelRule.RainSlick"/>. Estimates; tune on device.</summary>
[System.Serializable]
public class RainSlickSettings
{
    [Tooltip("Share of a non-Perfect landing's offset that the stone slides further out.")]
    [Range(0f, 1f)]
    public float slideFraction = 0.35f;

    [Tooltip("Longest slide, as a share of the stone's width, so a slide never throws a stone off on its own.")]
    [Range(0f, 0.4f)]
    public float maxSlideOfWidth = 0.14f;

    [Tooltip("Realtime seconds the slide takes.")]
    [Range(0.05f, 1f)]
    public float slideSeconds = 0.28f;

    [Tooltip("Poor landings slide too. Off: only Good slides, so the rule reads as 'Good isn't safe in the rain'.")]
    public bool poorSlides = true;
}

/// <summary>Per-level tuning for <see cref="LevelRule.Earthquake"/>. Estimates; tune on device.</summary>
[System.Serializable]
public class EarthquakeSettings
{
    [Tooltip("The first quake hits when the stack reaches this height.")]
    [Min(2)]
    public int firstAtHeight = 6;

    [Tooltip("Then again every N stones. Never on the temple's final stone.")]
    [Min(2)]
    public int everyNStones = 8;

    [Tooltip("Realtime seconds of warning (HOLD! prompt, rumble) before the shaking starts.")]
    [Range(0.4f, 3f)]
    public float warningSeconds = 1.3f;

    [Tooltip("Seconds the ground shakes.")]
    [Range(0.5f, 6f)]
    public float quakeSeconds = 2.4f;

    [Tooltip("Jolts per second while shaking.")]
    [Range(1f, 8f)]
    public float joltsPerSecond = 3.2f;

    [Tooltip("Sideways speed a jolt gives the top stone when not braced, in world units/s. " +
             "Lower stones get less, in proportion to their height.")]
    [Range(0f, 4f)]
    public float joltSpeed = 1.1f;

    [Tooltip("Share of the jolt that still lands while the player holds to brace.")]
    [Range(0f, 1f)]
    public float bracedFactor = 0.15f;
}

/// <summary>Per-level tuning for <see cref="LevelRule.RisingCenote"/>. Estimates; tune on device.</summary>
[System.Serializable]
public class RisingCenoteSettings
{
    [Tooltip("Seconds after the first stone lands before the water starts to climb.")]
    [Range(0f, 15f)]
    public float startDelaySeconds = 4f;

    [Tooltip("Climb speed, in stone heights per second. 0.2 = the player needs a stone every 5 s.")]
    [Range(0.02f, 1f)]
    public float stonesPerSecond = 0.2f;

    [Tooltip("The water never falls further than this many stones below the top, so a fast " +
             "builder still sees it coming instead of leaving it off-screen.")]
    [Range(2f, 12f)]
    public float maxLagStones = 5f;

    [Tooltip("Within this many stones of the top the water turns to warning colours.")]
    [Range(0.5f, 4f)]
    public float warningStones = 1.5f;
}

/// <summary>Per-level tuning for <see cref="LevelRule.Eclipse"/>. Estimates; tune on device.</summary>
[System.Serializable]
public class EclipseSettings
{
    [Tooltip("Stones at the top of the tower that stay lit.")]
    [Range(1f, 6f)]
    public float litStones = 2.5f;

    [Tooltip("Stones over which the light fades to dark.")]
    [Range(0.5f, 4f)]
    public float fadeStones = 1.5f;

    [Tooltip("How dark the unlit tower gets (0 = no eclipse, 1 = black).")]
    [Range(0f, 1f)]
    public float darkness = 0.9f;

    [Tooltip("Dim over the lit sky and the swing, so the whole scene reads as an eclipse.")]
    [Range(0f, 0.6f)]
    public float skyDim = 0.25f;
}
