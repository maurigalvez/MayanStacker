using UnityEngine;

/// <summary>
/// Manages sound effects for the main menu UI, including button clicks and navigation sounds
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MainMenuSoundManager : MonoBehaviour
{
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

    // Audio source component
    private AudioSource audioSource;

    // References
    private SettingsManager settingsManager;

    // State
    private bool isInitialized = false;

    private void Awake()
    {
        // Register with DependencyRegistry
        DependencyRegistry.Register<MainMenuSoundManager>(this);

        // Get or add AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure AudioSource
        audioSource.playOnAwake = false;
        audioSource.volume = defaultVolume;
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

        isInitialized = true;
    }

    /// <summary>
    /// Updates the volume based on SettingsManager settings
    /// </summary>
    private void UpdateVolume()
    {
        if (settingsManager != null && audioSource != null)
        {
            // You can adjust this to use specific volume settings from SettingsManager
            // For now, using the default volume with master volume multiplier if available
            audioSource.volume = defaultVolume;
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

        if (audioSource == null)
        {
            Debug.LogWarning("AudioSource component is missing!");
            return;
        }

        if (allowSimultaneousSounds)
        {
            // Play one-shot allows multiple sounds to overlap
            audioSource.PlayOneShot(clip);
        }
        else
        {
            // Stop current sound and play new one
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
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

        if (clip == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>
    /// Stops all currently playing sounds
    /// </summary>
    public void StopAllSounds()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Sets the volume for UI sounds
    /// </summary>
    /// <param name="volume">Volume value (0 to 1)</param>
    public void SetVolume(float volume)
    {
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }

    /// <summary>
    /// Gets the current volume
    /// </summary>
    /// <returns>Current volume value (0 to 1)</returns>
    public float GetVolume()
    {
        return audioSource != null ? audioSource.volume : 0f;
    }

    private void OnDestroy()
    {
        // Unregister from DependencyRegistry
        DependencyRegistry.Unregister<MainMenuSoundManager>(this);
    }
}

