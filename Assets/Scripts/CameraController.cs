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

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // References
    private Camera cam;
    private StackManager stackManager;
    private Ground ground;
    private Vector3 targetPosition;

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
    }

    private void Update()
    {
        if (followStack)
        {
            UpdateCameraFollow();
        }
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

        // Smoothly move camera
        Vector3 newPosition = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        newPosition.z = cameraOffset.z; // Keep Z offset
        transform.position = newPosition;
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
        transform.position = targetPosition;
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
