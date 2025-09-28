using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float orthographicSize = 8f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -10);
    [SerializeField] private bool followStack = false;
    [SerializeField] private float followSpeed = 2f;
    [SerializeField] private float maxFollowHeight = 5f;
    
    [Header("Camera Bounds")]
    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 15f;
    
    // References
    private Camera cam;
    private Vector3 targetPosition;
    private float initialY;
    
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
        
        // Set up camera
        SetupCamera();
        
        // Store initial position
        initialY = transform.position.y;
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
        // Find the highest stackable object
        float highestY = GetHighestStackableY();
        
        if (highestY > initialY + maxFollowHeight)
        {
            // Follow the stack as it grows
            targetPosition.y = Mathf.Clamp(highestY - maxFollowHeight, minY, maxY);
        }
        else
        {
            // Return to initial position
            targetPosition.y = initialY;
        }
        
        // Smoothly move camera
        Vector3 newPosition = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        newPosition.z = cameraOffset.z; // Keep Z offset
        transform.position = newPosition;
    }
    
    private float GetHighestStackableY()
    {
        StackableObject[] stackableObjects = FindObjectsOfType<StackableObject>();
        float highestY = 0f;
        
        foreach (StackableObject obj in stackableObjects)
        {
            if (obj.transform.position.y > highestY)
            {
                highestY = obj.transform.position.y;
            }
        }
        
        return highestY;
    }
    
    public void SetFollowStack(bool follow)
    {
        followStack = follow;
    }
    
    public void ResetCamera()
    {
        targetPosition = new Vector3(cameraOffset.x, initialY, cameraOffset.z);
        transform.position = targetPosition;
    }
    
    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<CameraController>(this);
    }
}
