using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a simplified front-view representation of the stack as a mini-map
/// Shows block positions, sizes, and rotations (tilt) to help track pyramid structure
/// Uses UI Image components for reliable rendering
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class StackOverviewUI : MonoBehaviour
{
    [Header("Stack Overview Settings")]
    [SerializeField] private float updateInterval = 0.1f; // Update every 0.1 seconds for performance
    [SerializeField] private Color blockColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
    [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private float outlineWidth = 2f;
    [SerializeField] private float padding = 0.1f; // Padding as fraction of stack size (0.1 = 10% padding)
    [SerializeField] private bool showOutline = true;
    [SerializeField] private bool debugMode = false; // Enable debug logging
    [SerializeField] private int minBlocksToShow = 5; // Minimum number of blocks before showing the overview

    [Header("Size Settings")]
    [SerializeField] private float defaultSizePercent = 0.15f; // 15% of screen
    [SerializeField] private float minSizePercent = 0.1f;
    [SerializeField] private float maxSizePercent = 0.3f;
    [SerializeField] private Vector2 sizePercent = new Vector2(0.15f, 0.2f); // Width, Height as percentage

    [Header("Block Image Settings")]
    [SerializeField] private Sprite blockSprite; // Sprite to use for block representation (assign in inspector)
    [SerializeField] private GameObject blockImagePrefab; // Prefab for block representation (optional, will create if null)

    // References
    private StackManager stackManager;
    private GameManager gameManager;
    private Ground ground;
    private RectTransform rectTransform;

    // Cached data
    private List<StackableObject> cachedStackObjects = new List<StackableObject>();
    private Dictionary<StackableObject, GameObject> blockImageMap = new Dictionary<StackableObject, GameObject>();
    private float lastUpdateTime = 0f;
    private bool isGameOver = false;

    // View bounds
    private Bounds stackBounds;
    private float scaleFactor = 1f;
    private Vector2 viewOffset = Vector2.zero;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // Get references via DependencyRegistry
        stackManager = DependencyRegistry.Find<StackManager>();
        gameManager = DependencyRegistry.Find<GameManager>();
        ground = DependencyRegistry.Find<Ground>();

        // Subscribe to events
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnStackChanged;
            stackManager.OnObjectRemovedFromStack += OnStackChanged;
        }

        if (gameManager != null)
        {
            gameManager.OnGameModeChanged += OnGameModeChanged;
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameRestart += OnGameRestart;
            gameManager.OnGameOver += OnGameOver;

            // Set initial visibility based on game mode
            OnGameModeChanged(gameManager.CurrentGameMode);
        }

        // Initialize size
        UpdateSize();

        // Initial update
        UpdateStackData();
        RefreshVisualization();
    }

    private void Update()
    {
        // Don't update if game is over
        if (isGameOver) return;

        // Update at intervals for performance
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateStackData();
            RefreshVisualization();
            lastUpdateTime = Time.time;
        }
    }

    /// <summary>
    /// Updates the cached stack data and calculates view bounds
    /// </summary>
    private void UpdateStackData()
    {
        if (stackManager == null)
        {
            if (debugMode) Debug.LogWarning("StackOverviewUI: StackManager is null!");
            return;
        }

        cachedStackObjects = stackManager.GetStackObjects();

        if (debugMode && Time.frameCount % 60 == 0)
        {
            Debug.Log($"StackOverviewUI: Updated stack data, count = {cachedStackObjects.Count}");
        }

        CalculateViewBounds();
    }

    /// <summary>
    /// Calculates the bounds of the stack to determine scaling
    /// </summary>
    private void CalculateViewBounds()
    {
        // Get ground level as base reference (Y=0 in UI space)
        float groundLevel = 0f;
        if (ground != null)
        {
            groundLevel = ground.GetGroundTop();
        }

        if (cachedStackObjects == null || cachedStackObjects.Count == 0)
        {
            // Use ground level as reference if no objects
            stackBounds = new Bounds(new Vector3(0, groundLevel, 0), new Vector3(2f, 1f, 0));
            scaleFactor = 1f;
            viewOffset = new Vector2(0f, groundLevel);
            return;
        }

        // Calculate bounds from all stack objects
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (StackableObject obj in cachedStackObjects)
        {
            if (obj == null) continue;

            Vector3 pos = obj.transform.position;
            Collider2D col = obj.Collider;

            if (col != null)
            {
                Bounds bounds = col.bounds;
                minX = Mathf.Min(minX, bounds.min.x);
                maxX = Mathf.Max(maxX, bounds.max.x);
                minY = Mathf.Min(minY, bounds.min.y);
                maxY = Mathf.Max(maxY, bounds.max.y);
            }
            else
            {
                // Fallback if no collider
                minX = Mathf.Min(minX, pos.x - 0.5f);
                maxX = Mathf.Max(maxX, pos.x + 0.5f);
                minY = Mathf.Min(minY, pos.y - 0.25f);
                maxY = Mathf.Max(maxY, pos.y + 0.25f);
            }
        }

        // Use ground level or minimum Y, whichever is lower (to ensure we start from the bottom)
        float baseY = Mathf.Min(minY, groundLevel);

        // Calculate bounds
        float width = maxX - minX;
        float height = maxY - baseY; // Height from base to top

        if (width < 1f) width = 1f;
        if (height < 1f) height = 1f;

        // Add padding as percentage of size
        float paddingX = width * padding;
        float paddingY = height * padding;

        // Calculate total height including padding
        float totalHeight = height + paddingY * 2f;

        stackBounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, baseY + totalHeight * 0.5f, 0),
            new Vector3(width + paddingX * 2f, totalHeight, 0)
        );

        // Calculate scale factor to fit in UI bounds
        Rect rect = rectTransform.rect;
        float uiWidth = rect.width;
        float uiHeight = rect.height;

        if (uiWidth > 0 && uiHeight > 0)
        {
            float scaleX = uiWidth / stackBounds.size.x;
            float scaleY = uiHeight / stackBounds.size.y;
            scaleFactor = Mathf.Min(scaleX, scaleY) * 0.9f; // 90% to add some margin
        }
        else
        {
            scaleFactor = 1f;
        }

        // Set view offset: X is centered, Y is at base (ground level)
        viewOffset = new Vector2(
            (minX + maxX) * 0.5f,
            baseY  // Start from ground/base level
        );
    }

    /// <summary>
    /// Converts world position to UI space
    /// </summary>
    private Vector2 WorldToUISpace(Vector3 worldPos)
    {
        Rect rect = rectTransform.rect;

        // Convert world position relative to view offset
        Vector2 relativePos = new Vector2(
            worldPos.x - viewOffset.x,
            worldPos.y - viewOffset.y  // Y=0 in world space (ground/base) maps to bottom of rect
        );

        // Scale and position in UI space
        // X is centered, Y starts from bottom (rect.yMin) and goes up
        // When relativePos.y = 0 (first block at base), position is at rect.yMin (bottom)
        Vector2 uiPos = new Vector2(
            rect.center.x + relativePos.x * scaleFactor,  // X centered
            rect.yMin + relativePos.y * scaleFactor        // Y starts from bottom (rect.yMin) and goes up
        );

        return uiPos;
    }

    /// <summary>
    /// Refreshes the visualization by creating/updating UI Image components
    /// </summary>
    private void RefreshVisualization()
    {
        // Don't render if game is over
        if (isGameOver) return;

        if (cachedStackObjects == null || rectTransform == null)
        {
            return;
        }

        // Only show if we have at least the minimum number of blocks
        if (cachedStackObjects.Count < minBlocksToShow)
        {
            ClearBlockImages();
            return;
        }

        // Remove images for objects that are no longer in the stack
        List<StackableObject> toRemove = new List<StackableObject>();
        foreach (var kvp in blockImageMap)
        {
            if (!cachedStackObjects.Contains(kvp.Key) || kvp.Key == null)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (StackableObject obj in toRemove)
        {
            if (blockImageMap.ContainsKey(obj))
            {
                if (blockImageMap[obj] != null)
                {
                    Destroy(blockImageMap[obj]);
                }
                blockImageMap.Remove(obj);
            }
        }

        // Create/update images for current stack objects
        foreach (StackableObject obj in cachedStackObjects)
        {
            if (obj == null) continue;

            if (!blockImageMap.ContainsKey(obj) || blockImageMap[obj] == null)
            {
                // Create new image
                CreateBlockImage(obj);
            }
            else
            {
                // Update existing image
                UpdateBlockImage(obj, blockImageMap[obj]);
            }
        }
    }

    /// <summary>
    /// Creates a UI Image component for a block
    /// </summary>
    private void CreateBlockImage(StackableObject obj)
    {
        GameObject imageObj;

        if (blockImagePrefab != null)
        {
            imageObj = Instantiate(blockImagePrefab, rectTransform);
        }
        else
        {
            // Create a simple Image GameObject
            imageObj = new GameObject($"BlockImage_{obj.GetInstanceID()}");
            imageObj.transform.SetParent(rectTransform, false);

            // Add Image component
            Image image = imageObj.AddComponent<Image>();
            image.color = blockColor;
            image.raycastTarget = false;

            // Use assigned sprite, or create a simple white sprite if none assigned
            if (blockSprite != null)
            {
                image.sprite = blockSprite;
            }
            else
            {
                // Fallback: Create a simple white sprite if no sprite is assigned
                Texture2D texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                image.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            }
        }

        blockImageMap[obj] = imageObj;
        UpdateBlockImage(obj, imageObj);
    }

    /// <summary>
    /// Updates the position, size, and rotation of a block image
    /// </summary>
    private void UpdateBlockImage(StackableObject obj, GameObject imageObj)
    {
        if (obj == null || imageObj == null) return;

        Collider2D col = obj.Collider;
        if (col == null) return;

        Bounds bounds = col.bounds;
        Vector3 center = bounds.center;
        Vector2 size = bounds.size;

        // Convert bottom of block to UI space (so first block's bottom is at Y=0)
        Vector3 bottomPos = new Vector3(center.x, bounds.min.y, center.z);
        Vector2 uiBottom = WorldToUISpace(bottomPos);
        Vector2 uiSize = size * scaleFactor;

        RectTransform imageRect = imageObj.GetComponent<RectTransform>();
        if (imageRect == null)
        {
            imageRect = imageObj.AddComponent<RectTransform>();
        }

        // Set anchor and pivot to bottom-center
        // This way, when anchoredPosition.y = 0, the bottom of the block is at Y=0
        imageRect.anchorMin = new Vector2(0.5f, 0f); // Bottom-center anchor
        imageRect.anchorMax = new Vector2(0.5f, 0f); // Bottom-center anchor (point anchor)
        imageRect.pivot = new Vector2(0.5f, 0f);     // Bottom-center pivot

        // Position block - with bottom-center pivot, Y=0 means bottom is at Y=0
        imageRect.anchoredPosition = new Vector2(uiBottom.x, uiBottom.y);
        imageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, uiSize.x);
        imageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, uiSize.y);

        // Set rotation
        float rotation = obj.transform.rotation.eulerAngles.z;
        imageRect.localRotation = Quaternion.Euler(0, 0, rotation);

        // Update color
        Image image = imageObj.GetComponent<Image>();
        if (image != null)
        {
            image.color = blockColor;
        }

        // Add outline if enabled
        if (showOutline)
        {
            UpdateOutline(imageObj, imageRect, uiSize);
        }
        else
        {
            // Remove outline if it exists
            Outline outline = imageObj.GetComponent<Outline>();
            if (outline != null)
            {
                Destroy(outline);
            }
        }
    }

    /// <summary>
    /// Adds or updates an outline on the block image
    /// </summary>
    private void UpdateOutline(GameObject imageObj, RectTransform imageRect, Vector2 size)
    {
        Outline outline = imageObj.GetComponent<Outline>();
        if (outline == null)
        {
            outline = imageObj.AddComponent<Outline>();
        }

        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(outlineWidth, outlineWidth);
    }

    /// <summary>
    /// Updates the size of the overview UI (only width, height remains unchanged)
    /// </summary>
    public void UpdateSize()
    {
        if (rectTransform == null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        // Only update width based on percentage, keep height unchanged
        float width = canvasRect.rect.width * sizePercent.x;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        // Height is not changed - it remains as set in the Inspector

        // Refresh visualization after size change
        RefreshVisualization();
    }

    /// <summary>
    /// Sets the width as a percentage of screen (0.0 to 1.0)
    /// Height is not changed - it remains as set in the Inspector
    /// </summary>
    public void SetSizePercent(Vector2 percent)
    {
        sizePercent.x = Mathf.Clamp(percent.x, minSizePercent, maxSizePercent);
        // sizePercent.y is kept for reference but not used to change height
        UpdateSize();
    }

    /// <summary>
    /// Gets the current size as percentage
    /// </summary>
    public Vector2 GetSizePercent()
    {
        return sizePercent;
    }

    /// <summary>
    /// Clears all block images
    /// </summary>
    private void ClearBlockImages()
    {
        foreach (var kvp in blockImageMap)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        blockImageMap.Clear();
    }

    /// <summary>
    /// Event handlers
    /// </summary>
    private void OnStackChanged(StackableObject obj)
    {
        UpdateStackData();
        RefreshVisualization();
    }

    private void OnGameModeChanged(GameMode mode)
    {
        // Show for all game modes
        gameObject.SetActive(true);
        RefreshVisualization();
    }

    private void OnGameStart()
    {
        isGameOver = false;
        UpdateStackData();
        RefreshVisualization();
    }

    private void OnGameOver()
    {
        isGameOver = true;
        ClearBlockImages();
    }

    private void OnGameRestart()
    {
        isGameOver = false;
        ClearBlockImages();
        cachedStackObjects.Clear();
        UpdateStackData();
        RefreshVisualization();
    }

    private void OnDestroy()
    {
        ClearBlockImages();

        // Unsubscribe from events
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnStackChanged;
            stackManager.OnObjectRemovedFromStack -= OnStackChanged;
        }

        if (gameManager != null)
        {
            gameManager.OnGameModeChanged -= OnGameModeChanged;
            gameManager.OnGameStart -= OnGameStart;
            gameManager.OnGameRestart -= OnGameRestart;
            gameManager.OnGameOver -= OnGameOver;
        }
    }
}
