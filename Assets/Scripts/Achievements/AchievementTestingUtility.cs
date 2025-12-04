using UnityEngine;

namespace TamalStacker.Achievements
{
    /// <summary>
    /// Editor testing utility for achievements
    /// Provides easy access to test achievement functionality without Google Play authentication
    /// Only active in Unity Editor
    /// </summary>
    public class AchievementTestingUtility : MonoBehaviour
    {
        [Header("Quick Test Actions")]
        [Tooltip("Achievement ID to test with")]
        public string testAchievementId = "first_stack";

        [Tooltip("Progress value to set for testing")]
        public int testProgressValue = 1;

        private AchievementManager achievementManager;

#if UNITY_EDITOR
        private void Start()
        {
            achievementManager = DependencyRegistry.Find<AchievementManager>();
            
            if (achievementManager == null)
            {
                Debug.LogWarning("AchievementTestingUtility: AchievementManager not found!");
            }
            else
            {
                Debug.Log("✅ AchievementTestingUtility ready! Use the Context Menu (right-click component) to test achievements.");
            }
        }

        /// <summary>
        /// Update progress for the test achievement
        /// </summary>
        [ContextMenu("1. Update Test Achievement Progress")]
        private void TestUpdateProgress()
        {
            if (achievementManager == null)
            {
                Debug.LogError("AchievementManager not found!");
                return;
            }

            if (string.IsNullOrEmpty(testAchievementId))
            {
                Debug.LogError("Test achievement ID is empty!");
                return;
            }

            var achievement = achievementManager.GetAchievement(testAchievementId);
            if (achievement == null)
            {
                Debug.LogError($"Achievement not found: {testAchievementId}");
                Debug.Log("Available achievement IDs:");
                var all = achievementManager.GetAllAchievements();
                foreach (var a in all)
                {
                    Debug.Log($"  - {a.id}");
                }
                return;
            }

            Debug.Log($"📈 Testing progress update: {achievement.title}");
            Debug.Log($"   Current: {achievementManager.GetProgress(testAchievementId)}/{achievement.targetValue}");
            Debug.Log($"   Setting to: {testProgressValue}/{achievement.targetValue}");
            
            achievementManager.UpdateProgress(testAchievementId, testProgressValue);
            
            Debug.Log($"   New: {achievementManager.GetProgress(testAchievementId)}/{achievement.targetValue}");
        }

        /// <summary>
        /// Unlock the test achievement immediately
        /// </summary>
        [ContextMenu("2. Unlock Test Achievement")]
        private void TestUnlockAchievement()
        {
            if (achievementManager == null)
            {
                Debug.LogError("AchievementManager not found!");
                return;
            }

            if (string.IsNullOrEmpty(testAchievementId))
            {
                Debug.LogError("Test achievement ID is empty!");
                return;
            }

            var achievement = achievementManager.GetAchievement(testAchievementId);
            if (achievement == null)
            {
                Debug.LogError($"Achievement not found: {testAchievementId}");
                return;
            }

            Debug.Log($"🏆 Testing unlock: {achievement.title}");
            achievementManager.UnlockAchievement(testAchievementId);
        }

        /// <summary>
        /// Show all achievements and their current status
        /// </summary>
        [ContextMenu("3. List All Achievements")]
        private void ListAllAchievements()
        {
            if (achievementManager == null)
            {
                Debug.LogError("AchievementManager not found!");
                return;
            }

            var achievements = achievementManager.GetAllAchievements();
            Debug.Log($"📋 Total Achievements: {achievements.Count}");
            Debug.Log($"🏆 Unlocked: {achievementManager.UnlockedAchievements}/{achievements.Count} ({achievementManager.GetCompletionPercentage():F1}%)");
            Debug.Log("─────────────────────────────────────────");

            foreach (var achievement in achievements)
            {
                int progress = achievementManager.GetProgress(achievement.id);
                bool unlocked = achievementManager.IsUnlocked(achievement.id);
                string status = unlocked ? "🏆 UNLOCKED" : $"⏳ {progress}/{achievement.targetValue}";
                Debug.Log($"{status} | [{achievement.category}] {achievement.title} (ID: {achievement.id})");
            }
        }

        /// <summary>
        /// Reset all achievement progress (for testing)
        /// </summary>
        [ContextMenu("4. Reset All Achievement Progress")]
        private void ResetAllProgress()
        {
            if (achievementManager == null)
            {
                Debug.LogError("AchievementManager not found!");
                return;
            }

            Debug.LogWarning("🔄 Resetting ALL achievement progress...");
            achievementManager.ResetAllProgress();
            Debug.Log("✅ All achievement progress has been reset!");
        }

