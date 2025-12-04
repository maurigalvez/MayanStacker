using System;
using System.Collections.Generic;
using UnityEngine;

namespace TamalStacker.Achievements
{
    /// <summary>
    /// Represents a queued achievement operation for offline sync
    /// </summary>
    [Serializable]
    public class QueuedAchievementOperation
    {
        /// <summary>
        /// Achievement ID
        /// </summary>
        public string achievementId;
        
        /// <summary>
        /// Google Play Games achievement ID
        /// </summary>
        public string googlePlayId;
        
        /// <summary>
        /// Type of operation: "unlock" or "progress"
        /// </summary>
        public string operationType;
        
        /// <summary>
        /// Progress value (for incremental achievements)
        /// </summary>
        public int progressValue;
        
        /// <summary>
        /// Timestamp when operation was queued
        /// </summary>
        public long timestamp;

        public QueuedAchievementOperation(string achievementId, string googlePlayId, string operationType, int progressValue)
        {
            this.achievementId = achievementId;
            this.googlePlayId = googlePlayId;
            this.operationType = operationType;
            this.progressValue = progressValue;
            this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    /// <summary>
    /// Stores achievement progress and unlock state
    /// This class is modular and can be reused in different games
    /// </summary>
    [Serializable]
    public class AchievementProgressData
    {
        /// <summary>
        /// Dictionary mapping achievement ID to current progress value
        /// </summary>
        [SerializeField]
        public Dictionary<string, int> progress = new Dictionary<string, int>();
        
        /// <summary>
        /// Dictionary mapping achievement ID to unlock status
        /// </summary>
        [SerializeField]
        public Dictionary<string, bool> unlocked = new Dictionary<string, bool>();
        
        /// <summary>
        /// Queue of operations to sync when online
        /// </summary>
        [SerializeField]
        public List<QueuedAchievementOperation> offlineQueue = new List<QueuedAchievementOperation>();
        
        /// <summary>
        /// Timestamp of last sync with Google Play Games
        /// </summary>
        public long lastSyncTimestamp = 0;

        /// <summary>
        /// Creates a new empty AchievementProgressData
        /// </summary>
        public AchievementProgressData()
        {
            progress = new Dictionary<string, int>();
            unlocked = new Dictionary<string, bool>();
            offlineQueue = new List<QueuedAchievementOperation>();
            lastSyncTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Get progress for an achievement (0 if not found)
        /// </summary>
        public int GetProgress(string achievementId)
        {
            return progress.ContainsKey(achievementId) ? progress[achievementId] : 0;
        }

        /// <summary>
        /// Set progress for an achievement
        /// </summary>
        public void SetProgress(string achievementId, int value)
        {
            progress[achievementId] = value;
        }

        /// <summary>
        /// Check if an achievement is unlocked
        /// </summary>
        public bool IsUnlocked(string achievementId)
        {
            return unlocked.ContainsKey(achievementId) && unlocked[achievementId];
        }

        /// <summary>
        /// Mark an achievement as unlocked
        /// </summary>
        public void SetUnlocked(string achievementId, bool isUnlocked)
        {
            unlocked[achievementId] = isUnlocked;
        }

        /// <summary>
        /// Add an operation to the offline queue
        /// </summary>
        public void QueueOperation(string achievementId, string googlePlayId, string operationType, int progressValue)
        {
            offlineQueue.Add(new QueuedAchievementOperation(achievementId, googlePlayId, operationType, progressValue));
        }

        /// <summary>
        /// Clear the offline queue
        /// </summary>
        public void ClearQueue()
        {
            offlineQueue.Clear();
        }

        /// <summary>
        /// Update the sync timestamp
        /// </summary>
        public void UpdateSyncTimestamp()
        {
            lastSyncTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Converts this progress data to JSON string for storage
        /// </summary>
        public string ToJson()
        {
            var wrapper = new SerializableAchievementProgressData
            {
                progressKeys = new List<string>(progress.Keys),
                progressValues = new List<int>(progress.Values),
                unlockedKeys = new List<string>(unlocked.Keys),
                unlockedValues = new List<bool>(unlocked.Values),
                offlineQueue = this.offlineQueue,
                lastSyncTimestamp = this.lastSyncTimestamp
            };

            return JsonUtility.ToJson(wrapper);
        }

        /// <summary>
        /// Creates AchievementProgressData from JSON string
        /// </summary>
        public static AchievementProgressData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new AchievementProgressData();
            }

            try
            {
                var wrapper = JsonUtility.FromJson<SerializableAchievementProgressData>(json);
                var data = new AchievementProgressData
                {
                    lastSyncTimestamp = wrapper.lastSyncTimestamp,
                    offlineQueue = wrapper.offlineQueue ?? new List<QueuedAchievementOperation>()
                };

                // Reconstruct dictionaries
                for (int i = 0; i < wrapper.progressKeys.Count; i++)
                {
                    data.progress[wrapper.progressKeys[i]] = wrapper.progressValues[i];
                }

                for (int i = 0; i < wrapper.unlockedKeys.Count; i++)
                {
                    data.unlocked[wrapper.unlockedKeys[i]] = wrapper.unlockedValues[i];
                }

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to deserialize AchievementProgressData: {e.Message}");
                return new AchievementProgressData();
            }
        }

        /// <summary>
        /// Wrapper class for JSON serialization (Unity JsonUtility doesn't support Dictionary)
        /// </summary>
        [Serializable]
        private class SerializableAchievementProgressData
        {
            public List<string> progressKeys = new List<string>();
            public List<int> progressValues = new List<int>();
            public List<string> unlockedKeys = new List<string>();
            public List<bool> unlockedValues = new List<bool>();
            public List<QueuedAchievementOperation> offlineQueue = new List<QueuedAchievementOperation>();
            public long lastSyncTimestamp = 0;
        }
    }
}

