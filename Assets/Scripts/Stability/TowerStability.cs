using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The tremor meter: the reason a drop is worth aiming.
///
/// Before this, an off-centre landing only cost points - the swing was too narrow to
/// overhang a block, so mashing the screen was nearly as good as timing it. Now a sloppy
/// landing is remembered: tremor rises, Perfects and Kukulkan bring it down, and when it
/// fills the temple trembles. The very next stone must land Perfect (the temple is saved and
/// Kukulkan straightens it) or the run ends with cause "tremor".
///
/// Self-bootstraps into gameplay scenes like GameFeelManager and builds its bar on its own
/// overlay canvas, so no scene or authored UI prefab is touched.
/// </summary>
public class TowerStability : MonoBehaviour
{
    public const string IntroSeenKey = "Stability_IntroSeen";

    /// <summary>Authored meter prefab that replaces the code-built bar when present.</summary>
    public const string MeterPrefabResourcePath = "UI/TowerStabilityMeter";

    private static TowerStability instance;

    /// <summary>The live instance in a gameplay scene, or null.</summary>
    public static TowerStability Instance => instance;

    /// <summary>Current tremor, 0 (still) to 1 (trembling).</summary>
    public float Tremor => tremor;

    /// <summary>True while the next landing must be Perfect.</summary>
    public bool IsTrembling => trembling;

    /// <summary>True while the meter is counting landings in the current run.</summary>
    public bool IsRunLive => enabled && settings != null && IsLive();

    public event System.Action<float> OnTremorChanged;
    public event System.Action<bool> OnTremblingChanged;

    private StabilitySettings settings;
    private GameManager gameManager;
    private StackManager stackManager;
    private LevelManager levelManager;
    private CameraController cameraController;

    private float tremor;
    private bool trembling;
    private bool collapsePending;
    private int lastKukulkanFrame = -1;
    private float nextShakeTime;

    // Meter: an authored prefab view, or the code-built bar below
    private TowerStabilityView view;
    private GameObject uiRoot;
    private RectTransform barRect;
    private RectTransform fillRect;
    private Image fillImage;
    private float displayedFill;
    private RectTransform previewRect;
    private Image previewImage;

    // Serpent's Edge cost preview and "look here" pulse, both driven by other systems.
    private float edgePreview;
    private float highlightUntil;

    private const float BarPadding = 6f;

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

