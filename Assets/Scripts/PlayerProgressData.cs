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
    /// Creates a new empty PlayerProgressData
    /// </summary>
    public PlayerProgressData()
    {
        levelStars = new Dictionary<int, int>();
        levelHighScores = new Dictionary<int, int>();
        infiniteStackerHighScore = 0;
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
            lastSyncTimestamp = this.lastSyncTimestamp,
            achievementProgressJson = this.achievementProgressJson,
            sunsetThemeUnlocked = this.sunsetThemeUnlocked,
            nightThemeUnlocked = this.nightThemeUnlocked
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
            var data = new PlayerProgressData
            {
                infiniteStackerHighScore = wrapper.infiniteStackerHighScore,
                lastSyncTimestamp = wrapper.lastSyncTimestamp,
                achievementProgressJson = wrapper.achievementProgressJson ?? "",
                sunsetThemeUnlocked = wrapper.sunsetThemeUnlocked,
                nightThemeUnlocked = wrapper.nightThemeUnlocked
            };

            // Reconstruct dictionaries
            for (int i = 0; i < wrapper.levelStarsKeys.Count; i++)
            {
                data.levelStars[wrapper.levelStarsKeys[i]] = wrapper.levelStarsValues[i];
            }

            for (int i = 0; i < wrapper.levelHighScoresKeys.Count; i++)
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
        public long lastSyncTimestamp = 0;
        public string achievementProgressJson = "";
        public bool sunsetThemeUnlocked = false;
        public bool nightThemeUnlocked = false;
    }
}

