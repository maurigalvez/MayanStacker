using UnityEngine;

/// <summary>
/// The blessings Kukulkan offers mid-run. Names are recorded by analytics, so add rather
/// than rename.
/// </summary>
public enum BoonId
{
    /// <summary>Double points for the next few blocks.</summary>
    SerpentsFavor,

    /// <summary>The next few blocks are noticeably wider.</summary>
    WideFoundation,

    /// <summary>Straightens the stack immediately.</summary>
    StoneMercy,

    /// <summary>Absorbs the next few combo breaks.</summary>
    JadeEye
}

/// <summary>
/// One boon's rules and presentation.
///
/// A stacker with only timing has no decisions in it: every run is the same input
/// sequence executed better or worse. Boons are the smallest thing that changes that —
/// every few blocks the player picks one of three, and two runs of equal height start
/// differing in how they were built rather than only in how cleanly they were tapped.
///
/// Each boon is deliberately a trade the player can reason about: more points, more
/// safety, or a reset of accumulated damage.
/// </summary>
public struct BoonDefinition
{
    public BoonId id;

    /// <summary>How many blocks the effect lasts. Zero means it resolves instantly.</summary>
    public int durationInBlocks;

    /// <summary>Score multiplier while active. 1 = no change.</summary>
    public float scoreMultiplier;

    /// <summary>Block width multiplier while active. 1 = no change.</summary>
    public float widthMultiplier;

    /// <summary>Number of combo breaks absorbed.</summary>
    public int comboShields;

    /// <summary>Straightens the stack the moment it's chosen.</summary>
    public bool straightensStack;

    public string nameKey;
    public string descriptionKey;
    public Color accentColor;

    /// <summary>Every boon that can be offered, in no particular order.</summary>
    public static readonly BoonId[] All =
    {
        BoonId.SerpentsFavor,
        BoonId.WideFoundation,
        BoonId.StoneMercy,
        BoonId.JadeEye
    };

    public static BoonDefinition For(BoonId id)
    {
        switch (id)
        {
            case BoonId.WideFoundation:
                return new BoonDefinition
                {
                    id = id,
                    durationInBlocks = 3,
                    scoreMultiplier = 1f,
                    widthMultiplier = 1.4f,
                    comboShields = 0,
                    straightensStack = false,
                    nameKey = "boon_wide_foundation",
                    descriptionKey = "boon_wide_foundation_desc",
                    accentColor = new Color(0.55f, 0.53f, 0.49f, 1f) // stone
                };

            case BoonId.StoneMercy:
                return new BoonDefinition
                {
                    id = id,
                    durationInBlocks = 0,
                    scoreMultiplier = 1f,
                    widthMultiplier = 1f,
                    comboShields = 0,
                    straightensStack = true,
                    nameKey = "boon_stone_mercy",
                    descriptionKey = "boon_stone_mercy_desc",
                    accentColor = new Color(0.79f, 0.64f, 0.29f, 1f) // gold
                };

            case BoonId.JadeEye:
                return new BoonDefinition
                {
                    id = id,
                    durationInBlocks = 0,
                    scoreMultiplier = 1f,
                    widthMultiplier = 1f,
                    comboShields = 2,
                    straightensStack = false,
                    nameKey = "boon_jade_eye",
                    descriptionKey = "boon_jade_eye_desc",
                    accentColor = new Color(0.24f, 0.78f, 0.62f, 1f) // jade
                };

            case BoonId.SerpentsFavor:
            default:
                return new BoonDefinition
                {
                    id = BoonId.SerpentsFavor,
                    durationInBlocks = 5,
                    scoreMultiplier = 2f,
                    widthMultiplier = 1f,
                    comboShields = 0,
                    straightensStack = false,
                    nameKey = "boon_serpents_favor",
                    descriptionKey = "boon_serpents_favor_desc",
                    accentColor = new Color(0.66f, 0.25f, 0.16f, 1f) // clay
                };
        }
    }
}
