using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InputManager : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float resumeInputDelay = 0.3f; // Delay after resume before accepting input

    // Input Actions
    private InputAction dropAction;
    private InputAction tapAction;
    private InputAction tapPositionAction;

    // References
    private ObjectSpawner objectSpawner;
    private GameManager gameManager;
    private UIManager uiManager;

    // State
    private bool isInputBlocked = false;
    private bool isPointerOverUIElement = false; // Track UI hover state continuously

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
        uiManager = DependencyRegistry.Find<UIManager>();

        // Subscribe to game events
        if (gameManager != null)
        {
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
        }

        // Subscribe to UI events (pause/resume)
        if (uiManager != null)
        {
            uiManager.OnGameResumed += OnGameResumed;
        }
    }

    private void Update()
    {
        // Continuously check if pointer is over UI
        // This tracks the state BEFORE any input action fires
        isPointerOverUIElement = IsCurrentlyOverUI();
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
        // FIRST: Check if pointer was over UI (tracked continuously in Update)
        if (isPointerOverUIElement)
        {
#if UNITY_EDITOR
            Debug.Log("Input blocked: Pointer is over UI element (tracked state)");
#endif
            return;
        }

        if (gameManager == null || !gameManager.IsGameActive || gameManager.IsGameOver)
            return;

        // Block input if game is paused
        if (uiManager != null && uiManager.IsPaused)
            return;

        // Block input if we just resumed from pause
        if (isInputBlocked)
            return;

        // Get the current screen position - use TapPosition action or current pointer position
        Vector2 screenPosition = GetScreenPositionForInput();

        ProcessInput(screenPosition);
    }

    private void OnTapPerformed(InputAction.CallbackContext context)
    {
        // FIRST: Check if pointer was over UI (tracked continuously in Update)
        if (isPointerOverUIElement)
        {
#if UNITY_EDITOR
            Debug.Log("Input blocked: Pointer is over UI element (tracked state)");
#endif
            return;
        }

        if (gameManager == null || !gameManager.IsGameActive || gameManager.IsGameOver)
            return;

        // Block input if game is paused
        if (uiManager != null && uiManager.IsPaused)
            return;

        // Block input if we just resumed from pause
        if (isInputBlocked)
            return;

        // Get the current screen position - use TapPosition action or current pointer position
        Vector2 screenPosition = GetScreenPositionForInput();

        ProcessInput(screenPosition);
    }

    /// <summary>
    /// Continuously checks if pointer/touch is currently over a UI element
    /// Called every frame in Update to track UI hover state
    /// </summary>
    private bool IsCurrentlyOverUI()
    {
        if (EventSystem.current == null)
            return false;

        // For touch input (mobile) - check if touch is active and over UI
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            int touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            bool isOverUI = EventSystem.current.IsPointerOverGameObject(touchId);
#if UNITY_EDITOR
            if (isOverUI)
            {
                Debug.Log($"Touch {touchId} is over UI");
            }
#endif
            return isOverUI;
        }

        // For mouse input (desktop/editor)
        bool isMouseOverUI = EventSystem.current.IsPointerOverGameObject();
#if UNITY_EDITOR
        if (isMouseOverUI)
            Debug.Log("Mouse is over UI");
#endif
        return isMouseOverUI;
    }

    /// <summary>
    /// Gets the screen position for input processing
    /// First tries the TapPosition action, then falls back to direct device reading
    /// </summary>
    private Vector2 GetScreenPositionForInput()
    {
        // First, try to get position from TapPosition action
        if (tapPositionAction != null)
        {
            Vector2 actionPosition = tapPositionAction.ReadValue<Vector2>();
            if (IsValidPosition(actionPosition) && actionPosition != Vector2.zero)
            {
#if UNITY_EDITOR
                Debug.Log($"Got position from TapPosition action: {actionPosition}");
#endif
                return actionPosition;
            }
        }

        // Fallback: Try to read directly from input devices
        // Try touch first (for mobile)
        if (Touchscreen.current != null)
        {
            var primaryTouch = Touchscreen.current.primaryTouch;
            Vector2 touchPosition = primaryTouch.position.ReadValue();
            if (IsValidPosition(touchPosition) && touchPosition != Vector2.zero)
            {
#if UNITY_EDITOR
                Debug.Log($"Got touch position: {touchPosition}");
#endif
                return touchPosition;
            }
        }

        // Fallback to mouse (for desktop/editor)
        if (Mouse.current != null)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            if (IsValidPosition(mousePosition) && mousePosition != Vector2.zero)
            {
#if UNITY_EDITOR
                Debug.Log($"Got mouse position: {mousePosition}");
#endif
                return mousePosition;
            }
        }

#if UNITY_EDITOR
        Debug.LogWarning("Could not get valid screen position from any input device");
#endif
        return Vector2.zero;
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

    /// <summary>
    /// Validates that a position is not infinity, negative infinity, or NaN
    /// </summary>
    private bool IsValidPosition(Vector2 position)
    {
        return !float.IsInfinity(position.x) &&
               !float.IsInfinity(position.y) &&
               !float.IsNaN(position.x) &&
               !float.IsNaN(position.y);
    }

    /// <summary>
    /// Check if the pointer/touch is over a Button UI element specifically
    /// Returns true only if touching a Button, false for other UI elements or gameplay area
    /// </summary>
    /// <param name="screenPosition">The screen position to check</param>
    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        // Check if EventSystem exists
        if (EventSystem.current == null)
        {
            Debug.LogError("EventSystem is not assigned in InputManager!");
            return false;
        }

        // If position is invalid or zero, use EventSystem's built-in check as fallback
        if (!IsValidPosition(screenPosition) || screenPosition == Vector2.zero)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"Invalid or zero screen position: {screenPosition}, using IsPointerOverGameObject");
#endif
            // Use EventSystem's built-in method - it tracks the current pointer automatically
            bool isOverUI = EventSystem.current.IsPointerOverGameObject();
#if UNITY_EDITOR
            Debug.Log($"IsPointerOverGameObject result: {isOverUI}");
#endif
            return isOverUI;
        }

#if UNITY_EDITOR
        Debug.Log($"Checking UI at position: {screenPosition}");
#endif

        // Create PointerEventData for raycasting
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        // Raycast to find UI elements under the pointer
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

#if UNITY_EDITOR
        Debug.Log($"Raycast found {raycastResults.Count} UI elements");
#endif

        // Check if any of the hit UI elements is a Button
        foreach (RaycastResult result in raycastResults)
        {
            // Check if this specific GameObject has a Button component
            if (result.gameObject.GetComponent<Button>() != null)
            {
#if UNITY_EDITOR
                Debug.Log($"Input blocked: Pointer is over button '{result.gameObject.name}'");
#endif
                return true;
            }
        }

        // Not over a button - allow input
        return false;
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

    private void OnGameResumed()
    {
        // Block input briefly after resuming to prevent accidental drop
        StartCoroutine(BlockInputTemporarily());
    }

    private System.Collections.IEnumerator BlockInputTemporarily()
    {
        isInputBlocked = true;
        yield return new WaitForSeconds(resumeInputDelay);
        isInputBlocked = false;
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

        if (uiManager != null)
        {
            uiManager.OnGameResumed -= OnGameResumed;
        }
    }
}
