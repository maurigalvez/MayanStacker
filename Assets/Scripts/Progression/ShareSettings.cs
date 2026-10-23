using UnityEngine;

/// <summary>
/// Tuning for the shareable result: a small Share chip on the result card that opens the system
/// share sheet with a one-line brag built from the run's best moment.
///
/// An asset in Resources; if it is missing, or <see cref="enableShare"/> is cleared, no chip is
/// offered and the card plays exactly as before.
/// </summary>
[CreateAssetMenu(fileName = "ShareSettings", menuName = "TamalStacker/Share Settings", order = 14)]
public class ShareSettings : ScriptableObject
{
    public const string ResourcePath = "ShareSettings";

    private static ShareSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or sharing is off.</summary>
    public static ShareSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<ShareSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableShare ? cached : null;
        }
    }

    [Tooltip("Master switch. Off: no Share chip on the result card.")]
    public bool enableShare = true;

    [Tooltip("Runs shorter than this many stones are not worth sharing; the chip stays hidden.")]
    public int minStones = 6;

    [Tooltip("A Perfect chain this long counts as a best moment worth naming.")]
    public int chainMoment = 5;

    [Tooltip("Store link added to the message. {0} is replaced by the app id. Empty = no link.")]
    public string storeUrlFormat = "https://play.google.com/store/apps/details?id={0}";
}
