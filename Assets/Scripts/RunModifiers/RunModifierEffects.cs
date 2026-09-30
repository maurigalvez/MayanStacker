using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The run modifiers that act on stones in flight rather than on scoring:
///  - Feather Fall: a dropped stone falls with less gravity, and gets its weight back on landing;
///  - Hot Stone: the swinging stone drops by itself a couple of swings after it arms, with a ring
///    over it counting down (<see cref="HotStoneTimerView"/>).
///
/// (Shrinking Offerings is applied where stones are sized, in ObjectSpawner.)
///
/// Everything reads <see cref="RunModifierService"/>, which only carries modifiers while a
/// Daily ritual runs, so every other mode is untouched. Self-bootstraps into gameplay scenes.
/// </summary>
public class RunModifierEffects : MonoBehaviour
{
    private static RunModifierEffects instance;

    private GameManager gameManager;
    private ObjectSpawner objectSpawner;
    private StackManager stackManager;
    private UIManager uiManager;
    private SpawnerHolder spawnerHolder;

    // Feather Fall: stones currently falling light, and the factor each got.
    private readonly Dictionary<Rigidbody2D, float> lightStones = new Dictionary<Rigidbody2D, float>();

    // Hot Stone
    private StackableObject hotStone;
    // Only if the swing holder is missing: roughly one pass at the starting swing speed.
    private const float FallbackSecondsPerSwing = 2f;

    private float hotRemaining; // swings left on the fuse
    private float hotTotal;
    private HotStoneTimerView timerView;
    private bool warnedMissingTimer;

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
        if (Object.FindFirstObjectByType<GameManager>() == null) return;

