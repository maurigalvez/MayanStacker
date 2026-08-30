using UnityEngine;

/// <summary>
/// Identifies a kind of block. The id is what analytics and save data record, so treat
/// these names as a contract and add rather than rename.
/// </summary>
public enum BlockVariantId
{
    /// <summary>The ordinary block the game has always spawned.</summary>
    Standard,

    /// <summary>Narrow and high-scoring — the block you take a risk on.</summary>
    JadeSliver,

    /// <summary>Wide, heavy and low-scoring — the block you bail out with.</summary>
    HeavyStone,

    /// <summary>Ordinary to land, but shatters the run on a poor landing.</summary>
    CrackedStone,

    /// <summary>Landing it perfectly summons Kukulkan immediately.</summary>
    OfferingStone
}

/// <summary>
/// One block's deviation from the standard block.
///
/// Every value is a multiplier against whatever the spawner is already configured with,
/// never an absolute — the spawner's <c>objectSize</c>, the prefab's mass and the theme's
/// sprite all stay in charge, and a variant only bends them. That keeps block variety
/// compatible with the existing per-level and per-theme setup instead of overriding it.
///
/// A class rather than a struct so <see cref="Standard"/> can be a shared fallback and a
/// missing variant reads as null instead of an all-zeroes block worth no points.
/// </summary>
[System.Serializable]
public class BlockVariant
{
    [Tooltip("Which block this is. Standard means 'no change'.")]
    public BlockVariantId id = BlockVariantId.Standard;

    [Tooltip("Scales the block's width. Below 1 is harder to land on and to land ON.")]
    [Range(0.4f, 2f)]
    public float widthMultiplier = 1f;

    [Tooltip("Scales the block's mass. Heavier blocks settle a wobbling stack.")]
    [Range(0.25f, 4f)]
    public float massMultiplier = 1f;

    [Tooltip("Scales the block's surface friction. Higher grips the stack better.")]
    [Range(0.25f, 3f)]
    public float frictionMultiplier = 1f;

    [Tooltip("Scales the points this block awards, before combo and run multipliers.")]
    [Range(0f, 10f)]
    public float scoreMultiplier = 1f;

    [Tooltip("If true, a landing below the fragile threshold ends the run.")]
    public bool shattersOnPoorLanding = false;

    [Tooltip("If true, landing this block perfectly triggers the Kukulkan shift immediately.")]
    public bool grantsKukulkanShiftOnPerfect = false;

    [Tooltip("If true, tint overrides the spawner's random colour so the block reads as special.")]
    public bool overrideTint = false;

    [Tooltip("Colour applied when Override Tint is on.")]
    public Color tint = Color.white;

    [Tooltip("Localization key for the block's name, shown when it spawns. Optional.")]
    public string nameKey = "";

    [Tooltip("Localization key for the one line that teaches what this block does. Shown the first time the player ever meets it. Optional.")]
    public string descriptionKey = "";

    [Tooltip("Texture for this block. Left empty it keeps the theme sprite and only the tint tells it apart.")]
    public Sprite sprite;

    /// <summary>True for anything that isn't an ordinary block.</summary>
    public bool IsSpecial => id != BlockVariantId.Standard;

    /// <summary>
    /// The no-op variant. Shared and never mutated — used wherever a variant is absent so
    /// callers can read multipliers without null checks.
    /// </summary>
    public static readonly BlockVariant Standard = new BlockVariant();

    /// <summary>
    /// The stock set, used to seed a fresh <see cref="BlockVarietyTable"/> asset. Tuning
    /// afterwards happens on the asset in the inspector, not here.
    /// </summary>
    public static BlockVariant Preset(BlockVariantId id)
    {
        switch (id)
        {
            case BlockVariantId.JadeSliver:
                return new BlockVariant
                {
                    id = id,
                    widthMultiplier = 0.62f,
                    massMultiplier = 0.8f,
                    scoreMultiplier = 3f,
                    overrideTint = true,
                    tint = new Color(0.24f, 0.78f, 0.62f, 1f), // jade
                    nameKey = "block_jade_sliver",
                    descriptionKey = "block_jade_sliver_desc"
                };

            case BlockVariantId.HeavyStone:
                return new BlockVariant
                {
                    id = id,
                    widthMultiplier = 1.32f,
                    massMultiplier = 2.4f,
                    frictionMultiplier = 1.6f,
                    scoreMultiplier = 0.5f,
                    overrideTint = true,
                    tint = new Color(0.55f, 0.53f, 0.49f, 1f), // weathered stone
                    nameKey = "block_heavy_stone",
                    descriptionKey = "block_heavy_stone_desc"
                };

            case BlockVariantId.CrackedStone:
                return new BlockVariant
                {
                    id = id,
                    scoreMultiplier = 2f,
                    shattersOnPoorLanding = true,
                    overrideTint = true,
                    tint = new Color(0.72f, 0.35f, 0.26f, 1f), // clay
                    nameKey = "block_cracked_stone",
                    descriptionKey = "block_cracked_stone_desc"
                };

            case BlockVariantId.OfferingStone:
                return new BlockVariant
                {
                    id = id,
                    scoreMultiplier = 1.5f,
                    grantsKukulkanShiftOnPerfect = true,
                    overrideTint = true,
                    tint = new Color(0.85f, 0.70f, 0.32f, 1f), // gold
                    nameKey = "block_offering_stone",
                    descriptionKey = "block_offering_stone_desc"
                };

            case BlockVariantId.Standard:
            default:
                return new BlockVariant();
        }
    }
}
