using UnityEngine;

/// <summary>
/// Animates a GameObject's position continuously between two points
/// Can be used for moving platforms, floating objects, UI elements, or any position-based animation
/// </summary>
public class PositionPingPongAnimation : MonoBehaviour
{
    [Header("Position Settings")]
    [SerializeField] private Vector3 positionA = Vector3.zero;
    [SerializeField] private Vector3 positionB = new Vector3(0, 2, 0);
    [SerializeField] private bool useLocalPosition = true;
    [SerializeField] private bool setPositionAOnStart = false;

    [Header("Animation Settings")]
    [SerializeField] private float speed = 1.0f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Options")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool unscaledTime = false;

    // State
    private bool isPlaying = false;
    private float currentTime = 0f;
    private Vector3 initialPosition;

    private void Start()
    {
        // Store initial position
        initialPosition = useLocalPosition ? transform.localPosition : transform.position;

        // Optionally set to position A at start
        if (setPositionAOnStart)
        {
            if (useLocalPosition)
                transform.localPosition = positionA;
            else
                transform.position = positionA;
        }

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

        // Interpolate between position A and B
        Vector3 newPosition = Vector3.Lerp(positionA, positionB, curveValue);

        if (useLocalPosition)
            transform.localPosition = newPosition;
        else
            transform.position = newPosition;
    }

    /// <summary>
    /// Starts the position animation
    /// </summary>
    public void Play()
    {
        isPlaying = true;
        currentTime = 0f;
    }

    /// <summary>
    /// Pauses the position animation
    /// </summary>
    public void Pause()
    {
        isPlaying = false;
    }

    /// <summary>
    /// Stops the animation and resets to initial position
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
        currentTime = 0f;

        if (useLocalPosition)
            transform.localPosition = initialPosition;
        else
            transform.position = initialPosition;
    }

    /// <summary>
    /// Stops the animation and moves to position A
    /// </summary>
    public void MoveToA()
    {
        isPlaying = false;
        currentTime = 0f;

        if (useLocalPosition)
            transform.localPosition = positionA;
        else
            transform.position = positionA;
    }

    /// <summary>
    /// Stops the animation and moves to position B
    /// </summary>
    public void MoveToB()
    {
        isPlaying = false;
        currentTime = 1f;

        if (useLocalPosition)
            transform.localPosition = positionB;
        else
            transform.position = positionB;
    }

    /// <summary>
    /// Sets position A to the current position
    /// </summary>
    [ContextMenu("Set Position A to Current")]
    public void SetPositionAToCurrent()
    {
        positionA = useLocalPosition ? transform.localPosition : transform.position;
    }

    /// <summary>
    /// Sets position B to the current position
    /// </summary>
    [ContextMenu("Set Position B to Current")]
    public void SetPositionBToCurrent()
    {
        positionB = useLocalPosition ? transform.localPosition : transform.position;
    }
}

