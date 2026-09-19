using UnityEngine;

/// <summary>
/// Tunables for UI motion: the press squash on buttons/toggles and the scale-in/out of popups.
///
/// Loaded from Resources/UIMotionSettings when that asset exists; otherwise the code defaults
/// below apply, so the feature works with no setup. Create the asset via
/// Assets ▸ Create ▸ TamalStacker ▸ UI Motion Settings to tune without touching code.
/// </summary>
[CreateAssetMenu(menuName = "TamalStacker/UI Motion Settings", fileName = "UIMotionSettings")]
public class UIMotionSettings : ScriptableObject
{
    public const string ResourcePath = "UIMotionSettings";

    [Header("Press (buttons & toggles)")]
    [Tooltip("Scale multiplier while a control is held down.")]
    [Range(0.5f, 1f)] public float pressScale = 0.9f;

    [Tooltip("Seconds to squash down to Press Scale.")]
    public float pressDuration = 0.06f;

    [Tooltip("Scale the control springs past on release before settling at 1.")]
    [Range(1f, 1.5f)] public float releasePeakScale = 1.05f;

    [Tooltip("Seconds for the whole release spring (overshoot + settle).")]
    public float releaseDuration = 0.2f;

    [Tooltip("Seconds to ease back without a bounce when a press turns into a scroll drag.")]
    public float cancelReturnDuration = 0.1f;

    [Header("Toggle checkmark")]
    [Tooltip("Scale the checkmark overshoots to when it pops in.")]
    [Range(1f, 1.5f)] public float checkmarkPopPeak = 1.2f;

    [Tooltip("Seconds for the checkmark to pop in or shrink out.")]
    public float checkmarkPopDuration = 0.2f;

    [Header("Popup open")]
    [Tooltip("Scale a popup starts from when it opens.")]
    [Range(0f, 1f)] public float popInStartScale = 0.8f;

    [Tooltip("Scale it overshoots to before settling at 1.")]
    [Range(1f, 1.5f)] public float popInPeakScale = 1.05f;

    [Tooltip("Seconds for the whole open animation.")]
    public float popInDuration = 0.25f;

    [Header("Popup close")]
    [Tooltip("Scale a popup shrinks to as it fades out.")]
    [Range(0f, 1f)] public float popOutEndScale = 0.85f;

    [Tooltip("Seconds for the close animation. Actions tied to the close wait this long.")]
    public float popOutDuration = 0.12f;

    private static UIMotionSettings current;

    /// <summary>The asset from Resources, or a defaults instance when none exists.</summary>
    public static UIMotionSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<UIMotionSettings>(ResourcePath);
                if (current == null)
                {
                    current = CreateInstance<UIMotionSettings>();
                    current.hideFlags = HideFlags.DontSave;
                }
            }
            return current;
        }
    }
}
