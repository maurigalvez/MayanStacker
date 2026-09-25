using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable data structure for storing player progress
/// Used for cloud sync via PlayFab Player Data API
/// </summary>
[Serializable]
public class PlayerProgressData
{
    /// <summary>
    /// Dictionary mapping level number to stars earned (0-3)
    /// </summary>
    [SerializeField]
    public Dictionary<int, int> levelStars = new Dictionary<int, int>();

    /// <summary>
    /// Dictionary mapping level number to high score
    /// </summary>
    [SerializeField]
    public Dictionary<int, int> levelHighScores = new Dictionary<int, int>();

    /// <summary>
    /// High score for Infinite Stacker mode
    /// </summary>
    public int infiniteStackerHighScore = 0;

    /// <summary>
    /// Leaderboard season the high scores were earned in (see LeaderboardSeason).
    /// Saves from before seasons existed read as 0.
    /// </summary>
    public int leaderboardSeason = 0;

    /// <summary>
    /// Most blocks stacked in one Infinite run (see InfiniteBest)
    /// </summary>
    public int infiniteBestBlocks = 0;

    /// <summary>
    /// Top edge of that tower in world units above the ground (see InfiniteBest)
    /// </summary>
    public float infiniteBestHeight = 0f;

    /// <summary>
    /// Timestamp of last sync (Unix timestamp)
    /// </summary>
    public long lastSyncTimestamp = 0;

    /// <summary>
    /// Achievement progress data (JSON serialized)
    /// Stored as JSON string for flexibility
    /// </summary>
    public string achievementProgressJson = "";

    /// <summary>
    /// Theme unlock status - Sunset theme unlocked
    /// </summary>
    public bool sunsetThemeUnlocked = false;

    /// <summary>
    /// Theme unlock status - Night theme unlocked
    /// </summary>
    public bool nightThemeUnlocked = false;

    /// <summary>
    /// Powers the player has earned (PowerId names, see PowerUnlocks). Merged as a union.
    /// </summary>
    public List<string> unlockedPowers = new List<string>();

    /// <summary>
    /// Creates a new empty PlayerProgressData
    /// </summary>
    public PlayerProgressData()
    {
        levelStars = new Dictionary<int, int>();
        levelHighScores = new Dictionary<int, int>();
        infiniteStackerHighScore = 0;
        // New data is built from this device's scores, which LeaderboardSeason has already migrated.
        leaderboardSeason = LeaderboardSeason.Current;
        infiniteBestBlocks = 0;
        infiniteBestHeight = 0f;
        lastSyncTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        achievementProgressJson = "";
        sunsetThemeUnlocked = false;
        nightThemeUnlocked = false;
    }

    /// <summary>
    /// Converts this progress data to JSON string for PlayFab storage
    /// </summary>
    public string ToJson()
    {
        // Create a serializable wrapper since Unity's JsonUtility doesn't support Dictionary directly
        var wrapper = new SerializableProgressData
        {
            levelStarsKeys = new List<int>(levelStars.Keys),
            levelStarsValues = new List<int>(levelStars.Values),
            levelHighScoresKeys = new List<int>(levelHighScores.Keys),
            levelHighScoresValues = new List<int>(levelHighScores.Values),
            infiniteStackerHighScore = this.infiniteStackerHighScore,
            leaderboardSeason = this.leaderboardSeason,
            infiniteBestBlocks = this.infiniteBestBlocks,
            infiniteBestHeight = this.infiniteBestHeight,
            lastSyncTimestamp = this.lastSyncTimestamp,
            achievementProgressJson = this.achievementProgressJson,
            sunsetThemeUnlocked = this.sunsetThemeUnlocked,
            nightThemeUnlocked = this.nightThemeUnlocked,
            unlockedPowers = new List<string>(unlockedPowers ?? new List<string>())
        };

        return JsonUtility.ToJson(wrapper);
    }

