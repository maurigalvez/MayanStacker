using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    // ---- Tunables ----
    // These are serialized so they can be tweaked in the Inspector. To edit them, add a
    // GameFeelManager component to a GameObject in the scene (otherwise the manager auto-spawns
    // at runtime using these code defaults). Field names are PascalCase to match their usages.

    [Header("Landing Shake (trauma 0-1 by accuracy tier)")]
    [SerializeField] private float GoodShake = 0.18f;
    [SerializeField] private float PerfectShake = 0.40f;
    [SerializeField] private float KukulkanShake = 0.85f;
    [SerializeField] private float GameOverShake = 0.70f;

    [Header("Hit-stop (Perfect landings)")]
    [SerializeField] private float PerfectHitStopDuration = 0.06f;
    [Range(0f, 1f)]
    [SerializeField] private float HitStopTimeScale = 0.05f;

    [Header("Kukulkan Shift Spectacle")]
    [Range(0.05f, 1f)]
    [SerializeField] private float KukulkanSlowMoScale = 0.35f;   // time-scale during the shift
    [SerializeField] private float KukulkanSlowMoHold = 0.12f;    // realtime hold before ramping back
    [SerializeField] private float KukulkanSlowMoRamp = 0.45f;    // realtime ramp back to normal speed
    [Range(0f, 0.5f)]
    [SerializeField] private float KukulkanZoomFraction = 0.12f;  // 0.12 = 12% punch-zoom in
    [SerializeField] private float KukulkanZoomDuration = 0.55f;
    [SerializeField] private Color KukulkanFlashColor = new Color(1f, 0.85f, 0.45f, 0.45f); // .a = peak alpha
    [SerializeField] private float KukulkanFlashDuration = 0.35f;

    [Header("Combo Heat (needs a vignette texture in Resources - disabled if none)")]
    [Tooltip("Resources path to the vignette sprite. Drop one at Assets/Resources/GameFeel/ComboHeatVignette.png")]
    [SerializeField] private string HeatVignetteResourcePath = "GameFeel/ComboHeatVignette";
    [SerializeField] private Color HeatColor = new Color(1f, 0.42f, 0.12f); // warm orange tint
    [Range(0f, 1f)]
    [SerializeField] private float HeatMaxAlpha = 0.32f;   // overlay alpha at max combo
    [SerializeField] private int HeatComboForMax = 6;      // combo at which heat is fully ramped
    [SerializeField] private float HeatLerpSpeed = 1.2f;   // how fast the tint eases toward its target

    [Header("Landing Guide")]
    [SerializeField] private Color GuideColor = new Color(1f, 0.9f, 0.55f, 0.28f);
    [SerializeField] private float GuideWidth = 0.06f;

    [Header("Accuracy Thresholds")]
    [Tooltip("Keep these matched to GameManager/StackableObject scoring or the feel tiers will disagree with the score/message tiers.")]
    [Range(0f, 1f)]
    [SerializeField] private float PerfectThreshold = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] private float GoodThreshold = 0.6f;

    private static GameFeelManager instance;

    private CameraController cameraController;
    private GameManager gameManager;
    private StackManager stackManager;
    private UIManager uiManager;
    private ObjectSpawner objectSpawner;
    private Coroutine hitStopRoutine;
    private Coroutine spectacleRoutine;
    private Coroutine flashRoutine;
    private bool kukulkanActive;      // suppresses the regular landing hit-stop during the shift

    // Overlay visuals (self-created, no scene wiring).
    private Canvas overlayCanvas;     // shared screen-space overlay for flash + heat
    private Image flashImage;         // gold Kukulkan flash
    private Image heatImage;          // combo warmth overlay (only if a texture is provided)
    private Sprite heatSprite;        // user-supplied vignette from Resources; null = heat disabled
    private bool heatSpriteResolved;  // whether we've already tried to load it
    private float heatAlpha;          // current tint alpha
    private float targetHeatAlpha;    // combo-driven target

    // Landing guide line (world-space).
    private LineRenderer guideLine;

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
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }

        if (gameManager != null)
        {
            gameManager.OnPerfectHitStreak += OnPerfectHitStreak;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnComboChanged += OnComboChanged;
            gameManager.OnGameStart += OnGameStart;
        }
    }

    private void Update()
    {
        UpdateComboHeat();
        UpdateLandingGuide();
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
        // The signature moment - full spectacle: shake + slow-mo + punch-zoom + flash + haptic.
        ShakeCamera(KukulkanShake);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Heavy);

        if (spectacleRoutine != null) StopCoroutine(spectacleRoutine);
        spectacleRoutine = StartCoroutine(KukulkanSpectacleRoutine());
    }

    /// <summary>
    /// Slow-mo dip while the stack snaps straight, with a camera punch-zoom and a gold
    /// screen flash. Uses realtime/unscaled timing so it plays through the slow-mo itself.
    /// </summary>
    private IEnumerator KukulkanSpectacleRoutine()
    {
        kukulkanActive = true;

        // Cancel any regular hit-stop so it doesn't fight the slow-mo time-scale.
        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;
        }

        // Visual punch (both self-gate on the reduce-motion / screen-shake setting).
        if (cameraController == null) cameraController = DependencyRegistry.Find<CameraController>();
        cameraController?.PunchZoom(KukulkanZoomFraction, KukulkanZoomDuration);
        FlashScreen(KukulkanFlashColor, KukulkanFlashDuration);

        // Slow-mo - skip if paused or the run just ended.
        bool canSlowMo = (uiManager == null || !uiManager.IsPaused)
                         && (gameManager == null || !gameManager.IsGameOver);
        if (canSlowMo)
        {
            Time.timeScale = KukulkanSlowMoScale;

            yield return new WaitForSecondsRealtime(KukulkanSlowMoHold);

            float t = 0f;
            while (t < KukulkanSlowMoRamp)
            {
                // Bail if a pause takes over the time-scale mid-ramp.
                if (uiManager != null && uiManager.IsPaused) break;
                t += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(KukulkanSlowMoScale, 1f, t / KukulkanSlowMoRamp);
                yield return null;
            }

            if (uiManager == null || !uiManager.IsPaused)
            {
                Time.timeScale = 1f;
            }
        }

        kukulkanActive = false;
        spectacleRoutine = null;
    }

    /// <summary>Fades a full-screen colour flash from color.a down to 0 over duration.</summary>
    private void FlashScreen(Color color, float duration)
    {
        if (!GameFeelSettings.ScreenShakeEnabled) return; // respect reduce-motion / photosensitivity
        EnsureFlashOverlay();
        if (flashImage == null) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(color, duration));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        float peak = color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(peak, 0f, Mathf.Clamp01(t / duration));
            flashImage.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        flashImage.color = new Color(color.r, color.g, color.b, 0f);
        flashRoutine = null;
    }

    /// <summary>Lazily builds a self-owned screen-space overlay canvas shared by the fullscreen effects.</summary>
    private void EnsureOverlayCanvas()
    {
        if (overlayCanvas != null) return;

        var canvasGO = new GameObject("GameFeelOverlayCanvas");
        canvasGO.transform.SetParent(transform, false);
        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 32000; // above gameplay UI
    }

    private Image CreateFullscreenImage(string name)
    {
        EnsureOverlayCanvas();
        var imgGO = new GameObject(name);
        imgGO.transform.SetParent(overlayCanvas.transform, false);
        var img = imgGO.AddComponent<Image>();
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    private void EnsureFlashOverlay()
    {
        if (flashImage != null) return;
        flashImage = CreateFullscreenImage("Flash");
        flashImage.color = new Color(KukulkanFlashColor.r, KukulkanFlashColor.g, KukulkanFlashColor.b, 0f);
        flashImage.transform.SetAsLastSibling(); // flash renders above the heat tint
    }

    private void EnsureHeatOverlay()
    {
        if (heatImage != null || heatSprite == null) return;
        heatImage = CreateFullscreenImage("ComboHeat");
        heatImage.sprite = heatSprite;            // user-supplied; shape/vignette is up to the texture
        heatImage.type = Image.Type.Simple;
        heatImage.color = new Color(HeatColor.r, HeatColor.g, HeatColor.b, 0f);
        heatImage.transform.SetAsFirstSibling(); // under the flash
    }

    // ---- Combo heat ----

    private void OnComboChanged(int combo, float multiplier)
    {
        if (!GameFeelSettings.ScreenShakeEnabled)
        {
            targetHeatAlpha = 0f; // respect reduce-motion
            return;
        }
        // Ramp from combo 2..HeatComboForMax so a single perfect doesn't tint the screen.
        float f = Mathf.Clamp01((combo - 1) / (float)(HeatComboForMax - 1));
        targetHeatAlpha = f * HeatMaxAlpha;
    }

    private void OnGameStart()
    {
        // Fresh run - clear any lingering heat.
        targetHeatAlpha = 0f;
    }

    private void UpdateComboHeat()
    {
        // Resolve the user-supplied vignette once. If none is provided, the whole effect is off.
        if (!heatSpriteResolved)
        {
            heatSprite = Resources.Load<Sprite>(HeatVignetteResourcePath);
            heatSpriteResolved = true;
        }
        if (heatSprite == null) return; // disabled until a texture is dropped in

        // Nothing to do until there's heat to show (avoids creating the overlay in the menu).
        if (heatImage == null && targetHeatAlpha <= 0f && heatAlpha <= 0f) return;

        if (heatImage == null) EnsureHeatOverlay();
        if (heatImage == null) return;

        // Ease toward the target tint (~0.8s from 0 to full).
        heatAlpha = Mathf.MoveTowards(heatAlpha, targetHeatAlpha, HeatMaxAlpha * HeatLerpSpeed * Time.unscaledDeltaTime);
        heatImage.color = new Color(HeatColor.r, HeatColor.g, HeatColor.b, heatAlpha);
    }

    // ---- Landing guide ----

    private void UpdateLandingGuide()
    {
        if (!GameFeelSettings.LandingGuideEnabled)
        {
            if (guideLine != null && guideLine.enabled) guideLine.enabled = false;
            return;
        }

        // Need an active game, a spawner, and a current block that hasn't been dropped yet.
        if (gameManager == null || !gameManager.IsGameActive || gameManager.IsGameOver
            || objectSpawner == null || stackManager == null)
        {
            if (guideLine != null && guideLine.enabled) guideLine.enabled = false;
            return;
        }

        GameObject current = objectSpawner.CurrentObject;
        StackableObject block = current != null ? current.GetComponent<StackableObject>() : null;
        if (block == null || block.IsDropped)
        {
            if (guideLine != null && guideLine.enabled) guideLine.enabled = false;
            return;
        }

        EnsureGuideLine();
        if (guideLine == null) return;

        // Draw from just below the block down to the projected landing surface at the block's X.
        float x = current.transform.position.x;
        float topY = current.transform.position.y;
        float landY = stackManager.GetStackTopY();
        if (landY > topY) landY = topY; // guard against odd states

        guideLine.enabled = true;
        guideLine.SetPosition(0, new Vector3(x, topY, 0f));
        guideLine.SetPosition(1, new Vector3(x, landY, 0f));
    }

    private void EnsureGuideLine()
    {
        if (guideLine != null) return;

        var go = new GameObject("LandingGuide");
        go.transform.SetParent(transform, false);
        guideLine = go.AddComponent<LineRenderer>();
        guideLine.useWorldSpace = true;
        guideLine.positionCount = 2;
        guideLine.numCapVertices = 2;
        guideLine.textureMode = LineTextureMode.Stretch;
        guideLine.startWidth = GuideWidth;
        guideLine.endWidth = GuideWidth;
        guideLine.material = new Material(Shader.Find("Sprites/Default"));
        guideLine.startColor = GuideColor;
        guideLine.endColor = new Color(GuideColor.r, GuideColor.g, GuideColor.b, GuideColor.a * 0.35f);
        guideLine.sortingOrder = -1; // behind the blocks
        guideLine.enabled = false;
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
        // Don't freeze if the game is paused, already over, or the Kukulkan slow-mo owns the clock.
        if (kukulkanActive) return;
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
            gameManager.OnComboChanged -= OnComboChanged;
            gameManager.OnGameStart -= OnGameStart;
        }

        // Safety: never leave the game frozen/slowed if we're torn down mid hit-stop or slow-mo.
        if (Time.timeScale < 1f && (uiManager == null || !uiManager.IsPaused))
        {
            Time.timeScale = 1f;
        }

        if (instance == this) instance = null;
    }
}
