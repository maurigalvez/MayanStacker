using UnityEngine;

/// <summary>
/// Per-run overrides for the numbers behind the modifiers, so one modifier can be gentle on a
/// Monday ritual and brutal on the Sunday one without a second enum value.
///
/// Every field is "0 = keep the table's value" (<see cref="RunModifierDefinition.For"/>), and
/// a field only matters when a modifier that uses it is in play.
/// </summary>
[System.Serializable]
public class RunModifierTuning
{
    [Tooltip("Shrinking Offerings: share of the base width lost per stacked stone (table: 0.025). 0 = table.")]
    [Range(0f, 0.1f)]
    public float shrinkPerStone;

    [Tooltip("Shrinking Offerings: narrowest a stone gets, as a share of the base width (table: 0.45). 0 = table.")]
    [Range(0f, 1f)]
    public float minWidthScale;

    [Tooltip("Hot Stone: swings (end-to-end passes) after a stone arms before it drops by itself (table: 1.5). 0 = table.")]
    [Range(0f, 4f)]
    public float hotStoneSwings;

    [Tooltip("Feather Fall: gravity multiplier on a falling stone (table: 0.4). 0 = table.")]
    [Range(0f, 1f)]
    public float featherGravity;

    [Tooltip("Speed Run: swing speed multiplier (table: 1.6). 0 = table.")]
    [Range(0f, 3f)]
    public float swingSpeedMultiplier;

    /// <summary>Writes every set field into <paramref name="def"/>, for the modifiers it has.</summary>
    public void ApplyTo(ref RunModifierDefinition def)
    {
        if (shrinkPerStone > 0f && def.widthShrinkPerStone > 0f) def.widthShrinkPerStone = shrinkPerStone;
        if (minWidthScale > 0f && def.widthShrinkPerStone > 0f) def.minWidthScale = minWidthScale;
        if (hotStoneSwings > 0f && def.autoDropSwings > 0f) def.autoDropSwings = hotStoneSwings;
        if (featherGravity > 0f && def.gravityScale < 1f) def.gravityScale = featherGravity;
        if (swingSpeedMultiplier > 0f && def.swingSpeedMultiplier > 1f) def.swingSpeedMultiplier = swingSpeedMultiplier;
    }
}
