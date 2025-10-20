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

        // Block input if game is paused
        if (uiManager != null && uiManager.IsPaused)
            return;

        // Block input if we just resumed from pause
        if (isInputBlocked)
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

        // Block input if game is paused
        if (uiManager != null && uiManager.IsPaused)
            return;

        // Block input if we just resumed from pause
        if (isInputBlocked)
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
        // Check if pointer is over a button - if so, ignore gameplay input
        if (IsPointerOverUI())
        {
            return;
        }

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
    /// Check if the pointer/touch is over a Button UI element specifically
    /// Returns true only if touching a Button, false for other UI elements or gameplay area
    /// </summary>
    private bool IsPointerOverUI()
    {
        // Check if EventSystem exists
        if (EventSystem.current == null)
            return false;

        Vector2 pointerPosition = Vector2.zero;
        bool hasValidInput = false;

        // Get pointer position based on input type
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            hasValidInput = true;
        }
        else if (Mouse.current != null)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            hasValidInput = true;
        }

        if (!hasValidInput)
            return false;

        // Create PointerEventData for raycasting
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = pointerPosition
        };

        // Raycast to find UI elements under the pointer
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        // Check if any of the hit UI elements is a Button
        foreach (RaycastResult result in raycastResults)
        {
            // Check if this specific GameObject has a Button component
            if (result.gameObject.GetComponent<Button>() != null)
            {
                Debug.Log($"Input blocked: Pointer is over button '{result.gameObject.name}'");
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
