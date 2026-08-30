using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The weighted table the spawner rolls against to decide what the next block is.
///
/// Lives as an asset in Resources so it can be tuned without touching a scene or prefab,
/// and so the whole feature can be switched off by clearing <see cref="enableVariants"/>.
/// If the asset is missing entirely the spawner falls back to standard blocks, meaning an
/// unmodified project behaves exactly as it did before block variety existed.
///
/// Guard rails are deliberately baked in here rather than left to callers: variety is a
/// spice, and a run that hands the player three cracked stones in a row reads as unfair
/// rather than varied.
/// </summary>
[CreateAssetMenu(fileName = "BlockVarietyTable", menuName = "TamalStacker/Block Variety Table", order = 2)]
public class BlockVarietyTable : ScriptableObject
{
    /// <summary>The Resources path the spawner loads this from when nothing is wired.</summary>
    public const string ResourcePath = "BlockVarietyTable";

    [System.Serializable]
    public class WeightedVariant
    {
        [Tooltip("Relative likelihood of this variant. Zero disables it without deleting the tuning.")]
        [Min(0f)]
        public float weight = 1f;

        public BlockVariant variant = new BlockVariant();
    }

    [Header("Master switch")]
    [Tooltip("Turn off to spawn nothing but standard blocks, as the game did originally.")]
    public bool enableVariants = true;

    [Header("Guard rails")]
    [Tooltip("No special blocks until the stack is at least this tall - the opening of a run should be readable.")]
    [Min(0)]
    public int minStackHeightForSpecials = 4;

    [Tooltip("Minimum ordinary blocks between two specials, so variety never becomes a gauntlet.")]
    [Min(0)]
    public int minGapBetweenSpecials = 2;

    [Tooltip("Suppress special blocks entirely while a new player is still in the FTUE.")]
    public bool suppressDuringFtue = true;

    [Header("Weights")]
    [Tooltip("Relative weight of an ordinary block. Raise it to make specials rarer.")]
    [Min(0f)]
    public float standardWeight = 10f;

    [Tooltip("The special blocks and how likely each is.")]
    public List<WeightedVariant> variants = new List<WeightedVariant>();

    // How many ordinary blocks have been spawned since the last special.
    private int blocksSinceSpecial = int.MaxValue;

    /// <summary>
    /// Picks the next block. Returns <see cref="BlockVariant.Standard"/> whenever variety
    /// is off, suppressed, or simply not rolled.
    /// </summary>
    /// <param name="stackHeight">Current stack height, for the opening-blocks guard.</param>
    /// <param name="allowSpecial">
    /// False for blocks the game has already given a meaning — the final block of a level,
    /// for instance, which uses its own sprite and must stay predictable.
    /// </param>
    /// <param name="specialChanceMultiplier">
    /// Scales the odds of any special. Altitude bands raise this so the upper reaches of a
    /// long run feel wilder than the base of the temple.
    /// </param>
    public BlockVariant Roll(int stackHeight, bool allowSpecial, float specialChanceMultiplier = 1f)
    {
        if (!enableVariants || !allowSpecial)
        {
            return Standard();
        }

        if (stackHeight < minStackHeightForSpecials)
        {
            return Standard();
        }

        if (blocksSinceSpecial < minGapBetweenSpecials)
        {
            return Standard();
        }

        if (suppressDuringFtue && FtueState.IsInFtue)
        {
            return Standard();
        }

        if (variants == null || variants.Count == 0)
        {
            return Standard();
        }

        // Weighted pick across "ordinary" plus every configured special.
        float specialTotal = 0f;
        for (int i = 0; i < variants.Count; i++)
        {
            WeightedVariant entry = variants[i];
            if (entry == null || entry.variant == null || entry.weight <= 0f) continue;
            specialTotal += entry.weight;
        }

        if (specialTotal <= 0f)
        {
            return Standard();
        }

        specialTotal *= Mathf.Max(0f, specialChanceMultiplier);
        float total = standardWeight + specialTotal;
        if (total <= 0f)
        {
            return Standard();
        }

        float roll = Random.value * total;
        if (roll < standardWeight)
        {
            return Standard();
        }

        // Walk the specials, re-applying the chance multiplier so relative odds hold.
        float cursor = standardWeight;
        for (int i = 0; i < variants.Count; i++)
        {
            WeightedVariant entry = variants[i];
            if (entry == null || entry.variant == null || entry.weight <= 0f) continue;

            cursor += entry.weight * Mathf.Max(0f, specialChanceMultiplier);
            if (roll < cursor)
            {
                blocksSinceSpecial = 0;
                return entry.variant;
            }
        }

        return Standard();
    }

    /// <summary>
    /// Clears the run-scoped spacing counter. Called when a run starts so the first special
    /// isn't gated by whatever the previous run happened to end on.
    /// </summary>
    public void ResetRunState()
    {
        blocksSinceSpecial = int.MaxValue;
    }

    private BlockVariant Standard()
    {
        if (blocksSinceSpecial < int.MaxValue) blocksSinceSpecial++;
        return BlockVariant.Standard;
    }

    /// <summary>
    /// Fills an empty table with the stock set. Used by the editor setup tool when it
    /// creates the asset; leaves a table that already has entries alone.
    /// </summary>
    public void SeedDefaults()
    {
        if (variants != null && variants.Count > 0) return;

        variants = new List<WeightedVariant>
        {
            new WeightedVariant { weight = 2.0f, variant = BlockVariant.Preset(BlockVariantId.JadeSliver) },
            new WeightedVariant { weight = 2.0f, variant = BlockVariant.Preset(BlockVariantId.HeavyStone) },
            new WeightedVariant { weight = 1.2f, variant = BlockVariant.Preset(BlockVariantId.CrackedStone) },
            new WeightedVariant { weight = 1.0f, variant = BlockVariant.Preset(BlockVariantId.OfferingStone) }
        };
    }
}
