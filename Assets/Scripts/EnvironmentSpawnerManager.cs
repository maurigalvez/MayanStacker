using UnityEngine;

/// <summary>
/// Manager class to help set up and configure the environment spawning system
/// This provides a convenient way to create and manage environment asset configurations
/// </summary>
public class EnvironmentSpawnerManager : MonoBehaviour
{
    [Header("Environment Spawner")]
    [SerializeField] private EnvironmentSpawner environmentSpawner;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool logSpawnEvents = false;

    private void Start()
    {
        // Get EnvironmentSpawner from DependencyRegistry if not assigned
        if (environmentSpawner == null)
        {
            environmentSpawner = DependencyRegistry.Find<EnvironmentSpawner>();
        }

        // Set up event listeners for debugging
        if (environmentSpawner != null && logSpawnEvents)
        {
            environmentSpawner.OnAssetSpawned += OnAssetSpawned;
            environmentSpawner.onAssetDespawned += OnAssetDespawned;
            environmentSpawner.OnHeightChanged += OnHeightChanged;
        }
    }

    private void OnAssetSpawned(EnvironmentAsset asset)
    {
        if (logSpawnEvents)
        {
            Debug.Log($"Environment Asset Spawned: {asset.AssetData.heightLabel} at height {environmentSpawner.GetCurrentHeight()}");
        }
    }

    private void OnAssetDespawned(EnvironmentAsset asset)
    {
        if (logSpawnEvents)
        {
            Debug.Log($"Environment Asset Despawned: {asset.AssetData.heightLabel}");
        }
    }

    private void OnHeightChanged(int newHeight)
    {
        if (logSpawnEvents)
        {
            Debug.Log($"Stack Height Changed: {newHeight}");
        }
    }

    private void OnGUI()
    {
        if (!showDebugInfo || environmentSpawner == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Environment Spawner Debug", GUI.skin.box);
        GUILayout.Label($"Current Height: {environmentSpawner.GetCurrentHeight()}");
        GUILayout.Label($"Active Assets: {environmentSpawner.GetActiveAssetCount()}");

        if (GUILayout.Button("Toggle Spawning"))
        {
            environmentSpawner.SetSpawningEnabled(!environmentSpawner.enabled);
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
