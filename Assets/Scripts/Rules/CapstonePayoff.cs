using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The capstone: a short beat when the final stone of a temple lands, so finishing reads as
/// finishing — the payoff shot of a clip.
///
/// The stack locks (so nothing can wobble off during the beat), the camera punches in, a gold
/// flash lands, and the temple's silhouette fades in over the tower. Then the result panel,
/// which UIManager holds back by <see cref="HoldSecondsFor"/>.
///
/// Presentation only, and it starts after LevelManager has already saved stars and progress,
/// unlocked the codex and marked the first temple complete — none of that waits on this.
///
/// Opt-in per level (<see cref="LevelData.capstonePayoff"/>); self-bootstraps into gameplay
/// scenes like the other run systems.
/// </summary>
public class CapstonePayoff : MonoBehaviour
{
    /// <summary>Realtime length of the beat before the result panel.</summary>
    public const float HoldSeconds = 1.8f;

    private const float FadeInSeconds = 0.7f;
    private const float SilhouetteAlpha = 0.9f;
    private const int SilhouetteSortingOrder = 20; // above the stones (4-5)

    private static readonly Color FlashColor = new Color(1f, 0.85f, 0.45f, 0.6f);
    private static readonly Color SilhouetteTint = new Color(1f, 0.86f, 0.5f, 1f);

    private static CapstonePayoff instance;

    private GameManager gameManager;
    private LevelManager levelManager;
    private StackManager stackManager;
    private CameraController cameraController;

    private SpriteRenderer silhouette;
    private Coroutine routine;

    /// <summary>How long the result UI should wait for <paramref name="level"/>'s capstone. 0 = none.</summary>
    public static float HoldSecondsFor(LevelData level)
    {
        return level != null && level.capstonePayoff ? HoldSeconds : 0f;
    }

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
        if (Object.FindFirstObjectByType<LevelManager>() == null) return;

        var go = new GameObject("CapstonePayoff");
        instance = go.AddComponent<CapstonePayoff>();
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
        levelManager = DependencyRegistry.Find<LevelManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        cameraController = DependencyRegistry.Find<CameraController>();

        if (levelManager != null) levelManager.OnLevelCompleted += OnLevelCompleted;

        if (gameManager != null)
        {
            gameManager.OnGameStart += Clear;
            gameManager.OnGameRestart += Clear;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        if (levelManager != null) levelManager.OnLevelCompleted -= OnLevelCompleted;

        if (gameManager != null)
        {
            gameManager.OnGameStart -= Clear;
            gameManager.OnGameRestart -= Clear;
        }
    }

    private void OnLevelCompleted(int stars, int score, bool showCodexPopup)
    {
        LevelData level = levelManager != null ? levelManager.CurrentLevel : null;
        if (HoldSecondsFor(level) <= 0f) return;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Play(level));
    }

    private IEnumerator Play(LevelData level)
    {
        List<StackableObject> stones = stackManager != null
            ? stackManager.GetStackObjects()
            : new List<StackableObject>();

        LockStack(stones);

        GameFeelManager.Flash(FlashColor, 0.6f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Heavy);
        if (cameraController != null)
        {
            cameraController.Shake(0.35f);
            cameraController.PunchZoom(0.1f, 0.9f);
        }

        GameAnalytics.Track("capstone_shown", new Dictionary<string, object>
        {
            { "level", level.levelNumber },
            { "silhouette", level.capstoneSilhouette != null }
        });

        // No art yet: the flash is the whole payoff.
        if (level.capstoneSilhouette == null || !TryGetTowerBounds(stones, out Bounds tower))
        {
            routine = null;
            yield break;
        }

        silhouette = BuildSilhouette(level.capstoneSilhouette, tower);

        float t = 0f;
        while (t < FadeInSeconds && silhouette != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FadeInSeconds);
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);

            Color c = SilhouetteTint;
            c.a = SilhouetteAlpha * eased;
            silhouette.color = c;
            silhouette.transform.localScale = baseScale * Mathf.Lerp(1.08f, 1f, eased);
            yield return null;
        }

        routine = null;
    }

    /// <summary>
    /// Freezes every stone where it stands. The level is already won; the beat must not end
    /// with the tower sliding apart under the silhouette.
    /// </summary>
    private static void LockStack(List<StackableObject> stones)
    {
        foreach (StackableObject stone in stones)
        {
            if (stone == null) continue;
            Rigidbody2D rb = stone.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private static bool TryGetTowerBounds(List<StackableObject> stones, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (StackableObject stone in stones)
        {
            if (stone == null || stone.SpriteRenderer == null) continue;

            if (!any)
            {
                bounds = stone.SpriteRenderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(stone.SpriteRenderer.bounds);
            }
        }

        return any;
    }

    private Vector3 baseScale = Vector3.one;

    /// <summary>
    /// A world-space sprite standing on the tower's base, as tall as the tower and as wide as
    /// its aspect ratio says, so the tower visibly becomes the temple.
    /// </summary>
    private SpriteRenderer BuildSilhouette(Sprite sprite, Bounds tower)
    {
        // Not Clear(): that would stop the coroutine building this.
        DestroySilhouette();

        var go = new GameObject("CapstoneSilhouette");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = SilhouetteSortingOrder;

        Color c = SilhouetteTint;
        c.a = 0f;
        sr.color = c;

        Vector2 spriteSize = sprite.bounds.size;
        float scale = spriteSize.y > 0f ? tower.size.y * 1.05f / spriteSize.y : 1f;
        baseScale = Vector3.one * scale;
        go.transform.localScale = baseScale;

        // Bottom of the sprite on the bottom of the tower, whatever the sprite's pivot.
        float pivotToBottom = (sprite.bounds.center.y - sprite.bounds.extents.y) * scale;
        go.transform.position = new Vector3(tower.center.x, tower.min.y - pivotToBottom, 0f);

        return sr;
    }

    private void Clear()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        DestroySilhouette();
    }

    private void DestroySilhouette()
    {
        if (silhouette != null)
        {
            Destroy(silhouette.gameObject);
            silhouette = null;
        }
    }
}
