using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central orchestrator for "juice" on key gameplay moments: screen shake, hit-stop
/// (brief time-scale dip) and haptics. Reacts to landings (scaled by accuracy), the
/// Kukulkan perfect-hit shift, and game over.
///
/// Self-bootstraps into any scene that has a GameManager, so it needs zero scene wiring.
/// It reads effects from CameraController (shake) + HapticFeedback and is gated by
/// GameFeelSettings, so it degrades gracefully if pieces are missing.
/// </summary>
public class GameFeelManager : MonoBehaviour
{
    // ---- Tunables (per-moment effect strengths) ----
    // Landing shake trauma by accuracy tier.
    private const float GoodShake = 0.18f;
    private const float PerfectShake = 0.40f;
    private const float KukulkanShake = 0.85f;
    private const float GameOverShake = 0.70f;

    // Hit-stop: how long the game freezes and to what time-scale.
    private const float PerfectHitStopDuration = 0.06f;
    private const float KukulkanHitStopDuration = 0.12f;
    private const float HitStopTimeScale = 0.05f;

    // Accuracy thresholds (mirror GameManager / StackableObject scoring).
    private const float PerfectThreshold = 0.9f;
    private const float GoodThreshold = 0.6f;

    private static GameFeelManager instance;

    private CameraController cameraController;
    private GameManager gameManager;
    private StackManager stackManager;
    private UIManager uiManager;
    private Coroutine hitStopRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
        // Recreate for later scene loads (mode switches) too.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInstance();

    private static void EnsureInstance()
    {
        if (instance != null) return;

        // Only spin up in gameplay scenes (those with a GameManager). Using FindFirstObjectByType
        // instead of DependencyRegistry.Find avoids a spurious "not found" warning in the menu.
        if (Object.FindFirstObjectByType<GameManager>() == null) return;

        var go = new GameObject("GameFeelManager");
        instance = go.AddComponent<GameFeelManager>();
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
        // Managers register in their Awake, which has already run for this scene by now.
        cameraController = DependencyRegistry.Find<CameraController>();
        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        uiManager = DependencyRegistry.Find<UIManager>();

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }

        if (gameManager != null)
        {
            gameManager.OnPerfectHitStreak += OnPerfectHitStreak;
            gameManager.OnGameOver += OnGameOver;
        }
    }

    private void OnObjectAddedToStack(StackableObject stackableObject)
    {
        if (stackableObject == null) return;

        float accuracy = stackableObject.LandingAccuracy;

        if (accuracy >= PerfectThreshold)
        {
            ShakeCamera(PerfectShake);
            DoHitStop(PerfectHitStopDuration);
            HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
        }
        else if (accuracy >= GoodThreshold)
        {
            ShakeCamera(GoodShake);
            HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
        }
        // Poor landings intentionally get no juice - keeps the reward legible.
    }

    private void OnPerfectHitStreak()
    {
        // The signature moment - lean into the spectacle.
        ShakeCamera(KukulkanShake);
        DoHitStop(KukulkanHitStopDuration);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Heavy);
    }

    private void OnGameOver()
    {
        ShakeCamera(GameOverShake);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Heavy);
    }

    private void ShakeCamera(float trauma)
    {
        if (cameraController == null)
        {
            cameraController = DependencyRegistry.Find<CameraController>();
        }
        cameraController?.Shake(trauma);
    }

    private void DoHitStop(float duration)
    {
        // Don't freeze if the game is paused or already over.
        if (uiManager != null && uiManager.IsPaused) return;
        if (gameManager != null && gameManager.IsGameOver) return;

        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = HitStopTimeScale;
        // Realtime wait so the freeze lasts a fixed wall-clock time regardless of timeScale.
        yield return new WaitForSecondsRealtime(duration);

        // Only restore if a pause didn't take over the time-scale in the meantime.
        if (uiManager == null || !uiManager.IsPaused)
        {
            Time.timeScale = 1f;
        }
        hitStopRoutine = null;
    }

    private void OnDestroy()
    {
        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }
        if (gameManager != null)
        {
            gameManager.OnPerfectHitStreak -= OnPerfectHitStreak;
            gameManager.OnGameOver -= OnGameOver;
        }

        // Safety: never leave the game frozen if we're torn down mid hit-stop.
        if (hitStopRoutine != null && Time.timeScale < 1f)
        {
            if (uiManager == null || !uiManager.IsPaused)
            {
                Time.timeScale = 1f;
            }
        }

        if (instance == this) instance = null;
    }
}
