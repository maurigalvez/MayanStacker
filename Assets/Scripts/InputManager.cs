using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private InputActionAsset inputActions;
    
    // Input Actions
    private InputAction dropAction;
    private InputAction tapAction;
    private InputAction tapPositionAction;
    
    // References
    private ObjectSpawner objectSpawner;
    private GameManager gameManager;
    
    // Events
    public System.Action<Vector2> OnScreenTapped;
    public System.Action OnDropInput;
    
    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<InputManager>(this);
        
        // Initialize input actions
        InitializeInputActions();
    }
    
    private void InitializeInputActions()
    {
        if (inputActions == null)
        {
            Debug.LogError("InputActionAsset is not assigned in InputManager!");
            return;
        }
        
        // Get actions from the input action asset
        dropAction = inputActions.FindAction("Drop");
        tapAction = inputActions.FindAction("Tap");
        tapPositionAction = inputActions.FindAction("TapPosition");
        
        // Subscribe to action events
        if (dropAction != null)
        {
            dropAction.performed += OnDropPerformed;
        }
        
        if (tapAction != null)
        {
            tapAction.performed += OnTapPerformed;
        }
    }
    
    private void Start()
    {
        // Get references
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        gameManager = DependencyRegistry.Find<GameManager>();
        
        // Subscribe to game events
        if (gameManager != null)
        {
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
        }
    }
    
    private void OnEnable()
    {
        // Enable input actions
        inputActions?.Enable();
    }
    
    private void OnDisable()
    {
        // Disable input actions
        inputActions?.Disable();
    }
    
    private void OnDropPerformed(InputAction.CallbackContext context)
    {
        if (gameManager == null || !gameManager.IsGameActive || gameManager.IsGameOver)
            return;
        
        // Get tap position if available
        Vector2 tapPosition = Vector2.zero;
        if (tapPositionAction != null)
        {
            tapPosition = tapPositionAction.ReadValue<Vector2>();
        }
        
        ProcessInput(tapPosition);
    }
    
    private void OnTapPerformed(InputAction.CallbackContext context)
    {
        if (gameManager == null || !gameManager.IsGameActive || gameManager.IsGameOver)
            return;
        
        // Get tap position
        Vector2 tapPosition = Vector2.zero;
        if (tapPositionAction != null)
        {
            tapPosition = tapPositionAction.ReadValue<Vector2>();
        }
        
        ProcessInput(tapPosition);
    }
    
    private void ProcessInput(Vector2 screenPosition)
    {
        // Notify listeners
        OnScreenTapped?.Invoke(screenPosition);
        OnDropInput?.Invoke();
        
        // Drop the current object
        if (objectSpawner != null)
        {
            objectSpawner.DropCurrentObject();
        }
    }
    
    private void OnGameOver()
    {
        // Disable input when game is over
        enabled = false;
    }
    
    private void OnGameRestart()
    {
        // Re-enable input when game restarts
        enabled = true;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from action events
        if (dropAction != null)
        {
            dropAction.performed -= OnDropPerformed;
        }
        
        if (tapAction != null)
        {
            tapAction.performed -= OnTapPerformed;
        }
        
        // Unregister from dependency registry
        DependencyRegistry.Unregister<InputManager>(this);
        
        // Unsubscribe from events
        if (gameManager != null)
        {
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnGameRestart -= OnGameRestart;
        }
    }
}
