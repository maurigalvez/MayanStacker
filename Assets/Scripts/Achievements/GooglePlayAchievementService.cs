using System.Collections.Generic;
using UnityEngine;

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace TamalStacker.Achievements
{
    /// <summary>
    /// Service layer for Google Play Games achievement operations
    /// Handles online/offline sync and queue management
    /// This class is modular and can be reused across different games
    /// </summary>
    public class GooglePlayAchievementService : MonoBehaviour
    {
        private AchievementManager achievementManager;
        private bool isAuthenticated = false;
        private bool enableDebugLogs = true;
        private bool editorTestingMode = false; // Set to true in Editor to skip Google Play services

        /// <summary>
        /// Initialize the service
        /// </summary>
        public void Initialize(AchievementManager manager)
        {
            achievementManager = manager;

#if UNITY_EDITOR
            // Enable editor testing mode in Unity Editor
            editorTestingMode = true;
            LogDebug("GooglePlayAchievementService initialized in EDITOR TESTING MODE (Google Play services disabled)");
#else
            // Check authentication status on device
            CheckAuthentication();
            LogDebug("GooglePlayAchievementService initialized");
#endif
        }

        /// <summary>
        /// Check if Google Play Games is authenticated
        /// </summary>
        private void CheckAuthentication()
        {
#if UNITY_ANDROID
            if (PlayGamesPlatform.Instance != null)
            {
                isAuthenticated = PlayGamesPlatform.Instance.IsAuthenticated();
                LogDebug($"Google Play Games authentication status: {isAuthenticated}");
            }
            else
            {
                LogDebug("Google Play Games platform not initialized");
            }
#else
            LogDebug("Google Play Games only available on Android");
#endif
        }

        /// <summary>
        /// Unlock an achievement on Google Play Games
        /// Queues operation if offline
        /// </summary>
        public void UnlockAchievement(string googlePlayId)
        {
            if (string.IsNullOrEmpty(googlePlayId))
            {
                Debug.LogWarning("Cannot unlock achievement: Google Play ID is empty");
                return;
            }

            // Skip Google Play services in editor testing mode
            if (editorTestingMode)
            {
                LogDebug($"[EDITOR TEST MODE] Would unlock achievement on Google Play: {googlePlayId}");
                return;
            }

#if UNITY_ANDROID
            CheckAuthentication();

            if (isAuthenticated)
            {
                // Submit immediately
                LogDebug($"Unlocking achievement on Google Play: {googlePlayId}");
                Social.ReportProgress(googlePlayId, 100.0, success =>
                {
                    if (success)
                    {
                        LogDebug($"Successfully unlocked achievement: {googlePlayId}");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to unlock achievement: {googlePlayId}");
                        // Queue for retry
                        QueueOperation(googlePlayId, "unlock", 100);
                    }
                });
            }
            else
            {
                // Queue for later
                LogDebug($"Not authenticated, queueing achievement unlock: {googlePlayId}");
                QueueOperation(googlePlayId, "unlock", 100);
            }
#else
            LogDebug($"Google Play Games not available on this platform, queueing: {googlePlayId}");
            QueueOperation(googlePlayId, "unlock", 100);
#endif
        }

        /// <summary>
        /// Report progress for an incremental achievement
        /// </summary>
        public void ReportProgress(string googlePlayId, int currentProgress, int targetValue)
        {
            if (string.IsNullOrEmpty(googlePlayId))
            {
                Debug.LogWarning("Cannot report progress: Google Play ID is empty");
                return;
            }

            // Skip Google Play services in editor testing mode
            if (editorTestingMode)
            {
                double percentage = (double)currentProgress / targetValue * 100.0;
                percentage = System.Math.Min(100.0, percentage);
                LogDebug($"[EDITOR TEST MODE] Would report progress for {googlePlayId}: {percentage:F1}% ({currentProgress}/{targetValue})");
                return;
            }

#if UNITY_ANDROID
            CheckAuthentication();

            if (isAuthenticated)
            {
                // Calculate percentage
                double percentage = (double)currentProgress / targetValue * 100.0;
                percentage = System.Math.Min(100.0, percentage);

                LogDebug($"Reporting progress for {googlePlayId}: {percentage:F1}%");
                Social.ReportProgress(googlePlayId, percentage, success =>
                {
                    if (success)
                    {
                        LogDebug($"Successfully reported progress: {googlePlayId}");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to report progress: {googlePlayId}");
                        // Queue for retry
                        QueueOperation(googlePlayId, "progress", currentProgress);
                    }
                });
            }
            else
            {
                // Queue for later
                LogDebug($"Not authenticated, queueing progress report: {googlePlayId}");
                QueueOperation(googlePlayId, "progress", currentProgress);
            }
#else
            LogDebug($"Google Play Games not available on this platform, queueing: {googlePlayId}");
            QueueOperation(googlePlayId, "progress", currentProgress);
#endif
        }

        /// <summary>
        /// Queue an operation for offline sync
        /// </summary>
        private void QueueOperation(string googlePlayId, string operationType, int progressValue)
        {
            var progressData = achievementManager.GetProgressData();
            if (progressData == null) return;

            // Find achievement by Google Play ID
            var achievements = achievementManager.GetAllAchievements();
            string achievementId = "";
            foreach (var achievement in achievements)
            {
                if (achievement.googlePlayId == googlePlayId)
                {
                    achievementId = achievement.id;
                    break;
                }
            }

            if (string.IsNullOrEmpty(achievementId))
            {
                Debug.LogWarning($"Cannot queue operation: Unknown Google Play ID {googlePlayId}");
                return;
            }

            progressData.QueueOperation(achievementId, googlePlayId, operationType, progressValue);
            LogDebug($"Queued {operationType} operation for {achievementId}");
        }

        /// <summary>
        /// Sync offline queue with Google Play Games
        /// </summary>
        public void SyncOfflineQueue(System.Action onSuccess, System.Action<string> onFailure)
        {
            // Skip in editor testing mode
            if (editorTestingMode)
            {
                LogDebug("[EDITOR TEST MODE] Skipping offline queue sync (not needed in Editor)");
                onSuccess?.Invoke();
                return;
            }

#if UNITY_ANDROID
            CheckAuthentication();

            if (!isAuthenticated)
            {
                string error = "Not authenticated with Google Play Games";
                LogDebug(error);
                onFailure?.Invoke(error);
                return;
            }

            var progressData = achievementManager.GetProgressData();
            if (progressData == null || progressData.offlineQueue.Count == 0)
            {
                LogDebug("No queued operations to sync");
                onSuccess?.Invoke();
                return;
            }

            LogDebug($"Syncing {progressData.offlineQueue.Count} queued operations...");
            
            // First, load achievements from Google Play to ensure SDK has them
            LogDebug("Loading achievements from Google Play Games before syncing...");
            PlayGamesPlatform.Instance.LoadAchievements(achievements =>
            {
                if (achievements == null)
                {
                    string error = "Failed to load achievements from Google Play Games";
                    Debug.LogWarning(error);
                    onFailure?.Invoke(error);
                    return;
                }
                
                LogDebug($"Successfully loaded {achievements.Length} achievements from Google Play");
                
                // Wait a moment for SDK to fully cache the achievements internally
                // LoadAchievements returns the list, but SDK needs time to update its internal cache
                StartCoroutine(WaitThenProcessQueue(progressData, onSuccess, onFailure));
            });
#else
            string error = "Google Play Games only available on Android";
            LogDebug(error);
            onFailure?.Invoke(error);
#endif
        }
        
        /// <summary>
        /// Wait for SDK cache to update, then process sync queue
        /// </summary>
        private System.Collections.IEnumerator WaitThenProcessQueue(AchievementProgressData progressData, System.Action onSuccess, System.Action<string> onFailure)
        {
            // Wait 0.75 seconds for SDK to fully update its internal cache
            // This is critical! LoadAchievements() returns the list, but the SDK's internal
            // cache (used by ReportProgress) needs time to update
            LogDebug("Waiting for SDK to update internal cache...");
            yield return new UnityEngine.WaitForSeconds(0.75f);
            
            LogDebug("SDK cache ready - processing sync queue now");
            ProcessSyncQueue(progressData, onSuccess, onFailure);
        }
        
        /// <summary>
        /// Process the sync queue after achievements are loaded
        /// </summary>
        private void ProcessSyncQueue(AchievementProgressData progressData, System.Action onSuccess, System.Action<string> onFailure)
        {
#if UNITY_ANDROID
            // Process queue
            var queue = new List<QueuedAchievementOperation>(progressData.offlineQueue);
            int successCount = 0;
            int failCount = 0;
            int totalOps = queue.Count;

            foreach (var op in queue)
            {
                if (op.operationType == "unlock")
                {
                    Social.ReportProgress(op.googlePlayId, 100.0, success =>
                    {
                        if (success)
                        {
                            successCount++;
                            LogDebug($"Synced unlock for {op.achievementId}");
                        }
                        else
                        {
                            failCount++;
                            Debug.LogWarning($"Failed to sync unlock for {op.achievementId}");
                        }

                        // Check if all operations completed
                        if (successCount + failCount >= totalOps)
                        {
                            CompleteSync(successCount, failCount, onSuccess, onFailure);
                        }
                    });
                }
                else if (op.operationType == "progress")
                {
                    // For progress, we need to get the achievement definition to calculate percentage
                    var achievement = achievementManager.GetAchievement(op.achievementId);
                    if (achievement != null)
                    {
                        double percentage = (double)op.progressValue / achievement.targetValue * 100.0;
                        percentage = System.Math.Min(100.0, percentage);

                        Social.ReportProgress(op.googlePlayId, percentage, success =>
                        {
                            if (success)
                            {
                                successCount++;
                                LogDebug($"Synced progress for {op.achievementId}: {percentage:F1}%");
                            }
                            else
                            {
                                failCount++;
                                Debug.LogWarning($"Failed to sync progress for {op.achievementId}");
                            }

                            // Check if all operations completed
                            if (successCount + failCount >= totalOps)
                            {
                                CompleteSync(successCount, failCount, onSuccess, onFailure);
                            }
                        });
                    }
                    else
                    {
                        failCount++;
                        Debug.LogWarning($"Unknown achievement: {op.achievementId}");

                        if (successCount + failCount >= totalOps)
                        {
                            CompleteSync(successCount, failCount, onSuccess, onFailure);
                        }
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Complete the sync process
        /// </summary>
        private void CompleteSync(int successCount, int failCount, System.Action onSuccess, System.Action<string> onFailure)
        {
            var progressData = achievementManager.GetProgressData();

            if (failCount == 0)
            {
                // All operations succeeded, clear queue
                progressData.ClearQueue();
                progressData.UpdateSyncTimestamp();
                LogDebug($"Sync completed successfully: {successCount} operations");
                onSuccess?.Invoke();
            }
            else
            {
                // Some operations failed
                string error = $"Sync completed with {failCount} failures out of {successCount + failCount} operations";
                Debug.LogWarning(error);

                // Remove successful operations from queue (more complex implementation would track this)
                // For simplicity, we'll keep failed operations in queue
                onFailure?.Invoke(error);
            }
        }

        /// <summary>
        /// Show Google Play Games achievements UI
        /// </summary>
        public void ShowAchievementsUI()
        {
            // Skip in editor testing mode
            if (editorTestingMode)
            {
                Debug.LogWarning("[EDITOR TEST MODE] Cannot show Google Play achievements UI in Editor. Use the in-game Achievement Panel instead.");
                return;
            }

#if UNITY_ANDROID
            CheckAuthentication();

            if (isAuthenticated)
            {
                PlayGamesPlatform.Instance.ShowAchievementsUI();
            }
            else
            {
                Debug.LogWarning("Cannot show achievements UI: Not authenticated with Google Play Games");
            }
#else
            Debug.LogWarning("Google Play Games achievements UI only available on Android");
#endif
        }

        /// <summary>
        /// Load achievement state from Google Play Games
        /// </summary>
        public void LoadAchievementState(System.Action<List<string>> onSuccess, System.Action<string> onFailure)
        {
            // Skip in editor testing mode
            if (editorTestingMode)
            {
                LogDebug("[EDITOR TEST MODE] Skipping achievement state load from Google Play (using local data only)");
                onSuccess?.Invoke(new List<string>());
                return;
            }

#if UNITY_ANDROID
            CheckAuthentication();

            if (!isAuthenticated)
            {
                string error = "Not authenticated with Google Play Games";
                LogDebug(error);
                onFailure?.Invoke(error);
                return;
            }

            PlayGamesPlatform.Instance.LoadAchievements(achievements =>
            {
                if (achievements == null)
                {
                    string error = "Failed to load achievements from Google Play Games";
                    Debug.LogWarning(error);
                    onFailure?.Invoke(error);
                    return;
                }

                // Extract unlocked achievement IDs
                List<string> unlockedIds = new List<string>();
                foreach (var achievement in achievements)
                {
                    if (achievement.completed)
                    {
                        unlockedIds.Add(achievement.id);
                    }
                }

                LogDebug($"Loaded {achievements.Length} achievements from Google Play, {unlockedIds.Count} unlocked");
                onSuccess?.Invoke(unlockedIds);
            });
#else
            string error = "Google Play Games only available on Android";
            LogDebug(error);
            onFailure?.Invoke(error);
#endif
        }

        /// <summary>
        /// Debug logging helper
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[GooglePlayAchievementService] {message}");
            }
        }
    }
}

