using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

/// <summary>
/// Serializable class that pairs a SpriteRenderer with its sprite name for atlas lookup
/// </summary>
[System.Serializable]
public class EnvironmentSpriteRendererEntry
{
    [Tooltip("The SpriteRenderer to apply sprites to")]
    public SpriteRenderer renderer;

    [Tooltip("The sprite name to look up in the sprite atlas (should match across all atlases)")]
    public string spriteName;
}

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

    [Header("Last Block Sprites (Level Mode)")]
    [Tooltip("Sprite to use for the last block in level mode - Morning/Day theme")]
    [SerializeField] private Sprite lastBlockSpriteMorning;
    [Tooltip("Sprite to use for the last block in level mode - Sunset theme")]
    [SerializeField] private Sprite lastBlockSpriteSunset;
    [Tooltip("Sprite to use for the last block in level mode - Night theme")]
    [SerializeField] private Sprite lastBlockSpriteNight;

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
    [Tooltip("References to environment sprite renderers with their sprite names - assign in Inspector")]
    [SerializeField] private EnvironmentSpriteRendererEntry[] environmentSpriteRenderersArray;

    [Header("Spawned Environment Color Tints")]
    [Tooltip("Color tints for assets spawned by EnvironmentSpawner - separate from static environment tints")]
    [SerializeField] private Color morningTintSpawned = Color.white;
    [SerializeField] private Color sunsetTintSpawned = new Color(1f, 0.7f, 0.5f, 1f); // Warm orange
    [SerializeField] private Color nightTintSpawned = new Color(0.3f, 0.3f, 0.5f, 1f); // Cool blue

    [Header("Environment Sprite Atlases")]
    [Tooltip("SpriteAtlas for Morning/Day theme - sprite names should match across all atlases")]
    [SerializeField] private SpriteAtlas environmentAtlasMorning;
    [Tooltip("SpriteAtlas for Sunset theme - sprite names should match across all atlases")]
    [SerializeField] private SpriteAtlas environmentAtlasSunset;
    [Tooltip("SpriteAtlas for Night theme - sprite names should match across all atlases")]
    [SerializeField] private SpriteAtlas environmentAtlasNight;
    [Tooltip("If true, applies sprites from atlases. If false, only applies color tint")]
    [SerializeField] private bool useEnvironmentSpriteAtlas = true;

    // References
    private ObjectSpawner objectSpawner;
    private StackManager stackManager;
    private GameManager gameManager;
    private ThemeManager themeManager;
    private List<SpriteRenderer> environmentSpriteRenderers = new List<SpriteRenderer>();
    private Dictionary<SpriteRenderer, string> environmentSpriteNames = new Dictionary<SpriteRenderer, string>();
    private HashSet<SpriteRenderer> spawnedEnvironmentRenderers = new HashSet<SpriteRenderer>();
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
        environmentSpriteNames.Clear();

        if (environmentSpriteRenderersArray != null)
        {
            foreach (EnvironmentSpriteRendererEntry entry in environmentSpriteRenderersArray)
            {
                if (entry != null && entry.renderer != null)
                {
                    if (!environmentSpriteRenderers.Contains(entry.renderer))
                    {
                        environmentSpriteRenderers.Add(entry.renderer);
                    }

                    // Store sprite name if provided
                    if (!string.IsNullOrEmpty(entry.spriteName))
                    {
                        environmentSpriteNames[entry.renderer] = entry.spriteName;
                    }
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
    /// Get last block sprite for the specified time of day
    /// </summary>
    public Sprite GetLastBlockSpriteForTimeOfDay(TimeOfDay timeOfDay)
    {
        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                return lastBlockSpriteMorning;
            case TimeOfDay.Sunset:
                return lastBlockSpriteSunset;
            case TimeOfDay.Night:
                return lastBlockSpriteNight;
            default:
                return lastBlockSpriteMorning;
        }
    }

    /// <summary>
    /// Get current last block sprite (for level mode)
    /// </summary>
    public Sprite GetCurrentLastBlockSprite()
    {
        // Only return sprite if in StackerLevels mode
        if (!IsStackerLevelsMode())
        {
            return null;
        }

        return GetLastBlockSpriteForTimeOfDay(currentTimeOfDay);
    }

    /// <summary>
    /// Apply environment color tint and sprites based on time of day
    /// </summary>
    private void ApplyEnvironmentTint(TimeOfDay timeOfDay)
    {
        SpriteAtlas targetAtlas = GetEnvironmentAtlasForTimeOfDay(timeOfDay);

        // Clean up null references before applying tints
        CleanupEnvironmentSpriteRenderers();

        // Update existing environment sprite renderers
        foreach (SpriteRenderer renderer in environmentSpriteRenderers)
        {
            if (renderer != null)
            {
                // Apply sprite from atlas if enabled and atlas is available
                if (useEnvironmentSpriteAtlas && targetAtlas != null)
                {
                    ApplySpriteFromAtlas(renderer, targetAtlas);
                }

                // Apply appropriate color tint (different for spawned vs static)
                Color targetTint = IsSpawnedRenderer(renderer)
                    ? GetSpawnedEnvironmentTintForTimeOfDay(timeOfDay)
                    : GetEnvironmentTintForTimeOfDay(timeOfDay);
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
    /// Get spawned environment color tint for the specified time of day
    /// </summary>
    private Color GetSpawnedEnvironmentTintForTimeOfDay(TimeOfDay timeOfDay)
    {
        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                return morningTintSpawned;
            case TimeOfDay.Sunset:
                return sunsetTintSpawned;
            case TimeOfDay.Night:
                return nightTintSpawned;
            default:
                return morningTintSpawned;
        }
    }

    /// <summary>
    /// Check if a renderer is from a spawned environment asset
    /// </summary>
    private bool IsSpawnedRenderer(SpriteRenderer renderer)
    {
        return renderer != null && spawnedEnvironmentRenderers.Contains(renderer);
    }

    /// <summary>
    /// Get environment SpriteAtlas for the specified time of day
    /// </summary>
    private SpriteAtlas GetEnvironmentAtlasForTimeOfDay(TimeOfDay timeOfDay)
    {
        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                return environmentAtlasMorning;
            case TimeOfDay.Sunset:
                return environmentAtlasSunset;
            case TimeOfDay.Night:
                return environmentAtlasNight;
            default:
                return environmentAtlasMorning;
        }
    }

    /// <summary>
    /// Apply sprite from atlas to a renderer based on stored sprite name
    /// </summary>
    private void ApplySpriteFromAtlas(SpriteRenderer renderer, SpriteAtlas atlas)
    {
        if (renderer == null || atlas == null) return;

        // Get the sprite name for this renderer from dictionary (set via inspector or registration)
        if (!environmentSpriteNames.TryGetValue(renderer, out string spriteName) || string.IsNullOrEmpty(spriteName))
        {
            // No sprite name available, skip sprite assignment
            return;
        }

        // Try to get sprite from atlas
        Sprite newSprite = atlas.GetSprite(spriteName);
        if (newSprite != null)
        {
            renderer.sprite = newSprite;
        }
        else
        {
            Debug.LogWarning($"StyleManager: Sprite '{spriteName}' not found in atlas '{atlas.name}'. Keeping current sprite.");
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
        // Remove null renderers from list
        environmentSpriteRenderers.RemoveAll(renderer => renderer == null);

        // Remove null renderers from sprite names dictionary
        List<SpriteRenderer> keysToRemove = new List<SpriteRenderer>();
        foreach (var kvp in environmentSpriteNames)
        {
            if (kvp.Key == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
        {
            environmentSpriteNames.Remove(key);
        }

        // Remove null renderers from spawned renderers set
        spawnedEnvironmentRenderers.RemoveWhere(renderer => renderer == null);
    }

    /// <summary>
    /// Register sprite renderers from an environment GameObject
    /// Automatically finds all SpriteRenderers in the GameObject and its children
    /// </summary>
    /// <param name="environmentObject">The GameObject containing environment sprite renderers</param>
    public void RegisterEnvironmentSpriteRenderers(GameObject environmentObject)
    {
        RegisterEnvironmentSpriteRenderers(environmentObject, false);
    }

    /// <summary>
    /// Register sprite renderers from an environment GameObject
    /// Automatically finds all SpriteRenderers in the GameObject and its children
    /// </summary>
    /// <param name="environmentObject">The GameObject containing environment sprite renderers</param>
    /// <param name="isSpawned">If true, marks these renderers as spawned assets (will use spawned tints)</param>
    public void RegisterEnvironmentSpriteRenderers(GameObject environmentObject, bool isSpawned)
    {
        if (environmentObject == null) return;

        SpriteRenderer[] renderers = environmentObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            RegisterEnvironmentSpriteRenderer(renderer, null, isSpawned);
        }
    }

    /// <summary>
    /// Register a single environment sprite renderer
    /// </summary>
    /// <param name="renderer">The SpriteRenderer to register</param>
    public void RegisterEnvironmentSpriteRenderer(SpriteRenderer renderer)
    {
        RegisterEnvironmentSpriteRenderer(renderer, null, false);
    }

    /// <summary>
    /// Register a single environment sprite renderer with an explicit sprite name
    /// </summary>
    /// <param name="renderer">The SpriteRenderer to register</param>
    /// <param name="spriteName">The sprite name to use for atlas lookup (if null, will use sprite's current name)</param>
    public void RegisterEnvironmentSpriteRenderer(SpriteRenderer renderer, string spriteName)
    {
        RegisterEnvironmentSpriteRenderer(renderer, spriteName, false);
    }

    /// <summary>
    /// Register a single environment sprite renderer with an explicit sprite name and spawn status
    /// </summary>
    /// <param name="renderer">The SpriteRenderer to register</param>
    /// <param name="spriteName">The sprite name to use for atlas lookup (if null, will use sprite's current name)</param>
    /// <param name="isSpawned">If true, marks this renderer as a spawned asset (will use spawned tints)</param>
    public void RegisterEnvironmentSpriteRenderer(SpriteRenderer renderer, string spriteName, bool isSpawned)
    {
        if (renderer == null) return;

        // Add to list if not already there
        if (!environmentSpriteRenderers.Contains(renderer))
        {
            environmentSpriteRenderers.Add(renderer);
        }

        // Mark as spawned if applicable
        if (isSpawned)
        {
            spawnedEnvironmentRenderers.Add(renderer);
        }

        // Store sprite name for atlas lookup
        if (!string.IsNullOrEmpty(spriteName))
        {
            // Use explicitly provided sprite name
            environmentSpriteNames[renderer] = spriteName;
        }
        else if (renderer.sprite != null && !string.IsNullOrEmpty(renderer.sprite.name))
        {
            // Fallback: use current sprite name and store it for future use
            string currentSpriteName = renderer.sprite.name;
            // Remove "(Clone)" suffix if present (Unity adds this when instantiating)
            if (currentSpriteName.EndsWith("(Clone)"))
            {
                currentSpriteName = currentSpriteName.Substring(0, currentSpriteName.Length - 7).Trim();
            }
            environmentSpriteNames[renderer] = currentSpriteName;
        }

        // Apply current style if in InfiniteStacker mode or StackerLevels mode
        if (IsInfiniteStackerMode() || IsStackerLevelsMode())
        {
            Color targetTint = isSpawned
                ? GetSpawnedEnvironmentTintForTimeOfDay(currentTimeOfDay)
                : GetEnvironmentTintForTimeOfDay(currentTimeOfDay);
            SpriteAtlas targetAtlas = GetEnvironmentAtlasForTimeOfDay(currentTimeOfDay);

            // Apply sprite from atlas if enabled
            if (useEnvironmentSpriteAtlas && targetAtlas != null)
            {
                ApplySpriteFromAtlas(renderer, targetAtlas);
            }

            // Apply color tint
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
    /// Check if the current game mode is InfiniteStacker.
    /// DailyChallenge intentionally piggy-backs on this so it shares
    /// InfiniteStacker's height-based time-of-day cycling and style application.
    /// </summary>
    private bool IsInfiniteStackerMode()
    {
        return gameManager != null
            && (gameManager.CurrentGameMode == GameMode.InfiniteStacker
                || gameManager.CurrentGameMode == GameMode.DailyChallenge);
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
        if (newMode == GameMode.InfiniteStacker || newMode == GameMode.DailyChallenge)
        {
            // Apply styles when switching to InfiniteStacker / DailyChallenge mode
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

        // Reset environment tints to white and sprites if atlases are enabled
        SpriteAtlas defaultAtlas = environmentAtlasMorning;
        foreach (SpriteRenderer renderer in environmentSpriteRenderers)
        {
            if (renderer != null)
            {
                if (useEnvironmentSpriteAtlas && defaultAtlas != null)
                {
                    ApplySpriteFromAtlas(renderer, defaultAtlas);
                }
                // Reset to white (both spawned and static use white when resetting)
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

