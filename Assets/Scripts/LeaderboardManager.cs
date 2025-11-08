using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages leaderboard data retrieval and coordination
/// Combines top scores with player position for comprehensive leaderboard display
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    [Header("Leaderboard Names")]
    [SerializeField] private string infiniteStackerLeaderboard = "AllTime_InfiniteStackerHighScores";
    [SerializeField] private string stackerLevelsLeaderboardPrefix = "AllTime_StackerLevel_"; // Will be suffixed with level number

    [Header("Settings")]
    [SerializeField] private bool showPlayerIfNotInTop = true;
    [SerializeField] private int playerContextEntries = 3; // Number of entries around player to show

    // References
    private PlayFabManager playFabManager;
    private ILevelManager levelManager;

    // Properties for accessing leaderboard names
    public string InfiniteStackerLeaderboard => infiniteStackerLeaderboard;
    public string StackerLevelsLeaderboardPrefix => stackerLevelsLeaderboardPrefix;

    // State
    private Dictionary<string, List<LeaderboardEntry>> cachedLeaderboards = new Dictionary<string, List<LeaderboardEntry>>();

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<LeaderboardManager>(this);
    }

    private void Start()
    {
        // Find PlayFabManager
        playFabManager = DependencyRegistry.Find<PlayFabManager>();

        if (playFabManager == null)
        {
            Debug.LogError("LeaderboardManager: PlayFabManager not found!");
        }

        // Find LevelManager
        levelManager = DependencyRegistry.Find<ILevelManager>();

        if (levelManager == null)
        {
            Debug.LogWarning("LeaderboardManager: LevelManager not found! Demo mode restrictions won't work.");
        }
    }

    /// <summary>
    /// Get the leaderboard name for Infinite Stacker mode
    /// </summary>
    public string GetInfiniteStackerLeaderboardName()
    {
        return infiniteStackerLeaderboard;
    }

    /// <summary>
    /// Get the leaderboard name for a specific Stacker Level
    /// </summary>
    /// <param name="levelNumber">The level number (1-based)</param>
    public string GetStackerLevelLeaderboardName(int levelNumber)
    {
        return $"{stackerLevelsLeaderboardPrefix}{levelNumber}";
    }

    /// <summary>
    /// Get the maximum level that should be accessible for leaderboards
    /// Respects demo mode restrictions
    /// </summary>
    public int GetMaxAccessibleLevel()
    {
        if (levelManager == null)
        {
            // If no level manager, return 0 (no levels accessible)
            return 0;
        }

        // In demo mode, respect the demo max level
        if (levelManager.IsDemoVersion)
        {
            return levelManager.DemoMaxLevel;
        }

        // In full version, return total levels
        return levelManager.TotalLevels;
    }

    /// <summary>
    /// Check if a level's leaderboard should be accessible
    /// Respects demo mode restrictions
    /// </summary>
    public bool IsLevelAccessible(int levelNumber)
    {
        if (levelManager == null)
        {
            // If no level manager, no levels are accessible
            return false;
        }

        // Check demo version restrictions
        if (levelManager.IsDemoVersion && levelNumber > levelManager.DemoMaxLevel)
        {
            return false;
        }

        // Check if level number is within valid range
        return levelNumber > 0 && levelNumber <= levelManager.TotalLevels;
    }

    /// <summary>
    /// Get leaderboard with player position included if not in top entries
    /// </summary>
    /// <param name="leaderboardName">Name of the leaderboard statistic</param>
    /// <param name="topCount">Number of top entries to retrieve</param>
    /// <param name="onSuccess">Callback with combined leaderboard entries</param>
    /// <param name="onFailure">Callback with error message</param>
    public void GetLeaderboardWithPlayerPosition(
        string leaderboardName,
        int topCount,
        System.Action<List<LeaderboardEntry>> onSuccess,
        System.Action<string> onFailure)
    {
        if (playFabManager == null || !playFabManager.IsLoggedIn)
        {
            onFailure?.Invoke("PlayFab not available or not logged in");
            return;
        }

        // First, get the top entries
        playFabManager.GetLeaderboard(leaderboardName, topCount,
            topEntries => OnTopEntriesReceived(leaderboardName, topEntries, topCount, onSuccess, onFailure),
            onFailure
        );
    }

    /// <summary>
    /// Called when top entries are received
    /// </summary>
    private void OnTopEntriesReceived(
        string leaderboardName,
        List<LeaderboardEntry> topEntries,
        int topCount,
        System.Action<List<LeaderboardEntry>> onSuccess,
        System.Action<string> onFailure)
    {
        if (topEntries == null || topEntries.Count == 0)
        {
            Debug.Log($"No entries found for leaderboard: {leaderboardName}");
            onSuccess?.Invoke(new List<LeaderboardEntry>());
            return;
        }

        // Check if player is in the top entries
        bool playerInTop = topEntries.Any(e => e.isCurrentPlayer);

        if (playerInTop || !showPlayerIfNotInTop)
        {
            // Player is already in top entries or we don't need to show player separately
            onSuccess?.Invoke(topEntries);
            return;
        }

        // Player not in top, get their position
        playFabManager.GetPlayerLeaderboardPosition(leaderboardName, playerContextEntries,
            playerEntries => OnPlayerEntriesReceived(topEntries, playerEntries, onSuccess),
            error =>
            {
                // If we can't get player position, just return the top entries
                Debug.LogWarning($"Could not get player position: {error}");
                onSuccess?.Invoke(topEntries);
            }
        );
    }

    /// <summary>
    /// Called when player entries are received
    /// Combines top entries with player position
    /// </summary>
    private void OnPlayerEntriesReceived(
        List<LeaderboardEntry> topEntries,
        List<LeaderboardEntry> playerEntries,
        System.Action<List<LeaderboardEntry>> onSuccess)
    {
        if (playerEntries == null || playerEntries.Count == 0)
        {
            // Player has no score on this leaderboard
            onSuccess?.Invoke(topEntries);
            return;
        }

        // Find the player entry
        LeaderboardEntry playerEntry = playerEntries.FirstOrDefault(e => e.isCurrentPlayer);

        if (playerEntry == null)
        {
            // No player entry found, just return top entries
            onSuccess?.Invoke(topEntries);
            return;
        }

        // Check if player is already in top entries (edge case - might have moved up since first request)
        bool playerAlreadyInTop = topEntries.Any(e => e.position == playerEntry.position);

        if (playerAlreadyInTop)
        {
            onSuccess?.Invoke(topEntries);
            return;
        }

        // Combine entries: top entries + separator + player entries
        var combinedEntries = new List<LeaderboardEntry>(topEntries);

        // Add a separator entry (you can customize this in the UI)
        combinedEntries.Add(new LeaderboardEntry(
            -1, // Special position indicating separator
            "...",
            0,
            false
        ));

        // Add player entries (should include player and some context around them)
        combinedEntries.AddRange(playerEntries);

        onSuccess?.Invoke(combinedEntries);
    }

    /// <summary>
    /// Get cached leaderboard data if available
    /// </summary>
    public List<LeaderboardEntry> GetCachedLeaderboard(string leaderboardName)
    {
        if (cachedLeaderboards.ContainsKey(leaderboardName))
        {
            return new List<LeaderboardEntry>(cachedLeaderboards[leaderboardName]);
        }
        return null;
    }

    /// <summary>
    /// Cache leaderboard data for offline access
    /// </summary>
    public void CacheLeaderboard(string leaderboardName, List<LeaderboardEntry> entries)
    {
        if (entries != null)
        {
            cachedLeaderboards[leaderboardName] = new List<LeaderboardEntry>(entries);
        }
    }

    /// <summary>
    /// Clear cached leaderboard data
    /// </summary>
    public void ClearCache()
    {
        cachedLeaderboards.Clear();
    }

    /// <summary>
    /// Clear cache for a specific leaderboard
    /// </summary>
    public void ClearCache(string leaderboardName)
    {
        if (cachedLeaderboards.ContainsKey(leaderboardName))
        {
            cachedLeaderboards.Remove(leaderboardName);
        }
    }

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<LeaderboardManager>(this);
    }
}

