using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tuning for the power meter and the powers it fires.
///
/// Optional: with no asset at Resources/PowerSettings the system still runs on these field
/// defaults (like StabilitySettings), because powers replace the boon picker rather than sit
/// beside it. Clear <see cref="enablePowers"/> to switch the whole system off.
///
/// All tuning here is an estimate — there is no analytics data yet. Tune on device.
/// </summary>
[CreateAssetMenu(fileName = "PowerSettings", menuName = "TamalStacker/Power Settings", order = 5)]
public class PowerSettings : ScriptableObject
{
    public const string ResourcePath = "PowerSettings";

    [Header("Master switch")]
    public bool enablePowers = true;

    [Header("Where powers apply")]
    public bool applyToInfinite = true;

    [Tooltip("Temple levels. On by default: a power unlocked at Dzibilchaltún carries into the temples after it.")]
    public bool applyToLevels = true;

    [Tooltip("The Daily is a fairness contract - everyone gets the same run. Off in this slice.")]
    public bool applyToDaily = false;

    [Header("Meter")]
    [Tooltip("Perfect landings needed to fill the meter.")]
    [Min(1)]
    public int perfectsToFill = 4;

    [Tooltip("Good and Poor landings leave the meter alone. Good already breaks the combo; " +
             "draining the meter too would punish the same landing twice.")]
    public bool nonPerfectDrains = false;

    [Tooltip("Charges held at once. The first slice caps it at one.")]
    [Min(1)]
    public int maxStoredCharges = 1;

    [Tooltip("Realtime seconds the button stays untappable after it fills, so the tap that " +
             "placed the stone can't fire the power by accident.")]
    [Range(0f, 1.5f)]
    public float armDelaySeconds = 0.45f;

    [Tooltip("Hide the meter while the core tap is still being taught. Deliberately the " +
             "tutorial, not FtueState.IsInFtue - that never clears for an Infinite-only player.")]
    public bool suppressDuringTutorial = true;

    [Header("Jaguar Slam")]
    [Tooltip("Realtime seconds the top stone takes to slide back to centre.")]
    [Range(0.08f, 0.8f)]
    public float slamDuration = 0.22f;

    [Tooltip("How far the stone hops while it slides, in world units, so the knock reads on video.")]
    [Range(0f, 1f)]
    public float slamHop = 0.35f;

    [Range(0f, 1f)] public float slamShake = 0.75f;
    [Range(0f, 0.3f)] public float slamHitStop = 0.09f;
    [Range(0f, 0.3f)] public float slamZoom = 0.08f;
    public Color slamFlash = new Color(1f, 0.78f, 0.35f, 0.35f);

    // No tremor relief, by design: the slam fixes the tower's shape, not the tremor, so it
    // can't make the meter trivial. It is allowed while trembling - the next landing still
    // has to be Perfect.

    [Header("Quetzal Feather")]
    [Tooltip("Drops the slow motion lasts for.")]
    [Min(1)]
    public int quetzalDrops = 3;

    [Tooltip("Swing speed while the feather is active (1 = normal).")]
    [Range(0.2f, 1f)]
    public float quetzalSwingScale = 0.5f;

    [Tooltip("Gravity on a stone dropped under the feather (1 = normal), so the fall reads slow too.")]
    [Range(0.2f, 1f)]
    public float quetzalFallScale = 0.55f;

    [Tooltip("Screen-edge tint while the feather is active.")]
    public Color quetzalTint = new Color(0.25f, 0.78f, 0.55f, 0.35f);

    [Header("Tzolk'in Rewind")]
    [Tooltip("Stones taken back off the top. Fewer when the tower is shorter; the foundation always stays.")]
    [Range(1, 6)]
    public int rewindStones = 3;

    [Tooltip("Seconds each stone takes to fly back up to the hook, fading out.")]
    [Range(0.2f, 1.5f)]
    public float rewindStoneSeconds = 0.6f;

    [Tooltip("Delay between one stone leaving and the next, top stone first.")]
    [Range(0f, 0.4f)]
    public float rewindStagger = 0.14f;

    [Tooltip("How long the screen shows the turning calendar and the time wash.")]
    [Range(0.4f, 2.5f)]
    public float rewindEffectSeconds = 1.2f;

    [Tooltip("Tint of the time wash over the screen while the calendar turns back.")]
    public Color rewindWash = new Color(0.85f, 0.72f, 0.45f, 0.28f);

    [Range(0f, 1f)] public float rewindShake = 0.2f;
    [Range(0f, 0.3f)] public float rewindHitStop = 0.05f;

    // Kukulkan's Call fires the Kukulkan shift through GameManager, so the straighten, slow-mo,
    // flash and sting match the offering stone and the tremor save. It is the only way a
    // player summons Kukulkan at will: where powers run, a Perfect streak no longer does
    // (GameManager.StreakShiftActive). The strongest power, so it unlocks last.

    [Header("First-time intro")]
    [Tooltip("Drops after the first full meter before the intro counts as seen without a use.")]
    [Min(1)]
    public int introMaxDrops = 4;

    [Header("Button (code-built layout; a prefab's own placement wins)")]
    [Tooltip("Bottom-left, on the same line as the Kukulkan medallion - keep it in the HUD strip, off the playfield.")]
    public Vector2 buttonAnchor = new Vector2(0f, 0f);
    public Vector2 buttonPosition = new Vector2(125f, 125f);
    public Vector2 buttonSize = new Vector2(160f, 160f);
    public int canvasSortingOrder = 3010;

    [Header("Catalog")]
    [Tooltip("Power assets. A power with no asset here uses its code default.")]
    public List<PowerDefinition> powers = new List<PowerDefinition>();

    /// <summary>True when the meter should run in <paramref name="mode"/>.</summary>
    public bool AppliesTo(GameMode mode)
    {
        if (!enablePowers) return false;

        switch (mode)
        {
            case GameMode.InfiniteStacker: return applyToInfinite;
            case GameMode.StackerLevels: return applyToLevels;
            case GameMode.DailyChallenge: return applyToDaily;
            default: return false;
        }
    }

    private readonly Dictionary<PowerId, PowerDefinition> defaults = new Dictionary<PowerId, PowerDefinition>();

    /// <summary>The asset for <paramref name="id"/>, or its code default.</summary>
    public PowerDefinition Get(PowerId id)
    {
        for (int i = 0; i < powers.Count; i++)
        {
            if (powers[i] != null && powers[i].id == id) return powers[i];
        }

        if (!defaults.TryGetValue(id, out PowerDefinition def) || def == null)
        {
            def = PowerDefinition.CreateDefault(id);
            defaults[id] = def;
        }
        return def;
    }

    /// <summary>
    /// The localized description of <paramref name="id"/>, with its number filled in: {0} is the
    /// Quetzal Feather's drop count and the Tzolk'in Rewind's stone count.
    /// </summary>
    public string Describe(PowerId id)
    {
        int arg = id == PowerId.TzolkinRewind ? rewindStones : quetzalDrops;
        return LocalizationManager.Get(Get(id).descriptionKey, arg);
    }

    private static PowerSettings current;

    /// <summary>The Resources asset, or a code-default instance when there is none.</summary>
    public static PowerSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<PowerSettings>(ResourcePath);
                if (current == null) current = CreateInstance<PowerSettings>();
            }
            return current;
        }
    }
}
