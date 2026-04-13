using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages audio settings for the game
/// Persists settings using PlayerPrefs
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("Audio Settings UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Mute Buttons")]
    [SerializeField] private Button masterMuteButton;
    [SerializeField] private Button musicMuteButton;
    [SerializeField] private Button sfxMuteButton;

    [Header("Mute Button Sprites")]
    [SerializeField] private Sprite unmutedSprite;
    [SerializeField] private Sprite mutedSprite;

    [Header("Language Settings")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("General Settings UI")]
    [SerializeField] private Button resetDefaultsButton;
    [SerializeField] private Button applyButton;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI savedFeedbackText;

    // Sound Manager References
    private MainMenuSoundManager mainMenuSoundManager;
    private GameSoundManager gameSoundManager;

    // Settings Keys
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string MASTER_MUTE_KEY = "MasterMute";
    private const string MUSIC_MUTE_KEY = "MusicMute";
    private const string SFX_MUTE_KEY = "SFXMute";

    // Default Values
    private const float DEFAULT_MASTER_VOLUME = 1.0f;
    private const float DEFAULT_MUSIC_VOLUME = 0.7f;
    private const float DEFAULT_SFX_VOLUME = 0.8f;
    private const int DEFAULT_MUTE = 0;

    private Coroutine savedFeedbackCoroutine;
    private CanvasGroup savedFeedbackCanvasGroup;
    private static readonly WaitForSeconds savedFeedbackHoldWait = new WaitForSeconds(1.5f);

    // Current Settings
    private float masterVolume;
    private float musicVolume;
    private float sfxVolume;
    private bool isMasterMuted;
    private bool isMusicMuted;
    private bool isSFXMuted;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<SettingsManager>(this);
        LoadSettings();
        ApplySettings();
    }

    private void Start()
    {
        // Find sound managers via DependencyRegistry
        mainMenuSoundManager = DependencyRegistry.Find<MainMenuSoundManager>();
        gameSoundManager = DependencyRegistry.Find<GameSoundManager>();

        InitializeSettingsUI();
        SetupUIListeners();
    }

    public void InitializeSettingsUI()
    {
        // Audio Settings
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = masterVolume;
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = musicVolume;
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = sfxVolume;
        }

        // Update mute button sprites
        UpdateMuteButtonSprite(masterMuteButton, isMasterMuted);
        UpdateMuteButtonSprite(musicMuteButton, isMusicMuted);
        UpdateMuteButtonSprite(sfxMuteButton, isSFXMuted);

        // Language dropdown
        InitializeLanguageDropdown();
    }

    private void SetupUIListeners()
    {
        // Audio Sliders
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        // Mute Buttons
        if (masterMuteButton != null)
            masterMuteButton.onClick.AddListener(ToggleMasterMute);

        if (musicMuteButton != null)
            musicMuteButton.onClick.AddListener(ToggleMusicMute);

        if (sfxMuteButton != null)
            sfxMuteButton.onClick.AddListener(ToggleSFXMute);

        // Language Dropdown
        if (languageDropdown != null)
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

        // General Buttons
        if (resetDefaultsButton != null)
            resetDefaultsButton.onClick.AddListener(ResetToDefaults);

        if (applyButton != null)
            applyButton.onClick.AddListener(ApplyAndSaveSettings);
    }

    // Audio Callbacks
    private void OnMasterVolumeChanged(float value)
    {
        masterVolume = value;
        ApplyAudioSettings();
    }

    private void OnMusicVolumeChanged(float value)
    {
        musicVolume = value;
        ApplyAudioSettings();
    }

    private void OnSFXVolumeChanged(float value)
    {
        sfxVolume = value;
        ApplyAudioSettings();
    }

    // Mute Button Controls
    /// <summary>
    /// Toggles master mute state
    /// </summary>
    private void ToggleMasterMute()
    {
        isMasterMuted = !isMasterMuted;
        UpdateMuteButtonSprite(masterMuteButton, isMasterMuted);
        ApplyAudioSettings();
    }

    /// <summary>
    /// Toggles music mute state
    /// </summary>
    private void ToggleMusicMute()
    {
        isMusicMuted = !isMusicMuted;
        UpdateMuteButtonSprite(musicMuteButton, isMusicMuted);
        ApplyAudioSettings();
    }

    /// <summary>
    /// Toggles SFX mute state
    /// </summary>
    private void ToggleSFXMute()
    {
        isSFXMuted = !isSFXMuted;
        UpdateMuteButtonSprite(sfxMuteButton, isSFXMuted);
        ApplyAudioSettings();
    }

    /// <summary>
    /// Updates the sprite of a mute button based on mute state
    /// </summary>
    /// <param name="button">The button to update</param>
    /// <param name="isMuted">Whether the audio is muted</param>
    private void UpdateMuteButtonSprite(Button button, bool isMuted)
    {
        if (button != null)
        {
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.sprite = isMuted ? mutedSprite : unmutedSprite;
            }
        }
    }

    // Settings Management
    private void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, DEFAULT_MASTER_VOLUME);
        musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, DEFAULT_MUSIC_VOLUME);
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, DEFAULT_SFX_VOLUME);
        isMasterMuted = PlayerPrefs.GetInt(MASTER_MUTE_KEY, DEFAULT_MUTE) == 1;
        isMusicMuted = PlayerPrefs.GetInt(MUSIC_MUTE_KEY, DEFAULT_MUTE) == 1;
        isSFXMuted = PlayerPrefs.GetInt(SFX_MUTE_KEY, DEFAULT_MUTE) == 1;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, masterVolume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, musicVolume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, sfxVolume);
        PlayerPrefs.SetInt(MASTER_MUTE_KEY, isMasterMuted ? 1 : 0);
        PlayerPrefs.SetInt(MUSIC_MUTE_KEY, isMusicMuted ? 1 : 0);
        PlayerPrefs.SetInt(SFX_MUTE_KEY, isSFXMuted ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log("Settings saved successfully!");
    }

    private void ApplySettings()
    {
        ApplyAudioSettings();
    }

    private void ApplyAudioSettings()
    {
        // Calculate effective volumes with individual mute states
        float effectiveMasterVolume = isMasterMuted ? 0 : masterVolume;
        float effectiveMusicVolume = (isMusicMuted ? 0 : musicVolume) * effectiveMasterVolume;
        float effectiveSFXVolume = (isSFXMuted ? 0 : sfxVolume) * effectiveMasterVolume;

        // Apply to AudioListener (master volume controls everything)
        AudioListener.volume = effectiveMasterVolume;

        // Apply to MainMenuSoundManager (Music and SFX)
        if (mainMenuSoundManager != null)
        {
            mainMenuSoundManager.SetMusicVolume(effectiveMusicVolume);
            mainMenuSoundManager.SetSFXVolume(effectiveSFXVolume);
        }

        // Apply to GameSoundManager (Music and SFX)
        if (gameSoundManager != null)
        {
            gameSoundManager.SetMusicVolume(effectiveMusicVolume);
            gameSoundManager.SetSFXVolume(effectiveSFXVolume);
        }
    }

    public void ApplyAndSaveSettings()
    {
        ApplySettings();
        SaveSettings();

        // Sound feedback
        if (mainMenuSoundManager != null)
            mainMenuSoundManager.PlayButtonClick();

        // Visual feedback
        ShowSavedFeedback();
    }

    private void ShowSavedFeedback()
    {
        if (savedFeedbackText == null) return;

        if (savedFeedbackCoroutine != null)
            StopCoroutine(savedFeedbackCoroutine);

        savedFeedbackCoroutine = StartCoroutine(SavedFeedbackRoutine());
    }

    private IEnumerator SavedFeedbackRoutine()
    {
        savedFeedbackText.text = LocalizationManager.Get("settings_saved");

        if (savedFeedbackCanvasGroup == null)
        {
            savedFeedbackCanvasGroup = savedFeedbackText.GetComponent<CanvasGroup>();
            if (savedFeedbackCanvasGroup == null)
                savedFeedbackCanvasGroup = savedFeedbackText.gameObject.AddComponent<CanvasGroup>();
        }

        savedFeedbackCanvasGroup.alpha = 1f;
        savedFeedbackText.gameObject.SetActive(true);

        // Hold visible
        yield return savedFeedbackHoldWait;

        // Fade out
        float fadeDuration = 0.5f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            savedFeedbackCanvasGroup.alpha = 1f - (elapsed / fadeDuration);
            yield return null;
        }

        savedFeedbackCanvasGroup.alpha = 0f;
        savedFeedbackText.gameObject.SetActive(false);
        savedFeedbackCoroutine = null;
    }

    // Language Settings

    private static readonly string[] LocaleCodes = { "en", "es-419" };
    private static readonly string[] LanguageNames = { "English", "Espa\u00f1ol (Latinoam\u00e9rica)" };

    private void InitializeLanguageDropdown()
    {
        if (languageDropdown == null) return;

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new System.Collections.Generic.List<string>(LanguageNames));

        // Set current selection based on active locale
        var locManager = DependencyRegistry.Find<LocalizationManager>();
        if (locManager != null)
        {
            string currentLocale = locManager.CurrentLocale;
            int index = System.Array.IndexOf(LocaleCodes, currentLocale);
            if (index >= 0)
            {
                languageDropdown.SetValueWithoutNotify(index);
            }
        }
    }

    private void OnLanguageChanged(int index)
    {
        if (index < 0 || index >= LocaleCodes.Length) return;

        var locManager = DependencyRegistry.Find<LocalizationManager>();
        if (locManager != null)
        {
            locManager.SetLanguage(LocaleCodes[index]);
        }
    }

    public void ResetToDefaults()
    {
        masterVolume = DEFAULT_MASTER_VOLUME;
        musicVolume = DEFAULT_MUSIC_VOLUME;
        sfxVolume = DEFAULT_SFX_VOLUME;
        isMasterMuted = DEFAULT_MUTE == 1;
        isMusicMuted = DEFAULT_MUTE == 1;
        isSFXMuted = DEFAULT_MUTE == 1;

        // Reset language to English
        var locManager = DependencyRegistry.Find<LocalizationManager>();
        if (locManager != null)
        {
            locManager.SetLanguage("en");
        }

        InitializeSettingsUI();
        ApplySettings();
        SaveSettings();

        Debug.Log("Settings reset to defaults!");
    }

    // Public Accessors
    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float SFXVolume => sfxVolume;
    public bool IsMasterMuted => isMasterMuted;
    public bool IsMusicMuted => isMusicMuted;
    public bool IsSFXMuted => isSFXMuted;

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<SettingsManager>(this);

        // Clean up slider listeners
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveAllListeners();

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveAllListeners();

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();

        // Clean up mute button listeners
        if (masterMuteButton != null)
            masterMuteButton.onClick.RemoveAllListeners();

        if (musicMuteButton != null)
            musicMuteButton.onClick.RemoveAllListeners();

        if (sfxMuteButton != null)
            sfxMuteButton.onClick.RemoveAllListeners();

        // Clean up language dropdown
        if (languageDropdown != null)
            languageDropdown.onValueChanged.RemoveAllListeners();

        // Clean up general button listeners
        if (resetDefaultsButton != null)
            resetDefaultsButton.onClick.RemoveAllListeners();

        if (applyButton != null)
            applyButton.onClick.RemoveAllListeners();
    }
}

