using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders paths between unlocked level buttons, showing the progression path
/// </summary>
public class LevelPathRenderer : MonoBehaviour
{
    [Header("Path Settings")]
    [SerializeField] private float pathWidth = 5f;
    [SerializeField] private Color pathColor = new Color(1f, 0.8f, 0.2f, 0.8f); // Gold/yellow color (default for all paths)
    [SerializeField] private Sprite pathSprite; // Optional sprite for the path (if null, uses default white sprite)

    [Header("Next Level Path (Highlighted)")]
    [Tooltip("The path from current selected level to next available level will use this color and width")]
    [SerializeField] private Color nextLevelPathColor = new Color(1f, 0.4f, 0.2f, 1f); // Orange/red highlight color
    [SerializeField] private float nextLevelPathWidth = 8f; // Wider than default to make it stand out
    [Tooltip("Special texture for the highlighted path (will animate on X axis)")]
    [SerializeField] private Sprite animatedPathSprite; // Special sprite for animated path
    [Tooltip("UV width for the animated texture (affects tiling/repeat). Higher values show more texture repeats")]
    [SerializeField] private float animatedTextureWidth = 2.5f; // Width of the UV rect for animated texture

    [Header("Next Level Path Animation")]
    [Tooltip("Enable pulsing animation for the next level path")]
    [SerializeField] private bool enablePathAnimation = true;
    [SerializeField] private float animationSpeed = 2f; // Speed of the pulse animation
    [SerializeField] private float pulseMinScale = 0.9f; // Minimum scale during pulse (0.9 = 90% of original)
    [SerializeField] private float pulseMaxScale = 1.1f; // Maximum scale during pulse (1.1 = 110% of original)
    [SerializeField] private float pulseMinAlpha = 0.7f; // Minimum alpha during pulse
    [SerializeField] private float pulseMaxAlpha = 1f; // Maximum alpha during pulse
    [Tooltip("Speed of texture scrolling animation on X axis")]
    [SerializeField] private float textureScrollSpeed = 1f; // Speed of texture scrolling

    [Header("Path Connection")]
    [SerializeField] private Vector2 startOffset = Vector2.zero; // Offset from level button center for path start
    [SerializeField] private Vector2 endOffset = Vector2.zero; // Offset from level button center for path end

    [Header("Update Settings")]
    [SerializeField] private bool updateOnLevelChange = true; // Update paths when level states change
    [SerializeField] private float updateDelay = 0.1f; // Delay before updating paths (to allow UI layout to settle)

    [Header("Path Parent")]
    [Tooltip("The parent Transform where path images will be created. " +
             "This should be the ScrollRect's content (the map that scrolls) so paths scroll with the map. " +
             "If left empty, will try to auto-detect from scroll view.")]
    [SerializeField] private Transform pathParent;

    [Header("Scroll View (Auto-detected if not set)")]
    [Tooltip("The scroll view content RectTransform where level buttons are placed. " +
             "Used for coordinate space calculations. If left empty, will try to auto-detect.")]
    [SerializeField] private RectTransform scrollViewContent;

    // References
    private MainMenuManager mainMenuManager;
    private LevelManager levelManager;
    private PlayFabManager playFabManager;
    private RectTransform parentRectTransform;

    // Path rendering
    private List<GameObject> pathObjects = new List<GameObject>();
    private bool isInitialized = false;
    private Coroutine initializeCoroutine;

    // Next level path animation
    private GameObject nextLevelPathObject = null;
    private Image nextLevelPathImage = null;
    private RawImage nextLevelPathRawImage = null; // RawImage for animated texture (if using animated sprite)
    private RectTransform nextLevelPathRect = null;
    private float nextLevelPathBaseWidth = 0f;
    private Color nextLevelPathBaseColor;
    private float animationTimer = 0f;

    // Sync state
    private bool hasSyncCompleted = false;
    private bool isWaitingForSync = false;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<LevelPathRenderer>(this);

