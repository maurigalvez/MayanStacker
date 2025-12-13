using UnityEngine;

/// <summary>
/// Utility class for checking network connectivity status
/// Provides simple methods to determine if the device is online or offline
/// </summary>
public static class NetworkUtility
{
    /// <summary>
    /// Check if the device has network connectivity
    /// </summary>
    /// <returns>True if device is online (via WiFi or mobile data), false if offline</returns>
    public static bool IsOnline()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
    }

    /// <summary>
    /// Check if the device is offline (no network connectivity)
    /// </summary>
    /// <returns>True if device is offline, false if online</returns>
    public static bool IsOffline()
    {
        return Application.internetReachability == NetworkReachability.NotReachable;
    }

    /// <summary>
    /// Get a human-readable description of the current network status
    /// </summary>
    /// <returns>String describing the network reachability status</returns>
    public static string GetNetworkStatusDescription()
    {
        switch (Application.internetReachability)
        {
            case NetworkReachability.NotReachable:
                return "Offline";
            case NetworkReachability.ReachableViaCarrierDataNetwork:
                return "Online (Mobile Data)";
            case NetworkReachability.ReachableViaLocalAreaNetwork:
                return "Online (WiFi)";
            default:
                return "Unknown";
        }
    }

    /// <summary>
    /// Check if the connection is via WiFi (more reliable for large data transfers)
    /// </summary>
    /// <returns>True if connected via WiFi, false otherwise</returns>
    public static bool IsConnectedViaWiFi()
    {
        return Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork;
    }

    /// <summary>
    /// Check if the connection is via mobile data
    /// </summary>
    /// <returns>True if connected via mobile data, false otherwise</returns>
    public static bool IsConnectedViaMobileData()
    {
        return Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork;
    }
}

