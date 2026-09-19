using UnityEngine;

/// <summary>
/// Which leaderboard era the player's saved best scores belong to.
///
/// Scores are only submitted when they beat the saved best, so resetting the PlayFab boards
/// alone would leave every existing player off the new board until they beat a score earned
/// under the old rules. Bumping <see cref="Current"/> makes each device forget its old bests
/// once, and <see cref="PlayerProgressData"/> ignores bests in cloud saves from older seasons.
///
/// To start a new season: bump Current, and reset InfiniteStackerHighScores and every
/// StackerLevel_N leaderboard in PlayFab when the build goes live. DailyChallenge resets on
/// its own. The personal-best ghost line (<see cref="InfiniteBest"/>) is deliberately kept.
/// </summary>
public static class LeaderboardSeason
{
    /// <summary>1 was launch; 2 began with the altitude bands / Serpent's Edge scoring update.</summary>
    public const int Current = 2;

    public const string SeenKey = "Leaderboard_SeasonSeen";

    // Well above the 27 shipped levels; deleting a missing key is harmless.
    private const int MaxLevelNumberToClear = 100;

    // Before scene load so no manager has read a best score yet.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void MigrateLocalScores()
    {
        int seen = PlayerPrefs.GetInt(SeenKey, 0);
        if (seen >= Current) return;

        PlayerPrefs.DeleteKey("HighScore_InfiniteStacker");
        PlayerPrefs.DeleteKey("HighScore_Levels");
        for (int i = 1; i <= MaxLevelNumberToClear; i++)
        {
            PlayerPrefs.DeleteKey($"Level_{i}_HighScore");
        }

        // Scores queued offline were earned under the old rules.
        OfflineScoreQueue.ClearAll();

        PlayerPrefs.SetInt(SeenKey, Current);
        PlayerPrefs.Save();
        Debug.Log($"[LeaderboardSeason] Cleared best scores from season {seen}; now season {Current}");
    }
}
