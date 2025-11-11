using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI highScoreText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI newHighScoreText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button gameOverMainMenuButton;

    [Header("Level Mode UI")]
    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private TextMeshProUGUI levelScoreText;
    [SerializeField] private Image[] stars; // Array of 3 star UI Images
    [SerializeField] private GameObject levelProgressDisplay; // Container for level progress UI (shown only in Level Mode)
    [SerializeField] private TextMeshProUGUI levelProgressText; // Shows "Height: X/Y"
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button retryLevelButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private TextMeshProUGUI gameModeText;

    [Header("Star Colors")]
    [SerializeField] private Color starInitialColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark gray
    [SerializeField] private Color starHighlightColor = new Color(1f, 0.84f, 0f, 1f); // Gold/yellow

    [Header("Game UI")]
    [SerializeField] private GameObject gameUI;
    [SerializeField] private TextMeshProUGUI instructionsText;
    [SerializeField] private GameObject stackHeightDisplay; // Container for stack height UI (shown only in Infinite Mode)
    [SerializeField] private TextMeshProUGUI stackHeightText;
    [SerializeField] private TextMeshProUGUI landingAccuracyText;
    [SerializeField] private GameObject pointsPopupPrefab;
    [SerializeField] private Transform pointsPopupParent;

    [Header("Combo UI")]
    [SerializeField] private GameObject comboDisplay;
    [SerializeField] private TextMeshProUGUI multiplierText;
    [SerializeField] private Image comboTimerBar; // Circular radial fill timer

    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button tryAgainButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button pauseMainMenuButton;

    [Header("Settings")]
    [SerializeField] private string scoreFormat = "Score: {0}";
    [SerializeField] private string highScoreFormat = "Best: {0}";
    [SerializeField] private string finalScoreFormat = "Final Score: {0}";
    [SerializeField] private string newHighScoreMessage = "NEW HIGH SCORE!";
    [SerializeField] private string stackHeightFormat = "Height: {0}";
    [SerializeField] private float landingAccuracyDisplayDuration = 2f;
    [SerializeField] private float pointsPopupDuration = 2f;
    [SerializeField] private float pointsPopupVerticalOffset = 30f;

    // PlayerPrefs key for tracking if player has seen instructions
    private const string INSTRUCTIONS_SEEN_KEY = "InfiniteMode_InstructionsSeen";

    [Header("Accuracy Text Colors")]
    [SerializeField] private Color perfectAccuracyColor = Color.green;
    [SerializeField] private Color goodAccuracyColor = Color.yellow;
    [SerializeField] private Color poorAccuracyColor = Color.red;

    [Header("Combo Settings")]
    [SerializeField]
    private Color[] comboMultiplierColors = new Color[]
    {
        Color.white,                           // 1x - no combo
        new Color(0.5f, 1f, 0.5f),            // 2x - light green
        new Color(0f, 1f, 0f),                // 3x - green
        new Color(1f, 0.84f, 0f),             // 4x - gold
        new Color(1f, 0.5f, 0f)               // 5x - orange
    };
    [SerializeField] private float comboScalePulse = 1.3f;
    [SerializeField] private float comboScalePulseDuration = 0.2f;
    [SerializeField] private bool showComboTimer = true;
    [SerializeField] private Color timerSafeColor = Color.green;
    [SerializeField] private Color timerWarningColor = Color.yellow;
    [SerializeField] private Color timerDangerColor = Color.red;

    // References
    private GameManager gameManager;
    private StackManager stackManager;
    private LevelManager levelManager;
    private GameSoundManager gameSoundManager;

    // State
    private Coroutine landingAccuracyCoroutine;
    private Coroutine comboPulseCoroutine;
    private bool isPaused = false;
    private bool isUpdatingComboTimer = false;

    // Events
    public System.Action OnGameResumed;
    public System.Action OnGamePaused;

    // Public properties
    public bool IsPaused => isPaused;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<UIManager>(this);
    }

    private void Start()
    {
        // Get references
        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        gameSoundManager = DependencyRegistry.Find<GameSoundManager>();

        // Subscribe to game events
        if (gameManager != null)
        {
            gameManager.OnScoreChanged += UpdateScore;
            gameManager.OnHighScoreChanged += UpdateHighScore;
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
            gameManager.OnGameModeChanged += OnGameModeChanged;
            gameManager.OnComboChanged += UpdateComboDisplay;
        }

        // Subscribe to stack events
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }

        // Subscribe to level events
        if (levelManager != null)
        {
            levelManager.OnLevelCompleted += OnLevelCompleted;
            levelManager.OnLevelFailed += OnLevelFailed;
            levelManager.OnLevelLoaded += OnLevelLoaded;
            levelManager.OnStackHeightUpdated += UpdateLevelProgress;
        }

        // Set up button listeners
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }

        if (gameOverMainMenuButton != null)
        {
            gameOverMainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(GoToNextLevel);
        }

        if (retryLevelButton != null)
        {
            retryLevelButton.onClick.AddListener(RetryLevel);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        // Set up pause menu button listeners
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(TogglePause);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (tryAgainButton != null)
        {
            tryAgainButton.onClick.AddListener(TryAgainFromPause);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (pauseMainMenuButton != null)
        {
            pauseMainMenuButton.onClick.AddListener(GoToMainMenuFromPause);
        }

        // Initialize UI
        InitializeUI();
    }

    private void InitializeUI()
    {
        // Show game UI, hide game over panel
        if (gameUI != null)
            gameUI.SetActive(true);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        // Update initial scores
        if (gameManager != null)
        {
            UpdateScore(gameManager.CurrentScore);
            UpdateHighScore(gameManager.HighScore);
            UpdateGameModeDisplay(gameManager.CurrentGameMode);
            UpdateComboDisplay(gameManager.CurrentCombo, gameManager.CurrentMultiplier);
        }

        // Initialize new UI elements
        UpdateStackHeight();
        HideLandingAccuracy();
        InitializeComboDisplay();
        InitializeComboTimer();

        // Hide instructions initially - they'll show when OnGameStart is called
        HideInstructions();

        // Initialize level UI if in level mode
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.StackerLevels)
        {
            InitializeLevelUI();
        }
    }

    private void UpdateScore(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = string.Format(scoreFormat, score);
        }
    }

    private void UpdateHighScore(int highScore)
    {
        if (highScoreText != null)
        {
            highScoreText.text = string.Format(highScoreFormat, highScore);
        }
    }

    private void OnGameStart()
    {
        // Hide game over panel, show game UI
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (gameUI != null)
            gameUI.SetActive(true);

        // Only show instructions for level 1 or first-time infinite mode players
        if (ShouldShowInstructions())
        {
            ShowInstructions();
            StartCoroutine(InstructionRoutine());
        }
    }

    private IEnumerator InstructionRoutine()
    {
        yield return new WaitForSeconds(3f);
        HideInstructions();

        // Mark that player has seen instructions in Infinite Mode
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.InfiniteStacker)
        {
            PlayerPrefs.SetInt(INSTRUCTIONS_SEEN_KEY, 1);
            PlayerPrefs.Save();
        }
    }

    private void OnGameOver()
    {
        // Show game over panel, hide game UI
        if (gameUI != null)
            gameUI.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // Update final score
        if (finalScoreText != null && gameManager != null)
        {
            finalScoreText.text = string.Format(finalScoreFormat, gameManager.CurrentScore);
        }

        // Check if it's a new high score
        if (gameManager != null && gameManager.CurrentScore >= gameManager.HighScore)
        {
            ShowNewHighScore();
        }
        else
        {
            HideNewHighScore();
        }
    }

    private void OnGameRestart()
    {
        // Hide game over panel
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // Reset stack height display to zero directly (don't query StackManager as it may not be cleared yet)
        if (stackHeightText != null)
        {
            stackHeightText.text = string.Format(stackHeightFormat, 0);
        }

        // Reset level progress if in level mode
        if (levelProgressText != null && levelProgressText.gameObject.activeSelf)
        {
            UpdateLevelProgress(0);
        }

        // Hide landing accuracy text
        HideLandingAccuracy();

        // Show instructions only if appropriate (level 1 or first-time infinite mode)
        if (ShouldShowInstructions())
        {
            ShowInstructions();
        }
        else
        {
            HideInstructions();
        }
    }

    /// <summary>
    /// Determines if instructions should be shown based on game mode and player experience
    /// </summary>
    private bool ShouldShowInstructions()
    {
        if (gameManager == null) return false;

        // In Level Mode: Only show for level 1
        if (gameManager.CurrentGameMode == GameMode.StackerLevels)
        {
            if (levelManager != null && levelManager.CurrentLevelIndex == 0)
            {
                return true; // Level 1 (index 0)
            }
            return false;
        }

        // In Infinite Mode: Only show if player hasn't seen instructions before
        if (gameManager.CurrentGameMode == GameMode.InfiniteStacker)
        {
            bool hasSeenInstructions = PlayerPrefs.GetInt(INSTRUCTIONS_SEEN_KEY, 0) == 1;
            return !hasSeenInstructions;
        }

        return false;
    }

    private void ShowInstructions()
    {
        if (instructionsText != null)
        {
            instructionsText.gameObject.SetActive(true);
            instructionsText.text = "Tap anywhere to drop the block!\nTry to stack them perfectly!";
        }
    }

    private void HideInstructions()
    {
        if (instructionsText != null)
        {
            instructionsText.gameObject.SetActive(false);
        }
    }

    private void UpdateStackHeight()
    {
        if (stackHeightText != null && stackManager != null)
        {
            int height = stackManager.GetStackCount();
            stackHeightText.text = string.Format(stackHeightFormat, height);
        }
    }

    private void OnObjectAddedToStack(StackableObject stackableObject)
    {
        // Update stack height when an object is added
        UpdateStackHeight();

        // Show landing accuracy feedback and points popup together
        if (stackableObject != null)
        {
            int basePoints = CalculatePointsFromAccuracy(stackableObject.LandingAccuracy);

            // Apply multiplier to get actual points awarded
            float multiplier = 1f;
            if (gameManager != null)
            {
                multiplier = gameManager.CurrentMultiplier;
            }
            int actualPoints = Mathf.RoundToInt(basePoints * multiplier);

            ShowLandingAccuracyAndPoints(stackableObject.LandingAccuracy, actualPoints, stackableObject.transform.position);
        }
    }

    private void ShowLandingAccuracyAndPoints(float accuracy, int points, Vector3 worldPosition)
    {
        if (landingAccuracyText == null) return;

        // Stop any existing coroutine
        if (landingAccuracyCoroutine != null)
        {
            StopCoroutine(landingAccuracyCoroutine);
        }

        // Get current combo from GameManager
        int currentCombo = 0;
        if (gameManager != null)
        {
            currentCombo = gameManager.CurrentCombo;
        }

        // Set the text based on accuracy with combo count
        string baseText = "";
        if (accuracy >= 0.9f)
        {
            baseText = "PERFECT!";
            landingAccuracyText.color = perfectAccuracyColor;
        }
        else if (accuracy >= 0.6f)
        {
            baseText = "GOOD";
            landingAccuracyText.color = goodAccuracyColor;
        }
        else
        {
            baseText = "POOR";
            landingAccuracyText.color = poorAccuracyColor;
        }

        // Add combo count if active (Good or Perfect landing)
        if (currentCombo > 0 && accuracy >= 0.6f)
        {
            landingAccuracyText.text = $"{baseText}\nx{currentCombo} COMBO";
        }
        else
        {
            landingAccuracyText.text = baseText;
        }

        // Convert world position to screen position for the accuracy label
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
        RectTransform accuracyRect = landingAccuracyText.GetComponent<RectTransform>();
        if (accuracyRect != null)
        {
            accuracyRect.position = screenPosition;
        }

        // Show the text
        landingAccuracyText.gameObject.SetActive(true);

        // Start coroutine to hide it after duration
        landingAccuracyCoroutine = StartCoroutine(HideLandingAccuracyAfterDelay());

        // Show points popup at the same location but slightly offset
        ShowPointsPopup(points, worldPosition);
    }

    private System.Collections.IEnumerator HideLandingAccuracyAfterDelay()
    {
        yield return new WaitForSeconds(landingAccuracyDisplayDuration);
        HideLandingAccuracy();
    }

    private void HideLandingAccuracy()
    {
        if (landingAccuracyText != null)
        {
            landingAccuracyText.gameObject.SetActive(false);
        }
    }

    private int CalculatePointsFromAccuracy(float accuracy)
    {
        if (accuracy >= 0.9f)
            return 100; // Perfect score
        else if (accuracy >= 0.6f)
            return 50;  // Good score
        else
            return 10;  // Poor score
    }

    private void ShowPointsPopup(int points, Vector3 worldPosition)
    {
        if (pointsPopupPrefab == null || pointsPopupParent == null) return;

        // Create the popup
        GameObject popup = Instantiate(pointsPopupPrefab, pointsPopupParent);

        // Set the text
        TextMeshProUGUI popupText = popup.GetComponentInChildren<TextMeshProUGUI>();
        if (popupText != null)
        {
            popupText.text = $"+{points}";

            // Color code based on base points (accounting for multiplier)
            // Perfect base = 100, Good base = 50, Poor base = 10
            if (points >= 90) // Perfect landing (100+ with multiplier or base perfect)
                popupText.color = perfectAccuracyColor;
            else if (points >= 45) // Good landing (50+ with multiplier or base good)
                popupText.color = goodAccuracyColor;
            else // Poor landing
                popupText.color = poorAccuracyColor;
        }

        // Convert world position to screen position with vertical offset to avoid overlap with accuracy label
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
        screenPosition.y += pointsPopupVerticalOffset; // Offset above the accuracy label
        RectTransform popupRect = popup.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.position = screenPosition;
        }

        // Start animation and destroy after duration
        StartCoroutine(AnimateAndDestroyPopup(popup));
    }

    private System.Collections.IEnumerator AnimateAndDestroyPopup(GameObject popup)
    {
        if (popup == null) yield break;

        RectTransform rectTransform = popup.GetComponent<RectTransform>();
        Vector3 startScale = Vector3.one;
        Vector3 endScale = Vector3.one * 1.5f;

        float elapsedTime = 0f;
        float animationDuration = 0.3f;

        // Scale up animation
        while (elapsedTime < animationDuration)
        {
            if (popup == null || rectTransform == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            rectTransform.localScale = Vector3.Lerp(startScale, endScale, progress);
            yield return null;
        }

        // Wait for remaining duration
        yield return new WaitForSeconds(pointsPopupDuration - animationDuration);

        // Scale down and fade out
        elapsedTime = 0f;
        Vector3 currentScale = rectTransform.localScale;
        Vector3 targetScale = Vector3.zero;

        while (elapsedTime < animationDuration && popup != null && rectTransform != null)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            rectTransform.localScale = Vector3.Lerp(currentScale, targetScale, progress);
            yield return null;
        }

        // Destroy the popup
        if (popup != null)
        {
            Destroy(popup);
        }
    }

    private void ShowNewHighScore()
    {
        if (newHighScoreText != null)
        {
            newHighScoreText.gameObject.SetActive(true);
            newHighScoreText.text = newHighScoreMessage;
        }
    }

    private void HideNewHighScore()
    {
        if (newHighScoreText != null)
        {
            newHighScoreText.gameObject.SetActive(false);
        }
    }

    // Combo UI Methods

    /// <summary>
    /// Initializes the combo display
    /// </summary>
    private void InitializeComboDisplay()
    {
        // Hide combo display initially (it will show when combo > 0)
        if (comboDisplay != null)
        {
            comboDisplay.SetActive(false);
        }
    }

    /// <summary>
    /// Initializes the combo timer display
    /// </summary>
    private void InitializeComboTimer()
    {
        if (comboTimerBar != null)
        {
            comboTimerBar.gameObject.SetActive(false);
            comboTimerBar.fillAmount = 1f; // Start full
        }
    }

    /// <summary>
    /// Updates the combo display with multiplier and timer
    /// </summary>
    private void UpdateComboDisplay(int combo, float multiplier)
    {
        // Show/hide combo display based on combo count and multiplier (only show when multiplier > 1)
        if (comboDisplay != null)
        {
            bool shouldShow = combo > 0 && multiplier > 1f;
            comboDisplay.SetActive(shouldShow);
        }

        // Update multiplier text with color coding (only show when multiplier > 1)
        if (multiplierText != null && multiplier > 1f)
        {
            // Format multiplier: show one decimal place if needed, otherwise show as integer
            string multiplierDisplay = (multiplier % 1 == 0) ? $"{multiplier:F0}x" : $"{multiplier:F1}x";
            multiplierText.text = multiplierDisplay;

            // Color code based on multiplier level (use floor for color index)
            int colorIndex = Mathf.Clamp(Mathf.FloorToInt(multiplier) - 1, 0, comboMultiplierColors.Length - 1);
            multiplierText.color = comboMultiplierColors[colorIndex];

            multiplierText.gameObject.SetActive(true);
        }
        else if (multiplierText != null)
        {
            multiplierText.gameObject.SetActive(false);
        }

        // Trigger pulse animation when combo increases (only when multiplier > 1)
        if (combo > 0 && multiplier > 1f)
        {
            TriggerComboPulse();

            // Start updating combo timer if not already
            if (!isUpdatingComboTimer)
            {
                StartCoroutine(UpdateComboTimerRoutine());
            }
        }
        else
        {
            // Hide timer when combo resets or multiplier is 1
            isUpdatingComboTimer = false;
            if (comboTimerBar != null)
            {
                comboTimerBar.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Updates the combo timer display continuously
    /// </summary>
    private IEnumerator UpdateComboTimerRoutine()
    {
        isUpdatingComboTimer = true;

        while (gameManager != null && gameManager.CurrentCombo > 0 && isUpdatingComboTimer)
        {
            float timeRemaining = gameManager.GetComboTimeRemaining();

            if (timeRemaining <= 0)
            {
                // Combo has decayed
                isUpdatingComboTimer = false;
                yield break;
            }

            UpdateComboTimerDisplay(timeRemaining);
            yield return null; // Update every frame for smooth timer
        }

        isUpdatingComboTimer = false;
    }

    /// <summary>
    /// Updates the circular timer bar display
    /// </summary>
    private void UpdateComboTimerDisplay(float timeRemaining)
    {
        if (!showComboTimer || comboTimerBar == null) return;

        // Show timer bar
        comboTimerBar.gameObject.SetActive(true);

        // Get decay time from GameManager for accurate calculation
        float decayTime = 3f; // Default, should match GameManager setting
        float fillAmount = timeRemaining / decayTime;

        // Update fill amount (1.0 = full circle, 0.0 = empty)
        comboTimerBar.fillAmount = fillAmount;

        // Color code the bar based on time remaining
        if (fillAmount > 0.5f)
        {
            comboTimerBar.color = timerSafeColor;
        }
        else if (fillAmount > 0.25f)
        {
            comboTimerBar.color = timerWarningColor;
        }
        else
        {
            comboTimerBar.color = timerDangerColor;
        }
    }

    /// <summary>
    /// Triggers a scale pulse animation on the combo display
    /// </summary>
    private void TriggerComboPulse()
    {
        if (comboDisplay == null) return;

        // Stop any existing pulse
        if (comboPulseCoroutine != null)
        {
            StopCoroutine(comboPulseCoroutine);
        }

        // Start new pulse
        comboPulseCoroutine = StartCoroutine(ComboPulseAnimation());
    }

    /// <summary>
    /// Animates a scale pulse on the combo display
    /// </summary>
    private IEnumerator ComboPulseAnimation()
    {
        RectTransform rectTransform = comboDisplay.GetComponent<RectTransform>();
        if (rectTransform == null) yield break;

        Vector3 originalScale = Vector3.one;
        Vector3 pulseScale = Vector3.one * comboScalePulse;

        float elapsedTime = 0f;

        // Scale up
        while (elapsedTime < comboScalePulseDuration / 2f)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / (comboScalePulseDuration / 2f);
            rectTransform.localScale = Vector3.Lerp(originalScale, pulseScale, progress);
            yield return null;
        }

        elapsedTime = 0f;

        // Scale back down
        while (elapsedTime < comboScalePulseDuration / 2f)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / (comboScalePulseDuration / 2f);
            rectTransform.localScale = Vector3.Lerp(pulseScale, originalScale, progress);
            yield return null;
        }

        // Ensure we end at original scale
        rectTransform.localScale = originalScale;
    }

    private void RestartGame()
    {
        // Play retry sound when restarting from game over
        if (gameSoundManager != null)
        {
            gameSoundManager.PlayRetryButtonSound();
            // Ensure music is playing (resumes if paused, starts if stopped)
            gameSoundManager.EnsureMusicPlaying();
        }

        if (gameManager != null)
        {
            gameManager.RestartGame();
        }
    }

    // Level Mode Methods

    private void InitializeLevelUI()
    {
        if (levelManager == null || levelManager.CurrentLevel == null) return;

        // Update level info
        if (levelNameText != null)
        {
            levelNameText.text = levelManager.CurrentLevel.levelName;
        }

        UpdateLevelProgress(stackManager?.GetStackCount() ?? 0);

        // Initialize stars to dark color
        InitializeStars();
    }

    /// <summary>
    /// Initializes all stars to the initial (dark) color
    /// </summary>
    private void InitializeStars()
    {
        if (stars == null || stars.Length == 0) return;

        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null)
            {
                stars[i].color = starInitialColor;
            }
        }
    }

    private void OnGameModeChanged(GameMode newMode)
    {
        UpdateGameModeDisplay(newMode);

        if (newMode == GameMode.StackerLevels)
        {
            InitializeLevelUI();
        }
    }

    private void UpdateGameModeDisplay(GameMode mode)
    {
        if (gameModeText != null)
        {
            gameModeText.text = mode == GameMode.InfiniteStacker ? "Infinite Mode" : "Level Mode";
        }

        // Show/hide level progress display based on mode (only shown in Level Mode)
        if (levelProgressDisplay != null)
        {
            levelProgressDisplay.SetActive(mode == GameMode.StackerLevels);
        }

        // Show/hide level progress text based on mode (kept for backward compatibility)
        if (levelProgressText != null)
        {
            levelProgressText.gameObject.SetActive(mode == GameMode.StackerLevels);
        }

        // Show/hide stack height display based on mode (only shown in Infinite Mode)
        if (stackHeightDisplay != null)
        {
            stackHeightDisplay.SetActive(mode == GameMode.InfiniteStacker);
        }
    }

    private void OnLevelLoaded(LevelData level)
    {
        if (level == null) return;

        if (levelNameText != null)
        {
            levelNameText.text = level.levelName;
        }

        UpdateLevelProgress(0);

        // Reset stars to dark color for new level
        InitializeStars();
    }

    private void UpdateLevelProgress(int currentHeight)
    {
        if (levelProgressText == null || levelManager == null || levelManager.CurrentLevel == null) return;

        int required = levelManager.CurrentLevel.requiredStackHeight;
        levelProgressText.text = $"Height: {currentHeight}/{required}";

        // Color code the text based on progress
        float progress = (float)currentHeight / required;
        if (progress >= 1.0f)
        {
            levelProgressText.color = Color.green;
        }
        else if (progress >= 0.5f)
        {
            levelProgressText.color = Color.yellow;
        }
        else
        {
            levelProgressText.color = Color.white;
        }
    }

    private void OnLevelCompleted(int stars, int score)
    {
        // Hide game UI
        if (gameUI != null)
            gameUI.SetActive(false);

        // Show level complete panel
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
        }

        // Update level name
        if (levelNameText != null && levelManager != null && levelManager.CurrentLevel != null)
        {
            levelNameText.text = $"{levelManager.CurrentLevel.levelName} Complete!";
        }

        // Update score
        if (levelScoreText != null)
        {
            levelScoreText.text = $"Score: {score}";
        }

        // Display stars
        DisplayStars(stars);

        // Show/hide next level button based on availability
        if (nextLevelButton != null && levelManager != null)
        {
            bool hasNextLevel = levelManager.CurrentLevelIndex < levelManager.TotalLevels - 1;

            // In demo mode, hide next level button if we're at the demo max level
            if (levelManager.IsDemoVersion)
            {
                int currentLevelNumber = levelManager.CurrentLevelIndex + 1; // Convert to 1-based
                if (currentLevelNumber >= levelManager.DemoMaxLevel)
                {
                    hasNextLevel = false; // Hide next level button at demo limit
                }
            }

            nextLevelButton.gameObject.SetActive(hasNextLevel);
        }
    }

    private void OnLevelFailed()
    {
        // Show game over panel in level mode
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.StackerLevels)
        {
            // Hide level complete panel if it was shown
            if (levelCompletePanel != null)
                levelCompletePanel.SetActive(false);

            // Show game over panel with level-specific messaging
            OnGameOver();

            if (finalScoreText != null && levelManager != null && levelManager.CurrentLevel != null)
            {
                int currentHeight = stackManager?.GetStackCount() ?? 0;
                finalScoreText.text = $"Level Failed!\nReached: {currentHeight}/{levelManager.CurrentLevel.requiredStackHeight}";
            }
        }
    }

    private void DisplayStars(int starCount)
    {
        if (stars == null || stars.Length == 0) return;

        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null)
            {
                // Change color based on whether star is earned
                stars[i].color = i < starCount ? starHighlightColor : starInitialColor;
            }
        }
    }

    private void GoToNextLevel()
    {
        if (levelManager != null)
        {
            // Hide level complete panel
            if (levelCompletePanel != null)
                levelCompletePanel.SetActive(false);

            // Show game UI again
            if (gameUI != null)
                gameUI.SetActive(true);

            // Load next level
            levelManager.NextLevel();

            // Restart the game
            if (gameManager != null)
            {
                gameManager.RestartGame();
            }
        }
    }

    /// <summary>
    /// Retries the current level in level mode
    /// </summary>
    private void RetryLevel()
    {
        if (levelManager != null)
        {
            // Hide level complete panel
            if (levelCompletePanel != null)
                levelCompletePanel.SetActive(false);

            // Show game UI again
            if (gameUI != null)
                gameUI.SetActive(true);

            // Play retry button sound and ensure music is playing
            if (gameSoundManager != null)
            {
                gameSoundManager.PlayRetryButtonSound();
                gameSoundManager.EnsureMusicPlaying();
            }

            // Restart the current level (doesn't advance)
            levelManager.RestartLevel();

            // Restart the game
            if (gameManager != null)
            {
                gameManager.RestartGame();
            }
        }
    }

    private void GoToMainMenu()
    {
        Debug.Log("Returning to Main Menu...");

        // Play home button sound
        if (gameSoundManager != null)
        {
            gameSoundManager.PlayHomeButtonSound();
        }

        // Load the main menu scene
        SceneLoader.LoadMainMenu();
    }

    // Pause Menu Methods

    /// <summary>
    /// Toggles the pause menu on/off
    /// </summary>
    private void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    /// <summary>
    /// Pauses the game and shows pause menu
    /// </summary>
    private void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }

        // Play pause sound and pause music
        if (gameSoundManager != null)
        {
            gameSoundManager.PlayPauseSound();
            gameSoundManager.PauseMusic();
        }

        // Notify listeners that game has paused
        OnGamePaused?.Invoke();

        Debug.Log("Game Paused");
    }

    /// <summary>
    /// Resumes the game from pause menu
    /// </summary>
    private void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        // Play unpause sound and resume music
        if (gameSoundManager != null)
        {
            gameSoundManager.PlayUnpauseSound();
            gameSoundManager.ResumeMusic();
        }

        // Notify listeners that game has resumed
        OnGameResumed?.Invoke();

        Debug.Log("Game Resumed");
    }

    /// <summary>
    /// Restarts the game from pause menu
    /// </summary>
    private void TryAgainFromPause()
    {
        // Resume time first
        Time.timeScale = 1f;
        isPaused = false;

        // Hide pause menu
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        // Play retry button sound and ensure music is playing
        if (gameSoundManager != null)
        {
            gameSoundManager.PlayRetryButtonSound();
            gameSoundManager.EnsureMusicPlaying();
        }

        // Restart the game
        RestartGame();
    }

    /// <summary>
    /// Opens settings from pause menu (placeholder)
    /// </summary>
    private void OpenSettings()
    {
        Debug.Log("Opening Settings... (Not yet implemented)");
        // TODO: Implement settings panel
        // For now, you can add a settings panel later
    }

    /// <summary>
    /// Returns to main menu from pause menu
    /// </summary>
    private void GoToMainMenuFromPause()
    {
        // Resume time first
        Time.timeScale = 1f;
        isPaused = false;

        // Hide pause menu
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        // Play home button sound
        if (gameSoundManager != null)
        {
            gameSoundManager.PlayHomeButtonSound();
        }

        // Go to main menu (don't call sound again since we already played it)
        SceneLoader.LoadMainMenu();
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<UIManager>(this);

        // Unsubscribe from events
        if (gameManager != null)
        {
            gameManager.OnScoreChanged -= UpdateScore;
            gameManager.OnHighScoreChanged -= UpdateHighScore;
            gameManager.OnGameStart -= OnGameStart;
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnGameRestart -= OnGameRestart;
            gameManager.OnGameModeChanged -= OnGameModeChanged;
            gameManager.OnComboChanged -= UpdateComboDisplay;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= OnLevelCompleted;
            levelManager.OnLevelFailed -= OnLevelFailed;
            levelManager.OnLevelLoaded -= OnLevelLoaded;
            levelManager.OnStackHeightUpdated -= UpdateLevelProgress;
        }

        // Stop any running coroutines
        if (landingAccuracyCoroutine != null)
        {
            StopCoroutine(landingAccuracyCoroutine);
        }

        if (comboPulseCoroutine != null)
        {
            StopCoroutine(comboPulseCoroutine);
        }

        // Stop combo timer update
        isUpdatingComboTimer = false;

        // Remove button listeners
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
        }

        if (gameOverMainMenuButton != null)
        {
            gameOverMainMenuButton.onClick.RemoveListener(GoToMainMenu);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveListener(GoToNextLevel);
        }

        if (retryLevelButton != null)
        {
            retryLevelButton.onClick.RemoveListener(RetryLevel);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(GoToMainMenu);
        }

        // Remove pause menu button listeners
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(TogglePause);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(ResumeGame);
        }

        if (tryAgainButton != null)
        {
            tryAgainButton.onClick.RemoveListener(TryAgainFromPause);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
        }

        if (pauseMainMenuButton != null)
        {
            pauseMainMenuButton.onClick.RemoveListener(GoToMainMenuFromPause);
        }
    }
}
