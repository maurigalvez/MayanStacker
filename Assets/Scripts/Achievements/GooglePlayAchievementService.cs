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
        private bool isSyncing = false; // Guard flag to prevent concurrent syncs

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
        /// Report progress for an achievement
        /// NOTE: Google Play Console has all achievements as Standard (non-incremental)
        /// Standard achievements only accept 0% or 100%, so we only report when at 100%
        /// </summary>
        public void ReportProgress(string googlePlayId, int currentProgress, int targetValue)
        {
            if (string.IsNullOrEmpty(googlePlayId))
            {
                Debug.LogWarning("Cannot report progress: Google Play ID is empty");
                return;
            }

            // Calculate percentage
            double percentage = (double)currentProgress / targetValue * 100.0;
            percentage = System.Math.Min(100.0, percentage);

            // Skip Google Play services in editor testing mode
            if (editorTestingMode)
            {
                LogDebug($"[EDITOR TEST MODE] Would report progress for {googlePlayId}: {percentage:F1}% ({currentProgress}/{targetValue})");
                return;
            }

#if UNITY_ANDROID
            // IMPORTANT: Google Play Console has Standard achievements (not Incremental)
            // Standard achievements only accept 100% (unlock) - skip partial progress
            if (percentage < 100.0)
            {
                LogDebug($"Skipping partial progress report for {googlePlayId}: {percentage:F1}% (Standard achievements only)");
                return;
            }

            CheckAuthentication();

            if (isAuthenticated)
            {
                // Achievement is at 100% - unlock it
                LogDebug($"Unlocking achievement via progress report: {googlePlayId} (100%)");
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
                // Queue for later as unlock (not progress)
                LogDebug($"Not authenticated, queueing achievement unlock: {googlePlayId}");
                QueueOperation(googlePlayId, "unlock", 100);
            }
#else
            if (percentage >= 100.0)
            {
                LogDebug($"Google Play Games not available on this platform, queueing unlock: {googlePlayId}");
                QueueOperation(googlePlayId, "unlock", 100);
            }
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
            // Check if already syncing
            if (isSyncing)
            {
                LogDebug("Sync already in progress, skipping duplicate request");
                return;
            }

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

            // Set syncing flag
            isSyncing = true;
            LogDebug($"Syncing {progressData.offlineQueue.Count} queued operations...");

            // First, load achievements from Google Play to ensure SDK has them
            LogDebug("Loading achievements from Google Play Games before syncing...");
            PlayGamesPlatform.Instance.LoadAchievements(achievements =>
            {
                if (achievements == null)
                {
                    string error = "Failed to load achievements from Google Play Games";
                    Debug.LogWarning(error);
                    isSyncing = false; // Clear flag on failure
                    onFailure?.Invoke(error);
                    return;
                }

                LogDebug($"Successfully loaded {achievements.Length} achievements from Google Play");

                // DEBUG: Log all achievement IDs from Google Play to help identify mismatches
                Debug.Log("=== GOOGLE PLAY ACHIEVEMENT IDs (RAW) ===");
                for (int i = 0; i < achievements.Length; i++)
                {
                    var ach = achievements[i];
                    Debug.Log($"Achievement [{i}]: ID='{ach.id}' | Completed={ach.completed} | Hidden={ach.hidden} | Progress={ach.percentCompleted}%");
                }
                Debug.Log($"=== END ({achievements.Length} total) ===");

                // Wait a moment for SDK to fully cache the achievements internally
                // LoadAchievements returns the list, but SDK needs time to update its internal cache
                StartCoroutine(WaitThenProcessQueue(progressData, achievements, onSuccess, onFailure));
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
        private System.Collections.IEnumerator WaitThenProcessQueue(AchievementProgressData progressData, UnityEngine.SocialPlatforms.IAchievement[] googlePlayAchievements, System.Action onSuccess, System.Action<string> onFailure)
        {
            // Wait 0.75 seconds for SDK to fully update its internal cache
            // This is critical! LoadAchievements() returns the list, but the SDK's internal
            // cache (used by ReportProgress) needs time to update
            LogDebug("Waiting for SDK to update internal cache...");
            yield return new UnityEngine.WaitForSeconds(0.75f);

            LogDebug("SDK cache ready - processing sync queue now");
            ProcessSyncQueue(progressData, googlePlayAchievements, onSuccess, onFailure);
        }

        /// <summary>
        /// Process the sync queue after achievements are loaded
        /// </summary>
        private void ProcessSyncQueue(AchievementProgressData progressData, UnityEngine.SocialPlatforms.IAchievement[] googlePlayAchievements, System.Action onSuccess, System.Action<string> onFailure)
        {
#if UNITY_ANDROID
            // Create a set of valid achievement IDs from Google Play
            var validGooglePlayIds = new System.Collections.Generic.HashSet<string>();
            var googlePlayIdMap = new Dictionary<string, string>(); // Maps name to ID

            Debug.Log("=== BUILDING VALIDATION SET ===");
            foreach (var ach in googlePlayAchievements)
            {
                string achId = ach.id;
                validGooglePlayIds.Add(achId);
                Debug.Log($"  ✓ Added to validation: '{achId}'");

                // Try to cast to GooglePlayGames.BasicApi.Achievement to get Name property
                var gpAch = ach as GooglePlayGames.BasicApi.Achievement;
                if (gpAch != null && !string.IsNullOrEmpty(gpAch.Name))
                {
                    googlePlayIdMap[gpAch.Name.ToLower()] = achId;
                    Debug.Log($"    → Mapped name '{gpAch.Name}' to ID");
                }
            }

            Debug.Log($"=== VALIDATION SET COMPLETE: {validGooglePlayIds.Count} IDs ===");

            // Filter queue - remove any "progress" operations that are < 100%
            // This cleans up old queue entries from before we added the 100% check
            var queue = new List<QueuedAchievementOperation>(progressData.offlineQueue);
            var filteredQueue = new List<QueuedAchievementOperation>();
            int filteredOutCount = 0;

            foreach (var op in queue)
            {
                bool shouldKeep = true;

                if (op.operationType == "progress")
                {
                    // Check if this operation is < 100% and should be filtered out
                    var achievement = achievementManager.GetAchievement(op.achievementId);
                    if (achievement != null)
                    {
                        double percentage = (double)op.progressValue / achievement.targetValue * 100.0;
                        if (percentage < 100.0)
                        {
                            // Filter out partial progress - shouldn't have been queued in the first place
                            shouldKeep = false;
                            filteredOutCount++;
                            LogDebug($"Filtered out invalid queue entry: {op.achievementId} at {percentage:F1}% (only 100% should be queued)");
                        }
                    }
                }

                if (shouldKeep)
                {
                    filteredQueue.Add(op);
                }
            }

            if (filteredOutCount > 0)
            {
                LogDebug($"Filtered out {filteredOutCount} invalid queue entries (progress < 100%)");
                // Update the queue to remove filtered entries
                progressData.offlineQueue.Clear();
                foreach (var op in filteredQueue)
                {
                    progressData.offlineQueue.Add(op);
                }
                // Persist the cleaned queue
                SaveProgressData(progressData);
            }

            // Process the filtered queue
            int successCount = 0;
            int failCount = 0;
            int skippedCount = 0;
            int totalOps = filteredQueue.Count;

            // If no operations to process after filtering, complete immediately
            if (totalOps == 0)
            {
                LogDebug("No valid operations to sync after filtering");
                CompleteSync(0, 0, onSuccess, onFailure);
                return;
            }

            foreach (var op in filteredQueue)
            {
                Debug.Log($"→ Processing sync for '{op.achievementId}' with Google Play ID: '{op.googlePlayId}'");

                // Validate that the Google Play ID exists
                if (!validGooglePlayIds.Contains(op.googlePlayId))
                {
                    Debug.LogError($"❌ ID '{op.googlePlayId}' NOT in validation set!");
                    Debug.Log($"   Validation set has {validGooglePlayIds.Count} IDs. Checking if this ID exists...");

                    // Try to find by title/name match as a fallback
                    var achievement = achievementManager.GetAchievement(op.achievementId);
                    string correctedId = null;

                    if (achievement != null && googlePlayIdMap.TryGetValue(achievement.title.ToLower(), out correctedId))
                    {
                        Debug.LogWarning($"Achievement ID mismatch for '{op.achievementId}'! Config has '{op.googlePlayId}' but corrected to '{correctedId}' (matched by title: '{achievement.title}')");
                        // Use the corrected ID for this sync
                        op.googlePlayId = correctedId;
                    }
                    else
                    {
                        skippedCount++;
                        failCount++;
                        Debug.LogError($"Skipping sync for '{op.achievementId}': Google Play ID '{op.googlePlayId}' not found in Google Play Console!");
                        Debug.LogError($"   Config file has: '{op.googlePlayId}'");
                        Debug.LogError($"   Available IDs: {string.Join(", ", validGooglePlayIds)}");

                        if (successCount + failCount + skippedCount >= totalOps)
                        {
                            CompleteSync(successCount, failCount, onSuccess, onFailure);
                        }
                        continue;
                    }
                }
                else
                {
                    Debug.Log($"✓ ID '{op.googlePlayId}' found in validation set");
                }

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
                        if (successCount + failCount + skippedCount >= totalOps)
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

                        // IMPORTANT: Google Play Console has all achievements as Standard (non-incremental)
                        // Standard achievements ONLY accept 0% or 100% - no partial progress
                        // So we only sync when achievement is at 100% (completed)
                        if (percentage >= 100.0)
                        {
                            // Achievement is complete - unlock it (report 100%)
                            Social.ReportProgress(op.googlePlayId, 100.0, success =>
                            {
                                if (success)
                                {
                                    successCount++;
                                    LogDebug($"Synced unlock for {op.achievementId} (was progress at 100%)");
                                }
                                else
                                {
                                    failCount++;
                                    Debug.LogWarning($"Failed to sync unlock for {op.achievementId}");
                                }

                                // Check if all operations completed
                                if (successCount + failCount + skippedCount >= totalOps)
                                {
                                    CompleteSync(successCount, failCount, onSuccess, onFailure);
                                }
                            });
                        }
                        else
                        {
                            // Skip partial progress - Google Play Console has Standard achievements only
                            // NOTE: This shouldn't happen anymore after queue filtering, but keep as safety check
                            skippedCount++;
                            LogDebug($"Skipping partial progress sync for {op.achievementId}: {percentage:F1}% (Standard achievements only accept 100%)");

                            if (successCount + failCount + skippedCount >= totalOps)
                            {
                                CompleteSync(successCount, failCount, onSuccess, onFailure);
                            }
                        }
                    }
                    else
                    {
                        failCount++;
                        Debug.LogWarning($"Unknown achievement: {op.achievementId}");

                        if (successCount + failCount + skippedCount >= totalOps)
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

            // Clear syncing flag
            isSyncing = false;

            if (failCount == 0)
            {
                // All operations succeeded, clear queue
                progressData.ClearQueue();
                progressData.UpdateSyncTimestamp();
                SaveProgressData(progressData);
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

                LogDebug($"Successfully loaded {achievements.Length} achievements from Google Play");

                // DEBUG: Log all achievement IDs for validation
                LogDebug("=== Google Play Achievement IDs (Validation) ===");
                foreach (var ach in achievements)
                {
                    var gpAch = ach as GooglePlayGames.BasicApi.Achievement;
                    if (gpAch != null)
                    {
                        string status = gpAch.IsUnlocked ? "UNLOCKED" : (gpAch.IsRevealed ? "REVEALED" : "HIDDEN");
                        LogDebug($"  [{status}] {gpAch.Id} | {gpAch.Name}");
                    }
                }
                LogDebug("=== End of Achievement IDs ===");

                // Extract unlocked achievement IDs
                List<string> unlockedIds = new List<string>();
                foreach (var achievement in achievements)
                {
                    if (achievement.completed)
                    {
                        unlockedIds.Add(achievement.id);
                    }
                }

                LogDebug($"Total: {achievements.Length} achievements, {unlockedIds.Count} unlocked");
                onSuccess?.Invoke(unlockedIds);
            });
#else
            string error = "Google Play Games only available on Android";
            LogDebug(error);
            onFailure?.Invoke(error);
#endif
        }

        /// <summary>
        /// Save progress data to PlayerPrefs
        /// Used after filtering or modifying the queue
        /// </summary>
        private void SaveProgressData(AchievementProgressData progressData)
        {
            if (progressData == null) return;

            string json = progressData.ToJson();
            PlayerPrefs.SetString("AchievementProgressData", json);
            PlayerPrefs.Save();
            LogDebug("Achievement progress saved to PlayerPrefs (queue modified)");
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

