using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Codex panel, which displays information about unlocked levels
/// </summary>
public class CodexManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject codexPanel;
    [SerializeField] private Transform codexEntryContainer;
    [SerializeField] private GameObject codexEntryPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Shared Detail Panel")]
    [SerializeField] private GameObject sharedDetailPanel;
    [SerializeField] private Button closeDetailPanelButton;
    [SerializeField] private TextMeshProUGUI detailLevelNameText;
    [SerializeField] private TextMeshProUGUI detailLocationText;
    [SerializeField] private TextMeshProUGUI detailDescriptionText;
    [SerializeField] private TextMeshProUGUI detailRequiredHeightText;
    [SerializeField] private TextMeshProUGUI detailHighScoreText;
    [SerializeField] private Transform detailStarContainer;
    [SerializeField] private GameObject detailStarIconPrefab;

    [Header("Empty State")]
    [SerializeField] private GameObject emptyStatePanel;
    [SerializeField] private string emptyStateMessage = "No levels unlocked yet. Play levels to unlock entries in the Codex!";

    // References (found via DependencyRegistry)
    private LevelManager levelManager;

    [Header("Detail Panel Visual States")]
    [SerializeField] private Color starObtainedColor = Color.white;
    [SerializeField] private Color starNotObtainedColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    // State
    private List<CodexEntryUI> spawnedEntries = new List<CodexEntryUI>();
    private CodexEntryUI currentlySelectedEntry = null;
    private GameObject[] detailStarIcons = new GameObject[3];
    private const int MAX_STARS = 3;

    private void Awake()
    {
        // Register with DependencyRegistry
        DependencyRegistry.Register<CodexManager>(this);
    }

    private void Start()
    {
        // Find dependencies
        levelManager = DependencyRegistry.Find<LevelManager>();

        if (levelManager == null)
        {
            Debug.LogWarning("CodexManager: LevelManager not found!");
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

        // Initialize detail panel star icons
        InitializeDetailPanelStars();
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
    }

    /// <summary>
    /// Hide the codex panel
    /// </summary>
    public void HideCodex()
    {
        if (codexPanel != null)
        {
            codexPanel.SetActive(false);
        }

        // Deselect any selected entry
        DeselectCurrentEntry();

        // Hide shared detail panel
        if (sharedDetailPanel != null)
        {
            sharedDetailPanel.SetActive(false);
        }
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

        Debug.Log($"CodexManager: Selected {clickedEntry.GetLevelData().levelName}");

        // Scroll to the selected entry if scroll rect exists
        ScrollToEntry(clickedEntry);
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
        DeselectCurrentEntry();
    }

    /// <summary>
    /// Scroll to a specific entry in the scroll view
    /// </summary>
    private void ScrollToEntry(CodexEntryUI entry)
    {
        if (scrollRect == null || entry == null) return;

        // Calculate the position to scroll to
        RectTransform entryRect = entry.GetComponent<RectTransform>();
        RectTransform contentRect = codexEntryContainer.GetComponent<RectTransform>();

        if (entryRect == null || contentRect == null) return;

        // Calculate normalized position (0 = bottom, 1 = top)
        float entryY = entryRect.anchoredPosition.y;
        float contentHeight = contentRect.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;

        if (contentHeight > viewportHeight)
        {
            float normalizedPosition = Mathf.Clamp01(
                (contentHeight - Mathf.Abs(entryY)) / (contentHeight - viewportHeight)
            );

            scrollRect.verticalNormalizedPosition = normalizedPosition;
        }
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
    /// Initialize the star icons in the shared detail panel
    /// </summary>
    private void InitializeDetailPanelStars()
    {
        // Clear any existing stars
        ClearDetailPanelStars();

        // Validate requirements
        if (detailStarIconPrefab == null || detailStarContainer == null)
        {
            return;
        }

        // Spawn 3 stars in the container
        for (int i = 0; i < MAX_STARS; i++)
        {
            GameObject starIcon = Instantiate(detailStarIconPrefab, detailStarContainer);
            detailStarIcons[i] = starIcon;
        }
    }

    /// <summary>
    /// Clear all spawned star icons in the detail panel
    /// </summary>
    private void ClearDetailPanelStars()
    {
        for (int i = 0; i < detailStarIcons.Length; i++)
        {
            if (detailStarIcons[i] != null)
            {
                Destroy(detailStarIcons[i]);
                detailStarIcons[i] = null;
            }
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

        // Get level stats
        int levelNumber = selectedEntry.GetLevelNumber();
        int starsEarned = levelManager != null ? levelManager.GetLevelStars(levelNumber) : 0;
        int highScore = levelManager != null ? levelManager.GetLevelHighScore(levelNumber) : 0;
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

        // Update required height - always show this
        if (detailRequiredHeightText != null)
        {
            detailRequiredHeightText.text = $"Required Height: {levelData.requiredStackHeight}";
        }

        // Update high score
        if (detailHighScoreText != null)
        {
            detailHighScoreText.text = highScore > 0
                ? $"High Score: {highScore}"
                : "Not yet completed";
        }

        // Update stars display
        UpdateDetailPanelStars(starsEarned);
    }

    /// <summary>
    /// Update the star icons in the detail panel based on stars earned
    /// </summary>
    private void UpdateDetailPanelStars(int starsEarned)
    {
        starsEarned = Mathf.Clamp(starsEarned, 0, MAX_STARS);

        for (int i = 0; i < detailStarIcons.Length; i++)
        {
            if (detailStarIcons[i] != null)
            {
                Image starImage = detailStarIcons[i].GetComponent<Image>();
                if (starImage != null)
                {
                    // Set color based on whether star is earned
                    starImage.color = i < starsEarned ? starObtainedColor : starNotObtainedColor;
                }

                detailStarIcons[i].SetActive(true);
            }
        }
    }

    private void OnDestroy()
    {
        // Unregister from DependencyRegistry
        DependencyRegistry.Unregister<CodexManager>(this);

        // Clean up button listeners
        if (closeDetailPanelButton != null)
        {
            closeDetailPanelButton.onClick.RemoveAllListeners();
        }

        // Clean up spawned entries
        ClearEntries();

        // Clean up detail panel stars
        ClearDetailPanelStars();
    }
}

