using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the run's altitude bands: watches the stack grow, and when it crosses a
/// threshold switches the scene's time of day, opens up the special-block mix and
/// announces the new band by name.
///
/// Self-bootstraps into any scene with a GameManager, the same way GameFeelManager does,
/// so it needs no scene wiring and no changes to any existing prefab. Announcements go
/// through <see cref="RunBanner"/>, which draws on its own non-interactive overlay canvas
/// rather than claiming a piece of the authored HUD.
///
/// With no AltitudeBandSet asset in Resources the whole system is inert.
/// </summary>
public class AltitudeBandManager : MonoBehaviour
{
    private static AltitudeBandManager instance;

    /// <summary>
    /// How much the current band scales special-block frequency. Read by ObjectSpawner.
    /// Static so the spawner needn't know whether bands are running at all: with no
    /// manager and no band set this stays 1 and the block mix is untouched.
    /// </summary>
    public static float SpecialBlockChanceMultiplier { get; private set; } = 1f;

    /// <summary>Index of the band the player is currently in, or -1 before a run starts.</summary>
    public static int CurrentBandIndex { get; private set; } = -1;

    /// <summary>Localization key of the current band's name, or empty.</summary>
    public static string CurrentBandNameKey { get; private set; } = "";

    private AltitudeBandSet bandSet;
    private GameManager gameManager;
    private StackManager stackManager;
    private StyleManager styleManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInstance();

    private static void EnsureInstance()
    {
        if (instance != null) return;

        // Gameplay scenes only. FindFirstObjectByType rather than DependencyRegistry so the
        // main menu doesn't log a spurious "not found" warning.
        if (Object.FindFirstObjectByType<GameManager>() == null) return;

        var go = new GameObject("AltitudeBandManager");
        instance = go.AddComponent<AltitudeBandManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        bandSet = Resources.Load<AltitudeBandSet>(AltitudeBandSet.ResourcePath);
    }

    private void Start()
    {
        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        styleManager = DependencyRegistry.Find<StyleManager>();

        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameRestart += OnRunEnded;
            gameManager.OnGameOver += OnRunEnded;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }
    }

    private void OnGameStart()
    {
        ResetToFirstBand();
    }

    private void OnRunEnded()
    {
        // The visuals are deliberately left alone: the game-over screen sits over the
        // scene the player actually died in, which reads better than snapping back to
        // daylight. Only the block-mix multiplier resets, so the next run starts fair.
        SpecialBlockChanceMultiplier = 1f;
    }

    private void ResetToFirstBand()
    {
        SpecialBlockChanceMultiplier = 1f;
        CurrentBandIndex = -1;
        CurrentBandNameKey = "";

        if (!BandsActive()) return;

        // Apply band 0 silently so every run starts in a known state.
        ApplyBand(0, announce: false);
    }

    private void OnObjectAddedToStack(StackableObject stackableObject)
    {
        if (!BandsActive()) return;
        if (stackManager == null) return;

        int height = stackManager.GetStackCount();
        int index = bandSet.IndexForHeight(height);

        if (index == CurrentBandIndex) return;

        // Only announce moving up. A stack that loses blocks shouldn't replay a milestone
        // the player already passed.
        bool movingUp = index > CurrentBandIndex;
        ApplyBand(index, announce: movingUp);
    }

    private void ApplyBand(int index, bool announce)
    {
        AltitudeBand band = bandSet.At(index);
        if (band == null) return;

        CurrentBandIndex = index;
        CurrentBandNameKey = band.nameKey;
        SpecialBlockChanceMultiplier = Mathf.Max(0f, band.specialBlockChanceMultiplier);

        if (band.applyTimeOfDay)
        {
            if (styleManager == null) styleManager = DependencyRegistry.Find<StyleManager>();

            // NOTE: StyleManager has its own autoCycleByHeight option. Leave that OFF while
            // bands are in use, or the two will fight over the time of day.
            if (styleManager != null)
            {
                styleManager.SetTimeOfDay(band.timeOfDay);
            }
        }

        if (announce && band.announce && !string.IsNullOrEmpty(band.nameKey))
        {
            RunBanner.Show(LocalizationManager.Get(band.nameKey), band.announceColor);

            GameAnalytics.Track("altitude_band_reached", new System.Collections.Generic.Dictionary<string, object>
            {
                { "band", band.nameKey },
                { "band_index", index },
                { "height", stackManager != null ? stackManager.GetStackCount() : 0 }
            });
        }
    }

    private bool BandsActive()
    {
        if (bandSet == null || gameManager == null) return false;
        return bandSet.AppliesTo(gameManager.CurrentGameMode);
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStart -= OnGameStart;
            gameManager.OnGameRestart -= OnRunEnded;
            gameManager.OnGameOver -= OnRunEnded;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }

        if (instance == this)
        {
            instance = null;
            SpecialBlockChanceMultiplier = 1f;
            CurrentBandIndex = -1;
            CurrentBandNameKey = "";
        }
    }
}
