using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages game settings including audio, graphics quality, and controls
/// Persists settings using PlayerPrefs
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("Audio Settings UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeText;
    [SerializeField] private TextMeshProUGUI musicVolumeText;
    [SerializeField] private TextMeshProUGUI sfxVolumeText;
    [SerializeField] private Toggle muteToggle;

    [Header("Graphics Settings UI")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle vsyncToggle;

    [Header("Control Settings UI")]
    [SerializeField] private Toggle invertYAxisToggle;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityText;

    [Header("General Settings UI")]
    [SerializeField] private Button resetDefaultsButton;
    [SerializeField] private Button applyButton;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;

    // Settings Keys
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string MUTE_KEY = "Mute";
    private const string QUALITY_KEY = "Quality";
    private const string FULLSCREEN_KEY = "Fullscreen";
    private const string RESOLUTION_KEY = "Resolution";
    private const string VSYNC_KEY = "VSync";
    private const string INVERT_Y_KEY = "InvertY";
    private const string SENSITIVITY_KEY = "Sensitivity";

    // Default Values
    private const float DEFAULT_MASTER_VOLUME = 1.0f;
    private const float DEFAULT_MUSIC_VOLUME = 0.7f;
    private const float DEFAULT_SFX_VOLUME = 0.8f;
    private const int DEFAULT_MUTE = 0;
    private const int DEFAULT_QUALITY = 2;
    private const int DEFAULT_FULLSCREEN = 1;
    private const int DEFAULT_VSYNC = 1;
    private const int DEFAULT_INVERT_Y = 0;
    private const float DEFAULT_SENSITIVITY = 1.0f;

    // Current Settings
    private float masterVolume;
    private float musicVolume;
    private float sfxVolume;
    private bool isMuted;
    private int qualityLevel;
    private bool isFullscreen;
    private int resolutionIndex;
    private bool vSyncEnabled;
    private bool invertYAxis;
    private float sensitivity;

    // Available resolutions
    private Resolution[] resolutions;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<SettingsManager>(this);

        DontDestroyOnLoad(gameObject);
        LoadSettings();
        ApplySettings();
    }

    private void Start()
    {
        InitializeSettingsUI();
        SetupUIListeners();
    }

    public void InitializeSettingsUI()
    {
        // Audio Settings
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = masterVolume;
            UpdateMasterVolumeText(masterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = musicVolume;
            UpdateMusicVolumeText(musicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = sfxVolume;
            UpdateSFXVolumeText(sfxVolume);
        }

        if (muteToggle != null)
        {
            muteToggle.isOn = isMuted;
        }

        // Graphics Settings
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            qualityDropdown.value = qualityLevel;
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = isFullscreen;
        }

        if (vsyncToggle != null)
        {
            vsyncToggle.isOn = vSyncEnabled;
        }

        // Initialize resolution dropdown
        InitializeResolutionDropdown();

        // Control Settings
        if (invertYAxisToggle != null)
        {
            invertYAxisToggle.isOn = invertYAxis;
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = sensitivity;
            UpdateSensitivityText(sensitivity);
        }
    }

    private void InitializeResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width} x {resolutions[i].height} @ {resolutions[i].refreshRate}Hz";
            options.Add(option);

            // Check if this is the current resolution
            if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionIndex = PlayerPrefs.GetInt(RESOLUTION_KEY, currentResolutionIndex);
        resolutionDropdown.value = resolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void SetupUIListeners()
    {
        // Audio
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        if (muteToggle != null)
            muteToggle.onValueChanged.AddListener(OnMuteToggled);

        // Graphics
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.AddListener(OnVSyncToggled);

        // Controls
        if (invertYAxisToggle != null)
            invertYAxisToggle.onValueChanged.AddListener(OnInvertYToggled);

        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

        // Buttons
        if (resetDefaultsButton != null)
            resetDefaultsButton.onClick.AddListener(ResetToDefaults);

        if (applyButton != null)
            applyButton.onClick.AddListener(ApplyAndSaveSettings);
    }

    // Audio Callbacks
    private void OnMasterVolumeChanged(float value)
    {
        masterVolume = value;
        UpdateMasterVolumeText(value);
        ApplyAudioSettings();
    }

    private void OnMusicVolumeChanged(float value)
    {
        musicVolume = value;
        UpdateMusicVolumeText(value);
        ApplyAudioSettings();
    }

    private void OnSFXVolumeChanged(float value)
    {
        sfxVolume = value;
        UpdateSFXVolumeText(value);
        ApplyAudioSettings();
    }

    private void OnMuteToggled(bool value)
    {
        isMuted = value;
        ApplyAudioSettings();
    }

    private void UpdateMasterVolumeText(float value)
    {
        if (masterVolumeText != null)
            masterVolumeText.text = $"{Mathf.RoundToInt(value * 100)}%";
    }

    private void UpdateMusicVolumeText(float value)
    {
        if (musicVolumeText != null)
            musicVolumeText.text = $"{Mathf.RoundToInt(value * 100)}%";
    }

    private void UpdateSFXVolumeText(float value)
    {
        if (sfxVolumeText != null)
            sfxVolumeText.text = $"{Mathf.RoundToInt(value * 100)}%";
    }

    // Graphics Callbacks
    private void OnQualityChanged(int index)
    {
        qualityLevel = index;
    }

    private void OnFullscreenToggled(bool value)
    {
        isFullscreen = value;
    }

    private void OnResolutionChanged(int index)
    {
        resolutionIndex = index;
    }

    private void OnVSyncToggled(bool value)
    {
        vSyncEnabled = value;
    }

    // Control Callbacks
    private void OnInvertYToggled(bool value)
    {
        invertYAxis = value;
    }

    private void OnSensitivityChanged(float value)
    {
        sensitivity = value;
        UpdateSensitivityText(value);
    }

    private void UpdateSensitivityText(float value)
    {
        if (sensitivityText != null)
            sensitivityText.text = $"{value:F1}x";
    }

    // Settings Management
    private void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, DEFAULT_MASTER_VOLUME);
        musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, DEFAULT_MUSIC_VOLUME);
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, DEFAULT_SFX_VOLUME);
        isMuted = PlayerPrefs.GetInt(MUTE_KEY, DEFAULT_MUTE) == 1;
        qualityLevel = PlayerPrefs.GetInt(QUALITY_KEY, DEFAULT_QUALITY);
        isFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, DEFAULT_FULLSCREEN) == 1;
        vSyncEnabled = PlayerPrefs.GetInt(VSYNC_KEY, DEFAULT_VSYNC) == 1;
        invertYAxis = PlayerPrefs.GetInt(INVERT_Y_KEY, DEFAULT_INVERT_Y) == 1;
        sensitivity = PlayerPrefs.GetFloat(SENSITIVITY_KEY, DEFAULT_SENSITIVITY);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, masterVolume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, musicVolume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, sfxVolume);
        PlayerPrefs.SetInt(MUTE_KEY, isMuted ? 1 : 0);
        PlayerPrefs.SetInt(QUALITY_KEY, qualityLevel);
        PlayerPrefs.SetInt(FULLSCREEN_KEY, isFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(RESOLUTION_KEY, resolutionIndex);
        PlayerPrefs.SetInt(VSYNC_KEY, vSyncEnabled ? 1 : 0);
        PlayerPrefs.SetInt(INVERT_Y_KEY, invertYAxis ? 1 : 0);
        PlayerPrefs.SetFloat(SENSITIVITY_KEY, sensitivity);
        PlayerPrefs.Save();

        Debug.Log("Settings saved successfully!");
    }

    private void ApplySettings()
    {
        ApplyAudioSettings();
        ApplyGraphicsSettings();
        // Control settings are read directly when needed
    }

    private void ApplyAudioSettings()
    {
        float effectiveMasterVolume = isMuted ? 0 : masterVolume;

        // Apply to AudioListener
        AudioListener.volume = effectiveMasterVolume;

        // Apply to specific audio sources if assigned
        if (musicAudioSource != null)
        {
            musicAudioSource.volume = musicVolume * effectiveMasterVolume;
        }

        if (sfxAudioSource != null)
        {
            sfxAudioSource.volume = sfxVolume * effectiveMasterVolume;
        }
    }

    private void ApplyGraphicsSettings()
    {
        QualitySettings.SetQualityLevel(qualityLevel);
        QualitySettings.vSyncCount = vSyncEnabled ? 1 : 0;

        if (resolutions != null && resolutionIndex >= 0 && resolutionIndex < resolutions.Length)
        {
            Resolution resolution = resolutions[resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, isFullscreen);
        }
    }

    public void ApplyAndSaveSettings()
    {
        ApplySettings();
        SaveSettings();
    }

    public void ResetToDefaults()
    {
        masterVolume = DEFAULT_MASTER_VOLUME;
        musicVolume = DEFAULT_MUSIC_VOLUME;
        sfxVolume = DEFAULT_SFX_VOLUME;
        isMuted = DEFAULT_MUTE == 1;
        qualityLevel = DEFAULT_QUALITY;
        isFullscreen = DEFAULT_FULLSCREEN == 1;
        vSyncEnabled = DEFAULT_VSYNC == 1;
        invertYAxis = DEFAULT_INVERT_Y == 1;
        sensitivity = DEFAULT_SENSITIVITY;

        InitializeSettingsUI();
        ApplySettings();
        SaveSettings();

        Debug.Log("Settings reset to defaults!");
    }

    // Public Accessors
    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float SFXVolume => sfxVolume;
    public bool IsMuted => isMuted;
    public int QualityLevel => qualityLevel;
    public bool IsFullscreen => isFullscreen;
    public bool VSyncEnabled => vSyncEnabled;
    public bool InvertYAxis => invertYAxis;
    public float Sensitivity => sensitivity;

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<SettingsManager>(this);

        // Clean up listeners
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveAllListeners();

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveAllListeners();

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();

        if (muteToggle != null)
            muteToggle.onValueChanged.RemoveAllListeners();

        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.RemoveAllListeners();

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveAllListeners();

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.RemoveAllListeners();

        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.RemoveAllListeners();

        if (invertYAxisToggle != null)
            invertYAxisToggle.onValueChanged.RemoveAllListeners();

        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.RemoveAllListeners();

        if (resetDefaultsButton != null)
            resetDefaultsButton.onClick.RemoveAllListeners();

        if (applyButton != null)
            applyButton.onClick.RemoveAllListeners();
    }
}

