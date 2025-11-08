using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the visual progress bar that displays level completion progress
/// as players stack pieces toward the required height
/// </summary>
public class LevelProgressUI : MonoBehaviour
{
    [Header("Progress Bar")]
    [SerializeField] private GameObject progressBarPanel;
    [SerializeField] private Image progressBarFill;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI targetText;

    [Header("Visual Settings")]
    [SerializeField] private Color progressColorStart = new Color(1f, 0.3f, 0.3f); // Red
    [SerializeField] private Color progressColorMid = new Color(1f, 0.8f, 0f); // Yellow
    [SerializeField] private Color progressColorComplete = new Color(0.3f, 1f, 0.3f); // Green
    [SerializeField] private bool animateProgress = true;
    [SerializeField] private float animationSpeed = 5f;
    [SerializeField] private bool showPercentage = true;
    [SerializeField] private string progressFormat = "{0}/{1}";
    [SerializeField] private string targetFormat = "Goal: {0}";

    [Header("Visual Effects")]
    [SerializeField] private bool pulseOnProgress = true;
    [SerializeField] private float pulseScale = 1.1f;
    [SerializeField] private float pulseDuration = 0.3f;

    // References
    private LevelManager levelManager;
    private StackManager stackManager;
    private GameManager gameManager;

    // State
    private int currentHeight = 0;
    private int requiredHeight = 0;
    private float targetFillAmount = 0f;
    private float currentFillAmount = 0f;
    private bool isInitialized = false;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<LevelProgressUI>(this);
    }

    private void Start()
    {
        // Find dependencies
        levelManager = DependencyRegistry.Find<LevelManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        gameManager = DependencyRegistry.Find<GameManager>();

        // Subscribe to events
        if (levelManager != null)
        {
            levelManager.OnLevelLoaded += OnLevelLoaded;
            levelManager.OnStackHeightUpdated += OnStackHeightUpdated;
            levelManager.OnLevelCompleted += OnLevelCompleted;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }

        if (gameManager != null)
        {
            gameManager.OnGameRestart += OnGameRestart;
            gameManager.OnGameModeChanged += OnGameModeChanged;
        }

        // Initialize UI
        InitializeUI();
    }

    private void Update()
    {
        // Smoothly animate progress bar if enabled
        if (animateProgress && Mathf.Abs(currentFillAmount - targetFillAmount) > 0.001f)
        {
            currentFillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, Time.deltaTime * animationSpeed);
            UpdateProgressBar();
        }
    }

    /// <summary>
    /// Initialize the UI components
    /// </summary>
    private void InitializeUI()
    {
        // Hide progress bar initially
        if (progressBarPanel != null)
        {
            progressBarPanel.SetActive(false);
        }

        // Initialize progress bar
        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = 0f;
            currentFillAmount = 0f;
            targetFillAmount = 0f;
        }

        isInitialized = true;
    }

    /// <summary>
    /// Called when a level is loaded
    /// </summary>
    private void OnLevelLoaded(LevelData levelData)
    {
        if (levelData == null) return;

        // Reset state
        currentHeight = 0;
        requiredHeight = levelData.requiredStackHeight;
        targetFillAmount = 0f;
        currentFillAmount = 0f;

        // Show progress bar panel
        if (progressBarPanel != null)
        {
            progressBarPanel.SetActive(true);
        }

        // Update UI
        UpdateProgressBar();
        UpdateTargetText();

        Debug.Log($"LevelProgressUI: Loaded level {levelData.levelName} - Target: {requiredHeight}");
    }

    /// <summary>
    /// Called when stack height is updated
    /// </summary>
    private void OnStackHeightUpdated(int newHeight)
    {
        currentHeight = newHeight;
        UpdateProgress();
    }

    /// <summary>
    /// Called when an object is added to stack (alternative tracking)
    /// </summary>
    private void OnObjectAddedToStack(StackableObject stackableObject)
    {
        if (stackManager != null)
        {
            currentHeight = stackManager.GetStackCount();
            UpdateProgress();

            // Trigger pulse effect
            if (pulseOnProgress)
            {
                PulseProgressBar();
            }
        }
    }

    /// <summary>
    /// Update the progress calculation and UI
    /// </summary>
    private void UpdateProgress()
    {
        if (requiredHeight <= 0) return;

        // Calculate progress (0 to 1, clamped)
        float progress = Mathf.Clamp01((float)currentHeight / requiredHeight);
        targetFillAmount = progress;

        // Update immediately if animation is disabled
        if (!animateProgress)
        {
            currentFillAmount = targetFillAmount;
            UpdateProgressBar();
        }
    }

    /// <summary>
    /// Update the progress bar visual
    /// </summary>
    private void UpdateProgressBar()
    {
        if (progressBarFill == null) return;

        // Update fill amount
        progressBarFill.fillAmount = currentFillAmount;

        // Update color based on progress
        Color targetColor = GetProgressColor(currentFillAmount);
        progressBarFill.color = targetColor;

        // Update progress text
        UpdateProgressText();
    }

    /// <summary>
    /// Get the appropriate color based on progress
    /// </summary>
    private Color GetProgressColor(float progress)
    {
        if (progress < 0.5f)
        {
            // Interpolate between start and mid color (0 to 0.5)
            return Color.Lerp(progressColorStart, progressColorMid, progress * 2f);
        }
        else
        {
            // Interpolate between mid and complete color (0.5 to 1)
            return Color.Lerp(progressColorMid, progressColorComplete, (progress - 0.5f) * 2f);
        }
    }

    /// <summary>
    /// Update the progress text display
    /// </summary>
    private void UpdateProgressText()
    {
        if (progressText == null) return;

        if (showPercentage)
        {
            int percentage = Mathf.RoundToInt(currentFillAmount * 100f);
            progressText.text = $"{percentage}%";
        }
        else
        {
            progressText.text = string.Format(progressFormat, currentHeight, requiredHeight);
        }
    }

    /// <summary>
    /// Update the target/goal text
    /// </summary>
    private void UpdateTargetText()
    {
        if (targetText == null) return;

        targetText.text = string.Format(targetFormat, requiredHeight);
    }

    /// <summary>
    /// Pulse effect for progress bar
    /// </summary>
    private void PulseProgressBar()
    {
        if (progressBarFill == null) return;

        StopAllCoroutines();
        StartCoroutine(PulseCoroutine());
    }

    /// <summary>
    /// Coroutine for pulse animation
    /// </summary>
    private System.Collections.IEnumerator PulseCoroutine()
    {
        Transform progressTransform = progressBarFill.transform;
        Vector3 originalScale = progressTransform.localScale;
        Vector3 targetScale = originalScale * pulseScale;

        float elapsed = 0f;
        float halfDuration = pulseDuration / 2f;

        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            progressTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        elapsed = 0f;

        // Scale down
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            progressTransform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        // Ensure we end at original scale
        progressTransform.localScale = originalScale;
    }

    /// <summary>
    /// Called when level is completed
    /// </summary>
    private void OnLevelCompleted(int stars, int score)
    {
        // Ensure progress bar shows full
        targetFillAmount = 1f;
        currentFillAmount = 1f;
        UpdateProgressBar();
    }

    /// <summary>
    /// Called when game is restarted
    /// </summary>
    private void OnGameRestart()
    {
        // Reset progress
        currentHeight = 0;
        targetFillAmount = 0f;
        currentFillAmount = 0f;
        UpdateProgressBar();
    }

    /// <summary>
    /// Called when game mode changes
    /// </summary>
    private void OnGameModeChanged(GameMode newMode)
    {
        // Show/hide progress bar based on game mode
        if (progressBarPanel != null)
        {
            bool shouldShow = newMode == GameMode.StackerLevels;
            progressBarPanel.SetActive(shouldShow);
        }
    }

    /// <summary>
    /// Public method to manually set visibility
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (progressBarPanel != null)
        {
            progressBarPanel.SetActive(visible);
        }
    }

    /// <summary>
    /// Public method to get current progress (0-1)
    /// </summary>
    public float GetProgress()
    {
        return currentFillAmount;
    }

    /// <summary>
    /// Public method to get current height
    /// </summary>
    public int GetCurrentHeight()
    {
        return currentHeight;
    }

    /// <summary>
    /// Public method to get required height
    /// </summary>
    public int GetRequiredHeight()
    {
        return requiredHeight;
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<LevelProgressUI>(this);

        // Unsubscribe from events
        if (levelManager != null)
        {
            levelManager.OnLevelLoaded -= OnLevelLoaded;
            levelManager.OnStackHeightUpdated -= OnStackHeightUpdated;
            levelManager.OnLevelCompleted -= OnLevelCompleted;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }

        if (gameManager != null)
        {
            gameManager.OnGameRestart -= OnGameRestart;
            gameManager.OnGameModeChanged -= OnGameModeChanged;
        }
    }
}

