using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject stackableObjectPrefab;
    [SerializeField] private float spawnDelay = 1f;

    [Header("Object Settings")]
    [SerializeField] private Vector2 objectSize = new Vector2(1f, 0.3f);
    [SerializeField] private Color[] objectColors = { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta };

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

    private void Start()
    {
        // Get UI manager reference
        uiManager = DependencyRegistry.Find<UIManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();

        // Subscribe to game events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
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

        // Mark that we're no longer waiting for landing
        waitingForLanding = false;

        // Spawn a new object after the current one has landed and settled
        SpawnNewObject();
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
            // Set a random color for visual variety
            Color randomColor = objectColors[Random.Range(0, objectColors.Length)];
            // Use the current sprite if it exists, otherwise create a default one
            if (spriteRenderer.sprite == null)
            {
                // Create a simple colored rectangle sprite only if no sprite exists
                Texture2D texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, randomColor);
                texture.Apply();

                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                spriteRenderer.sprite = sprite;
            }

            spriteRenderer.color = randomColor;

            // Set sprite renderer local scale for visual scaling
            spriteRenderer.transform.localScale *= objectSize;
        }

        // Set up collider using StackableObject reference
        BoxCollider2D collider = stackableObject.Collider as BoxCollider2D;
        if (collider != null)
        {
            collider.size = objectSize;
        }
    }

    private void OnGameStart()
    {
        canSpawn = true;
        // Don't spawn immediately - wait for title to finish
        // Spawning will happen in OnTitleFinished if no object exists
        if (currentObject == null && (uiManager == null || !uiManager.IsTitleShowing))
        {
            SpawnNewObject();
        }
    }

    private void OnTitleFinished()
    {
        // Title has finished, now we can spawn if game is active and no object exists
        if (canSpawn && currentObject == null)
        {
            SpawnNewObject();
        }
    }

    private void OnGameOver()
    {
        canSpawn = false;
    }

    private void OnGameRestart()
    {
        // Clean up current object
        if (currentObject != null)
        {
            Destroy(currentObject);
            currentObject = null;
        }

        canSpawn = true;
        waitingForLanding = false;

        // Don't spawn immediately - wait for title to finish
        // Spawning will happen in OnTitleFinished
        // If title is not showing (shouldn't happen, but safety check), spawn after delay
        if (uiManager == null || !uiManager.IsTitleShowing)
        {
            Invoke(nameof(SpawnNewObject), 0.5f);
        }
    }

    private void OnDestroy()
    {
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