    /// <summary>
    /// Creates PlayerProgressData from JSON string
    /// </summary>
    public static PlayerProgressData FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new PlayerProgressData();
        }

        try
        {
            var wrapper = JsonUtility.FromJson<SerializableProgressData>(json);
            // Bests from an older season would re-raise the bar the device just cleared, keeping
            // the player off the reset boards. Stars, themes and the ghost line still carry over.
            bool currentSeason = wrapper.leaderboardSeason >= LeaderboardSeason.Current;

            var data = new PlayerProgressData
            {
                infiniteStackerHighScore = currentSeason ? wrapper.infiniteStackerHighScore : 0,
                leaderboardSeason = wrapper.leaderboardSeason,
                infiniteBestBlocks = wrapper.infiniteBestBlocks,
                infiniteBestHeight = wrapper.infiniteBestHeight,
                lastSyncTimestamp = wrapper.lastSyncTimestamp,
                achievementProgressJson = wrapper.achievementProgressJson ?? "",
                sunsetThemeUnlocked = wrapper.sunsetThemeUnlocked,
                nightThemeUnlocked = wrapper.nightThemeUnlocked,
                // Saves from before powers existed have no list.
                unlockedPowers = wrapper.unlockedPowers ?? new List<string>()
            };

            // Reconstruct dictionaries
            for (int i = 0; i < wrapper.levelStarsKeys.Count; i++)
            {
                data.levelStars[wrapper.levelStarsKeys[i]] = wrapper.levelStarsValues[i];
            }

            for (int i = 0; currentSeason && i < wrapper.levelHighScoresKeys.Count; i++)
            {
                data.levelHighScores[wrapper.levelHighScoresKeys[i]] = wrapper.levelHighScoresValues[i];
            }

            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to deserialize PlayerProgressData: {e.Message}");
            return new PlayerProgressData();
        }
    }

    /// <summary>
    /// Folds another account's progress into this one, keeping the best of each (used when a
    /// Device ID account is merged into a Google account). Achievement progress is only taken
    /// when this side has none.
    /// </summary>
    public void MergeFrom(PlayerProgressData other)
    {
        if (other == null) return;

        foreach (var kvp in other.levelStars)
        {
            if (!levelStars.TryGetValue(kvp.Key, out int stars) || kvp.Value > stars)
            {
                levelStars[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in other.levelHighScores)
        {
            if (!levelHighScores.TryGetValue(kvp.Key, out int score) || kvp.Value > score)
            {
                levelHighScores[kvp.Key] = kvp.Value;
            }
        }

        infiniteStackerHighScore = Math.Max(infiniteStackerHighScore, other.infiniteStackerHighScore);
        leaderboardSeason = Math.Max(leaderboardSeason, other.leaderboardSeason);

        if (other.infiniteBestHeight > infiniteBestHeight)
        {
            infiniteBestHeight = other.infiniteBestHeight;
            infiniteBestBlocks = other.infiniteBestBlocks;
        }

        if (string.IsNullOrEmpty(achievementProgressJson))
        {
            achievementProgressJson = other.achievementProgressJson;
        }

        sunsetThemeUnlocked |= other.sunsetThemeUnlocked;
        nightThemeUnlocked |= other.nightThemeUnlocked;

        if (other.unlockedPowers != null)
        {
            unlockedPowers ??= new List<string>();
            foreach (string power in other.unlockedPowers)
            {
                if (!unlockedPowers.Contains(power)) unlockedPowers.Add(power);
            }
        }
    }

    /// <summary>
    /// Updates the sync timestamp to now
    /// </summary>
    public void UpdateSyncTimestamp()
    {
        lastSyncTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Wrapper class for JSON serialization (Unity JsonUtility doesn't support Dictionary)
    /// </summary>
    [Serializable]
    private class SerializableProgressData
    {
        public List<int> levelStarsKeys = new List<int>();
        public List<int> levelStarsValues = new List<int>();
        public List<int> levelHighScoresKeys = new List<int>();
        public List<int> levelHighScoresValues = new List<int>();
        public int infiniteStackerHighScore = 0;
        public int leaderboardSeason = 0;
        public int infiniteBestBlocks = 0;
        public float infiniteBestHeight = 0f;
        public long lastSyncTimestamp = 0;
        public string achievementProgressJson = "";
        public bool sunsetThemeUnlocked = false;
        public bool nightThemeUnlocked = false;
        public List<string> unlockedPowers = new List<string>();
    }
}

