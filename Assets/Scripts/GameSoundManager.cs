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

    [Header("Game Event Sound Effects")]
    [SerializeField] private AudioClip levelCompleteSound;
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioClip objectSpawnedSound;
    [SerializeField] private AudioClip objectDroppedSound;
    [SerializeField] private AudioClip comboSuccessSound;
    [SerializeField] private AudioClip comboLostSound;

    [Header("Settings")]
    [SerializeField] private float sfxVolume = 0.7f;
    [SerializeField] private bool allowSimultaneousSounds = true;

    [Header("Pitch Randomization")]
    [SerializeField] private bool enablePitchRandomization = true;
    [SerializeField] private float spawnedSoundMinPitch = 0.9f;
    [SerializeField] private float spawnedSoundMaxPitch = 1.1f;
    [SerializeField] private float droppedSoundMinPitch = 0.85f;
    [SerializeField] private float droppedSoundMaxPitch = 1.15f;

    [Header("Music Ducking")]
    [SerializeField] private bool enableMusicDucking = true;
    [SerializeField] private float duckedMusicVolume = 0.2f;
    [SerializeField] private float duckFadeSpeed = 2f;

    // Audio sources
    private AudioSource musicSource;
    private AudioSource sfxSource;

    // References
    private SettingsManager settingsManager;
    private GameManager gameManager;
    private LevelManager levelManager;
    private ObjectSpawner objectSpawner;

    // State
    private bool isInitialized = false;
    private bool isMusicPlaying = false;
    private float originalMusicVolume;
    private Coroutine duckingCoroutine = null;
    private float previousMultiplier = 1f;

    private void Awake()
    {
        // Register with DependencyRegistry
        DependencyRegistry.Register<GameSoundManager>(this);

        // Store original music volume
        originalMusicVolume = musicVolume;

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
        levelManager = DependencyRegistry.Find<LevelManager>();
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();

        // Subscribe to game events if GameManager exists
        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnComboChanged += OnComboChanged;
        }

        // Subscribe to level events if LevelManager exists
        if (levelManager != null)
        {
            levelManager.OnLevelCompleted += OnLevelCompleted;
        }

        // Subscribe to object spawner events if ObjectSpawner exists
        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned += OnObjectSpawned;
            objectSpawner.OnObjectDropped += OnObjectDropped;
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
        // Reset combo tracking
        previousMultiplier = 1f;

        // Ensure music is playing when game starts
        if (!isMusicPlaying)
        {
            PlayMusic();
        }
    }

    /// <summary>
    /// Called when the game is over
    /// </summary>
    private void OnGameOver()
    {
        PlayGameOverSound();
    }

    /// <summary>
    /// Called when a level is completed
    /// </summary>
    /// <param name="stars">Number of stars earned</param>
    /// <param name="score">Final score</param>
    private void OnLevelCompleted(int stars, int score)
    {
        PlayLevelCompleteSound();
    }

    /// <summary>
    /// Called when an object is spawned
    /// </summary>
    /// <param name="spawnedObject">The spawned game object</param>
    private void OnObjectSpawned(GameObject spawnedObject)
    {
        PlayObjectSpawnedSound();
    }

    /// <summary>
    /// Called when an object is dropped
    /// </summary>
    /// <param name="droppedObject">The dropped game object</param>
    private void OnObjectDropped(GameObject droppedObject)
    {
        PlayObjectDroppedSound();
    }

    /// <summary>
    /// Called when combo changes
    /// </summary>
    /// <param name="comboCount">Current combo count</param>
    /// <param name="multiplier">Current multiplier value</param>
    private void OnComboChanged(int comboCount, float multiplier)
    {
        // Play sound when multiplier increases (combo growing)
        if (multiplier > previousMultiplier && multiplier > 1f)
        {
            PlayComboSuccessSound();
        }
        // Play sound when combo is lost (multiplier drops to 1 or below)
        else if (multiplier <= 1f && previousMultiplier > 1f)
        {
            PlayComboLostSound();
        }

        previousMultiplier = multiplier;
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
    /// Plays the level complete sound effect
    /// </summary>
    public void PlayLevelCompleteSound()
    {
        if (enableMusicDucking && levelCompleteSound != null)
        {
            PlaySoundWithDucking(levelCompleteSound);
        }
        else
        {
            PlaySound(levelCompleteSound);
        }
    }

    /// <summary>
    /// Plays the game over sound effect
    /// </summary>
    public void PlayGameOverSound()
    {
        if (enableMusicDucking && gameOverSound != null)
        {
            PlaySoundWithDucking(gameOverSound);
        }
        else
        {
            PlaySound(gameOverSound);
        }
    }

    /// <summary>
    /// Plays the object spawned sound effect with pitch randomization
    /// </summary>
    public void PlayObjectSpawnedSound()
    {
        if (enablePitchRandomization)
        {
            PlaySoundWithRandomPitch(objectSpawnedSound, spawnedSoundMinPitch, spawnedSoundMaxPitch);
        }
        else
        {
            PlaySound(objectSpawnedSound);
        }
    }

    /// <summary>
    /// Plays the object dropped sound effect with pitch randomization
    /// </summary>
    public void PlayObjectDroppedSound()
    {
        if (enablePitchRandomization)
        {
            PlaySoundWithRandomPitch(objectDroppedSound, droppedSoundMinPitch, droppedSoundMaxPitch);
        }
        else
        {
            PlaySound(objectDroppedSound);
        }
    }

    /// <summary>
    /// Plays the combo success sound effect when multiplier increases
    /// </summary>
    public void PlayComboSuccessSound()
    {
        PlaySound(comboSuccessSound);
    }

    /// <summary>
    /// Plays the combo lost sound effect when combo decays to 1 or is lost
    /// </summary>
    public void PlayComboLostSound()
    {
        PlaySound(comboLostSound);
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
    /// Plays a sound with random pitch variation
    /// </summary>
    /// <param name="clip">The audio clip to play</param>
    /// <param name="minPitch">Minimum pitch value</param>
    /// <param name="maxPitch">Maximum pitch value</param>
    public void PlaySoundWithRandomPitch(AudioClip clip, float minPitch, float maxPitch)
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

        // Store original pitch
        float originalPitch = sfxSource.pitch;

        // Set random pitch
        sfxSource.pitch = Random.Range(minPitch, maxPitch);

        // Play the sound
        if (allowSimultaneousSounds)
        {
            sfxSource.PlayOneShot(clip);
        }
        else
        {
            sfxSource.Stop();
            sfxSource.clip = clip;
            sfxSource.Play();
        }

        // Reset pitch back to original after a short delay
        // For PlayOneShot, we need to reset immediately as it doesn't affect the one-shot playback
        sfxSource.pitch = originalPitch;
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

    /// <summary>
    /// Plays a sound effect and ducks (lowers) the music volume while it plays
    /// </summary>
    /// <param name="clip">The audio clip to play</param>
    private void PlaySoundWithDucking(AudioClip clip)
    {
        if (clip == null || sfxSource == null || musicSource == null)
        {
            return;
        }

        // Stop any existing ducking coroutine
        if (duckingCoroutine != null)
        {
            StopCoroutine(duckingCoroutine);
        }

        // Start new ducking coroutine
        duckingCoroutine = StartCoroutine(DuckMusicCoroutine(clip));
    }

    /// <summary>
    /// Coroutine that smoothly ducks music volume down, plays the SFX, then restores music volume
    /// </summary>
    /// <param name="sfxClip">The sound effect to play</param>
    private System.Collections.IEnumerator DuckMusicCoroutine(AudioClip sfxClip)
    {
        // Store the target volume (original or current settings volume)
        float targetVolume = originalMusicVolume;

        // Fade music volume down
        float startVolume = musicSource.volume;
        float targetDuckedVolume = duckedMusicVolume * originalMusicVolume;
        float fadeTime = 0f;
        float fadeDuration = 1f / duckFadeSpeed;

        while (fadeTime < fadeDuration)
        {
            fadeTime += Time.deltaTime;
            float t = fadeTime / fadeDuration;
            musicSource.volume = Mathf.Lerp(startVolume, targetDuckedVolume, t);
            yield return null;
        }

        musicSource.volume = targetDuckedVolume;

        // Play the sound effect
        sfxSource.PlayOneShot(sfxClip);

        // Wait for the sound effect to finish playing
        yield return new WaitForSeconds(sfxClip.length);

        // Fade music volume back up
        startVolume = musicSource.volume;
        fadeTime = 0f;

        while (fadeTime < fadeDuration)
        {
            fadeTime += Time.deltaTime;
            float t = fadeTime / fadeDuration;
            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        musicSource.volume = targetVolume;
        duckingCoroutine = null;
    }

    private void OnDestroy()
    {
        // Unregister from DependencyRegistry
        DependencyRegistry.Unregister<GameSoundManager>(this);

        // Unsubscribe from events
        if (gameManager != null)
        {
            gameManager.OnGameStart -= OnGameStart;
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnComboChanged -= OnComboChanged;
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= OnLevelCompleted;
        }

        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned -= OnObjectSpawned;
            objectSpawner.OnObjectDropped -= OnObjectDropped;
        }
    }
}
