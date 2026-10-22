using UnityEngine;

/// <summary>
/// Tuning for the swing's tempo: a gentle learner pace on the first stones, then a ramp so a
/// run quickens instead of swinging at one speed from the first stone to the last.
///
/// An asset in Resources so it can be tuned (or switched off) without opening a scene. If the
/// asset is missing, or <see cref="enableTempo"/> is cleared, the swing plays exactly as it did
/// before. Only the swing's speed changes: the landing windows are never touched.
/// </summary>
[CreateAssetMenu(fileName = "TempoSettings", menuName = "TamalStacker/Tempo Settings", order = 5)]
public class TempoSettings : ScriptableObject
{
    /// <summary>The Resources path the swing loads this from.</summary>
    public const string ResourcePath = "TempoSettings";

    [Header("Master switch")]
    public bool enableTempo = true;

    [Header("Learner stones (every mode)")]
    [Tooltip("The first stones of a run swing slower so the rhythm can be read before it quickens.")]
    [Range(0, 6)]
    public int learnerStones = 3;

    [Tooltip("Swing speed on the learner stones, as a share of the normal speed.")]
    [Range(0.6f, 1f)]
    public float learnerScale = 0.9f;

    [Tooltip("How quickly the swing eases from one tempo to the next (higher = snappier).")]
    [Range(0.5f, 8f)]
    public float easeRate = 3f;

    [Header("Temples")]
    [Tooltip("Slowest swing a temple may have, per tier: temples 1-6, 7-14, 15-21, 22-27. Stops the early temples feeling dead; temples already faster are untouched.")]
    public float[] templeTierFloors = { 1.05f, 1.15f, 1.25f, 1.35f };

    [Tooltip("Speed added over the floor by the end of the ramp (an absolute swing multiplier, so 0.10 on a x1.2 temple ends at x1.3).")]
    [Range(0f, 0.5f)]
    public float templeRampGain = 0.10f;

    [Tooltip("Share of the temple's required height at which the ramp tops out.")]
    [Range(0.2f, 1f)]
    public float templeRampEndFraction = 0.6f;

    [Header("Daily rituals")]
    [Tooltip("Swing speed gained by the end of the ramp on a difficulty-1 ritual (0.15 = x1.15).")]
    [Range(0f, 0.6f)]
    public float dailyGainEasy = 0.15f;

    [Tooltip("Swing speed gained by the end of the ramp on every other ritual.")]
    [Range(0f, 0.6f)]
    public float dailyGain = 0.35f;

    [Tooltip("Stone at which the Daily ramp tops out.")]
    [Min(2)]
    public int dailyRampEndStone = 20;

    [Tooltip("Speed Run and Hot Stone rituals already pressure the player, so they keep their own tempo.")]
    public bool skipPressureRituals = true;

    [Header("Infinite")]
    [Tooltip("Stack height at which the swing starts quickening (the scene's own value is 10).")]
    [Min(1)]
    public int infiniteStartBlock = 5;

    [Tooltip("Stones the speed ramp takes to reach its maximum (the scene's own value is 50).")]
    [Min(10)]
    public int infiniteRampStones = 40;

    [Tooltip("Curve exponent of the ramp: 1 = steady, higher = back-loaded (the scene's own curve is 2).")]
    [Range(1f, 3f)]
    public float infiniteCurveExponent = 1.4f;

    /// <summary>Which of the four temple tiers a level number falls into.</summary>
    public static int TierOf(int levelNumber)
    {
        if (levelNumber <= 6) return 0;
        if (levelNumber <= 14) return 1;
        if (levelNumber <= 21) return 2;
        return 3;
    }

    /// <summary>
    /// The tempo multiplier on top of a temple's own swing speed, for a stack of
    /// <paramref name="stones"/>. 1 = the level's speed unchanged.
    /// </summary>
    public float TempleScale(int levelNumber, float levelSpeedMultiplier, int stones, int requiredStones)
    {
        float levelSpeed = Mathf.Max(0.1f, levelSpeedMultiplier);
        float floor = 0f;
        if (templeTierFloors != null && templeTierFloors.Length > 0)
            floor = templeTierFloors[Mathf.Min(TierOf(levelNumber), templeTierFloors.Length - 1)];

        float rampStones = Mathf.Max(1f, templeRampEndFraction * Mathf.Max(1, requiredStones));
        float progress = Mathf.Clamp01(stones / rampStones);
        float target = Mathf.Max(levelSpeed, floor) + templeRampGain * progress;
        return target / levelSpeed * LearnerFactor(stones);
    }

    /// <summary>The Daily ramp's multiplier for <paramref name="stones"/> placed.</summary>
    public float DailyScale(int difficulty, int stones)
    {
        float progress = Mathf.Clamp01(stones / (float)Mathf.Max(2, dailyRampEndStone));
        float gain = difficulty <= 1 ? dailyGainEasy : dailyGain;
        return (1f + gain * progress) * LearnerFactor(stones);
    }

    /// <summary>The slow opening stones, for any mode that has no ramp of its own on top.</summary>
    public float LearnerFactor(int stones)
    {
        return stones < learnerStones ? learnerScale : 1f;
    }
}