        var go = new GameObject("RunModifierEffects");
        instance = go.AddComponent<RunModifierEffects>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        gameManager = DependencyRegistry.Find<GameManager>();
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        stackManager = DependencyRegistry.Find<StackManager>();
        uiManager = DependencyRegistry.Find<UIManager>();
        spawnerHolder = DependencyRegistry.Find<SpawnerHolder>();

        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned += OnStoneSpawned;
            objectSpawner.OnObjectDropped += OnStoneDropped;
        }
        if (stackManager != null) stackManager.OnObjectAddedToStack += OnStoneLanded;
        if (gameManager != null)
        {
            gameManager.OnGameStart += ResetRun;
            gameManager.OnGameRestart += ResetRun;
            gameManager.OnGameOver += ResetRun;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned -= OnStoneSpawned;
            objectSpawner.OnObjectDropped -= OnStoneDropped;
        }
        if (stackManager != null) stackManager.OnObjectAddedToStack -= OnStoneLanded;
        if (gameManager != null)
        {
            gameManager.OnGameStart -= ResetRun;
            gameManager.OnGameRestart -= ResetRun;
            gameManager.OnGameOver -= ResetRun;
        }
    }

    private void ResetRun()
    {
        hotStone = null;
        lightStones.Clear();
        if (timerView != null) timerView.Hide();
    }

    // ---- Feather Fall ----

    private void OnStoneDropped(GameObject go)
    {
        if (hotStone != null && go != null && go == hotStone.gameObject) hotStone = null;

        float factor = RunModifierService.GravityScale;
        if (go == null || Mathf.Approximately(factor, 1f)) return;

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null || lightStones.ContainsKey(rb)) return;

        // Multiplied in and divided back out, so it stacks with the Quetzal Feather either way round.
        rb.gravityScale *= factor;
        lightStones[rb] = factor;
    }

    private void OnStoneLanded(StackableObject stone)
    {
        if (stone == null) return;
        var rb = stone.GetComponent<Rigidbody2D>();
        if (rb == null || !lightStones.TryGetValue(rb, out float factor)) return;

        // Landed: the stone settles with its full weight, so the tower isn't floaty.
        rb.gravityScale /= factor;
        lightStones.Remove(rb);
    }

    // ---- Hot Stone ----

    private void OnStoneSpawned(GameObject go)
    {
        float swings = RunModifierService.AutoDropSwings;
        if (swings <= 0f || go == null)
        {
            hotStone = null;
            return;
        }

        hotStone = go.GetComponent<StackableObject>();
        hotTotal = swings;
        hotRemaining = swings;
        EnsureTimerView();
    }

    private void Update()
    {
        if (hotStone == null || objectSpawner == null)
        {
            if (timerView != null) timerView.Hide();
            return;
        }

        bool live = gameManager != null && gameManager.IsGameActive && !gameManager.IsGameOver
                    && objectSpawner.CurrentObject == hotStone.gameObject && !hotStone.IsDropped;
        if (!live)
        {
            hotStone = null;
            if (timerView != null) timerView.Hide();
            return;
        }

        // The clock only runs while the player could act: armed, unpaused, not held by a
        // rule (Cabracán's brace) or a power (the Rewind's fly-back). Scaled time, so pause,
        // hit-stop and Kukulkan's slow-mo hold it too.
        bool paused = uiManager != null && uiManager.IsPaused;
        bool ticking = objectSpawner.IsCurrentObjectArmed && !paused && Time.timeScale > 0f
                       && !InputManager.DropsBlockedByRule;

        // The fuse burns in swings, not seconds, so every stone gets the same number of passes
        // over the centre whether the swing is slow (the start) or fast. Same rate the holder
        // advances its swing by, so recoil phase kicks don't burn the fuse.
        float swingsPerSecond = spawnerHolder != null ? spawnerHolder.SwingRate / Mathf.PI : 0f;
        if (ticking)
        {
            hotRemaining -= swingsPerSecond > 0f
                ? swingsPerSecond * Time.deltaTime
                : Time.deltaTime / FallbackSecondsPerSwing;
        }

        float left = Mathf.Max(0f, hotRemaining);
        float secondsLeft = left / (swingsPerSecond > 0f ? swingsPerSecond : 1f / FallbackSecondsPerSwing);
        if (timerView != null) timerView.Track(hotStone.transform, Mathf.Clamp01(hotRemaining / hotTotal), secondsLeft, left);

        if (hotRemaining > 0f || !ticking) return;

        // Time's up: the stone drops wherever the swing has it. DropCurrentObject still
        // applies every other gate, so a refused drop simply retries next frame.
        objectSpawner.DropCurrentObject();
        if (hotStone != null && hotStone.IsDropped)
        {
            GameAnalytics.Track("daily_hot_stone_autodrop", new Dictionary<string, object>
            {
                { "height", stackManager != null ? stackManager.GetStackCount() : 0 }
            });
        }
    }

    /// <summary>
    /// Loads the authored timer once. Without it Hot Stone still drops the stone on time;
    /// it just has no countdown on screen, so say where to make it.
    /// </summary>
    private void EnsureTimerView()
    {
        if (timerView != null) return;

        var prefab = Resources.Load<GameObject>(HotStoneTimerView.PrefabResourcePath);
        if (prefab == null)
        {
            if (!warnedMissingTimer)
            {
                warnedMissingTimer = true;
                Debug.LogWarning($"[RunModifier] Resources/{HotStoneTimerView.PrefabResourcePath}.prefab is missing, so Hot Stone " +
                                 "drops without a countdown. Create it with TamalStacker ▸ Daily Challenge ▸ Create Hot Stone Timer Prefab.");
            }
            return;
        }

        GameObject go = Instantiate(prefab, transform, false);
        timerView = go.GetComponent<HotStoneTimerView>();
        if (timerView != null && timerView.IsUsable)
        {
            timerView.Hide();
            return;
        }

        Debug.LogWarning($"[RunModifier] Resources/{HotStoneTimerView.PrefabResourcePath} has no usable HotStoneTimerView " +
                         "(needs Canvas, Timer and Ring).");
        go.SetActive(false);
        Destroy(go);
        timerView = null;
        warnedMissingTimer = true;
    }
}
