using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float orthographicSize = 8f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -10);
    [SerializeField] private bool followStack = false;
    [SerializeField] private float followSpeed = 2f;
    [SerializeField] private float cameraHeightOffset = 5f; // Height offset from the highest object

    [Header("Camera Bounds")]
    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 15f;

    [Header("Screen Shake")]
    [Tooltip("Maximum positional shake offset (world units) at full trauma")]
    [SerializeField] private float shakeMaxOffset = 0.55f;
    [Tooltip("Maximum rotational shake (degrees) at full trauma")]
    [SerializeField] private float shakeMaxAngle = 2.5f;
    [Tooltip("How quickly trauma decays back to zero (higher = snappier)")]
    [SerializeField] private float shakeDecay = 1.6f;
    [Tooltip("Noise frequency of the shake wobble")]
    [SerializeField] private float shakeFrequency = 26f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // References
    private Camera cam;
    private StackManager stackManager;
    private Ground ground;
    private Vector3 targetPosition;

    // Shake state - CameraController is the single writer of transform.position,
    // so shake is applied as an offset on top of a tracked base position.
    private Vector3 basePosition;
    private Vector3 appliedShakeOffset;
    private float trauma;              // 0..1, shake intensity that decays over time
    private float shakeSeed;           // per-instance offset so multiple cams don't sync

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<CameraController>(this);
    }

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }

        // Get StackManager reference
        stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager == null)
        {
            stackManager = StackManager.Instance;
        }

        // Get Ground reference
        ground = DependencyRegistry.Find<Ground>();
        if (ground == null)
        {
            ground = FindFirstObjectByType<Ground>();
        }

        // Set up camera
        SetupCamera();

        // Set initial target position
        targetPosition = transform.position;
        basePosition = transform.position;
        shakeSeed = Random.value * 100f;
    }

    private void Update()
    {
        if (followStack)
        {
            UpdateCameraFollow();
        }
    }

    /// <summary>
    /// Applies the shake offset on top of the base position. Runs in LateUpdate so it
    /// always layers on after any follow logic in Update, keeping a single writer.
    /// </summary>
    private void LateUpdate()
    {
        if (trauma > 0f)
        {
            // Quadratic falloff feels punchier than linear.
            float shake = trauma * trauma;
            // Use unscaled time so the shake keeps animating during hit-stop (timeScale dip).
            float t = Time.unscaledTime * shakeFrequency + shakeSeed;

            float offX = (Mathf.PerlinNoise(t, 0f) * 2f - 1f) * shakeMaxOffset * shake;
            float offY = (Mathf.PerlinNoise(0f, t) * 2f - 1f) * shakeMaxOffset * shake;
            float angle = (Mathf.PerlinNoise(t, t) * 2f - 1f) * shakeMaxAngle * shake;

            appliedShakeOffset = new Vector3(offX, offY, 0f);
            transform.position = basePosition + appliedShakeOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            trauma = Mathf.Max(0f, trauma - shakeDecay * Time.unscaledDeltaTime);
        }
        else if (appliedShakeOffset != Vector3.zero)
        {
            // Settle back exactly to base once the shake finishes.
            appliedShakeOffset = Vector3.zero;
            transform.position = basePosition;
            transform.rotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Adds trauma to the camera, producing a decaying shake. Gated by the
    /// player's screen-shake setting. amount is 0..1 (values stack up to 1).
    /// </summary>
    public void Shake(float amount)
    {
        if (!GameFeelSettings.ScreenShakeEnabled) return;
        trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
    }

    private void SetupCamera()
    {
        if (cam == null) return;

        // Set orthographic size
        cam.orthographicSize = orthographicSize;

        // Set camera position
        transform.position = cameraOffset;

        // Set background color
        cam.backgroundColor = new Color(0.2f, 0.3f, 0.4f); // Nice blue background
    }

    private void UpdateCameraFollow()
    {
        if (stackManager == null) return;

        // Get stack objects
        List<StackableObject> stackObjects = stackManager.GetStackObjects();

        // Only follow if there's at least one object in the stack
        if (stackObjects.Count > 0)
        {
            // Find the highest stackable object
            float highestY = GetHighestStackableY();

            // Position camera to keep the highest object in view with offset
            targetPosition.y = Mathf.Clamp(highestY + cameraHeightOffset, minY, maxY);
        }
        else
        {
            // No objects in stack, follow the ground
            if (ground != null)
            {
                float groundY = ground.GetGroundTop();
                targetPosition.y = Mathf.Clamp(groundY + cameraHeightOffset, minY, maxY);

                if (debugMode)
                {
                    Debug.Log($"CameraController: No stack objects, following Ground at Y: {groundY:F2}");
                }
            }
            else
            {
                // Fallback to initial camera position if no ground found
                targetPosition.y = cameraOffset.y;

                if (debugMode)
                {
                    Debug.LogWarning("CameraController: No stack objects and no Ground found, using camera offset");
                }
            }
        }

        // Smoothly move camera. Write the base position (not the transform directly) so the
        // shake offset applied in LateUpdate is layered on top rather than fought over.
        Vector3 newPosition = Vector3.Lerp(basePosition, targetPosition, followSpeed * Time.deltaTime);
        newPosition.z = cameraOffset.z; // Keep Z offset
        basePosition = newPosition;

        // If no shake is active, keep the transform in sync immediately.
        if (trauma <= 0f && appliedShakeOffset == Vector3.zero)
        {
            transform.position = basePosition;
        }
    }

    private float GetHighestStackableY()
    {
        if (stackManager == null)
        {
            if (debugMode)
            {
                Debug.LogWarning("CameraController: StackManager is null!");
            }
            return 0f;
        }

        List<StackableObject> stackableObjects = stackManager.GetStackObjects();
        float highestY = 0f;
        StackableObject highestObject = null;

        foreach (StackableObject obj in stackableObjects)
        {
            if (obj != null && obj.transform.position.y > highestY)
            {
                highestY = obj.transform.position.y;
                highestObject = obj;
            }
        }

        if (debugMode && stackableObjects.Count > 0)
        {
            Debug.Log($"CameraController: Stack Count: {stackableObjects.Count}, Highest Y: {highestY:F2}, Highest Object: {(highestObject != null ? highestObject.name : "None")}");
        }

        return highestY;
    }

    public void SetFollowStack(bool follow)
    {
        followStack = follow;
    }

    public void ResetCamera()
    {
        targetPosition = cameraOffset;
        basePosition = cameraOffset;
        appliedShakeOffset = Vector3.zero;
        trauma = 0f;
        transform.position = targetPosition;
        transform.rotation = Quaternion.identity;
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<CameraController>(this);
    }

    private void OnDrawGizmos()
    {
        if (!debugMode || stackManager == null) return;

        List<StackableObject> stackableObjects = stackManager.GetStackObjects();
        if (stackableObjects.Count == 0) return;

        // Find the highest object
        float highestY = 0f;
        StackableObject highestObject = null;

        foreach (StackableObject obj in stackableObjects)
        {
            if (obj != null && obj.transform.position.y > highestY)
            {
                highestY = obj.transform.position.y;
                highestObject = obj;
            }
        }

        // Draw a visual indicator for the highest stackable object
        if (highestObject != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 position = highestObject.transform.position;
            Gizmos.DrawWireSphere(position, 0.5f);
            Gizmos.DrawLine(position, position + Vector3.up * 2f);
        }
    }
}
