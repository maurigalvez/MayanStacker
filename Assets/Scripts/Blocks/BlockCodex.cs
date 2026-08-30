using UnityEngine;

/// <summary>
/// Remembers which special blocks the player has already been introduced to.
///
/// Block variety is the one system the game hands the player without ever naming it: a
/// narrow jade block and a fat grey one simply appear, and nothing says that one is worth
/// triple or that the cracked one ends the run on a sloppy landing. This is the record
/// that lets the game teach each block exactly once, the first time it ever shows up, and
/// then never nag about it again.
///
/// Persisted per variant rather than as a single "seen the specials" flag, because the
/// blocks arrive weeks apart in practice — a player can meet the Jade Sliver in their
/// first session and the Offering Stone a hundred runs later, and the second one still
/// deserves its introduction.
/// </summary>
public static class BlockCodex
{
    private const string PP_PREFIX = "BlockSeen_";

    /// <summary>True once the player has been shown what this block is.</summary>
    public static bool HasSeen(BlockVariantId id)
    {
        if (id == BlockVariantId.Standard) return true;
        return PlayerPrefs.GetInt(Key(id), 0) == 1;
    }

    /// <summary>
    /// Records that the block has been introduced. Returns true only on the call that
    /// actually flipped the flag, so callers can use it as a "show the banner?" test
    /// without a separate <see cref="HasSeen"/> check racing against it.
    /// </summary>
    public static bool MarkSeen(BlockVariantId id)
    {
        if (id == BlockVariantId.Standard) return false;
        if (HasSeen(id)) return false;

        PlayerPrefs.SetInt(Key(id), 1);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>Wipes the codex so every block introduces itself again. Used by PlayerDataReset.</summary>
    public static void ResetAll()
    {
        foreach (BlockVariantId id in System.Enum.GetValues(typeof(BlockVariantId)))
        {
            if (id == BlockVariantId.Standard) continue;
            PlayerPrefs.DeleteKey(Key(id));
        }
        PlayerPrefs.Save();
    }

    private static string Key(BlockVariantId id) => PP_PREFIX + id;
}
