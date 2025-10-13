using UnityEngine;

public class EnvironmentAsset : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private bool enableMovement = true;
    [SerializeField] private bool useAnimationCurve = false;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);
    [SerializeField] private float curveDuration = 10f;

    [Header("Visual Effects")]
    [SerializeField] private bool enableFadeOut = true;
    [SerializeField] private float fadeOutDistance = 5f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Rotation")]
    [SerializeField] private bool enableRotation = false;
    [SerializeField] private float rotationSpeed = 0f;
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    // Private variables
    private EnvironmentAssetData assetData;
    private bool movingFromLeft;
    private float leftBound;
    private float rightBound;
    private float leftDespawnBound;
    private float rightDespawnBound;
    private float currentSpeed;
    private float distanceTraveled = 0f;
    private float totalDistance;
    private Vector3 startPosition;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private float curveTime = 0f;

    // Time-based despawning
    private float lifetime;
    private float timeAlive = 0f;

    // Events
    public System.Action<EnvironmentAsset> OnDespawn;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void Start()
    {
        startPosition = transform.position;
        totalDistance = Mathf.Abs(rightBound - leftBound);
    }

    private void Update()
    {
        if (assetData == null) return;

        // Update time for time-based despawning
        if (assetData.despawnType == DespawnType.Time)
        {
            timeAlive += Time.deltaTime;
        }

        // Update movement (only if enabled)
        if (enableMovement)
        {
            UpdateMovement();
        }

        // Update visual effects
        if (enableFadeOut)
        {
            UpdateFadeEffect();
        }

        // Update rotation
        if (enableRotation)
        {
            UpdateRotation();
        }

        // Check for despawning
        CheckDespawnConditions();
    }

    private void UpdateMovement()
    {
        // Calculate current speed based on curve or constant speed
        float speedMultiplier = 1f;
        if (useAnimationCurve)
        {
            curveTime += Time.deltaTime;
            float curveProgress = Mathf.Clamp01(curveTime / curveDuration);
            speedMultiplier = speedCurve.Evaluate(curveProgress);
        }

        float finalSpeed = currentSpeed * speedMultiplier;

        // Determine movement direction
        float direction = movingFromLeft ? 1f : -1f;

        // Move the asset
        Vector3 movement = Vector3.right * direction * finalSpeed * Time.deltaTime;
        transform.Translate(movement);

        // Update distance traveled
        distanceTraveled += Mathf.Abs(movement.x);
    }

    private void UpdateFadeEffect()
    {
        if (spriteRenderer == null) return;

        // Calculate distance from spawn point
        float distanceFromStart = Vector3.Distance(transform.position, startPosition);

        // Calculate fade progress
        float fadeProgress = Mathf.Clamp01(distanceFromStart / fadeOutDistance);

        // Apply fade using curve
        float alpha = fadeCurve.Evaluate(fadeProgress);

        // Update color
        Color currentColor = originalColor;
        currentColor.a = alpha;
        spriteRenderer.color = currentColor;
    }

    private void UpdateRotation()
    {
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }

    private void CheckDespawnConditions()
    {
        bool shouldDespawn = false;

        // Check despawn based on type
        if (assetData.despawnType == DespawnType.Time)
        {
            // Time-based despawning
            if (timeAlive >= lifetime)
            {
                shouldDespawn = true;
            }
        }
        else // DespawnType.Movement
        {
            // Movement-based despawning
            float assetX = transform.position.x;

            // Check if moved beyond despawn bounds
            if (movingFromLeft && assetX > rightDespawnBound)
            {
                shouldDespawn = true;
            }
            else if (!movingFromLeft && assetX < leftDespawnBound)
            {
                shouldDespawn = true;
            }
        }

        if (shouldDespawn)
        {
            Despawn();
        }
    }

    public void Initialize(EnvironmentAssetData data, bool fromLeft, float leftBoundary, float rightBoundary)
    {
        assetData = data;
        movingFromLeft = fromLeft;
        leftBound = leftBoundary;
        rightBound = rightBoundary;
        currentSpeed = assetData.GetRandomSpeed();

        // Initialize despawn bounds (will be updated by spawner)
        leftDespawnBound = leftBoundary;
        rightDespawnBound = rightBoundary;

        // Initialize time-based despawning
        if (assetData.despawnType == DespawnType.Time)
        {
            lifetime = assetData.GetRandomLifetime();
            timeAlive = 0f;
        }

        // Set initial rotation if moving from right
        if (!movingFromLeft && enableRotation)
        {
            transform.rotation = Quaternion.Euler(0, 180, 0); // Flip horizontally
        }

        // Apply order in layer if sprite renderer exists
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = assetData.GetRandomOrderInLayer();
        }
    }

    public void UpdateDespawnBounds(float leftDespawn, float rightDespawn)
    {
        leftDespawnBound = leftDespawn;
        rightDespawnBound = rightDespawn;
    }

    public void Despawn()
    {
        OnDespawn?.Invoke(this);
        Destroy(gameObject);
    }

    // Public getters
    public EnvironmentAssetData AssetData => assetData;
    public bool IsMovingFromLeft => movingFromLeft;
    public float DistanceTraveled => distanceTraveled;
    public float CurrentSpeed => currentSpeed;
    public float TimeAlive => timeAlive;
    public float Lifetime => lifetime;
    public float RemainingLifetime => Mathf.Max(0f, lifetime - timeAlive);

    // Public setters for runtime modifications
    public void SetMovementEnabled(bool enabled)
    {
        enableMovement = enabled;
    }

    public void SetSpeed(float speed)
    {
        currentSpeed = speed;
    }

    public void SetRotationEnabled(bool enabled)
    {
        enableRotation = enabled;
    }

    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }
}