        var go = new GameObject("TowerStability");
        instance = go.AddComponent<TowerStability>();
    }

    /// <summary>Forgets that the explainer was shown, so it fires again. Used by data resets.</summary>
    public static void ResetIntro()
    {
        PlayerPrefs.DeleteKey(IntroSeenKey);
    }

    /// <summary>
    /// Records the explainer as seen without showing it. The tutorial teaches the meter in its
    /// own beat, and the banner repeating it moments later would talk over that line.
    /// </summary>
    public static void MarkIntroSeen()
    {
        if (PlayerPrefs.GetInt(IntroSeenKey, 0) == 1) return;
        PlayerPrefs.SetInt(IntroSeenKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Shows a ghost segment of <paramref name="amount"/> tremor above the fill; 0 hides it.</summary>
    public void SetEdgePreview(float amount)
    {
        edgePreview = Mathf.Clamp01(amount);
    }

    /// <summary>Pulses the meter for <paramref name="seconds"/> (realtime) to draw the eye to it.</summary>
    public void Highlight(float seconds)
    {
        highlightUntil = Mathf.Max(highlightUntil, Time.unscaledTime + seconds);
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
        settings = Resources.Load<StabilitySettings>(StabilitySettings.ResourcePath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<StabilitySettings>();
        }

        if (!settings.enableStability)
        {
            enabled = false;
            return;
        }

        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        cameraController = DependencyRegistry.Find<CameraController>();

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameRestart += OnGameRestart;
            gameManager.OnGameOver += OnGameOver;
            gameManager.OnPerfectHitStreak += OnPerfectHitStreak;
        }

        BuildUI();
        ResetRun();
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
            gameManager.OnGameStart -= OnGameStart;
            gameManager.OnGameRestart -= OnGameRestart;
            gameManager.OnGameOver -= OnGameOver;
            gameManager.OnPerfectHitStreak -= OnPerfectHitStreak;
        }
    }

    // ---- Rules ----

    private bool AppliesToCurrentMode()
    {
        if (gameManager == null || settings == null) return false;

        switch (gameManager.CurrentGameMode)
        {
            case GameMode.InfiniteStacker: return settings.applyToInfinite;
            case GameMode.StackerLevels: return settings.applyToLevels;
            case GameMode.DailyChallenge: return settings.applyToDaily;
            default: return true;
        }
    }

    private bool IsLive()
    {
        if (gameManager == null || !gameManager.IsGameActive || gameManager.IsGameOver) return false;
        if (levelManager != null && levelManager.IsLevelComplete) return false;
        return AppliesToCurrentMode();
    }

    private bool IsSuppressed => settings.suppressDuringTutorial && FtueState.NeedsTutorial;

    private void OnObjectAddedToStack(StackableObject block)
    {
        if (block == null || !IsLive() || collapsePending) return;

        // The foundation stone lands on the ground and has nothing to be off-centre from.
        if (stackManager != null && stackManager.GetStackCount() <= 1) return;

        float accuracy = block.LandingAccuracy;
        bool perfect = accuracy >= gameManager.PerfectThreshold;
        bool good = !perfect && accuracy >= gameManager.GoodThreshold;

        if (trembling)
        {
            if (perfect) Steady();
            else StartCoroutine(CollapseAfterFrame(accuracy));
            return;
        }

        float delta;
        if (perfect)
        {
            delta = -settings.perfectRelief;
        }
        else if (good)
        {
            // 0 just under Perfect, 1 just over Poor: a near-miss costs less than a scrape.
            float t = Mathf.InverseLerp(gameManager.PerfectThreshold, gameManager.GoodThreshold, accuracy);
            delta = Mathf.Lerp(settings.goodStrainMin, settings.goodStrainMax, t);
        }
        else
        {
            delta = settings.poorStrain;
        }

        // Serpent's Edge: the bonus is paid for in tremor whatever the tier, so a Perfect edge
        // landing forfeits its relief rather than cancelling the cost.
        if (block.IsEdgeDrop && settings.enableEdge)
        {
            delta = Mathf.Max(delta, 0f) + settings.edgeStrain;
        }

        SetTremor(tremor + delta);

        if (delta > 0f) MaybeShowIntro();

        if (tremor >= 1f) EnterTrembling();
    }

    private void OnPerfectHitStreak()
    {
        lastKukulkanFrame = Time.frameCount;
        if (!IsLive() || trembling) return;

        SetTremor(tremor - settings.kukulkanRelief);
    }

    private void EnterTrembling()
    {
        trembling = true;
        nextShakeTime = 0f;
        OnTremblingChanged?.Invoke(true);

        RunBanner.Show(
            LocalizationManager.Get("stability_warning_title"),
            LocalizationManager.Get("stability_warning_body"),
            RunOverlayUI.Clay,
            settings.warningHoldSeconds);

        HapticFeedback.Trigger(HapticFeedback.HapticType.Heavy);
        if (cameraController != null) cameraController.Shake(settings.trembleShakeTrauma * 2f);

        GameAnalytics.Track("tremor_warning", RunEventData());
    }

    private void Steady()
    {
        trembling = false;
        OnTremblingChanged?.Invoke(false);

        // A Perfect that also completed the streak has already summoned Kukulkan this frame;
        // a second shift would replay the spectacle on top of itself.
        if (settings.kukulkanOnSave && lastKukulkanFrame != Time.frameCount)
        {
            gameManager.TriggerKukulkanShift();
        }

        // Set after the shift so its relief doesn't stack on top of the save.
        SetTremor(settings.tremorAfterSave);

        RunBanner.Show(LocalizationManager.Get("stability_saved"), RunOverlayUI.Gold);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);

        GameAnalytics.Track("tremor_saved", RunEventData());
    }

    /// <summary>
    /// Ends the run one frame late: LevelManager completes a temple from the same landing
    /// event, and a stone that finishes the temple must never also collapse it.
    /// </summary>
    private IEnumerator CollapseAfterFrame(float accuracy)
    {
        collapsePending = true;
        yield return null;
        collapsePending = false;

        if (gameManager == null || gameManager.IsGameOver) yield break;

        if (levelManager != null && levelManager.IsLevelComplete)
        {
            trembling = false;
            OnTremblingChanged?.Invoke(false);
            yield break;
        }

        var data = RunEventData();
        data["accuracy"] = accuracy;
        GameAnalytics.Track("tremor_collapse", data);

        gameManager.GameOver("tremor");
    }

    private void SetTremor(float value)
    {
        float max = IsSuppressed ? settings.tutorialCap : 1f;
        float clamped = Mathf.Clamp(value, 0f, max);
        if (Mathf.Approximately(clamped, tremor)) return;

        tremor = clamped;
        OnTremorChanged?.Invoke(tremor);
    }

    private void MaybeShowIntro()
    {
        if (trembling || PlayerPrefs.GetInt(IntroSeenKey, 0) == 1) return;

        // The tutorial has its own Tremor beat (FtueTutorial), which marks this seen.
        if (FtueState.NeedsTutorial) return;

        PlayerPrefs.SetInt(IntroSeenKey, 1);
        PlayerPrefs.Save();

        RunBanner.Show(
            LocalizationManager.Get("stability_intro_title"),
            LocalizationManager.Get("stability_intro_body"),
            RunOverlayUI.Gold,
            2.6f,
            settings.introBannerYOffset);

        GameAnalytics.Track("tremor_intro_shown");
    }

    private Dictionary<string, object> RunEventData()
    {
        return new Dictionary<string, object>
        {
            { "mode", gameManager != null ? gameManager.CurrentGameMode.ToString() : "unknown" },
            { "height", stackManager != null ? stackManager.GetStackCount() : 0 }
        };
    }

    // ---- Run lifecycle ----

    private void OnGameStart() => ResetRun();

    private void OnGameRestart() => ResetRun();

    private void OnGameOver()
    {
        if (trembling)
        {
            trembling = false;
            OnTremblingChanged?.Invoke(false);
        }
    }

    private void ResetRun()
    {
        StopAllCoroutines();
        collapsePending = false;
        trembling = false;
        tremor = 0f;
        displayedFill = 0f;
        edgePreview = 0f;
        highlightUntil = 0f;
        OnTremorChanged?.Invoke(tremor);

        if (uiRoot != null) uiRoot.SetActive(AppliesToCurrentMode());
    }

    // ---- Presentation ----

    private void Update()
    {
        if (trembling && IsLive() && cameraController != null && Time.unscaledTime >= nextShakeTime)
        {
            cameraController.Shake(settings.trembleShakeTrauma);
            nextShakeTime = Time.unscaledTime + settings.trembleShakeInterval;
        }

        UpdateBar();
    }

    private void BuildUI()
    {
        uiRoot = new GameObject("TowerStabilityUI");
        uiRoot.transform.SetParent(transform, false);

        if (BuildFromPrefab()) return;
        BuildInCode();
    }

    /// <summary>
    /// Instantiates the authored meter, if there is one. Returns false when no usable prefab
    /// exists, so the code-built bar is used instead.
    /// </summary>
    private bool BuildFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(MeterPrefabResourcePath);
        if (prefab == null) return false;

        // The prefab carries its own Canvas; the host owns its lifetime and visibility.
        var instantiated = Instantiate(prefab, uiRoot.transform, false);
        view = instantiated.GetComponent<TowerStabilityView>();

        if (view == null || !view.IsUsable)
        {
            Debug.LogWarning($"[TowerStability] Resources/{MeterPrefabResourcePath} has no usable " +
                             "TowerStabilityView (needs Fill + Fill Image) - using the code-built meter instead.");
            // Hide first: Destroy is deferred, and a half-built canvas shouldn't flash for a frame.
            instantiated.SetActive(false);
            Destroy(instantiated);
            view = null;
            return false;
        }

        view.SetLabel(LocalizationManager.Get("stability_label"));
        view.SetFill(0f, false);
        return true;
    }

    private void BuildInCode()
    {
        RunOverlayUI.CreateCanvas(uiRoot, settings.canvasSortingOrder, interactive: false);

        barRect = RunOverlayUI.CreateChild("TremorBar", uiRoot.transform);
        RunOverlayUI.Place(barRect, settings.barAnchor, settings.barPosition, settings.barSize);

        var background = barRect.gameObject.AddComponent<Image>();
        background.color = RunOverlayUI.Backdrop;
        background.raycastTarget = false;

        fillRect = RunOverlayUI.CreateChild("Fill", barRect);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 0f);
        fillRect.pivot = new Vector2(0.5f, 0f);
        fillRect.anchoredPosition = new Vector2(0f, BarPadding);
        fillRect.sizeDelta = new Vector2(-2f * BarPadding, 0f);

        fillImage = fillRect.gameObject.AddComponent<Image>();
        fillImage.color = RunOverlayUI.Jade;
        fillImage.raycastTarget = false;

        previewRect = RunOverlayUI.CreateChild("EdgePreview", barRect);
        previewRect.anchorMin = new Vector2(0f, 0f);
        previewRect.anchorMax = new Vector2(1f, 0f);
        previewRect.pivot = new Vector2(0.5f, 0f);
        previewImage = previewRect.gameObject.AddComponent<Image>();
        previewImage.raycastTarget = false;
        previewRect.gameObject.SetActive(false);

        var label = RunOverlayUI.CreateLabel("Label", barRect,
            LocalizationManager.Get("stability_label"), 30f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(220f, 50f));
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void UpdateBar()
    {
        if (uiRoot == null || !uiRoot.activeSelf) return;

        displayedFill = Mathf.MoveTowards(displayedFill, tremor, 2.5f * Time.unscaledDeltaTime);

        // Fades out over its last half-second rather than snapping off.
        float highlight = Mathf.Clamp01((highlightUntil - Time.unscaledTime) * 2f);

        if (view != null)
        {
            view.SetPreview(trembling ? 0f : edgePreview);
            view.SetFill(displayedFill, trembling, highlight);
            return;
        }

        if (barRect == null) return;

        float innerHeight = Mathf.Max(0f, settings.barSize.y - 2f * BarPadding);
        fillRect.sizeDelta = new Vector2(-2f * BarPadding, innerHeight * displayedFill);

        bool showPreview = edgePreview > 0f && !trembling;
        if (previewRect.gameObject.activeSelf != showPreview) previewRect.gameObject.SetActive(showPreview);
        if (showPreview)
        {
            float top = Mathf.Min(1f, displayedFill + edgePreview);
            previewRect.anchoredPosition = new Vector2(0f, BarPadding + innerHeight * displayedFill);
            previewRect.sizeDelta = new Vector2(-2f * BarPadding, innerHeight * (top - displayedFill));

            Color ghost = RunOverlayUI.Gold;
            ghost.a = 0.35f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f));
            previewImage.color = ghost;
        }

        Color color = displayedFill < 0.6f
            ? Color.Lerp(RunOverlayUI.Jade, RunOverlayUI.Gold, displayedFill / 0.6f)
            : Color.Lerp(RunOverlayUI.Gold, RunOverlayUI.Clay, (displayedFill - 0.6f) / 0.4f);

        if (trembling)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
            color = Color.Lerp(RunOverlayUI.Clay, RunOverlayUI.Parchment, pulse * 0.5f);
            barRect.localScale = Vector3.one * (1f + 0.06f * pulse);
        }
        else if (highlight > 0f)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            barRect.localScale = Vector3.one * (1f + 0.12f * highlight * wave);
        }
        else
        {
            barRect.localScale = Vector3.one;
        }

        fillImage.color = color;
    }
}
