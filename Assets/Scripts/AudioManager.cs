using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages audio playback for music and sound effects
/// Integrates with SettingsManager for volume control
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music Tracks")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameMusic;
    [SerializeField] private AudioClip victoryMusic;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip buttonClick;
    [SerializeField] private AudioClip buttonHover;
    [SerializeField] private AudioClip blockPlace;
    [SerializeField] private AudioClip perfectMatch;
    [SerializeField] private AudioClip levelComplete;
    [SerializeField] private AudioClip gameOver;

    [Header("Settings")]
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private float musicFadeDuration = 1f;
    [SerializeField] private float sfxPitchVariation = 0.1f;

    // References
    private SettingsManager settingsManager;

    // State
    private AudioClip currentMusicTrack;
    private bool isFading = false;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<AudioManager>(this);

        // Create audio sources if not assigned
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
    }

    private void Start()
    {
        // Get settings manager via DependencyRegistry
        settingsManager = DependencyRegistry.Find<SettingsManager>();

        // Apply volume settings
        UpdateVolumeSettings();

        // Play menu music on start
        if (playMusicOnStart && menuMusic != null)
        {
            PlayMusic(menuMusic);
        }
    }

    /// <summary>
    /// Update audio source volumes based on settings
    /// </summary>
    public void UpdateVolumeSettings()
    {
        if (settingsManager == null) return;

        float masterVolume = settingsManager.IsMuted ? 0 : settingsManager.MasterVolume;

        if (musicSource != null)
        {
            musicSource.volume = settingsManager.MusicVolume * masterVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.volume = settingsManager.SFXVolume * masterVolume;
        }
    }

    /// <summary>
    /// Play a music track
    /// </summary>
    public void PlayMusic(AudioClip clip, bool fadeIn = true)
    {
        if (clip == null || musicSource == null) return;

        // Don't restart if already playing
        if (currentMusicTrack == clip && musicSource.isPlaying) return;

        if (fadeIn && musicSource.isPlaying)
        {
            // Fade out current, then fade in new
            StartCoroutine(CrossfadeMusic(clip));
        }
        else
        {
            // Play immediately
            currentMusicTrack = clip;
            musicSource.clip = clip;
            musicSource.Play();
        }
    }

    /// <summary>
    /// Crossfade between music tracks
    /// </summary>
    private System.Collections.IEnumerator CrossfadeMusic(AudioClip newClip)
    {
        if (isFading) yield break;
        isFading = true;

        float startVolume = musicSource.volume;
        float timer = 0;

        // Fade out
        while (timer < musicFadeDuration / 2)
        {
            timer += Time.deltaTime;
            float progress = timer / (musicFadeDuration / 2);
            musicSource.volume = Mathf.Lerp(startVolume, 0, progress);
            yield return null;
        }

        // Switch track
        currentMusicTrack = newClip;
        musicSource.clip = newClip;
        musicSource.Play();

        // Fade in
        timer = 0;
        while (timer < musicFadeDuration / 2)
        {
            timer += Time.deltaTime;
            float progress = timer / (musicFadeDuration / 2);
            musicSource.volume = Mathf.Lerp(0, startVolume, progress);
            yield return null;
        }

        musicSource.volume = startVolume;
        isFading = false;
    }

    /// <summary>
    /// Stop music with optional fade out
    /// </summary>
    public void StopMusic(bool fadeOut = true)
    {
        if (musicSource == null) return;

        if (fadeOut)
        {
            StartCoroutine(FadeOutMusic());
        }
        else
        {
            musicSource.Stop();
            currentMusicTrack = null;
        }
    }

    /// <summary>
    /// Fade out music
    /// </summary>
    private System.Collections.IEnumerator FadeOutMusic()
    {
        if (isFading) yield break;
        isFading = true;

        float startVolume = musicSource.volume;
        float timer = 0;

        while (timer < musicFadeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / musicFadeDuration;
            musicSource.volume = Mathf.Lerp(startVolume, 0, progress);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume;
        currentMusicTrack = null;
        isFading = false;
    }

    /// <summary>
    /// Play a sound effect
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f, bool randomPitch = false)
    {
        if (clip == null || sfxSource == null) return;

        // Apply pitch variation if requested
        if (randomPitch)
        {
            sfxSource.pitch = 1f + Random.Range(-sfxPitchVariation, sfxPitchVariation);
        }
        else
        {
            sfxSource.pitch = 1f;
        }

        sfxSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>
    /// Play sound at a specific position in 3D space
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null) return;

        AudioSource.PlayClipAtPoint(clip, position, volumeScale * (settingsManager?.SFXVolume ?? 1f));
    }

    // Convenience methods for common sounds

    public void PlayButtonClick()
    {
        PlaySFX(buttonClick);
    }

    public void PlayButtonHover()
    {
        PlaySFX(buttonHover, 0.5f);
    }

    public void PlayBlockPlace()
    {
        PlaySFX(blockPlace, 1f, true);
    }

    public void PlayPerfectMatch()
    {
        PlaySFX(perfectMatch);
    }

    public void PlayLevelComplete()
    {
        PlaySFX(levelComplete);
    }

    public void PlayGameOver()
    {
        PlaySFX(gameOver);
    }

    // Music track methods

    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic);
    }

    public void PlayGameMusic()
    {
        PlayMusic(gameMusic);
    }

    public void PlayVictoryMusic()
    {
        PlayMusic(victoryMusic);
    }

    /// <summary>
    /// Pause music
    /// </summary>
    public void PauseMusic()
    {
        if (musicSource != null)
        {
            musicSource.Pause();
        }
    }

    /// <summary>
    /// Resume music
    /// </summary>
    public void ResumeMusic()
    {
        if (musicSource != null)
        {
            musicSource.UnPause();
        }
    }

    /// <summary>
    /// Get current music track
    /// </summary>
    public AudioClip CurrentMusicTrack => currentMusicTrack;

    /// <summary>
    /// Check if music is playing
    /// </summary>
    public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

    private void OnDestroy()
    {
        // Unregister from dependency registry
        DependencyRegistry.Unregister<AudioManager>(this);
    }
}