        /// <summary>
        /// Test unlocking first 3 achievements
        /// </summary>
        [ContextMenu("5. Quick Test - Unlock First 3 Achievements")]
        private void QuickTestUnlockFirst3()
        {
            if (achievementManager == null)
            {
                Debug.LogError("AchievementManager not found!");
                return;
            }

            var achievements = achievementManager.GetAllAchievements();
            int count = Mathf.Min(3, achievements.Count);
            
            Debug.Log($"🧪 Quick Test: Unlocking first {count} achievements...");
            
            for (int i = 0; i < count; i++)
            {
                var achievement = achievements[i];
                Debug.Log($"   Unlocking: {achievement.title}");
                achievementManager.UnlockAchievement(achievement.id);
            }
            
            Debug.Log("✅ Quick test complete!");
        }

        /// <summary>
        /// Test incremental progress on first incremental achievement
        /// </summary>
        [ContextMenu("6. Quick Test - Incremental Progress")]
        private void QuickTestIncrementalProgress()
        {
            if (achievementManager == null)
            {
                Debug.LogError("AchievementManager not found!");
                return;
            }

            var achievements = achievementManager.GetAllAchievements();
            AchievementDefinition incrementalAchievement = null;

            // Find first incremental achievement
            foreach (var achievement in achievements)
            {
                if (achievement.GetAchievementType() == AchievementType.Incremental)
                {
                    incrementalAchievement = achievement;
                    break;
                }
            }

            if (incrementalAchievement == null)
            {
                Debug.LogWarning("No incremental achievements found!");
                return;
            }

            Debug.Log($"🧪 Testing incremental progress: {incrementalAchievement.title}");
            int currentProgress = achievementManager.GetProgress(incrementalAchievement.id);
            int newProgress = currentProgress + 1;
            
            Debug.Log($"   Current: {currentProgress}/{incrementalAchievement.targetValue}");
            Debug.Log($"   Incrementing to: {newProgress}/{incrementalAchievement.targetValue}");
            
            achievementManager.UpdateProgress(incrementalAchievement.id, newProgress);
            
            if (newProgress >= incrementalAchievement.targetValue)
            {
                Debug.Log("   🏆 Achievement should now be unlocked!");
            }
        }

        /// <summary>
        /// Show achievement notification test (if notification UI exists)
        /// </summary>
        [ContextMenu("7. Test Achievement Notification UI")]
        private void TestNotificationUI()
        {
            if (achievementManager == null)
            {
                Debug.LogError("AchievementManager not found!");
                return;
            }

            if (string.IsNullOrEmpty(testAchievementId))
            {
                Debug.LogError("Test achievement ID is empty!");
                return;
            }

            var achievement = achievementManager.GetAchievement(testAchievementId);
            if (achievement == null)
            {
                Debug.LogError($"Achievement not found: {testAchievementId}");
                return;
            }

            // Check if already unlocked
            if (achievementManager.IsUnlocked(testAchievementId))
            {
                Debug.LogWarning($"Achievement '{achievement.title}' is already unlocked!");
                Debug.Log("Tip: Reset achievement progress first, or choose a different achievement ID");
                return;
            }

            Debug.Log($"📢 Testing notification for: {achievement.title}");
            Debug.Log("   Unlocking achievement - notification should appear if AchievementNotificationUI is active in scene");
            
            achievementManager.UnlockAchievement(testAchievementId);
        }

        /// <summary>
        /// Show categories
        /// </summary>
        [ContextMenu("8. List Achievement Categories")]
        private void ListCategories()
        {
            if (achievementManager == null)
            {
                Debug.LogError("AchievementManager not found!");
                return;
            }

            var achievements = achievementManager.GetAllAchievements();
            System.Collections.Generic.HashSet<string> categories = new System.Collections.Generic.HashSet<string>();
            
            foreach (var achievement in achievements)
            {
                categories.Add(achievement.category);
            }

            Debug.Log($"📁 Achievement Categories ({categories.Count}):");
            foreach (var category in categories)
            {
                var categoryAchievements = achievementManager.GetAchievementsByCategory(category);
                int unlocked = 0;
                foreach (var a in categoryAchievements)
                {
                    if (achievementManager.IsUnlocked(a.id))
                    {
                        unlocked++;
                    }
                }
                Debug.Log($"   {category}: {unlocked}/{categoryAchievements.Count} unlocked");
            }
        }
#else
        private void Awake()
        {
            // Automatically remove this component in builds
            Debug.Log("AchievementTestingUtility is only for Editor - removing from build");
            Destroy(this);
        }
#endif
    }
}

