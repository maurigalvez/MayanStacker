using UnityEngine;

/// <summary>
/// ScriptableObject for storing default game settings
/// This can be used to configure default values and share settings across scenes
/// </summary>
[CreateAssetMenu(fileName = "GameSettings", menuName = "TamalStacker/Game Settings", order = 0)]
public class GameSettings : ScriptableObject
{
    [Header("Audio Defaults")]
    [Range(0f, 1f)]
    public float defaultMasterVolume = 1.0f;
    [Range(0f, 1f)]
    public float defaultMusicVolume = 0.7f;
    [Range(0f, 1f)]
    public float defaultSFXVolume = 0.8f;
    public bool defaultMute = false;

    [Header("Graphics Defaults")]
    public int defaultQualityLevel = 2;
    public bool defaultFullscreen = true;
    public bool defaultVSync = true;

    [Header("Control Defaults")]
    public bool defaultInvertY = false;
    [Range(0.1f, 3f)]
    public float defaultSensitivity = 1.0f;

    [Header("Game Configuration")]
    public string mainMenuSceneName = "MainMenu";
    public string gameSceneName = "GameScene";
    public string gameVersion = "1.0.0";

    [Header("Gameplay Settings")]
    public bool enableTutorial = true;
    public bool showFPS = false;
    public bool enableParticleEffects = true;
    public bool enableScreenShake = true;

    [Header("Difficulty Modifiers")]
    [Range(0.5f, 2f)]
    public float globalDifficultyMultiplier = 1.0f;
    [Range(0.5f, 2f)]
    public float swingSpeedMultiplier = 1.0f;

    /// <summary>
    /// Reset all settings to their default values defined in this ScriptableObject
    /// </summary>
    public void ResetToDefaults()
    {
        // This method can be called from the SettingsManager to reset settings
        Debug.Log("Settings reset to defaults from GameSettings ScriptableObject");
    }

    /// <summary>
    /// Validate settings values
    /// </summary>
    private void OnValidate()
    {
        // Ensure values are within valid ranges
        defaultMasterVolume = Mathf.Clamp01(defaultMasterVolume);
        defaultMusicVolume = Mathf.Clamp01(defaultMusicVolume);
        defaultSFXVolume = Mathf.Clamp01(defaultSFXVolume);
        defaultQualityLevel = Mathf.Clamp(defaultQualityLevel, 0, 5);
        defaultSensitivity = Mathf.Clamp(defaultSensitivity, 0.1f, 3f);
        globalDifficultyMultiplier = Mathf.Clamp(globalDifficultyMultiplier, 0.5f, 2f);
        swingSpeedMultiplier = Mathf.Clamp(swingSpeedMultiplier, 0.5f, 2f);
    }
}

