using System.Reflection;
using UnityEngine;

public class SpawnerHolder : MonoBehaviour
{
    [Header("Swing Settings")]
    [SerializeField] private float swingAmplitude = 2f; // How far the swing goes
    [SerializeField] private float swingSpeed = 1f; // How fast the swing oscillates
    [SerializeField] private float swingAngle = 15f; // Maximum angle in degrees for the swing
    [SerializeField] private bool useCircularMotion = false; // Toggle between pendulum and circular motion
    [SerializeField] private bool randomizeStartAngle = true; // Randomize swing start angle on each spawn


    [Header("Rotation Settings")]
    [SerializeField] private bool enableRotation = true; // Enable rotation based on swing
    [SerializeField] private float rotationMultiplier = 1f; // How much the spawner rotates relative to swing
    [SerializeField] private float rotationSmoothing = 5f; // How smoothly the rotation follows the swing

    [Header("Drop Recoil Settings")]
    [SerializeField] private bool enableDropRecoil = true; // Enable recoil effect when dropping objects
    [SerializeField] private float recoilSwingBoost = 1.5f; // How much to boost the swing speed on drop (multiplier)
    [SerializeField] private float recoilDuration = 0.3f; // How long the recoil effect lasts in seconds
    [SerializeField] private float recoilRotationBoost = 1.2f; // How much to boost rotation on drop (multiplier)
    [SerializeField] private bool enableDirectionalRecoil = true; // Add directional impulse to swing on drop
    [SerializeField] private float recoilDirectionStrength = 2f; // How much to shift the swing phase (in radians)


    [Header("Spawner Reference")]
    [SerializeField] private ObjectSpawner objectSpawner; // Reference to the spawner to move
    [SerializeField] private Vector3 spawnerOffset = new Vector3(0f, 0f, 0f); // Offset from holder position

    [Header("Neck Follow Settings")]
    [SerializeField] private Transform[] neckPieces = new Transform[3]; // Array of neck pieces (top to bottom, top piece follows spawner)
    [SerializeField] private bool enableNeckFollow = true; // Enable neck following movement
    [SerializeField] private float neckFollowSpeed = 8f; // How fast the neck follows (higher = more responsive)
    [SerializeField] private float neckMovementReduction = 0.6f; // How much each piece reduces movement relative to the one above (0-1, lower = less movement)
    [SerializeField] private float neckRotationReduction = 0.4f; // How much each piece reduces rotation relative to the one above (0-1, lower = less rotation)

    [Header("Stack Height Settings")]
    [SerializeField] private float consistentHeightOffset = 2f; // Consistent height above the highest point (ground or stack)
    [SerializeField] private float heightAnimationSpeed = 5f; // How fast the holder animates to new height (units per second)

    [Header("Height-Based Swing Scaling (InfiniteStacker Only)")]
    [SerializeField] private bool enableHeightBasedScaling = true; // Enable swing scaling based on block count
    [SerializeField] private int scalingStartBlockCount = 10; // Number of blocks at which scaling begins
    [SerializeField] private float maxSwingSpeedMultiplier = 2.5f; // Maximum swing speed multiplier at high stacks
    [SerializeField] private float maxSwingAmplitudeMultiplier = 2f; // Maximum swing amplitude multiplier at high stacks
    [SerializeField] private float scalingCurve = 1.5f; // Curve of the scaling (1 = linear, >1 = exponential, <1 = logarithmic)

    [Header("Swing Constraints")]
    [SerializeField] private bool constrainToScreen = true; // Keep swing within screen bounds
    [SerializeField] private float screenMargin = 1f; // Margin from screen edges

    // Private variables

    private float swingTime = 0f;
    private Vector3 initialSpawnerPosition;
    private Vector3 holderCenterPosition;
    private float currentStackHeight = 0f;
    private float targetHolderHeight = 0f; // Target height for smooth animation
    private float groundHeight = 0f; // Height of the ground/floor
    private Camera mainCamera;
    private float currentRotationZ = 0f;
    private float targetRotationZ = 0f;
    private float previousRotationZ = 0f;
    private float spawnerAngularVelocity = 0f;
    private float recoilTimer = 0f;
    private float originalSwingSpeed = 0f;
    private float originalSwingAmplitude = 0f;
    private float originalRotationMultiplier = 0f;
    private float currentScaledSwingSpeed = 0f; // Current base swing speed after height scaling (used by recoil)
    private float currentScaledSwingAmplitude = 0f; // Current base swing amplitude after height scaling
    private float levelSwingSpeedMultiplier = 1f; // Level-specific swing speed multiplier (from LevelData)
    private float levelSwingAmplitudeMultiplier = 1f; // Level-specific swing amplitude multiplier (from LevelData)

