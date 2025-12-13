using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TamalStacker.Achievements
{
    /// <summary>
    /// In-game achievement viewer panel
    /// Similar to CodexManager pattern - displays all achievements with progress
    /// This class is modular and can be reused across different games
    /// Note: Panel activation/deactivation is handled by MainMenuManager (or parent manager)
    /// This component only manages internal state and content
    /// </summary>
    public class AchievementPanelUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform achievementEntryContainer;
        [SerializeField] private GameObject achievementEntryPrefab;
        [SerializeField] private ScrollRect achievementsScrollRect;

        [Header("Filter Buttons")]
        [SerializeField] private Button allButton;
        [SerializeField] private Button sitesButton;
        [SerializeField] private Button infiniteButton;
        [SerializeField] private Button codexButton;
        [SerializeField] private Button perfectButton;
        [SerializeField] private Button perfectLevelButton;
        [SerializeField] private Button secretButton;

        [Header("Button Highlighting")]
        [SerializeField] private Color selectedButtonColor = new Color(1f, 0.8f, 0.2f); // Gold/yellow
        [SerializeField] private Color normalButtonColor = Color.white;

        [Header("Stats Display")]
        [SerializeField] private TextMeshProUGUI unlockedCountText;
        [SerializeField] private Image completionFillImage;

        [Header("Google Play Button")]
        [SerializeField] private Button showGooglePlayAchievementsButton;

        [Header("Empty State")]
        [SerializeField] private GameObject emptyStatePanel;
        [SerializeField] private TextMeshProUGUI emptyStateText;

        // References
        private AchievementManager achievementManager;
        private GooglePlayAchievementService googlePlayService;
        private MainMenuSoundManager soundManager;

        // State
        private List<GameObject> spawnedEntries = new List<GameObject>();
        private string currentFilter = "all";

        private void Awake()
        {
            // Register with DependencyRegistry
            DependencyRegistry.Register<AchievementPanelUI>(this);
        }

        private void Start()
        {
            // Find achievement manager
            achievementManager = DependencyRegistry.Find<AchievementManager>();
            soundManager = DependencyRegistry.Find<MainMenuSoundManager>();

            if (achievementManager == null)
            {
                Debug.LogWarning("AchievementPanelUI: AchievementManager not found!");
            }

            // Set up filter buttons
            SetupFilterButtons();

            // Set up Google Play button
            if (showGooglePlayAchievementsButton != null)
            {
                showGooglePlayAchievementsButton.onClick.AddListener(ShowGooglePlayAchievements);
            }

            // Subscribe to achievement events
            if (achievementManager != null)
            {
                achievementManager.OnAchievementUnlocked += OnAchievementUnlocked;
                achievementManager.OnAchievementProgressUpdated += OnAchievementProgressUpdated;
            }
        }

        /// <summary>
        /// Set up filter button listeners
        /// </summary>
        private void SetupFilterButtons()
        {
            if (allButton != null)
            {
                allButton.onClick.AddListener(() => SetFilter("all"));
            }

            if (sitesButton != null)
            {
                sitesButton.onClick.AddListener(() => SetFilter("sites"));
            }

            if (infiniteButton != null)
            {
                infiniteButton.onClick.AddListener(() => SetFilter("infinite"));
            }

            if (codexButton != null)
            {
                codexButton.onClick.AddListener(() => SetFilter("codex"));
            }

            if (perfectButton != null)
            {
                perfectButton.onClick.AddListener(() => SetFilter("perfect"));
            }

            if (perfectLevelButton != null)
            {
                perfectLevelButton.onClick.AddListener(() => SetFilter("perfect_level"));
            }

            if (secretButton != null)
            {
                secretButton.onClick.AddListener(() => SetFilter("secret"));
            }
        }

        /// <summary>
        /// Update filter button visibility based on unlocked achievements
        /// Hidden categories only show their button if at least one achievement is unlocked
        /// </summary>
        private void UpdateFilterButtonVisibility()
        {
            if (achievementManager == null) return;

            // Check perfect_level category (hidden category)
            if (perfectLevelButton != null)
            {
                bool hasUnlocked = HasUnlockedInCategory("perfect_level");
                perfectLevelButton.gameObject.SetActive(hasUnlocked);
            }

            // Check secret category (hidden category)
            if (secretButton != null)
            {
                bool hasUnlocked = HasUnlockedInCategory("secret");
                secretButton.gameObject.SetActive(hasUnlocked);
            }
        }

        /// <summary>
        /// Check if the player has unlocked at least one achievement in a category
        /// </summary>
        private bool HasUnlockedInCategory(string category)
        {
            if (achievementManager == null) return false;

            var categoryAchievements = achievementManager.GetAchievementsByCategory(category);
            foreach (var achievement in categoryAchievements)
            {
                if (achievementManager.IsUnlocked(achievement.id))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Refresh and display achievement panel content
        /// Note: Panel GameObject activation is handled by MainMenuManager
        /// </summary>
        public void ShowPanel()
        {
            // Reset to "all" filter when panel is opened
            currentFilter = "all";

            // Update button highlight to show "all" is selected
            UpdateButtonHighlight();

            // Refresh panel content
            RefreshPanel();
        }

        /// <summary>
        /// Refresh the panel with current achievement data
        /// </summary>
        public void RefreshPanel()
        {
            // Try to find achievement manager if not already found
            if (achievementManager == null)
            {
                achievementManager = DependencyRegistry.Find<AchievementManager>();
            }

            if (achievementManager == null)
            {
                Debug.LogWarning("AchievementPanelUI: AchievementManager not found!");
                ShowEmptyState(true, "Achievement system not found.");
                return;
            }

            if (!achievementManager.IsInitialized)
            {
                Debug.LogWarning("AchievementPanelUI: AchievementManager not initialized yet");
                ShowEmptyState(true, "Achievement system is initializing...");
                return;
            }

            // Clear existing entries
            ClearEntries();

            // Get achievements based on filter
            List<AchievementDefinition> achievements;
            if (currentFilter == "all")
            {
                achievements = achievementManager.GetAllAchievements();
                Debug.Log($"AchievementPanelUI: Retrieved {achievements.Count} achievements (filter: all)");
            }
            else
            {
                achievements = achievementManager.GetAchievementsByCategory(currentFilter);
                Debug.Log($"AchievementPanelUI: Retrieved {achievements.Count} achievements (filter: {currentFilter})");
            }

            if (achievements.Count == 0)
            {
                ShowEmptyState(true, "No achievements in this category.");
                return;
            }

            ShowEmptyState(false, "");

            // Spawn entries
            foreach (var achievement in achievements)
            {
                SpawnAchievementEntry(achievement);
            }

            // Update stats
            UpdateStatsDisplay();

            // Note: Filter buttons (including Perfect Level and Secret) are always visible

            // Reset scroll position
            if (achievementsScrollRect != null)
            {
                achievementsScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// Spawn a single achievement entry
        /// </summary>
        private void SpawnAchievementEntry(AchievementDefinition achievement)
        {
            if (achievementEntryPrefab == null || achievementEntryContainer == null)
            {
                Debug.LogWarning("Achievement entry prefab or container not assigned");
                return;
            }

            GameObject entryObj = Instantiate(achievementEntryPrefab, achievementEntryContainer);

            // Get progress data
            int currentProgress = achievementManager.GetProgress(achievement.id);
            bool isUnlocked = achievementManager.IsUnlocked(achievement.id);

            // Get AchievementEntryUI component and set it up
            var entryUI = entryObj.GetComponent<AchievementEntryUI>();
            if (entryUI != null)
            {
                entryUI.Setup(achievement, currentProgress, isUnlocked);
            }
            else
            {
                Debug.LogWarning("Achievement entry prefab is missing AchievementEntryUI component!");
            }

            spawnedEntries.Add(entryObj);
        }

        /// <summary>
        /// Clear all spawned entries
        /// </summary>
        private void ClearEntries()
        {
            foreach (var entry in spawnedEntries)
            {
                if (entry != null)
                {
                    Destroy(entry);
                }
            }
            spawnedEntries.Clear();
        }

        /// <summary>
        /// Set the current filter
        /// </summary>
        private void SetFilter(string filter)
        {
            currentFilter = filter;
            
            // Play category-specific sound effect
            if (soundManager != null)
            {
                soundManager.PlayAchievementCategory(filter);
            }
            
            UpdateButtonHighlight();
            RefreshPanel();
        }

        /// <summary>
        /// Update button highlighting to show which filter is active
        /// </summary>
        private void UpdateButtonHighlight()
        {
            // Reset all buttons to normal color
            ResetButtonColor(allButton);
            ResetButtonColor(sitesButton);
            ResetButtonColor(infiniteButton);
            ResetButtonColor(codexButton);
            ResetButtonColor(perfectButton);
            ResetButtonColor(perfectLevelButton);
            ResetButtonColor(secretButton);

            // Highlight the active button
            switch (currentFilter)
            {
                case "all":
                    HighlightButton(allButton);
                    break;
                case "sites":
                    HighlightButton(sitesButton);
                    break;
                case "infinite":
                    HighlightButton(infiniteButton);
                    break;
                case "codex":
                    HighlightButton(codexButton);
                    break;
                case "perfect":
                    HighlightButton(perfectButton);
                    break;
                case "perfect_level":
                    HighlightButton(perfectLevelButton);
                    break;
                case "secret":
                    HighlightButton(secretButton);
                    break;
            }
        }

        /// <summary>
        /// Highlight a button to show it's selected
        /// </summary>
        private void HighlightButton(Button button)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = selectedButtonColor;
            colors.selectedColor = selectedButtonColor;
            button.colors = colors;

            // Also update the image color immediately
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selectedButtonColor;
            }
        }

        /// <summary>
        /// Reset button to normal color
        /// </summary>
        private void ResetButtonColor(Button button)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = normalButtonColor;
            colors.selectedColor = normalButtonColor;
            button.colors = colors;

            // Also update the image color immediately
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = normalButtonColor;
            }
        }

        /// <summary>
        /// Update stats display (unlocked count)
        /// </summary>
        private void UpdateStatsDisplay()
        {
            if (achievementManager == null) return;

            int total = achievementManager.TotalAchievements;
            int unlocked = achievementManager.UnlockedAchievements;
            float percentage = achievementManager.GetCompletionPercentage();

            if (unlockedCountText != null)
            {
                unlockedCountText.text = $"{unlocked}/{total}";
            }

            if (completionFillImage != null)
            {
                completionFillImage.fillAmount = percentage / 100f;
            }
        }

        /// <summary>
        /// Show or hide empty state
        /// </summary>
        private void ShowEmptyState(bool show, string message)
        {
            if (emptyStatePanel != null)
            {
                emptyStatePanel.SetActive(show);
            }

            if (emptyStateText != null && !string.IsNullOrEmpty(message))
            {
                emptyStateText.text = message;
            }
        }

        /// <summary>
        /// Show Google Play Games achievements UI
        /// </summary>
        private void ShowGooglePlayAchievements()
        {
            if (googlePlayService == null)
            {
                // Try to get from achievement manager
                var manager = DependencyRegistry.Find<AchievementManager>();
                if (manager != null)
                {
                    googlePlayService = manager.GetComponent<GooglePlayAchievementService>();
                }
            }

            if (googlePlayService != null)
            {
                googlePlayService.ShowAchievementsUI();
            }
            else
            {
                Debug.LogWarning("Google Play Achievement Service not found");
            }
        }

        /// <summary>
        /// Called when an achievement is unlocked
        /// </summary>
        private void OnAchievementUnlocked(AchievementDefinition achievement)
        {
            // Refresh panel if it's currently open (check if this component is active)
            if (gameObject.activeInHierarchy)
            {
                RefreshPanel();
            }
        }

        /// <summary>
        /// Called when achievement progress is updated
        /// </summary>
        private void OnAchievementProgressUpdated(AchievementDefinition achievement, int current, int target)
        {
            // Refresh panel if it's currently open (check if this component is active)
            if (gameObject.activeInHierarchy)
            {
                RefreshPanel();
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (achievementManager != null)
            {
                achievementManager.OnAchievementUnlocked -= OnAchievementUnlocked;
                achievementManager.OnAchievementProgressUpdated -= OnAchievementProgressUpdated;
            }

            // Clean up button listeners
            if (allButton != null) allButton.onClick.RemoveAllListeners();
            if (sitesButton != null) sitesButton.onClick.RemoveAllListeners();
            if (infiniteButton != null) infiniteButton.onClick.RemoveAllListeners();
            if (codexButton != null) codexButton.onClick.RemoveAllListeners();
            if (perfectButton != null) perfectButton.onClick.RemoveAllListeners();
            if (perfectLevelButton != null) perfectLevelButton.onClick.RemoveAllListeners();
            if (secretButton != null) secretButton.onClick.RemoveAllListeners();
            if (showGooglePlayAchievementsButton != null) showGooglePlayAchievementsButton.onClick.RemoveAllListeners();

            // Unregister from DependencyRegistry
            DependencyRegistry.Unregister<AchievementPanelUI>(this);

            // Clear entries
            ClearEntries();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor utility: Auto-assign category icons to the achievement entry prefab
        /// Gets the icon sprites from the category filter buttons and assigns them to the prefab
        /// </summary>
        [ContextMenu("Setup Category Icons in Prefab")]
        private void SetupCategoryIconsInPrefab()
        {
            if (achievementEntryPrefab == null)
            {
                Debug.LogError("Achievement Entry Prefab is not assigned!");
                return;
            }

            AchievementEntryUI entryUI = achievementEntryPrefab.GetComponent<AchievementEntryUI>();
            if (entryUI == null)
            {
                Debug.LogError("Achievement Entry Prefab doesn't have AchievementEntryUI component!");
                return;
            }

            // Get sprites from category buttons
            Sprite sitesIcon = sitesButton?.GetComponent<Image>()?.sprite;
            Sprite infiniteIcon = infiniteButton?.GetComponent<Image>()?.sprite;
            Sprite codexIcon = codexButton?.GetComponent<Image>()?.sprite;
            Sprite perfectIcon = perfectButton?.GetComponent<Image>()?.sprite;
            Sprite perfectLevelIcon = perfectLevelButton?.GetComponent<Image>()?.sprite;
            Sprite secretIcon = secretButton?.GetComponent<Image>()?.sprite;

            // Use SerializedObject to modify prefab
            UnityEditor.SerializedObject serializedEntry = new UnityEditor.SerializedObject(entryUI);

            if (sitesIcon != null)
                serializedEntry.FindProperty("sitesCategoryIcon").objectReferenceValue = sitesIcon;
            if (infiniteIcon != null)
                serializedEntry.FindProperty("infiniteCategoryIcon").objectReferenceValue = infiniteIcon;
            if (codexIcon != null)
                serializedEntry.FindProperty("codexCategoryIcon").objectReferenceValue = codexIcon;
            if (perfectIcon != null)
                serializedEntry.FindProperty("perfectCategoryIcon").objectReferenceValue = perfectIcon;
            if (perfectLevelIcon != null)
                serializedEntry.FindProperty("perfectLevelCategoryIcon").objectReferenceValue = perfectLevelIcon;
            if (secretIcon != null)
                serializedEntry.FindProperty("secretCategoryIcon").objectReferenceValue = secretIcon;

            serializedEntry.ApplyModifiedProperties();

            Debug.Log("Category icons assigned to Achievement Entry Prefab successfully!");
            Debug.Log($"Assigned: Sites={sitesIcon != null}, Infinite={infiniteIcon != null}, Codex={codexIcon != null}, Perfect={perfectIcon != null}, PerfectLevel={perfectLevelIcon != null}, Secret={secretIcon != null}");
        }
#endif
    }
}

