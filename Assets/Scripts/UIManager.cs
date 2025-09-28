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
    [SerializeField] private Button quitButton;

    [Header("Game UI")]
    [SerializeField] private GameObject gameUI;
    [SerializeField] private TextMeshProUGUI instructionsText;
    [SerializeField] private TextMeshProUGUI stackHeightText;
    [SerializeField] private TextMeshProUGUI landingAccuracyText;
    [SerializeField] private GameObject pointsPopupPrefab;
    [SerializeField] private Transform pointsPopupParent;

    [Header("Settings")]
    [SerializeField] private string scoreFormat = "Score: {0}";
    [SerializeField] private string highScoreFormat = "Best: {0}";
    [SerializeField] private string finalScoreFormat = "Final Score: {0}";
    [SerializeField] private string newHighScoreMessage = "NEW HIGH SCORE!";
    [SerializeField] private string stackHeightFormat = "Height: {0}";
    [SerializeField] private float landingAccuracyDisplayDuration = 2f;
    [SerializeField] private float pointsPopupDuration = 2f;
    [SerializeField] private float pointsPopupVerticalOffset = 30f;

    // References
    private GameManager gameManager;
    private StackManager stackManager;

    // State
    private Coroutine landingAccuracyCoroutine;

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

        // Subscribe to game events
        if (gameManager != null)
        {
            gameManager.OnScoreChanged += UpdateScore;
            gameManager.OnHighScoreChanged += UpdateHighScore;
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
        }

        // Subscribe to stack events
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }

        // Set up button listeners
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
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

        // Update initial scores
        if (gameManager != null)
        {
            UpdateScore(gameManager.CurrentScore);
            UpdateHighScore(gameManager.HighScore);
        }

        // Show instructions
        ShowInstructions();

        // Initialize new UI elements
        UpdateStackHeight();
        HideLandingAccuracy();
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

        // Hide instructions after a delay
        Invoke(nameof(HideInstructions), 3f);
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

        // Show instructions again
        ShowInstructions();
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
            int points = CalculatePointsFromAccuracy(stackableObject.LandingAccuracy);
            ShowLandingAccuracyAndPoints(stackableObject.LandingAccuracy, points, stackableObject.transform.position);
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

        // Set the text based on accuracy
        if (accuracy >= 0.9f)
        {
            landingAccuracyText.text = "PERFECT!";
            landingAccuracyText.color = Color.green;
        }
        else if (accuracy >= 0.6f)
        {
            landingAccuracyText.text = "GOOD";
            landingAccuracyText.color = Color.yellow;
        }
        else
        {
            landingAccuracyText.text = "POOR";
            landingAccuracyText.color = Color.red;
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

            // Color code based on points
            if (points >= 100)
                popupText.color = Color.green;
            else if (points >= 50)
                popupText.color = Color.yellow;
            else
                popupText.color = Color.red;
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

    private void RestartGame()
    {
        if (gameManager != null)
        {
            gameManager.RestartGame();
        }
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }

        // Stop any running coroutines
        if (landingAccuracyCoroutine != null)
        {
            StopCoroutine(landingAccuracyCoroutine);
        }

        // Remove button listeners
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }
    }
}
