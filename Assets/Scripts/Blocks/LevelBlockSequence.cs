using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One authored position in a level's block order.
///
/// <see cref="rolled"/> is the default so a half-authored sequence is useful: pin the two
/// or three blocks that give the level its shape and let the seeded table fill the rest,
/// rather than having to specify every slot to specify any of them.
/// </summary>
[System.Serializable]
public class LevelBlockSlot
{
    [Tooltip("Leave on to let the seeded variety table decide this block. Turn off to pin the block below.")]
    public bool rolled = true;

    [Tooltip("The block this position always spawns. Standard means an ordinary block.")]
    public BlockVariantId variant = BlockVariantId.Standard;
}

/// <summary>
/// An optional hand-authored block order for a level.
///
/// Seeding <see cref="RunRandom"/> already makes a level repeatable, but repeatable is not
/// the same as designed: a hash decides what the sequence is, and a dull one is dull for
/// everyone, forever. This is the deliberate version — "level 12 is the cracked-stone
/// level" as a decision rather than a hope.
///
/// The two layers compose. Slots are read by block index from the bottom of the stack;
/// anything past the end of the list, and any slot left <see cref="LevelBlockSlot.rolled"/>,
/// falls through to the seeded roll. An empty sequence is the normal case and means the
/// level is entirely seed-driven.
/// </summary>
[System.Serializable]
public class LevelBlockSequence
{
    [Tooltip("Block order from the bottom of the stack up. Positions past the end are rolled from the variety table.")]
    public List<LevelBlockSlot> slots = new List<LevelBlockSlot>();

    /// <summary>True when nothing is authored, so callers can skip the lookup entirely.</summary>
    public bool IsEmpty => slots == null || slots.Count == 0;

    /// <summary>
    /// The block pinned at <paramref name="blockIndex"/>, counting the first block of the
    /// run as 0. Returns false when that position is unauthored and should be rolled.
    /// </summary>
    public bool TryGetPinned(int blockIndex, out BlockVariantId id)
    {
        id = BlockVariantId.Standard;

        if (IsEmpty || blockIndex < 0 || blockIndex >= slots.Count) return false;

        LevelBlockSlot slot = slots[blockIndex];
        if (slot == null || slot.rolled) return false;

        id = slot.variant;
        return true;
    }
}
