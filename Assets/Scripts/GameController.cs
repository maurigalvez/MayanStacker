using UnityEngine;

public class GameController : MonoBehaviour
{
    [Header("Game References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ObjectSpawner objectSpawner;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private CameraController cameraController;

    [Header("Game Settings")]
    [SerializeField] private bool autoFindComponents = true;
    [SerializeField] private bool enableCameraFollow = true;

    private void Start()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        // Find components if auto-find is enabled
        if (autoFindComponents)
        {
            FindComponents();
        }

        // Set up camera follow
        if (cameraController != null)
        {
            cameraController.SetFollowStack(enableCameraFollow);
        }

        // Subscribe to game events
        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnGameRestart += OnGameRestart;
        }

        Debug.Log("Game Controller initialized successfully!");
    }

    private void FindComponents()
    {
        // Find GameManager
        if (gameManager == null)
        {
            gameManager = DependencyRegistry.Find<GameManager>();
        }

        // Find ObjectSpawner
        if (objectSpawner == null)
        {
            objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        }

        // Find InputManager
        if (inputManager == null)
        {
            inputManager = DependencyRegistry.Find<InputManager>();
        }

        // Find UIManager
        if (uiManager == null)
        {
            uiManager = DependencyRegistry.Find<UIManager>();
        }

        // Find CameraController
        if (cameraController == null)
        {
            cameraController = DependencyRegistry.Find<CameraController>();
        }
    }

    private void OnGameStart()
    {
        Debug.Log("Game Started!");

        // Reset camera position
        if (cameraController != null)
        {
            cameraController.ResetCamera();
        }
    }

    private void OnGameOver()
    {
        Debug.Log("Game Over!");

        // Disable camera follow when game is over
        if (cameraController != null)
        {
            cameraController.SetFollowStack(false);
        }
    }

    private void OnGameRestart()
    {
        Debug.Log("Game Restarting...");

        // Reset camera position immediately before re-enabling follow to prevent showing unwanted areas
        if (cameraController != null)
        {
            cameraController.ResetCamera();
            cameraController.SetFollowStack(enableCameraFollow);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (gameManager != null)
        {
            gameManager.OnGameStart -= OnGameStart;
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnGameRestart -= OnGameRestart;
        }
    }

    // Public methods for external access
    public void SetCameraFollow(bool follow)
    {
        enableCameraFollow = follow;
        if (cameraController != null)
        {
            cameraController.SetFollowStack(follow);
        }
    }

    public GameManager GetGameManager() => gameManager;
    public ObjectSpawner GetObjectSpawner() => objectSpawner;
    public InputManager GetInputManager() => inputManager;
    public UIManager GetUIManager() => uiManager;
    public CameraController GetCameraController() => cameraController;
}
