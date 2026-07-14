using UnityEngine;

/// <summary>
/// Lightweight haptics using the built-in Android vibrator (no third-party packages).
/// Three graded intensities are approximated with short vibration durations.
/// No-ops on platforms without a vibrator and when haptics are disabled in settings.
/// </summary>
public static class HapticFeedback
{
    public enum HapticType
    {
        Light,   // subtle confirmation (e.g. a Good landing)
        Medium,  // satisfying hit (e.g. a Perfect landing)
        Heavy    // big moment (e.g. Kukulkan shift, game over)
    }

    // Durations in milliseconds. Kept short so they read as "taps", not buzzes.
    private const long LightMs = 18;
    private const long MediumMs = 35;
    private const long HeavyMs = 70;

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject _vibrator;
    private static bool _vibratorResolved;

    private static AndroidJavaObject Vibrator
    {
        get
        {
            if (_vibratorResolved) return _vibrator;
            _vibratorResolved = true;
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"HapticFeedback: could not resolve vibrator service: {e.Message}");
                _vibrator = null;
            }
            return _vibrator;
        }
    }
#endif

    public static void Trigger(HapticType type)
    {
        if (!GameFeelSettings.HapticsEnabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        long ms = type switch
        {
            HapticType.Light => LightMs,
            HapticType.Medium => MediumMs,
            HapticType.Heavy => HeavyMs,
            _ => MediumMs
        };

        var vibrator = Vibrator;
        if (vibrator == null) return;
        try
        {
            // Use the simple duration-based vibrate for broad device compatibility.
            // (Amplitude-controlled VibrationEffect requires API 26+; this works everywhere.)
            vibrator.Call("vibrate", ms);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"HapticFeedback: vibrate failed: {e.Message}");
        }
#else
        // Editor / non-Android: no-op (avoids the coarse ~500ms Handheld.Vibrate).
        _ = type;
#endif
    }
}
