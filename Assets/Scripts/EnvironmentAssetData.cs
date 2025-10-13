using UnityEngine;

public enum DespawnType
{
    Movement,   // Despawns when moving beyond screen bounds
    Time        // Despawns after a set duration
}

[CreateAssetMenu(fileName = "EnvironmentAssetData", menuName = "TamalStacker/Environment Asset Data")]
public class EnvironmentAssetData : ScriptableObject
{
    [Header("Asset Configuration")]
    [Tooltip("The prefab to spawn for this environment asset")]
    public GameObject assetPrefab;

    [Tooltip("Height label/name for this environment tier")]
    public string heightLabel = "Ground Level";

    [Tooltip("Minimum stack height required to activate this environment")]
    public int minStackHeight = 0;

    [Tooltip("Maximum stack height for this environment (0 = no limit)")]
    public int maxStackHeight = 0;

    [Header("Spawning Settings")]
    [Tooltip("Probability weight for this asset to be selected (higher = more likely)")]
    [Range(0.1f, 10f)]
    public float spawnWeight = 1f;

    [Tooltip("Minimum time between spawns for this asset type")]
    public float minSpawnInterval = 2f;

    [Tooltip("Maximum time between spawns for this asset type")]
    public float maxSpawnInterval = 5f;

    [Header("Despawn Settings")]
    [Tooltip("How this asset should despawn")]
    public DespawnType despawnType = DespawnType.Movement;

    [Tooltip("Time duration before despawning (only used when despawnType is Time)")]
    public Vector2 lifetimeRange = new Vector2(3f, 8f);

    [Header("Movement Settings")]
    [Tooltip("Speed range for horizontal movement")]
    public Vector2 speedRange = new Vector2(1f, 3f);

    [Tooltip("DEPRECATED: Despawn distance is now managed by EnvironmentSpawner")]
    public float despawnDistance = 15f;

    [Header("Position Settings")]
    [Tooltip("Y position offset from ground level")]
    public float yPositionOffset = 0f;

    [Tooltip("Random Y position variation")]
    public float yPositionVariation = 1f;

    [Tooltip("Whether this asset can spawn from both left and right sides")]
    public bool canSpawnFromBothSides = true;

    [Header("Scale Settings")]
    [Tooltip("Minimum scale for this asset")]
    public Vector3 minScale = Vector3.one;

    [Tooltip("Maximum scale for this asset")]
    public Vector3 maxScale = Vector3.one;

    [Header("Sprite Renderer Settings")]
    [Tooltip("Minimum order in layer for sprite renderer")]
    public int minOrderInLayer = 0;

    [Tooltip("Maximum order in layer for sprite renderer")]
    public int maxOrderInLayer = 0;

    /// <summary>
    /// Check if this environment asset should be active for the given stack height
    /// </summary>
    public bool IsActiveForHeight(int stackHeight)
    {
        if (stackHeight < minStackHeight) return false;
        if (maxStackHeight > 0 && stackHeight > maxStackHeight) return false;
        return true;
    }

    /// <summary>
    /// Get a random spawn interval for this asset
    /// </summary>
    public float GetRandomSpawnInterval()
    {
        return Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    /// <summary>
    /// Get a random speed for this asset
    /// </summary>
    public float GetRandomSpeed()
    {
        return Random.Range(speedRange.x, speedRange.y);
    }

    /// <summary>
    /// Get a random scale for this asset
    /// </summary>
    public Vector3 GetRandomScale()
    {
        Vector3 scale;
        scale.x = Random.Range(minScale.x, maxScale.x);
        scale.y = Random.Range(minScale.y, maxScale.y);
        scale.z = Random.Range(minScale.z, maxScale.z);
        return scale;
    }

    /// <summary>
    /// Get a random order in layer value for this asset
    /// </summary>
    public int GetRandomOrderInLayer()
    {
        return Random.Range(minOrderInLayer, maxOrderInLayer + 1);
    }

    /// <summary>
    /// Get a random lifetime duration for this asset (used for time-based despawning)
    /// </summary>
    public float GetRandomLifetime()
    {
        return Random.Range(lifetimeRange.x, lifetimeRange.y);
    }
}
