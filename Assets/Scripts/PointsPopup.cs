using TMPro;
using UnityEngine;

public class PointsPopup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI pointsText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float moveUpDistance = 50f;

    [Header("Scale Pop")]
    [Tooltip("Scale the popup starts at before popping in")]
    [SerializeField] private float popStartScale = 0.3f;
    [Tooltip("Peak scale of the overshoot before settling")]
    [SerializeField] private float popOvershootScale = 1.35f;
    [Tooltip("How long the pop-in (with overshoot) takes, in seconds")]
    [SerializeField] private float popDuration = 0.25f;
    [Tooltip("Extra scale multiplier applied to high-value (Perfect) popups")]
    [SerializeField] private float bigPointsScaleBonus = 1.25f;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private float elapsedTime = 0f;
    private bool isAnimating = false;
    private float settleScale = 1f;

    private void Awake()
    {
        // Get components if not assigned
        if (pointsText == null)
            pointsText = GetComponentInChildren<TextMeshProUGUI>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // Disable raycast target to prevent blocking input
        if (pointsText != null)
            pointsText.raycastTarget = false;

        // Initialize canvas group
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    public void Initialize(int points, Vector3 worldPosition)
    {
        // Set the points text
        if (pointsText != null)
        {
            pointsText.text = $"+{points}";

            // Color code based on points
            if (points >= 100)
                pointsText.color = Color.green;
            else if (points >= 50)
                pointsText.color = Color.yellow;
            else
                pointsText.color = Color.red;
        }

        // High-value landings pop a little bigger for extra punch.
        settleScale = points >= 100 ? bigPointsScaleBonus : 1f;

        // Convert world position to screen position
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.position = screenPosition;
            startPosition = rectTransform.position;
            endPosition = startPosition + Vector3.up * moveUpDistance;
        }

        // Start the animation
        StartAnimation();
    }

    private void StartAnimation()
    {
        isAnimating = true;
        elapsedTime = 0f;

        // Begin small so the pop-in reads as a punch.
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * (popStartScale * settleScale);
        }
    }

    /// <summary>
    /// Scale curve: overshoot past the settle scale then ease back, for an elastic pop.
    /// </summary>
    private float EvaluatePopScale()
    {
        if (elapsedTime >= popDuration)
        {
            return settleScale;
        }

        float t = Mathf.Clamp01(elapsedTime / popDuration);
        // Rise quickly to the overshoot peak (~40% in), then settle back down.
        float shaped;
        if (t < 0.4f)
        {
            float k = t / 0.4f;
            shaped = Mathf.Lerp(popStartScale, popOvershootScale, k * k * (3f - 2f * k));
        }
        else
        {
            float k = (t - 0.4f) / 0.6f;
            shaped = Mathf.Lerp(popOvershootScale, 1f, k * k * (3f - 2f * k));
        }
        return shaped * settleScale;
    }

    private void Update()
    {
        if (!isAnimating) return;

        elapsedTime += Time.deltaTime;
        float totalDuration = fadeInDuration + displayDuration + fadeOutDuration;

        if (elapsedTime >= totalDuration)
        {
            // Animation complete, destroy the popup
            Destroy(gameObject);
            return;
        }

        // Update position (move up over time)
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            float moveProgress = Mathf.Clamp01(elapsedTime / (fadeInDuration + displayDuration + fadeOutDuration));
            rectTransform.position = Vector3.Lerp(startPosition, endPosition, moveProgress);

            // Elastic scale pop on appearance, then hold at settle scale.
            rectTransform.localScale = Vector3.one * EvaluatePopScale();
        }

        // Update alpha based on phase
        if (canvasGroup != null)
        {
            if (elapsedTime <= fadeInDuration)
            {
                // Fade in
                float fadeInProgress = elapsedTime / fadeInDuration;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, fadeInProgress);
            }
            else if (elapsedTime <= fadeInDuration + displayDuration)
            {
                // Stay visible
                canvasGroup.alpha = 1f;
            }
            else
            {
                // Fade out
                float fadeOutProgress = (elapsedTime - fadeInDuration - displayDuration) / fadeOutDuration;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeOutProgress);
            }
        }
    }

    private void OnDestroy()
    {
        isAnimating = false;
    }
}
