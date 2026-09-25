using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Pinch-to-zoom (touch) and wheel-to-zoom (mouse) for the level map.
/// One-finger panning belongs to the LevelMapScrollRect on the same object; while two fingers
/// are down this component owns the map: it zooms around the point between the fingers and
/// pans with them, and tells the ScrollRect to stand down so the two don't fight.
/// </summary>
public class ScrollView_PinchScale : MonoBehaviour, IScrollHandler
{
    [Header("References")]
    [SerializeField] private RectTransform mapRect;
    private ScrollRect scrollRect;
    private LevelMapScrollRect mapScrollRect;

    [Header("Zoom Settings")]
    [SerializeField] private float minZoom = 0.5f;
    [SerializeField] private float maxZoom = 7f;
    [Tooltip("Zoom factor applied per mouse-wheel notch")]
    [SerializeField] private float wheelZoomStep = 1.15f;

    [Header("Animation Settings")]
    [SerializeField] private float centeringAnimationDuration = 0.5f;
    [SerializeField] private AnimationCurve centeringAnimationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // How long after a pinch level buttons keep ignoring taps, so lifting the
    // finger that held still during the pinch doesn't open a level
    private const float TapBlockWindow = 0.15f;
    // Fingers closer than this (pixels) give an unstable zoom ratio
    private const float MinPinchDistance = 10f;

    private static float lastPinchTime = float.NegativeInfinity;

    private float currentZoom;
    private bool isPinching;
    private bool pinchRejected;
    private bool gestureHadPinch;
    private float previousTouchDistance;
    private Vector2 previousTouchCenter;
    private Camera uiCamera;
    private Coroutine centeringAnimationCoroutine;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    // Public property to access current zoom
    public float CurrentZoom => currentZoom;

    /// <summary>
    /// True during a pinch and briefly after, so level buttons can ignore taps that are really
    /// the end of a zoom gesture.
    /// </summary>
    public static bool BlocksTaps => Time.unscaledTime - lastPinchTime < TapBlockWindow;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<ScrollView_PinchScale>(this);

