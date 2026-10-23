using UnityEngine;

/// <summary>
/// Tuning for the Perfect ladder: each Perfect in a row climbs one rung. The combo chime walks up a
/// marimba (major pentatonic) scale instead of a straight pitch slide, and the landing shake,
/// hit-stop and haptic grow with the rung. A Good or Poor landing drops the player back to the
/// bottom, so the ladder is the same chain the combo fill bar already shows.
///
/// An asset in Resources; if it is missing, or <see cref="enableLadder"/> is cleared, the old
/// linear pitch and the flat Perfect juice play exactly as before. Landing windows are untouched.
/// </summary>
[CreateAssetMenu(fileName = "PerfectLadderSettings", menuName = "TamalStacker/Perfect Ladder Settings", order = 6)]
public class PerfectLadderSettings : ScriptableObject
{
    public const string ResourcePath = "PerfectLadderSettings";

    private static PerfectLadderSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or the ladder is switched off.</summary>
    public static PerfectLadderSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<PerfectLadderSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableLadder ? cached : null;
        }
    }

    [Header("Master switch")]
    public bool enableLadder = true;

    [Header("Marimba scale")]
    [Tooltip("Semitones above the base chime for each rung (major pentatonic, one octave). The top rung repeats while the chain continues.")]
    public int[] rungSemitones = { 0, 2, 4, 7, 9, 12 };

    [Header("Landing juice")]
    [Tooltip("Extra camera trauma per rung above the first, added to the Perfect shake.")]
    [Range(0f, 0.15f)]
    public float shakePerRung = 0.05f;

    [Tooltip("Largest extra trauma the ladder may add.")]
    [Range(0f, 0.6f)]
    public float maxExtraShake = 0.25f;

    [Tooltip("Extra hit-stop seconds per rung above the first.")]
    [Range(0f, 0.03f)]
    public float hitStopPerRung = 0.008f;

    [Tooltip("Largest extra hit-stop the ladder may add, in seconds.")]
    [Range(0f, 0.12f)]
    public float maxExtraHitStop = 0.04f;

    [Tooltip("Rung (1 = first Perfect) from which the landing haptic becomes Heavy.")]
    [Min(2)]
    public int heavyHapticFromRung = 5;

    [Header("Top of the ladder")]
    [Tooltip("Optional conch / feather flourish played when the top rung is reached. Empty = no flourish (see HUMAN_TODO for the clip brief).")]
    public AudioClip topFlourishClip;

    /// <summary>Semitone offset for a rung (1 = first Perfect); the top rung repeats.</summary>
    public int SemitonesFor(int rung)
    {
        if (rungSemitones == null || rungSemitones.Length == 0) return 0;
        int i = Mathf.Clamp(rung - 1, 0, rungSemitones.Length - 1);
        return rungSemitones[i];
    }

    /// <summary>The rung that counts as the top of the ladder.</summary>
    public int TopRung => rungSemitones == null ? 1 : Mathf.Max(1, rungSemitones.Length);

    public float ExtraShake(int rung) => Mathf.Min(maxExtraShake, Mathf.Max(0, rung - 1) * shakePerRung);

    public float ExtraHitStop(int rung) => Mathf.Min(maxExtraHitStop, Mathf.Max(0, rung - 1) * hitStopPerRung);
}
