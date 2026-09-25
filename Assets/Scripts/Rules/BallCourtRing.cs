using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Temple rule <see cref="LevelRule.BallCourtRing"/>: a stone hoop, like the rings set into a
/// Mayan ball-court wall, drifts side to side above the tower on some drops. A stone whose
/// fits through the opening scores a bonus with a burst of feedback. Missing it
/// costs nothing — an optional goal, not added difficulty — and a viewer gets it instantly.
///
/// The ring has no collider at all: it's two sprites (the back half of the hoop behind the
/// stones, the front half in front, so a stone visibly passes *through*) and the pass is a
/// crossing test on the stone's centre. A stone can never clip the rim and get knocked away.
///
/// The drift is a plain sine with no randomness, so every attempt at the temple, and every
/// player, faces the same ring.
///
/// Self-bootstraps into gameplay scenes; inert unless the current level sets the rule.
/// </summary>
public class BallCourtRing : MonoBehaviour
{
    private const int BackSortingOrder = 3;   // behind the stones (4-5)
    private const int FrontSortingOrder = 10; // in front of them
    private const float InnerFraction = 0.86f; // hole diameter / ring diameter in the art
    private const float EllipseSquash = 0.22f;  // how flat the hoop looks from the side

    private static readonly Color RingColor = new Color(0.78f, 0.70f, 0.52f, 1f);
    private static readonly Color RingBackColor = new Color(0.52f, 0.46f, 0.34f, 1f);

    private static BallCourtRing instance;
    private static Sprite backSprite;
    private static Sprite frontSprite;

    /// <summary>Stones dropped through the ring this attempt.</summary>
    public static int PassedThisRun { get; private set; }

    private GameManager gameManager;
    private LevelManager levelManager;
    private StackManager stackManager;
    private ObjectSpawner objectSpawner;
    private CameraController cameraController;
    private GameSoundManager soundManager;

    private BallCourtRingSettings settings;
    private bool ruleActive;

    private Transform ringRoot;
    private SpriteRenderer back;
    private SpriteRenderer front;

    private StackableObject target;   // the stone this ring was raised for
    private bool resolved;
    private float ringY;
    private float previousStoneY;
    private float driftStartTime;
    private float holeWidth;          // stone width + clearance, fixed when the ring is raised
    private float fade;               // 0..1 visibility
    private float burst;              // 0..1 pass burst, decays
    private bool missed;
    private bool hintShownThisRun;

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

        var go = new GameObject("BallCourtRing");
        instance = go.AddComponent<BallCourtRing>();
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
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        cameraController = DependencyRegistry.Find<CameraController>();
        soundManager = DependencyRegistry.Find<GameSoundManager>();

        if (gameManager != null)
        {
            gameManager.OnGameStart += ResetRun;
            gameManager.OnGameRestart += ResetRun;
            gameManager.OnGameOver += Dismiss;
        }

        if (objectSpawner != null) objectSpawner.OnObjectSpawned += OnObjectSpawned;
        if (levelManager != null) levelManager.OnLevelCompleted += OnLevelCompleted;

        BuildRing();
        ResetRun();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        if (gameManager != null)
        {
            gameManager.OnGameStart -= ResetRun;
            gameManager.OnGameRestart -= ResetRun;
            gameManager.OnGameOver -= Dismiss;
        }

