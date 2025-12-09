using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StackManager : MonoBehaviour
{
    [Header("Stack Settings")]
    [SerializeField] private float balanceThreshold = 15f; // Degrees of tilt before stack falls
    [SerializeField] private float fallMargin = 1f; // Margin below ground or stack before considering fallen
    [SerializeField] private float stabilityCheckDelay = 2f; // Delay before checking stability after landing
    [SerializeField] private float fallVelocityThreshold = -5f; // Downward velocity indicating a fall (more strict)
    [SerializeField] private float horizontalDistanceThreshold = 8f; // Max horizontal distance from stack center
    [SerializeField] private float stackHeightFallThreshold = 4f; // How far below stack top before considered fallen
    [SerializeField] private float settlingTimeGracePeriod = 1.5f; // Time after landing before strict checks apply

    [Header("Stack Straightening")]
    [Tooltip("Duration of the straightening animation in seconds")]
    [SerializeField] private float straighteningDuration = 0.5f;
    [Tooltip("Particle effect to play when Kukulkan shift is triggered")]
    [SerializeField] private ParticleSystem kukulkanShiftParticles;
    [Tooltip("Whether to freeze rotation after straightening")]
    [SerializeField] private bool freezeRotationAfterStraightening = false;
    [Tooltip("Mass multiplier to make blocks heavier after straightening")]
    [SerializeField] private float stabilizedMassMultiplier = 3f;
    [Tooltip("Drag multiplier to reduce movement after straightening")]
    [SerializeField] private float stabilizedDragMultiplier = 5f;
    [Tooltip("Angular drag multiplier to reduce rotation after straightening")]
    [SerializeField] private float stabilizedAngularDragMultiplier = 10f;
    [Tooltip("Friction multiplier to prevent sliding after straightening")]
    [SerializeField] private float stabilizedFrictionMultiplier = 2f;
    [Tooltip("Whether to freeze X position to prevent horizontal sliding")]
    [SerializeField] private bool freezeXPositionAfterStraightening = true;

    // Stack tracking
    private List<StackableObject> stackObjects = new List<StackableObject>();
    private List<StackableObject> droppedObjects = new List<StackableObject>(); // Track all dropped objects
    private Dictionary<StackableObject, float> landingTimes = new Dictionary<StackableObject, float>(); // Track when objects landed
    private HashSet<StackableObject> stabilizedBlocks = new HashSet<StackableObject>(); // Blocks that have been aligned by Kukulkan's shift - no longer need fall detection
    private bool isCheckingStability = false;

    // Cached values for performance (recalculated when stack changes or periodically)
    private float cachedStackTopY = 0f;
    private float cachedStackCenterX = 0f;
    private bool stackCacheDirty = true; // Flag to indicate cache needs recalculation
    [SerializeField] private int cacheUpdateInterval = 3; // Update cache every N FixedUpdates (balance between accuracy and performance)
    private int fixedUpdateCount = 0;

    // Ground reference
    private Ground ground;

    // Helper class to store physics properties
    private class PhysicsProperties
    {
        public float mass;
        public float drag;
        public float angularDrag;
        public float friction;
        public RigidbodyConstraints2D constraints;
    }

    // Events
    public System.Action OnStackFall;
    public System.Action<StackableObject> OnObjectAddedToStack;
    public System.Action<StackableObject> OnObjectRemovedFromStack;
    public System.Action OnStackStraightened; // Fired when stack straightening animation completes

    // Singleton pattern for easy access
    private static StackManager instance;
    public static StackManager Instance => instance;

    private void Awake()
    {
        // Singleton setup
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Register with dependency registry
        DependencyRegistry.Register<StackManager>(this);
    }

    private void Start()
    {
        // Subscribe to game events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameRestart += OnGameRestart;
            gameManager.OnPerfectHitStreak += OnPerfectHitStreak;
        }

        // Get ground reference
        ground = DependencyRegistry.Find<Ground>();
        if (ground == null)
        {
            ground = FindFirstObjectByType<Ground>();
        }

        // Initialize cache
        UpdateStackCache();
    }

    private void FixedUpdate()
    {
        fixedUpdateCount++;

        // Update cached values periodically (every N FixedUpdates) to account for stack tilting/movement
        // Also update immediately if stack structure changed (objects added/removed)
        if (stackCacheDirty || fixedUpdateCount >= cacheUpdateInterval)
        {
            UpdateStackCache();
            fixedUpdateCount = 0;
        }

        // Continuously monitor dropped objects for fall conditions
        CheckDroppedObjectsForFalls();
    }

    /// <summary>
    /// Updates cached stack values (top Y and center X) for performance
    /// </summary>
    private void UpdateStackCache()
    {
        cachedStackTopY = CalculateStackTopY();
        cachedStackCenterX = CalculateStackCenterX();
        stackCacheDirty = false;
    }

    /// <summary>
    /// Continuously check if any dropped objects have fallen below the threshold
    /// </summary>
    private void CheckDroppedObjectsForFalls()
    {
        if (droppedObjects.Count == 0) return;

        // Don't check for game over if we only have the first block
        // The first block can't "fall off" since there's nothing to fall from
        if (stackObjects.Count == 0)
        {
            return;
        }

        // Get ground level for fall detection
        float groundLevel = 0f;
        if (ground != null)
        {
            groundLevel = ground.GetGroundTop();
        }

        // Check each dropped object - ONLY for critical failures
        float fallThreshold = groundLevel - fallMargin * 3f; // Much more lenient - 3x the margin

        // Pre-calculate stack bounds for efficiency (use cached values)
        float stackTopY = cachedStackTopY;
        float stackCenterX = cachedStackCenterX;

        foreach (StackableObject obj in droppedObjects)
        {
            if (obj == null) continue;
            if (!obj.IsDropped) continue;

            // Skip blocks that have been stabilized by Kukulkan's shift - they're safe and don't need monitoring
            if (stabilizedBlocks.Contains(obj))
            {
                continue;
            }

            // Early exit optimization: Skip objects that are clearly safe (well above threshold)
            // This avoids expensive checks for objects that are clearly not falling
            float objY = obj.transform.position.y;
            if (objY > fallThreshold + 5f) // 5 units above threshold - clearly safe
            {
                continue;
            }

            // Check if object is within grace period (recently landed)
            bool inGracePeriod = false;
            if (landingTimes.ContainsKey(obj))
            {
                float timeSinceLanding = Time.time - landingTimes[obj];
                inGracePeriod = timeSinceLanding < settlingTimeGracePeriod;
            }

            // ONLY CHECK: Fallen significantly below ground threshold
            // This is the ONLY reliable check - block has clearly fallen off the world
            // Skip if in grace period to allow initial settling
            if (!inGracePeriod && objY < fallThreshold)
            {
                Debug.Log($"Game Over: Block fell below ground! Object Y: {objY:F2}, Threshold: {fallThreshold:F2}");
                TriggerStackFall();
                return;
            }

            // SECONDARY CHECK: Block is falling fast AND far from stack
            // Only trigger if BOTH conditions are true to avoid false positives
            // Only check if object is actually near or below stack level (optimization)
            if (!inGracePeriod && obj.HasLanded && stackObjects.Count >= 2 && objY < stackTopY + 2f)
            {
                Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
                if (rb == null) continue;

                // Must be falling fast AND be far below the stack AND far horizontally
                float velocityY = rb.linearVelocity.y;
                bool fallingFast = velocityY < fallVelocityThreshold;
                bool farBelowStack = objY < stackTopY - stackHeightFallThreshold;
                float distanceFromCenter = Mathf.Abs(obj.transform.position.x - stackCenterX);
                bool farFromCenter = distanceFromCenter > horizontalDistanceThreshold;

                // Only trigger if ALL THREE conditions are met (very conservative)
                if (fallingFast && farBelowStack && farFromCenter)
                {
                    Debug.Log($"Game Over: Block clearly falling off! Velocity: {velocityY:F2}, Y: {objY:F2}, Stack Top: {stackTopY:F2}");
                    TriggerStackFall();
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Register a dropped object for monitoring (called when object is dropped)
    /// </summary>
    public void RegisterDroppedObject(StackableObject stackableObject)
    {
        if (stackableObject == null || droppedObjects.Contains(stackableObject)) return;
        droppedObjects.Add(stackableObject);
        // Mark cache as dirty since dropped objects affect stack top calculation
        stackCacheDirty = true;
    }

    /// <summary>
    /// Add an object to the stack when it lands
    /// </summary>
    public void AddObjectToStack(StackableObject stackableObject)
    {
        if (stackableObject == null || stackObjects.Contains(stackableObject)) return;

        stackObjects.Add(stackableObject);

        // Record landing time for grace period
        landingTimes[stackableObject] = Time.time;

        // Mark cache as dirty so it gets recalculated
        stackCacheDirty = true;

        OnObjectAddedToStack?.Invoke(stackableObject);

        // Start stability check after a delay to allow physics to settle
        if (!isCheckingStability)
        {
            StartCoroutine(DelayedStabilityCheck());
        }
    }

    /// <summary>
    /// Remove an object from the stack (when it falls off or is destroyed)
    /// </summary>
    public void RemoveObjectFromStack(StackableObject stackableObject)
    {
        if (stackableObject == null) return;

        bool removed = stackObjects.Remove(stackableObject);
        if (removed)
        {
            // Mark cache as dirty so it gets recalculated
            stackCacheDirty = true;
            OnObjectRemovedFromStack?.Invoke(stackableObject);
        }

        // Also remove from dropped objects list, landing times, and stabilized blocks
        droppedObjects.Remove(stackableObject);
        landingTimes.Remove(stackableObject);
        stabilizedBlocks.Remove(stackableObject);
    }

    /// <summary>
    /// Coroutine to check stability after a delay
    /// </summary>
    private IEnumerator DelayedStabilityCheck()
    {
        isCheckingStability = true;
        yield return new WaitForSeconds(stabilityCheckDelay);

        CheckGameOverConditions();
        isCheckingStability = false;
    }

    /// <summary>
    /// Check all game over conditions comprehensively
    /// </summary>
    private void CheckGameOverConditions()
    {
        if (stackObjects.Count == 0) return;

        // Check for objects that have fallen off the tower
        if (CheckForFallenObjects())
        {
            Debug.Log("Game Over: Objects have fallen off the tower!");
            TriggerStackFall();
            return;
        }

        // Note: Removed tower height limit to allow unlimited building height

        // Check for excessive tilt only if we have enough objects
        if (stackObjects.Count >= 3 && CheckExcessiveTilt())
        {
            Debug.Log("Game Over: Tower has excessive tilt!");
            TriggerStackFall();
            return;
        }

        // Check if the tower structure is still intact
        /*if (CheckTowerIntegrity())
        {
            Debug.Log("Game Over: Tower structure has collapsed!");
            TriggerStackFall();
            return;
        }*/
    }

    /// <summary>
    /// Check if any objects have fallen below the ground or significantly below the stack
    /// </summary>
    private bool CheckForFallenObjects()
    {
        if (stackObjects.Count == 0) return false;

        // Calculate the effective ground level (either ground top or bottom of stack)
        float effectiveGroundLevel = GetEffectiveGroundLevel();

        // Calculate the fall threshold (ground level minus margin)
        float fallThreshold = effectiveGroundLevel - fallMargin;

        foreach (StackableObject obj in stackObjects)
        {
            if (obj == null) continue;

            // Check if object has fallen below the effective ground level with margin
            if (obj.transform.position.y < fallThreshold)
            {
                Debug.Log($"Object fell below ground! Object Y: {obj.transform.position.y:F2}, Ground Level: {effectiveGroundLevel:F2}, Threshold: {fallThreshold:F2}");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get the effective ground level - either the ground top or the bottom of the stack
    /// </summary>
    private float GetEffectiveGroundLevel()
    {
        // Start with ground level as baseline
        float groundLevel = 0f;
        if (ground != null)
        {
            groundLevel = ground.GetGroundTop();
        }

        // If we have objects in the stack, use the lowest object position as the effective ground
        if (stackObjects.Count > 0)
        {
            float lowestObjectY = float.MaxValue;
            foreach (StackableObject obj in stackObjects)
            {
                if (obj != null && obj.transform.position.y < lowestObjectY)
                {
                    lowestObjectY = obj.transform.position.y;
                }
            }

            // Use the lower of ground level or lowest object position
            groundLevel = Mathf.Min(groundLevel, lowestObjectY);
        }

        return groundLevel;
    }

    /// <summary>
    /// Check for excessive tilt in the stack
    /// </summary>
    private bool CheckExcessiveTilt()
    {
        foreach (StackableObject obj in stackObjects)
        {
            if (obj == null) continue;

            float tiltAngle = Mathf.Abs(obj.transform.rotation.eulerAngles.z);
            // Normalize angle to 0-180 range
            if (tiltAngle > 180f) tiltAngle = 360f - tiltAngle;

            if (tiltAngle > balanceThreshold)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Check if the tower structure is still intact (no major gaps or separations)
    /// </summary>
    private bool CheckTowerIntegrity()
    {
        if (stackObjects.Count < 2) return false;

        // Sort objects by height
        List<StackableObject> sortedObjects = new List<StackableObject>(stackObjects);
        sortedObjects.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

        // Check for significant gaps between consecutive objects
        for (int i = 0; i < sortedObjects.Count - 1; i++)
        {
            if (sortedObjects[i] == null || sortedObjects[i + 1] == null) continue;

            float heightDifference = sortedObjects[i + 1].transform.position.y - sortedObjects[i].transform.position.y;

            // If there's a large gap (more than 2 object heights), the tower is broken
            if (heightDifference > 2f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Trigger stack fall and notify game manager
    /// </summary>
    private void TriggerStackFall()
    {
        // Notify game manager that the stack has fallen
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.GameOver();
        }

        // Notify listeners
        OnStackFall?.Invoke();
    }

    /// <summary>
    /// Get all objects currently in the stack
    /// </summary>
    public List<StackableObject> GetStackObjects()
    {
        // Return a copy to prevent external modification
        return new List<StackableObject>(stackObjects);
    }

    /// <summary>
    /// Get the number of objects in the stack
    /// </summary>
    public int GetStackCount()
    {
        return stackObjects.Count;
    }

    /// <summary>
    /// Get the top object in the stack (most recently added)
    /// </summary>
    public StackableObject GetTopObject()
    {
        return stackObjects.Count > 0 ? stackObjects[stackObjects.Count - 1] : null;
    }

    /// <summary>
    /// Get the bottom object in the stack (first added)
    /// </summary>
    public StackableObject GetBottomObject()
    {
        return stackObjects.Count > 0 ? stackObjects[0] : null;
    }

    /// <summary>
    /// Clear all objects from the stack
    /// </summary>
    public void ClearStack()
    {
        // Destroy all physical objects in the stack
        foreach (StackableObject obj in stackObjects)
        {
            if (obj != null && obj.gameObject != null)
            {
                Destroy(obj.gameObject);
            }
        }

        // Destroy all dropped objects
        foreach (StackableObject obj in droppedObjects)
        {
            if (obj != null && obj.gameObject != null && !stackObjects.Contains(obj))
            {
                Destroy(obj.gameObject);
            }
        }

        // Clear the lists
        stackObjects.Clear();
        droppedObjects.Clear();
        landingTimes.Clear();
        stabilizedBlocks.Clear(); // Clear stabilized blocks when stack is cleared

        // Reset cache
        stackCacheDirty = true;
        UpdateStackCache();
    }

    /// <summary>
    /// Manually trigger a stability check (useful for testing or immediate checks)
    /// </summary>
    public void TriggerStabilityCheck()
    {
        if (!isCheckingStability)
        {
            StartCoroutine(DelayedStabilityCheck());
        }
    }

    /// <summary>
    /// Get the current effective ground level (ground top or bottom of stack)
    /// </summary>
    public float GetCurrentGroundLevel()
    {
        return GetEffectiveGroundLevel();
    }

    /// <summary>
    /// Get the horizontal center position of the stack (uses cached value)
    /// </summary>
    private float GetStackCenterX()
    {
        if (stackCacheDirty)
        {
            UpdateStackCache();
        }
        return cachedStackCenterX;
    }

    /// <summary>
    /// Calculate the horizontal center position of the stack (internal calculation)
    /// </summary>
    private float CalculateStackCenterX()
    {
        if (stackObjects.Count == 0 && droppedObjects.Count == 0)
        {
            return 0f; // Default to world center if no objects
        }

        float totalX = 0f;
        int count = 0;

        // Use stack objects for center calculation (more stable)
        if (stackObjects.Count > 0)
        {
            foreach (StackableObject obj in stackObjects)
            {
                if (obj != null)
                {
                    totalX += obj.transform.position.x;
                    count++;
                }
            }
        }
        else if (droppedObjects.Count > 0)
        {
            // Fallback to dropped objects if no stack yet
            foreach (StackableObject obj in droppedObjects)
            {
                if (obj != null)
                {
                    totalX += obj.transform.position.x;
                    count++;
                }
            }
        }

        return count > 0 ? totalX / count : 0f;
    }

    /// <summary>
    /// Get the Y position of the top of the stack (uses cached value)
    /// </summary>
    private float GetStackTopY()
    {
        if (stackCacheDirty)
        {
            UpdateStackCache();
        }
        return cachedStackTopY;
    }

    /// <summary>
    /// Calculate the Y position of the top of the stack (internal calculation)
    /// </summary>
    private float CalculateStackTopY()
    {
        if (stackObjects.Count == 0 && droppedObjects.Count == 0)
        {
            // Return ground level if no objects
            return ground != null ? ground.GetGroundTop() : 0f;
        }

        float highestY = float.MinValue;

        // Check stack objects
        foreach (StackableObject obj in stackObjects)
        {
            if (obj != null && obj.transform.position.y > highestY)
            {
                highestY = obj.transform.position.y;
            }
        }

        // Also check dropped objects for accurate top position
        foreach (StackableObject obj in droppedObjects)
        {
            if (obj != null && obj.transform.position.y > highestY)
            {
                highestY = obj.transform.position.y;
            }
        }

        return highestY != float.MinValue ? highestY : (ground != null ? ground.GetGroundTop() : 0f);
    }

    /// <summary>
    /// Get the ground reference
    /// </summary>
    public Ground GetGround()
    {
        return ground;
    }

    /// <summary>
    /// Called when perfect hit streak is achieved - straightens and stabilizes the stack
    /// </summary>
    private void OnPerfectHitStreak()
    {
        if (stackObjects.Count == 0) return;

        // Don't straighten if game is over
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null && gameManager.IsGameOver)
        {
            return;
        }

        Debug.Log($"Straightening stack with {stackObjects.Count} objects");

        // Play particle effect for Kukulkan shift
        if (kukulkanShiftParticles != null)
        {
            kukulkanShiftParticles.Play();
        }

        // Notify that stack straightening is starting (for UI display)
        OnStackStraightened?.Invoke();

        StartCoroutine(StraightenStackCoroutine());
    }

    /// <summary>
    /// Coroutine to smoothly straighten and stabilize the stack
    /// </summary>
    private IEnumerator StraightenStackCoroutine()
    {
        if (stackObjects.Count == 0) yield break;

        // Sort objects by Y position (bottom to top) to find the first block
        List<StackableObject> sortedObjects = new List<StackableObject>(stackObjects);
        sortedObjects.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

        // Get the first block's X position (bottom block)
        float firstBlockX = sortedObjects.Count > 0 && sortedObjects[0] != null
            ? sortedObjects[0].transform.position.x
            : 0f;

        // Store initial positions, rotations, and rigidbody states
        Dictionary<StackableObject, Vector3> initialPositions = new Dictionary<StackableObject, Vector3>();
        Dictionary<StackableObject, Quaternion> initialRotations = new Dictionary<StackableObject, Quaternion>();
        Dictionary<StackableObject, Vector3> targetPositions = new Dictionary<StackableObject, Vector3>();
        Dictionary<StackableObject, RigidbodyType2D> originalBodyTypes = new Dictionary<StackableObject, RigidbodyType2D>();
        Dictionary<StackableObject, PhysicsProperties> originalPhysicsProperties = new Dictionary<StackableObject, PhysicsProperties>();

        // Calculate target positions - align all blocks to first block's X, keep current Y
        foreach (StackableObject obj in sortedObjects)
        {
            if (obj == null) continue;

            Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            Collider2D col = obj.Collider;
            if (col == null) continue;

            // Store initial state
            initialPositions[obj] = obj.transform.position;
            initialRotations[obj] = obj.transform.rotation;
            originalBodyTypes[obj] = rb.bodyType;

            // Store original physics properties
            PhysicsProperties props = new PhysicsProperties
            {
                mass = rb.mass,
                drag = rb.linearDamping,
                angularDrag = rb.angularDamping,
                constraints = rb.constraints
            };

            // Get friction from physics material
            if (col.sharedMaterial != null)
            {
                props.friction = col.sharedMaterial.friction;
            }
            else
            {
                props.friction = 0.6f; // Default
            }

            originalPhysicsProperties[obj] = props;

            // Disable physics by setting to kinematic (prevents collisions during animation)
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // Target position: align X to first block, keep current Y
            Vector3 targetPos = new Vector3(firstBlockX, obj.transform.position.y, obj.transform.position.z);
            targetPositions[obj] = targetPos;
        }

        // Animate to target positions
        float elapsed = 0f;
        while (elapsed < straighteningDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / straighteningDuration);
            // Use smooth step for easing
            float smoothT = t * t * (3f - 2f * t);

            foreach (StackableObject obj in sortedObjects)
            {
                if (obj == null) continue;
                if (!initialPositions.ContainsKey(obj) || !targetPositions.ContainsKey(obj)) continue;

                Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
                if (rb == null) continue;

                // Interpolate position
                Vector3 currentPos = Vector3.Lerp(initialPositions[obj], targetPositions[obj], smoothT);
                obj.transform.position = currentPos;

                // Interpolate rotation to zero
                Quaternion targetRotation = Quaternion.identity;
                obj.transform.rotation = Quaternion.Lerp(initialRotations[obj], targetRotation, smoothT);
            }

            yield return null;
        }

        // Finalize positions and rotations, and make blocks more solid
        foreach (StackableObject obj in sortedObjects)
        {
            if (obj == null) continue;
            if (!targetPositions.ContainsKey(obj)) continue;

            Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            Collider2D col = obj.Collider;
            if (col == null) continue;

            // Set final position and rotation
            obj.transform.position = targetPositions[obj];
            obj.transform.rotation = Quaternion.identity;

            // Restore original body type (back to Dynamic)
            if (originalBodyTypes.ContainsKey(obj))
            {
                rb.bodyType = originalBodyTypes[obj];
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }

            // Apply stabilization - make blocks more solid and grounded
            if (originalPhysicsProperties.ContainsKey(obj))
            {
                PhysicsProperties originalProps = originalPhysicsProperties[obj];

                // Increase mass to make blocks heavier and more stable
                rb.mass = originalProps.mass * stabilizedMassMultiplier;

                // Increase drag to reduce movement
                rb.linearDamping = originalProps.drag * stabilizedDragMultiplier;

                // Increase angular drag to reduce rotation
                rb.angularDamping = originalProps.angularDrag * stabilizedAngularDragMultiplier;

                // Increase friction to prevent sliding
                PhysicsMaterial2D material = col.sharedMaterial;
                if (material == null)
                {
                    material = new PhysicsMaterial2D("StabilizedStackableMaterial");
                    col.sharedMaterial = material;
                }
                material.friction = originalProps.friction * stabilizedFrictionMultiplier;
            }

            // Set constraints for stability
            RigidbodyConstraints2D constraints = RigidbodyConstraints2D.None;

            if (freezeRotationAfterStraightening)
            {
                constraints |= RigidbodyConstraints2D.FreezeRotation;
            }

            if (freezeXPositionAfterStraightening)
            {
                constraints |= RigidbodyConstraints2D.FreezePositionX;
            }

            rb.constraints = constraints;

            // Reset velocities
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // Mark this block as stabilized - it no longer needs fall detection monitoring
            stabilizedBlocks.Add(obj);
        }

        Debug.Log($"Stack straightened and stabilized! {sortedObjects.Count} blocks marked as stabilized.");
    }

    private void OnGameRestart()
    {
        // Clear the stack when game restarts
        ClearStack();

        // Clear stabilized blocks set
        stabilizedBlocks.Clear();

        // Reset stability checking state
        isCheckingStability = false;
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<StackManager>(this);

        // Unsubscribe from events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameRestart -= OnGameRestart;
            gameManager.OnPerfectHitStreak -= OnPerfectHitStreak;
        }
    }
}
