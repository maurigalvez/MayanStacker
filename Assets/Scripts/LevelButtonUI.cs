using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the appearance and data for a single level selection button
/// </summary>
public class LevelButtonUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI levelNumberText;
    [SerializeField] private GameObject starIconPrefab;
    [SerializeField] private RectTransform starContainer;
    [SerializeField] private Vector2 starContainerCompletedPosition;
    [SerializeField] private Vector2 starContainerUnlockedPosition;
    [SerializeField] private Transform[] starPositions; // Array of 3 transforms for star positions

    [Header("Visual States")]
    [SerializeField] private Image visualStateImage;
    [SerializeField] private Vector2 visualStateCompletedPosition;
    [SerializeField] private Vector2 visualStateNotCompletedPosition;
    [SerializeField] private Vector2 unlockedSize;
    [SerializeField] private Sprite unlockedSprite;
    [SerializeField] private Vector2 lockedSize;
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private Vector2 completedSize;
    [SerializeField] private Sprite completedSprite;

    [Header("Colors")]
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] private Color completedColor = new Color(1f, 0.9f, 0.6f, 1f);
    [SerializeField] private Color starObtainedColor = Color.white;
    [SerializeField] private Color starNotObtainedColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("Pulse Animation")]
    [SerializeField] private float pulseMinScale = 0.9f;
    [SerializeField] private float pulseMaxScale = 1.15f;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private Color pulseColor = new Color(1f, 0.8f, 0.2f, 1f); // Gold/yellow highlight color
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private float pulseColorIntensity = 0.5f; // How much the color pulses (0-1)

    [Header("Current Level Indicator")]
    [SerializeField] private GameObject currentLevelIndicator; // Optional "Current Level" text or icon
    [SerializeField] private Image highlightBorder; // Optional border/glow effect
    [SerializeField] private Color highlightBorderColor = new Color(1f, 0.8f, 0.2f, 0.8f);
    [SerializeField] private float highlightBorderWidth = 3f;
    [SerializeField] private float arrowAnimationDistance = 10f; // How far the arrow moves up/down
    [SerializeField] private float arrowAnimationSpeed = 3f; // Speed of the arrow animation

    // Level data
    private int levelIndex;
    private int levelNumber;
    private bool isUnlocked;
    private int starsEarned;

    // Animation state
    private bool isPulsing = false;
    private Vector3 originalScale;
    private Vector3 baseScale; // Base scale accounting for button state (full or half for unlocked)
    private float pulseTimer = 0f;
    private Color originalTextColor;
    private Color originalBackgroundColor;
    private float originalFontSize;
    private Vector3 originalIndicatorPosition;
    private float arrowAnimationTimer = 0f;

    // Spawned UI elements
    private List<GameObject> spawnedStarIcons = new List<GameObject>();
    private const int MAX_STARS = 3;

    private void Awake()
    {
        // Get button reference if not assigned
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        // Store original scale
        originalScale = transform.localScale;
        baseScale = originalScale; // Initialize base scale (will be updated in UpdateAppearance)

        // Store original colors and font size
        if (levelNumberText != null)
        {
            originalTextColor = levelNumberText.color;
            originalFontSize = levelNumberText.fontSize;
        }
        if (backgroundImage != null)
        {
            originalBackgroundColor = backgroundImage.color;
        }

        // Spawn star icons
        SpawnStarIcons();

        // Hide current level indicator initially and store original position
        if (currentLevelIndicator != null)
        {
            originalIndicatorPosition = currentLevelIndicator.transform.localPosition;
            currentLevelIndicator.SetActive(false);
        }

        // Hide highlight border initially
        if (highlightBorder != null)
        {
            highlightBorder.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Handle pulse animation
        if (isPulsing)
        {
            pulseTimer += Time.deltaTime * pulseSpeed;
            float normalizedValue = (Mathf.Sin(pulseTimer) + 1f) / 2f; // 0 to 1
            float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, normalizedValue);
            transform.localScale = baseScale * scale;

            // Pulse color effect on text
            if (levelNumberText != null)
            {
                Color targetColor = Color.Lerp(normalTextColor, pulseColor, normalizedValue * pulseColorIntensity);
                levelNumberText.color = targetColor;
            }

            // Pulse color effect on background/border
            if (highlightBorder != null && highlightBorder.gameObject.activeSelf)
            {
                float alpha = 0.5f + (normalizedValue * 0.5f); // Pulse between 0.5 and 1.0 alpha
                Color borderColor = highlightBorderColor;
                borderColor.a = alpha;
                highlightBorder.color = borderColor;
            }

            // Animate arrow indicator up and down
            if (currentLevelIndicator != null && currentLevelIndicator.activeSelf)
            {
                arrowAnimationTimer += Time.deltaTime * arrowAnimationSpeed;
                float verticalOffset = Mathf.Sin(arrowAnimationTimer) * arrowAnimationDistance;
                Vector3 newPosition = originalIndicatorPosition;
                newPosition.y += verticalOffset;
                currentLevelIndicator.transform.localPosition = newPosition;
            }
        }
    }

    /// <summary>
    /// Spawn the star icon UI elements
    /// </summary>
    private void SpawnStarIcons()
    {
        // Clear any existing stars
        ClearStarIcons();

        // Validate requirements
        if (starIconPrefab == null)
        {
            Debug.LogWarning("Star icon prefab is not assigned on LevelButtonUI!");
            return;
        }

        if (starPositions == null || starPositions.Length == 0)
        {
            Debug.LogWarning("Star positions are not assigned on LevelButtonUI!");
            return;
        }

        // Spawn a star at each defined position
        for (int i = 0; i < starPositions.Length && i < MAX_STARS; i++)
        {
            if (starPositions[i] != null)
            {
                GameObject starIcon = Instantiate(starIconPrefab, starPositions[i]);
                spawnedStarIcons.Add(starIcon);
            }
            else
            {
                Debug.LogWarning($"Star position {i} is null on LevelButtonUI!");
                // Add null placeholder to keep indices aligned
                spawnedStarIcons.Add(null);
            }
        }
    }

    /// <summary>
    /// Clear all spawned star icons
    /// </summary>
    private void ClearStarIcons()
    {
        foreach (var star in spawnedStarIcons)
        {
            if (star != null)
            {
                Destroy(star);
            }
        }

        spawnedStarIcons.Clear();
    }

    /// <summary>
    /// Initialize the level button with data
    /// </summary>
    /// <param name="levelIndex">Zero-based level index</param>
    /// <param name="levelNumber">Display level number (1-based)</param>
    /// <param name="isUnlocked">Whether the level is unlocked</param>
    /// <param name="stars">Number of stars earned (0-3)</param>
    public void Initialize(int levelIndex, int levelNumber, bool isUnlocked, int stars)
    {
        this.levelIndex = levelIndex;
        this.levelNumber = levelNumber;
        this.isUnlocked = isUnlocked;
        this.starsEarned = Mathf.Clamp(stars, 0, 3);

        UpdateAppearance();
    }

    /// <summary>
    /// Update the visual appearance of the button based on its state
    /// </summary>
    private void UpdateAppearance()
    {
        // Update button interactability
        if (button != null)
        {
            button.interactable = isUnlocked;
        }

        // Update button scale: highlighted levels are full scale, completed levels are 60%, others are 50%
        if (isPulsing)
        {
            // Highlighted/pulsing - full scale
            baseScale = originalScale;
            transform.localScale = baseScale;
        }
        else if (starsEarned > 0)
        {
            // Completed but not highlighted - 60% scale
            baseScale = originalScale * 0.6f;
            transform.localScale = baseScale;
        }
        else
        {
            // Not highlighted and not completed - half scale (for locked and unlocked but not completed)
            baseScale = originalScale * 0.5f;
            transform.localScale = baseScale;
        }

        // Update background sprite and color
        if (backgroundImage != null)
        {
            visualStateImage.rectTransform.localPosition = visualStateNotCompletedPosition;
            if (!isUnlocked)
            {
                // Locked state
                visualStateImage.sprite = lockedSprite;
                visualStateImage.rectTransform.sizeDelta = lockedSize;
            }
            else if (starsEarned > 0)
            {
                visualStateImage.rectTransform.localPosition = visualStateCompletedPosition;
                starContainer.localPosition = starContainerCompletedPosition;
                // Completed state
                visualStateImage.sprite = completedSprite != null ? completedSprite : unlockedSprite;
                visualStateImage.rectTransform.sizeDelta = completedSize;
            }
            else
            {
                starContainer.localPosition = starContainerUnlockedPosition;
                // Unlocked but not completed
                visualStateImage.sprite = unlockedSprite;
                visualStateImage.rectTransform.sizeDelta = unlockedSize;
            }
        }

        // Update level number text
        if (levelNumberText != null)
        {
            levelNumberText.text = levelNumber.ToString();
            if (isUnlocked)
            {
                levelNumberText.color = Color.white;
            }
            else
            {
                levelNumberText.color = lockedColor;
            }
        }

        // Update stars display
        UpdateStarsDisplay();
    }

    /// <summary>
    /// Update the star icons based on stars earned
    /// </summary>
    private void UpdateStarsDisplay()
    {
        if (spawnedStarIcons == null || spawnedStarIcons.Count == 0) return;

        // Only show stars if the level is completed (has at least 1 star)
        bool shouldShowStars = starsEarned > 0;

        for (int i = 0; i < spawnedStarIcons.Count; i++)
        {
            if (spawnedStarIcons[i] != null)
            {
                // Hide stars if level is not completed
                if (!shouldShowStars)
                {
                    spawnedStarIcons[i].SetActive(false);
                    continue;
                }

                // Show stars for completed levels
                spawnedStarIcons[i].SetActive(true);

                // Get the Image component on the star
                Image starImage = spawnedStarIcons[i].GetComponent<Image>();
                if (starImage != null)
                {
                    // Set color based on whether star is earned
                    if (i < starsEarned)
                    {
                        // Star is obtained - white color
                        starImage.color = starObtainedColor;
                    }
                    else
                    {
                        // Star not obtained - grey color
                        starImage.color = starNotObtainedColor;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Add a click listener to the button
    /// </summary>
    public void AddClickListener(System.Action<int> onClick)
    {
        if (button != null)
        {
            button.onClick.AddListener(() => onClick?.Invoke(levelIndex));
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
    /// Update the button's state (useful for refreshing after progress)
    /// </summary>
    public void UpdateState(bool isUnlocked, int stars)
    {
        this.isUnlocked = isUnlocked;
        this.starsEarned = Mathf.Clamp(stars, 0, 3);
        UpdateAppearance();
    }

    /// <summary>
    /// Start the pulse animation to highlight this level button
    /// </summary>
    public void StartPulseAnimation()
    {
        isPulsing = true;
        pulseTimer = 0f;
        arrowAnimationTimer = 0f;

        // Update base scale to full scale for highlighted level
        baseScale = originalScale;

        // Show current level indicator and reset position
        if (currentLevelIndicator != null)
        {
            currentLevelIndicator.transform.localPosition = originalIndicatorPosition;
            currentLevelIndicator.SetActive(true);
        }

        // Show and configure highlight border
        if (highlightBorder != null)
        {
            highlightBorder.gameObject.SetActive(true);
            highlightBorder.color = highlightBorderColor;

            // Make the border slightly larger than the button
            RectTransform borderRect = highlightBorder.rectTransform;
            RectTransform buttonRect = GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                borderRect.sizeDelta = buttonRect.sizeDelta + Vector2.one * highlightBorderWidth * 2f;
            }
        }

        // Make level number text larger and bolder
        if (levelNumberText != null)
        {
            originalTextColor = levelNumberText.color;
            // Increase font size by 20% when pulsing
            levelNumberText.fontSize = levelNumberText.fontSize * 1.2f;
            levelNumberText.fontStyle = FontStyles.Bold;
        }
    }

    /// <summary>
    /// Stop the pulse animation and reset scale to base scale
    /// </summary>
    public void StopPulseAnimation()
    {
        isPulsing = false;

        // Update base scale based on completion status when not highlighted
        if (starsEarned > 0)
        {
            // Completed - 60% scale
            baseScale = originalScale * 0.6f;
        }
        else
        {
            // Not completed - 50% scale
            baseScale = originalScale * 0.5f;
        }
        transform.localScale = baseScale;

        // Hide current level indicator and reset position
        if (currentLevelIndicator != null)
        {
            currentLevelIndicator.transform.localPosition = originalIndicatorPosition;
            currentLevelIndicator.SetActive(false);
        }

        // Reset arrow animation timer
        arrowAnimationTimer = 0f;

        // Hide highlight border
        if (highlightBorder != null)
        {
            highlightBorder.gameObject.SetActive(false);
        }

        // Reset text color and size
        if (levelNumberText != null)
        {
            levelNumberText.color = originalTextColor;
            // Reset font size to original
            levelNumberText.fontSize = originalFontSize;
            levelNumberText.fontStyle = FontStyles.Normal;
        }
    }

    private void OnDestroy()
    {
        // Clean up spawned star icons
        ClearStarIcons();
    }
}

