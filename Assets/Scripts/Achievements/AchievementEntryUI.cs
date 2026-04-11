using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TamalStacker.Achievements
{
    /// <summary>
    /// UI component for a single achievement entry in the achievement panel
    /// Displays achievement icon, title, description, progress bar, and locked state
    /// Attach this to the achievement entry prefab
    /// </summary>
    public class AchievementEntryUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Text component for achievement title")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Tooltip("Text component for achievement description")]
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Tooltip("Text component for progress display (e.g., '5/10')")]
        [SerializeField] private TextMeshProUGUI progressText;

        [Tooltip("Container GameObject for the progress bar (hidden for single-goal achievements)")]
        [SerializeField] private GameObject progressBarDisplay;

        [Tooltip("Fill image for visual progress bar")]
        [SerializeField] private Image progressBarFillImage;

        [Tooltip("Image component for achievement icon")]
        [SerializeField] private Image iconImage;

        [Tooltip("Overlay that appears when achievement is locked")]
        [SerializeField] private GameObject lockedOverlay;

        [Tooltip("Optional: Background image to highlight unlocked achievements")]
        [SerializeField] private Image backgroundImage;

        [Header("Category Icon Mapping")]
        [Tooltip("Sprite to use for 'sites' category achievements")]
        [SerializeField] private Sprite sitesCategoryIcon;

        [Tooltip("Sprite to use for 'infinite' category achievements")]
        [SerializeField] private Sprite infiniteCategoryIcon;

        [Tooltip("Sprite to use for 'codex' category achievements")]
        [SerializeField] private Sprite codexCategoryIcon;

        [Tooltip("Sprite to use for 'perfect' category achievements")]
        [SerializeField] private Sprite perfectCategoryIcon;

        [Tooltip("Sprite to use for 'perfect_level' category achievements")]
        [SerializeField] private Sprite perfectLevelCategoryIcon;

        [Tooltip("Sprite to use for 'secret' category achievements")]
        [SerializeField] private Sprite secretCategoryIcon;

        [Header("Visual Settings")]
        [Tooltip("Color tint for locked achievements")]
        [SerializeField] private Color lockedIconColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Tooltip("Color tint for unlocked achievements")]
        [SerializeField] private Color unlockedIconColor = Color.white;

        // Localized hidden achievement text
        private string hiddenTitle => LocalizationManager.Get("achievement_hidden_title");
        private string hiddenDescription => LocalizationManager.Get("achievement_hidden_description");

        // Cached data
        private AchievementDefinition currentAchievement;
        private bool isUnlocked;

        /// <summary>
        /// Set up the entry with achievement data
        /// </summary>
        /// <param name="achievement">The achievement definition</param>
        /// <param name="currentProgress">Current progress value</param>
        /// <param name="unlocked">Whether the achievement is unlocked</param>
        public void Setup(AchievementDefinition achievement, int currentProgress, bool unlocked)
        {
            if (achievement == null)
            {
                Debug.LogWarning("AchievementEntryUI: Cannot setup with null achievement");
                return;
            }

            currentAchievement = achievement;
            isUnlocked = unlocked;

            // Set title
            UpdateTitle(achievement, unlocked);

            // Set description
            UpdateDescription(achievement, unlocked);

            // Set progress
            UpdateProgress(achievement, currentProgress, unlocked);

            // Set progress bar
            UpdateProgressBar(achievement, currentProgress, unlocked);

            // Set icon
            UpdateIcon(achievement, unlocked);

            // Set locked overlay
            UpdateLockedOverlay(unlocked);
        }

        /// <summary>
        /// Update the title text based on achievement and unlock status
        /// </summary>
        private void UpdateTitle(AchievementDefinition achievement, bool unlocked)
        {
            if (titleText == null) return;

            // Hidden achievements show "???" until unlocked
            if (achievement.hidden && !unlocked)
            {
                titleText.text = hiddenTitle;
            }
            else
            {
                titleText.text = LocalizationManager.GetAchievementTitle(achievement.id, achievement.title);
            }
        }

        /// <summary>
        /// Update the description text based on achievement and unlock status
        /// </summary>
        private void UpdateDescription(AchievementDefinition achievement, bool unlocked)
        {
            if (descriptionText == null) return;

            // Hidden achievements show special text until unlocked
            if (achievement.hidden && !unlocked)
            {
                descriptionText.text = hiddenDescription;
            }
            else
            {
                descriptionText.text = LocalizationManager.GetAchievementDescription(achievement.id, achievement.description);
            }
        }

        /// <summary>
        /// Update the progress text based on achievement type and progress
        /// </summary>
        private void UpdateProgress(AchievementDefinition achievement, int currentProgress, bool unlocked)
        {
            if (progressText == null) return;

            // For achievements with targetValue > 1, show progress
            if (achievement.targetValue > 1)
            {
                // Clamp current progress to target value for display
                // (progress can exceed target, but we don't want to show "5/3")
                int displayProgress = Mathf.Min(currentProgress, achievement.targetValue);

                // Show "X/Y" for incremental achievements
                progressText.text = $"{displayProgress}/{achievement.targetValue}";
                progressText.gameObject.SetActive(true);
            }
            else
            {
                // For single-goal achievements, hide the progress text
                // The locked/unlocked state is already shown via the locked overlay
                progressText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Update the progress bar visual based on current progress
        /// </summary>
        private void UpdateProgressBar(AchievementDefinition achievement, int currentProgress, bool unlocked)
        {
            // Hide progress bar display for single-goal achievements (targetValue == 1)
            // These are binary achievements (unlocked or not), so no progress bar is needed
            bool shouldShowProgressBar = achievement.targetValue > 1;

            if (progressBarDisplay != null)
            {
                progressBarDisplay.SetActive(shouldShowProgressBar);
            }

            if (progressBarFillImage == null) return;

            // Calculate progress percentage
            // Clamp current progress to target value to ensure we don't exceed 100%
            int clampedProgress = Mathf.Min(currentProgress, achievement.targetValue);
            float progress = achievement.targetValue > 0
                ? (float)clampedProgress / achievement.targetValue
                : 0f;

            progressBarFillImage.fillAmount = Mathf.Clamp01(progress);

            // Show progress bar for achievements with progress (will display at 100% when unlocked)
            progressBarFillImage.gameObject.SetActive(shouldShowProgressBar);
        }

        /// <summary>
        /// Update the icon sprite and color based on achievement
        /// Uses achievement-specific icon if available, falls back to category icon
        /// </summary>
        private void UpdateIcon(AchievementDefinition achievement, bool unlocked)
        {
            if (iconImage == null) return;

            Sprite iconToUse = null;

            // Try to load achievement-specific icon from Resources if path is provided
            if (!string.IsNullOrEmpty(achievement.iconPath))
            {
                iconToUse = Resources.Load<Sprite>(achievement.iconPath);
                if (iconToUse == null)
                {
                    Debug.LogWarning($"Achievement icon not found at path: {achievement.iconPath}, falling back to category icon");
                }
            }

            // If no achievement-specific icon found, use category icon as fallback
            if (iconToUse == null)
            {
                iconToUse = GetCategoryIcon(achievement.category);
            }

            // Set the icon if we have one
            if (iconToUse != null)
            {
                iconImage.sprite = iconToUse;
                iconImage.gameObject.SetActive(true);

                // Apply color tint based on unlock status
                // Unlocked = full color, Locked = dark/desaturated
                iconImage.color = unlocked ? unlockedIconColor : lockedIconColor;
            }
            else
            {
                // No icon available at all (neither specific nor category)
                Debug.LogWarning($"No icon available for achievement '{achievement.id}' with category '{achievement.category}'");
                iconImage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Get the category icon sprite based on achievement category
        /// </summary>
        private Sprite GetCategoryIcon(string category)
        {
            switch (category?.ToLower())
            {
                case "sites":
                    return sitesCategoryIcon;
                case "infinite":
                    return infiniteCategoryIcon;
                case "codex":
                    return codexCategoryIcon;
                case "perfect":
                    return perfectCategoryIcon;
                case "perfect_level":
                    return perfectLevelCategoryIcon;
                case "secret":
                    return secretCategoryIcon;
                default:
                    Debug.LogWarning($"Unknown achievement category: {category}");
                    return null;
            }
        }

        /// <summary>
        /// Show or hide the locked overlay
        /// </summary>
        private void UpdateLockedOverlay(bool unlocked)
        {
            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(!unlocked);
            }
        }

        /// <summary>
        /// Refresh the entry (useful when progress updates while entry is visible)
        /// </summary>
        public void Refresh(int currentProgress, bool unlocked)
        {
            if (currentAchievement == null)
            {
                Debug.LogWarning("AchievementEntryUI: Cannot refresh without initial setup");
                return;
            }

            Setup(currentAchievement, currentProgress, unlocked);
        }

        /// <summary>
        /// Get the achievement ID associated with this entry
        /// </summary>
        public string GetAchievementId()
        {
            return currentAchievement?.id;
        }

        /// <summary>
        /// Check if this entry is currently displaying an unlocked achievement
        /// </summary>
        public bool IsUnlocked()
        {
            return isUnlocked;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validate that all required UI references are assigned
        /// </summary>
        private void OnValidate()
        {
            if (titleText == null)
            {
                Debug.LogWarning($"AchievementEntryUI on {gameObject.name}: Title Text not assigned!", this);
            }

            if (descriptionText == null)
            {
                Debug.LogWarning($"AchievementEntryUI on {gameObject.name}: Description Text not assigned!", this);
            }

            if (iconImage == null)
            {
                Debug.LogWarning($"AchievementEntryUI on {gameObject.name}: Icon Image not assigned!", this);
            }

            if (progressBarDisplay == null)
            {
                Debug.LogWarning($"AchievementEntryUI on {gameObject.name}: Progress Bar Display not assigned!", this);
            }
        }
#endif
    }
}

