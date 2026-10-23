using UnityEngine;

/// <summary>
/// Tuning for Sun Surge: a long enough chain of Perfects sets the sun alight for the next few
/// stones. Each stone placed during the surge trails gold, lands with a slow-mo beat and a camera
/// punch, and pays a flat bonus folded into the single points total (no maths on screen).
///
/// Layering with the Offering Stone: the Offering is earned at 4 Perfects and its own Perfect calls
/// Kukulkan (the 5th). The surge waits until the 8th Perfect in a row, so the two never fire on the
/// same landing and the chain keeps paying out: Offering, Kukulkan, then the Sun.
///
/// An asset in Resources; if it is missing, or <see cref="enableSurge"/> is cleared, the game plays
/// exactly as before. Landing windows and the leaderboard rules are untouched: the bonus is earned
/// in the run, never bought.
/// </summary>
[CreateAssetMenu(fileName = "SunSurgeSettings", menuName = "TamalStacker/Sun Surge Settings", order = 7)]
public class SunSurgeSettings : ScriptableObject
{
    public const string ResourcePath = "SunSurgeSettings";

    private static SunSurgeSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or Sun Surge is switched off.</summary>
    public static SunSurgeSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<SunSurgeSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableSurge ? cached : null;
        }
    }

    [Header("Master switch")]
    public bool enableSurge = true;

    [Header("Trigger")]
    [Tooltip("Perfects in a row that set the sun alight. Above the Offering (4) and Kukulkan (5) moments on purpose.")]
    [Min(5)]
    public int perfectsToTrigger = 8;

    [Tooltip("Stones that must land after a surge ends before the chain counts toward the next one.")]
    [Min(0)]
    public int cooldownStones = 6;

    [Header("Surge")]
    [Tooltip("Stones the surge lasts, counting every landing (a Good or Poor does not end it early).")]
    [Min(1)]
    public int surgeStones = 4;

    [Tooltip("Points multiplier folded into every landing during the surge. Shown only as the single total.")]
    [Range(1f, 3f)]
    public float scoreMultiplier = 1.5f;

    [Header("Ignition")]
    [Tooltip("Camera trauma at ignition.")]
    [Range(0f, 1f)]
    public float igniteShake = 0.6f;

    [Tooltip("Camera punch-zoom fraction at ignition.")]
    [Range(0f, 0.15f)]
    public float ignitePunchZoom = 0.05f;

    public Color igniteFlash = new Color(1f, 0.82f, 0.35f, 0.5f);

    [Min(0.05f)]
    public float igniteFlashSeconds = 0.45f;

    [Header("Each landing in the surge")]
    [Tooltip("Hit-stop seconds on a landing (the slow-mo beat).")]
    [Range(0f, 0.25f)]
    public float landingHitStop = 0.12f;

    [Tooltip("Camera punch-zoom fraction on a landing.")]
    [Range(0f, 0.1f)]
    public float landingPunchZoom = 0.025f;

    [Tooltip("Extra camera trauma on a landing.")]
    [Range(0f, 0.6f)]
    public float landingShake = 0.25f;

    [Header("Music")]
    [Tooltip("Music pitch while the surge burns (1 = unchanged). Small values read as intensity, not as a glitch.")]
    [Range(1f, 1.15f)]
    public float musicPitch = 1.05f;

    [Header("Banner")]
    public Color bannerColor = new Color(1f, 0.82f, 0.35f, 1f);
}
