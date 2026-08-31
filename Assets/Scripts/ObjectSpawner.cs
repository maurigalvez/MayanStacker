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

    [Header("Block Variety")]
    [Tooltip("Optional weighted table of special blocks. Left empty, the spawner loads 'BlockVarietyTable' from Resources; with neither, only standard blocks spawn.")]
    [SerializeField] private BlockVarietyTable varietyTable;

    [Header("Level Mode Settings")]
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

        // Fall back to the Resources asset so block variety can be enabled project-wide
        // without editing the spawner prefab. Null here simply means standard blocks only.
        if (varietyTable == null)
        {
            varietyTable = Resources.Load<BlockVarietyTable>(BlockVarietyTable.ResourcePath);
        }

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

        // Decide what kind of block this is before anything is sized or tinted.
        BlockVariant variant = RollVariant();
        stackableObject.ApplyVariant(variant);

        // Everything below sizes the block from blockSize instead of the spawner's
        // objectSize, so a variant can be narrower or wider without changing any of the
        // sprite-scaling, collider-fitting or last-block aspect-ratio maths. With no
        // variant the two are identical and behaviour is unchanged.
        // A Wide Foundation boon widens the block on top of whatever the variant did.
        // Read the multiplier before ticking it down, so a 3-block boon widens 3 blocks.
        float widthMultiplier = variant.widthMultiplier * ActiveBoons.WidthMultiplier;
        ActiveBoons.RegisterBlockSpawned();

        Vector2 blockSize = new Vector2(objectSize.x * widthMultiplier, objectSize.y);

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

            // If this is the last block in level mode, get sprite from StyleManager
            if (isLastBlock)
            {
                // Refresh StyleManager reference if null (in case it wasn't available at Start)
                if (styleManager == null)
                {
                    styleManager = DependencyRegistry.Find<StyleManager>();
                }

                if (styleManager != null)
                {
                    Sprite lastBlockSprite = styleManager.GetCurrentLastBlockSprite();
                    if (lastBlockSprite != null)
                    {
                        spriteRenderer.sprite = lastBlockSprite;
                    }
                    else
                    {
                        // Fallback to regular stackable sprite if last block sprite not set
                        Sprite styleSprite = styleManager.GetCurrentStackableSprite();
                        if (styleSprite != null)
                        {
                            spriteRenderer.sprite = styleSprite;
                        }
                        else if (originalSprite != null)
                        {
                            spriteRenderer.sprite = originalSprite;
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
                }
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

            // A variant carrying its own texture wins over the theme sprite. Block variety
            // is a gameplay difference, so it has to be legible as a difference - colour
            // alone is the fallback for a variant no artist has drawn yet.
            if (variant.sprite != null)
            {
                spriteRenderer.sprite = variant.sprite;
            }

            // Ensure sprite renderer is enabled
            spriteRenderer.enabled = true;

            // A special block overrides the random colour so it reads as different at a
            // glance - the player has to be able to see what they're being offered. A
            // bespoke texture is authored in its final colours, so the random per-block
            // tint is dropped rather than smeared over the art.
            spriteRenderer.color = variant.overrideTint ? variant.tint
                : variant.sprite != null ? Color.white
                : randomColor;

            // Get the sprite's actual size in world units AFTER setting the final sprite
            // This ensures we're calculating scale based on the sprite that will actually be displayed
            Vector2 spriteSize = spriteRenderer.sprite != null
                ? spriteRenderer.sprite.bounds.size
                : Vector2.one;

            Vector3 spriteScale;
            Vector2 actualVisualSize;

            // For the last block sprite, maintain aspect ratio to avoid squishing
            if (isLastBlock)
            {
                // Refresh StyleManager reference if null
                if (styleManager == null)
                {
                    styleManager = DependencyRegistry.Find<StyleManager>();
                }

                Sprite lastBlockSprite = styleManager != null ? styleManager.GetCurrentLastBlockSprite() : null;
                if (lastBlockSprite != null)
                {
                    // Scale to match width while maintaining aspect ratio
                    // This ensures the sprite isn't squished and maintains its natural proportions
                    float scaleX = blockSize.x / spriteSize.x;
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
                    // Last block sprite not available, use regular scaling
                    spriteScale = new Vector3(
                        blockSize.x / spriteSize.x,
                        blockSize.y / spriteSize.y,
                        1f
                    );
                    actualVisualSize = blockSize;
                }
            }
            else
            {
                // For all other sprites, scale to match blockSize exactly (original behavior)
                // Formula: scale = desiredSize / spriteSize
                spriteScale = new Vector3(
                    blockSize.x / spriteSize.x,
                    blockSize.y / spriteSize.y,
                    1f
                );

                // Visual size matches blockSize for regular blocks
                actualVisualSize = blockSize;
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
                if (isLastBlock)
                {
                    // Refresh StyleManager reference if null
                    if (styleManager == null)
                    {
                        styleManager = DependencyRegistry.Find<StyleManager>();
                    }

                    Sprite lastBlockSprite = styleManager != null ? styleManager.GetCurrentLastBlockSprite() : null;
                    if (lastBlockSprite != null)
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
                        // Last block sprite not available, use regular collider size
                        colliderSize = blockSize;
                        colliderSize.y = blockSize.y * 0.95f;
                    }
                }
                else
                {
                    // For all other cases, use blockSize (original behavior)
                    colliderSize = blockSize;

                    // Reduce Y size by 5% to eliminate gaps between blocks
                    colliderSize.y = blockSize.y * 0.95f;
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
                Vector2 colliderSize = blockSize;

                // Reduce Y size by 5% to eliminate gaps between blocks
                colliderSize.y = blockSize.y * 0.95f;

                if (isLastBlock && lastBlockColliderYScale != 1f)
                {
                    colliderSize.y = blockSize.y * lastBlockColliderYScale * 0.95f;
                }
                collider.size = colliderSize;
            }
        }
    }

    /// <summary>
    /// Picks the variant for the block about to be created.
    ///
    /// The final block of a level is always ordinary: it uses its own sprite, its own
    /// collider scaling and completes the objective, so making it narrow or fragile would
    /// be a rug-pull rather than a choice.
    /// </summary>
    private BlockVariant RollVariant()
    {
        if (varietyTable == null) return BlockVariant.Standard;

        if (stackManager == null)
        {
            stackManager = DependencyRegistry.Find<StackManager>();
        }

        int stackHeight = stackManager != null ? stackManager.GetStackCount() : 0;
        bool allowSpecial = !IsLastBlockInLevel();

        // A level may pin specific positions. Those win over the roll, and over the
        // table's guard rails: an authored slot is a stated intention, not an accident of
        // weights, so the "no specials before height N" and spacing rules don't apply to
        // it. The last-block rule still does, for the reason above.
        if (allowSpecial && TryGetPinnedVariant(stackHeight, out BlockVariant pinned))
        {
            return pinned;
        }

        return varietyTable.Roll(
            stackHeight,
            allowSpecial: allowSpecial,
            specialChanceMultiplier: AltitudeBandManager.SpecialBlockChanceMultiplier);
    }

    /// <summary>
    /// Looks up the block a level has authored for this position, if any.
    ///
    /// Only level mode has an authored sequence — Infinite has no fixed length to author
    /// against, and the Daily's shape comes from its modifier rather than from a per-level
    /// asset. Both fall through to the roll.
    /// </summary>
    private bool TryGetPinnedVariant(int blockIndex, out BlockVariant variant)
    {
        variant = BlockVariant.Standard;

        if (gameManager == null || gameManager.CurrentGameMode != GameMode.StackerLevels)
        {
            return false;
        }

        // The master switch still governs everything: turning variety off has to mean
        // standard blocks only, authored or not.
        if (!varietyTable.enableVariants)
        {
            return false;
        }

        if (levelManager == null)
        {
            levelManager = DependencyRegistry.Find<LevelManager>();
        }

        LevelData level = levelManager != null ? levelManager.CurrentLevel : null;
        if (level == null || level.blockSequence == null || level.blockSequence.IsEmpty)
        {
            return false;
        }

        if (!level.blockSequence.TryGetPinned(blockIndex, out BlockVariantId id))
        {
            return false;
        }

        variant = varietyTable.Find(id);
        return true;
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

        // Special-block spacing is per-run, so the first special of a new run isn't gated
        // by whatever the previous run happened to end on.
        if (varietyTable != null) varietyTable.ResetRunState();

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
