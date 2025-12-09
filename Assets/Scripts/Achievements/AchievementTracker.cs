using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TamalStacker.Achievements
{
    /// <summary>
    /// Tracks game events and updates achievement progress
    /// This is the game-specific component that subscribes to game events
    /// For a different game, modify the event subscriptions and tracking logic
    /// </summary>
    public class AchievementTracker : MonoBehaviour
    {
        private AchievementManager achievementManager;

        // References to game managers
        private GameManager gameManager;
        private LevelManager levelManager;
        private StackManager stackManager;
        private LeaderboardManager leaderboardManager;

        // Tracking state
        private Dictionary<string, int> sessionTracking = new Dictionary<string, int>();
        private int sitesCompleted = 0;
        private int codexEntriesUnlocked = 0;
        private int maxConsecutivePerfectHits = 0;
        private int maxInfiniteHeight = 0; // All-time max height
        private int currentSessionHeight = 0; // Current run height (resets on game start)
        private HashSet<string> sessionAchievementsShown = new HashSet<string>(); // Track which achievements shown this session
        private int perfectLevelCompletions = 0;
        private int bestLeaderboardRank = int.MaxValue;
        private bool checkedTimeBasedAchievements = false;

        /// <summary>
        /// Initialize the tracker
        /// </summary>
        public void Initialize(AchievementManager manager)
        {
            achievementManager = manager;

            // Subscribe to scene loaded events to re-find managers after scene changes
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Find game managers via DependencyRegistry
            FindAndSubscribeToManagers();

            // Load existing progress
            LoadTrackingState();

            // Check time-based achievements
            CheckTimeBasedAchievements();

            Debug.Log("[AchievementTracker] Initialized and subscribed to game events");
        }

        /// <summary>
        /// Called when a new scene is loaded
        /// Re-find and re-subscribe to managers that might only exist in certain scenes
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[AchievementTracker] Scene loaded: {scene.name} - Re-checking for managers...");
            FindAndSubscribeToManagers();

            // Reload tracking state when returning to main menu (to update achievement progress)
            if (scene.name.Contains("Main") || scene.name.Contains("Menu"))
            {
                Debug.Log("[AchievementTracker] Reloading tracking state for main menu...");
                LoadTrackingState();
            }
        }

        /// <summary>
        /// Find managers and subscribe to their events
        /// Safe to call multiple times - won't double-subscribe
        /// </summary>
        private void FindAndSubscribeToManagers()
        {
            // Unsubscribe from old references first (in case managers were replaced)
            UnsubscribeFromEvents();

            // Find game managers via DependencyRegistry
            gameManager = DependencyRegistry.Find<GameManager>();
            levelManager = DependencyRegistry.Find<LevelManager>();
            stackManager = DependencyRegistry.Find<StackManager>();
            leaderboardManager = DependencyRegistry.Find<LeaderboardManager>();

            // Subscribe to game events
            SubscribeToEvents();

            // Log what we found (GameManager not found is expected in MainMenu scene)
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool isGameScene = currentScene.Contains("Game") || stackManager != null;

            if (gameManager == null && isGameScene)
            {
                Debug.LogWarning($"[AchievementTracker] GameManager not found in {currentScene} scene - some achievements may not track properly");
            }

            Debug.Log($"[AchievementTracker] Manager status in '{currentScene}' - GameManager: {(gameManager != null ? "✓" : "✗")}, LevelManager: {(levelManager != null ? "✓" : "✗")}, StackManager: {(stackManager != null ? "✓" : "✗")}");
        }

        /// <summary>
        /// Load tracking state from progress data
        /// </summary>
        private void LoadTrackingState()
        {
            // Count sites completed
            if (levelManager != null)
            {
                var levels = levelManager.GetAllLevels();
                sitesCompleted = 0;
                codexEntriesUnlocked = 0;
                perfectLevelCompletions = 0;

                foreach (var level in levels)
                {
                    int stars = levelManager.GetLevelStars(level.levelNumber);
                    if (stars > 0)
                    {
                        sitesCompleted++;
                    }

                    // Count codex entries (same as completed levels for now)
                    if (levelManager.IsCodexUnlocked(level.levelNumber))
                    {
                        codexEntriesUnlocked++;
                    }

                    // Check if level was completed perfectly
                    int perfectDrops = PlayerPrefs.GetInt($"Level_{level.levelNumber}_PerfectDrops", 0);
                    int totalDrops = PlayerPrefs.GetInt($"Level_{level.levelNumber}_TotalDrops", 0);
                    if (totalDrops > 0 && perfectDrops == totalDrops && stars > 0)
                    {
                        perfectLevelCompletions++;
                    }
                }
            }

            // Load infinite stacker max height
            maxInfiniteHeight = PlayerPrefs.GetInt("InfiniteStacker_MaxHeight", 0);

            // Load max consecutive perfect hits
            maxConsecutivePerfectHits = PlayerPrefs.GetInt("MaxConsecutivePerfectHits", 0);

            // Load best leaderboard rank
            bestLeaderboardRank = PlayerPrefs.GetInt("BestLeaderboardRank", int.MaxValue);

            // Update achievement progress based on loaded state
            UpdateAllAchievementProgress();
        }

        /// <summary>
        /// Subscribe to game events
        /// Safe to call multiple times - checks for null before subscribing
        /// </summary>
        private void SubscribeToEvents()
        {
            if (levelManager != null)
            {
                levelManager.OnLevelCompleted += OnLevelCompleted;
                Debug.Log("[AchievementTracker] Subscribed to LevelManager events");
            }

            if (gameManager != null)
            {
                gameManager.OnGameStart += OnGameStart;
                gameManager.OnGameRestart += OnGameRestart;
                gameManager.OnGameOver += OnGameOver;
                gameManager.OnConsecutivePerfectHitsChanged += OnConsecutivePerfectHitsChanged;
                Debug.Log("[AchievementTracker] Subscribed to GameManager events");
            }

            if (stackManager != null)
            {
                stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
                Debug.Log("[AchievementTracker] Subscribed to StackManager events");
            }
        }

        /// <summary>
        /// Unsubscribe from game events
        /// Safe to call multiple times - checks for null before unsubscribing
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (levelManager != null)
            {
                levelManager.OnLevelCompleted -= OnLevelCompleted;
            }

            if (gameManager != null)
            {
                gameManager.OnGameStart -= OnGameStart;
                gameManager.OnGameRestart -= OnGameRestart;
                gameManager.OnGameOver -= OnGameOver;
                gameManager.OnConsecutivePerfectHitsChanged -= OnConsecutivePerfectHitsChanged;
            }

            if (stackManager != null)
            {
                stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
            }
        }

        /// <summary>
        /// Called when a level is completed
        /// </summary>
        private void OnLevelCompleted(int stars, int score, bool showCodexPopup)
        {
            if (stars <= 0 || levelManager == null || gameManager == null) return;

            // Check if this is a new completion
            int levelNumber = levelManager.CurrentLevel?.levelNumber ?? 0;
            bool wasCompletedBefore = levelManager.IsLevelCompletedBefore(levelNumber);

            if (!wasCompletedBefore)
            {
                sitesCompleted++;
                SaveTrackingState();

                // Update sites completed achievements
                UpdateSitesCompletedAchievements();

                // Count codex unlock
                if (showCodexPopup || !levelManager.IsCodexUnlocked(levelNumber))
                {
                    codexEntriesUnlocked++;
                    UpdateCodexAchievements();
                }
            }

            // Track perfect level completion
            // Check if all drops in the level were perfect
            if (!sessionTracking.ContainsKey("levelPerfectDrops"))
            {
                sessionTracking["levelPerfectDrops"] = 0;
            }

            if (!sessionTracking.ContainsKey("levelTotalDrops"))
            {
                sessionTracking["levelTotalDrops"] = 0;
            }

            int perfectDrops = sessionTracking["levelPerfectDrops"];
            int totalDrops = sessionTracking["levelTotalDrops"];

            // Save for this level
            PlayerPrefs.SetInt($"Level_{levelNumber}_PerfectDrops", perfectDrops);
            PlayerPrefs.SetInt($"Level_{levelNumber}_TotalDrops", totalDrops);
            PlayerPrefs.Save();

            if (totalDrops > 0 && perfectDrops == totalDrops)
            {
                Debug.Log($"Level {levelNumber} completed with perfect drops!");
                perfectLevelCompletions++;
                SaveTrackingState();

                // Update perfect level achievements
                UpdatePerfectLevelAchievements(levelNumber);
            }

            // Reset session tracking for next level
            sessionTracking["levelPerfectDrops"] = 0;
            sessionTracking["levelTotalDrops"] = 0;
        }

        /// <summary>
        /// Called when game starts
        /// </summary>
        private void OnGameStart()
        {
            // Reset current session height for infinite stacker
            if (gameManager != null && gameManager.CurrentGameMode == GameMode.InfiniteStacker)
            {
                currentSessionHeight = 0;
                sessionAchievementsShown.Clear(); // Reset achievements shown this session
                Debug.Log("[AchievementTracker] Infinite Stacker started - reset session height and achievements");
            }
        }

        /// <summary>
        /// Called when game restarts
        /// </summary>
        private void OnGameRestart()
        {
            // Reset current session height for infinite stacker
            if (gameManager != null && gameManager.CurrentGameMode == GameMode.InfiniteStacker)
            {
                currentSessionHeight = 0;
                sessionAchievementsShown.Clear(); // Reset achievements shown this session
                Debug.Log("[AchievementTracker] Infinite Stacker restarted - reset session height and achievements");
            }
        }

        /// <summary>
        /// Called when game over occurs
        /// </summary>
        private void OnGameOver()
        {
            if (gameManager == null || gameManager.CurrentGameMode != GameMode.InfiniteStacker)
            {
                return;
            }

            // Track infinite stacker height - update all-time max if we beat it
            if (stackManager != null)
            {
                int currentHeight = stackManager.GetStackCount();
                if (currentHeight > maxInfiniteHeight)
                {
                    maxInfiniteHeight = currentHeight;
                    SaveTrackingState();
                    Debug.Log($"[AchievementTracker] New all-time max height: {maxInfiniteHeight}");
                }
            }
        }

        /// <summary>
        /// Called when an object is added to the stack
        /// </summary>
        private void OnObjectAddedToStack(StackableObject stackableObject)
        {
            // Track drops for perfect level completion
            if (!sessionTracking.ContainsKey("levelTotalDrops"))
            {
                sessionTracking["levelTotalDrops"] = 0;
            }
            sessionTracking["levelTotalDrops"]++;

            // Check if it was a perfect drop (accuracy >= 0.9)
            // We'll approximate this by checking combo increase
            if (gameManager != null && gameManager.CurrentCombo > 0)
            {
                if (!sessionTracking.ContainsKey("levelPerfectDrops"))
                {
                    sessionTracking["levelPerfectDrops"] = 0;
                }
                sessionTracking["levelPerfectDrops"]++;
            }

            // Check infinite stacker achievements during gameplay (not just at game over)
            if (gameManager != null && gameManager.CurrentGameMode == GameMode.InfiniteStacker && stackManager != null)
            {
                // Update current session height
                currentSessionHeight = stackManager.GetStackCount();

                // Update all-time max height if we've reached a new record
                if (currentSessionHeight > maxInfiniteHeight)
                {
                    maxInfiniteHeight = currentSessionHeight;
                    SaveTrackingState();
                    Debug.Log($"[AchievementTracker] New all-time max height: {maxInfiniteHeight}");
                }

                // Update achievement progress based on current session height
                // This will trigger notifications when thresholds are met (25, 50, 100, etc.)
                UpdateInfiniteHeightAchievements(currentSessionHeight);
            }
        }

        /// <summary>
        /// Called when consecutive perfect hits count changes
        /// </summary>
        private void OnConsecutivePerfectHitsChanged(int consecutivePerfectHits)
        {
            if (consecutivePerfectHits > maxConsecutivePerfectHits)
            {
                maxConsecutivePerfectHits = consecutivePerfectHits;
                SaveTrackingState();
                UpdatePerfectPlacementsAchievements();
            }
        }

        /// <summary>
        /// Check time-based achievements
        /// </summary>
        private void CheckTimeBasedAchievements()
        {
            if (checkedTimeBasedAchievements) return;

            System.DateTime now = System.DateTime.Now;
            int hour = now.Hour;

            // Morning: 5:00 AM - 10:00 AM
            if (hour >= 5 && hour < 10)
            {
                achievementManager.UnlockAchievement("sunrise_builder");
            }

            // Night: 10:00 PM - 2:00 AM
            if (hour >= 22 || hour < 2)
            {
                achievementManager.UnlockAchievement("midnight_builder");
            }

            checkedTimeBasedAchievements = true;
        }

        /// <summary>
        /// Update sites completed achievements
        /// </summary>
        private void UpdateSitesCompletedAchievements()
        {
            achievementManager.UpdateProgress("first_steps", sitesCompleted);
            achievementManager.UpdateProgress("path_of_jaguar", sitesCompleted);
            achievementManager.UpdateProgress("maya_wanderer", sitesCompleted);
            achievementManager.UpdateProgress("temple_traveler", sitesCompleted);
            achievementManager.UpdateProgress("keeper_of_ruins", sitesCompleted);
            achievementManager.UpdateProgress("world_of_maya", sitesCompleted);
        }

        /// <summary>
        /// Update infinite height achievements
        /// </summary>
        /// <param name="height">Height to use for progress update (defaults to all-time max)</param>
        private void UpdateInfiniteHeightAchievements(int height = -1)
        {
            // Use provided height, or fall back to all-time max
            int progressHeight = height >= 0 ? height : maxInfiniteHeight;

            // Update progress (this will only unlock if not already unlocked)
            achievementManager.UpdateProgress("survivor", progressHeight);
            achievementManager.UpdateProgress("peak_ascender", progressHeight);
            achievementManager.UpdateProgress("sky_piercer", progressHeight);

            // For infinite stacker, manually trigger notifications for milestones reached this session
            // This ensures players see notifications every run, not just the first time ever
            if (height >= 0) // Only do this for current session updates (not initialization)
            {
                CheckAndNotifyMilestone("survivor", 25, height);
                CheckAndNotifyMilestone("peak_ascender", 50, height);
                CheckAndNotifyMilestone("sky_piercer", 100, height);
            }
        }

        /// <summary>
        /// Check if a milestone was reached and show notification if not already shown this session
        /// </summary>
        private void CheckAndNotifyMilestone(string achievementId, int milestone, int currentHeight)
        {
            // Check if we just reached this milestone (within 1 block to account for timing)
            bool justReached = currentHeight >= milestone && currentHeight <= milestone + 1;

            // If we just reached it and haven't shown it this session
            if (justReached && !sessionAchievementsShown.Contains(achievementId))
            {
                sessionAchievementsShown.Add(achievementId);

                // Get the achievement definition
                var achievement = achievementManager.GetAchievement(achievementId);
                if (achievement != null)
                {
                    Debug.Log($"[AchievementTracker] Showing notification for {achievement.title} at height {currentHeight}");

                    // Check if already unlocked (first time ever vs. this session)
                    bool isAlreadyUnlocked = achievementManager.IsUnlocked(achievementId);

                    if (isAlreadyUnlocked)
                    {
                        // Achievement was unlocked before - just show notification without unlocking again
                        // This shows the notification on every run after first unlock
                        achievementManager.OnAchievementUnlocked?.Invoke(achievement);
                    }
                    else
                    {
                        // First time unlocking - use UnlockAchievement which will:
                        // 1. Mark as unlocked in progress data
                        // 2. Trigger OnAchievementUnlocked event (shows notification)
                        // 3. Report to Google Play Games
                        achievementManager.UnlockAchievement(achievementId);
                    }
                }
            }
        }

        /// <summary>
        /// Update codex achievements
        /// </summary>
        private void UpdateCodexAchievements()
        {
            achievementManager.UpdateProgress("page_keeper", codexEntriesUnlocked);
            achievementManager.UpdateProgress("lore_seeker", codexEntriesUnlocked);
            achievementManager.UpdateProgress("codex_scholar", codexEntriesUnlocked);
        }

        /// <summary>
        /// Update perfect placements achievements
        /// </summary>
        private void UpdatePerfectPlacementsAchievements()
        {
            achievementManager.UpdateProgress("perfect_dropper", maxConsecutivePerfectHits);
            achievementManager.UpdateProgress("stone_stack_specialist", maxConsecutivePerfectHits);
        }

        /// <summary>
        /// Update perfect level completion achievements
        /// </summary>
        private void UpdatePerfectLevelAchievements(int levelNumber)
        {
            // Perfectionist Architect: Beat any site with only perfect drops
            achievementManager.UpdateProgress("perfectionist_architect", perfectLevelCompletions);

            // Zero Room for Error: Beat a level 10+ with only perfect drops
            if (levelNumber >= 10)
            {
                achievementManager.UnlockAchievement("zero_room_for_error");
            }

            // Feathered Serpent's Blessing: Finish 5 levels with perfect drops
            achievementManager.UpdateProgress("feathered_serpent_blessing", perfectLevelCompletions);
        }

        /// <summary>
        /// Update leaderboard rank achievement (call this from UI when leaderboard is loaded)
        /// </summary>
        public void UpdateLeaderboardRank(int rank)
        {
            if (rank < bestLeaderboardRank)
            {
                bestLeaderboardRank = rank;
                SaveTrackingState();

                // Temple Guardian: Get in top 5
                if (rank <= 5)
                {
                    achievementManager.UnlockAchievement("temple_guardian");
                }
            }
        }

        /// <summary>
        /// Update all achievement progress (called on initialization)
        /// </summary>
        private void UpdateAllAchievementProgress()
        {
            UpdateSitesCompletedAchievements();
            UpdateInfiniteHeightAchievements();
            UpdateCodexAchievements();
            UpdatePerfectPlacementsAchievements();

            // Check if we already have perfect level completions
            if (perfectLevelCompletions > 0)
            {
                achievementManager.UpdateProgress("perfectionist_architect", perfectLevelCompletions);
                achievementManager.UpdateProgress("feathered_serpent_blessing", perfectLevelCompletions);
            }
        }

        /// <summary>
        /// Save tracking state to PlayerPrefs
        /// </summary>
        private void SaveTrackingState()
        {
            PlayerPrefs.SetInt("InfiniteStacker_MaxHeight", maxInfiniteHeight);
            PlayerPrefs.SetInt("MaxConsecutivePerfectHits", maxConsecutivePerfectHits);
            PlayerPrefs.SetInt("BestLeaderboardRank", bestLeaderboardRank);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Force update all achievements with current tracking data (for debugging)
        /// </summary>
        [ContextMenu("Force Update All Achievements")]
        public void ForceUpdateAllAchievements()
        {
            Debug.Log($"[AchievementTracker] Force updating achievements - Sites: {sitesCompleted}, Codex: {codexEntriesUnlocked}, Perfect Hits: {maxConsecutivePerfectHits}");

            if (achievementManager == null)
            {
                Debug.LogError("AchievementManager is null!");
                return;
            }

            // Check current progress in AchievementManager
            int currentFirstSteps = achievementManager.GetProgress("first_steps");
            Debug.Log($"Current 'first_steps' progress in AchievementManager: {currentFirstSteps}");
            Debug.Log($"Trying to update to: {sitesCompleted}");

            UpdateAllAchievementProgress();
            Debug.Log("Force update complete!");
        }

        private void OnDestroy()
        {
            // Unsubscribe from scene events
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // Unsubscribe from game events
            UnsubscribeFromEvents();
        }
    }
}

