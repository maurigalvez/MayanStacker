using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages the Codex panel, which displays information about unlocked levels
/// </summary>
public class CodexManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject codexPanel;
    [SerializeField] private Transform codexEntryContainer;
    [SerializeField] private GameObject codexEntryPrefab;
    [SerializeField] private RectTransform scrollContainer; // The scroll area to animate
    [SerializeField] private ScrollRect entriesScrollRect; // The ScrollRect holding the entries

    [Header("Shared Detail Panel")]
    [SerializeField] private GameObject sharedDetailPanel;
    [SerializeField] private ScrollRect detailPanelScrollRect; // The ScrollRect for the detail panel content
    [SerializeField] private Button closeDetailPanelButton;
    [SerializeField] private TextMeshProUGUI detailLevelNameText;
    [SerializeField] private TextMeshProUGUI detailLocationText;
    [SerializeField] private TextMeshProUGUI detailDescriptionText;
    [SerializeField] private Image detailSiteImage;

    [Header("Empty State")]
    [SerializeField] private GameObject emptyStatePanel;
    [SerializeField] private string emptyStateMessage = "No levels unlocked yet. Play levels to unlock entries in the Codex!";

    [Header("Animation Settings")]
    [SerializeField] private float scrollUpAnimationDuration = 0.8f;
    [SerializeField] private AnimationCurve scrollAnimationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

#if UNITY_EDITOR
    [Header("Editor Only - Testing")]
    [SerializeField] private bool unlockAllCodexEntriesInEditor = false;
    [Tooltip("When enabled in editor, all codex entries will be unlocked on Start")]
