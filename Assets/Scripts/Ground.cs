using UnityEngine;

public class Ground : MonoBehaviour
{
    [Header("Ground Settings")]
    [SerializeField] private float groundHeight = 0.5f;
    [SerializeField] private Color groundColor = Color.gray;
    
    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<Ground>(this);
    }
    
    private void Start()
    {
        SetupGround();
    }
    
    private void SetupGround()
    {
        // Set up the ground as a static collider
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // Set up sprite renderer
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        // Create ground sprite
        CreateGroundSprite();
        
        // Set up collider size
        Vector2 screenSize = GetScreenSize();
        collider.size = new Vector2(screenSize.x * 2, groundHeight);
        
        // Set up transform
        transform.localScale = new Vector2(screenSize.x * 2, groundHeight);
        transform.position = new Vector3(0, transform.position.y, 0);
        
        // Set tag
        gameObject.tag = "Ground";
        
        // Make it static
        gameObject.isStatic = true;
    }
    
    private void CreateGroundSprite()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;
        
        // Use the current set sprite and apply the ground color
        spriteRenderer.color = groundColor;
    }
    
    private Vector2 GetScreenSize()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
        
        if (mainCamera != null)
        {
            float height = mainCamera.orthographicSize * 2;
            float width = height * mainCamera.aspect;
            return new Vector2(width, height);
        }
        
        // Default screen size if no camera found
        return new Vector2(10f, 10f);
    }
    
    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<Ground>(this);
    }
    
    // Public methods for getting ground information
    public float GetGroundHeight()
    {
        return groundHeight;
    }
    
    public float GetGroundTop()
    {
        return transform.position.y + (groundHeight * 0.5f);
    }
    
    public float GetGroundBottom()
    {
        return transform.position.y - (groundHeight * 0.5f);
    }
    
    public Vector3 GetGroundPosition()
    {
        return transform.position;
    }
    
    // Public getters
    public float GroundHeight => groundHeight;
    public Color GroundColor => groundColor;
}
