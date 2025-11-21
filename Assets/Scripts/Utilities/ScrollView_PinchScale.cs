using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Handles pinch-to-zoom and scroll-to-zoom for a UI ScrollView using the new Unity Input System
/// </summary>
public class ScrollView_PinchScale : MonoBehaviour, IDragHandler, IScrollHandler
{
    [Header("References")]
    [SerializeField] private RectTransform mapRect;
    private ScrollRect scrollRect;

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
        // Ensure ScrollRect is found (in case it wasn't found in Awake)
        if (scrollRect == null)
        {
            scrollRect = GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                scrollRect = GetComponentInParent<ScrollRect>();
            }
        }

        if (enableDebugLogs)
        {
            if (scrollRect != null)
            {
                Debug.Log($"ScrollView_PinchScale: ScrollRect found - Content: {scrollRect.content?.name}, Viewport: {scrollRect.viewport?.name}");
            }
            else
            {
                Debug.LogWarning("ScrollView_PinchScale: No ScrollRect found");
            }
        }

        // Initialize zoom at maximum scale
        currentZoom = maxZoom;
        if (mapRect != null)
        {
            mapRect.localScale = Vector3.one * currentZoom;
        }
    }

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<ScrollView_PinchScale>(this);

        // Try to find ScrollRect early (might be on this GameObject or parent)
        scrollRect = GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = GetComponentInParent<ScrollRect>();
        }

        if (enableDebugLogs && scrollRect != null)
        {
            Debug.Log($"ScrollView_PinchScale: Found ScrollRect in Awake");
        }

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

    /// <summary>
    /// Centers the map view on a specific RectTransform (e.g., a level button)
    /// </summary>
    /// <param name="targetRect">The RectTransform to center on</param>
    /// <param name="animate">Whether to animate the centering (default: false for instant)</param>
    public void CenterOnRectTransform(RectTransform targetRect, bool animate = false)
    {
        if (mapRect == null || targetRect == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("ScrollView_PinchScale: Cannot center - mapRect or targetRect is null");
            return;
        }

        // If we have a ScrollRect, use its normalized position for centering
        if (scrollRect != null)
        {
            CenterOnRectTransformWithScrollRect(targetRect, animate);
            return;
        }

        // Fallback to direct anchoredPosition manipulation
        RectTransform viewportRect = mapRect.parent as RectTransform;
        if (viewportRect == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("ScrollView_PinchScale: Cannot center - viewport parent is null and no ScrollRect found");
            return;
        }

        // Get the target's position in the map's local coordinate space
        Vector2 targetPosInMap = mapRect.InverseTransformPoint(targetRect.position);

        // Get the viewport center in the map's local coordinate space
        Vector2 viewportCenterInMap = mapRect.InverseTransformPoint(viewportRect.position);

        // Calculate how much we need to move the map to center the target
        Vector2 offset = viewportCenterInMap - targetPosInMap;

        // Apply the offset to center the target
        mapRect.anchoredPosition += offset;

        if (enableDebugLogs)
        {
            Debug.Log($"ScrollView_PinchScale: Centered on {targetRect.name} (no ScrollRect). " +
                     $"Target pos in map: {targetPosInMap}, Viewport center in map: {viewportCenterInMap}, Offset: {offset}, " +
                     $"New anchored position: {mapRect.anchoredPosition}");
        }
    }

    /// <summary>
    /// Centers using ScrollRect's normalized position (more reliable for ScrollRect-based UI)
    /// </summary>
    private void CenterOnRectTransformWithScrollRect(RectTransform targetRect, bool animate)
    {
        if (scrollRect == null || scrollRect.content == null || targetRect == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("ScrollView_PinchScale: Cannot center with ScrollRect - missing components");
            return;
        }

        // Force canvas update to ensure layout is complete
        Canvas.ForceUpdateCanvases();

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

        if (viewport == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("ScrollView_PinchScale: Cannot get viewport");
            return;
        }

        // Get the target's position in content's local space
        Vector2 targetPosInContent = content.InverseTransformPoint(targetRect.position);

        // Get viewport bounds in content's local space
        Vector3[] viewportCorners = new Vector3[4];
        viewport.GetWorldCorners(viewportCorners);

        // Convert viewport corners to content's local space
        Vector2 viewportMin = content.InverseTransformPoint(viewportCorners[0]);
        Vector2 viewportMax = content.InverseTransformPoint(viewportCorners[2]);
        Vector2 viewportSize = viewportMax - viewportMin;
        Vector2 viewportCenter = viewportMin + viewportSize * 0.5f;

        // Calculate the offset needed to center the target
        Vector2 offset = viewportCenter - targetPosInContent;

        // Store current position
        Vector2 currentPosition = content.anchoredPosition;

        // Apply the offset
        content.anchoredPosition += offset;

        // Force another canvas update after moving
        Canvas.ForceUpdateCanvases();

        if (enableDebugLogs)
        {
            Debug.Log($"ScrollView_PinchScale: Centered on {targetRect.name} (using ScrollRect). " +
                     $"Target world pos: {targetRect.position}, Target in content: {targetPosInContent}, " +
                     $"Viewport center in content: {viewportCenter}, " +
                     $"Offset: {offset}, Old position: {currentPosition}, New position: {content.anchoredPosition}");
        }
    }

    /// <summary>
    /// Centers the map on the current level based on player progress
    /// Finds the current level via LevelManager and MainMenuManager, then centers on that level's button
    /// </summary>
    public void CenterMapOnCurrentLevel()
    {
        if (enableDebugLogs)
            Debug.Log("ScrollView_PinchScale: CenterMapOnCurrentLevel called");

        // Force canvas update to ensure layout is complete
        Canvas.ForceUpdateCanvases();

        // Get LevelManager via DependencyRegistry
        var levelManager = DependencyRegistry.Find<LevelManager>();
        if (levelManager == null)
        {
            Debug.LogWarning("ScrollView_PinchScale: LevelManager not found via DependencyRegistry");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"ScrollView_PinchScale: Found LevelManager with {levelManager.TotalLevels} total levels");

        // Get MainMenuManager via DependencyRegistry to access level buttons
        var mainMenuManager = DependencyRegistry.Find<MainMenuManager>();
        if (mainMenuManager == null)
        {
            Debug.LogWarning("ScrollView_PinchScale: MainMenuManager not found via DependencyRegistry");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"ScrollView_PinchScale: Found MainMenuManager with {mainMenuManager.GetLevelButtonCount()} level buttons");

        // Get the current level index
        int currentLevelIndex = GetCurrentLevelIndex(levelManager);

        if (enableDebugLogs)
            Debug.Log($"ScrollView_PinchScale: Current level index: {currentLevelIndex} (level {currentLevelIndex + 1})");

        // Get the level button for the current level
        var levelButton = mainMenuManager.GetLevelButton(currentLevelIndex);
        if (levelButton != null)
        {
            RectTransform buttonRect = levelButton.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                if (enableDebugLogs)
                    Debug.Log($"ScrollView_PinchScale: Found level button RectTransform: {buttonRect.name} at world position {buttonRect.position}, local position {buttonRect.localPosition}");

                // Small delay to ensure everything is laid out
                StartCoroutine(CenterOnButtonDelayed(buttonRect));
            }
            else
            {
                Debug.LogWarning($"ScrollView_PinchScale: Level button found but RectTransform is null");
            }
        }
        else
        {
            Debug.LogWarning($"ScrollView_PinchScale: Could not find level button for index {currentLevelIndex}. " +
                           $"MainMenuManager has {mainMenuManager.GetLevelButtonCount()} buttons.");
        }
    }

    /// <summary>
    /// Coroutine to center on button after a small delay to ensure layout is complete
    /// </summary>
    private System.Collections.IEnumerator CenterOnButtonDelayed(RectTransform buttonRect)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        CenterOnRectTransform(buttonRect, animate: false);

        if (enableDebugLogs)
            Debug.Log($"ScrollView_PinchScale: Centering complete");
    }

    /// <summary>
    /// Find the current level index (highest unlocked level with progress, or next playable level)
    /// This represents the player's current progress in the game
    /// </summary>
    /// <param name="levelManager">The LevelManager instance</param>
    /// <returns>Level index of the current level, or 0 if none found</returns>
    private int GetCurrentLevelIndex(LevelManager levelManager)
    {
        if (levelManager == null) return 0;

        int totalLevels = levelManager.TotalLevels;
        int highestLevelWithProgress = -1;

        // Find the highest level that has at least 1 star (completed)
        // This represents the player's furthest progress
        for (int i = totalLevels - 1; i >= 0; i--)
        {
            int levelNumber = i + 1; // Convert to 1-based
            bool isUnlocked = levelManager.IsLevelUnlocked(levelNumber);
            int stars = levelManager.GetLevelStars(levelNumber);

            if (isUnlocked && stars > 0)
            {
                highestLevelWithProgress = i;
                break; // Found the highest, no need to continue
            }
        }

        // If we found a level with progress, try to return the next unlocked level
        if (highestLevelWithProgress >= 0)
        {
            // Check if there's a next level that's unlocked
            int nextLevelIndex = highestLevelWithProgress + 1;
            if (nextLevelIndex < totalLevels)
            {
                int nextLevelNumber = nextLevelIndex + 1;
                if (levelManager.IsLevelUnlocked(nextLevelNumber))
                {
                    // Return the next level (the one they should play next)
                    return nextLevelIndex;
                }
            }
            // If no next level is unlocked, return the highest level with progress
            return highestLevelWithProgress;
        }

        // If no progress, return the first playable level (should be level 1)
        int nextPlayable = GetNextPlayableLevelIndex(levelManager);
        return nextPlayable >= 0 ? nextPlayable : 0;
    }

    /// <summary>
    /// Find the next playable level (first unlocked level with 0 stars)
    /// </summary>
    /// <param name="levelManager">The LevelManager instance</param>
    /// <returns>Level index of the next playable level, or -1 if none found</returns>
    private int GetNextPlayableLevelIndex(LevelManager levelManager)
    {
        if (levelManager == null) return -1;

        int totalLevels = levelManager.TotalLevels;

        // Find the first unlocked level that hasn't been completed (0 stars)
        for (int i = 0; i < totalLevels; i++)
        {
            int levelNumber = i + 1; // Convert to 1-based
            bool isUnlocked = levelManager.IsLevelUnlocked(levelNumber);
            int stars = levelManager.GetLevelStars(levelNumber);

            if (isUnlocked && stars == 0)
            {
                return i; // Return the index (0-based)
            }
        }

        // If all levels are completed, return -1
        return -1;
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<ScrollView_PinchScale>(this);
    }
}
