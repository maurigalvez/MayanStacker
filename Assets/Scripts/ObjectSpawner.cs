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

    private void Start()
    {
        // Get UI manager reference
        uiManager = DependencyRegistry.Find<UIManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();

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

            // Store the original sprite from the prefab (if it exists)
            Sprite originalSprite = spriteRenderer.sprite;

            // If this is the last block in level mode and we have a special sprite, use it
            if (isLastBlock && lastBlockSprite != null)
            {
                spriteRenderer.sprite = lastBlockSprite;
            }
            // Otherwise, ensure we have a sprite (use original if it exists, or create a default one)
            else
            {
                // Use the original sprite if it exists
                if (originalSprite != null)
                {
                    spriteRenderer.sprite = originalSprite;
                }
                // Create a default sprite only if no sprite exists
                else if (spriteRenderer.sprite == null)
                {
                    // Create a simple colored rectangle sprite only if no sprite exists
                    Texture2D texture = new Texture2D(1, 1);
                    texture.SetPixel(0, 0, randomColor);
                    texture.Apply();

                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                    spriteRenderer.sprite = sprite;
                }
            }

            // Ensure sprite renderer is enabled
            spriteRenderer.enabled = true;

            spriteRenderer.color = randomColor;

            // Set sprite renderer local scale for visual scaling
            spriteRenderer.transform.localScale *= objectSize;
        }

        // Set up collider using StackableObject reference
        BoxCollider2D collider = stackableObject.Collider as BoxCollider2D;
        if (collider != null)
        {
            // Check if this is the last block in level mode
            bool isLastBlock = IsLastBlockInLevel();

            // Set collider size - adjust Y scale for last block if needed
            Vector2 colliderSize = objectSize;
            if (isLastBlock && lastBlockColliderYScale != 1f)
            {
                colliderSize.y = objectSize.y * lastBlockColliderYScale;
            }

            collider.size = colliderSize;
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
