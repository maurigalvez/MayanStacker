using UnityEngine;

/// <summary>
/// The player's tallest Infinite tower, kept apart from the high score.
///
/// The high score mixes in combo and accuracy bonuses, so it can't say where on screen the
/// best tower reached. This records the tower itself: its block count (the same number the
/// HUD's height counter shows) and the top edge of the stone that reached it, measured above
/// the ground so the ghost line lands in the same place whatever the ground's position.
///
/// PlayerPrefs-backed, and synced through <see cref="PlayerProgressData"/> by taking the
/// larger of the local and cloud values.
/// </summary>
public static class InfiniteBest
{
    public const string BlocksKey = "InfiniteBest_Blocks";
    public const string HeightKey = "InfiniteBest_Height";

    /// <summary>Most blocks ever stacked in one Infinite run. 0 until the first run ends.</summary>
    public static int Blocks => PlayerPrefs.GetInt(BlocksKey, 0);

    /// <summary>Top edge of that tower, in world units above the ground's top.</summary>
    public static float Height => PlayerPrefs.GetFloat(HeightKey, 0f);

    /// <summary>
    /// Stores the tower if it beats the current best. Block count decides, so the line never
    /// moves for a run the HUD would call shorter.
    /// </summary>
    /// <returns>True when the stored best was replaced.</returns>
    public static bool TryRecord(int blocks, float height)
    {
        if (blocks <= Blocks) return false;

        PlayerPrefs.SetInt(BlocksKey, blocks);
        PlayerPrefs.SetFloat(HeightKey, Mathf.Max(0f, height));
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>Copies the local best into outgoing cloud data.</summary>
    public static void WriteTo(PlayerProgressData data)
    {
        if (data == null) return;
        data.infiniteBestBlocks = Blocks;
        data.infiniteBestHeight = Height;
    }

    /// <summary>
    /// Adopts the cloud's best when it is taller. A taller local best is kept and reaches the
    /// cloud on the next save, because <see cref="WriteTo"/> reads from here.
    /// </summary>
    public static void MergeFromCloud(PlayerProgressData data)
    {
        if (data == null) return;
        TryRecord(data.infiniteBestBlocks, data.infiniteBestHeight);
    }

    /// <summary>Forgets the best tower. Used by data resets.</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(BlocksKey);
        PlayerPrefs.DeleteKey(HeightKey);
    }
}
