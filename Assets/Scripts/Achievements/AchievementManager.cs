using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TamalStacker.Achievements
{
    /// <summary>
    /// Main achievement manager following DependencyRegistry pattern
    /// Manages achievement loading, tracking, and synchronization with Google Play Games
    /// This class is designed to be modular and reusable across different games
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string achievementConfigPath = "achievements_config";
        [SerializeField] private bool enableDebugLogs = true;

        [Header("Persistence")]
        [SerializeField] private bool saveToPlayerPrefs = true;
        [SerializeField] private bool syncWithCloud = true;

        // Achievement data
        private Dictionary<string, AchievementDefinition> achievements = new Dictionary<string, AchievementDefinition>();
        private AchievementProgressData progressData;

        // References to other managers
        private AchievementTracker tracker;
        private GooglePlayAchievementService googlePlayService;

        // State
        private bool isInitialized = false;

        // Events
        public System.Action<AchievementDefinition, int, int> OnAchievementProgressUpdated; // achievement, current, target
        public System.Action<AchievementDefinition> OnAchievementUnlocked;
        public System.Action OnSyncCompleted;
        public System.Action<string> OnSyncFailed;

        // Properties
        public bool IsInitialized => isInitialized;
        public int TotalAchievements => achievements.Count;
        public int UnlockedAchievements => progressData?.unlocked.Count(kvp => kvp.Value) ?? 0;

        private void Awake()
        {
            if (DependencyRegistry.Find<AchievementManager>() != null)
            {
                Destroy(gameObject);
                return;
            }
            // Register with DependencyRegistry
            DependencyRegistry.Register<AchievementManager>(this);

            // Persist across scenes
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialize();

            // Subscribe to PlayFab sync events for cloud save
            var playFabManager = DependencyRegistry.Find<PlayFabManager>();
            if (playFabManager != null)
            {
                playFabManager.OnProgressSynced += OnProgressSyncedFromCloud;
                
                // If PlayFab is already logged in (returning from game scene), sync achievements
                if (playFabManager.IsLoggedIn)
                {
                    LogDebug("PlayFab already logged in, will sync achievements with Google Play...");
                    StartCoroutine(SyncWithGooglePlayDelayed());
                }
            }
        }
        
        /// <summary>
        /// Sync with Google Play after a delay to ensure SDK is ready
        /// </summary>
        private System.Collections.IEnumerator SyncWithGooglePlayDelayed()
        {
            // Wait 1 second to ensure Google Play Games is ready
            yield return new WaitForSeconds(1f);
            
            LogDebug("Now syncing achievements with Google Play...");
            SyncWithGooglePlay();
        }

        /// <summary>
        /// Initialize the achievement system
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                LogDebug("AchievementManager already initialized");
                return;
            }

            LogDebug("Initializing AchievementManager...");

            // Load achievement definitions from JSON
            LoadAchievementDefinitions();

            // Load progress data from PlayerPrefs
            LoadProgressData();

            // Set initialized flag BEFORE initializing tracker
            // This allows the tracker to update achievement progress during initialization
            isInitialized = true;

            // Initialize Google Play service
            InitializeGooglePlayService();

            // Initialize tracker (will load and update progress)
            InitializeTracker();

            LogDebug($"AchievementManager initialized with {achievements.Count} achievements");
        }

        /// <summary>
        /// Load achievement definitions from JSON configuration
        /// </summary>
        private void LoadAchievementDefinitions()
        {
            TextAsset configAsset = Resources.Load<TextAsset>(achievementConfigPath);
            if (configAsset == null)
            {
                Debug.LogError($"Failed to load achievement config from Resources/{achievementConfigPath}");
                return;
            }

            try
            {
                AchievementConfig config = JsonUtility.FromJson<AchievementConfig>(configAsset.text);
                if (config == null || config.achievements == null)
                {
                    Debug.LogError("Achievement config is null or has no achievements");
                    return;
                }

                achievements.Clear();
                foreach (var achievement in config.achievements)
                {
                    if (achievement.IsValid())
                    {
                        achievements[achievement.id] = achievement;
                    }
                }

                LogDebug($"Loaded {achievements.Count} achievement definitions");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse achievement config: {e.Message}");
            }
        }

        /// <summary>
        /// Load progress data from PlayerPrefs
        /// </summary>
        private void LoadProgressData()
        {
            if (!saveToPlayerPrefs)
            {
                progressData = new AchievementProgressData();
                return;
            }

            string json = PlayerPrefs.GetString("AchievementProgressData", "");
            if (string.IsNullOrEmpty(json))
            {
                progressData = new AchievementProgressData();
                LogDebug("No saved achievement progress found, starting fresh");
            }
            else
            {
                progressData = AchievementProgressData.FromJson(json);
                LogDebug($"Loaded achievement progress: {progressData.unlocked.Count(kvp => kvp.Value)} unlocked");
            }
        }

        /// <summary>
        /// Save progress data to PlayerPrefs
        /// </summary>
        private void SaveProgressData()
        {
            if (!saveToPlayerPrefs || progressData == null) return;

            string json = progressData.ToJson();
            PlayerPrefs.SetString("AchievementProgressData", json);
            PlayerPrefs.Save();
            LogDebug("Achievement progress saved to PlayerPrefs");
        }

        /// <summary>
        /// Initialize Google Play service
        /// </summary>
        private void InitializeGooglePlayService()
        {
            googlePlayService = gameObject.AddComponent<GooglePlayAchievementService>();
            googlePlayService.Initialize(this);
        }

        /// <summary>
        /// Initialize achievement tracker
        /// </summary>
        private void InitializeTracker()
        {
            tracker = gameObject.AddComponent<AchievementTracker>();
            tracker.Initialize(this);
        }

        /// <summary>
        /// Get an achievement definition by ID
        /// </summary>
        public AchievementDefinition GetAchievement(string achievementId)
        {
            return achievements.ContainsKey(achievementId) ? achievements[achievementId] : null;
        }

        /// <summary>
        /// Get all achievement definitions
        /// </summary>
        public List<AchievementDefinition> GetAllAchievements()
        {
            return new List<AchievementDefinition>(achievements.Values);
        }

        /// <summary>
        /// Get achievements by category
        /// </summary>
        public List<AchievementDefinition> GetAchievementsByCategory(string category)
        {
            return achievements.Values.Where(a => a.category == category).ToList();
        }

        /// <summary>
        /// Get current progress for an achievement
        /// </summary>
        public int GetProgress(string achievementId)
        {
            return progressData?.GetProgress(achievementId) ?? 0;
        }

        /// <summary>
        /// Check if an achievement is unlocked
        /// </summary>
        public bool IsUnlocked(string achievementId)
        {
            return progressData?.IsUnlocked(achievementId) ?? false;
        }

        /// <summary>
        /// Update progress for an achievement
        /// </summary>
        public void UpdateProgress(string achievementId, int newProgress)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("AchievementManager not initialized yet");
                return;
            }

            if (!achievements.ContainsKey(achievementId))
            {
                Debug.LogWarning($"Unknown achievement ID: {achievementId}");
                return;
            }

            var achievement = achievements[achievementId];
            int currentProgress = progressData.GetProgress(achievementId);

            // Only update if progress increased
            if (newProgress <= currentProgress)
            {
                return;
            }

            // Update progress
            progressData.SetProgress(achievementId, newProgress);
            SaveProgressData();

            LogDebug($"Progress updated for '{achievement.title}': {newProgress}/{achievement.targetValue}");

            // Notify listeners
            OnAchievementProgressUpdated?.Invoke(achievement, newProgress, achievement.targetValue);

            // Check if achievement should be unlocked
            if (newProgress >= achievement.targetValue && !progressData.IsUnlocked(achievementId))
            {
                UnlockAchievement(achievementId);
            }

            // Report to Google Play Games
            if (achievement.GetAchievementType() == AchievementType.Incremental)
            {
                googlePlayService?.ReportProgress(achievement.googlePlayId, newProgress, achievement.targetValue);
            }
        }

        /// <summary>
        /// Unlock an achievement
        /// </summary>
        public void UnlockAchievement(string achievementId)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("AchievementManager not initialized yet");
                return;
            }

            if (!achievements.ContainsKey(achievementId))
            {
                Debug.LogWarning($"Unknown achievement ID: {achievementId}");
                return;
            }

            // Check if already unlocked
            if (progressData.IsUnlocked(achievementId))
            {
                LogDebug($"Achievement '{achievementId}' already unlocked");
                return;
            }

            var achievement = achievements[achievementId];

            // Mark as unlocked
            progressData.SetUnlocked(achievementId, true);

            // Ensure progress is at target value
            if (progressData.GetProgress(achievementId) < achievement.targetValue)
            {
                progressData.SetProgress(achievementId, achievement.targetValue);
            }

            SaveProgressData();

            Debug.Log($"🏆 Achievement Unlocked: {achievement.title}");

            // Notify listeners
            OnAchievementUnlocked?.Invoke(achievement);

            // Report to Google Play Games
            googlePlayService?.UnlockAchievement(achievement.googlePlayId);
        }

        /// <summary>
        /// Sync with Google Play Games
        /// Processes offline queue and updates local state
        /// </summary>
        public void SyncWithGooglePlay()
        {
            if (googlePlayService == null)
            {
                Debug.LogWarning("Google Play service not initialized");
                return;
            }

            LogDebug("Starting Google Play Games sync...");
            googlePlayService.SyncOfflineQueue(
                onSuccess: () =>
                {
                    LogDebug("Google Play Games sync completed successfully");
                    OnSyncCompleted?.Invoke();
                },
                onFailure: (error) =>
                {
                    Debug.LogWarning($"Google Play Games sync failed: {error}");
                    OnSyncFailed?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Get the progress data for cloud sync
        /// </summary>
        public AchievementProgressData GetProgressData()
        {
            return progressData;
        }

        /// <summary>
        /// Called when progress is synced from cloud (after login)
        /// Updates local progress with cloud data (server authoritative)
        /// </summary>
        private void OnProgressSyncedFromCloud(PlayerProgressData data)
        {
            if (data == null || string.IsNullOrEmpty(data.achievementProgressJson)) return;

            LogDebug("Syncing achievement progress from cloud...");

            try
            {
                var cloudAchievementData = AchievementProgressData.FromJson(data.achievementProgressJson);
                UpdateFromCloud(cloudAchievementData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to sync achievements from cloud: {e.Message}");
            }
        }

        /// <summary>
        /// Update progress from cloud (for cloud save sync)
        /// </summary>
        public void UpdateFromCloud(AchievementProgressData cloudData)
        {
            if (cloudData == null) return;

            LogDebug("Updating achievement progress from cloud...");

            bool hasChanges = false;

            // Merge unlocked achievements (cloud is authoritative)
            foreach (var kvp in cloudData.unlocked)
            {
                if (kvp.Value && !progressData.IsUnlocked(kvp.Key))
                {
                    progressData.SetUnlocked(kvp.Key, true);
                    hasChanges = true;
                    LogDebug($"Unlocked achievement from cloud: {kvp.Key}");
                }
            }

            // Merge progress (take maximum)
            foreach (var kvp in cloudData.progress)
            {
                int localProgress = progressData.GetProgress(kvp.Key);
                if (kvp.Value > localProgress)
                {
                    progressData.SetProgress(kvp.Key, kvp.Value);
                    hasChanges = true;
                    LogDebug($"Updated progress from cloud for {kvp.Key}: {kvp.Value}");
                }
            }

            if (hasChanges)
            {
                SaveProgressData();
                LogDebug("Achievement progress synced from cloud");
            }
        }

        /// <summary>
        /// Reset all achievement progress (for testing)
        /// </summary>
        [ContextMenu("Reset All Achievement Progress")]
        public void ResetAllProgress()
        {
            Debug.LogWarning("Resetting all achievement progress...");
            progressData = new AchievementProgressData();
            SaveProgressData();
            Debug.Log("Achievement progress reset complete!");
        }

        /// <summary>
        /// Debug: Print current achievement progress
        /// </summary>
        [ContextMenu("Debug: Print Achievement Progress")]
        public void DebugPrintProgress()
        {
            Debug.Log("=== ACHIEVEMENT PROGRESS DEBUG ===");
            Debug.Log($"Initialized: {isInitialized}");
            Debug.Log($"Total Achievements: {achievements.Count}");
            Debug.Log($"Unlocked Achievements: {UnlockedAchievements}");

            Debug.Log("\n--- Sites Completed Achievements ---");
            Debug.Log($"first_steps: {GetProgress("first_steps")}/1 - Unlocked: {IsUnlocked("first_steps")}");
            Debug.Log($"path_of_jaguar: {GetProgress("path_of_jaguar")}/5 - Unlocked: {IsUnlocked("path_of_jaguar")}");
            Debug.Log($"maya_wanderer: {GetProgress("maya_wanderer")}/10 - Unlocked: {IsUnlocked("maya_wanderer")}");

            Debug.Log("\n--- Codex Achievements ---");
            Debug.Log($"page_keeper: {GetProgress("page_keeper")}/3 - Unlocked: {IsUnlocked("page_keeper")}");

            Debug.Log("=== END DEBUG ===");
        }

        /// <summary>
        /// Get completion percentage
        /// </summary>
        public float GetCompletionPercentage()
        {
            if (achievements.Count == 0) return 0f;
            int unlocked = progressData.unlocked.Count(kvp => kvp.Value);
            return (float)unlocked / achievements.Count * 100f;
        }

        /// <summary>
        /// Debug logging helper
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AchievementManager] {message}");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from PlayFab events
            var playFabManager = DependencyRegistry.Find<PlayFabManager>();
            if (playFabManager != null)
            {
                playFabManager.OnProgressSynced -= OnProgressSyncedFromCloud;
            }

            // Unregister from DependencyRegistry
            DependencyRegistry.Unregister<AchievementManager>(this);
        }
    }
}

