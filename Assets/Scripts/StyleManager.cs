using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages visual style changes based on time of day
/// Handles sprite swapping for crane and stackable objects, and color tinting for environment
/// </summary>
public class StyleManager : MonoBehaviour
{
    /// <summary>
    /// Time of day options
    /// </summary>
    public enum TimeOfDay
    {
        Morning,
        Sunset,
        Night
    }

    [Header("Time of Day Settings")]
    [SerializeField] private TimeOfDay currentTimeOfDay = TimeOfDay.Morning;
    [Tooltip("Automatically cycle through times of day based on stack height")]
    [SerializeField] private bool autoCycleByHeight = false;
    [Tooltip("Stack height threshold for switching to Sunset")]
    [SerializeField] private int sunsetHeightThreshold = 20;
    [Tooltip("Stack height threshold for switching to Night")]
    [SerializeField] private int nightHeightThreshold = 40;

    [Header("Crane Sprites")]
    [SerializeField] private Sprite craneSpriteMorning;
    [SerializeField] private Sprite craneSpriteSunset;
    [SerializeField] private Sprite craneSpriteNight;
    [Tooltip("Reference to crane SpriteRenderer(s) - assign in Inspector")]
    [SerializeField] private SpriteRenderer[] craneSpriteRenderers;

    [Header("Neck Connection Sprites")]
    [SerializeField] private Sprite neckSpriteMorning;
    [SerializeField] private Sprite neckSpriteSunset;
    [SerializeField] private Sprite neckSpriteNight;
    [Tooltip("Reference to neck connection SpriteRenderer(s) - assign in Inspector")]
    [SerializeField] private SpriteRenderer[] neckSpriteRenderers;

    [Header("Stackable Object Sprites")]
    [SerializeField] private Sprite stackableSpriteMorning;
    [SerializeField] private Sprite stackableSpriteSunset;
    [SerializeField] private Sprite stackableSpriteNight;
    [Tooltip("If true, uses sprites from StyleManager. If false, uses prefab sprites")]
    [SerializeField] private bool overrideStackableSprites = true;

    [Header("Sky Sprites")]
    [SerializeField] private Sprite skySpriteMorning;
    [SerializeField] private Sprite skySpriteSunset;
    [SerializeField] private Sprite skySpriteNight;
    [Tooltip("Reference to sky SpriteRenderer - assign in Inspector")]
    [SerializeField] private SpriteRenderer skySpriteRenderer;
    [Tooltip("If true, swaps sky sprites. If false, only applies color tint")]
    [SerializeField] private bool useSkySpriteSwap = true;

    [Header("Environment Color Tints")]
    [SerializeField] private Color morningTint = Color.white;
    [SerializeField] private Color sunsetTint = new Color(1f, 0.7f, 0.5f, 1f); // Warm orange
    [SerializeField] private Color nightTint = new Color(0.3f, 0.3f, 0.5f, 1f); // Cool blue
    [Tooltip("References to environment sprite renderers - assign in Inspector")]
    [SerializeField] private SpriteRenderer[] environmentSpriteRenderersArray;

    // References
    private ObjectSpawner objectSpawner;
    private StackManager stackManager;
    private GameManager gameManager;
    private ThemeManager themeManager;
    private List<SpriteRenderer> environmentSpriteRenderers = new List<SpriteRenderer>();
    private int lastStackHeight = 0;

    // Events
    public System.Action<TimeOfDay> OnTimeOfDayChanged;

    // Properties
    public TimeOfDay CurrentTimeOfDay => currentTimeOfDay;

    private void Awake()
    {
        // Register with dependency registry
        DependencyRegistry.Register<StyleManager>(this);
    }

    private void Start()
    {
        // Subscribe to scene loading events to refresh dependencies
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Find dependencies
        RefreshDependencies();

        // Initialize environment sprite renderers from serialized array
        InitializeEnvironmentSpriteRenderers();

        // Apply initial style based on game mode
        if (IsInfiniteStackerMode())
        {
            // InfiniteStacker: use height-based or default time of day
            ApplyStyle(currentTimeOfDay);
        }
        else if (IsStackerLevelsMode())
        {
            // StackerLevels: use selected theme from ThemeManager
            ApplyThemeFromThemeManager();
        }

        // Subscribe to stack manager events for height-based time of day cycling
        if (autoCycleByHeight && stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
            // Initialize last stack height
            lastStackHeight = stackManager.GetStackCount();
        }
    }

