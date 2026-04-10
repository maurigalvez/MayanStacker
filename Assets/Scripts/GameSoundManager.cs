using UnityEngine;

/// <summary>
/// Manages game music and sound effects during gameplay, including UI button sounds
/// </summary>
public class GameSoundManager : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioClip defaultGameMusic;
    [Tooltip("Music tracks for Infinite Stacker mode (randomly selected if multiple)")]
    [SerializeField] private AudioClip[] infiniteStackerMusicTracks;
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
    [SerializeField] private AudioClip codexUnlockSound;
    [SerializeField] private AudioClip kukulkanShiftSound;
    [Tooltip("Tick sound played repeatedly while the final score counts up on the level complete panel")]
    [SerializeField] private AudioClip scoreCountSound;

    [Header("Settings")]
    [SerializeField] private float sfxVolume = 0.7f;
    [SerializeField] private bool allowSimultaneousSounds = true;

    [Header("Pitch Randomization")]
    [SerializeField] private bool enablePitchRandomization = true;
    [SerializeField] private float spawnedSoundMinPitch = 0.9f;
    [SerializeField] private float spawnedSoundMaxPitch = 1.1f;
    [SerializeField] private float droppedSoundMinPitch = 0.85f;
    [SerializeField] private float droppedSoundMaxPitch = 1.15f;

    [Header("Combo Pitch Progression")]
    [Tooltip("Base pitch for combo success sound (1.0 = normal pitch)")]
    [SerializeField] private float comboBasePitch = 1.0f;
    [Tooltip("Pitch increase per combo increment")]
    [SerializeField] private float comboPitchIncrement = 0.1f;
    [Tooltip("Maximum pitch for combo sound")]
    [SerializeField] private float comboMaxPitch = 2.0f;

    [Header("Music Ducking")]
    [SerializeField] private bool enableMusicDucking = true;
    [SerializeField] private float duckedMusicVolume = 0.2f;
    [SerializeField] private float duckFadeSpeed = 2f;

    // Audio sources
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource comboSource; // Separate AudioSource for combo sounds with pitch modifications

    // References
    private SettingsManager settingsManager;
    private GameManager gameManager;
    private LevelManager levelManager;
    private ObjectSpawner objectSpawner;
    private StackManager stackManager;

    // State
    private bool isInitialized = false;
    private bool isMusicPlaying = false;
    private float originalMusicVolume;
    private float originalSfxPitch = 1.0f;
    private Coroutine duckingCoroutine = null;
    private float previousMultiplier = 1f;
    private int currentComboCount = 0;
    private int previousComboCount = 0; // Track previous combo count to detect increases
    private int pitchBaseCombo = 0; // Combo count at the time of last Kukulkan shift (for pitch calculation)
    private bool skipNextComboSound = false; // Flag to skip combo sound when Kukulkan shift is triggered

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
        sfxSource.pitch = 1.0f;
        originalSfxPitch = sfxSource.pitch;

        // Create and configure separate AudioSource for combo sounds (allows pitch modification)
        comboSource = gameObject.AddComponent<AudioSource>();
        comboSource.playOnAwake = false;
        comboSource.loop = false;
        comboSource.volume = sfxVolume;
        comboSource.pitch = comboBasePitch;
    }

    private void Start()
    {
        // Find dependencies
        settingsManager = DependencyRegistry.Find<SettingsManager>();
        gameManager = DependencyRegistry.Find<GameManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        stackManager = DependencyRegistry.Find<StackManager>();

        // Subscribe to game events if GameManager exists
        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnComboChanged += OnComboChanged;
            gameManager.OnPerfectHitStreak += OnPerfectHitStreak;
        }

        // Subscribe to level events if LevelManager exists
        if (levelManager != null)
        {
            levelManager.OnLevelCompleted += OnLevelCompleted;
            levelManager.OnLevelLoaded += OnLevelLoaded;
        }

        // Subscribe to object spawner events if ObjectSpawner exists
        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned += OnObjectSpawned;
            objectSpawner.OnObjectDropped += OnObjectDropped;
        }

        // Subscribe to stack events if StackManager exists
        if (stackManager != null)
        {
            stackManager.OnStackStraightened += OnStackStraightened;
        }

        // Apply current volume settings
        if (settingsManager != null)
        {
            UpdateVolume();
        }

        isInitialized = true;

        // Start playing music based on current game mode
        PlayMusicForCurrentMode();
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

            // Apply volume to combo source as well
            if (comboSource != null)
            {
                comboSource.volume = sfxVolume;
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
        previousComboCount = 0;
        pitchBaseCombo = 0;
        ResetComboPitch();

        // Ensure music is playing when game starts
        if (!isMusicPlaying)
        {
            PlayMusicForCurrentMode();
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
    /// Called when a level is loaded
    /// </summary>
    /// <param name="levelData">The loaded level data</param>
    private void OnLevelLoaded(LevelData levelData)
    {
        if (levelData != null && levelData.gameMusic != null)
        {
            // Level has specific music - switch to it
            SetMusic(levelData.gameMusic);
        }
        else
        {
            // No level-specific music, use default for current mode
            PlayMusicForCurrentMode();
        }
    }

    /// <summary>
    /// Called when a level is completed
    /// </summary>
    /// <param name="stars">Number of stars earned</param>
    /// <param name="score">Final score</param>
    /// <param name="showCodexPopup">Whether to show codex unlock popup</param>
    private void OnLevelCompleted(int stars, int score, bool showCodexPopup)
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
        // Update current combo count
        currentComboCount = comboCount;

        // Play sound when combo count increases (including first perfect hit)
        // This ensures the sound plays on the first perfect hit (comboCount goes from 0 to 1)
        if (comboCount > previousComboCount && comboCount > 0)
        {
            // Skip combo sound if Kukulkan shift was just triggered
            if (!skipNextComboSound)
            {
                PlayComboSuccessSound();
            }
            else
            {
                // Reset the flag after skipping once
                // Update pitchBaseCombo to account for the skipped combo
                // This ensures the next sound plays at base pitch (relativeComboCount = 1)
                pitchBaseCombo = comboCount;
                skipNextComboSound = false;
                if (comboSource != null)
                {
                    comboSource.pitch = comboBasePitch;
                }
            }
        }
        // Play sound when combo is lost (combo count drops to 0)
        else if (comboCount == 0 && previousComboCount > 0)
        {
            ResetComboPitch();
            PlayComboLostSound();
        }

        previousComboCount = comboCount;
        previousMultiplier = multiplier;
    }

    /// <summary>
    /// Called when perfect hit streak is achieved (Kukulkan shift triggers)
    /// This fires immediately when the streak is achieved, before the animation
    /// </summary>
    private void OnPerfectHitStreak()
    {
        // Reset pitch tracking when Kukulkan shift is activated
        // The combo count may continue, but pitch should reset to base
        pitchBaseCombo = currentComboCount; // Store current combo as the new base for pitch calculation

        // Reset the AudioSource pitch to base immediately
        if (comboSource != null)
        {
            comboSource.pitch = comboBasePitch;
        }

        // Skip the next combo sound to avoid playing it when Kukulkan shift triggers
        skipNextComboSound = true;

        // Play sound immediately when perfect hit streak is achieved
        PlayKukulkanShiftSound();
    }

    /// <summary>
    /// Called when Kukulkan's shift ability is triggered (stack straightening)
    /// This is called when the animation completes, but we handle sound in OnPerfectHitStreak for immediate response
    /// </summary>
    private void OnStackStraightened()
    {
        // Sound is already played in OnPerfectHitStreak for immediate response
        // This event is kept for any future needs, but sound plays immediately on OnPerfectHitStreak
    }


    /// <summary>
    /// Plays music based on the current game mode
    /// </summary>
    private void PlayMusicForCurrentMode()
    {
        if (gameManager == null)
        {
            // Fallback to default music if GameManager not available
            SetMusic(defaultGameMusic);
            return;
        }

        if (gameManager.CurrentGameMode == GameMode.InfiniteStacker)
        {
            // Infinite Stacker mode - use random track from available tracks
            PlayInfiniteStackerMusic();
        }
        else if (gameManager.CurrentGameMode == GameMode.StackerLevels)
        {
            // Stacker Levels mode - check if current level has specific music
            if (levelManager != null && levelManager.CurrentLevel != null && levelManager.CurrentLevel.gameMusic != null)
            {
                SetMusic(levelManager.CurrentLevel.gameMusic);
            }
            else
            {
                // No level-specific music, use default
                SetMusic(defaultGameMusic);
            }
        }
        else
        {
            // Fallback to default music
            SetMusic(defaultGameMusic);
        }
    }

    /// <summary>
    /// Plays a random music track from the Infinite Stacker music tracks
    /// </summary>
    private void PlayInfiniteStackerMusic()
    {
        AudioClip musicToPlay = null;

        // If we have Infinite Stacker tracks, randomly select one
        if (infiniteStackerMusicTracks != null && infiniteStackerMusicTracks.Length > 0)
        {
            // Filter out null tracks
            var validTracks = System.Array.FindAll(infiniteStackerMusicTracks, track => track != null);
            if (validTracks.Length > 0)
            {
                musicToPlay = validTracks[Random.Range(0, validTracks.Length)];
            }
        }

        // Fallback to default music if no Infinite Stacker tracks available
        if (musicToPlay == null)
        {
            musicToPlay = defaultGameMusic;
        }

        SetMusic(musicToPlay);
    }

    /// <summary>
    /// Sets the music clip and plays it
    /// </summary>
    /// <param name="clip">The audio clip to play</param>
    private void SetMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null)
        {
            Debug.LogWarning("GameSoundManager: Cannot set music - clip or musicSource is null");
            return;
        }

        // If different music is already playing, stop and switch
        if (musicSource.isPlaying && musicSource.clip != clip)
        {
            musicSource.Stop();
        }

        musicSource.clip = clip;

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
            isMusicPlaying = true;
        }
    }

    /// <summary>
    /// Plays or resumes the game music (uses current mode's music)
    /// </summary>
    public void PlayMusic()
    {
        PlayMusicForCurrentMode();
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
        if (musicSource == null)
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
            // Reload music for current mode
            PlayMusicForCurrentMode();
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
    /// Plays the codex unlock sound effect
    /// </summary>
    public void PlayCodexUnlockSound()
    {
        if (enableMusicDucking && codexUnlockSound != null)
        {
            PlaySoundWithDucking(codexUnlockSound);
        }
        else
        {
            PlaySound(codexUnlockSound);
        }
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
    /// Plays the score count tick sound (used while animating score on level complete panel).
    /// Optional pitch parameter lets callers ramp the tick pitch as the score climbs.
    /// </summary>
    public void PlayScoreCountSound(float pitch = 1f)
    {
        if (!isInitialized || scoreCountSound == null || sfxSource == null)
        {
            return;
        }

        float originalPitch = sfxSource.pitch;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(scoreCountSound);
        sfxSource.pitch = originalPitch;
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
    /// Pitch increases progressively with combo count
    /// </summary>
    public void PlayComboSuccessSound()
    {
        if (comboSuccessSound == null)
        {
            return;
        }

        // Calculate pitch based on combo count
        float pitch = CalculateComboPitch(currentComboCount);

        Debug.Log($"Playing combo sound - Combo: {currentComboCount}, Pitch: {pitch:F2}");

        // Play with the calculated pitch
        PlaySoundWithPitch(comboSuccessSound, pitch);
    }

    /// <summary>
    /// Calculates the pitch for combo sound based on combo count
    /// Uses relative combo count (current - pitchBaseCombo) to reset pitch when Kukulkan shift is triggered
    /// </summary>
    /// <param name="comboCount">Current combo count</param>
    /// <returns>Pitch value for the combo sound</returns>
    private float CalculateComboPitch(int comboCount)
    {
        // Calculate relative combo count (resets when Kukulkan shift is triggered)
        int relativeComboCount = comboCount - pitchBaseCombo;

        // Ensure relative combo is at least 0
        relativeComboCount = Mathf.Max(0, relativeComboCount);

        // Calculate pitch: base + ((relative combo count - 1) * increment), capped at max
        // Relative combo count of 1 = base pitch, relative combo count of 2 = base + increment, etc.
        float calculatedPitch = comboBasePitch + ((relativeComboCount - 1) * comboPitchIncrement);
        return Mathf.Clamp(calculatedPitch, comboBasePitch, comboMaxPitch);
    }

    /// <summary>
    /// Resets combo pitch tracking (called when combo is lost or Kukulkan shift is activated)
    /// </summary>
    private void ResetComboPitch()
    {
        // Reset pitch base when combo is lost (not when Kukulkan shift happens, as that's handled separately)
        // When combo is lost, we want to reset everything
        if (currentComboCount == 0)
        {
            pitchBaseCombo = 0;
        }

        if (comboSource != null)
        {
            comboSource.pitch = comboBasePitch;
        }
    }

    /// <summary>
    /// Plays the combo lost sound effect when combo decays to 1 or is lost
    /// </summary>
    public void PlayComboLostSound()
    {
        PlaySound(comboLostSound);
    }

    /// <summary>
    /// Plays the Kukulkan shift sound effect when stack straightening is triggered
    /// Plays immediately without music ducking to avoid delay
    /// </summary>
    public void PlayKukulkanShiftSound()
    {
        if (kukulkanShiftSound == null || sfxSource == null)
        {
            return;
        }

        // Play immediately without ducking to avoid any delay
        // Use PlayOneShot for instant playback
        if (allowSimultaneousSounds)
        {
            sfxSource.PlayOneShot(kukulkanShiftSound);
        }
        else
        {
            sfxSource.Stop();
            sfxSource.clip = kukulkanShiftSound;
            sfxSource.Play();
        }
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
    /// Plays a sound with a specific pitch value using a dedicated AudioSource
    /// </summary>
    /// <param name="clip">The audio clip to play</param>
    /// <param name="pitch">The pitch value to use</param>
    private void PlaySoundWithPitch(AudioClip clip, float pitch)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("GameSoundManager is not initialized yet");
            return;
        }

        if (clip == null || comboSource == null)
        {
            return;
        }

        // Set the desired pitch on the dedicated combo AudioSource
        comboSource.pitch = pitch;
        comboSource.volume = sfxVolume;

        // Play the sound using the combo source
        // This allows us to modify pitch without affecting other sounds
        if (allowSimultaneousSounds)
        {
            comboSource.PlayOneShot(clip);
        }
        else
        {
            // Stop current sound and play new one with the set pitch
            comboSource.Stop();
            comboSource.clip = clip;
            comboSource.Play();
        }
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
            gameManager.OnPerfectHitStreak -= OnPerfectHitStreak;
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= OnLevelCompleted;
            levelManager.OnLevelLoaded -= OnLevelLoaded;
        }

        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned -= OnObjectSpawned;
            objectSpawner.OnObjectDropped -= OnObjectDropped;
        }

        if (stackManager != null)
        {
            stackManager.OnStackStraightened -= OnStackStraightened;
        }
    }
}
