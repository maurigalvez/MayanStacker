using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Handles pinch-to-zoom and scroll-to-zoom for a UI ScrollView using the new Unity Input System
/// </summary>
public class ScrollView_PinchScale : MonoBehaviour, IDragHandler, IScrollHandler
{
    [Header("References")]
    [SerializeField] private RectTransform mapRect;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float minZoom = 0.5f;
    [SerializeField] private float maxZoom = 3f;
    [SerializeField] private float mouseScrollSensitivity = 0.1f;
    [SerializeField] private float pinchSensitivity = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool useFallbackTouchscreen = false;

    private float currentZoom;
    private float previousTouchDistance = 0f;
    private bool isPinching = false;

    private void Start()
    {
        // Initialize zoom at maximum scale
        currentZoom = maxZoom;
        if (mapRect != null)
        {
            mapRect.localScale = Vector3.one * currentZoom;
        }
    }

    private void Awake()
    {
#if UNITY_ANDROID || UNITY_IOS
        // Enable Enhanced Touch for multi-touch support on mobile platforms only
        if (!EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Enable();
            if (enableDebugLogs)
                Debug.Log("Enhanced Touch Support Enabled");
        }
#endif
    }

    private void OnEnable()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (!EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Enable();
            if (enableDebugLogs)
                Debug.Log("Enhanced Touch Support Enabled in OnEnable");
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_ANDROID || UNITY_IOS
        // Don't disable here as it might be needed by other scripts
        // EnhancedTouchSupport.Disable();
#endif
    }

    private void Update()
    {
#if UNITY_ANDROID || UNITY_IOS
        // Handle pinch zoom on mobile platforms only
        if (useFallbackTouchscreen)
        {
            HandlePinchZoomFallback();
        }
        else
        {
            HandlePinchZoom();
        }
#endif

#if UNITY_STANDALONE || UNITY_WEBGL || UNITY_EDITOR
        // Mouse scroll works on desktop, web, and editor only
        HandleMouseScrollWheel();
#endif
    }

    /// <summary>
    /// Handles pinch-to-zoom gesture on touch devices using the new Input System
    /// </summary>
    private void HandlePinchZoom()
    {
        int touchCount = Touch.activeTouches.Count;

        if (enableDebugLogs && touchCount > 0)
        {
            Debug.Log($"Active touches: {touchCount}");
        }

        // Check if there are exactly 2 active touches
        if (touchCount == 2)
        {
            Touch touchZero = Touch.activeTouches[0];
            Touch touchOne = Touch.activeTouches[1];

            // Calculate current distance between touches
            float currentDistance = Vector2.Distance(touchZero.screenPosition, touchOne.screenPosition);

            // Calculate the midpoint (pinch center) between the two touches
            Vector2 pinchCenter = (touchZero.screenPosition + touchOne.screenPosition) / 2f;

            // On first frame of two-touch gesture, just store the distance
            if (!isPinching ||
                touchZero.phase == UnityEngine.InputSystem.TouchPhase.Began ||
                touchOne.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                previousTouchDistance = currentDistance;
                isPinching = true;

                if (enableDebugLogs)
                    Debug.Log($"Started pinch - Initial distance: {currentDistance}");
                return;
            }

            // Calculate the difference in distance
            float distanceDelta = currentDistance - previousTouchDistance;

            if (enableDebugLogs && Mathf.Abs(distanceDelta) > 1f)
            {
                Debug.Log($"Pinch distance delta: {distanceDelta}, Current: {currentDistance}, Previous: {previousTouchDistance}");
            }

            previousTouchDistance = currentDistance;

            // Apply zoom based on distance change, using the pinch center as focal point
            if (Mathf.Abs(distanceDelta) > 0.1f) // Add threshold to avoid jitter
            {
                Zoom(distanceDelta * pinchSensitivity, pinchCenter);
            }
        }
        else
        {
            // Reset previous distance when not pinching
            if (isPinching && enableDebugLogs)
            {
                Debug.Log("Pinch ended");
            }
            previousTouchDistance = 0f;
            isPinching = false;
        }
    }