        // If path parent is manually set, use it
        if (pathParent != null)
        {
            parentRectTransform = pathParent.GetComponent<RectTransform>();
            if (parentRectTransform == null)
            {
                // If it's not a RectTransform, try to get it from a child or use the Transform
                parentRectTransform = pathParent.GetComponentInChildren<RectTransform>();
                if (parentRectTransform == null)
                {
                    Debug.LogWarning("LevelPathRenderer: pathParent is not a RectTransform. Path positioning may not work correctly.");
                }
            }
            Debug.Log($"LevelPathRenderer: Using manually assigned path parent: {pathParent.name}");
        }
        else
        {
            // Try to find the scroll view content automatically
            FindScrollViewContent();

            // If we couldn't find it automatically, use this GameObject's RectTransform or parent
            if (parentRectTransform == null)
            {
                parentRectTransform = GetComponent<RectTransform>();
                if (parentRectTransform == null)
                {
                    parentRectTransform = transform.parent?.GetComponent<RectTransform>();
                }
            }
        }
    }

    /// <summary>
    /// Automatically find the scroll view content where level buttons are placed
    /// </summary>
    private void FindScrollViewContent()
    {
        // If manually assigned, use that
        if (scrollViewContent != null)
        {
            parentRectTransform = scrollViewContent;
            Debug.Log($"LevelPathRenderer: Using manually assigned scroll view content: {parentRectTransform.name}");
            return;
        }

        // Method 1: Find ScrollRect in parent hierarchy and get its content
        ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null && scrollRect.content != null)
        {
            parentRectTransform = scrollRect.content;
            Debug.Log($"LevelPathRenderer: Auto-detected scroll view content from parent: {parentRectTransform.name}");
            return;
        }

        // Method 2: Try to find ScrollRect by searching siblings/children
        // (This handles the case where the component is a sibling of the ScrollRect)
        Transform current = transform.parent;
        while (current != null)
        {
            scrollRect = current.GetComponentInChildren<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
            {
                parentRectTransform = scrollRect.content;
                Debug.Log($"LevelPathRenderer: Auto-detected scroll view content from hierarchy: {parentRectTransform.name}");
                return;
            }
            current = current.parent;
        }

        Debug.LogWarning("LevelPathRenderer: Could not automatically find scroll view content. " +
                        "Please assign 'Scroll View Content' in the Inspector, or place this component " +
                        "as a child of the ScrollRect's content RectTransform (the map that scrolls).");
    }

    private void Start()
    {
        // Find dependencies via DependencyRegistry
        mainMenuManager = DependencyRegistry.Find<MainMenuManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        playFabManager = DependencyRegistry.Find<PlayFabManager>();

        if (mainMenuManager == null)
        {
            Debug.LogWarning("LevelPathRenderer: MainMenuManager not found. Paths will not be rendered.");
            return;
        }

        if (levelManager == null)
        {
            Debug.LogWarning("LevelPathRenderer: LevelManager not found. Paths will not be rendered.");
            return;
        }

        // Subscribe to PlayFab sync events
        if (playFabManager != null)
        {
            playFabManager.OnProgressSynced += OnProgressSynced;
            playFabManager.OnProgressSyncFailed += OnProgressSyncFailed;

            // Check if PlayFabManager is already logged in
            // Since PlayFabManager persists across scenes, if it's logged in,
            // sync likely already happened when we first logged in
            if (playFabManager.IsLoggedIn)
            {
                // Already logged in - sync likely completed earlier
                // Proceed with initialization using current level data
                Debug.Log("LevelPathRenderer: PlayFabManager already logged in. Assuming sync completed. Initializing paths.");
                hasSyncCompleted = true;
                StartInitializePaths();
            }
            else
            {
                // Not logged in yet - wait for login and sync
                StartCoroutine(WaitForSyncOrProceed());
            }
        }
        else
        {
            // No PlayFab manager, proceed with initialization using local data
            Debug.Log("LevelPathRenderer: PlayFabManager not found. Using local level data.");
            hasSyncCompleted = true;
            StartInitializePaths();
        }
    }

    /// <summary>
    /// Wait for sync to complete or proceed if sync doesn't happen within a reasonable time
    /// This is only called when PlayFabManager is not yet logged in
    /// </summary>
    private IEnumerator WaitForSyncOrProceed()
    {
        // Wait a short time to see if login and sync events fire
        yield return new WaitForSeconds(0.5f);

        // Check if login happened while we were waiting
        if (playFabManager != null && playFabManager.IsLoggedIn)
        {
            // Login happened - sync should follow, but wait a bit more
            isWaitingForSync = true;
            Debug.Log("LevelPathRenderer: PlayFab logged in. Waiting for cloud sync to complete...");

            // Wait up to 5 seconds for sync to complete
            float timeout = 5f;
            float elapsed = 0f;

            while (!hasSyncCompleted && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (!hasSyncCompleted)
            {
                Debug.LogWarning("LevelPathRenderer: Sync timeout after login. Using local level data.");
                hasSyncCompleted = true;
                StartInitializePaths();
            }
        }
        else
        {
            // Still not logged in - proceed with local data
            Debug.Log("LevelPathRenderer: PlayFab not logged in. Using local level data.");
            hasSyncCompleted = true;
            StartInitializePaths();
        }
    }

    /// <summary>
    /// Called when progress sync completes successfully
    /// </summary>
    private void OnProgressSynced(PlayerProgressData data)
    {
        hasSyncCompleted = true;
        isWaitingForSync = false;

        Debug.Log("LevelPathRenderer: Cloud sync completed. Initializing paths with synced data.");

        // Initialize paths now that we have synced data
        StartInitializePaths();
    }

    /// <summary>
    /// Called when progress sync fails
    /// </summary>
    private void OnProgressSyncFailed(string error)
    {
        hasSyncCompleted = true;
        isWaitingForSync = false;

        Debug.LogWarning($"LevelPathRenderer: Cloud sync failed ({error}). Using local level data.");

        // Initialize paths with local data even though sync failed
        StartInitializePaths();
    }

    /// <summary>
    /// Start the initialization coroutine
    /// </summary>
    private void StartInitializePaths()
    {
        // Stop any existing coroutine
        if (initializeCoroutine != null)
        {
            StopCoroutine(initializeCoroutine);
        }

        initializeCoroutine = StartCoroutine(InitializePathsCoroutine());
    }

    /// <summary>
    /// Initialize and render all paths between unlocked levels (coroutine version)
    /// </summary>
    private IEnumerator InitializePathsCoroutine()
    {
        // Wait for UI to layout
        yield return new WaitForSeconds(updateDelay);

        ClearPaths();

        if (mainMenuManager == null || levelManager == null)
        {
            yield break;
        }

        // Wait for level buttons to be available
        int totalLevels = levelManager.TotalLevels;
        int maxRetries = 10;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            int buttonCount = mainMenuManager.GetLevelButtonCount();

            if (buttonCount >= totalLevels)
            {
                // Level buttons are ready, break out of retry loop
                break;
            }

            // Wait a bit and try again
            yield return new WaitForSeconds(updateDelay);
            retryCount++;
        }

        // Final check - if buttons still not ready, log warning and exit
        if (mainMenuManager.GetLevelButtonCount() < totalLevels)
        {
            Debug.LogWarning("LevelPathRenderer: Level buttons not ready after retries. Paths may not render correctly.");
            yield break;
        }

        // Find the last completed level (highest level with stars > 0)
        int lastCompletedIndex = GetLastCompletedLevelIndex();

        // Find the next available level (next unlocked level after last completed)
        int nextAvailableIndex = GetNextAvailableLevelIndex(lastCompletedIndex);

        // Only create path to the next level (from last completed to next available)
        if (lastCompletedIndex >= 0 && nextAvailableIndex >= 0 && nextAvailableIndex > lastCompletedIndex)
        {
            // Verify both levels are unlocked
            int lastCompletedLevelNumber = lastCompletedIndex + 1;
            int nextAvailableLevelNumber = nextAvailableIndex + 1;

            bool lastCompletedUnlocked = levelManager.IsLevelUnlocked(lastCompletedLevelNumber);
            bool nextAvailableUnlocked = levelManager.IsLevelUnlocked(nextAvailableLevelNumber);

            if (lastCompletedUnlocked && nextAvailableUnlocked)
            {
                // Create the path from last completed to next available level
                // This is always the highlighted path since it's the only one shown
                GameObject pathObj = CreatePathBetweenLevels(
                    lastCompletedIndex,
                    nextAvailableIndex,
                    nextLevelPathColor,
                    nextLevelPathWidth,
                    true // Always highlighted since it's the only path
                );

                // Store reference to the path for animation
                if (pathObj != null)
                {
                    nextLevelPathObject = pathObj;
                    nextLevelPathImage = pathObj.GetComponent<Image>();
                    nextLevelPathRawImage = pathObj.GetComponent<RawImage>();
                    nextLevelPathRect = pathObj.GetComponent<RectTransform>();
                    nextLevelPathBaseWidth = nextLevelPathWidth;
                    nextLevelPathBaseColor = nextLevelPathColor;
                }
            }
        }
        else if (lastCompletedIndex < 0 && nextAvailableIndex >= 0)
        {
            // No levels completed yet - show path from level 0 (or first level) to first available
            // Find the first level (index 0) and create path to next available
            int firstLevelNumber = 1;
            int nextAvailableLevelNumber = nextAvailableIndex + 1;

            bool firstLevelUnlocked = levelManager.IsLevelUnlocked(firstLevelNumber);
            bool nextAvailableUnlocked = levelManager.IsLevelUnlocked(nextAvailableLevelNumber);

            if (firstLevelUnlocked && nextAvailableUnlocked && nextAvailableIndex > 0)
            {
                // Create path from first level to next available
                GameObject pathObj = CreatePathBetweenLevels(
                    0,
                    nextAvailableIndex,
                    nextLevelPathColor,
                    nextLevelPathWidth,
                    true
                );

                if (pathObj != null)
                {
                    nextLevelPathObject = pathObj;
                    nextLevelPathImage = pathObj.GetComponent<Image>();
                    nextLevelPathRawImage = pathObj.GetComponent<RawImage>();
                    nextLevelPathRect = pathObj.GetComponent<RectTransform>();
                    nextLevelPathBaseWidth = nextLevelPathWidth;
                    nextLevelPathBaseColor = nextLevelPathColor;
                }
            }
        }

        isInitialized = true;
        initializeCoroutine = null;
    }

    /// <summary>
    /// Find the last completed level (highest level with stars > 0)
    /// </summary>
    /// <returns>Zero-based index of the last completed level, or -1 if none found</returns>
    private int GetLastCompletedLevelIndex()
    {
        if (levelManager == null) return -1;

        int totalLevels = levelManager.TotalLevels;
        int lastCompleted = -1;

        // Find the highest level with stars > 0
        for (int i = 0; i < totalLevels; i++)
        {
            int levelNumber = i + 1; // Convert to 1-based
            int stars = levelManager.GetLevelStars(levelNumber);
            if (stars > 0)
            {
                lastCompleted = i;
            }
        }

        return lastCompleted;
    }

    /// <summary>
    /// Find the next available level (next unlocked level after the specified level)
    /// </summary>
    /// <param name="fromIndex">The level index to start from (0-based), or -1 to find first unlocked</param>
    /// <returns>Zero-based index of the next available level, or -1 if none found</returns>
    private int GetNextAvailableLevelIndex(int fromIndex)
    {
        if (levelManager == null) return -1;

        int totalLevels = levelManager.TotalLevels;

        // If no starting level, find the first unlocked level
        if (fromIndex < 0)
        {
            for (int i = 0; i < totalLevels; i++)
            {
                int levelNumber = i + 1; // Convert to 1-based
                if (levelManager.IsLevelUnlocked(levelNumber))
                {
                    return i;
                }
            }
            return -1;
        }

        // Find the next unlocked level after the starting one
        for (int i = fromIndex + 1; i < totalLevels; i++)
        {
            int levelNumber = i + 1; // Convert to 1-based
            if (levelManager.IsLevelUnlocked(levelNumber))
            {
                return i;
            }
        }

        // If no level found after starting level, return -1
        return -1;
    }

    /// <summary>
    /// Create a path between two level buttons
    /// </summary>
    /// <param name="fromLevelIndex">Zero-based index of the starting level</param>
    /// <param name="toLevelIndex">Zero-based index of the ending level</param>
    /// <param name="segmentColor">Color for this path segment</param>
    /// <param name="segmentWidth">Width for this path segment</param>
    /// <param name="isHighlighted">Whether this is the highlighted path that should use animated texture</param>
    /// <returns>The created path GameObject</returns>
    private GameObject CreatePathBetweenLevels(int fromLevelIndex, int toLevelIndex, Color segmentColor, float segmentWidth, bool isHighlighted = false)
    {
        // Get level button references
        LevelButtonUI fromButton = mainMenuManager.GetLevelButton(fromLevelIndex);
        LevelButtonUI toButton = mainMenuManager.GetLevelButton(toLevelIndex);

        if (fromButton == null || toButton == null)
        {
            Debug.LogWarning($"LevelPathRenderer: Could not find level buttons for indices {fromLevelIndex} and {toLevelIndex}");
            return null;
        }

        // Get button positions in world space
        RectTransform fromRect = fromButton.GetComponent<RectTransform>();
        RectTransform toRect = toButton.GetComponent<RectTransform>();

        if (fromRect == null || toRect == null)
        {
            Debug.LogWarning($"LevelPathRenderer: Level buttons missing RectTransform components");
            return null;
        }

        // Determine the parent for the path object
        Transform pathParentTransform = null;
        if (pathParent != null)
        {
            pathParentTransform = pathParent;
        }
        else if (parentRectTransform != null)
        {
            pathParentTransform = parentRectTransform;
        }
        else
        {
            pathParentTransform = transform;
        }

        // Calculate start and end positions in world space
        Vector3 startPosWorld = fromRect.position + (Vector3)startOffset;
        Vector3 endPosWorld = toRect.position + (Vector3)endOffset;

        // Convert to local space relative to the path parent
        Vector2 startPos, endPos;
        if (pathParentTransform != null)
        {
            startPos = pathParentTransform.InverseTransformPoint(startPosWorld);
            endPos = pathParentTransform.InverseTransformPoint(endPosWorld);
        }
        else if (parentRectTransform != null)
        {
            startPos = parentRectTransform.InverseTransformPoint(startPosWorld);
            endPos = parentRectTransform.InverseTransformPoint(endPosWorld);
        }
        else
        {
            // Fallback to world space (not ideal, but better than nothing)
            startPos = startPosWorld;
            endPos = endPosWorld;
        }

        // Create path GameObject
        GameObject pathObj = new GameObject($"Path_{fromLevelIndex}_to_{toLevelIndex}");
        pathObj.transform.SetParent(pathParentTransform, false);

        // Add RectTransform
        RectTransform pathRect = pathObj.AddComponent<RectTransform>();

        // Calculate path properties
        Vector2 direction = endPos - startPos;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Set position (center of the path)
        pathRect.anchoredPosition = (startPos + endPos) / 2f;
        pathRect.sizeDelta = new Vector2(distance, segmentWidth);
        pathRect.localRotation = Quaternion.Euler(0, 0, angle);

        // For highlighted paths with animated sprite, use RawImage with material for texture scrolling
        // For other paths, use Image
        if (isHighlighted && animatedPathSprite != null && animatedPathSprite.texture != null)
        {
            // Use RawImage for animated texture scrolling on highlighted path
            RawImage pathRawImage = pathObj.AddComponent<RawImage>();
            pathRawImage.texture = animatedPathSprite.texture;
            pathRawImage.color = segmentColor; // Use highlighted color
            pathRawImage.raycastTarget = false; // Don't block UI interactions

            // For RawImage scrolling, set initial UV rect with configurable width
            // The texture should be set to "Repeat" in its import settings for seamless scrolling
            pathRawImage.uvRect = new Rect(0f, 0f, animatedTextureWidth, 1f);
        }
        else
        {
            // Use regular Image component
            Image pathImage = pathObj.AddComponent<Image>();
            pathImage.color = segmentColor;
            pathImage.raycastTarget = false; // Don't block UI interactions

            // For highlighted paths without animated sprite, use pathSprite if available
            // For regular paths, use pathSprite or default
            if (isHighlighted && pathSprite != null)
            {
                // Highlighted path uses pathSprite with highlighted color
                pathImage.sprite = pathSprite;
            }
            else if (!isHighlighted && pathSprite != null)
            {
                // Regular path uses pathSprite with regular color
                pathImage.sprite = pathSprite;
            }
            else
            {
                // Use a simple white sprite (Unity's default)
                pathImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            }
        }

        // Set sorting order to be behind level buttons
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            pathObj.transform.SetSiblingIndex(0); // Move to back
        }

        pathObjects.Add(pathObj);

        return pathObj;
    }

    private void Update()
    {
        // Animate the next level path if enabled
        if (enablePathAnimation && nextLevelPathObject != null && nextLevelPathRect != null)
        {
            animationTimer += Time.deltaTime * animationSpeed;

            // Calculate pulse value (sine wave from -1 to 1, normalized to 0-1)
            float normalizedPulse = (Mathf.Sin(animationTimer) + 1f) / 2f; // 0 to 1

            // Pulse the width
            float currentWidth = Mathf.Lerp(nextLevelPathBaseWidth * pulseMinScale, nextLevelPathBaseWidth * pulseMaxScale, normalizedPulse);
            Vector2 currentSize = nextLevelPathRect.sizeDelta;
            nextLevelPathRect.sizeDelta = new Vector2(currentSize.x, currentWidth);

            // Pulse the alpha and update color
            float currentAlpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, normalizedPulse);
            Color currentColor = nextLevelPathBaseColor;
            currentColor.a = currentAlpha;

            // Update color for either Image or RawImage
            if (nextLevelPathImage != null)
            {
                nextLevelPathImage.color = currentColor;
            }
            if (nextLevelPathRawImage != null)
            {
                nextLevelPathRawImage.color = currentColor;
            }

            // Animate texture scrolling on X axis if using RawImage with animated sprite
            // Note: Texture Wrap Mode must be set to "Repeat" in import settings for seamless scrolling
            if (nextLevelPathRawImage != null && animatedPathSprite != null)
            {
                // Scroll texture on X axis by updating UV rect position
                // This approach preserves the size and only modifies the position
                Vector2 scrollDelta = new Vector2(textureScrollSpeed * Time.deltaTime, 0f);
                nextLevelPathRawImage.uvRect = new Rect(
                    nextLevelPathRawImage.uvRect.position + scrollDelta,
                    nextLevelPathRawImage.uvRect.size
                );
            }
        }
    }

    /// <summary>
    /// Get world position of a RectTransform's center
    /// </summary>
    private Vector2 GetWorldPosition(RectTransform rect)
    {
        return rect.position;
    }

    /// <summary>
    /// Clear all existing paths
    /// </summary>
    private void ClearPaths()
    {
        foreach (var pathObj in pathObjects)
        {
            if (pathObj != null)
            {
                Destroy(pathObj);
            }
        }
        pathObjects.Clear();

        // Clear animation references
        nextLevelPathObject = null;
        nextLevelPathImage = null;
        nextLevelPathRawImage = null;
        nextLevelPathRect = null;
        animationTimer = 0f;
    }

    /// <summary>
    /// Refresh paths (useful when level states change)
    /// </summary>
    public void RefreshPaths()
    {
        // Only refresh if sync has completed
        if (!hasSyncCompleted)
        {
            Debug.Log("LevelPathRenderer: Waiting for sync to complete before refreshing paths.");
            return;
        }

        // Clear animation references
        nextLevelPathObject = null;
        nextLevelPathImage = null;
        nextLevelPathRawImage = null;
        nextLevelPathRect = null;
        animationTimer = 0f;

        isInitialized = false;
        StartInitializePaths();
    }

    /// <summary>
    /// Update paths when level selection is shown
    /// </summary>
    public void OnLevelSelectionShown()
    {
        RefreshPaths();
    }

    private void OnDestroy()
    {
        // Stop any running coroutines
        if (initializeCoroutine != null)
        {
            StopCoroutine(initializeCoroutine);
            initializeCoroutine = null;
        }

        // Unsubscribe from PlayFab events
        if (playFabManager != null)
        {
            playFabManager.OnProgressSynced -= OnProgressSynced;
            playFabManager.OnProgressSyncFailed -= OnProgressSyncFailed;
        }

        // Unregister from dependency registry
        DependencyRegistry.Unregister<LevelPathRenderer>(this);

        // Clean up paths
        ClearPaths();
    }
}

