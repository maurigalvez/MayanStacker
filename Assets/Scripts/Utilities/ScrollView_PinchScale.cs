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

    private float currentZoom;
    private float previousTouchDistance = 0f;

    private void Start()
    {
        // Initialize zoom at maximum scale
        currentZoom = maxZoom;
        if (mapRect != null)
        {
            mapRect.localScale = Vector3.one * currentZoom;
        }
    }

    private void OnEnable()
    {
        // Enable Enhanced Touch for multi-touch support
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        // Disable Enhanced Touch when component is disabled
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        //HandlePinchZoom();
        HandleMouseScrollWheel();
    }

    /// <summary>
    /// Handles pinch-to-zoom gesture on touch devices using the new Input System
    /// </summary>
    private void HandlePinchZoom()
    {
        // Check if there are exactly 2 active touches
        if (Touch.activeTouches.Count == 2)
        {
            Touch touchZero = Touch.activeTouches[0];
            Touch touchOne = Touch.activeTouches[1];

            // Calculate current distance between touches
            float currentDistance = Vector2.Distance(touchZero.screenPosition, touchOne.screenPosition);

            // On first frame of two-touch gesture, just store the distance
            if (touchZero.phase == UnityEngine.InputSystem.TouchPhase.Began ||
                touchOne.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                previousTouchDistance = currentDistance;
                return;
            }

            // Calculate the difference in distance
            float distanceDelta = currentDistance - previousTouchDistance;
            previousTouchDistance = currentDistance;

            // Apply zoom based on distance change
            Zoom(distanceDelta * 0.01f);
        }
        else
        {
            // Reset previous distance when not pinching
            previousTouchDistance = 0f;
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
    /// Zooms towards the center of the screen
    /// </summary>
    /// <param name="increment">The amount to zoom (positive = zoom in, negative = zoom out)</param>
    private void Zoom(float increment)
    {
        if (mapRect != null)
        {
            // Calculate new zoom level
            float newZoom = Mathf.Clamp(currentZoom + increment, minZoom, maxZoom);

            // Only proceed if zoom actually changed
            if (Mathf.Abs(newZoom - currentZoom) < 0.001f)
                return;

            // Get the center of the screen in world space
            RectTransform parentRect = mapRect.parent as RectTransform;
            if (parentRect != null)
            {
                // Center of the viewport (parent rect)
                Vector2 viewportCenter = parentRect.rect.center;

                // Convert viewport center to local point on the map (before scaling)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapRect,
                    RectTransformUtility.WorldToScreenPoint(null, parentRect.TransformPoint(viewportCenter)),
                    null,
                    out Vector2 localPointBeforeZoom
                );

                // Calculate the ratio of zoom change
                float zoomRatio = newZoom / currentZoom;

                // Apply the new zoom
                currentZoom = newZoom;
                mapRect.localScale = Vector3.one * currentZoom;

                // The local point will now be at a different screen position due to scaling
                // We need to adjust the anchored position to keep that point at the screen center
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
