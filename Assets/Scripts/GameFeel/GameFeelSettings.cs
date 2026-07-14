using UnityEngine;

/// <summary>
/// Central, PlayerPrefs-backed toggles for game-feel effects (screen shake, haptics).
/// Kept separate from SettingsManager so the effects have zero scene-wiring dependencies -
/// GameFeelManager / CameraController / HapticFeedback read these directly.
/// A settings UI can later drive these via the setters.
/// </summary>
public static class GameFeelSettings
{
    private const string SCREEN_SHAKE_KEY = "ScreenShakeEnabled";
    private const string HAPTICS_KEY = "HapticsEnabled";
    private const string LANDING_GUIDE_KEY = "LandingGuideEnabled";

    // Both default ON - juice is opt-out, not opt-in.
    public static bool ScreenShakeEnabled
    {
        get => PlayerPrefs.GetInt(SCREEN_SHAKE_KEY, 1) == 1;
        set { PlayerPrefs.SetInt(SCREEN_SHAKE_KEY, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool HapticsEnabled
    {
        get => PlayerPrefs.GetInt(HAPTICS_KEY, 1) == 1;
        set { PlayerPrefs.SetInt(HAPTICS_KEY, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Faint drop-guide under the swinging block. Default on (accessibility / readability).
    public static bool LandingGuideEnabled
    {
        get => PlayerPrefs.GetInt(LANDING_GUIDE_KEY, 1) == 1;
        set { PlayerPrefs.SetInt(LANDING_GUIDE_KEY, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
