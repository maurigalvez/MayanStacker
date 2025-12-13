using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnvironmentSpawner : MonoBehaviour
{
    [Header("Environment Configuration")]
    [SerializeField] private EnvironmentAssetData[] environmentAssets;
    [SerializeField] private bool enableSpawning = true;

    [Header("Spawning Settings")]
    [SerializeField] private float globalSpawnInterval = 3f;
    [SerializeField] private float spawnIntervalVariation = 1f;// Distance from screen edges

    [Header("Screen Boundaries")]
    [SerializeField] private float spawnOffsetFromEdge = 5f; // How far from screen edge to spawn
    [SerializeField] private float despawnOffsetFromEdge = 10f; // How far beyond screen edge before despawning

    [Header("Height Integration")]
    [SerializeField] private bool useStackHeight = true;
    [SerializeField] private float heightCheckInterval = 0.5f;

    // Private variables
    private StackManager stackManager;
    private CameraController cameraController;
    private StyleManager styleManager;
    private List<EnvironmentAsset> activeAssets = new List<EnvironmentAsset>();
    private float leftBound;
    private float rightBound;
    private int currentStackHeight = 0;
    private Coroutine spawningCoroutine;
    private Coroutine heightCheckCoroutine;

    // Events
    public System.Action<EnvironmentAsset> OnAssetSpawned;
    public System.Action<EnvironmentAsset> onAssetDespawned;
    public System.Action<int> OnHeightChanged;

    private void Start()
    {
#if UNITY_EDITOR
        Debug.Log("[EnvironmentSpawner] Start: Initializing environment spawner");
#endif
        // Register with dependency registry
        DependencyRegistry.Register<EnvironmentSpawner>(this);

        // Get stack manager
        stackManager = DependencyRegistry.Find<StackManager>();

        // Get camera controller
        cameraController = DependencyRegistry.Find<CameraController>();

        // Get style manager
        styleManager = DependencyRegistry.Find<StyleManager>();

        // Calculate screen bounds
        CalculateScreenBounds();

        // Subscribe to game events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameRestart += OnGameRestart;
        }

        // Start spawning if enabled
        if (enableSpawning)
        {
            StartSpawning();
        }

        // Start height checking if enabled
        if (useStackHeight)
        {
            StartHeightChecking();
        }
    }

    private void Update()
    {
        // Update despawn bounds for all active assets
        UpdateAssetDespawnBounds();
    }

    private void UpdateAssetDespawnBounds()
    {
        if (cameraController == null) return;

        // Get camera component
        Camera cam = cameraController.GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Get current camera position
        Vector3 cameraPosition = cameraController.transform.position;

        // Calculate despawn bounds based on current camera position
        float screenHeight = cam.orthographicSize * 2f;
        float screenWidth = screenHeight * cam.aspect;
        float leftDespawnBound = cameraPosition.x - (screenWidth / 2f) - despawnOffsetFromEdge;
        float rightDespawnBound = cameraPosition.x + (screenWidth / 2f) + despawnOffsetFromEdge;

        // Update despawn bounds for each active asset (only for movement-based assets)
        for (int i = activeAssets.Count - 1; i >= 0; i--)
        {
            if (activeAssets[i] == null)
            {
                activeAssets.RemoveAt(i);
                continue;
            }

            // Only update bounds for movement-based despawn assets
            if (activeAssets[i].AssetData != null && activeAssets[i].AssetData.despawnType == DespawnType.Movement)
            {
                activeAssets[i].UpdateDespawnBounds(leftDespawnBound, rightDespawnBound);
            }
        }
    }

    private void CalculateScreenBounds()
    {
        if (cameraController == null)
        {
            cameraController = DependencyRegistry.Find<CameraController>();
        }

        if (cameraController != null)
        {
            // Get camera component from controller
            Camera cam = cameraController.GetComponent<Camera>();
            if (cam == null)
            {
                cam = Camera.main;
            }

            if (cam != null)
            {
                // Get camera position from CameraController
                Vector3 cameraPosition = cameraController.transform.position;

                // Calculate screen width in world units
                float screenHeight = cam.orthographicSize * 2f;
                float screenWidth = screenHeight * cam.aspect;

                // Calculate spawn positions (offset from screen edges)
                leftBound = cameraPosition.x - (screenWidth / 2f) - spawnOffsetFromEdge;
                rightBound = cameraPosition.x + (screenWidth / 2f) + spawnOffsetFromEdge;

#if UNITY_EDITOR
                Debug.Log($"[EnvironmentSpawner] CalculateScreenBounds: CameraX={cameraPosition.x:F2}, ScreenWidth={screenWidth:F2}, Left={leftBound:F2}, Right={rightBound:F2}");
#endif
                return;
            }
        }

#if UNITY_EDITOR
        Debug.LogWarning("[EnvironmentSpawner] CalculateScreenBounds: CameraController not found, using default bounds");
#endif
        // Fallback to default values
        leftBound = -15f;
        rightBound = 15f;
    }

    private void StartSpawning()
    {
#if UNITY_EDITOR
        Debug.Log("[EnvironmentSpawner] StartSpawning: Starting spawn coroutine");
#endif
        if (spawningCoroutine != null)
        {
            StopCoroutine(spawningCoroutine);
        }
        spawningCoroutine = StartCoroutine(SpawnCoroutine());
    }

    private void StartHeightChecking()
    {
#if UNITY_EDITOR
        Debug.Log("[EnvironmentSpawner] StartHeightChecking: Starting height check coroutine");
#endif
        if (heightCheckCoroutine != null)
        {
            StopCoroutine(heightCheckCoroutine);
        }
        heightCheckCoroutine = StartCoroutine(HeightCheckCoroutine());
    }

    private IEnumerator SpawnCoroutine()
    {
#if UNITY_EDITOR
        Debug.Log("[EnvironmentSpawner] SpawnCoroutine: Entered spawn loop");
#endif
        while (enableSpawning)
        {
            yield return new WaitForSeconds(GetRandomSpawnInterval());

            if (CanSpawnAsset())
            {
                SpawnRandomAsset();
            }
        }
    }

    private IEnumerator HeightCheckCoroutine()
    {
#if UNITY_EDITOR
        Debug.Log("[EnvironmentSpawner] HeightCheckCoroutine: Entered height check loop");
#endif
        while (useStackHeight)
        {
            yield return new WaitForSeconds(heightCheckInterval);

            int newHeight = GetCurrentStackHeight();
            if (newHeight != currentStackHeight)
            {
#if UNITY_EDITOR
                Debug.Log($"[EnvironmentSpawner] HeightCheckCoroutine: Height changed from {currentStackHeight} to {newHeight}");
#endif
                currentStackHeight = newHeight;
                OnHeightChanged?.Invoke(currentStackHeight);
            }
        }
    }

    private bool CanSpawnAsset()
    {
        // Check if we have any environment assets configured
        if (environmentAssets == null || environmentAssets.Length == 0)
            return false;

        // Check if any assets are available for current height
        return GetAvailableAssetsForCurrentHeight().Count > 0;
    }

    private List<EnvironmentAssetData> GetAvailableAssetsForCurrentHeight()
    {
        List<EnvironmentAssetData> availableAssets = new List<EnvironmentAssetData>();

        foreach (var assetData in environmentAssets)
        {
            if (assetData != null && assetData.IsActiveForHeight(currentStackHeight))
            {
                availableAssets.Add(assetData);
            }
        }

        return availableAssets;
    }

    private void SpawnRandomAsset()
    {
#if UNITY_EDITOR
        Debug.Log("[EnvironmentSpawner] SpawnRandomAsset: Attempting to spawn random asset");
#endif
        // Recalculate bounds based on current camera position
        CalculateScreenBounds();

        var availableAssets = GetAvailableAssetsForCurrentHeight();
        if (availableAssets.Count == 0) return;

        // Weighted random selection
        EnvironmentAssetData selectedAsset = SelectWeightedRandomAsset(availableAssets);
        if (selectedAsset == null || selectedAsset.assetPrefab == null) return;

        // Determine spawn side and position
        bool spawnFromLeft = Random.Range(0f, 1f) < 0.5f;
        if (!selectedAsset.canSpawnFromBothSides)
        {
            spawnFromLeft = true; // Default to left if only one side allowed
        }

        Vector3 spawnPosition = CalculateSpawnPosition(selectedAsset, spawnFromLeft);

        // Spawn the asset
        GameObject spawnedObject = Instantiate(selectedAsset.assetPrefab, spawnPosition, Quaternion.identity);
        spawnedObject.transform.localScale = selectedAsset.GetRandomScale();

        // Set up the environment asset component
        EnvironmentAsset environmentAsset = spawnedObject.GetComponent<EnvironmentAsset>();
        if (environmentAsset == null)
        {
            environmentAsset = spawnedObject.AddComponent<EnvironmentAsset>();
        }

        environmentAsset.Initialize(selectedAsset, spawnFromLeft, leftBound, rightBound);

        // Register sprite renderers with StyleManager (EnvironmentAsset will also register in Start, but this ensures immediate registration)
        if (styleManager != null)
        {
            styleManager.RegisterEnvironmentSpriteRenderers(spawnedObject);
        }

        // Track the asset
        activeAssets.Add(environmentAsset);

        // Clean up any null references
        activeAssets.RemoveAll(asset => asset == null);

        // Subscribe to despawn event
        environmentAsset.OnDespawn += OnAssetDespawned;

#if UNITY_EDITOR
        Debug.Log($"[EnvironmentSpawner] SpawnRandomAsset: Spawned {selectedAsset.assetPrefab.name} at {spawnPosition} from {(spawnFromLeft ? "left" : "right")}");
#endif

        // Notify listeners
        OnAssetSpawned?.Invoke(environmentAsset);
    }

    private EnvironmentAssetData SelectWeightedRandomAsset(List<EnvironmentAssetData> assets)
    {
        if (assets.Count == 0) return null;
        if (assets.Count == 1) return assets[0];

        float totalWeight = 0f;
        foreach (var asset in assets)
        {
            totalWeight += asset.spawnWeight;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var asset in assets)
        {
            currentWeight += asset.spawnWeight;
            if (randomValue <= currentWeight)
            {
                return asset;
            }
        }

        return assets[assets.Count - 1]; // Fallback
    }

    private Vector3 CalculateSpawnPosition(EnvironmentAssetData assetData, bool spawnFromLeft)
    {
        float xPosition = spawnFromLeft ? leftBound : rightBound;

        // Get camera Y position for Y position base
        float cameraY = 0f;
        if (cameraController != null)
        {
            cameraY = cameraController.transform.position.y;
        }

        float yPosition = cameraY + assetData.yPositionOffset + Random.Range(-assetData.yPositionVariation, assetData.yPositionVariation);

        return new Vector3(xPosition, yPosition, 0f);
    }

    private float GetRandomSpawnInterval()
    {
        float interval = globalSpawnInterval + Random.Range(-spawnIntervalVariation, spawnIntervalVariation);
        return interval;
    }

    private int GetCurrentStackHeight()
    {
        if (stackManager != null)
        {
            int height = stackManager.GetStackCount();
            return height;
        }
        return 0;
    }

    private void OnAssetDespawned(EnvironmentAsset asset)
    {
#if UNITY_EDITOR
        Debug.Log($"[EnvironmentSpawner] OnAssetDespawned: Asset despawned, active count={activeAssets.Count - 1}");
#endif
        if (activeAssets.Contains(asset))
        {
            activeAssets.Remove(asset);
        }
        onAssetDespawned?.Invoke(asset);
    }

    private void OnGameRestart()
    {
#if UNITY_EDITOR
        Debug.Log($"[EnvironmentSpawner] OnGameRestart: Restarting spawner, clearing {activeAssets.Count} active assets");
#endif
        // Clear all active assets
        foreach (var asset in activeAssets)
        {
            if (asset != null)
            {
                asset.OnDespawn -= OnAssetDespawned;
                Destroy(asset.gameObject);
            }
        }
        activeAssets.Clear();

        // Reset height
        currentStackHeight = 0;

        // Restart spawning if it was running
        if (enableSpawning)
        {
            StartSpawning();
        }
    }

    // Public methods for external control
    public void SetSpawningEnabled(bool enabled)
    {
#if UNITY_EDITOR
        Debug.Log($"[EnvironmentSpawner] SetSpawningEnabled: {enabled}");
#endif
        enableSpawning = enabled;

        if (enabled)
        {
            StartSpawning();
        }
        else if (spawningCoroutine != null)
        {
            StopCoroutine(spawningCoroutine);
            spawningCoroutine = null;
        }
    }

    public void SetHeightCheckingEnabled(bool enabled)
    {
#if UNITY_EDITOR
        Debug.Log($"[EnvironmentSpawner] SetHeightCheckingEnabled: {enabled}");
#endif
        useStackHeight = enabled;

        if (enabled)
        {
            StartHeightChecking();
        }
        else if (heightCheckCoroutine != null)
        {
            StopCoroutine(heightCheckCoroutine);
            heightCheckCoroutine = null;
        }
    }

    public int GetCurrentHeight()
    {
        return currentStackHeight;
    }

    public int GetActiveAssetCount()
    {
        return activeAssets.Count;
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        Debug.Log("[EnvironmentSpawner] OnDestroy: Cleaning up environment spawner");
#endif
        // Unregister from dependency registry
        DependencyRegistry.Unregister<EnvironmentSpawner>(this);

        // Stop coroutines
        if (spawningCoroutine != null)
        {
            StopCoroutine(spawningCoroutine);
        }

        if (heightCheckCoroutine != null)
        {
            StopCoroutine(heightCheckCoroutine);
        }

        // Unsubscribe from events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameRestart -= OnGameRestart;
        }

        // Clean up active assets
        foreach (var asset in activeAssets)
        {
            if (asset != null)
            {
                asset.OnDespawn -= OnAssetDespawned;
            }
        }
    }
}
