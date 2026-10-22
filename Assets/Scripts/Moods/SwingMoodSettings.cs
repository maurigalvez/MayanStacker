using UnityEngine;

/// <summary>The swing patterns a stone can roll. Steady is the plain pendulum every stone had before.</summary>
public enum SwingMood
{
    Steady,
    Quickening,
    Lurch,
    Drift,
    Echo
}

/// <summary>
/// Tuning for Swing Moods: a stone sometimes swings with a twist, announced by a glow on the
/// hanging stone before the twist begins. The landing windows are never touched; only the
/// path the stone takes to reach them changes.
///
/// An asset in Resources so it can be tuned (or switched off) without opening a scene. If the
/// asset is missing, or <see cref="enableMoods"/> is cleared, every stone swings Steady.
/// </summary>
[CreateAssetMenu(fileName = "SwingMoodSettings", menuName = "TamalStacker/Swing Mood Settings", order = 6)]
public class SwingMoodSettings : ScriptableObject
{
    /// <summary>The Resources path the swing loads this from.</summary>
    public const string ResourcePath = "SwingMoodSettings";

    [Header("Master switch")]
    public bool enableMoods = true;

    [Header("Where moods appear")]
    [Tooltip("Stones already on the tower before the first mood can roll. Keeps the opening stones plain.")]
    [Min(0)]
    public int firstMoodStone = 3;

    [Tooltip("Temples below this number never roll a mood.")]
    [Min(1)]
    public int templeMinLevel = 3;

    public bool enableInTemples = true;
    public bool enableInDaily = true;
    public bool enableInInfinite = true;

    [Header("Rolling")]
    [Tooltip("Chance a stone rolls a mood at all (the rest swing Steady).")]
    [Range(0f, 1f)]
    public float moodChance = 0.35f;

    public float quickeningWeight = 3f;
    public float lurchWeight = 2f;
    public float driftWeight = 2f;
    public float echoWeight = 2f;

    [Header("Pacing")]
    [Tooltip("On: moods follow a rhythm (calm, mood, calm), get likelier with height and are guaranteed early. Off: every stone just rolls Mood Chance.")]
    public bool enablePacing = true;

    [Tooltip("Calm stones forced after a mood before another can roll.")]
    [Min(0)]
    public int calmStonesAfterMood = 1;

    [Tooltip("The first mood is guaranteed once the tower is this many stones past First Mood Stone.")]
    [Min(0)]
    public int guaranteeFirstMoodWithin = 1;

    [Tooltip("Longest run of calm stones before a mood is forced.")]
    [Min(2)]
    public int maxCalmStones = 4;

    [Tooltip("Mood chance on the first stones where a mood can roll.")]
    [Range(0f, 1f)]
    public float chanceAtStart = 0.3f;

    [Tooltip("Mood chance once the tower is Stones To Peak past First Mood Stone.")]
    [Range(0f, 1f)]
    public float chanceAtPeak = 0.55f;

    [Min(1)]
    public int stonesToPeak = 25;

    /// <summary>Mood chance for a stone, rising with height when pacing is on.</summary>
    public float ChanceAt(int stonesPlaced)
    {
        if (!enablePacing) return moodChance;
        float t = Mathf.Clamp01((stonesPlaced - firstMoodStone) / (float)Mathf.Max(1, stonesToPeak));
        return Mathf.Lerp(chanceAtStart, chanceAtPeak, t);
    }

    [Header("Telegraph")]
    [Tooltip("Seconds the glow shows on the stone before the mood starts moving it.")]
    [Range(0.2f, 1.5f)]
    public float cueSeconds = 0.6f;

    [Tooltip("Seconds the mood takes to fade in once the cue ends.")]
    [Range(0.1f, 1.5f)]
    public float rampSeconds = 0.5f;

    [Range(0f, 1f)]
    public float cueGlowAlpha = 0.7f;

    [Range(0f, 1f)]
    public float activeGlowAlpha = 0.3f;

    [Header("Quickening")]
    [Tooltip("Extra swing speed gained by the end of the quickening (0.35 = x1.35).")]
    [Range(0f, 1f)]
    public float quickeningGain = 0.35f;

    [Tooltip("Seconds the quickening takes to reach full gain.")]
    [Min(0.5f)]
    public float quickeningSeconds = 3f;

    [Header("Lurch")]
    [Tooltip("Swing speed at the very edge of the swing, as a share of normal. The stone hangs there a beat.")]
    [Range(0.1f, 1f)]
    public float lurchEdgeSpeed = 0.45f;

    [Tooltip("How tightly the slowdown hugs the edge (higher = only the last sliver of the swing).")]
    [Range(1f, 12f)]
    public float lurchSharpness = 6f;

    [Header("Drift")]
    [Tooltip("World units the swing's centre wanders to either side.")]
    [Range(0f, 1.5f)]
    public float driftAmplitude = 0.5f;

    [Tooltip("Radians per second of the centre's wander (about 7 s per full cycle at 0.9).")]
    [Range(0.2f, 3f)]
    public float driftRate = 0.9f;

    [Header("Echo")]
    [Tooltip("Seconds behind the stone its ghost trails; the ghost shows where the stone was.")]
    [Range(0.1f, 1f)]
    public float echoDelay = 0.4f;

    [Range(0f, 1f)]
    public float echoAlpha = 0.35f;

    [Header("Glow colours (Mayan palette)")]
    public Color quickeningColor = new Color(0.95f, 0.55f, 0.18f, 1f); // sun amber
    public Color lurchColor = new Color(0.72f, 0.36f, 0.22f, 1f);      // clay
    public Color driftColor = new Color(0.30f, 0.74f, 0.62f, 1f);      // jade
    public Color echoColor = new Color(0.85f, 0.78f, 0.55f, 1f);       // old gold

    public Color ColorOf(SwingMood mood)
    {
        switch (mood)
        {
            case SwingMood.Quickening: return quickeningColor;
            case SwingMood.Lurch: return lurchColor;
            case SwingMood.Drift: return driftColor;
            case SwingMood.Echo: return echoColor;
            default: return Color.white;
        }
    }

    /// <summary>Picks a mood by weight. <paramref name="value01"/> is a uniform draw in [0,1).</summary>
    public SwingMood Pick(float value01, bool echoAvailable)
    {
        float echo = echoAvailable ? Mathf.Max(0f, echoWeight) : 0f;
        float q = Mathf.Max(0f, quickeningWeight), l = Mathf.Max(0f, lurchWeight), d = Mathf.Max(0f, driftWeight);
        float total = q + l + d + echo;
        if (total <= 0f) return SwingMood.Steady;

        float r = value01 * total;
        if ((r -= q) < 0f) return SwingMood.Quickening;
        if ((r -= l) < 0f) return SwingMood.Lurch;
        if ((r -= d) < 0f) return SwingMood.Drift;
        return echo > 0f ? SwingMood.Echo : SwingMood.Steady;
    }

    /// <summary>Whether a mode / temple may roll moods at all, before any chance is applied.</summary>
    public bool AllowedIn(GameMode mode, int levelNumber, int stonesPlaced)
    {
        if (!enableMoods || stonesPlaced < firstMoodStone) return false;
        switch (mode)
        {
            case GameMode.StackerLevels: return enableInTemples && levelNumber >= templeMinLevel;
            case GameMode.DailyChallenge: return enableInDaily;
            case GameMode.InfiniteStacker: return enableInInfinite;
            default: return false;
        }
    }
}
