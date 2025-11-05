using UnityEngine;

/// <summary>
/// Animates a GameObject's scale continuously between min and max values
/// Can be used for pulsing UI elements, floating objects, or any scale-based animation
/// </summary>
public class ScalePulseAnimation : MonoBehaviour
{
    [Header("Scale Settings")]
    [SerializeField] private Vector3 minScale = new Vector3(0.8f, 0.8f, 0.8f);
    [SerializeField] private Vector3 maxScale = new Vector3(1.2f, 1.2f, 1.2f);

    [Header("Animation Settings")]
    [SerializeField] private float speed = 1.0f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Options")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool unscaledTime = false;

    // State
    private bool isPlaying = false;
    private float currentTime = 0f;
    private Vector3 initialScale;

    private void Start()
    {
        // Store initial scale
        initialScale = transform.localScale;

        if (playOnStart)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!isPlaying) return;

        // Update time
        float deltaTime = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        currentTime += deltaTime * speed;

        // Calculate ping-pong value (0 to 1 and back)
        float t = Mathf.PingPong(currentTime, 1.0f);

        // Apply animation curve
        float curveValue = animationCurve.Evaluate(t);

        // Interpolate between min and max scale
        transform.localScale = Vector3.Lerp(minScale, maxScale, curveValue);
    }

    /// <summary>
    /// Starts the scale animation
    /// </summary>
    public void Play()
    {
        isPlaying = true;
        currentTime = 0f;
    }

    /// <summary>
    /// Pauses the scale animation
    /// </summary>
    public void Pause()
    {
        isPlaying = false;
    }

    /// <summary>
    /// Stops the animation and resets to initial scale
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
        currentTime = 0f;
        transform.localScale = initialScale;
    }

    /// <summary>
    /// Stops the animation and resets to minimum scale
    /// </summary>
    public void ResetToMin()
    {
        isPlaying = false;
        currentTime = 0f;
        transform.localScale = minScale;
    }

    /// <summary>
    /// Stops the animation and resets to maximum scale
    /// </summary>
    public void ResetToMax()
    {
        isPlaying = false;
        currentTime = 1f;
        transform.localScale = maxScale;
    }
}