#endif

    // References (found via DependencyRegistry)
    private LevelManager levelManager;
    private MainMenuSoundManager soundManager;

    // State
    private List<CodexEntryUI> spawnedEntries = new List<CodexEntryUI>();
    private CodexEntryUI currentlySelectedEntry = null;
    private Coroutine scrollAnimationCoroutine;

    private void Awake()
    {
        // Register with DependencyRegistry
        DependencyRegistry.Register<CodexManager>(this);
    }

    private void Start()
    {
        // Find dependencies
        levelManager = DependencyRegistry.Find<LevelManager>();
        soundManager = DependencyRegistry.Find<MainMenuSoundManager>();

        if (levelManager == null)
        {
            Debug.LogWarning("CodexManager: LevelManager not found!");
        }

        // Set scroll container to codex panel transform if not assigned
        if (scrollContainer == null && codexPanel != null)
        {
            scrollContainer = codexPanel.GetComponent<RectTransform>();
            if (scrollContainer == null)
            {
                Debug.LogWarning("CodexManager: Codex panel does not have a RectTransform component. Scale animations will not work.");
            }
        }

        // Find ScrollRect if not assigned
        if (entriesScrollRect == null && codexPanel != null)
        {
            entriesScrollRect = codexPanel.GetComponentInChildren<ScrollRect>();
            if (entriesScrollRect == null)
            {
                Debug.LogWarning("CodexManager: ScrollRect not found in codex panel hierarchy. Scroll position reset will not work.");
            }
        }

        // Find detail panel ScrollRect if not assigned
        if (detailPanelScrollRect == null && sharedDetailPanel != null)
        {
            detailPanelScrollRect = sharedDetailPanel.GetComponentInChildren<ScrollRect>();
            if (detailPanelScrollRect == null)
            {
                Debug.LogWarning("CodexManager: ScrollRect not found in shared detail panel hierarchy. Detail panel scroll position reset will not work.");
            }
        }

        // Hide codex panel by default
        if (codexPanel != null)
        {
            codexPanel.SetActive(false);
        }

        // Hide shared detail panel by default
        if (sharedDetailPanel != null)
        {
            sharedDetailPanel.SetActive(false);
        }

        // Set up close button listener
        if (closeDetailPanelButton != null)
        {
            closeDetailPanelButton.onClick.AddListener(CloseDetailPanel);
        }

#if UNITY_EDITOR
        // Editor-only: Unlock all codex entries if enabled
        if (unlockAllCodexEntriesInEditor)
        {
            UnlockAllCodexEntries();
        }
#endif
    }

    /// <summary>
    /// Show the codex panel and populate it with unlocked levels
    /// </summary>
    public void ShowCodex()
    {
        if (codexPanel != null)
        {
            codexPanel.SetActive(true);
        }

        PopulateCodex();

        // Start scroll-up animation after a brief delay to ensure UI is laid out
        AnimateScrollUp();
    }

    /// <summary>
    /// Animate the scroll container scaling up from small to full size
    /// </summary>
    private void AnimateScrollUp()
    {
        if (scrollContainer == null) return;

        // Stop any existing animation
        if (scrollAnimationCoroutine != null)
        {
            StopCoroutine(scrollAnimationCoroutine);
        }

        // Start the animation coroutine
        scrollAnimationCoroutine = StartCoroutine(ScrollAnimationCoroutine(0f, 1f));
    }

    /// <summary>
    /// Hide the codex panel with scroll-down animation
    /// </summary>
    /// <param name="onComplete">Callback invoked when the animation completes</param>
    public void HideCodex(System.Action onComplete = null)
    {
        // Start scroll-down animation and hide after it completes
        StartCoroutine(HideCodexWithAnimation(onComplete));
    }

    /// <summary>
    /// Coroutine that scrolls down the codex and then hides it
    /// </summary>
    private IEnumerator HideCodexWithAnimation(System.Action onComplete)
    {
        // Deselect any selected entry
        DeselectCurrentEntry();

        // Hide shared detail panel immediately
        if (sharedDetailPanel != null)
        {
            sharedDetailPanel.SetActive(false);
        }

        // Animate scale down from full size to small
        if (scrollContainer != null)
        {
            scrollAnimationCoroutine = StartCoroutine(ScrollAnimationCoroutine(1f, 0f));
            yield return scrollAnimationCoroutine;
        }

        // Hide the panel after animation completes
        if (codexPanel != null)
        {
            codexPanel.SetActive(false);
        }

        // Invoke callback if provided
        onComplete?.Invoke();
    }

    private IEnumerator ScrollAnimationCoroutine(float yStartScale, float yEndScale)
    {
        if (scrollContainer == null) yield break;

        float elapsedTime = 0f;
        var startScale = scrollContainer.localScale;
        startScale.y = yStartScale;
        scrollContainer.localScale = startScale;
        var newScale = startScale;
        while (elapsedTime < scrollUpAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / scrollUpAnimationDuration;
            float curveValue = scrollAnimationCurve.Evaluate(normalizedTime);

            float currentScale = Mathf.Lerp(yStartScale, yEndScale, curveValue);
            newScale.y = currentScale;
            scrollContainer.localScale = newScale;
            yield return null;
        }
        scrollContainer.localScale = newScale;

        // Reset scroll position to top when scroll-up animation completes
        if (yEndScale >= 1f && entriesScrollRect != null)
        {
            entriesScrollRect.verticalNormalizedPosition = 1f;
        }

        scrollAnimationCoroutine = null;
    }

    /// <summary>
    /// Populate the codex with entries for all unlocked levels
    /// </summary>
    private void PopulateCodex()
    {
        // Clear existing entries
        ClearEntries();

        // Validate requirements
        if (levelManager == null)
        {
            Debug.LogWarning("CodexManager: Cannot populate codex without LevelManager");
            ShowEmptyState(true);
            return;
        }

        if (codexEntryPrefab == null)
        {
            Debug.LogError("CodexManager: Codex entry prefab is not assigned!");
            ShowEmptyState(true);
            return;
        }

        if (codexEntryContainer == null)
        {
            Debug.LogError("CodexManager: Codex entry container is not assigned!");
            ShowEmptyState(true);
            return;
        }

        // Get all levels
        List<LevelData> allLevels = levelManager.GetAllLevels();
        int totalCount = 0;
        int completedCount = 0;

        // Create an entry for each level (both completed and incomplete)
        foreach (LevelData levelData in allLevels)
        {
            if (levelData == null) continue;

            var levelNumber = levelData.levelNumber;

            // Check if level is completed (has stars or high score)
            int stars = levelManager.GetLevelStars(levelNumber);
            int highScore = levelManager.GetLevelHighScore(levelNumber);
            bool isCompleted = stars > 0 || highScore > 0;

            if (isCompleted)
            {
                completedCount++;
            }

            SpawnCodexEntry(levelData, isCompleted);
            totalCount++;
        }

        // Show empty state if no levels at all
        ShowEmptyState(totalCount == 0);

        Debug.Log($"CodexManager: Populated codex with {totalCount} levels ({completedCount} completed)");
    }

    /// <summary>
    /// Spawn a single codex entry for a level
    /// </summary>
    private void SpawnCodexEntry(LevelData levelData, bool isCompleted)
    {
        // Instantiate the entry
        GameObject entryObj = Instantiate(codexEntryPrefab, codexEntryContainer);

        // Get the CodexEntryUI component
        CodexEntryUI entryUI = entryObj.GetComponent<CodexEntryUI>();
        if (entryUI == null)
        {
            Debug.LogError("CodexManager: Codex entry prefab is missing CodexEntryUI component!");
            Destroy(entryObj);
            return;
        }

        // Get level progress data
        int levelNumber = levelData.levelNumber;
        int highScore = levelManager.GetLevelHighScore(levelNumber);

        // Initialize the entry with completion status
        entryUI.Initialize(levelData, highScore, isCompleted);

        // Add click listener
        entryUI.AddClickListener(OnEntryClicked);

        // Track the spawned entry
        spawnedEntries.Add(entryUI);
    }

    /// <summary>
    /// Clear all spawned codex entries
    /// </summary>
    private void ClearEntries()
    {
        foreach (var entry in spawnedEntries)
        {
            if (entry != null)
            {
                entry.ClearClickListeners();
                Destroy(entry.gameObject);
            }
        }

        spawnedEntries.Clear();
        currentlySelectedEntry = null;
    }

    /// <summary>
    /// Handle when a codex entry is clicked
    /// </summary>
    private void OnEntryClicked(CodexEntryUI clickedEntry)
    {
        if (clickedEntry == null) return;

        // If clicking the same entry, deselect it
        if (currentlySelectedEntry == clickedEntry)
        {
            DeselectCurrentEntry();
            return;
        }

        // Play codex selection sound effect
        if (soundManager != null)
        {
            soundManager.PlayCodexSelect();
        }

        // Deselect previous entry
        if (currentlySelectedEntry != null)
        {
            currentlySelectedEntry.SetSelected(false);
        }

        // Select the new entry
        currentlySelectedEntry = clickedEntry;
        currentlySelectedEntry.SetSelected(true);

        // Update the shared detail panel with the selected entry's data
        UpdateSharedDetailPanel(clickedEntry);

        // Reset detail panel scroll position to top when selecting a new entry
        if (detailPanelScrollRect != null)
        {
            detailPanelScrollRect.verticalNormalizedPosition = 1f;
        }

        Debug.Log($"CodexManager: Selected {clickedEntry.GetLevelData().levelName}");

        // Note: ScrollToEntry removed - scrolling is handled by the ScrollRect component in the UI
    }

    /// <summary>
    /// Deselect the currently selected entry
    /// </summary>
    private void DeselectCurrentEntry()
    {
        if (currentlySelectedEntry != null)
        {
            currentlySelectedEntry.SetSelected(false);
            currentlySelectedEntry = null;
        }

        // Hide the shared detail panel
        if (sharedDetailPanel != null)
        {
            sharedDetailPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Close the detail panel (called by close button)
    /// </summary>
    public void CloseDetailPanel()
    {
        // Play codex close sound effect
        if (soundManager != null)
        {
            soundManager.PlayCodexClose();
        }

        DeselectCurrentEntry();
    }


    /// <summary>
    /// Show or hide the empty state panel
    /// </summary>
    private void ShowEmptyState(bool show)
    {
        if (emptyStatePanel != null)
        {
            emptyStatePanel.SetActive(show);
        }
    }

    /// <summary>
    /// Refresh the codex (useful after completing a level)
    /// </summary>
    public void RefreshCodex()
    {
        if (codexPanel != null && codexPanel.activeSelf)
        {
            PopulateCodex();
        }
    }

    /// <summary>
    /// Update the shared detail panel with data from the selected entry
    /// </summary>
    private void UpdateSharedDetailPanel(CodexEntryUI selectedEntry)
    {
        if (selectedEntry == null || sharedDetailPanel == null)
        {
            return;
        }

        LevelData levelData = selectedEntry.GetLevelData();
        if (levelData == null)
        {
            return;
        }

        // Show the shared detail panel
        sharedDetailPanel.SetActive(true);

        // Get level completion status
        int levelNumber = selectedEntry.GetLevelNumber();
        int highScore = levelManager != null ? levelManager.GetLevelHighScore(levelNumber) : 0;
        int starsEarned = levelManager != null ? levelManager.GetLevelStars(levelNumber) : 0;
        bool isCompleted = starsEarned > 0 || highScore > 0;

        // Update level name - show "????" if not completed
        if (detailLevelNameText != null)
        {
            detailLevelNameText.text = isCompleted ? levelData.levelName : "????";
        }

        // Update location - hide if not completed
        if (detailLocationText != null)
        {
            if (isCompleted)
            {
                detailLocationText.text = string.IsNullOrEmpty(levelData.location)
                    ? "Unknown Location"
                    : levelData.location;
            }
            else
            {
                detailLocationText.text = "????";
            }
        }

        // Update description - hide if not completed
        if (detailDescriptionText != null)
        {
            if (isCompleted)
            {
                detailDescriptionText.text = string.IsNullOrEmpty(levelData.levelDescription)
                    ? "No description available."
                    : levelData.levelDescription;
            }
            else
            {
                detailDescriptionText.text = "Complete this level to reveal its secrets...";
            }
        }

        // Update site image
        if (detailSiteImage != null)
        {
            if (isCompleted && levelData.siteImage != null)
            {
                detailSiteImage.sprite = levelData.siteImage;
                detailSiteImage.gameObject.SetActive(true);
            }
            else
            {
                detailSiteImage.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        // Stop any running animations
        if (scrollAnimationCoroutine != null)
        {
            StopCoroutine(scrollAnimationCoroutine);
            scrollAnimationCoroutine = null;
        }

        // Unregister from DependencyRegistry
        DependencyRegistry.Unregister<CodexManager>(this);

        // Clean up button listeners
        if (closeDetailPanelButton != null)
        {
            closeDetailPanelButton.onClick.RemoveAllListeners();
        }

        // Clean up spawned entries
        ClearEntries();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only: Unlock all codex entries for testing
    /// Also gives each level at least 1 star so they show as completed in the codex
    /// </summary>
    [ContextMenu("Unlock All Codex Entries (Editor Only)")]
    public void UnlockAllCodexEntries()
    {
        if (!Application.isEditor)
        {
            Debug.LogWarning("UnlockAllCodexEntries can only be called in the Unity Editor!");
            return;
        }

        if (levelManager == null)
        {
            levelManager = DependencyRegistry.Find<LevelManager>();
            if (levelManager == null)
            {
                Debug.LogError("CodexManager: Cannot unlock codex entries without LevelManager!");
                return;
            }
        }

        List<LevelData> allLevels = levelManager.GetAllLevels();
        int unlockedCount = 0;
        int completedCount = 0;

        foreach (LevelData level in allLevels)
        {
            if (level == null) continue;

            int levelNumber = level.levelNumber;
            
            // Mark codex as unlocked
            PlayerPrefs.SetInt($"Level_{levelNumber}_CodexUnlocked", 1);
            unlockedCount++;

            // Give level at least 1 star so it shows as completed in codex
            int currentStars = levelManager.GetLevelStars(levelNumber);
            if (currentStars == 0)
            {
                PlayerPrefs.SetInt($"Level_{levelNumber}_Stars", 1);
                completedCount++;
            }
        }

        PlayerPrefs.Save();
        
        // Reload progress in LevelManager to reflect the changes immediately
        // Using reflection to call private LoadProgress method (editor-only, so safe)
        var loadProgressMethod = levelManager.GetType().GetMethod("LoadProgress", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        loadProgressMethod?.Invoke(levelManager, null);
        
        Debug.Log($"CodexManager (Editor): Unlocked all {unlockedCount} codex entries and marked {completedCount} levels as completed!");
    }
#endif
}

