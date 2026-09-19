using UnityEngine;

/// <summary>
/// Tuning for the tremor meter - the consequence that makes every drop matter.
///
/// Off-centre landings shake the temple; Perfect landings and Kukulkan calm it. When the
/// meter fills the temple trembles, and the next stone must land Perfect or the run ends.
///
/// Unlike the other run-content settings this one is ON without an asset: it is the core
/// loop, not an optional layer. Put an asset at Resources/StabilitySettings to tune it or
/// to switch it off with <see cref="enableStability"/>.
/// </summary>
[CreateAssetMenu(fileName = "StabilitySettings", menuName = "TamalStacker/Stability Settings", order = 5)]
public class StabilitySettings : ScriptableObject
{
    /// <summary>The Resources path TowerStability loads this from.</summary>
    public const string ResourcePath = "StabilitySettings";

    [Header("Master switch")]
    public bool enableStability = true;

    [Header("Where it applies")]
    public bool applyToInfinite = true;
    public bool applyToLevels = true;

    [Tooltip("Tremor is deterministic from landing accuracy, so it doesn't break the Daily's fairness contract.")]
    public bool applyToDaily = true;

    [Header("Meter (0 = still, 1 = trembling)")]
    [Tooltip("Tremor removed by a Perfect landing.")]
    [Range(0f, 1f)]
    public float perfectRelief = 0.15f;

    [Tooltip("Tremor added by a Good landing that only just missed Perfect.")]
    [Range(0f, 1f)]
    public float goodStrainMin = 0.05f;

    [Tooltip("Tremor added by a Good landing that only just made Good.")]
    [Range(0f, 1f)]
    public float goodStrainMax = 0.18f;

    [Tooltip("Tremor added by a Poor landing.")]
    [Range(0f, 1f)]
    public float poorStrain = 0.4f;

    [Tooltip("Tremor removed whenever Kukulkan straightens the temple (earned streak, offering stone, Stone Mercy).")]
    [Range(0f, 1f)]
    public float kukulkanRelief = 0.35f;

    [Header("Last chance")]
    [Tooltip("Tremor left after the player saves a trembling temple with a Perfect.")]
    [Range(0f, 1f)]
    public float tremorAfterSave = 0.5f;

    [Tooltip("A save also summons the Kukulkan straighten, so the rescue looks like one.")]
    public bool kukulkanOnSave = true;

    [Tooltip("Realtime seconds the warning banner holds.")]
    public float warningHoldSeconds = 2.2f;

    [Tooltip("Camera trauma applied repeatedly while trembling.")]
    [Range(0f, 1f)]
    public float trembleShakeTrauma = 0.12f;

    [Tooltip("Realtime seconds between tremble shakes.")]
    public float trembleShakeInterval = 0.3f;

    [Header("Guard rails")]
    [Tooltip("Never end a run on tremor while the player is still in the core-tap tutorial. " +
             "Deliberately NeedsTutorial, not IsInFtue - an Infinite-only player never leaves IsInFtue.")]
    public bool suppressDuringTutorial = true;

    [Tooltip("Highest the meter can climb while suppressed, so the bar still teaches without killing.")]
    [Range(0f, 0.99f)]
    public float tutorialCap = 0.9f;

    [Header("Serpent's Edge (risk window)")]
    [Tooltip("A choice on every drop: release near the end of the swing for bonus points, " +
             "paid for in tremor. Needs the tremor meter, so it is off whenever stability is.")]
    public bool enableEdge = true;

    [Tooltip("How far out the swing must be (0 centre, 1 end) for a drop to count. 0.92 is " +
             "roughly the outer quarter of each swing's time.")]
    [Range(0.5f, 0.99f)]
    public float edgePhaseThreshold = 0.92f;

    [Tooltip("Base-score multiplier for a stone released in the window. Stacks with combo, " +
             "block variant, boon and modifier multipliers.")]
    public float edgeScoreMultiplier = 3f;

    [Tooltip("The sweet spot: an edge drop released at least this far out lands as a Perfect " +
             "(combo grows and applies) wherever on the stack it lands. The rest of the window " +
             "scores by the real landing, so a sloppy edge tap is a Good and breaks the combo. " +
             "0.98 is about half the window's time; raise it to make the timing harder.")]
    [Range(0.9f, 0.999f)]
    public float edgeSweetSpotThreshold = 0.98f;

    [Tooltip("Tremor added by an edge landing on top of its accuracy tier. A Perfect edge " +
             "landing gets no Perfect relief - the risk is never free.")]
    [Range(0f, 1f)]
    public float edgeStrain = 0.3f;

    [Tooltip("Stones that must already be on the stack before the window opens.")]
    public int edgeMinStackHeight = 2;

    [Tooltip("The window unlocks once the tutorial is resolved AND the player has either " +
             "finished a temple or started this many runs. Keeps the first run about the tap.")]
    public int edgeUnlockRuns = 2;

    [Tooltip("Drops after the intro before the lesson counts as seen even if the player " +
             "never takes the edge.")]
    public int edgeIntroMaxDrops = 6;

    [Header("Serpent's Edge cue")]
    [Tooltip("Played each time the stone swings into the window. Empty = a code-generated " +
             "placeholder rattle.")]
    public AudioClip edgeRattleClip;

    [Range(0f, 1f)]
    public float edgeRattleVolume = 0.5f;

    [Tooltip("Light haptic tick each time the stone swings into the window.")]
    public bool edgeEntryHaptic = true;

    [Tooltip("Realtime seconds before the rattle/haptic may fire again, so a fast swing " +
             "doesn't turn it into a buzz.")]
    public float edgeCueCooldown = 0.5f;

    [Tooltip("Light haptic tick the moment the stone reaches the sweet spot - the timing " +
             "cue, alongside the rim flash.")]
    public bool edgeSweetSpotHaptic = true;

    [Header("Serpent's Edge UI (1080x1920 reference)")]
    [Tooltip("World-units offset of the edge strips from the top stone's upper surface - the " +
             "player watches the tower while aiming, so the cue sits on its rim.")]
    public float edgeZoneSurfaceOffset = 0f;

    [Tooltip("Width of each strip in world units, centred on where an edge drop lands. " +
             "Stones are 4 wide, so 1.2 covers about the outer third of the rim.")]
    public float edgeZoneWorldWidth = 1.2f;

    [Tooltip("Strip height when it has no sprite. With a sprite the height follows the art's " +
             "proportions instead.")]
    public float edgeZoneHeight = 22f;
    public float edgeZoneMinWidth = 60f;
    public int edgeCanvasSortingOrder = 2990;

    [Tooltip("Vertical position of the first-time Serpent's Edge intro banners (FTUE only; " +
             "in regular play the xN callout is a line on the landing label). The banner is a " +
             "full-width strip, so it sits in the lower screen, clear of the swing (where the " +
             "x3 markers are), the landing popup at screen center, and the Tremor meter.")]
    public float edgeIntroBannerYOffset = -560f;

    [Header("Meter UI (1080x1920 reference)")]
    public Vector2 barAnchor = new Vector2(1f, 0.5f);
    public Vector2 barPosition = new Vector2(-56f, 0f);
    public Vector2 barSize = new Vector2(36f, 520f);
    public int canvasSortingOrder = 3000;

    [Tooltip("Vertical offset of the one-time explainer banner.")]
    public float introBannerYOffset = 260f;
}
