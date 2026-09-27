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

    [Tooltip("The band's outer end: landings less accurate than this are past the edge and " +
             "don't count (1 dead centre, 0 just off the stone). Accuracy scales with the " +
             "stones' widths, so the band sits the same way on every stone. 0.675 is 1.3 units " +
             "off-centre for two 4-wide stones - kept inside the base swing's reach (~1.55) so " +
             "a slightly drifted tower still reaches both sides.")]
    [Range(0.05f, 0.85f)]
    public float edgeBandOuterAccuracy = 0.675f;

    [Tooltip("The band's inner end - the whole band is the sweet spot. A drop that would land " +
             "between the outer accuracy and this lands as a Perfect (combo grows and applies) " +
             "and pays exactly the rim's number; anywhere else is an ordinary drop with no x3. " +
             "0.75 is 1.0 units off-centre for two 4-wide stones. Lower it towards the outer " +
             "accuracy to make the timing harder.")]
    [Range(0.05f, 0.89f)]
    public float edgeSweetSpotAccuracy = 0.75f;

    [Tooltip("Base-score multiplier for a stone released in the window. Stacks with combo, " +
             "block variant, boon and modifier multipliers.")]
    public float edgeScoreMultiplier = 3f;

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

    [Header("Serpent's Edge UI (1080x1920 reference)")]
    [Tooltip("World-units offset of the edge strips from the top stone's upper surface - the " +
             "player watches the tower while aiming, so the cue sits on its rim.")]
    public float edgeZoneSurfaceOffset = 0f;

    [Tooltip("Strip height. Each strip is exactly as wide as the edge band, so art is " +
             "stretched to this height rather than kept at its own proportions, which would " +
             "make it a hairline on a band only a few tenths of a unit wide.")]
    public float edgeZoneHeight = 22f;
    public int edgeCanvasSortingOrder = 2990;

    [Header("Meter UI (1080x1920 reference)")]
    public Vector2 barAnchor = new Vector2(1f, 0.5f);
    public Vector2 barPosition = new Vector2(-56f, 0f);
    public Vector2 barSize = new Vector2(36f, 520f);
    public int canvasSortingOrder = 3000;
}
