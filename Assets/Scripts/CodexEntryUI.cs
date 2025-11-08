using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the appearance and interaction for a single codex entry (level information)
/// </summary>
public class CodexEntryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI levelNameText;

    [Header("Visual States")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.6f, 1f);

    // Level data
    private LevelData levelData;
    private int levelNumber;
    private int highScore;
    private bool isSelected = false;
    private bool isCompleted = false;

    private void Awake()
    {
        // Get button reference if not assigned
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    /// <summary>
    /// Initialize the codex entry with level data
    /// </summary>
    public void Initialize(LevelData levelData, int highScore, bool isCompleted)
    {
        this.levelData = levelData;
        this.levelNumber = levelData.levelNumber;
        this.highScore = highScore;
        this.isCompleted = isCompleted;

        UpdateAppearance();
    }

    /// <summary>
    /// Update the visual appearance based on data
    /// </summary>
    private void UpdateAppearance()
    {
        if (levelData == null) return;

        // Update main button display
        if (levelNameText != null)
        {
            // Show "????" if level not completed, otherwise show the level name
            string displayName = isCompleted ? levelData.levelName : "????";
            levelNameText.text = $"{levelNumber}. {displayName}";
        }

        // Update background color based on selection
        if (backgroundImage != null)
        {
            backgroundImage.color = isSelected ? selectedColor : normalColor;
        }
    }

    /// <summary>
    /// Set the selected state of this entry
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateAppearance();
    }

    /// <summary>
    /// Add a click listener to the button
    /// </summary>
    public void AddClickListener(System.Action<CodexEntryUI> onClick)
    {
        if (button != null)
        {
            button.onClick.AddListener(() => onClick?.Invoke(this));
        }
    }

    /// <summary>
    /// Remove all click listeners
    /// </summary>
    public void ClearClickListeners()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// Get the level data associated with this entry
    /// </summary>
    public LevelData GetLevelData()
    {
        return levelData;
    }

    /// <summary>
    /// Get the level number
    /// </summary>
    public int GetLevelNumber()
    {
        return levelNumber;
    }

    private void OnDestroy()
    {
        // Clear button listeners
        ClearClickListeners();
    }
}

