using UnityEngine;

/// <summary>
/// Manages game music and sound effects during gameplay, including UI button sounds
/// </summary>
public class GameSoundManager : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioClip gameMusic;
    [SerializeField] private bool loopMusic = true;
    [SerializeField] private float musicVolume = 0.5f;

    [Header("UI Button Sound Effects")]
    [SerializeField] private AudioClip pauseSound;
    [SerializeField] private AudioClip unpauseSound;
    [SerializeField] private AudioClip homeButtonSound;
    [SerializeField] private AudioClip retryButtonSound;

    [Header("Settings")]
    [SerializeField] private float sfxVolume = 0.7f;
    [SerializeField] private bool allowSimultaneousSounds = true;

    // Audio sources
    private AudioSource musicSource;
    private AudioSource sfxSource;

    // References
    private SettingsManager settingsManager;
    private GameManager gameManager;

    // State
    private bool isInitialized = false;
    private bool isMusicPlaying = false;

    private void Awake()
    {
        // Register with DependencyRegistry
        DependencyRegistry.Register<GameSoundManager>(this);

        // Create and configure music AudioSource
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = loopMusic;
        musicSource.volume = musicVolume;

        // Create and configure SFX AudioSource
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
    }

    private void Start()
    {
        // Find dependencies
        settingsManager = DependencyRegistry.Find<SettingsManager>();
        gameManager = DependencyRegistry.Find<GameManager>();

        // Subscribe to game events if GameManager exists
        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
        }

        // Apply current volume settings
        if (settingsManager != null)
        {
            UpdateVolume();
        }

        isInitialized = true;

        // Start playing music
        PlayMusic();
    }

    /// <summary>
    /// Updates the volume based on SettingsManager settings
    /// </summary>
    private void UpdateVolume()
    {
        if (settingsManager != null)
        {
            // Apply music volume
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }

            // Apply SFX volume
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }
        }
    }

    /// <summary>
    /// Called when the game starts
    /// </summary>
    private void OnGameStart()
    {
        // Ensure music is playing when game starts
        if (!isMusicPlaying)
        {
            PlayMusic();
        }
    }


    /// <summary>
    /// Plays or resumes the game music
    /// </summary>
    public void PlayMusic()
    {
        if (gameMusic == null || musicSource == null)
        {
            return;
        }

        if (!musicSource.isPlaying)
        {
            musicSource.clip = gameMusic;
            musicSource.Play();
            isMusicPlaying = true;
        }
    }

    /// <summary>
    /// Stops the game music
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
            isMusicPlaying = false;
        }
    }

    /// <summary>
    /// Pauses the game music
    /// </summary>
    public void PauseMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Pause();
        }
    }

    /// <summary>
    /// Resumes the game music
    /// </summary>
    public void ResumeMusic()
    {
        if (musicSource != null && isMusicPlaying)
        {
            musicSource.UnPause();
        }
    }

    /// <summary>
    /// Ensures music is playing - resumes if paused, starts if stopped
    /// </summary>
    public void EnsureMusicPlaying()
    {
        if (musicSource == null || gameMusic == null)
        {
            return;
        }

        // If music source is paused (isMusicPlaying is true but not playing)
        if (isMusicPlaying && !musicSource.isPlaying)
        {
            musicSource.UnPause();
        }
        // If music hasn't started or was stopped
        else if (!musicSource.isPlaying)
        {
            musicSource.clip = gameMusic;
            musicSource.Play();
            isMusicPlaying = true;
        }
    }

    /// <summary>
    /// Plays the pause button sound effect
    /// </summary>
    public void PlayPauseSound()
    {
        PlaySound(pauseSound);
    }

    /// <summary>
    /// Plays the unpause button sound effect
    /// </summary>
    public void PlayUnpauseSound()
    {
        PlaySound(unpauseSound);
    }

    /// <summary>
    /// Plays the home button sound effect
    /// </summary>
    public void PlayHomeButtonSound()
    {
        PlaySound(homeButtonSound);
    }

    /// <summary>
    /// Plays the retry button sound effect
    /// </summary>
    public void PlayRetryButtonSound()
    {
        PlaySound(retryButtonSound);
    }

    /// <summary>
    /// Plays a specific sound effect
    /// </summary>
    /// <param name="clip">The audio clip to play</param>
    public void PlaySound(AudioClip clip)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("GameSoundManager is not initialized yet");
            return;
        }

        if (clip == null)
        {
            // Silently return if clip is not assigned (allows optional sounds)
            return;
        }

        if (sfxSource == null)
        {
            Debug.LogWarning("SFX AudioSource component is missing!");
            return;
        }

        if (allowSimultaneousSounds)
        {
            // Play one-shot allows multiple sounds to overlap
            sfxSource.PlayOneShot(clip);
        }
        else
        {
            // Stop current sound and play new one
            sfxSource.Stop();
            sfxSource.clip = clip;
            sfxSource.Play();
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
            Debug.LogWarning("GameSoundManager is not initialized yet");
            return;
        }

        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>
    /// Stops all currently playing sound effects
    /// </summary>
    public void StopAllSounds()
    {
        if (sfxSource != null)
        {
            sfxSource.Stop();
        }
    }

    /// <summary>
    /// Sets the music volume
    /// </summary>
    /// <param name="volume">Volume value (0 to 1)</param>
    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
        {
            musicVolume = Mathf.Clamp01(volume);
            musicSource.volume = musicVolume;
        }
    }

    /// <summary>
    /// Sets the SFX volume
    /// </summary>
    /// <param name="volume">Volume value (0 to 1)</param>
    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null)
        {
            sfxVolume = Mathf.Clamp01(volume);
            sfxSource.volume = sfxVolume;
        }
    }

    /// <summary>
    /// Gets the current music volume
    /// </summary>
    /// <returns>Current music volume value (0 to 1)</returns>
    public float GetMusicVolume()
    {
        return musicSource != null ? musicSource.volume : 0f;
    }

    /// <summary>
    /// Gets the current SFX volume
    /// </summary>
    /// <returns>Current SFX volume value (0 to 1)</returns>
    public float GetSFXVolume()
    {
        return sfxSource != null ? sfxSource.volume : 0f;
    }

    private void OnDestroy()
    {
        // Unregister from DependencyRegistry
        DependencyRegistry.Unregister<GameSoundManager>(this);

        // Unsubscribe from events
        if (gameManager != null)
        {
            gameManager.OnGameStart -= OnGameStart;
        }
    }
}
