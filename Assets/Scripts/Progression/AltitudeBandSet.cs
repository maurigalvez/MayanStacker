using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One stretch of a tall run, with the flavour and rules that apply inside it.
/// </summary>
[System.Serializable]
public class AltitudeBand
{
    [Tooltip("Localization key for the band's name, announced when the player enters it.")]
    public string nameKey = "";

    [Tooltip("Stack height at which this band begins. The first band should start at 0.")]
    [Min(0)]
    public int startsAtBlock = 0;

    [Tooltip("Switch the scene's time of day when this band begins.")]
    public bool applyTimeOfDay = false;

    [Tooltip("Time of day to switch to. Ignored unless Apply Time Of Day is on.")]
    public StyleManager.TimeOfDay timeOfDay = StyleManager.TimeOfDay.Morning;

    [Tooltip("Scales how often special blocks appear inside this band. 1 = unchanged.")]
    [Range(0f, 4f)]
    public float specialBlockChanceMultiplier = 1f;

    [Tooltip("Announce this band with an on-screen banner when the player reaches it.")]
    public bool announce = true;

    [Tooltip("Colour of the announcement banner.")]
    public Color announceColor = new Color(0.79f, 0.64f, 0.29f, 1f);
}

/// <summary>
/// The ladder of altitude bands for a long run.
///
/// A run of Infinite Stacker is otherwise a flat line: the same block, the same backdrop,
/// forever. Bands give it an arc — the temple darkens, stranger stones start appearing,
/// and each threshold is a named landmark the player can remember reaching.
///
/// Deliberately does NOT touch swing speed. SpawnerHolder already ramps swing with stack
/// height (see its Height-Based Swing Scaling settings), and having two systems write the
/// same value would make both unpredictable. Bands own the look and the block mix; the
/// spawner keeps owning difficulty.
/// </summary>
[CreateAssetMenu(fileName = "AltitudeBandSet", menuName = "TamalStacker/Altitude Band Set", order = 3)]
public class AltitudeBandSet : ScriptableObject
{
    /// <summary>The Resources path the band manager loads this from.</summary>
    public const string ResourcePath = "AltitudeBandSet";

    [Header("Master switch")]
    [Tooltip("Turn off to disable altitude bands entirely.")]
    public bool enableBands = true;

    [Header("Where bands apply")]
    [Tooltip("Infinite Stacker is the mode long enough for bands to matter.")]
    public bool applyToInfinite = true;

    [Tooltip("Daily runs are capped at a fixed block count, so bands rarely trigger. Off by default.")]
    public bool applyToDaily = false;

    [Tooltip("Levels have short fixed objectives and their own art direction. Off by default.")]
    public bool applyToLevels = false;

    [Header("Bands")]
    [Tooltip("Ordered low to high. The first entry is the run's starting state and is never announced.")]
    public List<AltitudeBand> bands = new List<AltitudeBand>();

    /// <summary>
    /// Index of the band covering <paramref name="stackHeight"/>, or 0 when none match.
    /// </summary>
    public int IndexForHeight(int stackHeight)
    {
        if (bands == null || bands.Count == 0) return 0;

        int index = 0;
        for (int i = 0; i < bands.Count; i++)
        {
            if (bands[i] != null && stackHeight >= bands[i].startsAtBlock)
            {
                index = i;
            }
        }

        return index;
    }

    /// <summary>Band at <paramref name="index"/>, or null when out of range.</summary>
    public AltitudeBand At(int index)
    {
        if (bands == null || index < 0 || index >= bands.Count) return null;
        return bands[index];
    }

    /// <summary>True when this mode should run bands at all.</summary>
    public bool AppliesTo(GameMode mode)
    {
        if (!enableBands) return false;

        switch (mode)
        {
            case GameMode.InfiniteStacker: return applyToInfinite;
            case GameMode.DailyChallenge: return applyToDaily;
            case GameMode.StackerLevels: return applyToLevels;
            default: return false;
        }
    }

    /// <summary>
    /// Fills an empty set with the stock ladder. Called by the editor setup tool on
    /// creation; a set that already has bands is left alone.
    /// </summary>
    public void SeedDefaults()
    {
        if (bands != null && bands.Count > 0) return;

        bands = new List<AltitudeBand>
        {
            // The foundation: ordinary blocks, daylight, no announcement.
            new AltitudeBand
            {
                nameKey = "band_foundation",
                startsAtBlock = 0,
                applyTimeOfDay = true,
                timeOfDay = StyleManager.TimeOfDay.Morning,
                specialBlockChanceMultiplier = 1f,
                announce = false
            },
            new AltitudeBand
            {
                nameKey = "band_canopy",
                startsAtBlock = 12,
                specialBlockChanceMultiplier = 1.35f,
                announceColor = RunOverlayUIColors.Jade
            },
            new AltitudeBand
            {
                nameKey = "band_dusk",
                startsAtBlock = 24,
                applyTimeOfDay = true,
                timeOfDay = StyleManager.TimeOfDay.Sunset,
                specialBlockChanceMultiplier = 1.7f,
                announceColor = RunOverlayUIColors.Gold
            },
            new AltitudeBand
            {
                nameKey = "band_night",
                startsAtBlock = 40,
                applyTimeOfDay = true,
                timeOfDay = StyleManager.TimeOfDay.Night,
                specialBlockChanceMultiplier = 2.1f,
                announceColor = RunOverlayUIColors.Parchment
            },
            new AltitudeBand
            {
                nameKey = "band_serpent",
                startsAtBlock = 60,
                applyTimeOfDay = true,
                timeOfDay = StyleManager.TimeOfDay.Night,
                specialBlockChanceMultiplier = 2.6f,
                announceColor = RunOverlayUIColors.Clay
            }
        };
    }
}

/// <summary>
/// Palette constants usable from a ScriptableObject field initialiser, where the static
/// readonly fields on <see cref="RunOverlayUI"/> would be awkward to reference.
/// </summary>
internal static class RunOverlayUIColors
{
    internal static readonly Color Jade = new Color(0.09f, 0.42f, 0.34f, 1f);
    internal static readonly Color Clay = new Color(0.66f, 0.25f, 0.16f, 1f);
    internal static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    internal static readonly Color Parchment = new Color(0.93f, 0.90f, 0.82f, 1f);
}
