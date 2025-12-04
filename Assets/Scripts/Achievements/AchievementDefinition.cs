using System;
using UnityEngine;

namespace TamalStacker.Achievements
{
    /// <summary>
    /// Defines the type of achievement
    /// </summary>
    public enum AchievementType
    {
        /// <summary>
        /// Achievement that is unlocked once (e.g., complete first level)
        /// </summary>
        OneTime,
        
        /// <summary>
        /// Achievement that tracks incremental progress (e.g., complete 10 levels)
        /// </summary>
        Incremental
    }

    /// <summary>
    /// Defines the condition type for achievement tracking
    /// </summary>
    public enum AchievementConditionType
    {
        /// <summary>
        /// Number of sites/levels completed (stars > 0)
        /// </summary>
        SitesCompleted,
        
        /// <summary>
        /// Maximum height reached in Infinite Stacker mode
        /// </summary>
        InfiniteHeight,
        
        /// <summary>
        /// Number of unique codex entries unlocked
        /// </summary>
        CodexEntries,
        
        /// <summary>
        /// Maximum consecutive perfect placements in a session
        /// </summary>
        PerfectPlacements,
        
        /// <summary>
        /// Best leaderboard rank achieved in any level
        /// </summary>
        LeaderboardRank,
        
        /// <summary>
        /// Number of levels completed with only perfect drops
        /// </summary>
        PerfectLevelCompletions,
        
        /// <summary>
        /// Level completed with only perfect drops and level number >= threshold
        /// </summary>
        PerfectHighLevelCompletion,
        
        /// <summary>
        /// Playing during specific time of day (morning)
        /// </summary>
        TimeMorning,
        
        /// <summary>
        /// Playing during specific time of day (night)
        /// </summary>
        TimeNight
    }

    /// <summary>
    /// Defines a condition for achievement unlocking
    /// </summary>
    [Serializable]
    public class AchievementCondition
    {
        /// <summary>
        /// Type of condition to check
        /// </summary>
        public string type;
        
        /// <summary>
        /// Target value for the condition
        /// </summary>
        public int value;

        /// <summary>
        /// Parse the type string to enum
        /// </summary>
        public AchievementConditionType GetConditionType()
        {
            if (Enum.TryParse(type, out AchievementConditionType result))
            {
                return result;
            }
            Debug.LogError($"Unknown achievement condition type: {type}");
            return AchievementConditionType.SitesCompleted;
        }
    }

    /// <summary>
    /// Defines an achievement configuration
    /// This class is modular and can be reused in different games
    /// </summary>
    [Serializable]
    public class AchievementDefinition
    {
        /// <summary>
        /// Unique identifier for this achievement (internal use)
        /// </summary>
        public string id;
        
        /// <summary>
        /// Google Play Games achievement ID
        /// </summary>
        public string googlePlayId;
        
        /// <summary>
        /// Display name of the achievement
        /// </summary>
        public string title;
        
        /// <summary>
        /// Description of how to unlock the achievement
        /// </summary>
        public string description;
        
        /// <summary>
        /// Path to the achievement icon in Resources folder
        /// </summary>
        public string iconPath;
        
        /// <summary>
        /// Type of achievement (OneTime or Incremental)
        /// </summary>
        public string type;
        
        /// <summary>
        /// Target value for incremental achievements
        /// </summary>
        public int targetValue;
        
        /// <summary>
        /// Optional category for grouping achievements
        /// </summary>
        public string category;
        
        /// <summary>
        /// If true, this achievement is hidden until unlocked
        /// Hidden categories only show filter buttons if at least one achievement is unlocked
        /// </summary>
        public bool hidden = false;
        
        /// <summary>
        /// Condition that must be met to unlock this achievement
        /// </summary>
        public AchievementCondition condition;

        /// <summary>
        /// Get the achievement type as enum
        /// </summary>
        public AchievementType GetAchievementType()
        {
            if (Enum.TryParse(type, out AchievementType result))
            {
                return result;
            }
            Debug.LogError($"Unknown achievement type: {type}");
            return AchievementType.OneTime;
        }

        /// <summary>
        /// Validate the achievement definition
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("Achievement has no ID");
                return false;
            }

            if (string.IsNullOrEmpty(googlePlayId))
            {
                Debug.LogWarning($"Achievement {id} has no Google Play ID");
            }

            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError($"Achievement {id} has no title");
                return false;
            }

            if (condition == null)
            {
                Debug.LogError($"Achievement {id} has no condition");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Container for loading achievements from JSON
    /// </summary>
    [Serializable]
    public class AchievementConfig
    {
        public AchievementDefinition[] achievements;
    }
}

