using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TamalStacker.Achievements
{
    /// <summary>
    /// Custom in-game popup notification system for achievement unlocks
    /// Displays animated toast notifications with queuing support
    /// This class is modular and can be reused across different games
    /// </summary>
    public class AchievementNotificationUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject notificationPanel;
        [SerializeField] private Image achievementIcon;
        [SerializeField] private TextMeshProUGUI achievementTitle;
        [SerializeField] private TextMeshProUGUI achievementDescription;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private Vector2 startPosition = new Vector2(0, 100);
        [SerializeField] private Vector2 displayPosition = new Vector2(0, -50);
        [SerializeField] private AnimationCurve slideInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Audio")]
        [SerializeField] private AudioClip unlockSound;
        [SerializeField] private AudioSource audioSource;

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

        [Header("Testing (Inspector Only)")]
        [SerializeField] private string testAchievementId = "first_steps";
        [Tooltip("Test with a custom title and description")]
        [SerializeField] private bool useCustomTestData = false;
        [SerializeField] private string testTitle = "Test Achievement";
        [SerializeField] private string testDescription = "This is a test notification";

        // Queue for multiple notifications
        private Queue<AchievementDefinition> notificationQueue = new Queue<AchievementDefinition>();
        private bool isDisplaying = false;

        // References
        private AchievementManager achievementManager;
        private RectTransform panelRectTransform;

        private void Awake()
        {
            // Get components
            if (notificationPanel != null)
            {
                panelRectTransform = notificationPanel.GetComponent<RectTransform>();
            }

            if (canvasGroup == null && notificationPanel != null)
            {
                canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = notificationPanel.AddComponent<CanvasGroup>();
                }
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            // Hide notification panel initially
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }

            // Register with DependencyRegistry
            DependencyRegistry.Register<AchievementNotificationUI>(this);
        }

        private void Start()
        {
            // Find achievement manager
            achievementManager = DependencyRegistry.Find<AchievementManager>();

            if (achievementManager != null)
            {
                // Subscribe to achievement unlocked event
                achievementManager.OnAchievementUnlocked += OnAchievementUnlocked;
            }
            else
            {
                Debug.LogWarning("AchievementNotificationUI: AchievementManager not found!");
            }
        }

        /// <summary>
        /// Called when an achievement is unlocked
        /// </summary>
        private void OnAchievementUnlocked(AchievementDefinition achievement)
        {
            if (achievement == null) return;

            Debug.Log($"[AchievementNotificationUI] Queueing notification for: {achievement.title}");

            // Add to queue
            notificationQueue.Enqueue(achievement);

            // Start displaying if not already doing so
            if (!isDisplaying)
            {
                StartCoroutine(DisplayNextNotification());
            }
        }

        /// <summary>
        /// Display the next notification in the queue
        /// </summary>
        private IEnumerator DisplayNextNotification()
        {
            while (notificationQueue.Count > 0)
            {
                isDisplaying = true;
                var achievement = notificationQueue.Dequeue();

                yield return StartCoroutine(ShowNotification(achievement));

                // Wait a bit before showing next notification
                yield return new WaitForSeconds(0.5f);
            }

            isDisplaying = false;
        }

        /// <summary>
        /// Show a single notification
        /// </summary>
        private IEnumerator ShowNotification(AchievementDefinition achievement)
        {
            if (notificationPanel == null || achievementTitle == null)
            {
                Debug.LogWarning("AchievementNotificationUI: UI components not assigned!");
                yield break;
            }

            // Set achievement data (localized)
            achievementTitle.text = LocalizationManager.GetAchievementTitle(achievement.id, achievement.title);
            if (achievementDescription != null)
            {
                achievementDescription.text = LocalizationManager.GetAchievementDescription(achievement.id, achievement.description);
            }

            // Load icon if available
            if (achievementIcon != null)
            {
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
                    achievementIcon.sprite = iconToUse;
                    achievementIcon.gameObject.SetActive(true);
                }
                else
                {
                    // No icon available at all (neither specific nor category)
                    Debug.LogWarning($"No icon available for achievement '{achievement.id}' with category '{achievement.category}'");
                    achievementIcon.gameObject.SetActive(false);
                }
            }

            // Play sound
            if (unlockSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(unlockSound);
            }

            // Show and animate panel
            notificationPanel.SetActive(true);

            // Animate in
            yield return StartCoroutine(AnimateIn());

            // Display for duration
            yield return new WaitForSeconds(displayDuration);

            // Animate out
            yield return StartCoroutine(AnimateOut());

            // Hide panel
            notificationPanel.SetActive(false);
        }

        /// <summary>
        /// Animate notification sliding in and fading in
        /// </summary>
        private IEnumerator AnimateIn()
        {
            if (panelRectTransform == null || canvasGroup == null) yield break;

            float elapsed = 0f;
            canvasGroup.alpha = 0f;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                float curveT = slideInCurve.Evaluate(t);

                // Fade in
                canvasGroup.alpha = t;

                // Slide in from top
                panelRectTransform.anchoredPosition = Vector2.Lerp(startPosition, displayPosition, curveT);

                yield return null;
            }

            canvasGroup.alpha = 1f;
            panelRectTransform.anchoredPosition = displayPosition;
        }

        /// <summary>
        /// Animate notification fading out
        /// </summary>
        private IEnumerator AnimateOut()
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;

                canvasGroup.alpha = 1f - t;

                yield return null;
            }

            canvasGroup.alpha = 0f;
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
        /// Test notification with achievement ID (can be called from Inspector)
        /// Right-click component in Inspector → Test Notification
        /// </summary>
        [ContextMenu("Test Notification")]
        private void TestNotificationFromInspector()
        {
            if (useCustomTestData)
            {
                // Create a test achievement definition
                var testAchievement = new AchievementDefinition
                {
                    id = "test_achievement",
                    title = testTitle,
                    description = testDescription,
                    iconPath = "",
                    type = "OneTime",
                    targetValue = 1,
                    category = "test"
                };

                OnAchievementUnlocked(testAchievement);
                Debug.Log($"Showing test notification with custom data: {testTitle}");
            }
            else
            {
                ShowTestNotification(testAchievementId);
            }
        }

        /// <summary>
        /// Test multiple notifications in queue (can be called from Inspector)
        /// Right-click component in Inspector → Test Multiple Notifications
        /// </summary>
        [ContextMenu("Test Multiple Notifications")]
        private void TestMultipleNotifications()
        {
            if (achievementManager == null || !achievementManager.IsInitialized)
            {
                Debug.LogWarning("AchievementManager not initialized. Showing dummy notifications...");

                // Show 3 test notifications
                for (int i = 1; i <= 3; i++)
                {
                    var testAchievement = new AchievementDefinition
                    {
                        id = $"test_{i}",
                        title = $"Test Achievement {i}",
                        description = $"This is test notification #{i}",
                        iconPath = "",
                        type = "OneTime",
                        targetValue = 1,
                        category = "test"
                    };
                    OnAchievementUnlocked(testAchievement);
                }
                return;
            }

            // Show first 3 achievements from config
            var allAchievements = achievementManager.GetAllAchievements();
            int count = System.Math.Min(3, allAchievements.Count);

            for (int i = 0; i < count; i++)
            {
                OnAchievementUnlocked(allAchievements[i]);
            }

            Debug.Log($"Queued {count} test notifications");
        }

        /// <summary>
        /// Manually show a notification (for testing)
        /// </summary>
        public void ShowTestNotification(string achievementId)
        {
            if (achievementManager == null)
            {
                Debug.LogWarning("AchievementManager not found");
                return;
            }

            var achievement = achievementManager.GetAchievement(achievementId);
            if (achievement != null)
            {
                OnAchievementUnlocked(achievement);
                Debug.Log($"Showing test notification for: {achievement.title}");
            }
            else
            {
                Debug.LogWarning($"Achievement not found: {achievementId}");
            }
        }

        /// <summary>
        /// Clear notification queue
        /// </summary>
        public void ClearQueue()
        {
            notificationQueue.Clear();
            StopAllCoroutines();
            isDisplaying = false;
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (achievementManager != null)
            {
                achievementManager.OnAchievementUnlocked -= OnAchievementUnlocked;
            }

            // Unregister from DependencyRegistry
            DependencyRegistry.Unregister<AchievementNotificationUI>(this);
        }
    }
}