        scrollRect = GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = GetComponentInParent<ScrollRect>();
        }
        mapScrollRect = scrollRect as LevelMapScrollRect;

        if (scrollRect == null)
            Debug.LogWarning("ScrollView_PinchScale: No ScrollRect found");
        else if (mapScrollRect == null)
            Debug.LogWarning("ScrollView_PinchScale: ScrollRect is not a LevelMapScrollRect, two-finger pinch will fight the scroll drag");

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.rootCanvas.worldCamera;
    }

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
        // Needed for multi-touch; left enabled on disable since other scripts may rely on it
        if (!EnhancedTouchSupport.enabled)
            EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EndPinch();
        gestureHadPinch = false;
        pinchRejected = false;
    }

    private void Update()
    {
        if (mapRect == null) return;

        var touches = Touch.activeTouches;

        if (touches.Count >= 2)
        {
            HandlePinch(touches[0], touches[1]);
        }
        else
        {
            if (isPinching) EndPinch();
            pinchRejected = false;
        }

        // Keep blocking taps until every finger of a pinch gesture has lifted
        if (touches.Count == 0)
            gestureHadPinch = false;
        if (gestureHadPinch)
            lastPinchTime = Time.unscaledTime;
    }

    private void HandlePinch(Touch touchZero, Touch touchOne)
    {
        Vector2 posZero = touchZero.screenPosition;
        Vector2 posOne = touchOne.screenPosition;
        float distance = Vector2.Distance(posZero, posOne);
        Vector2 center = (posZero + posOne) * 0.5f;

        if (!isPinching)
        {
            if (pinchRejected) return;
            if (!IsOverMap(touchZero.startScreenPosition) || !IsOverMap(touchOne.startScreenPosition))
            {
                // Pinch started on a popup or another panel, leave the map alone until the fingers lift
                pinchRejected = true;
                return;
            }
            BeginPinch(distance, center);
            return;
        }

        // Pan with the fingers, then scale around the point between them
        RectTransform parentRect = mapRect.parent as RectTransform;
        if (parentRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, previousTouchCenter, uiCamera, out Vector2 previousLocal) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, center, uiCamera, out Vector2 currentLocal))
        {
            mapRect.anchoredPosition += currentLocal - previousLocal;
        }

        if (previousTouchDistance > MinPinchDistance && distance > MinPinchDistance)
        {
            ZoomTo(currentZoom * (distance / previousTouchDistance), center);
        }

        previousTouchDistance = distance;
        previousTouchCenter = center;
    }

    private void BeginPinch(float distance, Vector2 center)
    {
        isPinching = true;
        gestureHadPinch = true;
        previousTouchDistance = distance;
        previousTouchCenter = center;

        if (mapScrollRect != null)
            mapScrollRect.DragSuppressed = true;

        if (centeringAnimationCoroutine != null)
        {
            StopCoroutine(centeringAnimationCoroutine);
            centeringAnimationCoroutine = null;
        }

        if (enableDebugLogs)
            Debug.Log($"ScrollView_PinchScale: Pinch started - distance {distance}, zoom {currentZoom}");
    }

    private void EndPinch()
    {
        if (!isPinching) return;
        isPinching = false;

        if (mapScrollRect != null)
            mapScrollRect.DragSuppressed = false;

        if (enableDebugLogs)
            Debug.Log($"ScrollView_PinchScale: Pinch ended - zoom {currentZoom}");
    }

    /// <summary>
    /// True when the topmost UI element under the screen point belongs to this map,
    /// so pinching on a popup drawn over the map doesn't zoom it.
    /// </summary>
    private bool IsOverMap(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return true;

        var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
        raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, raycastResults);
        return raycastResults.Count > 0 && raycastResults[0].gameObject.transform.IsChildOf(transform);
    }

    /// <summary>
    /// Mouse wheel zoom (desktop and Editor), towards the cursor
    /// </summary>
    public void OnScroll(PointerEventData eventData)
    {
        float scroll = eventData.scrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        ZoomTo(currentZoom * (scroll > 0f ? wheelZoomStep : 1f / wheelZoomStep), eventData.position);
    }

    /// <summary>
    /// Sets the zoom, clamped to [minZoom, maxZoom], keeping the map point under
    /// screenPosition where it is on screen. The ScrollRect clamps the result to the viewport.
    /// </summary>
    private void ZoomTo(float targetZoom, Vector2 screenPosition)
    {
        float newZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        if (Mathf.Approximately(newZoom, currentZoom)) return;

        // Local point relative to the map pivot; it sits at anchoredPosition + point * zoom in the parent
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRect, screenPosition, uiCamera, out Vector2 focalPoint))
        {
            mapRect.anchoredPosition -= focalPoint * (newZoom - currentZoom);
        }

        currentZoom = newZoom;
        mapRect.localScale = Vector3.one * currentZoom;
    }


    /// <summary>
    /// Centers the map view on a specific RectTransform (e.g., a level button)
    /// Requires ScrollRect to be present
    /// </summary>
    /// <param name="targetRect">The RectTransform to center on</param>
    /// <param name="animate">Whether to animate the centering (default: false for instant)</param>
    public void CenterOnRectTransform(RectTransform targetRect, bool animate = false)
    {
        if (scrollRect == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("ScrollView_PinchScale: Cannot center - ScrollRect is required");
            return;
        }

        if (mapRect == null || targetRect == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("ScrollView_PinchScale: Cannot center - mapRect or targetRect is null");
            return;
        }

        CenterOnRectTransformWithScrollRect(targetRect, animate);
    }

    /// <summary>
    /// Centers using ScrollRect's scroll position, accounting for content scaling
    /// Map is both content and mapRect, and it scales 3-7x
    /// Numbered containers also have their own scale (not 1)
    /// Target buttons are nested: Map -> Locations -> numbered containers -> Level Button(Clone)
    /// Works by calculating where target is relative to content pivot, then adjusting anchoredPosition
    /// </summary>
    private void CenterOnRectTransformWithScrollRect(RectTransform target, bool animate)
    {
        ScrollRect scroll = scrollRect;
        RectTransform content = scroll.content;
        RectTransform viewport = scroll.viewport;

        if (content == null || viewport == null || target == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("ScrollView_PinchScale: Cannot center - content, viewport, or target is null");
            return;
        }

        // Force canvas update to ensure layout is correct
        Canvas.ForceUpdateCanvases();

        // Get the canvas for coordinate conversion
        Canvas canvas = content.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;

        // --- 1. Get target's center position in content's local unscaled space ---
        Vector2 targetRectCenter = target.rect.center;
        Vector3 targetCenterWorld = target.TransformPoint(targetRectCenter);
        Vector2 targetCenterScreen = RectTransformUtility.WorldToScreenPoint(cam, targetCenterWorld);

        Vector2 targetInContentSpace;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content,
            targetCenterScreen,
            cam,
            out targetInContentSpace
        );

        // --- 2. Calculate the required anchoredPosition to center the target ---
        // Key relationship: When content is scaled by S, a point at position P in content's local unscaled space
        // appears at position: anchoredPosition + P * S in viewport space.
        // 
        // We want the target (at targetInContentSpace) to be at the viewport center.
        // Viewport center is at viewport.rect.center in viewport space (typically 0,0).
        //
        // Therefore: viewport.rect.center = anchoredPosition + targetInContentSpace * scale
        // Solving for anchoredPosition: anchoredPosition = viewport.rect.center - targetInContentSpace * scale

        Vector3 contentScale = content.localScale;
        Vector2 viewportCenter = viewport.rect.center;

        Vector2 newAnchoredPos = viewportCenter - new Vector2(
            targetInContentSpace.x * contentScale.x,
            targetInContentSpace.y * contentScale.y
        );

        // --- 3. Clamp to scroll bounds (optional) ---
        //newAnchoredPos.x = Mathf.Clamp(newAnchoredPos.x, -GetRightClamp(), GetLeftClamp());
        //newAnchoredPos.y = Mathf.Clamp(newAnchoredPos.y, -GetBottomClamp(), GetTopClamp());

        // --- 4. Apply position (with animation if requested) ---
        Vector2 startPos = content.anchoredPosition;

        if (animate && centeringAnimationDuration > 0f)
        {
            // Stop any existing animation
            if (centeringAnimationCoroutine != null)
            {
                StopCoroutine(centeringAnimationCoroutine);
            }

            // Start new animation
            centeringAnimationCoroutine = StartCoroutine(AnimateCentering(content, startPos, newAnchoredPos, centeringAnimationDuration));
        }
        else
        {
            // Set position instantly
            content.anchoredPosition = newAnchoredPos;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"ScrollView_PinchScale: Centering - Target: {target.name}, " +
                     $"Target in content space: {targetInContentSpace}, " +
                     $"Viewport center: {viewportCenter}, " +
                     $"Target * scale: ({targetInContentSpace.x * contentScale.x}, {targetInContentSpace.y * contentScale.y}), " +
                     $"Start anchoredPos: {startPos}, " +
                     $"Target anchoredPos: {newAnchoredPos}, " +
                     $"Animate: {animate}, Content scale: {contentScale}");
        }
    }

    /// <summary>
    /// Coroutine to smoothly animate the content's anchoredPosition from start to target
    /// </summary>
    private System.Collections.IEnumerator AnimateCentering(RectTransform content, Vector2 startPos, Vector2 targetPos, float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // Apply animation curve if provided
            if (centeringAnimationCurve != null && centeringAnimationCurve.length > 0)
            {
                t = centeringAnimationCurve.Evaluate(t);
            }

            // Smoothly interpolate between start and target position
            content.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);

            yield return null;
        }

        // Ensure we end exactly at the target position
        content.anchoredPosition = targetPos;
        centeringAnimationCoroutine = null;

        if (enableDebugLogs)
        {
            Debug.Log($"ScrollView_PinchScale: Centering animation complete");
        }
    }

    private float GetLeftClamp()   // content cannot move more right than this
    {
        float contentWidth = scrollRect.content.rect.width;
        float viewportWidth = scrollRect.viewport.rect.width;

        float pivot = scrollRect.content.pivot.x;

        // how far the content can move so the LEFT edge aligns with viewport LEFT
        return (contentWidth * pivot) - (viewportWidth * 0.5f);
    }

    private float GetRightClamp()  // content cannot move more left than this
    {
        float contentWidth = scrollRect.content.rect.width;
        float viewportWidth = scrollRect.viewport.rect.width;

        float pivot = scrollRect.content.pivot.x;

        // how far the content can move so the RIGHT edge aligns with viewport RIGHT
        return (contentWidth * (1f - pivot)) - (viewportWidth * 0.5f);
    }

    private float GetTopClamp()    // content cannot move more down than this
    {
        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;

        float pivot = scrollRect.content.pivot.y;

        return (contentHeight * (1f - pivot)) - (viewportHeight * 0.5f);
    }

    private float GetBottomClamp() // content cannot move more up than this
    {
        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;

        float pivot = scrollRect.content.pivot.y;

        return (contentHeight * pivot) - (viewportHeight * 0.5f);
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
        // Stop any running animation
        if (centeringAnimationCoroutine != null)
        {
            StopCoroutine(centeringAnimationCoroutine);
            centeringAnimationCoroutine = null;
        }

        // Unregister from dependency registry
        DependencyRegistry.Unregister<ScrollView_PinchScale>(this);
    }
}
