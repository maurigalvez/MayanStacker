using UnityEngine;

/// <summary>
/// Tuning for the near-miss and clutch moments: small stingers for the risky landings.
///  - Clutch Save: a Perfect steadies a trembling tower (the "Steady" banner already shows).
///  - Razor Edge: a Poor landing within a hair of Good, with the run still alive.
/// Neither adds a fail state or changes scoring; they only add feedback. An asset in Resources; if
/// it is missing, or <see cref="enableMoments"/> is cleared, the game plays exactly as before.
/// </summary>
[CreateAssetMenu(fileName = "ClutchMomentSettings", menuName = "TamalStacker/Clutch Moment Settings", order = 9)]
public class ClutchMomentSettings : ScriptableObject
{
    public const string ResourcePath = "ClutchMomentSettings";

    private static ClutchMomentSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or the moments are switched off.</summary>
    public static ClutchMomentSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<ClutchMomentSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableMoments ? cached : null;
        }
    }

    [Header("Master switch")]
    public bool enableMoments = true;

    [Header("Clutch Save (Perfect steadies a trembling tower)")]
    [Range(0f, 1f)] public float clutchShake = 0.6f;
    [Range(0f, 0.3f)] public float clutchHitStop = 0.18f;
    [Range(0f, 0.15f)] public float clutchPunchZoom = 0.05f;
    public Color clutchFlash = new Color(1f, 0.82f, 0.35f, 0.4f);
    [Min(0.05f)] public float clutchFlashSeconds = 0.35f;

    [Header("Razor Edge (Poor within a hair of Good)")]
    [Tooltip("How far below the Good cutoff (landing accuracy, 0-1) still counts as a hair's breadth.")]
    [Range(0.005f, 0.15f)] public float razorMargin = 0.04f;
    [Tooltip("Stones that must land before another Razor Edge can fire, so it stays special.")]
    [Min(0)] public int razorCooldownStones = 3;
    [Range(0f, 0.3f)] public float razorHitStop = 0.1f;
    [Range(0f, 0.15f)] public float razorPunchZoom = 0.03f;
    [Range(0f, 1f)] public float razorShake = 0.2f;
    public Color razorBannerColor = new Color(0.95f, 0.55f, 0.3f, 1f);
    [Range(0.4f, 1.2f)] public float razorBannerSeconds = 0.9f;
}
