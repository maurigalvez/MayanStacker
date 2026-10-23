using UnityEngine;

/// <summary>
/// Tuning for terrace milestones: every few stones the tower "ascends a terrace" and the run gets
/// a landmark. A terrace is a short, non-blocking celebration (banner, flash, punch, haptic); every
/// few terraces is a grand one with a bigger beat, so the climb has landmarks of different weight.
///
/// The default spacing (7) never lands on an Altitude Band threshold (12, 24, 40, 60), so the two
/// announcements do not collide. An asset in Resources; if it is missing, or
/// <see cref="enableTerraces"/> is cleared, the game plays exactly as before.
/// </summary>
[CreateAssetMenu(fileName = "TerraceSettings", menuName = "TamalStacker/Terrace Settings", order = 8)]
public class TerraceSettings : ScriptableObject
{
    public const string ResourcePath = "TerraceSettings";

    private static TerraceSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or terraces are switched off.</summary>
    public static TerraceSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<TerraceSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableTerraces ? cached : null;
        }
    }

    [Header("Master switch")]
    public bool enableTerraces = true;

    [Header("Spacing")]
    [Tooltip("Stones in the tower per terrace. Keep it off the Altitude Band thresholds (12, 24, 40, 60).")]
    [Min(3)]
    public int stonesPerTerrace = 7;

    [Tooltip("Every Nth terrace is a grand one with a bigger beat.")]
    [Min(2)]
    public int grandEvery = 3;

    [Header("Ordinary terrace")]
    [Range(0f, 1f)] public float shake = 0.3f;
    [Range(0f, 0.1f)] public float punchZoom = 0.03f;
    public Color flash = new Color(0.55f, 0.9f, 0.7f, 0.3f);
    [Min(0.05f)] public float flashSeconds = 0.3f;
    public Color bannerColor = new Color(0.55f, 0.9f, 0.7f, 1f);

    [Header("Grand terrace")]
    [Range(0f, 1f)] public float grandShake = 0.55f;
    [Range(0f, 0.15f)] public float grandPunchZoom = 0.06f;
    [Range(0f, 0.25f)] public float grandHitStop = 0.08f;
    public Color grandFlash = new Color(1f, 0.82f, 0.35f, 0.4f);
    [Min(0.05f)] public float grandFlashSeconds = 0.4f;
    public Color grandBannerColor = new Color(1f, 0.82f, 0.35f, 1f);

    [Header("Banner")]
    [Tooltip("Seconds the banner stays up. Kept short: the climb never waits on it.")]
    [Range(0.4f, 1.2f)] public float bannerSeconds = 1.0f;
}