    // Neck chain tracking - each piece follows the one above it
    private struct NeckPieceData
    {
        public Vector3 targetPosition;
        public Vector3 baseOffset;
        public float originalRotationZ; // Original orientation to preserve
        public float targetRotationZ; // Target rotation (original + follow offset)
        public float currentRotationZ; // Current smoothed rotation
        public float followRotationOffset; // Rotation offset from following (added to original)
    }
    private NeckPieceData[] neckPieceData; // Data for each neck piece
    private Vector3[] neckPiecePositions; // Current positions for smooth following

    private GameObject lastDroppedObject;
    private Ground ground;
    private GameManager gameManager;
    private LevelManager levelManager;

    // Events

    public System.Action<Vector3> OnSpawnerPositionChanged;


    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<SpawnerHolder>(this);

        // Get camera reference

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }

        // Store initial positions

        holderCenterPosition = transform.position;

        // Try to find the Ground script to get actual ground height

        ground = DependencyRegistry.Find<Ground>();
        if (ground == null)
        {
            ground = FindFirstObjectByType<Ground>();
        }

        // Get ground height from Ground script, fallback to 0 if not found
        groundHeight = ground != null ? ground.GetGroundTop() : 0f;
        currentStackHeight = groundHeight;
        targetHolderHeight = groundHeight + consistentHeightOffset;

        // If no spawner is assigned, try to find one using DependencyRegistry

        if (objectSpawner == null)
        {
            objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        }


        if (objectSpawner != null)
        {
            initialSpawnerPosition = objectSpawner.transform.position;
        }

        // Store original values for recoil restoration and height scaling
        originalSwingSpeed = swingSpeed;
        originalSwingAmplitude = swingAmplitude;
        originalRotationMultiplier = rotationMultiplier;

        // Initialize neck pieces if assigned
        if (neckPieces != null && neckPieces.Length > 0)
        {
            // Initialize arrays
            neckPieceData = new NeckPieceData[neckPieces.Length];
            neckPiecePositions = new Vector3[neckPieces.Length];

            // Store base offsets and initial values for each neck piece
            for (int i = 0; i < neckPieces.Length; i++)
            {
                if (neckPieces[i] != null)
                {
                    neckPieceData[i].baseOffset = neckPieces[i].position - holderCenterPosition;
                    neckPieceData[i].targetPosition = neckPieces[i].position;
                    neckPiecePositions[i] = neckPieces[i].position;

                    // Store original rotation to preserve orientation
                    neckPieceData[i].originalRotationZ = neckPieces[i].eulerAngles.z;
                    neckPieceData[i].followRotationOffset = 0f;
                    neckPieceData[i].currentRotationZ = neckPieceData[i].originalRotationZ;
                    neckPieceData[i].targetRotationZ = neckPieceData[i].originalRotationZ;
                }
            }
        }
    }


    private void Start()
    {
        // Subscribe to game events if available
        gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
        }

        // Subscribe to spawner events to track dropped and spawned objects
        if (objectSpawner != null)
        {
            objectSpawner.OnObjectDropped += OnObjectDropped;
            objectSpawner.OnObjectSpawned += OnObjectSpawned;
        }

        // Subscribe to stack manager events to know when objects are successfully added to stack
        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }

        // Subscribe to level manager events to apply level-specific settings
        levelManager = DependencyRegistry.Find<LevelManager>();
        if (levelManager != null)
        {
            levelManager.OnLevelLoaded += OnLevelLoaded;

            // If a level is already loaded, apply its settings now
            if (levelManager.CurrentLevel != null)
            {
                OnLevelLoaded(levelManager.CurrentLevel);
            }
        }
    }


    private void Update()
    {
        if (objectSpawner == null) return;

        // Update height-based scaling for InfiniteStacker mode
        UpdateHeightBasedScaling();

        // Update recoil effect
        UpdateRecoilEffect();

        // Animate holder height smoothly towards target
        AnimateHolderHeight();

        // Update swing motion
        UpdateSwingMotion();

        // Apply the swing to the spawner
        ApplySwingToSpawner();

        // Update neck follow movement
        UpdateNeckFollow();
    }


    private void UpdateStackHeight()
    {
        if (objectSpawner == null) return;

        // Get object size from the ObjectSpawner
        Vector2 objectSize = objectSpawner.ObjectSize;

        // Use the more accurate stack height calculation
        float highestPoint = GetActualStackHeight(objectSize);

        // If no objects in stack but we have a falling object, track it
        var stackManager = DependencyRegistry.Find<StackManager>();
        if ((stackManager == null || stackManager.GetStackCount() == 0) && lastDroppedObject != null)
        {
            // Fallback: If no stack objects, use the last dropped object
            StackableObject stackableObj = lastDroppedObject.GetComponent<StackableObject>();
            if (stackableObj != null)
            {
                float objectTop = GetObjectTopPosition(stackableObj, objectSize);
                highestPoint = Mathf.Max(highestPoint, objectTop);
            }
        }

        // Validate the calculated height to prevent excessive values
        // Allow for some physics settling and gaps between objects
        float maxReasonableHeight = groundHeight + (stackManager?.GetStackCount() ?? 0) * objectSize.y * 1.2f;
        highestPoint = Mathf.Min(highestPoint, maxReasonableHeight);

        // Update current stack height
        currentStackHeight = highestPoint;

        // Set target height for smooth animation (will be animated in Update)
        targetHolderHeight = currentStackHeight + consistentHeightOffset;
    }

    /// <summary>
    /// Smoothly animates the holder height towards the target height
    /// </summary>
    private void AnimateHolderHeight()
    {
        float currentHeight = transform.position.y;

        // Use MoveTowards for smooth, consistent speed animation
        float newHeight = Mathf.MoveTowards(currentHeight, targetHolderHeight, heightAnimationSpeed * Time.deltaTime);

        // Only update if there's a change to avoid unnecessary transforms
        if (Mathf.Abs(newHeight - currentHeight) > 0.001f)
        {
            Vector3 holderPosition = transform.position;
            holderPosition.y = newHeight;
            transform.position = holderPosition;

            // Update the stored center position
            holderCenterPosition = transform.position;
        }
    }

    /// <summary>
    /// Updates swing speed and amplitude based on block count (InfiniteStacker mode only)
    /// or applies level settings (StackerLevels mode)
    /// </summary>
    private void UpdateHeightBasedScaling()
    {
        // Check if we're in level mode - use level multipliers
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.StackerLevels)
        {
            // In level mode, apply level multipliers to original values
            currentScaledSwingSpeed = originalSwingSpeed * levelSwingSpeedMultiplier;
            currentScaledSwingAmplitude = originalSwingAmplitude * levelSwingAmplitudeMultiplier;

            // Apply level-modified values only if recoil is not active
            if (recoilTimer <= 0f)
            {
                swingSpeed = currentScaledSwingSpeed;
                swingAmplitude = currentScaledSwingAmplitude;
            }
            return;
        }

        // Reset level multipliers when not in level mode
        if (levelSwingSpeedMultiplier != 1f || levelSwingAmplitudeMultiplier != 1f)
        {
            levelSwingSpeedMultiplier = 1f;
            levelSwingAmplitudeMultiplier = 1f;
        }

        // Only apply height-based scaling in InfiniteStacker mode
        if (!enableHeightBasedScaling || gameManager == null || gameManager.CurrentGameMode != GameMode.InfiniteStacker)
        {
            // Reset to original values if not in InfiniteStacker mode or scaling is disabled
            currentScaledSwingSpeed = originalSwingSpeed;
            currentScaledSwingAmplitude = originalSwingAmplitude;
            if (recoilTimer <= 0f)
            {
                swingSpeed = originalSwingSpeed;
                swingAmplitude = originalSwingAmplitude;
            }
            return;
        }

        // Get current block count from StackManager
        var stackManager = DependencyRegistry.Find<StackManager>();
        int currentBlockCount = stackManager != null ? stackManager.GetStackCount() : 0;

        // Only apply scaling if we have enough blocks
        if (currentBlockCount < scalingStartBlockCount)
        {
            // Below start block count, use original values
            currentScaledSwingSpeed = originalSwingSpeed;
            currentScaledSwingAmplitude = originalSwingAmplitude;
            if (recoilTimer <= 0f)
            {
                swingSpeed = originalSwingSpeed;
                swingAmplitude = originalSwingAmplitude;
            }
            return;
        }

        // Calculate normalized progress (0 = at start block count, 1 = very high stacks)
        // Use a reasonable maximum block count for scaling calculation (e.g., 50 blocks above start)
        int maxScalingBlockCount = scalingStartBlockCount + 50;
        int blocksAboveStart = currentBlockCount - scalingStartBlockCount;
        float normalizedProgress = Mathf.Clamp01((float)blocksAboveStart / (float)(maxScalingBlockCount - scalingStartBlockCount));

        // Apply curve to the normalized progress
        float curvedProgress = Mathf.Pow(normalizedProgress, 1f / scalingCurve);

        // Calculate multipliers (1.0 at start, maxMultiplier at high stacks)
        float speedMultiplier = Mathf.Lerp(1f, maxSwingSpeedMultiplier, curvedProgress);
        float amplitudeMultiplier = Mathf.Lerp(1f, maxSwingAmplitudeMultiplier, curvedProgress);

        // Calculate and store scaled base values
        currentScaledSwingSpeed = originalSwingSpeed * speedMultiplier;
        currentScaledSwingAmplitude = originalSwingAmplitude * amplitudeMultiplier;

        // Apply scaled values only if recoil is not active (recoil will override)
        if (recoilTimer <= 0f)
        {
            swingSpeed = currentScaledSwingSpeed;
            swingAmplitude = currentScaledSwingAmplitude;
        }
    }

    /// <summary>
    /// Get the top position of an object using consistent calculation method
    /// </summary>
    private float GetObjectTopPosition(StackableObject stackableObj, Vector2 defaultSize)
    {
        if (stackableObj == null) return 0f;

        // Get the actual collider size if available, otherwise use default
        BoxCollider2D collider = stackableObj.Collider as BoxCollider2D;
        float objectHeight = collider != null ? collider.size.y : defaultSize.y;

        // Calculate the top position: center position + half height
        // In Unity, transform.position is the center of the object
        return stackableObj.transform.position.y + (objectHeight * 0.5f);
    }

    /// <summary>
    /// Get the actual stack height more accurately by sorting objects by height
    /// </summary>
    private float GetActualStackHeight(Vector2 objectSize)
    {
        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager == null || stackManager.GetStackCount() == 0)
        {
            return groundHeight;
        }

        var stackObjects = stackManager.GetStackObjects();
        if (stackObjects.Count == 0) return groundHeight;

        // Sort objects by Y position to find the actual top
        float maxHeight = groundHeight;

        foreach (var stackObj in stackObjects)
        {
            if (stackObj != null)
            {
                float objectTop = GetObjectTopPosition(stackObj, objectSize);
                maxHeight = Mathf.Max(maxHeight, objectTop);
            }
        }

        return maxHeight;
    }


    private void OnObjectSpawned(GameObject spawnedObject)
    {
        // Reset the recoil effect when a new object spawns
        ResetRecoilEffect();
    }

    private void OnObjectDropped(GameObject droppedObject)
    {
        // Track the last dropped object so we can follow its position
        lastDroppedObject = droppedObject;

        // Apply recoil effect to the spawner holder for a natural feel
        if (enableDropRecoil)
        {
            ApplyDropRecoil();
        }

        // Randomize swing start angle for unpredictability
        if (randomizeStartAngle)
        {
            RandomizeSwingStartAngle();
        }

        // Subscribe to the dropped object's landing event to know when it successfully lands
        StackableObject stackableObj = droppedObject.GetComponent<StackableObject>();
        if (stackableObj != null)
        {
            stackableObj.OnObjectLanded += OnObjectLanded;
        }
    }

    private void ApplyDropRecoil()
    {
        // Start the recoil timer
        recoilTimer = recoilDuration;

        // Boost the swing speed temporarily (use current scaled speed as base, not original)
        swingSpeed = currentScaledSwingSpeed * recoilSwingBoost;

        // Boost the rotation multiplier temporarily
        if (enableRotation)
        {
            rotationMultiplier = originalRotationMultiplier * recoilRotationBoost;
        }

        // Apply directional impulse to the swing
        if (enableDirectionalRecoil)
        {
            // Randomly push the swing left or right
            float direction = Random.Range(-1f, 1f);
            swingTime += direction * recoilDirectionStrength;
        }
    }

    private void ResetRecoilEffect()
    {
        // Cancel any ongoing recoil
        recoilTimer = 0f;

        // Immediately restore to scaled values (height-based scaling will set these correctly)
        swingSpeed = currentScaledSwingSpeed;
        rotationMultiplier = originalRotationMultiplier;
    }

    private void UpdateRecoilEffect()
    {
        if (recoilTimer > 0f)
        {
            recoilTimer -= Time.deltaTime;

            // Calculate the progress of the recoil recovery (0 = just started, 1 = finished)
            float recoveryProgress = 1f - (recoilTimer / recoilDuration);

            // Smoothly interpolate back to scaled values using ease-out
            float easedProgress = 1f - Mathf.Pow(1f - recoveryProgress, 2f);

            // Interpolate swing speed from recoiled to scaled base (use current scaled speed as target)
            float recoiledSpeed = currentScaledSwingSpeed * recoilSwingBoost;
            swingSpeed = Mathf.Lerp(recoiledSpeed, currentScaledSwingSpeed, easedProgress);

            // Restore rotation multiplier
            if (enableRotation)
            {
                rotationMultiplier = Mathf.Lerp(originalRotationMultiplier * recoilRotationBoost, originalRotationMultiplier, easedProgress);
            }

            // If timer is done, ensure we're back to exact scaled values
            if (recoilTimer <= 0f)
            {
                swingSpeed = currentScaledSwingSpeed;
                rotationMultiplier = originalRotationMultiplier;
            }
        }
    }

    private void OnObjectAddedToStack(StackableObject stackableObject)
    {
        // Object has been successfully added to the stack, now update stack height
        UpdateStackHeight();
    }

    private void OnObjectLanded(StackableObject landedObject, float landingAccuracy)
    {
        // Unsubscribe from this object's landing event since it has landed
        landedObject.OnObjectLanded -= OnObjectLanded;

        // Update stack height now that we know the object has landed successfully
        UpdateStackHeight();
    }


    private float GetGroundHeight()
    {
        if (ground == null) return 0f;

        // Use the proper method to get ground top height
        return ground.GetGroundTop();
    }


    private void RandomizeSwingStartAngle()
    {
        // Generate a random offset to the swing time to create different starting angles
        // This makes each swing cycle start at a different point
        float randomOffset = Random.Range(0f, 2f * Mathf.PI); // Full circle in radians


        if (useCircularMotion)
        {
            // For circular motion, offset the swing time by a random amount
            swingTime = randomOffset;
        }
        else
        {
            // For pendulum motion, offset the swing time by a random amount
            swingTime = randomOffset;
        }
    }


    private void UpdateSwingMotion()
    {
        // Increment swing time
        swingTime += swingSpeed * Time.deltaTime;
    }


    private void ApplySwingToSpawner()
    {
        Vector3 swingOffset = Vector3.zero;


        if (useCircularMotion)
        {
            // Circular motion - more fluid swinging
            float x = Mathf.Sin(swingTime) * swingAmplitude;
            float y = Mathf.Cos(swingTime * 0.5f) * (swingAmplitude * 0.3f); // Subtle vertical movement
            swingOffset = new Vector3(x, y, 0f);

            // Calculate rotation for circular motion

            if (enableRotation)
            {
                targetRotationZ = Mathf.Sin(swingTime * 0.5f) * swingAngle * rotationMultiplier;
            }
        }
        else
        {
            // Pendulum motion - more realistic swinging
            float pendulumAngle = Mathf.Sin(swingTime) * swingAngle;
            float radians = pendulumAngle * Mathf.Deg2Rad;

            // Calculate pendulum position

            float x = Mathf.Sin(radians) * swingAmplitude;
            float y = -Mathf.Abs(Mathf.Cos(radians)) * (swingAmplitude * 0.2f); // Slight vertical dip
            swingOffset = new Vector3(x, y, 0f);

            // Calculate rotation for pendulum motion - spawner should tilt with the swing

            if (enableRotation)
            {
                targetRotationZ = pendulumAngle * rotationMultiplier;
            }
        }

        // Apply constraints if needed

        if (constrainToScreen && mainCamera != null)
        {
            swingOffset = ApplyScreenConstraints(swingOffset);
        }

        // Calculate final spawner position

        Vector3 finalSpawnerPosition = holderCenterPosition + spawnerOffset + swingOffset;

        // Apply the position to the spawner (this will override the spawner's Y position management)

        objectSpawner.transform.position = finalSpawnerPosition;

        // Apply rotation to the spawner

        if (enableRotation)
        {
            // Smoothly interpolate to the target rotation
            previousRotationZ = currentRotationZ;
            currentRotationZ = Mathf.LerpAngle(currentRotationZ, targetRotationZ, rotationSmoothing * Time.deltaTime);
            objectSpawner.transform.rotation = Quaternion.Euler(0f, 0f, currentRotationZ);

            // Calculate angular velocity of the spawner (degrees per second)
            float rotationDelta = Mathf.DeltaAngle(previousRotationZ, currentRotationZ);
            spawnerAngularVelocity = rotationDelta / Time.deltaTime;
        }

        // Notify listeners

        OnSpawnerPositionChanged?.Invoke(finalSpawnerPosition);

        // Update neck pieces target positions and rotations in a chain (top piece follows spawner)
        if (neckPieces != null && neckPieces.Length > 0 && enableNeckFollow && neckPieceData != null)
        {
            // Calculate the movement difference from holder center to spawner
            Vector3 spawnerBasePosition = holderCenterPosition + spawnerOffset;
            Vector3 spawnerMovement = finalSpawnerPosition - spawnerBasePosition;
            float spawnerRotationOffset = enableRotation ? currentRotationZ : 0f; // Spawner's rotation as an offset from identity

            // Process each neck piece in order (top to bottom)
            Vector3 previousPiecePosition = finalSpawnerPosition;
            float previousPieceRotationOffset = spawnerRotationOffset; // Rotation offset to pass down the chain

            for (int i = 0; i < neckPieces.Length; i++)
            {
                if (neckPieces[i] == null) continue;

                // Calculate movement from the piece/spawner above (relative to its base position)
                Vector3 previousBasePosition = i == 0 ? spawnerBasePosition : (holderCenterPosition + neckPieceData[i - 1].baseOffset);
                Vector3 movementFromPrevious = previousPiecePosition - previousBasePosition;

                // Apply reduction factor for this piece's movement
                // Each piece moves less than the one above it
                neckPieceData[i].targetPosition = holderCenterPosition + neckPieceData[i].baseOffset + (movementFromPrevious * neckMovementReduction);

                // Apply rotation reduction for this piece (as an offset to original orientation)
                // Add the reduced rotation offset to the original orientation
                neckPieceData[i].followRotationOffset = previousPieceRotationOffset * neckRotationReduction;
                neckPieceData[i].targetRotationZ = neckPieceData[i].originalRotationZ + neckPieceData[i].followRotationOffset;

                // Update for next piece to follow this one (pass down the follow offset)
                previousPiecePosition = neckPieceData[i].targetPosition;
                previousPieceRotationOffset = neckPieceData[i].followRotationOffset;
            }
        }
    }

    /// <summary>
    /// Updates all neck pieces' positions and rotations to smoothly follow in a chain with lag
    /// </summary>
    private void UpdateNeckFollow()
    {
        if (neckPieces == null || neckPieces.Length == 0 || !enableNeckFollow || neckPieceData == null) return;

        // Smoothly lerp each neck piece towards its target
        for (int i = 0; i < neckPieces.Length; i++)
        {
            if (neckPieces[i] == null) continue;

            // Smoothly lerp position towards target
            neckPiecePositions[i] = Vector3.Lerp(neckPiecePositions[i], neckPieceData[i].targetPosition, neckFollowSpeed * Time.deltaTime);
            neckPieces[i].position = neckPiecePositions[i];

            // Smoothly lerp rotation towards target (preserving original orientation)
            neckPieceData[i].currentRotationZ = Mathf.LerpAngle(neckPieceData[i].currentRotationZ, neckPieceData[i].targetRotationZ, neckFollowSpeed * Time.deltaTime);
            neckPieces[i].rotation = Quaternion.Euler(0f, 0f, neckPieceData[i].currentRotationZ);
        }
    }


    private Vector3 ApplyScreenConstraints(Vector3 swingOffset)
    {
        if (mainCamera == null) return swingOffset;

        // Get screen bounds

        Vector3 screenPos = mainCamera.WorldToScreenPoint(holderCenterPosition + spawnerOffset + swingOffset);

        // Check horizontal constraints

        if (screenPos.x < screenMargin)
        {
            float clampedX = mainCamera.ScreenToWorldPoint(new Vector3(screenMargin, 0, screenPos.z)).x;
            swingOffset.x = clampedX - holderCenterPosition.x - spawnerOffset.x;
        }
        else if (screenPos.x > Screen.width - screenMargin)
        {
            float clampedX = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width - screenMargin, 0, screenPos.z)).x;
            swingOffset.x = clampedX - holderCenterPosition.x - spawnerOffset.x;
        }


        return swingOffset;
    }

    // Public methods to control the swing

    public void SetSwingAmplitude(float amplitude)
    {
        swingAmplitude = Mathf.Max(0f, amplitude);
    }


    public void SetSwingSpeed(float speed)
    {
        swingSpeed = Mathf.Max(0.1f, speed);
        originalSwingSpeed = swingSpeed; // Update original value so recoil works correctly
    }


    public void SetSwingAngle(float angle)
    {
        swingAngle = Mathf.Clamp(angle, 0f, 90f);
    }


    public void SetCircularMotion(bool useCircular)
    {
        useCircularMotion = useCircular;
    }


    public void SetRotationEnabled(bool enabled)
    {
        enableRotation = enabled;
        if (!enabled && objectSpawner != null)
        {
            // Reset rotation when disabled
            objectSpawner.transform.rotation = Quaternion.identity;
            currentRotationZ = 0f;
            targetRotationZ = 0f;
        }
    }


    public void SetRotationMultiplier(float multiplier)
    {
        rotationMultiplier = Mathf.Max(0f, multiplier);
        originalRotationMultiplier = rotationMultiplier; // Update original value so recoil works correctly
    }


    public void SetRotationSmoothing(float smoothing)
    {
        rotationSmoothing = Mathf.Max(0.1f, smoothing);
    }


    public void SetGroundHeight(float height)
    {
        groundHeight = height;
    }


    public void SetGround(Ground groundScript)
    {
        ground = groundScript;
        if (ground != null)
        {
            groundHeight = GetGroundHeight();
        }
    }


    public void SetConsistentHeightOffset(float offset)
    {
        consistentHeightOffset = Mathf.Max(0f, offset);
    }


    public void SetRandomizeStartAngle(bool randomize)
    {
        randomizeStartAngle = randomize;
    }


    public void RandomizeSwingNow()
    {
        RandomizeSwingStartAngle();
    }


    public void ResetSwing()
    {
        swingTime = 0f;
        currentRotationZ = 0f;
        targetRotationZ = 0f;
        previousRotationZ = 0f;
        spawnerAngularVelocity = 0f;
        if (objectSpawner != null)
        {
            objectSpawner.transform.position = new Vector3(holderCenterPosition.x, holderCenterPosition.y, holderCenterPosition.z);
            objectSpawner.transform.rotation = Quaternion.identity;
        }
    }

    public void SetDropRecoilEnabled(bool enabled)
    {
        enableDropRecoil = enabled;
    }

    public void SetRecoilSwingBoost(float boost)
    {
        recoilSwingBoost = Mathf.Max(1f, boost);
    }

    public void SetRecoilDuration(float duration)
    {
        recoilDuration = Mathf.Max(0.1f, duration);
    }

    public void SetRecoilRotationBoost(float boost)
    {
        recoilRotationBoost = Mathf.Max(1f, boost);
    }

    public void SetDirectionalRecoilEnabled(bool enabled)
    {
        enableDirectionalRecoil = enabled;
    }

    public void SetRecoilDirectionStrength(float strength)
    {
        recoilDirectionStrength = Mathf.Max(0f, strength);
    }

    public void SetHeightAnimationSpeed(float speed)
    {
        heightAnimationSpeed = Mathf.Max(0.1f, speed);
    }

    // Game event handlers

    private void OnGameStart()
    {
        // Initialize stack height when game starts to ensure proper spawner positioning
        UpdateStackHeight();

        // Optionally adjust swing parameters when game starts
        // Could make the swing more intense during gameplay
    }


    private void OnGameOver()
    {
        // Optionally stop or reduce swing when game ends
        // SetSwingSpeed(0.5f); // Slow down the swing
    }


    private void OnGameRestart()
    {
        // Reset swing and stack height when game restarts
        swingTime = 0f;
        currentStackHeight = groundHeight;
        targetHolderHeight = groundHeight + consistentHeightOffset;
        currentRotationZ = 0f;
        targetRotationZ = 0f;
        previousRotationZ = 0f;
        spawnerAngularVelocity = 0f;
        recoilTimer = 0f;
        lastDroppedObject = null;

        // Re-apply level settings if in level mode, otherwise restore original values
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.StackerLevels && levelManager != null && levelManager.CurrentLevel != null)
        {
            // Re-apply level multipliers
            levelSwingSpeedMultiplier = levelManager.CurrentLevel.swingSpeedMultiplier;
            levelSwingAmplitudeMultiplier = levelManager.CurrentLevel.swingAmplitudeMultiplier;
            currentScaledSwingSpeed = originalSwingSpeed * levelSwingSpeedMultiplier;
            currentScaledSwingAmplitude = originalSwingAmplitude * levelSwingAmplitudeMultiplier;
        }
        else
        {
            // Restore original swing and rotation values
            levelSwingSpeedMultiplier = 1f;
            levelSwingAmplitudeMultiplier = 1f;
            currentScaledSwingSpeed = originalSwingSpeed;
            currentScaledSwingAmplitude = originalSwingAmplitude;
        }

        swingSpeed = currentScaledSwingSpeed;
        rotationMultiplier = originalRotationMultiplier;

        // Reset holder height to be above ground with consistent offset (animated, not snapped)
        Vector3 holderPosition = transform.position;
        holderPosition.y = groundHeight + consistentHeightOffset;
        transform.position = holderPosition;
        holderCenterPosition = transform.position;

        // Reset spawner position and rotation
        if (objectSpawner != null)
        {
            objectSpawner.transform.position = holderCenterPosition + spawnerOffset;
            objectSpawner.transform.rotation = Quaternion.identity;
        }

        // Reset neck pieces positions and rotations to original orientations
        if (neckPieces != null && neckPieces.Length > 0 && neckPieceData != null)
        {
            for (int i = 0; i < neckPieces.Length; i++)
            {
                if (neckPieces[i] != null)
                {
                    neckPieceData[i].targetPosition = neckPieces[i].position;
                    neckPiecePositions[i] = neckPieces[i].position;
                    neckPieceData[i].followRotationOffset = 0f;
                    neckPieceData[i].currentRotationZ = neckPieceData[i].originalRotationZ;
                    neckPieceData[i].targetRotationZ = neckPieceData[i].originalRotationZ;
                    neckPieces[i].rotation = Quaternion.Euler(0f, 0f, neckPieceData[i].originalRotationZ);
                }
            }
        }

        // Update stack height to ensure proper positioning
        UpdateStackHeight();
    }


    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<SpawnerHolder>(this);

        // Unsubscribe from spawner events
        if (objectSpawner != null)
        {
            objectSpawner.OnObjectDropped -= OnObjectDropped;
            objectSpawner.OnObjectSpawned -= OnObjectSpawned;
        }

        // Unsubscribe from stack manager events
        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }

        // Unsubscribe from game events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameStart -= OnGameStart;
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnGameRestart -= OnGameRestart;
        }

        // Unsubscribe from level manager events
        if (levelManager != null)
        {
            levelManager.OnLevelLoaded -= OnLevelLoaded;
        }
    }

    /// <summary>
    /// Called when a level is loaded - applies level-specific swing settings from LevelData
    /// </summary>
    private void OnLevelLoaded(LevelData levelData)
    {
        if (levelData == null) return;

        // Store level multipliers
        levelSwingSpeedMultiplier = levelData.swingSpeedMultiplier;
        levelSwingAmplitudeMultiplier = levelData.swingAmplitudeMultiplier;

        // Apply level multipliers to original base values
        currentScaledSwingSpeed = originalSwingSpeed * levelSwingSpeedMultiplier;
        currentScaledSwingAmplitude = originalSwingAmplitude * levelSwingAmplitudeMultiplier;

        // Apply immediately if no recoil is active
        if (recoilTimer <= 0f)
        {
            swingSpeed = currentScaledSwingSpeed;
            swingAmplitude = currentScaledSwingAmplitude;
        }

        Debug.Log($"SpawnerHolder: Applied level settings - Speed: {levelSwingSpeedMultiplier}x, Amplitude: {levelSwingAmplitudeMultiplier}x");
    }

    // Gizmos for visualization in Scene view

    private void OnDrawGizmosSelected()
    {
        if (objectSpawner == null) return;

        // Draw swing range

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(holderCenterPosition + spawnerOffset, swingAmplitude);

        // Draw connection line

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, objectSpawner.transform.position);

        // Draw stack height indicator

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(0, currentStackHeight, 0), new Vector3(2f, 0.1f, 0f));

        // Draw screen constraints if enabled

        if (constrainToScreen && mainCamera != null)
        {
            Gizmos.color = Color.red;
            Vector3 leftBound = mainCamera.ScreenToWorldPoint(new Vector3(screenMargin, Screen.height * 0.5f, 10f));
            Vector3 rightBound = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width - screenMargin, Screen.height * 0.5f, 10f));
            Gizmos.DrawLine(new Vector3(leftBound.x, holderCenterPosition.y - 2f, 0f), new Vector3(leftBound.x, holderCenterPosition.y + 2f, 0f));
            Gizmos.DrawLine(new Vector3(rightBound.x, holderCenterPosition.y - 2f, 0f), new Vector3(rightBound.x, holderCenterPosition.y + 2f, 0f));
        }
    }

    /// <summary>
    /// Debug method to log current height calculations
    /// </summary>
    public void LogHeightDebugInfo()
    {
        var stackManager = DependencyRegistry.Find<StackManager>();
        int stackCount = stackManager?.GetStackCount() ?? 0;

        Debug.Log($"SpawnerHolder Height Debug:\n" +
                  $"Ground Height: {groundHeight:F2}\n" +
                  $"Current Stack Height: {currentStackHeight:F2}\n" +
                  $"Stack Count: {stackCount}\n" +
                  $"Consistent Height Offset: {consistentHeightOffset:F2}\n" +
                  $"Holder Y Position: {transform.position.y:F2}\n" +
                  $"Spawner Y Position: {objectSpawner?.transform.position.y:F2}");
    }

    // Public getters

    public ObjectSpawner ObjectSpawner => objectSpawner;
    public float SwingAmplitude => swingAmplitude;
    public float SwingSpeed => swingSpeed;
    public float SwingAngle => swingAngle;
    public bool UseCircularMotion => useCircularMotion;
    public Vector3 HolderCenterPosition => holderCenterPosition;
    public float CurrentStackHeight => currentStackHeight;
    public bool EnableRotation => enableRotation;
    public float RotationMultiplier => rotationMultiplier;
    public float RotationSmoothing => rotationSmoothing;
    public float CurrentRotationZ => currentRotationZ;
    public float GroundHeight => groundHeight;
    public float ConsistentHeightOffset => consistentHeightOffset;
    public Ground Ground => ground;
    public bool RandomizeStartAngle => randomizeStartAngle;
    public bool EnableDropRecoil => enableDropRecoil;
    public float RecoilSwingBoost => recoilSwingBoost;
    public float RecoilDuration => recoilDuration;
    public float RecoilRotationBoost => recoilRotationBoost;
    public bool EnableDirectionalRecoil => enableDirectionalRecoil;
    public float RecoilDirectionStrength => recoilDirectionStrength;
    public float SpawnerAngularVelocity => spawnerAngularVelocity;
    public float HeightAnimationSpeed => heightAnimationSpeed;
}