        if (objectSpawner != null) objectSpawner.OnObjectSpawned -= OnObjectSpawned;
        if (levelManager != null) levelManager.OnLevelCompleted -= OnLevelCompleted;
    }

    private void ResetRun()
    {
        PassedThisRun = 0;
        hintShownThisRun = false;

        LevelData level = levelManager != null ? levelManager.CurrentLevel : null;
        ruleActive = gameManager != null
                     && gameManager.CurrentGameMode == GameMode.StackerLevels
                     && level != null
                     && level.HasRule(LevelRule.BallCourtRing);
        settings = ruleActive ? level.ringSettings : null;

        target = null;
        resolved = true;
        fade = 0f;
        burst = 0f;
        if (ringRoot != null) ringRoot.gameObject.SetActive(false);
    }

    private void OnLevelCompleted(int stars, int score, bool showCodexPopup) => Dismiss();

    private void Dismiss()
    {
        target = null;
        resolved = true;
    }

    // ---- Raising a ring ----

    private void OnObjectSpawned(GameObject spawned)
    {
        if (!ruleActive || settings == null || spawned == null) return;
        if (gameManager == null || !gameManager.IsGameActive || gameManager.IsGameOver) return;

        int height = stackManager != null ? stackManager.GetStackCount() : 0;
        if (!ShouldRaiseAt(height)) return;

        var stone = spawned.GetComponent<StackableObject>();
        if (stone == null || stone.Collider == null) return;

        target = stone;
        resolved = false;
        missed = false;
        holeWidth = stone.Collider.bounds.size.x + settings.clearance;
        burst = 0f;
        fade = 0f;
        driftStartTime = Time.time;
        ringY = TargetRingY();
        previousStoneY = stone.Collider.bounds.center.y;

        ringRoot.localScale = Vector3.one;
        ringRoot.gameObject.SetActive(true);
        PlaceRing(0f);

        // The first ring of an attempt names the goal once; after that it speaks for itself.
        if (!hintShownThisRun)
        {
            hintShownThisRun = true;
            RunBanner.Show(LocalizationManager.Get("ring_hint"), RunOverlayUI.Gold, 1.8f, -180f);
        }
    }

    /// <summary>Every Nth stone from <c>firstAtHeight</c>, never the temple's final stone.</summary>
    private bool ShouldRaiseAt(int height)
    {
        if (height < settings.firstAtHeight) return false;
        if ((height - settings.firstAtHeight) % Mathf.Max(1, settings.everyNthStone) != 0) return false;

        // The stone about to drop is number height + 1; the last one belongs to the capstone.
        LevelData level = levelManager.CurrentLevel;
        return level == null || height + 1 < level.requiredStackHeight;
    }

    // ---- Per frame ----

    private void Update()
    {
        if (ringRoot == null || !ringRoot.gameObject.activeSelf) return;

        bool showing = !resolved && target != null;
        // A miss snaps away, so a stone that clipped the rim never looks like it went through.
        float fadeSpeed = showing ? 4f : (missed ? 12f : 2.5f);
        fade = Mathf.MoveTowards(fade, showing ? 1f : 0f, Time.deltaTime * fadeSpeed);
        burst = Mathf.MoveTowards(burst, 0f, Time.unscaledDeltaTime * 1.8f);

        if (showing)
        {
            // Until the stone is released the tower top and the stone can still move (the
            // camera and spawner follow the stack), so the ring keeps its place between them.
            if (!target.IsDropped) ringY = Mathf.Lerp(ringY, TargetRingY(), 1f - Mathf.Exp(-10f * Time.deltaTime));
            else CheckCrossing();
        }

        PlaceRing(fade);

        if (fade <= 0f && burst <= 0f) ringRoot.gameObject.SetActive(false);
    }

    private void CheckCrossing()
    {
        if (target.Collider == null)
        {
            resolved = true;
            return;
        }

        Vector3 centre = target.Collider.bounds.center;

        // A stone that has already landed, or somehow fell past, is a miss.
        bool crossed = previousStoneY > ringY && centre.y <= ringY;
        previousStoneY = centre.y;

        if (!crossed && !target.HasLanded) return;

        resolved = true;
        bool through = crossed && Mathf.Abs(centre.x - RingX()) <= settings.clearance * 0.5f;

        if (through) OnPassed();
        else OnMissed();
    }

    private void OnPassed()
    {
        PassedThisRun++;
        burst = 1f;

        // One total, no breakdown: the score counter moves and the banner names the feat.
        if (gameManager != null) gameManager.AddScore(settings.bonusPoints);

        RunBanner.Show(
            LocalizationManager.Get("ring_passed_title"),
            LocalizationManager.Get("ring_passed_count", PassedThisRun),
            RunOverlayUI.Gold,
            1.1f,
            -180f); // below centre: the swing band is up top

        if (cameraController != null) cameraController.Shake(0.22f);
        GameFeelManager.Flash(new Color(1f, 0.85f, 0.45f, 0.22f), 0.25f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
        AudioClip passSound = TempleRuleArt.Current.ringPassSound;
        if (soundManager != null)
        {
            if (passSound != null) soundManager.PlaySound(passSound, 1f);
            else soundManager.PlayComboSuccessSound();
        }

        GameAnalytics.Track("ring_passed", EventData());
    }

    private void OnMissed()
    {
        missed = true;
        GameAnalytics.Track("ring_missed", EventData());
    }

    private Dictionary<string, object> EventData()
    {
        return new Dictionary<string, object>
        {
            { "level", levelManager != null && levelManager.CurrentLevel != null ? levelManager.CurrentLevel.levelNumber : 0 },
            { "height", stackManager != null ? stackManager.GetStackCount() : 0 },
            { "passed_this_run", PassedThisRun }
        };
    }

    // ---- Placement ----

    private float TowerCentreX()
    {
        StackableObject top = stackManager != null ? stackManager.GetTopObject() : null;
        return top != null ? top.ColliderCenterX : 0f;
    }

    private float RingX()
    {
        float phase = (Time.time - driftStartTime) * settings.driftCyclesPerSecond * 2f * Mathf.PI;
        return TowerCentreX() + Mathf.Sin(phase) * settings.driftRange;
    }

    /// <summary>Between the top of the tower and the underside of the hanging stone.</summary>
    private float TargetRingY()
    {
        StackableObject top = stackManager != null ? stackManager.GetTopObject() : null;
        float towerTop = top != null && top.Collider != null
            ? top.Collider.bounds.max.y
            : (stackManager != null ? stackManager.GetStackTopY() : 0f);

        float stoneBottom = target != null && target.Collider != null
            ? target.Collider.bounds.min.y
            : towerTop + 4f;

        return Mathf.Lerp(towerTop, stoneBottom, settings.heightBetween);
    }

    private void PlaceRing(float alpha)
    {
        if (settings == null) return;

        float x = target != null ? RingX() : ringRoot.position.x;
        ringRoot.position = new Vector3(x, ringY, 0f);

        // The hole is drawn at exactly the width the crossing test accepts.
        float width = holeWidth / InnerFraction;
        float pop = 1f + 0.35f * burst;
        ringRoot.localScale = new Vector3(width * pop, width * EllipseSquash * pop, 1f);

        Color rim = missed ? Color.Lerp(RingColor, RunOverlayUI.Clay, 0.6f) : RingColor;
        Color frontColor = Color.Lerp(rim, RunOverlayUI.Gold, burst);
        frontColor.a = Mathf.Max(alpha, burst);
        front.color = frontColor;

        Color backColor = Color.Lerp(RingBackColor, RunOverlayUI.Gold, burst);
        backColor.a = Mathf.Max(alpha, burst);
        back.color = backColor;
    }

    // ---- Art ----

    private void BuildRing()
    {
        EnsureSprites();

        // Authored halves win over the code-drawn ring. They must be drawn like it: 1 world
        // unit across at scale 1, with the hole InnerFraction of the ring's width.
        TempleRuleArt art = TempleRuleArt.Current;
        Sprite backArt = art.ringBack != null ? art.ringBack : backSprite;
        Sprite frontArt = art.ringFront != null ? art.ringFront : frontSprite;

        ringRoot = new GameObject("Ring").transform;
        ringRoot.SetParent(transform, false);

        back = CreateHalf("Back", backArt, BackSortingOrder);
        front = CreateHalf("Front", frontArt, FrontSortingOrder);

        ringRoot.gameObject.SetActive(false);
    }

    private SpriteRenderer CreateHalf(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(ringRoot, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>
    /// Draws the hoop seen slightly from above: an elliptical band, split into the far half
    /// (top of the ellipse) and the near half (bottom). Both sprites are 1 world unit across
    /// at scale 1 and are sized by the transform.
    /// </summary>
    private static void EnsureSprites()
    {
        if (backSprite != null && frontSprite != null) return;

        const int w = 256;
        const int h = 256;
        float rimFraction = (1f - InnerFraction) * 0.5f;

        backSprite = Sprite.Create(DrawHalf(w, h, rimFraction, backHalf: true),
            new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        frontSprite = Sprite.Create(DrawHalf(w, h, rimFraction, backHalf: false),
            new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    private static Texture2D DrawHalf(int w, int h, float rimFraction, bool backHalf)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[w * h];
        float inner = 1f - 2f * rimFraction;
        float feather = 2f / w;

        for (int y = 0; y < h; y++)
        {
            float ny = (y + 0.5f) / h * 2f - 1f;
            bool isBack = ny >= 0f;
            if (isBack != backHalf) continue;

            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);

                float outerEdge = Mathf.Clamp01((1f - r) / feather);
                float innerEdge = Mathf.Clamp01((r - inner) / feather);
                float a = Mathf.Min(outerEdge, innerEdge);
                if (a <= 0f) continue;

                // Carved-stone shading: lighter along the outer lip.
                float lip = Mathf.InverseLerp(inner, 1f, r);
                byte shade = (byte)(200 + 55 * lip);
                pixels[y * w + x] = new Color32(shade, shade, shade, (byte)(a * 255));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
