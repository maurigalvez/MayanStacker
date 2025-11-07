using UnityEngine;

/// <summary>
/// Manages sound effects for the main menu UI, including button clicks and navigation sounds
/// </summary>
public class MainMenuSoundManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioSource musicAudioSource;

    [Header("Button Sound Effects")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip buttonHoverSound;
    [SerializeField] private AudioClip backButtonSound;

    [Header("Panel Transition Sounds")]
    [SerializeField] private AudioClip panelOpenSound;
    [SerializeField] private AudioClip panelCloseSound;

    [Header("Game Mode Selection Sounds")]
    [SerializeField] private AudioClip infiniteModeSelectSound;
    [SerializeField] private AudioClip levelModeSelectSound;
    [SerializeField] private AudioClip levelButtonClickSound;

    [Header("Settings")]
    [SerializeField] private float defaultVolume = 0.7f;
    [SerializeField] private bool allowSimultaneousSounds = true;

    // References
    private SettingsManager settingsManager;

    // State
    private bool isInitialized = false;

    private void Awake()
    {
        // Register with DependencyRegistry
        DependencyRegistry.Register<MainMenuSoundManager>(this);

        // Configure SFX AudioSource
        if (sfxAudioSource != null)
        {
            sfxAudioSource.playOnAwake = false;
            sfxAudioSource.volume = defaultVolume;
        }

        // Configure Music AudioSource
        if (musicAudioSource != null)
        {
            musicAudioSource.playOnAwake = false;
            musicAudioSource.loop = true;
            musicAudioSource.volume = defaultVolume;
        }
    }

    private void Start()
    {
        // Find dependencies
        settingsManager = DependencyRegistry.Find<SettingsManager>();

        // Subscribe to settings changes if SettingsManager exists
        if (settingsManager != null)
        {
            // Apply current volume settings
            UpdateVolume();
        }

        // Start playing music if available
        PlayMusic();

        isInitialized = true;
    }

    /// <summary>
    /// Updates the volume based on SettingsManager settings
    /// </summary>
    private void UpdateVolume()
    {
        if (settingsManager != null)
        {
            // You can adjust this to use specific volume settings from SettingsManager
            // For now, using the default volume with master volume multiplier if available
            if (sfxAudioSource != null)
            {
                sfxAudioSource.volume = defaultVolume;
            }
            if (musicAudioSource != null)
            {
                musicAudioSource.volume = defaultVolume;
            }
        }
    }

    /// <summary>
    /// Plays a standard button click sound
    /// </summary>
    public void PlayButtonClick()
    {
        PlaySound(buttonClickSound);
    }

    /// <summary>
    /// Plays a button hover sound
    /// </summary>
    public void PlayButtonHover()
    {
        PlaySound(buttonHoverSound);
    }

    /// <summary>
    /// Plays a back button sound
    /// </summary>
    public void PlayBackButton()
    {
        PlaySound(backButtonSound);
    }

    /// <summary>
    /// Plays a panel opening sound
    /// </summary>
    public void PlayPanelOpen()
    {
        PlaySound(panelOpenSound);
    }

    /// <summary>
    /// Plays a panel closing sound
    /// </summary>
    public void PlayPanelClose()
    {
        PlaySound(panelCloseSound);
    }

    /// <summary>
    /// Plays the infinite mode selection sound
    /// </summary>
    public void PlayInfiniteModeSelect()
    {
        PlaySound(infiniteModeSelectSound);
    }

    /// <summary>
    /// Plays the level mode selection sound
    /// </summary>
    public void PlayLevelModeSelect()
    {
        PlaySound(levelModeSelectSound);
    }

    /// <summary>
    /// Plays a level button click sound
    /// </summary>
    public void PlayLevelButtonClick()
    {
        PlaySound(levelButtonClickSound);
    }

    /// <summary>
    /// Plays a specific sound effect
    /// </summary>
    /// <param name="clip">The audio clip to play</param>
    public void PlaySound(AudioClip clip)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("MainMenuSoundManager is not initialized yet");
            return;
        }

        if (clip == null)
        {
            // Silently return if clip is not assigned (allows optional sounds)
            return;
        }

        if (sfxAudioSource == null)
        {
            Debug.LogWarning("SFX AudioSource component is missing!");
            return;
        }

        if (allowSimultaneousSounds)
        {
            // Play one-shot allows multiple sounds to overlap
            sfxAudioSource.PlayOneShot(clip);
        }
        else
        {
            // Stop current sound and play new one
            sfxAudioSource.Stop();
            sfxAudioSource.clip = clip;
            sfxAudioSource.Play();
        }
    }

    /// <summary>
    /// Plays a sound with custom volume
    /// </summary>
    /// <param name="clip">The audio clip to play</param>
    /// <param name="volumeScale">Volume scale (0 to 1)</param>
    public void PlaySound(AudioClip clip, float volumeScale)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("MainMenuSoundManager is not initialized yet");
            return;
        }

        if (clip == null || sfxAudioSource == null)
        {
            return;
        }

        sfxAudioSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>
    /// Stops all currently playing sounds
    /// </summary>
    public void StopAllSounds()
    {
        if (sfxAudioSource != null)
        {
            sfxAudioSource.Stop();
        }
    }

    /// <summary>
    /// Plays the background music
    /// </summary>
    public void PlayMusic()
    {
        if (musicAudioSource != null && !musicAudioSource.isPlaying)
        {
            musicAudioSource.Play();
        }
    }

    /// <summary>
    /// Pauses the background music
    /// </summary>
    public void PauseMusic()
    {
        if (musicAudioSource != null && musicAudioSource.isPlaying)
        {
            musicAudioSource.Pause();
        }
    }

    /// <summary>
    /// Stops the background music
    /// </summary>
    public void StopMusic()
    {
        if (musicAudioSource != null)
        {
            musicAudioSource.Stop();
        }
    }

    /// <summary>
    /// Sets the volume for SFX sounds
    /// </summary>
    /// <param name="volume">Volume value (0 to 1)</param>
    public void SetSFXVolume(float volume)
    {
        if (sfxAudioSource != null)
        {
            sfxAudioSource.volume = Mathf.Clamp01(volume);
        }
    }

    /// <summary>
    /// Sets the volume for background music
    /// </summary>
    /// <param name="volume">Volume value (0 to 1)</param>
    public void SetMusicVolume(float volume)
    {
        if (musicAudioSource != null)
        {
            musicAudioSource.volume = Mathf.Clamp01(volume);
        }
    }

    /// <summary>
    /// Gets the current SFX volume
    /// </summary>
    /// <returns>Current SFX volume value (0 to 1)</returns>
    public float GetSFXVolume()
    {
        return sfxAudioSource != null ? sfxAudioSource.volume : 0f;
    }

    /// <summary>
    /// Gets the current music volume
    /// </summary>
    /// <returns>Current music volume value (0 to 1)</returns>
    public float GetMusicVolume()
    {
        return musicAudioSource != null ? musicAudioSource.volume : 0f;
    }

    private void OnDestroy()
    {
        // Unregister from DependencyRegistry
        DependencyRegistry.Unregister<MainMenuSoundManager>(this);
    }
}


