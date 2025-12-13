using System.Collections;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject stackableObjectPrefab;
    [SerializeField] private float spawnDelay = 1f;

    [Header("Object Settings")]
    [SerializeField] private Vector2 objectSize = new Vector2(1f, 0.3f);
    [SerializeField] private Color[] objectColors = { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta };

    [Header("Level Mode Settings")]
    [Tooltip("Sprite to use for the last block in level mode")]
    [SerializeField] private Sprite lastBlockSprite;
    [Tooltip("Y scale multiplier for the last block's collider (to match different texture height)")]
    [SerializeField] private float lastBlockColliderYScale = 1f;

    // State
    private GameObject currentObject;
    private bool canSpawn = true;
    private bool waitingForLanding = false;

    // Events
    public System.Action<GameObject> OnObjectSpawned;
    public System.Action<GameObject> OnObjectDropped;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<ObjectSpawner>(this);
    }

    // References
    private UIManager uiManager;
    private LevelManager levelManager;
    private GameManager gameManager;
    private StackManager stackManager;
    private StyleManager styleManager;

    private void Start()
    {
        // Get UI manager reference
        uiManager = DependencyRegistry.Find<UIManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        styleManager = DependencyRegistry.Find<StyleManager>();

        // Subscribe to game events
        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
        }

        // Subscribe to level events
        if (levelManager != null)
        {
            levelManager.OnLevelCompleted += OnLevelCompleted;
        }

        // Subscribe to UI events (title finished)
        if (uiManager != null)
        {
            uiManager.OnTitleFinished += OnTitleFinished;
        }

        // Don't spawn immediately - wait for game start and title to finish
    }


    private void OnObjectLanded(StackableObject landedObject, float landingAccuracy)
    {
        // Unsubscribe from this object's event
        landedObject.OnObjectLanded -= OnObjectLanded;

        // Don't spawn a new object if level is completed
        if (levelManager != null && levelManager.IsLevelComplete)
        {
            // Mark that we're no longer waiting for landing
            waitingForLanding = false;
            return;
        }

        // Spawn a new object BEFORE clearing the waiting flag
        // This prevents race conditions with OnTitleFinished trying to spawn at the same time
        SpawnNewObject();

        // Mark that we're no longer waiting for landing (AFTER spawning)
        waitingForLanding = false;
    }

    public void DropCurrentObject()
    {
        if (currentObject == null || waitingForLanding) return;

        // Don't allow dropping if game is over
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null && gameManager.IsGameOver)
        {
            return;
        }

        // Don't allow dropping if level is completed
        if (levelManager != null && levelManager.IsLevelComplete)
        {
            return;
        }

        StackableObject stackableObject = currentObject.GetComponent<StackableObject>();
        if (stackableObject != null && !stackableObject.IsDropped)
        {
            stackableObject.Drop();
            OnObjectDropped?.Invoke(currentObject);

            // Mark that we're waiting for this object to land
            waitingForLanding = true;

            // Subscribe to the object's landing event to spawn next object when it lands
            stackableObject.OnObjectLanded += OnObjectLanded;
        }
    }

    private void SpawnNewObject()
    {
        if (!canSpawn) return;

        // Refresh StyleManager reference before spawning to ensure we have the latest theme
        if (styleManager == null)
        {
            styleManager = DependencyRegistry.Find<StyleManager>();
        }

        // Create new object
        GameObject newObject = CreateStackableObject();

        // Position it at the spawner's position
        newObject.transform.position = transform.position;

        currentObject = newObject;
        OnObjectSpawned?.Invoke(newObject);
    }

    private GameObject CreateStackableObject()
    {
        GameObject obj;

        if (stackableObjectPrefab != null)
        {
            obj = Instantiate(stackableObjectPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            // Create a default stackable object if no prefab is assigned
            obj = CreateDefaultStackableObject();
        }

        // Parent the object to this spawner so it swings with it
        obj.transform.SetParent(transform);

        // Set up the object
        SetupStackableObject(obj);

        return obj;
    }

    private GameObject CreateDefaultStackableObject()
    {
        // Create a new GameObject
        GameObject obj = new GameObject("StackableObject");

        // Add required components
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<BoxCollider2D>();
        obj.AddComponent<Rigidbody2D>();
        obj.AddComponent<StackableObject>();

        // Set tag
        obj.tag = "Stackable";

        return obj;
    }

    private void SetupStackableObject(GameObject obj)
    {
        // Ensure it has the StackableObject component
        StackableObject stackableObject = obj.GetComponent<StackableObject>();
        if (stackableObject == null)
        {
            stackableObject = obj.AddComponent<StackableObject>();
        }

        // Set up sprite renderer using StackableObject reference
        SpriteRenderer spriteRenderer = stackableObject.SpriteRenderer;
        if (spriteRenderer != null)
        {
            // Check if this is the last block in level mode
            bool isLastBlock = IsLastBlockInLevel();

            // Set a random color for visual variety
            Color randomColor = objectColors[Random.Range(0, objectColors.Length)];

            // Store the original sprite from the prefab (if it exists) for fallback only
            Sprite originalSprite = spriteRenderer.sprite;

            // Clear the sprite first to avoid using prefab sprite when StyleManager should be used
            spriteRenderer.sprite = null;

            // If this is the last block in level mode and we have a special sprite, use it
            if (isLastBlock && lastBlockSprite != null)
            {
                spriteRenderer.sprite = lastBlockSprite;
            }
            // Otherwise, check StyleManager for time-of-day sprites
            else
            {
                // Refresh StyleManager reference if null (in case it wasn't available at Start)
                if (styleManager == null)
                {
                    styleManager = DependencyRegistry.Find<StyleManager>();
                }

                if (styleManager != null)
                {
                    Sprite styleSprite = styleManager.GetCurrentStackableSprite();
                    if (styleSprite != null)
                    {
                        // Use StyleManager sprite for time of day
                        spriteRenderer.sprite = styleSprite;
                    }
                    else
                    {
                        // StyleManager exists but returned null - use original sprite as fallback
                        // This ensures blocks have a visible texture even if StyleManager isn't configured
                        if (originalSprite != null)
                        {
                            spriteRenderer.sprite = originalSprite;
                            Debug.LogWarning("StyleManager returned null sprite. Using prefab sprite as fallback. Check if overrideStackableSprites is enabled and sprites are assigned in StyleManager.");
                        }
                        else
                        {
                            // Create a properly sized default sprite if no original sprite exists
                            int textureWidth = 64;
                            int textureHeight = 64;
                            Texture2D texture = new Texture2D(textureWidth, textureHeight);

                            // Fill texture with the random color
                            Color[] pixels = new Color[textureWidth * textureHeight];
                            for (int i = 0; i < pixels.Length; i++)
                            {
                                pixels[i] = randomColor;
                            }
                            texture.SetPixels(pixels);
                            texture.Apply();

                            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), new Vector2(0.5f, 0.5f));
                            spriteRenderer.sprite = sprite;

                            Debug.LogWarning("StyleManager returned null sprite and no prefab sprite available. Created default colored sprite. Check if overrideStackableSprites is enabled and sprites are assigned in StyleManager.");
                        }
                    }
                }
                else
                {
                    // StyleManager doesn't exist - use original sprite or create default
                    if (originalSprite != null)
                    {
                        spriteRenderer.sprite = originalSprite;
                    }
                    else
                    {
                        // Create a default sprite
                        Texture2D texture = new Texture2D(1, 1);
                        texture.SetPixel(0, 0, randomColor);
                        texture.Apply();

                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                        spriteRenderer.sprite = sprite;
                    }
                }
            }

            // Ensure sprite renderer is enabled
            spriteRenderer.enabled = true;

            spriteRenderer.color = randomColor;

            // Get the sprite's actual size in world units AFTER setting the final sprite
            // This ensures we're calculating scale based on the sprite that will actually be displayed
            Vector2 spriteSize = spriteRenderer.sprite != null
                ? spriteRenderer.sprite.bounds.size
                : Vector2.one;

            Vector3 spriteScale;
            Vector2 actualVisualSize;

            // For the last block sprite, maintain aspect ratio to avoid squishing
            if (isLastBlock && lastBlockSprite != null)
            {
                // Scale to match width while maintaining aspect ratio
                // This ensures the sprite isn't squished and maintains its natural proportions
                float scaleX = objectSize.x / spriteSize.x;
                float scaleY = scaleX; // Use same scale for both axes to maintain aspect ratio
                spriteScale = new Vector3(scaleX, scaleY, 1f);

                // Calculate the actual visual size after scaling (maintains aspect ratio)
                actualVisualSize = new Vector2(
                    spriteSize.x * spriteScale.x,
                    spriteSize.y * spriteScale.y
                );
            }
            else
            {
                // For all other sprites, scale to match objectSize exactly (original behavior)
                // Formula: scale = desiredSize / spriteSize
                spriteScale = new Vector3(
                    objectSize.x / spriteSize.x,
                    objectSize.y / spriteSize.y,
                    1f
                );

                // Visual size matches objectSize for regular blocks
                actualVisualSize = objectSize;
            }

            // Set sprite renderer local scale for visual scaling
            // Note: If spriteRenderer is on a child object, this scales the child, not the parent
            spriteRenderer.transform.localScale = spriteScale;

            // Set up collider using StackableObject reference
            BoxCollider2D collider = stackableObject.Collider as BoxCollider2D;
            if (collider != null)
            {
                Vector2 colliderSize;

                // For the last block, use the actual visual size (which maintains aspect ratio)
                if (isLastBlock && lastBlockSprite != null)
                {
                    // Set collider size to match the actual visual size after scaling
                    colliderSize = actualVisualSize;

                    // Reduce Y size by 5% to eliminate gaps between blocks
                    colliderSize.y = actualVisualSize.y * 0.95f;

                    // Adjust Y scale for last block in level mode if needed
                    // This multiplier adjusts the collider to account for different sprite proportions
                    if (lastBlockColliderYScale != 1f)
                    {
                        colliderSize.y = actualVisualSize.y * lastBlockColliderYScale * 0.95f;
                    }
                }
                else
                {
                    // For all other cases, use objectSize (original behavior)
                    colliderSize = objectSize;

                    // Reduce Y size by 5% to eliminate gaps between blocks
                    colliderSize.y = objectSize.y * 0.95f;
                }

                collider.size = colliderSize;
            }
        }
        else
        {
            // If no sprite renderer, still set up collider with default size
            BoxCollider2D collider = stackableObject.Collider as BoxCollider2D;
            if (collider != null)
            {
                bool isLastBlock = IsLastBlockInLevel();
                Vector2 colliderSize = objectSize;

                // Reduce Y size by 5% to eliminate gaps between blocks
                colliderSize.y = objectSize.y * 0.95f;

                if (isLastBlock && lastBlockColliderYScale != 1f)
                {
                    colliderSize.y = objectSize.y * lastBlockColliderYScale * 0.95f;
                }
                collider.size = colliderSize;
            }
        }
    }

    /// <summary>
    /// Check if the next block to be spawned is the last block needed for the current level
    /// </summary>
    private bool IsLastBlockInLevel()
    {
        // Only check in level mode
        if (gameManager == null || gameManager.CurrentGameMode != GameMode.StackerLevels)
        {
            return false;
        }

        // Need both level manager and stack manager to determine this
        if (levelManager == null || stackManager == null)
        {
            return false;
        }

        // Check if level is already complete (shouldn't spawn more blocks, but check anyway)
        if (levelManager.IsLevelComplete)
        {
            return false;
        }

        // Get current stack height and required height
        int currentHeight = stackManager.GetStackCount();
        LevelData currentLevel = levelManager.CurrentLevel;

        if (currentLevel == null)
        {
            return false;
        }

        // This is the last block if current height + 1 equals required height
        // (the +1 accounts for the block we're about to spawn)
        return (currentHeight + 1) == currentLevel.requiredStackHeight;
    }

    private void OnGameStart()
    {
        canSpawn = true;
        // Spawn immediately when game starts, even if title is still showing
        // This allows players to start dropping blocks before the title disappears
        if (currentObject == null)
        {
            // Use coroutine to ensure StyleManager is ready (handles race condition)
            StartCoroutine(SpawnFirstObjectWhenReady());
        }
    }

    /// <summary>
    /// Coroutine to spawn the first object, ensuring StyleManager is ready first
    /// This handles race conditions where OnGameStart is called before StyleManager initializes
    /// </summary>
    private IEnumerator SpawnFirstObjectWhenReady()
    {
        // Wait one frame to ensure all managers have initialized
        yield return null;

        // Refresh StyleManager reference
        if (styleManager == null)
        {
            styleManager = DependencyRegistry.Find<StyleManager>();
        }

        // If StyleManager exists, wait until it has a sprite available (or give up after a few frames)
        if (styleManager != null)
        {
            int maxWaitFrames = 5;
            int framesWaited = 0;

            while (framesWaited < maxWaitFrames)
            {
                Sprite testSprite = styleManager.GetCurrentStackableSprite();
                if (testSprite != null)
                {
                    // StyleManager has a sprite ready, we can spawn
                    break;
                }

                // Wait another frame
                yield return null;
                framesWaited++;
            }
        }

        // Now spawn the object
        if (currentObject == null && canSpawn)
        {
            SpawnNewObject();
        }
    }

    private void OnTitleFinished()
    {
        // Title has finished, now we can spawn if game is active and no object exists
        // Don't spawn if we're waiting for a landing - that will trigger the spawn instead
        if (canSpawn && currentObject == null && !waitingForLanding)
        {
            SpawnNewObject();
        }
    }

    private void OnGameOver()
    {
        // Cancel any pending spawns
        CancelInvoke(nameof(SpawnNewObject));

        canSpawn = false;
    }

    private void OnLevelCompleted(int stars, int score, bool isFirstCompletion)
    {
        // Cancel any pending spawns
        CancelInvoke(nameof(SpawnNewObject));

        // Destroy the current object when level is completed ONLY if it hasn't been dropped yet
        // This prevents the "stuck block" issue where a block is left swinging
        // But we don't want to destroy blocks that have already been dropped and landed
        if (currentObject != null)
        {
            // Check if the object is still parented to the spawner (hasn't been dropped yet)
            // If it's been dropped, it will have been deparented, so we shouldn't destroy it
            if (currentObject.transform.parent == transform)
            {
                // Block is still swinging - destroy it to prevent it from being stuck
                Destroy(currentObject);
                currentObject = null;
            }
            else
            {
                // Block has been dropped - just clear the reference, don't destroy it
                // The block that landed should remain visible
                currentObject = null;
            }
        }

        canSpawn = false;
        waitingForLanding = false;
    }

    private void OnGameRestart()
    {
        // Cancel any pending Invoke calls to prevent double spawning
        CancelInvoke(nameof(SpawnNewObject));

        // Clean up ALL stackable objects, not just the current one
        // This ensures that any blocks spawned after level completion are removed
        GameObject[] allStackableObjects = GameObject.FindGameObjectsWithTag("Stackable");
        foreach (GameObject obj in allStackableObjects)
        {
            Destroy(obj);
        }

        currentObject = null;
        canSpawn = true;
        waitingForLanding = false;

        // Don't spawn here - let OnGameStart handle spawning
        // This prevents double spawning since OnGameRestart is typically followed by OnGameStart
    }

    private void OnDestroy()
    {
        // Cancel any pending Invoke calls
        CancelInvoke();

        // Unregister from dependency registry
        DependencyRegistry.Unregister<ObjectSpawner>(this);

        // Unsubscribe from events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameStart -= OnGameStart;
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnGameRestart -= OnGameRestart;
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= OnLevelCompleted;
        }

        if (uiManager != null)
        {
            uiManager.OnTitleFinished -= OnTitleFinished;
        }
    }

    // Public getters
    public GameObject CurrentObject => currentObject;
    public bool CanSpawn => canSpawn;
    public bool WaitingForLanding => waitingForLanding;
    public Vector2 ObjectSize => objectSize;
}