    /// <summary>
    /// Fallback pinch-to-zoom using Touchscreen API directly
    /// </summary>
    private void HandlePinchZoomFallback()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("No touchscreen detected!");
            return;
        }

        var touches = touchscreen.touches;
        int activeTouchCount = 0;
        UnityEngine.InputSystem.Controls.TouchControl touch0 = null;
        UnityEngine.InputSystem.Controls.TouchControl touch1 = null;

        // Count active touches
        for (int i = 0; i < touches.Count; i++)
        {
            if (touches[i].press.isPressed)
            {
                if (activeTouchCount == 0)
                    touch0 = touches[i];
                else if (activeTouchCount == 1)
                    touch1 = touches[i];

                activeTouchCount++;
            }
        }

        if (enableDebugLogs && activeTouchCount > 0)
        {
            Debug.Log($"Fallback - Active touches: {activeTouchCount}");
        }

        if (activeTouchCount == 2 && touch0 != null && touch1 != null)
        {
            Vector2 pos0 = touch0.position.ReadValue();
            Vector2 pos1 = touch1.position.ReadValue();

            float currentDistance = Vector2.Distance(pos0, pos1);

            // Calculate the midpoint (pinch center) between the two touches
            Vector2 pinchCenter = (pos0 + pos1) / 2f;

            if (!isPinching)
            {
                previousTouchDistance = currentDistance;
                isPinching = true;

                if (enableDebugLogs)
                    Debug.Log($"Fallback - Started pinch: {currentDistance}");
                return;
            }

            float distanceDelta = currentDistance - previousTouchDistance;

            if (enableDebugLogs && Mathf.Abs(distanceDelta) > 1f)
            {
                Debug.Log($"Fallback - Delta: {distanceDelta}");
            }

            previousTouchDistance = currentDistance;

            if (Mathf.Abs(distanceDelta) > 0.1f)
            {
                Zoom(distanceDelta * pinchSensitivity, pinchCenter);
            }
        }
        else
        {
            if (isPinching && enableDebugLogs)
            {
                Debug.Log("Fallback - Pinch ended");
            }
            previousTouchDistance = 0f;
            isPinching = false;
        }
    }

    /// <summary>
    /// Handles mouse scroll wheel zoom using the new Input System
    /// </summary>
    private void HandleMouseScrollWheel()
    {
        // Get scroll delta from mouse
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 scrollDelta = mouse.scroll.ReadValue();

            // Only process if there's actual scroll input
            if (scrollDelta.y != 0)
            {
                Zoom(scrollDelta.y * mouseScrollSensitivity);
            }
        }
    }

    /// <summary>
    /// Handles drag events from Unity's EventSystem (for panning the map)
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
#if UNITY_ANDROID || UNITY_IOS
        // Don't drag when pinching with two fingers
        if (isPinching || Touch.activeTouches.Count > 1)
        {
            if (enableDebugLogs)
                Debug.Log("Drag blocked - pinching active");
            return;
        }
#endif

        if (mapRect != null)
        {
            mapRect.anchoredPosition += eventData.delta;
        }
    }

    /// <summary>
    /// Handles scroll events from Unity's EventSystem (backup for mouse scroll)
    /// This provides compatibility with UI ScrollRect and other UI elements
    /// </summary>
    public void OnScroll(PointerEventData eventData)
    {
        Zoom(eventData.scrollDelta.y * zoomSpeed);
    }

    /// <summary>
    /// Applies zoom increment to the map and clamps it within min/max bounds
    /// Zooms towards a specific screen position or the center of the viewport
    /// </summary>
    /// <param name="increment">The amount to zoom (positive = zoom in, negative = zoom out)</param>
    /// <param name="screenPosition">Optional screen position to zoom towards. If null, zooms towards viewport center</param>
    private void Zoom(float increment, Vector2? screenPosition = null)
    {
        if (mapRect != null)
        {
            // Calculate new zoom level
            float newZoom = Mathf.Clamp(currentZoom + increment, minZoom, maxZoom);

            // Only proceed if zoom actually changed
            if (Mathf.Abs(newZoom - currentZoom) < 0.001f)
                return;

            // Get the zoom focal point
            RectTransform parentRect = mapRect.parent as RectTransform;
            if (parentRect != null)
            {
                Vector2 zoomFocalPoint;

                if (screenPosition.HasValue)
                {
                    // Use the provided screen position (e.g., pinch center)
                    zoomFocalPoint = screenPosition.Value;
                }
                else
                {
                    // Default to center of the viewport
                    Vector2 viewportCenter = parentRect.rect.center;
                    zoomFocalPoint = RectTransformUtility.WorldToScreenPoint(null, parentRect.TransformPoint(viewportCenter));
                }

                // Convert focal point to local point on the map (before scaling)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapRect,
                    zoomFocalPoint,
                    null,
                    out Vector2 localPointBeforeZoom
                );

                // Calculate the ratio of zoom change
                float zoomRatio = newZoom / currentZoom;

                // Apply the new zoom
                currentZoom = newZoom;
                mapRect.localScale = Vector3.one * currentZoom;

                // The local point will now be at a different screen position due to scaling
                // We need to adjust the anchored position to keep that point at the same screen position
                Vector2 offsetDelta = localPointBeforeZoom * (zoomRatio - 1f);
                mapRect.anchoredPosition -= offsetDelta;
            }
            else
            {
                // Fallback to simple zoom if no parent rect
                currentZoom = newZoom;
                mapRect.localScale = Vector3.one * currentZoom;
            }
        }
    }
}
