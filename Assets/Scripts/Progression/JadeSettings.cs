using UnityEngine;

/// <summary>
/// Tuning for the jade wallet: what each in-run achievement pays and the guards that keep it from
/// being farmed. Jade is soft currency earned by playing; nothing here can be bought, and no
/// amount of it changes a ranked score (sinks are cosmetic, see the cosmetic catalog).
///
/// An asset in Resources; if it is missing, or <see cref="enableJade"/> is cleared, nothing is
/// earned and the game plays exactly as before. The wallet itself keeps its balance either way.
/// </summary>
[CreateAssetMenu(fileName = "JadeSettings", menuName = "TamalStacker/Jade Settings", order = 9)]
public class JadeSettings : ScriptableObject
{
    public const string ResourcePath = "JadeSettings";

    private static JadeSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or jade is switched off.</summary>
    public static JadeSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<JadeSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableJade ? cached : null;
        }
    }

    [Header("Master switch")]
    public bool enableJade = true;

    [Header("Per run (paid when the run ends)")]
    [Tooltip("Jade per Perfect landing.")]
    [Min(0)] public int perPerfect = 1;

    [Tooltip("Jade per Serpent's Edge landing.")]
    [Min(0)] public int perEdgeLanding = 2;

    [Tooltip("Jade for every full step of this many stones in the tower.")]
    [Min(1)] public int heightStepStones = 10;
    [Min(0)] public int perHeightStep = 3;

    [Tooltip("Jade per Sun Surge ignited.")]
    [Min(0)] public int perSunSurge = 5;

    [Tooltip("A run that falls before this many stones pays nothing, so instant restarts can't be farmed.")]
    [Min(0)] public int minStonesToPay = 3;

    [Tooltip("Most a single run can pay. The cap is what makes jade a pace, not a skill ceiling.")]
    [Min(1)] public int maxPerRun = 60;

    [Header("Milestones (paid immediately)")]
    [Tooltip("Jade per star gained for the first time on a temple.")]
    [Min(0)] public int perNewStar = 10;

    [Tooltip("Jade for the first Daily run of each UTC day, finished or not.")]
    [Min(0)] public int dailyParticipation = 10;

    [Tooltip("Extra jade when the Daily ritual is finished (once per UTC day).")]
    [Min(0)] public int dailyComplete = 15;
}
