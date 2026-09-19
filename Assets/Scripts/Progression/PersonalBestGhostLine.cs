using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A faint line across the screen at the height of the player's tallest Infinite tower, so
/// every run has something to climb toward.
///
/// Infinite only, and hidden until a first run has set a best. Passing it flashes the line
/// gold, shows a banner and retires the line for the rest of the run. The run's peak is
/// committed to <see cref="InfiniteBest"/> on game over, the same moment the high score saves.
///
/// Self-bootstraps into gameplay scenes like TowerStability and builds its label on its own
/// overlay canvas, so no scene or authored UI prefab is touched.
/// </summary>
public class PersonalBestGhostLine : MonoBehaviour
{
    [Header("Line")]
    [SerializeField] private Color LineColor = new Color(0.93f, 0.90f, 0.82f, 0.4f);
    [SerializeField] private float LineWidth = 0.05f;

    [Header("Passing the best")]
    [SerializeField] private float PassFadeDuration = 0.8f;

    [Header("Label")]
    [Tooltip("Just above the authored HUD (0), below banners and the tremor meter.")]
    [SerializeField] private int CanvasSortingOrder = 10;
    [SerializeField] private float LabelFontSize = 30f;
    [SerializeField] private float LabelMargin = 24f;

    private static PersonalBestGhostLine instance;

    private GameManager gameManager;
    private StackManager stackManager;

    private LineRenderer line;
    private Material lineMaterial;
    private GameObject uiRoot;
    private RectTransform canvasRect;
    private TextMeshProUGUI label;

    private int bestBlocks;
    private float bestHeight;
    private int runPeakBlocks;
    private float runPeakHeight;
    private bool passed;
    private float passFade;
    private Coroutine passRoutine;

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

        var go = new GameObject("PersonalBestGhostLine");
        instance = go.AddComponent<PersonalBestGhostLine>();
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
        stackManager = DependencyRegistry.Find<StackManager>();

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStart += ResetRun;
            gameManager.OnGameRestart += ResetRun;
            gameManager.OnGameOver += OnGameOver;
        }

        BuildLine();
        BuildLabel();
        ResetRun();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStart -= ResetRun;
            gameManager.OnGameRestart -= ResetRun;
            gameManager.OnGameOver -= OnGameOver;
        }

        if (lineMaterial != null) Destroy(lineMaterial);
    }

    // ---- Run tracking ----

    private bool IsInfiniteRunLive()
    {
        return gameManager != null
            && gameManager.CurrentGameMode == GameMode.InfiniteStacker
            && gameManager.IsGameActive
            && !gameManager.IsGameOver;
    }

    private void ResetRun()
    {
        bestBlocks = InfiniteBest.Blocks;
        bestHeight = InfiniteBest.Height;
        runPeakBlocks = 0;
        runPeakHeight = 0f;
        passed = false;
        passFade = 0f;

        if (passRoutine != null)
        {
            StopCoroutine(passRoutine);
            passRoutine = null;
        }

        ApplyLineColor(LineColor);

        if (label != null)
        {
            label.text = LocalizationManager.Get("ghost_best_label", bestBlocks);
            label.color = WithAlpha(RunOverlayUI.Parchment, 0.7f);
        }
    }

    private void OnObjectAddedToStack(StackableObject block)
    {
        if (block == null || stackManager == null || !IsInfiniteRunLive()) return;

        int count = stackManager.GetStackCount();
        if (count <= runPeakBlocks) return;

        // The stone that just raised the count is the top of the tower.
        float top = block.Collider != null ? block.Collider.bounds.max.y : block.transform.position.y;
        runPeakBlocks = count;
        runPeakHeight = Mathf.Max(0f, top - GroundTop());

        if (!passed && bestBlocks > 0 && count > bestBlocks)
        {
            passed = true;
            passRoutine = StartCoroutine(PassRoutine());

            RunBanner.Show(LocalizationManager.Get("ghost_new_best"), RunOverlayUI.Gold);
            GameAnalytics.Track("pb_ghost_passed", new Dictionary<string, object>
            {
                { "best_blocks", bestBlocks }
            });
        }
    }

    private void OnGameOver()
    {
        if (gameManager == null || gameManager.CurrentGameMode != GameMode.InfiniteStacker) return;
        if (!InfiniteBest.TryRecord(runPeakBlocks, runPeakHeight)) return;

        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        if (playFabManager != null && playFabManager.IsLoggedIn)
        {
            playFabManager.SaveCurrentProgressToCloud();
        }
    }

    private IEnumerator PassRoutine()
    {
        passFade = 1f;
        float elapsed = 0f;

        while (elapsed < PassFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            passFade = 1f - Mathf.Clamp01(elapsed / PassFadeDuration);

            ApplyLineColor(WithAlpha(RunOverlayUI.Gold, 0.9f * passFade));
            if (label != null) label.color = WithAlpha(RunOverlayUI.Gold, passFade);
            yield return null;
        }

        passFade = 0f;
        passRoutine = null;
    }

    // ---- Presentation ----

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        bool show = cam != null && IsInfiniteRunLive() && bestBlocks > 0 && (!passed || passFade > 0f);

        SetVisible(show);
        if (!show) return;

        float y = GroundTop() + bestHeight;
        float depth = Mathf.Abs(cam.transform.position.z);
        float left = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x;
        float right = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x;

        line.SetPosition(0, new Vector3(left, y, 0f));
        line.SetPosition(1, new Vector3(right, y, 0f));

        PlaceLabel(cam, new Vector3(left, y, 0f));
    }

    private void PlaceLabel(Camera cam, Vector3 worldLeft)
    {
        if (label == null || canvasRect == null) return;

        Vector2 screen = cam.WorldToScreenPoint(worldLeft);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            label.enabled = false;
            return;
        }

        Rect bounds = canvasRect.rect;
        label.enabled = local.y >= bounds.yMin && local.y <= bounds.yMax;
        label.rectTransform.anchoredPosition = new Vector2(bounds.xMin + LabelMargin, local.y + 4f);
    }

    private void SetVisible(bool visible)
    {
        if (line != null && line.enabled != visible) line.enabled = visible;
        if (uiRoot != null && uiRoot.activeSelf != visible) uiRoot.SetActive(visible);
    }

    private float GroundTop()
    {
        Ground ground = stackManager != null ? stackManager.GetGround() : null;
        return ground != null ? ground.GetGroundTop() : 0f;
    }

    private void BuildLine()
    {
        var go = new GameObject("GhostLine");
        go.transform.SetParent(transform, false);

        line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 0;
        line.startWidth = LineWidth;
        line.endWidth = LineWidth;
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        line.material = lineMaterial;
        line.sortingOrder = -1; // behind the blocks, like the landing guide
        line.enabled = false;
    }

    private void BuildLabel()
    {
        uiRoot = new GameObject("GhostLineUI", typeof(RectTransform));
        uiRoot.transform.SetParent(transform, false);
        canvasRect = (RectTransform)uiRoot.transform;

        RunOverlayUI.CreateCanvas(uiRoot, CanvasSortingOrder, interactive: false);

        label = RunOverlayUI.CreateLabel("BestLabel", uiRoot.transform, string.Empty,
            LabelFontSize, WithAlpha(RunOverlayUI.Parchment, 0.7f));
        RunOverlayUI.Place(label.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 50f));
        label.rectTransform.pivot = Vector2.zero;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void ApplyLineColor(Color color)
    {
        if (line == null) return;
        line.startColor = color;
        line.endColor = color;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
