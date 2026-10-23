using UnityEngine;

/// <summary>
/// Tuning for Second Chance: when a temple stone misses the tower, the player may watch a rewarded
/// ad (free for Remove Ads owners) to take that stone again instead of ending the run.
///
/// Temples only: it never appears in Infinite or the Daily, so no ranked score can be lifted by it.
/// Only a plain miss qualifies (the stone never joined the tower, or slipped off beside it), so the
/// tower is untouched and nothing needs rebuilding.
///
/// An asset in Resources; if it is missing, or <see cref="enableSecondChance"/> is cleared, a miss
/// ends the run exactly as before.
/// </summary>
[CreateAssetMenu(fileName = "SecondChanceSettings", menuName = "TamalStacker/Second Chance Settings", order = 11)]
public class SecondChanceSettings : ScriptableObject
{
    public const string ResourcePath = "SecondChanceSettings";

    private static SecondChanceSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or Second Chance is off.</summary>
    public static SecondChanceSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<SecondChanceSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableSecondChance ? cached : null;
        }
    }

    [Header("Master switch")]
    public bool enableSecondChance = true;

    [Header("Rules")]
    [Tooltip("Offers per run. 1 keeps it a rescue, not a way to play through every miss.")]
    [Min(1)] public int offersPerRun = 1;

    [Tooltip("The offer isn't made before this many stones are in the tower, so a fumbled opening just restarts.")]
    [Min(0)] public int minStones = 4;

    [Tooltip("Seconds the offer waits (unscaled) before it is taken as a decline.")]
    [Min(2f)] public float offerSeconds = 8f;
}
