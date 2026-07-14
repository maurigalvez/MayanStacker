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

    [Header("Daily Challenge Result Card")]
    [Tooltip("Shows the day's modifier name on the game-over panel for Daily Challenge runs.")]
    [SerializeField] private TextMeshProUGUI dailyModifierNameText;
    [Tooltip("Shows the player's rank on the daily leaderboard after their score is submitted.")]
    [SerializeField] private TextMeshProUGUI dailyRankText;
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
    [Tooltip("Delay in seconds before showing the level complete panel")]
    [SerializeField] private float levelCompleteDelay = 1f;
    [Tooltip("Duration in seconds for the final score count-up animation on the level complete panel")]
    [SerializeField] private float scoreCountUpDuration = 1.25f;
    [Tooltip("Interval in seconds between score tick sounds while counting up")]
    [SerializeField] private float scoreCountTickInterval = 0.06f;
    [Tooltip("Starting pitch for the score count tick sound")]
    [SerializeField] private float scoreCountStartPitch = 1.0f;
    [Tooltip("Ending pitch for the score count tick sound (ramps up as count progresses)")]
    [SerializeField] private float scoreCountEndPitch = 1.6f;

    [Header("Codex Unlock Popup")]
    [SerializeField] private GameObject codexUnlockPopup;
    [SerializeField] private TextMeshProUGUI codexUnlockText;
    [SerializeField] private Button codexUnlockDismissButton;
    [SerializeField] private float codexPopupDisplayDuration = 3f;

    [Header("Star Colors")]
    [SerializeField] private Color starInitialColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark gray
    [SerializeField] private Color starHighlightColor = new Color(1f, 0.84f, 0f, 1f); // Gold/yellow
    [Tooltip("Scale multiplier at the peak of the star reveal pop")]
    [SerializeField] private float starRevealPopScale = 1.5f;
    [Tooltip("Duration in seconds of the star reveal pop animation")]
    [SerializeField] private float starRevealPopDuration = 0.25f;

    [Header("Game UI")]
    [SerializeField] private GameObject gameUI;
    [SerializeField] private TextMeshProUGUI instructionsText;
    [SerializeField] private GameObject stackHeightDisplay; // Container for stack height UI (shown only in Infinite Mode)
    [SerializeField] private TextMeshProUGUI stackHeightText;
    [SerializeField] private TextMeshProUGUI landingAccuracyText;
    [SerializeField] private GameObject pointsPopupPrefab;
    [SerializeField] private Transform pointsPopupParent;
    [SerializeField] private StackOverviewUI stackOverviewUI; // Mini-map showing stack overview (shown in all game modes)
    [SerializeField] private TextMeshProUGUI gameTitleText; // Title shown before game starts (Level name or "Infinite Stacker")
    [SerializeField] private float gameTitleDisplayDuration = 2.5f; // How long to show the title before game starts

    [Header("Label Change Animation")]
    [Tooltip("Scale multiplier at the peak of the pulse when a label value changes")]
    [SerializeField] private float labelPulseScale = 1.25f;
    [Tooltip("Duration in seconds of the label pulse animation")]
    [SerializeField] private float labelPulseDuration = 0.25f;

    [Header("Combo UI")]
    [SerializeField] private GameObject comboDisplay;
    [SerializeField] private TextMeshProUGUI multiplierText;
    [SerializeField] private Image comboTimerBar; // Circular radial fill timer

    [Header("Kukulkan's Shift UI")]
    [SerializeField] private TextMeshProUGUI kukulkanShiftText; // TextMeshProUGUI component for the message
    [SerializeField] private Color kukulkanShiftColor = Color.yellow;
    [SerializeField] private float kukulkanShiftDisplayDuration = 3f;
    [SerializeField] private float kukulkanShiftAnimationDuration = 0.5f;
    [SerializeField] private float kukulkanShiftAppearScale = 2f;
    [SerializeField] private float kukulkanShiftFinalScale = 1.5f;

    [Header("Kukulkan's Wrath Meter")]
    [SerializeField] private GameObject kukulkanWrathMeter; // Container for the meter (always visible)
    [SerializeField] private Image kukulkanWrathFillImage; // Fill image that goes up based on consecutive perfects
    [SerializeField] private Image kukulkanWrathBackgroundImage; // Background/display image (optional, for styling)

    [Header("Achievement Notification")]
    [SerializeField] private GameObject achievementNotificationPrefab; // Prefab for achievement notifications
    [SerializeField] private Transform achievementNotificationParent; // Transform parent for spawned notifications (typically canvas root)

    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button tryAgainButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button pauseMainMenuButton;

    [Header("Settings")]
    [SerializeField] private float landingAccuracyDisplayDuration = 2f;
    [SerializeField] private float pointsPopupDuration = 2f;
    [SerializeField] private float pointsPopupVerticalOffset = 30f;
    [SerializeField] private float accuracyAnimationDuration = 0.3f;
    [SerializeField] private float accuracyAppearScale = 1.5f;
    [SerializeField] private float accuracyFinalScale = 1.2f;

    [Header("Accuracy Text Juice (per tier)")]
    [Tooltip("Perfect: overshoot peak scale, settle scale, punch-in duration, and rotation wobble (deg)")]
    [SerializeField] private float perfectPeakScale = 2.4f;
    [SerializeField] private float perfectSettleScale = 1.45f;
    [SerializeField] private float perfectAppearDuration = 0.32f;
    [SerializeField] private float perfectPunchRotation = 9f;
    [Tooltip("Good: moderate pop")]
    [SerializeField] private float goodPeakScale = 1.65f;
    [SerializeField] private float goodSettleScale = 1.15f;
    [SerializeField] private float goodAppearDuration = 0.24f;
    [Tooltip("Poor: subdued, quick")]
    [SerializeField] private float poorPeakScale = 1.08f;
    [SerializeField] private float poorSettleScale = 0.95f;
    [SerializeField] private float poorAppearDuration = 0.16f;

    // Per-landing animation params, set by tier in ShowLandingAccuracyAndPoints.
    private float curPeakScale = 1.5f;
    private float curSettleScale = 1.2f;
    private float curAppearDuration = 0.3f;
    private float curPunchRotation = 0f;

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
    [SerializeField] private float comboPopInOvershoot = 1.5f;
    [SerializeField] private float comboPopInDuration = 0.35f;
    [SerializeField] private bool showComboTimer = true;

    // References
    private GameManager gameManager;
    private StackManager stackManager;
    private LevelManager levelManager;
    private GameSoundManager gameSoundManager;
    private GameObject achievementNotificationInstance; // Spawned instance of achievement notification UI

    // State
    private Coroutine landingAccuracyCoroutine;
    private Coroutine comboPulseCoroutine;
    private bool isComboPoppingIn = false;
    private Coroutine gameTitleCoroutine;
    private Coroutine levelCompleteCoroutine;
    private Coroutine perfectHitStreakCoroutine;
    private bool isPaused = false;
    private bool isUpdatingComboTimer = false;
    private bool isUpdatingSpeedRunTimer = false;
    private bool isTitleShowing = false; // Track if title is currently showing
    private bool isSubscribedToGameStart = false; // Track subscription to avoid duplicates

    // Events
    public System.Action OnGameResumed;
    public System.Action OnGamePaused;
    public System.Action OnTitleFinished; // Fired when the game title finishes displaying

    // Public properties
    public bool IsPaused => isPaused;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<UIManager>(this);

        // Spawn achievement notification UI if prefab is assigned
        SpawnAchievementNotificationUI();

        // Subscribe to OnGameStart early to avoid missing events (Android timing fix)
        // Try to find GameManager early - it might already be registered if it persists across scenes
        gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null && !isSubscribedToGameStart)
        {
            gameManager.OnGameStart += OnGameStart;
            isSubscribedToGameStart = true;
        }
    }

    private void Start()
    {
        // Get references
        if (gameManager == null)
        {
            gameManager = DependencyRegistry.Find<GameManager>();
        }
        stackManager = DependencyRegistry.Find<StackManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        gameSoundManager = DependencyRegistry.Find<GameSoundManager>();

        // Subscribe to game events (OnGameStart already subscribed in Awake for Android timing fix)
        if (gameManager != null)
        {
            gameManager.OnScoreChanged += UpdateScore;
            gameManager.OnHighScoreChanged += UpdateHighScore;
            // Subscribe to OnGameStart if not already subscribed (Android timing fix - subscribe early in Awake)
            if (!isSubscribedToGameStart)
            {
                gameManager.OnGameStart += OnGameStart;
                isSubscribedToGameStart = true;
            }
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
            gameManager.OnGameModeChanged += OnGameModeChanged;
            gameManager.OnComboChanged += UpdateComboDisplay;
            gameManager.OnConsecutivePerfectHitsChanged += UpdateKukulkanWrathMeter;
        }

        // Subscribe to stack events
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
            stackManager.OnStackStraightened += OnStackStraightened;
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

        if (codexUnlockDismissButton != null)
        {
            codexUnlockDismissButton.onClick.AddListener(DismissCodexPopup);
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

        if (codexUnlockPopup != null)
            codexUnlockPopup.SetActive(false);

        // Hide game title initially
        if (gameTitleText != null)
            gameTitleText.gameObject.SetActive(false);

        // Disable raycast targets on popup text labels to prevent blocking input
        DisableRaycastTargetsOnPopupTexts();

        // Hide Kukulkan's Shift text initially
        HideKukulkanShiftText();

        // Initialize Kukulkan's Wrath meter (always visible)
        InitializeKukulkanWrathMeter();

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

    /// <summary>
    /// Spawns the achievement notification UI prefab at the specified transform
    /// </summary>
    private void SpawnAchievementNotificationUI()
    {
        // Check if prefab is assigned
        if (achievementNotificationPrefab == null)
        {
            Debug.LogWarning("UIManager: Achievement Notification Prefab not assigned. Skipping spawn.");
            return;
        }

        // Determine parent transform (use this transform if not specified)
        Transform parentTransform = achievementNotificationParent != null ? achievementNotificationParent : transform;

        // Spawn the notification UI
        achievementNotificationInstance = Instantiate(achievementNotificationPrefab, parentTransform);

        // Reset RectTransform position to zero
        RectTransform rectTransform = achievementNotificationInstance.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.offsetMin = Vector2.zero; // Left and Bottom offsets
            rectTransform.offsetMax = Vector2.zero; // Right and Top offsets (negative values in inspector)
            rectTransform.localScale = Vector3.one;
        }

        Debug.Log($"UIManager: Achievement Notification UI spawned at {parentTransform.name} with position reset to zero");
    }

    /// <summary>
    /// Disables raycast targets on all popup text labels to prevent them from blocking input
    /// </summary>
    private void DisableRaycastTargetsOnPopupTexts()
    {
        if (landingAccuracyText != null)
            landingAccuracyText.raycastTarget = false;

        if (gameTitleText != null)
            gameTitleText.raycastTarget = false;

        if (instructionsText != null)
            instructionsText.raycastTarget = false;

        if (multiplierText != null)
            multiplierText.raycastTarget = false;

        if (kukulkanShiftText != null)
            kukulkanShiftText.raycastTarget = false;
    }

    private void UpdateScore(int score)
    {
        SetTextAnimated(scoreText, score.ToString());
    }

    private void UpdateHighScore(int highScore)
    {
        SetTextAnimated(highScoreText, highScore.ToString());
    }

    /// <summary>
    /// Sets a TMP label's text and plays a quick scale pulse if the value changed.
    /// </summary>
    private void SetTextAnimated(TextMeshProUGUI label, string newText)
    {
        if (label == null) return;
        if (label.text == newText) return;

        label.text = newText;

        if (!isActiveAndEnabled || !label.gameObject.activeInHierarchy) return;
        if (labelPulseDuration <= 0f || labelPulseScale <= 1f) return;

        StartCoroutine(PulseLabelRoutine(label.rectTransform));
    }

    private IEnumerator PulseLabelRoutine(RectTransform rt)
    {
        if (rt == null) yield break;

        Vector3 baseScale = Vector3.one;
        Vector3 peakScale = baseScale * labelPulseScale;
        float half = labelPulseDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.LerpUnclamped(baseScale, peakScale, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.LerpUnclamped(peakScale, baseScale, t / half);
            yield return null;
        }

        rt.localScale = baseScale;
    }

    private void OnGameStart()
    {
        // Ensure we have references (handles timing issues where OnGameStart fires before Start())
        if (gameManager == null)
        {
            gameManager = DependencyRegistry.Find<GameManager>();
        }
        if (levelManager == null)
        {
            levelManager = DependencyRegistry.Find<LevelManager>();
        }

        // Hide game over panel, show game UI
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (gameUI != null)
            gameUI.SetActive(true);

        // Daily Challenge: refresh the remaining-blocks display now that config is ready
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.DailyChallenge)
        {
            var dailyMgr = DependencyRegistry.Find<DailyChallengeManager>();
            if (dailyMgr != null && dailyMgr.IsSpeedRun)
            {
                StartSpeedRunTimer();
            }
            else
            {
                UpdateStackHeight();
            }
        }

        // Show game title before starting (instructions and input will wait for title to finish)
        ShowGameTitle();

        // Instructions will be shown after title finishes (handled in GameTitleRoutine)
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
        // Stop combo timer update to prevent sound effects after game over
        isUpdatingComboTimer = false;
        StopSpeedRunTimer();

        // Show game over panel, hide game UI
        if (gameUI != null)
            gameUI.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // Update final score
        if (finalScoreText != null && gameManager != null)
        {
            finalScoreText.text = gameManager.CurrentScore.ToString();
        }

        // Check if it's a new high score (only show in Infinite Stacker mode)
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.InfiniteStacker &&
            gameManager.CurrentScore >= gameManager.HighScore)
        {
            ShowNewHighScore();
        }
        else
        {
            HideNewHighScore();
        }

        // Daily Challenge: populate the result-card extras (modifier name + leaderboard rank).
        UpdateDailyChallengeResultCard();
    }

    /// <summary>
    /// Populates the Daily Challenge result-card fields (modifier name + rank).
    /// Hides them for any non-Daily mode.
    /// </summary>
    private void UpdateDailyChallengeResultCard()
    {
        bool isDaily = gameManager != null && gameManager.CurrentGameMode == GameMode.DailyChallenge;

        if (dailyModifierNameText != null)
        {
            dailyModifierNameText.gameObject.SetActive(isDaily);
        }
        if (dailyRankText != null)
        {
            dailyRankText.gameObject.SetActive(isDaily);
        }
        if (!isDaily) return;

        var dailyMgr = DependencyRegistry.Find<DailyChallengeManager>();
        if (dailyMgr != null && dailyMgr.HasConfig && dailyModifierNameText != null)
        {
            string modifierName = LocalizationManager.Get(
                DailyChallengeManager.GetModifierDisplayNameKey(dailyMgr.CurrentConfig.modifier));
            string modifierLine = LocalizationManager.Get("daily_result_modifier", modifierName);

            // SpeedRun: append elapsed time and time bonus to the result card
            if (dailyMgr.IsSpeedRun)
            {
                float elapsed = dailyMgr.ElapsedTime;
                int minutes = (int)(elapsed / 60f);
                int seconds = (int)(elapsed % 60f);
                int timeBonus = dailyMgr.CalculateSpeedRunTimeBonus();
                string timeLine = LocalizationManager.Get("daily_speedrun_result", minutes, seconds, timeBonus);
                modifierLine += "\n" + timeLine;
            }

            dailyModifierNameText.text = modifierLine;
        }

        if (dailyRankText != null)
        {
            dailyRankText.text = LocalizationManager.Get("daily_result_rank_loading");
        }

        // Submission is async — give PlayFab a beat to write the score, then read back our position.
        StartCoroutine(FetchDailyRankWithDelay(0.75f));
    }

    private IEnumerator FetchDailyRankWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager == null || !playFabManager.IsLoggedIn || dailyRankText == null)
        {
            if (dailyRankText != null)
                dailyRankText.text = LocalizationManager.Get("daily_result_rank_unavailable");
            yield break;
        }

        playFabManager.GetPlayerLeaderboardPosition("DailyChallenge_Leaderboard", 1,
            entries =>
            {
                if (dailyRankText == null) return;
                if (entries != null && entries.Count > 0)
                {
                    var me = entries.Find(e => e.isCurrentPlayer);
                    int position = me != null ? me.position : entries[0].position;
                    dailyRankText.text = LocalizationManager.Get("daily_result_rank", position);
                }
                else
                {
                    dailyRankText.text = LocalizationManager.Get("daily_result_rank_unavailable");
                }
            },
            error =>
            {
                if (dailyRankText != null)
                    dailyRankText.text = LocalizationManager.Get("daily_result_rank_unavailable");
            });
    }

    private void OnGameRestart()
    {
        // Hide game over panel
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // Reset stack height display (don't query StackManager as it may not be cleared yet)
        StopSpeedRunTimer();
        if (stackHeightText != null)
        {
            // Daily Challenge: reset to full block count target (SpeedRun timer restarts in OnGameStart)
            if (gameManager != null && gameManager.CurrentGameMode == GameMode.DailyChallenge)
            {
                var dailyMgr = DependencyRegistry.Find<DailyChallengeManager>();
                if (dailyMgr != null && dailyMgr.IsSpeedRun)
                {
                    stackHeightText.text = "0:00.0";
                }
                else
                {
                    stackHeightText.text = (dailyMgr != null ? dailyMgr.BlockCountTarget : 0).ToString();
                }
            }
            else
            {
                stackHeightText.text = 0.ToString();
            }
        }

        // Reset level progress if in level mode
        if (levelProgressText != null && levelProgressText.gameObject.activeSelf)
        {
            UpdateLevelProgress(0);
        }

        // Hide landing accuracy text
        HideLandingAccuracy();

        // Hide Kukulkan's Shift text
        HideKukulkanShiftText();

        // Reset Kukulkan's Wrath meter
        if (gameManager != null)
        {
            UpdateKukulkanWrathMeter(0);
        }

        // Don't show title here - OnGameStart() will be called after RestartGame() and will show it
        // This prevents the title from showing twice during restart

        // Instructions will be shown after title finishes if needed (handled in GameTitleRoutine)
        // Otherwise, make sure they're hidden
        if (!ShouldShowInstructions())
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
            instructionsText.text = LocalizationManager.Get("instructions");
        }
    }

    private void HideInstructions()
    {
        if (instructionsText != null)
        {
            instructionsText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Shows the game title (level name or "Infinite Stacker") before the game starts
    /// </summary>
    private void ShowGameTitle()
    {
        if (gameTitleText == null) return;

        // Ensure we have references - get them if not already set (handles timing issues on Android)
        if (gameManager == null)
        {
            gameManager = DependencyRegistry.Find<GameManager>();
            if (gameManager == null) return;
        }

        if (levelManager == null)
        {
            levelManager = DependencyRegistry.Find<LevelManager>();
        }

        // Stop any existing title coroutine
        if (gameTitleCoroutine != null)
        {
            StopCoroutine(gameTitleCoroutine);
        }

        // If in level mode and level isn't loaded yet, wait for it via coroutine
        if (gameManager.CurrentGameMode == GameMode.StackerLevels &&
            (levelManager == null || levelManager.CurrentLevel == null))
        {
            gameTitleCoroutine = StartCoroutine(WaitForLevelAndShowTitle());
            return;
        }

        // Mark that title is showing
        isTitleShowing = true;

        // Determine title text based on game mode
        string titleText = "";
        if (gameManager.CurrentGameMode == GameMode.StackerLevels)
        {
            // Show level number and level name if available
            if (levelManager != null && levelManager.CurrentLevel != null)
            {
                titleText = LocalizationManager.Get("level_title_format", levelManager.CurrentLevel.levelNumber, LocalizationManager.GetLevelName(levelManager.CurrentLevel));
            }
            else
            {
                titleText = LocalizationManager.Get("mode_level");
            }
        }
        else if (gameManager.CurrentGameMode == GameMode.InfiniteStacker)
        {
            titleText = LocalizationManager.Get("mode_infinite_stacker");
        }
        else if (gameManager.CurrentGameMode == GameMode.DailyChallenge)
        {
            // Daily Challenge intro: "Ritual Of The Sun : <modifier>"
            var dailyMgr = DependencyRegistry.Find<DailyChallengeManager>();
            string modifierName = "";
            if (dailyMgr != null && dailyMgr.HasConfig)
            {
                modifierName = LocalizationManager.Get(
                    DailyChallengeManager.GetModifierDisplayNameKey(dailyMgr.CurrentConfig.modifier));
            }
            titleText = LocalizationManager.Get("daily_title_format", modifierName);
        }

        // Set the text
        gameTitleText.text = titleText;

        // Ensure UIManager is active before starting coroutine (Android timing fix)
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("UIManager not active, cannot start title coroutine");
            return;
        }

        // Start the title display coroutine
        gameTitleCoroutine = StartCoroutine(GameTitleRoutine());
    }

    /// <summary>
    /// Waits for level to load, then shows the title with correct level information
    /// </summary>
    private IEnumerator WaitForLevelAndShowTitle()
    {
        // Wait for level manager and level to be available
        float timeout = 5f; // Maximum wait time
        float elapsed = 0f;

        while ((levelManager == null || levelManager.CurrentLevel == null) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Wait a frame to ensure everything is initialized (Android timing fix)
        yield return null;

        // Now show the title with the level information
        if (levelManager != null && levelManager.CurrentLevel != null && gameTitleText != null)
        {
            // Mark that title is showing
            isTitleShowing = true;

            // Set the text with level information
            string titleText = LocalizationManager.Get("level_title_format", levelManager.CurrentLevel.levelNumber, LocalizationManager.GetLevelName(levelManager.CurrentLevel));
            gameTitleText.text = titleText;

            // Ensure UIManager is active before starting coroutine (Android timing fix)
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning("UIManager not active, cannot start title coroutine");
                yield break;
            }

            // Start the title display coroutine
            gameTitleCoroutine = StartCoroutine(GameTitleRoutine());
        }
        else
        {
            // Fallback if level still not loaded after timeout
            if (gameTitleText != null)
            {
                isTitleShowing = true;
                gameTitleText.text = LocalizationManager.Get("mode_level");

                // Ensure UIManager is active before starting coroutine (Android timing fix)
                if (gameObject.activeInHierarchy)
                {
                    gameTitleCoroutine = StartCoroutine(GameTitleRoutine());
                }
                else
                {
                    Debug.LogWarning("UIManager not active, cannot start title coroutine");
                }
            }
        }
    }

    /// <summary>
    /// Public property to check if title is currently showing
    /// </summary>
    public bool IsTitleShowing => isTitleShowing;

    /// <summary>
    /// Coroutine that shows the game title with animation, then hides it
    /// </summary>
    private IEnumerator GameTitleRoutine()
    {
        if (gameTitleText == null) yield break;

        // Ensure the UIManager GameObject is active (required for coroutines on Android)
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("UIManager not active, cannot show game title");
            yield break;
        }

        // Wait a frame to ensure everything is initialized (helps with Android timing issues)
        yield return null;

        // Wait before showing the title (reduced from 0.25s to ensure better Android compatibility)
        yield return new WaitForSeconds(0.1f);

        // Show the title
        if (gameTitleText == null) yield break;
        gameTitleText.gameObject.SetActive(true);

        // Wait for end of frame to ensure GameObject is fully active and ready for component access (Android fix)
        yield return new WaitForEndOfFrame();

        if (gameTitleText == null) yield break;

        // Get components for animation
        RectTransform titleRect = gameTitleText.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = gameTitleText.GetComponent<CanvasGroup>();

        // Add CanvasGroup if it doesn't exist (for fading)
        if (canvasGroup == null)
        {
            canvasGroup = gameTitleText.gameObject.AddComponent<CanvasGroup>();
        }

        // Animation settings
        float animationDuration = 0.5f;
        float elapsedTime = 0f;

        // Appear animation: scale from large and fade in
        Vector3 startScale = Vector3.one * 1.5f;
        Vector3 targetScale = Vector3.one;

        if (titleRect != null)
        {
            titleRect.localScale = startScale;
        }
        canvasGroup.alpha = 0f;

        // Fade in and scale down
        while (elapsedTime < animationDuration)
        {
            if (gameTitleText == null || titleRect == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;

            if (titleRect != null)
            {
                titleRect.localScale = Vector3.Lerp(startScale, targetScale, progress);
            }
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);

            yield return null;
        }

        // Ensure final values
        if (titleRect != null)
        {
            titleRect.localScale = targetScale;
        }
        canvasGroup.alpha = 1f;

        // Hold for display duration (subtract animation time)
        float holdDuration = gameTitleDisplayDuration - (animationDuration * 2);
        if (holdDuration > 0)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        // Disappear animation: fade out and scale down
        elapsedTime = 0f;
        startScale = targetScale;
        Vector3 endScale = Vector3.one * 0.8f;

        while (elapsedTime < animationDuration)
        {
            if (gameTitleText == null || titleRect == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;

            if (titleRect != null)
            {
                titleRect.localScale = Vector3.Lerp(startScale, endScale, progress);
            }
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);

            yield return null;
        }

        // Hide the title
        if (gameTitleText != null)
        {
            gameTitleText.gameObject.SetActive(false);

            // Reset scale and alpha for next appearance
            if (titleRect != null)
            {
                titleRect.localScale = Vector3.one;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        // Mark that title is no longer showing
        isTitleShowing = false;

        // Fire event that title has finished
        OnTitleFinished?.Invoke();

        // Now show instructions if needed (after title is done)
        if (ShouldShowInstructions())
        {
            ShowInstructions();
            StartCoroutine(InstructionRoutine());
        }
    }

    private void UpdateStackHeight()
    {
        if (stackHeightText == null) return;

        // Daily Challenge: show contextual info instead of stack height
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.DailyChallenge)
        {
            var dailyMgr = DependencyRegistry.Find<DailyChallengeManager>();
            if (dailyMgr != null && dailyMgr.HasConfig)
            {
                // SpeedRun: timer is updated continuously by the coroutine, skip here
                if (dailyMgr.IsSpeedRun) return;

                int remaining = Mathf.Max(0, dailyMgr.BlockCountTarget - dailyMgr.BlocksPlaced);
                SetTextAnimated(stackHeightText, remaining.ToString());
                return;
            }
        }

        if (stackManager != null)
        {
            int height = stackManager.GetStackCount();
            SetTextAnimated(stackHeightText, height.ToString());
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

        // Set the text based on accuracy with combo count.
        // Each tier also gets its own animation profile so Perfect visibly punches
        // harder than Good/Poor (not just a color swap).
        string baseText = "";
        if (accuracy >= 0.9f)
        {
            baseText = LocalizationManager.Get("accuracy_perfect");
            landingAccuracyText.color = perfectAccuracyColor;
            curPeakScale = perfectPeakScale;
            curSettleScale = perfectSettleScale;
            curAppearDuration = perfectAppearDuration;
            curPunchRotation = perfectPunchRotation;
        }
        else if (accuracy >= 0.6f)
        {
            baseText = LocalizationManager.Get("accuracy_good");
            landingAccuracyText.color = goodAccuracyColor;
            curPeakScale = goodPeakScale;
            curSettleScale = goodSettleScale;
            curAppearDuration = goodAppearDuration;
            curPunchRotation = 0f;
        }
        else
        {
            baseText = LocalizationManager.Get("accuracy_poor");
            landingAccuracyText.color = poorAccuracyColor;
            curPeakScale = poorPeakScale;
            curSettleScale = poorSettleScale;
            curAppearDuration = poorAppearDuration;
            curPunchRotation = 0f;
        }

        // Add combo count if active (Perfect landing only)
        if (currentCombo > 0 && accuracy >= 0.9f)
        {
            landingAccuracyText.text = LocalizationManager.Get("combo_format", baseText, currentCombo);
        }
        else
        {
            landingAccuracyText.text = baseText;
        }

        // Position the accuracy label at the center of the screen
        RectTransform accuracyRect = landingAccuracyText.GetComponent<RectTransform>();
        if (accuracyRect != null)
        {
            accuracyRect.anchoredPosition = Vector2.zero; // Center of parent canvas
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
        if (landingAccuracyText == null) yield break;

        RectTransform accuracyRect = landingAccuracyText.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = landingAccuracyText.GetComponent<CanvasGroup>();

        // Add CanvasGroup if it doesn't exist (for fading)
        if (canvasGroup == null)
        {
            canvasGroup = landingAccuracyText.gameObject.AddComponent<CanvasGroup>();
        }

        // Appear animation: elastic pop that overshoots the peak then settles.
        // Amplitude/duration/rotation come from the per-tier profile set above, so a
        // Perfect punches in big (with a rotation wobble) while a Poor barely blips.
        float elapsedTime = 0f;
        float appearDuration = Mathf.Max(0.05f, curAppearDuration);
        Vector3 targetScale = Vector3.one * curSettleScale;

        accuracyRect.localScale = Vector3.one * (curSettleScale * 0.5f);
        accuracyRect.localRotation = Quaternion.identity;
        canvasGroup.alpha = 0f;

        while (elapsedTime < appearDuration)
        {
            if (landingAccuracyText == null || accuracyRect == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / appearDuration);

            // Rise quickly to the overshoot peak (~45% in) then ease back to settle.
            float scale;
            if (progress < 0.45f)
            {
                float k = progress / 0.45f;
                scale = Mathf.Lerp(curSettleScale * 0.5f, curPeakScale, k * k * (3f - 2f * k));
            }
            else
            {
                float k = (progress - 0.45f) / 0.55f;
                scale = Mathf.Lerp(curPeakScale, curSettleScale, k * k * (3f - 2f * k));
            }
            accuracyRect.localScale = Vector3.one * scale;

            // Damped rotation wobble adds "impact" - Perfect only (curPunchRotation > 0).
            if (curPunchRotation > 0f)
            {
                float damp = Mathf.Exp(-4f * progress);
                float angle = Mathf.Sin(progress * Mathf.PI * 3f) * curPunchRotation * damp;
                accuracyRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            // Snappy fade-in (done well before the scale settles).
            canvasGroup.alpha = Mathf.Clamp01(progress * 2.5f);

            yield return null;
        }

        // Ensure final values
        accuracyRect.localScale = targetScale;
        accuracyRect.localRotation = Quaternion.identity;
        canvasGroup.alpha = 1f;

        // Hold for display duration (subtract appear + fade-out time)
        float holdDuration = landingAccuracyDisplayDuration - appearDuration - accuracyAnimationDuration;
        if (holdDuration > 0)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        // Disappear animation: fade out and scale down (relative to this tier's settle scale)
        elapsedTime = 0f;
        Vector3 startScale = targetScale;
        Vector3 endScale = Vector3.one * (curSettleScale * 0.8f);

        while (elapsedTime < accuracyAnimationDuration)
        {
            if (landingAccuracyText == null || accuracyRect == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / accuracyAnimationDuration;

            accuracyRect.localScale = Vector3.Lerp(startScale, endScale, progress);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);

            yield return null;
        }

        // Hide the text
        HideLandingAccuracy();
    }

    private void HideLandingAccuracy()
    {
        if (landingAccuracyText != null)
        {
            landingAccuracyText.gameObject.SetActive(false);

            // Reset scale, rotation and alpha for next appearance
            RectTransform accuracyRect = landingAccuracyText.GetComponent<RectTransform>();
            if (accuracyRect != null)
            {
                accuracyRect.localScale = Vector3.one;
                accuracyRect.localRotation = Quaternion.identity;
            }

            CanvasGroup canvasGroup = landingAccuracyText.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
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

            // Disable raycast target to prevent blocking input
            popupText.raycastTarget = false;

            // Color code based on base points (accounting for multiplier)
            // Perfect base = 100, Good base = 50, Poor base = 10
            if (points >= 90) // Perfect landing (100+ with multiplier or base perfect)
                popupText.color = perfectAccuracyColor;
            else if (points >= 45) // Good landing (50+ with multiplier or base good)
                popupText.color = goodAccuracyColor;
            else // Poor landing
                popupText.color = poorAccuracyColor;
        }

        // Position at center of screen, above the accuracy label
        RectTransform popupRect = popup.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            // Center position with vertical offset above accuracy text
            popupRect.anchoredPosition = new Vector2(0, pointsPopupVerticalOffset);
        }

        // Start animation and destroy after duration
        StartCoroutine(AnimateAndDestroyPopup(popup));
    }

    private System.Collections.IEnumerator AnimateAndDestroyPopup(GameObject popup)
    {
        if (popup == null) yield break;

        RectTransform rectTransform = popup.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = popup.GetComponent<CanvasGroup>();

        // Add CanvasGroup if it doesn't exist (for fading)
        if (canvasGroup == null)
        {
            canvasGroup = popup.AddComponent<CanvasGroup>();
        }

        float elapsedTime = 0f;
        float animationDuration = accuracyAnimationDuration;

        // Appear animation: scale from large to slightly larger and fade in
        Vector3 startScale = Vector3.one * accuracyAppearScale;
        Vector3 targetScale = Vector3.one * accuracyFinalScale;

        rectTransform.localScale = startScale;
        canvasGroup.alpha = 0f;

        // Fade in and scale to final size
        while (elapsedTime < animationDuration)
        {
            if (popup == null || rectTransform == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, progress);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
            yield return null;
        }

        // Ensure final values
        rectTransform.localScale = targetScale;
        canvasGroup.alpha = 1f;

        // Hold for display duration (subtract animation time)
        float holdDuration = pointsPopupDuration - (animationDuration * 2);
        if (holdDuration > 0)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        // Disappear animation: fade out and scale down
        elapsedTime = 0f;
        startScale = targetScale;
        Vector3 endScale = Vector3.one * 0.8f;

        while (elapsedTime < animationDuration && popup != null && rectTransform != null)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            rectTransform.localScale = Vector3.Lerp(startScale, endScale, progress);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
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
            newHighScoreText.text = LocalizationManager.Get("new_high_score");
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
        bool wasShowing = comboDisplay != null && comboDisplay.activeSelf;
        bool shouldShow = combo > 0 && multiplier > 1f;
        if (comboDisplay != null)
        {
            comboDisplay.SetActive(shouldShow);
        }
        bool justAppeared = shouldShow && !wasShowing;

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
            if (justAppeared)
                TriggerComboPopIn();
            else if (!isComboPoppingIn)
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
            // Stop updating if game is over or level is completed
            if (gameManager.IsGameOver)
            {
                isUpdatingComboTimer = false;
                yield break;
            }

            if (levelManager != null && levelManager.IsLevelComplete)
            {
                isUpdatingComboTimer = false;
                yield break;
            }

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
    }

    /// <summary>
    /// Starts the SpeedRun elapsed-time display coroutine.
    /// Updates stackHeightText every frame with mm:ss.f format.
    /// </summary>
    private void StartSpeedRunTimer()
    {
        StopSpeedRunTimer();
        isUpdatingSpeedRunTimer = true;
        StartCoroutine(UpdateSpeedRunTimerRoutine());
    }

    private void StopSpeedRunTimer()
    {
        isUpdatingSpeedRunTimer = false;
    }

    private IEnumerator UpdateSpeedRunTimerRoutine()
    {
        while (isUpdatingSpeedRunTimer && stackHeightText != null)
        {
            if (gameManager == null || gameManager.IsGameOver)
            {
                isUpdatingSpeedRunTimer = false;
                yield break;
            }

            var dailyMgr = DependencyRegistry.Find<DailyChallengeManager>();
            if (dailyMgr == null || !dailyMgr.IsSpeedRun)
            {
                isUpdatingSpeedRunTimer = false;
                yield break;
            }

            float elapsed = dailyMgr.ElapsedTime;
            int minutes = (int)(elapsed / 60f);
            int seconds = (int)(elapsed % 60f);
            int tenths = (int)((elapsed * 10f) % 10f);
            stackHeightText.text = $"{minutes}:{seconds:D2}.{tenths}";

            yield return null;
        }

        isUpdatingSpeedRunTimer = false;
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

    /// <summary>
    /// Triggers a pop-in scale animation when the combo first appears
    /// </summary>
    private void TriggerComboPopIn()
    {
        if (comboDisplay == null) return;

        if (comboPulseCoroutine != null)
        {
            StopCoroutine(comboPulseCoroutine);
        }

        comboPulseCoroutine = StartCoroutine(ComboPopInAnimation());
    }

    /// <summary>
    /// Animates a pop-in (scale 0 -> overshoot -> 1) on the combo display
    /// </summary>
    private IEnumerator ComboPopInAnimation()
    {
        RectTransform rectTransform = comboDisplay.GetComponent<RectTransform>();
        if (rectTransform == null) yield break;

        isComboPoppingIn = true;
        rectTransform.localScale = Vector3.zero;

        Vector3 startScale = Vector3.zero;
        Vector3 overshootScale = Vector3.one * comboPopInOvershoot;
        Vector3 endScale = Vector3.one;

        float elapsedTime = 0f;
        float upDuration = comboPopInDuration * 0.6f;
        float downDuration = comboPopInDuration * 0.4f;

        // Scale up with ease-out from 0 to overshoot
        while (elapsedTime < upDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / upDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            rectTransform.localScale = Vector3.LerpUnclamped(startScale, overshootScale, eased);
            yield return null;
        }

        elapsedTime = 0f;

        // Settle back from overshoot to 1
        while (elapsedTime < downDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / downDuration);
            float eased = progress * progress * (3f - 2f * progress);
            rectTransform.localScale = Vector3.LerpUnclamped(overshootScale, endScale, eased);
            yield return null;
        }

        rectTransform.localScale = endScale;
        isComboPoppingIn = false;
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
            levelNameText.text = $"Level {levelManager.CurrentLevel.levelNumber}\n{levelManager.CurrentLevel.levelName}";
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
            gameModeText.text = mode == GameMode.InfiniteStacker ? LocalizationManager.Get("mode_infinite") : LocalizationManager.Get("mode_level");
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

        // Show/hide stack height display based on mode (shown in Infinite Mode and Daily Challenge)
        if (stackHeightDisplay != null)
        {
            stackHeightDisplay.SetActive(mode == GameMode.InfiniteStacker || mode == GameMode.DailyChallenge);
        }

        // Show stack overview UI for all game modes
        if (stackOverviewUI != null)
        {
            stackOverviewUI.gameObject.SetActive(true);
        }
    }

    private void OnLevelLoaded(LevelData level)
    {
        if (level == null) return;

        if (levelNameText != null)
        {
            levelNameText.text = LocalizationManager.Get("level_title_format", level.levelNumber, LocalizationManager.GetLevelName(level));
        }

        UpdateLevelProgress(0);

        // Reset stars to dark color for new level
        InitializeStars();

        // If title is waiting for level to load (via WaitForLevelAndShowTitle coroutine),
        // update the title text immediately - the coroutine will continue and show it
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.StackerLevels &&
            isTitleShowing && gameTitleText != null)
        {
            // Update title text if it was showing fallback mode text - the coroutine will handle the animation
            string modeLabel = LocalizationManager.Get("mode_level");
            if (gameTitleText.text == modeLabel || gameTitleText.text.Contains(modeLabel))
            {
                gameTitleText.text = LocalizationManager.Get("level_title_format", level.levelNumber, LocalizationManager.GetLevelName(level));
            }
        }
    }

    private void UpdateLevelProgress(int currentHeight)
    {
        if (levelProgressText == null || levelManager == null || levelManager.CurrentLevel == null) return;

        int required = levelManager.CurrentLevel.requiredStackHeight;
        SetTextAnimated(levelProgressText, $"{currentHeight}/{required}");

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

    private void OnLevelCompleted(int stars, int score, bool showCodexPopup)
    {
        // Stop combo timer update to prevent sound effects after level completion
        isUpdatingComboTimer = false;

        // Hide game UI
        if (gameUI != null)
            gameUI.SetActive(false);

        // Show codex unlock popup if this is the first completion
        if (showCodexPopup && levelManager != null && levelManager.CurrentLevel != null)
        {
            ShowCodexUnlockPopup(LocalizationManager.GetLevelName(levelManager.CurrentLevel));

            // Mark codex as unlocked after showing popup
            levelManager.MarkCodexUnlockedForLevel(levelManager.CurrentLevel.levelNumber);
        }

        // Stop any existing level complete coroutine
        if (levelCompleteCoroutine != null)
        {
            StopCoroutine(levelCompleteCoroutine);
        }

        // Start coroutine to show level complete panel after delay
        levelCompleteCoroutine = StartCoroutine(ShowLevelCompletePanelDelayed(stars, score));
    }

    /// <summary>
    /// Coroutine to show the level complete panel after a delay
    /// </summary>
    private IEnumerator ShowLevelCompletePanelDelayed(int stars, int score)
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(levelCompleteDelay);

        // Show level complete panel
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
        }

        // Update level name
        if (levelNameText != null && levelManager != null && levelManager.CurrentLevel != null)
        {
            levelNameText.text = LocalizationManager.Get("level_complete_format", levelManager.CurrentLevel.levelNumber, LocalizationManager.GetLevelName(levelManager.CurrentLevel));
        }

        // Animate the score counting up; stars light up progressively as the count climbs
        yield return StartCoroutine(AnimateLevelScoreCountUp(score, stars));

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
                finalScoreText.text = LocalizationManager.Get("level_failed_format", currentHeight, levelManager.CurrentLevel.requiredStackHeight);
            }
        }
    }

    /// <summary>
    /// Animates the level complete score text counting up from 0 to the final score,
    /// playing a tick sound at a fixed interval with a pitch ramp. Stars light up
    /// progressively at eased progress thresholds while the score climbs.
    /// </summary>
    private IEnumerator AnimateLevelScoreCountUp(int finalScore, int earnedStarCount)
    {
        if (levelScoreText == null)
        {
            yield break;
        }

        int totalStarSlots = stars != null ? stars.Length : 0;

        // Reset all stars to "unearned" state at normal scale
        if (stars != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    stars[i].color = starInitialColor;
                    stars[i].transform.localScale = Vector3.one;
                }
            }
        }

        // If there's nothing to count or duration is effectively zero, snap to the final value
        // and immediately highlight the earned stars
        if (finalScore <= 0 || scoreCountUpDuration <= 0f)
        {
            levelScoreText.text = LocalizationManager.Get("level_score_format", finalScore);
            for (int i = 0; i < totalStarSlots && i < earnedStarCount; i++)
            {
                if (stars[i] != null) stars[i].color = starHighlightColor;
            }
            yield break;
        }

        levelScoreText.text = LocalizationManager.Get("level_score_format", 0);

        float elapsed = 0f;
        float nextTickTime = 0f;
        int displayedScore = 0;
        int starsLit = 0;
        int starsToLight = Mathf.Clamp(earnedStarCount, 0, totalStarSlots);

        while (elapsed < scoreCountUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / scoreCountUpDuration);

            // Ease-out so the count-up decelerates toward the final value
            float eased = 1f - (1f - t) * (1f - t);
            int newScore = Mathf.FloorToInt(Mathf.Lerp(0f, finalScore, eased));

            if (newScore != displayedScore)
            {
                displayedScore = newScore;
                levelScoreText.text = LocalizationManager.Get("level_score_format", displayedScore);
            }

            // Light up the next star once eased progress crosses its threshold
            // e.g., for 3 earned stars: thresholds at 1/3, 2/3, 3/3
            while (starsLit < starsToLight && eased >= (float)(starsLit + 1) / starsToLight)
            {
                LightUpStar(starsLit);
                starsLit++;
            }

            if (gameSoundManager != null && elapsed >= nextTickTime && displayedScore < finalScore)
            {
                float pitch = Mathf.Lerp(scoreCountStartPitch, scoreCountEndPitch, t);
                gameSoundManager.PlayScoreCountSound(pitch);
                nextTickTime = elapsed + Mathf.Max(0.01f, scoreCountTickInterval);
            }

            yield return null;
        }

        // Ensure final value is displayed exactly, and play one last tick at max pitch
        levelScoreText.text = $"Score: {finalScore}";
        if (gameSoundManager != null)
        {
            gameSoundManager.PlayScoreCountSound(scoreCountEndPitch);
        }

        // Safety: ensure any remaining earned stars are lit (handles rounding edge cases)
        while (starsLit < starsToLight)
        {
            LightUpStar(starsLit);
            starsLit++;
        }
    }

    /// <summary>
    /// Highlights a single star and starts its pop animation. Fire-and-forget so it
    /// runs in parallel with the score count-up coroutine.
    /// </summary>
    private void LightUpStar(int index)
    {
        if (stars == null || index < 0 || index >= stars.Length || stars[index] == null)
        {
            return;
        }

        stars[index].color = starHighlightColor;

        if (gameSoundManager != null)
        {
            float pitch = Mathf.Lerp(scoreCountStartPitch, scoreCountEndPitch, (float)(index + 1) / Mathf.Max(1, stars.Length));
            gameSoundManager.PlayScoreCountSound(pitch);
        }

        StartCoroutine(StarPopAnimation(stars[index].transform));
    }

    private IEnumerator StarPopAnimation(Transform starTransform)
    {
        if (starTransform == null) yield break;

        float half = Mathf.Max(0.01f, starRevealPopDuration * 0.5f);
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            starTransform.localScale = Vector3.one * Mathf.Lerp(1f, starRevealPopScale, t);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            starTransform.localScale = Vector3.one * Mathf.Lerp(starRevealPopScale, 1f, t);
            yield return null;
        }
        starTransform.localScale = Vector3.one;
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

    // Codex Unlock Popup Methods

    /// <summary>
    /// Shows the codex unlock popup with the level name
    /// </summary>
    private void ShowCodexUnlockPopup(string levelName)
    {
        if (codexUnlockPopup == null || codexUnlockText == null) return;

        // Set the text
        codexUnlockText.text = LocalizationManager.Get("codex_unlock_format", levelName);

        // Play codex unlock sound
        if (gameSoundManager != null)
        {
            gameSoundManager.PlayCodexUnlockSound();
        }

        // Show the popup
        codexUnlockPopup.SetActive(true);

        // Start animation and auto-dismiss
        StartCoroutine(AnimateCodexPopup());
    }

    /// <summary>
    /// Dismisses the codex unlock popup
    /// </summary>
    private void DismissCodexPopup()
    {
        if (codexUnlockPopup == null) return;

        codexUnlockPopup.SetActive(false);
    }

    /// <summary>
    /// Animates the codex popup appearance and auto-dismisses after duration
    /// </summary>
    private IEnumerator AnimateCodexPopup()
    {
        if (codexUnlockPopup == null) yield break;

        RectTransform popupRect = codexUnlockPopup.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = codexUnlockPopup.GetComponent<CanvasGroup>();

        // Add CanvasGroup if it doesn't exist (for fading)
        if (canvasGroup == null)
        {
            canvasGroup = codexUnlockPopup.AddComponent<CanvasGroup>();
        }

        float elapsedTime = 0f;
        float animationDuration = 0.5f;

        // Appear animation: scale from large and fade in
        Vector3 startScale = Vector3.one * 0.5f;
        Vector3 targetScale = Vector3.one;

        popupRect.localScale = startScale;
        canvasGroup.alpha = 0f;

        // Fade in and scale up
        while (elapsedTime < animationDuration)
        {
            if (codexUnlockPopup == null || popupRect == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            popupRect.localScale = Vector3.Lerp(startScale, targetScale, progress);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
            yield return null;
        }

        // Ensure final values
        popupRect.localScale = targetScale;
        canvasGroup.alpha = 1f;

        // Hold for display duration
        yield return new WaitForSeconds(codexPopupDisplayDuration);

        // Disappear animation: fade out and scale down
        elapsedTime = 0f;
        startScale = targetScale;
        Vector3 endScale = Vector3.one * 0.8f;

        while (elapsedTime < animationDuration)
        {
            if (codexUnlockPopup == null || popupRect == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            popupRect.localScale = Vector3.Lerp(startScale, endScale, progress);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
            yield return null;
        }

        // Hide the popup
        DismissCodexPopup();
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
            if (isSubscribedToGameStart)
            {
                gameManager.OnGameStart -= OnGameStart;
                isSubscribedToGameStart = false;
            }
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnGameRestart -= OnGameRestart;
            gameManager.OnGameModeChanged -= OnGameModeChanged;
            gameManager.OnComboChanged -= UpdateComboDisplay;
            gameManager.OnConsecutivePerfectHitsChanged -= UpdateKukulkanWrathMeter;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
            stackManager.OnStackStraightened -= OnStackStraightened;
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

        if (gameTitleCoroutine != null)
        {
            StopCoroutine(gameTitleCoroutine);
        }

        // Stop combo timer and speed-run timer updates
        isUpdatingComboTimer = false;
        isUpdatingSpeedRunTimer = false;

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

        if (codexUnlockDismissButton != null)
        {
            codexUnlockDismissButton.onClick.RemoveListener(DismissCodexPopup);
        }

        // Stop perfect hit streak coroutine
        if (perfectHitStreakCoroutine != null)
        {
            StopCoroutine(perfectHitStreakCoroutine);
        }

        // Clean up spawned achievement notification UI
        if (achievementNotificationInstance != null)
        {
            Destroy(achievementNotificationInstance);
        }
    }

    /// <summary>
    /// Called when stack has been straightened - shows "Kukulkan's Shift" message
    /// </summary>
    private void OnStackStraightened()
    {
        if (kukulkanShiftText == null) return;

        // Stop any existing coroutine
        if (perfectHitStreakCoroutine != null)
        {
            StopCoroutine(perfectHitStreakCoroutine);
        }

        // Set the text and color
        kukulkanShiftText.text = LocalizationManager.Get("kukulkan_shift");
        kukulkanShiftText.color = kukulkanShiftColor;

        // Show the text
        kukulkanShiftText.gameObject.SetActive(true);

        // Start animation coroutine
        perfectHitStreakCoroutine = StartCoroutine(ShowKukulkanShiftAnimation());
    }

    /// <summary>
    /// Coroutine to animate the Kukulkan's Shift message
    /// </summary>
    private IEnumerator ShowKukulkanShiftAnimation()
    {
        if (kukulkanShiftText == null) yield break;

        RectTransform textRect = kukulkanShiftText.GetComponent<RectTransform>();
        if (textRect == null) yield break;

        Color originalColor = kukulkanShiftColor;

        // Appear animation: scale from large and fade in
        float elapsedTime = 0f;
        Vector3 startScale = Vector3.one * kukulkanShiftAppearScale;
        Vector3 targetScale = Vector3.one * kukulkanShiftFinalScale;

        textRect.localScale = startScale;
        kukulkanShiftText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

        // Fade in and scale down to final size
        while (elapsedTime < kukulkanShiftAnimationDuration)
        {
            if (kukulkanShiftText == null || textRect == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / kukulkanShiftAnimationDuration;
            float smoothProgress = progress * progress * (3f - 2f * progress); // Smooth step

            textRect.localScale = Vector3.Lerp(startScale, targetScale, smoothProgress);
            float alpha = Mathf.Lerp(0f, 1f, smoothProgress);
            kukulkanShiftText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            yield return null;
        }

        // Ensure final values
        textRect.localScale = targetScale;
        kukulkanShiftText.color = originalColor;

        // Hold for display duration (subtract animation time)
        float holdDuration = kukulkanShiftDisplayDuration - (kukulkanShiftAnimationDuration * 2);
        if (holdDuration > 0)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        // Disappear animation: fade out and scale down
        elapsedTime = 0f;
        Vector3 startScaleOut = targetScale;
        Vector3 endScale = Vector3.one * 0.8f;

        while (elapsedTime < kukulkanShiftAnimationDuration)
        {
            if (kukulkanShiftText == null || textRect == null) yield break;

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / kukulkanShiftAnimationDuration;
            float smoothProgress = progress * progress * (3f - 2f * progress); // Smooth step

            textRect.localScale = Vector3.Lerp(startScaleOut, endScale, smoothProgress);
            float alpha = Mathf.Lerp(1f, 0f, smoothProgress);
            kukulkanShiftText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            yield return null;
        }

        // Hide the text
        HideKukulkanShiftText();
    }

    /// <summary>
    /// Hides the Kukulkan's Shift text and resets its properties
    /// </summary>
    private void HideKukulkanShiftText()
    {
        if (kukulkanShiftText != null)
        {
            kukulkanShiftText.gameObject.SetActive(false);

            // Reset scale and color for next appearance
            RectTransform textRect = kukulkanShiftText.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.localScale = Vector3.one;
            }

            kukulkanShiftText.color = kukulkanShiftColor;
        }
    }

    // Kukulkan's Wrath Meter Methods

    /// <summary>
    /// Initializes the Kukulkan's Wrath meter - ensures it's always visible
    /// </summary>
    private void InitializeKukulkanWrathMeter()
    {
        // Always show the meter
        if (kukulkanWrathMeter != null)
        {
            kukulkanWrathMeter.SetActive(true);
        }

        // Initialize fill to empty
        if (kukulkanWrathFillImage != null)
        {
            kukulkanWrathFillImage.fillAmount = 0f;
        }

        // Update with current value if game is active
        if (gameManager != null)
        {
            UpdateKukulkanWrathMeter(gameManager.ConsecutivePerfectHits);
        }
    }

    /// <summary>
    /// Updates the Kukulkan's Wrath meter fill amount based on consecutive perfect hits
    /// </summary>
    /// <param name="consecutivePerfectHits">Current number of consecutive perfect hits</param>
    private void UpdateKukulkanWrathMeter(int consecutivePerfectHits)
    {
        // Ensure meter is visible
        if (kukulkanWrathMeter != null)
        {
            kukulkanWrathMeter.SetActive(true);
        }

        if (kukulkanWrathFillImage == null || gameManager == null) return;

        // Calculate fill amount: consecutivePerfectHits / perfectHitsRequired
        int perfectHitsRequired = gameManager.PerfectHitsRequired;
        if (perfectHitsRequired > 0)
        {
            float fillAmount = (float)consecutivePerfectHits / perfectHitsRequired;
            fillAmount = Mathf.Clamp01(fillAmount); // Ensure between 0 and 1

            kukulkanWrathFillImage.fillAmount = fillAmount;
        }
        else
        {
            // If perfect hits required is 0 or invalid, set to 0
            kukulkanWrathFillImage.fillAmount = 0f;
        }
    }
}
