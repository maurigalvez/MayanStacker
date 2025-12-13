using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a queue of scores that need to be synced to the cloud when network becomes available
/// Scores are stored in PlayerPrefs to persist across app sessions
/// </summary>
public static class OfflineScoreQueue
{
    private const string QUEUE_KEY = "OfflineScoreQueue";

    /// <summary>
    /// Represents a queued score entry
    /// </summary>
    [Serializable]
    public class QueuedScore
    {
        public string leaderboardName;
        public int score;
    }

    /// <summary>
    /// Wrapper class for JSON serialization
    /// </summary>
    [Serializable]
    private class ScoreQueueData
    {
        public List<QueuedScore> scores = new List<QueuedScore>();
    }

    /// <summary>
    /// Queue a score for syncing when network becomes available
    /// Prevents duplicate entries (same leaderboard + score)
    /// </summary>
    /// <param name="leaderboardName">Name of the leaderboard</param>
    /// <param name="score">Score value to sync</param>
    public static void QueueScore(string leaderboardName, int score)
    {
        if (string.IsNullOrEmpty(leaderboardName))
        {
            Debug.LogWarning("OfflineScoreQueue: Cannot queue score with empty leaderboard name");
            return;
        }

        var queue = GetQueuedScores();

        // Check for duplicate (same leaderboard + score)
        bool isDuplicate = queue.Exists(q => q.leaderboardName == leaderboardName && q.score == score);
        if (isDuplicate)
        {
            Debug.Log($"OfflineScoreQueue: Score {score} for {leaderboardName} already queued, skipping duplicate");
            return;
        }

        // Add new score to queue
        queue.Add(new QueuedScore { leaderboardName = leaderboardName, score = score });
        SaveQueue(queue);

        Debug.Log($"OfflineScoreQueue: Queued score {score} for {leaderboardName} (Total queued: {queue.Count})");
    }

    /// <summary>
    /// Get all queued scores
    /// </summary>
    /// <returns>List of queued scores</returns>
    public static List<QueuedScore> GetQueuedScores()
    {
        string json = PlayerPrefs.GetString(QUEUE_KEY, "");
        
        if (string.IsNullOrEmpty(json))
        {
            return new List<QueuedScore>();
        }

        try
        {
            ScoreQueueData data = JsonUtility.FromJson<ScoreQueueData>(json);
            return data?.scores ?? new List<QueuedScore>();
        }
        catch (Exception e)
        {
            Debug.LogError($"OfflineScoreQueue: Failed to deserialize queue data: {e.Message}");
            return new List<QueuedScore>();
        }
    }

    /// <summary>
    /// Remove a specific score from the queue (after successful sync)
    /// </summary>
    /// <param name="leaderboardName">Name of the leaderboard</param>
    /// <param name="score">Score value to remove</param>
    public static void ClearQueuedScore(string leaderboardName, int score)
    {
        var queue = GetQueuedScores();
        int removed = queue.RemoveAll(q => q.leaderboardName == leaderboardName && q.score == score);
        
        if (removed > 0)
        {
            SaveQueue(queue);
            Debug.Log($"OfflineScoreQueue: Removed score {score} for {leaderboardName} from queue (Remaining: {queue.Count})");
        }
    }

    /// <summary>
    /// Clear all queued scores
    /// </summary>
    public static void ClearAll()
    {
        PlayerPrefs.DeleteKey(QUEUE_KEY);
        PlayerPrefs.Save();
        Debug.Log("OfflineScoreQueue: Cleared all queued scores");
    }

    /// <summary>
    /// Get the number of queued scores
    /// </summary>
    /// <returns>Number of scores in the queue</returns>
    public static int GetQueueCount()
    {
        return GetQueuedScores().Count;
    }

    /// <summary>
    /// Check if there are any queued scores
    /// </summary>
    /// <returns>True if queue has scores, false otherwise</returns>
    public static bool HasQueuedScores()
    {
        return GetQueueCount() > 0;
    }

    /// <summary>
    /// Save the queue to PlayerPrefs
    /// </summary>
    private static void SaveQueue(List<QueuedScore> queue)
    {
        try
        {
            ScoreQueueData data = new ScoreQueueData { scores = queue };
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(QUEUE_KEY, json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"OfflineScoreQueue: Failed to save queue data: {e.Message}");
        }
    }
}

