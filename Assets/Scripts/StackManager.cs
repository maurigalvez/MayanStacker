using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StackManager : MonoBehaviour
{
    [Header("Stack Settings")]
    [SerializeField] private float balanceThreshold = 15f; // Degrees of tilt before stack falls
    [SerializeField] private float fallMargin = 1f; // Margin below ground or stack before considering fallen
    [SerializeField] private float stabilityCheckDelay = 2f; // Delay before checking stability after landing

    // Stack tracking
    private List<StackableObject> stackObjects = new List<StackableObject>();
    private bool isCheckingStability = false;

    // Ground reference
    private Ground ground;

    // Events
    public System.Action OnStackFall;
    public System.Action<StackableObject> OnObjectAddedToStack;
    public System.Action<StackableObject> OnObjectRemovedFromStack;

    // Singleton pattern for easy access
    private static StackManager instance;
    public static StackManager Instance => instance;

    private void Awake()
    {
        // Singleton setup
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
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
        }

        // Get ground reference
        ground = DependencyRegistry.Find<Ground>();
        if (ground == null)
        {
            ground = FindFirstObjectByType<Ground>();
        }
    }

    /// <summary>
    /// Add an object to the stack when it lands
    /// </summary>
    public void AddObjectToStack(StackableObject stackableObject)
    {
        if (stackableObject == null || stackObjects.Contains(stackableObject)) return;

        stackObjects.Add(stackableObject);
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
            OnObjectRemovedFromStack?.Invoke(stackableObject);
        }
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

        // Clear the list
        stackObjects.Clear();
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
    /// Get the ground reference
    /// </summary>
    public Ground GetGround()
    {
        return ground;
    }

    private void OnGameRestart()
    {
        // Clear the stack when game restarts
        ClearStack();

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
        }
    }
}