    /// <summary>
    /// Called when a new scene is loaded
    /// Refreshes dependencies since ThemeManager persists across scenes
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshDependencies();

        // Reapply theme if in StackerLevels mode (ThemeManager persists, so theme should be applied)
        if (IsStackerLevelsMode())
        {
            ApplyThemeFromThemeManager();
        }
    }

    /// <summary>
    /// Refreshes dependencies and re-subscribes to events
    /// Call this when switching scenes or if managers are recreated
    /// </summary>
    private void RefreshDependencies()
    {
        // Unsubscribe from old references if they exist
        if (gameManager != null)
        {
            gameManager.OnGameModeChanged -= OnGameModeChanged;
            gameManager.OnGameRestart -= OnGameRestart;
        }

        if (themeManager != null)
        {
            themeManager.OnThemeChanged -= OnThemeChanged;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }

        // Find new references
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        stackManager = DependencyRegistry.Find<StackManager>();
        gameManager = DependencyRegistry.Find<GameManager>();
        themeManager = DependencyRegistry.Find<ThemeManager>();

        // Subscribe to game mode changes and restart events
        if (gameManager != null)
        {
            gameManager.OnGameModeChanged += OnGameModeChanged;
            gameManager.OnGameRestart += OnGameRestart;
        }

        // Subscribe to theme changes for level play
        if (themeManager != null)
        {
            themeManager.OnThemeChanged += OnThemeChanged;

            // Apply theme immediately if in StackerLevels mode (ensures first block gets correct sprite)
            if (IsStackerLevelsMode())
            {
                ApplyThemeFromThemeManager();
            }
        }

        // Re-subscribe to stack manager events for height-based time of day cycling
        if (autoCycleByHeight && stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }
    }

    /// <summary>
    /// Handle when an object is added to the stack - check if we need to update time of day
    /// </summary>
    private void OnObjectAddedToStack(StackableObject stackableObject)
    {
        // Only check if in InfiniteStacker mode and auto-cycling is enabled
        if (!IsInfiniteStackerMode() || !autoCycleByHeight || stackManager == null) return;

        int currentStackHeight = stackManager.GetStackCount();
        if (currentStackHeight != lastStackHeight)
        {
            lastStackHeight = currentStackHeight;
            CheckHeightBasedTimeOfDay();
        }
    }

    /// <summary>
    /// Check and update time of day based on stack height
    /// </summary>
    private void CheckHeightBasedTimeOfDay()
    {
        TimeOfDay targetTimeOfDay = currentTimeOfDay;

        if (lastStackHeight >= nightHeightThreshold)
        {
            targetTimeOfDay = TimeOfDay.Night;
        }
        else if (lastStackHeight >= sunsetHeightThreshold)
        {
            targetTimeOfDay = TimeOfDay.Sunset;
        }
        else
        {
            targetTimeOfDay = TimeOfDay.Morning;
        }

        if (targetTimeOfDay != currentTimeOfDay)
        {
            SetTimeOfDay(targetTimeOfDay);
        }
    }

    /// <summary>
    /// Initialize environment sprite renderers from serialized array
    /// </summary>
    private void InitializeEnvironmentSpriteRenderers()
    {
        environmentSpriteRenderers.Clear();

        if (environmentSpriteRenderersArray != null)
        {
            foreach (SpriteRenderer renderer in environmentSpriteRenderersArray)
            {
                if (renderer != null && !environmentSpriteRenderers.Contains(renderer))
                {
                    environmentSpriteRenderers.Add(renderer);
                }
            }
        }
    }


    /// <summary>
    /// Set the time of day and apply style changes
    /// </summary>
    public void SetTimeOfDay(TimeOfDay timeOfDay)
    {
        if (currentTimeOfDay == timeOfDay) return;

        currentTimeOfDay = timeOfDay;

        // Apply styles if in InfiniteStacker mode or StackerLevels mode
        if (IsInfiniteStackerMode() || IsStackerLevelsMode())
        {
            ApplyStyle(timeOfDay);
        }

        OnTimeOfDayChanged?.Invoke(timeOfDay);
    }

    /// <summary>
    /// Apply style changes based on time of day
    /// </summary>
    private void ApplyStyle(TimeOfDay timeOfDay)
    {
        // Apply crane sprite
        ApplyCraneSprite(timeOfDay);

        // Apply neck connection sprites
        ApplyNeckSprites(timeOfDay);

        // Apply stackable object sprite (will be used for next spawn)
        // Note: We can't change existing objects, only future ones

        // Apply sky sprite
        ApplySkySprite(timeOfDay);

        // Apply environment color tint
        ApplyEnvironmentTint(timeOfDay);
    }

    /// <summary>
    /// Apply crane sprite based on time of day
    /// </summary>
    private void ApplyCraneSprite(TimeOfDay timeOfDay)
    {
        if (craneSpriteRenderers == null || craneSpriteRenderers.Length == 0) return;

        Sprite targetSprite = GetCraneSpriteForTimeOfDay(timeOfDay);
        if (targetSprite == null) return;

        // Apply sprite to all crane sprite renderers
        foreach (SpriteRenderer renderer in craneSpriteRenderers)
        {
            if (renderer != null)
            {
                renderer.sprite = targetSprite;
            }
        }
    }

    /// <summary>
    /// Get crane sprite for the specified time of day
    /// </summary>
    private Sprite GetCraneSpriteForTimeOfDay(TimeOfDay timeOfDay)
    {
        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                return craneSpriteMorning;
            case TimeOfDay.Sunset:
                return craneSpriteSunset;
            case TimeOfDay.Night:
                return craneSpriteNight;
            default:
                return craneSpriteMorning;
        }
    }

    /// <summary>
    /// Apply neck connection sprites based on time of day
    /// </summary>
    private void ApplyNeckSprites(TimeOfDay timeOfDay)
    {
        if (neckSpriteRenderers == null || neckSpriteRenderers.Length == 0) return;

        Sprite targetSprite = GetNeckSpriteForTimeOfDay(timeOfDay);
        if (targetSprite == null) return;

        // Apply sprite to all neck sprite renderers
        foreach (SpriteRenderer renderer in neckSpriteRenderers)
        {
            if (renderer != null)
            {
                renderer.sprite = targetSprite;
            }
        }
    }

    /// <summary>
    /// Get neck connection sprite for the specified time of day
    /// </summary>
    private Sprite GetNeckSpriteForTimeOfDay(TimeOfDay timeOfDay)
    {
        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                return neckSpriteMorning;
            case TimeOfDay.Sunset:
                return neckSpriteSunset;
            case TimeOfDay.Night:
                return neckSpriteNight;
            default:
                return neckSpriteMorning;
        }
    }

    /// <summary>
    /// Apply sky sprite based on time of day
    /// </summary>
    private void ApplySkySprite(TimeOfDay timeOfDay)
    {
        if (skySpriteRenderer == null) return;

        if (useSkySpriteSwap)
        {
            // Swap sprite based on time of day
            Sprite targetSprite = GetSkySpriteForTimeOfDay(timeOfDay);
            if (targetSprite != null)
            {
                skySpriteRenderer.sprite = targetSprite;
            }
        }
        else
        {
            // Only apply color tint (existing behavior)
            Color targetTint = GetEnvironmentTintForTimeOfDay(timeOfDay);
            skySpriteRenderer.color = targetTint;
        }
    }

    /// <summary>
    /// Get sky sprite for the specified time of day
    /// </summary>
    private Sprite GetSkySpriteForTimeOfDay(TimeOfDay timeOfDay)
    {
        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                return skySpriteMorning;
            case TimeOfDay.Sunset:
                return skySpriteSunset;
            case TimeOfDay.Night:
                return skySpriteNight;
            default:
                return skySpriteMorning;
        }
    }

    /// <summary>
    /// Get stackable object sprite for the specified time of day
    /// </summary>
    public Sprite GetStackableSpriteForTimeOfDay(TimeOfDay timeOfDay)
    {
        if (!overrideStackableSprites) return null;

        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                return stackableSpriteMorning;
            case TimeOfDay.Sunset:
                return stackableSpriteSunset;
            case TimeOfDay.Night:
                return stackableSpriteNight;
            default:
                return stackableSpriteMorning;
        }
    }

    /// <summary>
    /// Get current stackable object sprite
    /// </summary>
    public Sprite GetCurrentStackableSprite()
    {
        // Return sprite if in InfiniteStacker mode or StackerLevels mode
        if (!IsInfiniteStackerMode() && !IsStackerLevelsMode())
        {
            return null;
        }

        return GetStackableSpriteForTimeOfDay(currentTimeOfDay);
    }

    /// <summary>
    /// Apply environment color tint based on time of day
    /// </summary>
    private void ApplyEnvironmentTint(TimeOfDay timeOfDay)
    {
        Color targetTint = GetEnvironmentTintForTimeOfDay(timeOfDay);

        // Clean up null references before applying tints
        CleanupEnvironmentSpriteRenderers();

        // Update existing environment sprite renderers
        foreach (SpriteRenderer renderer in environmentSpriteRenderers)
        {
            if (renderer != null)
            {
                renderer.color = targetTint;
            }
        }
    }

    /// <summary>
    /// Get environment color tint for the specified time of day
    /// </summary>
    private Color GetEnvironmentTintForTimeOfDay(TimeOfDay timeOfDay)
    {
        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                return morningTint;
            case TimeOfDay.Sunset:
                return sunsetTint;
            case TimeOfDay.Night:
                return nightTint;
            default:
                return morningTint;
        }
    }

    /// <summary>
    /// Refresh the list of environment sprite renderers
    /// Useful when new environment objects are spawned
    /// </summary>
    public void RefreshEnvironmentSpriteRenderers()
    {
        InitializeEnvironmentSpriteRenderers();
        // Re-apply current tint to renderers
        ApplyEnvironmentTint(currentTimeOfDay);
    }

    /// <summary>
    /// Clean up null references from the environment sprite renderers list
    /// Should be called periodically to remove destroyed objects
    /// </summary>
    public void CleanupEnvironmentSpriteRenderers()
    {
        environmentSpriteRenderers.RemoveAll(renderer => renderer == null);
    }

    /// <summary>
    /// Register sprite renderers from an environment GameObject
    /// Automatically finds all SpriteRenderers in the GameObject and its children
    /// </summary>
    /// <param name="environmentObject">The GameObject containing environment sprite renderers</param>
    public void RegisterEnvironmentSpriteRenderers(GameObject environmentObject)
    {
        if (environmentObject == null) return;

        SpriteRenderer[] renderers = environmentObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            RegisterEnvironmentSpriteRenderer(renderer);
        }
    }

    /// <summary>
    /// Register a single environment sprite renderer
    /// </summary>
    /// <param name="renderer">The SpriteRenderer to register</param>
    public void RegisterEnvironmentSpriteRenderer(SpriteRenderer renderer)
    {
        if (renderer == null) return;

        // Add to list if not already there
        if (!environmentSpriteRenderers.Contains(renderer))
        {
            environmentSpriteRenderers.Add(renderer);
        }

        // Apply current tint if in InfiniteStacker mode or StackerLevels mode
        if (IsInfiniteStackerMode() || IsStackerLevelsMode())
        {
            Color targetTint = GetEnvironmentTintForTimeOfDay(currentTimeOfDay);
            renderer.color = targetTint;
        }
    }

    /// <summary>
    /// Apply environment tint to a specific sprite renderer
    /// Useful for newly spawned environment assets
    /// </summary>
    /// <param name="renderer">The SpriteRenderer to apply tint to</param>
    public void ApplyTintToSpriteRenderer(SpriteRenderer renderer)
    {
        // Register the renderer first (which will also apply the tint)
        RegisterEnvironmentSpriteRenderer(renderer);
    }

    /// <summary>
    /// Check if the current game mode is InfiniteStacker
    /// </summary>
    private bool IsInfiniteStackerMode()
    {
        return gameManager != null && gameManager.CurrentGameMode == GameMode.InfiniteStacker;
    }

    /// <summary>
    /// Check if the current game mode is StackerLevels
    /// </summary>
    private bool IsStackerLevelsMode()
    {
        return gameManager != null && gameManager.CurrentGameMode == GameMode.StackerLevels;
    }

    /// <summary>
    /// Convert GameTheme to TimeOfDay
    /// </summary>
    private TimeOfDay GameThemeToTimeOfDay(GameTheme theme)
    {
        switch (theme)
        {
            case GameTheme.Day:
                return TimeOfDay.Morning;
            case GameTheme.Sunset:
                return TimeOfDay.Sunset;
            case GameTheme.Night:
                return TimeOfDay.Night;
            default:
                return TimeOfDay.Morning;
        }
    }

    /// <summary>
    /// Apply theme from ThemeManager (for level play)
    /// </summary>
    private void ApplyThemeFromThemeManager()
    {
        if (themeManager == null) return;

        GameTheme selectedTheme = themeManager.GetSelectedTheme();
        TimeOfDay timeOfDay = GameThemeToTimeOfDay(selectedTheme);

        // Update current time of day
        currentTimeOfDay = timeOfDay;

        // Apply the style
        ApplyStyle(timeOfDay);
    }

    /// <summary>
    /// Handle theme changes from ThemeManager
    /// </summary>
    private void OnThemeChanged(GameTheme theme)
    {
        // Only apply theme changes when in StackerLevels mode
        if (IsStackerLevelsMode())
        {
            TimeOfDay timeOfDay = GameThemeToTimeOfDay(theme);
            SetTimeOfDay(timeOfDay);
        }
    }

    /// <summary>
    /// Handle game mode changes
    /// </summary>
    private void OnGameModeChanged(GameMode newMode)
    {
        if (newMode == GameMode.InfiniteStacker)
        {
            // Apply styles when switching to InfiniteStacker mode
            ApplyStyle(currentTimeOfDay);
        }
        else if (newMode == GameMode.StackerLevels)
        {
            // Apply theme from ThemeManager when switching to StackerLevels mode
            ApplyThemeFromThemeManager();
        }
        else
        {
            // Reset to default when switching away from supported modes
            ResetToDefaultStyles();
        }
    }

    /// <summary>
    /// Handle game restart - reset environment styles to morning
    /// </summary>
    private void OnGameRestart()
    {
        // Reset time of day to morning
        currentTimeOfDay = TimeOfDay.Morning;

        // Reset stack height tracking
        lastStackHeight = 0;
        if (stackManager != null)
        {
            lastStackHeight = stackManager.GetStackCount();
        }

        // Reset styles based on game mode
        if (IsInfiniteStackerMode())
        {
            // InfiniteStacker: reset to morning
            ApplyStyle(TimeOfDay.Morning);
        }
        else if (IsStackerLevelsMode())
        {
            // StackerLevels: reapply theme from ThemeManager
            ApplyThemeFromThemeManager();
        }
        else
        {
            ResetToDefaultStyles();
        }
    }

    /// <summary>
    /// Reset styles to default (used when switching away from InfiniteStacker mode)
    /// </summary>
    private void ResetToDefaultStyles()
    {
        // Reset crane sprites to morning (or original prefab sprites)
        // Note: We can't easily restore original sprites, so we'll just leave them as-is
        // The main thing is to stop applying new styles

        // Reset sky sprite to morning if sprite swapping is enabled
        if (useSkySpriteSwap && skySpriteRenderer != null && skySpriteMorning != null)
        {
            skySpriteRenderer.sprite = skySpriteMorning;
        }

        // Reset environment tints to white
        foreach (SpriteRenderer renderer in environmentSpriteRenderers)
        {
            if (renderer != null)
            {
                renderer.color = Color.white;
            }
        }

        // Reset sky color to white if not using sprite swap
        if (!useSkySpriteSwap && skySpriteRenderer != null)
        {
            skySpriteRenderer.color = Color.white;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from scene loading events
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Unregister from dependency registry
        DependencyRegistry.Unregister<StyleManager>(this);

        // Unsubscribe from events
        if (gameManager != null)
        {
            gameManager.OnGameModeChanged -= OnGameModeChanged;
            gameManager.OnGameRestart -= OnGameRestart;
        }

        if (themeManager != null)
        {
            themeManager.OnThemeChanged -= OnThemeChanged;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }
    }
}

