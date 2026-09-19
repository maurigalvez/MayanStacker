using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Serpent's Edge: a risk choice on every drop.
///
/// Release the stone while it is near either end of Kukulkan's swing and it scores
/// <see cref="StabilitySettings.edgeScoreMultiplier"/> times its base points - but it adds
/// <see cref="StabilitySettings.edgeStrain"/> tremor on top of whatever its landing earned, and
/// forfeits a Perfect's relief.
///
/// The edge is a timing test. Released in the sweet spot (the very end of the swing,
/// <see cref="StabilitySettings.edgeSweetSpotThreshold"/>) the stone lands as a Perfect wherever
/// it lands on the stone, so the combo grows and multiplies the x3. Released anywhere else in the
/// window it scores its real off-centre landing - usually a Good, which breaks the combo. A
/// careless edge tap is never a cheap high score, and Good-breaks-combo stays intact.
///
/// What the player sees and hears:
///  - golden strips on the top stone's rim where an edge drop would land, the live side lit
///    while releasing counts and flashing during the sweet spot - on the tower, because that is
///    where the player looks to aim. Each shows what a sweet-spot drop there would score;
///  - a rattle and a light haptic tick each time the stone swings into the window;
///  - a pulsing ghost segment on the Tremor meter showing what the drop will cost;
///  - a short callout and a meter pulse when an edge stone lands.
///
/// The window stays shut during the tutorial run, while the temple trembles (that stone must
/// be Perfect), below <see cref="StabilitySettings.edgeMinStackHeight"/>, and whenever the
/// Tremor meter isn't running - the tremor is the price, so no meter means no window.
///
/// First time it opens for a player it runs a short, action-gated intro (FtueState tracks it).
///
/// Self-bootstraps into gameplay scenes like TowerStability; touches no scene or authored UI.
/// </summary>
public class RiskWindow : MonoBehaviour
{
    /// <summary>Authored marker prefab that replaces the code-built markers when present.</summary>
    public const string ViewPrefabResourcePath = "UI/SerpentsEdge";

    private static RiskWindow instance;

    /// <summary>The live instance in a gameplay scene, or null.</summary>
    public static RiskWindow Instance => instance;

    /// <summary>True when the window can be used on the current stone.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>True when releasing right now would be an edge drop.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>True when releasing right now would be a sweet-spot edge drop (lands as a Perfect).</summary>
    public bool IsSweet { get; private set; }

    /// <summary>Raised for every stone released in the window, right after it is marked.</summary>
    public event System.Action<StackableObject> OnEdgeDrop;

    private StabilitySettings settings;
    private GameManager gameManager;
    private StackManager stackManager;
    private ObjectSpawner objectSpawner;
    private SpawnerHolder spawnerHolder;
    private GameSoundManager soundManager;
    private Camera mainCamera;

    private bool wasOpen;
    private bool wasSweet;
    private float lastCueTime = float.NegativeInfinity;
    private static AudioClip placeholderRattle;

    private GameObject uiRoot;
    private RiskWindowView view;
    private RectTransform canvasRect;

    private bool introActive;
    private int introDrops;

    // Value preview: the hanging stone's component is cached so the per-frame projection
    // doesn't GetComponent every frame, and each label is only rebuilt when its value changes.
    private GameObject projectedStoneObject;
    private StackableObject projectedStone;
    private readonly int[] projectionKey = { int.MinValue, int.MinValue };

    // Run stats for the result card.
    private int pendingEdgeComboBefore;

    /// <summary>Edge stones that landed on the stack this run.</summary>
    public int RunEdgeLanded { get; private set; }

    /// <summary>Points this run earned on top of what the same landings would have scored off the edge.</summary>
    public int RunEdgeBonusPoints { get; private set; }

    /// <summary>Live combos an edge landing ended this run.</summary>
    public int RunEdgeCombosBroken { get; private set; }

    /// <summary>
    /// The share of <paramref name="points"/> the edge multiplier added. Used by the landing
    /// callout and the run total, so both report the same number.
    /// </summary>
    public static int EdgeBonus(int points, float edgeMultiplier)
    {
        if (edgeMultiplier <= 1f || points <= 0) return 0;
        return points - Mathf.RoundToInt(points / edgeMultiplier);
    }

    private const float IntroBannerHold = 3.4f;
    private const float FollowupBannerHold = 2.4f;

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

        var go = new GameObject("RiskWindow");
        instance = go.AddComponent<RiskWindow>();
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

        if (!settings.enableStability || !settings.enableEdge)
        {
            enabled = false;
            return;
        }

        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        spawnerHolder = DependencyRegistry.Find<SpawnerHolder>();
        soundManager = DependencyRegistry.Find<GameSoundManager>();
        mainCamera = Camera.main;

        if (objectSpawner != null) objectSpawner.OnObjectDropped += OnObjectDropped;
        if (stackManager != null) stackManager.OnObjectAddedToStack += OnObjectAddedToStack;

        if (gameManager != null)
        {
            gameManager.OnGameStart += ResetRun;
            gameManager.OnGameRestart += ResetRun;
        }

        BuildUI();
        ResetRun();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        if (objectSpawner != null) objectSpawner.OnObjectDropped -= OnObjectDropped;
        if (stackManager != null) stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;

        if (gameManager != null)
        {
            gameManager.OnGameStart -= ResetRun;
            gameManager.OnGameRestart -= ResetRun;
        }
    }

    // ---- Rules ----

    /// <summary>True once this player has the window at all: past the tutorial run.</summary>
    private bool IsUnlockedForPlayer =>
        !FtueState.NeedsTutorial &&
        (FtueState.HasCompletedFirstTemple || FtueState.LifetimeRuns >= settings.edgeUnlockRuns);

    private bool ComputeAvailable()
    {
        if (gameManager == null || objectSpawner == null || spawnerHolder == null) return false;

        TowerStability stability = TowerStability.Instance;
        if (stability == null || !stability.IsRunLive || stability.IsTrembling) return false;

        if (!IsUnlockedForPlayer) return false;
        if (stackManager == null || stackManager.GetStackCount() < settings.edgeMinStackHeight) return false;

        // Only a stone that is hanging and ready to drop can be released in the window.
        return objectSpawner.CurrentObject != null
            && !objectSpawner.WaitingForLanding
            && objectSpawner.IsCurrentObjectArmed;
    }

    private bool IsPhaseInWindow(float phase) => Mathf.Abs(phase) >= settings.edgePhaseThreshold;

    // Never looser than the window itself, so a mistuned asset can't make every edge drop sweet.
    private bool IsPhaseInSweetSpot(float phase) =>
        Mathf.Abs(phase) >= Mathf.Max(settings.edgeSweetSpotThreshold, settings.edgePhaseThreshold);

    private void Update()
    {
        IsAvailable = ComputeAvailable();
        float phase = spawnerHolder != null ? spawnerHolder.SwingPhase : 0f;
        IsOpen = IsAvailable && IsPhaseInWindow(phase);
        IsSweet = IsOpen && IsPhaseInSweetSpot(phase);

        if (IsOpen && !wasOpen) PlayEntryCue();
        wasOpen = IsOpen;

        // The timing cue: the rim flashes (UpdateUI) and the phone ticks as the stone turns.
        if (IsSweet && !wasSweet && settings.edgeSweetSpotHaptic) HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
        wasSweet = IsSweet;

        if (IsAvailable && !introActive && !FtueState.HasSeenEdgeIntro)
        {
            BeginIntro();
        }

        TowerStability stability = TowerStability.Instance;
        if (stability != null) stability.SetEdgePreview(IsOpen ? settings.edgeStrain : 0f);

        UpdateUI(phase);
    }

    /// <summary>
    /// The non-visual half of the cue: the player's eyes are on the tower, so the moment the
    /// stone swings into the window is also heard and felt.
    /// </summary>
    private void PlayEntryCue()
    {
        if (Time.unscaledTime - lastCueTime < settings.edgeCueCooldown) return;
        lastCueTime = Time.unscaledTime;

        if (settings.edgeEntryHaptic) HapticFeedback.Trigger(HapticFeedback.HapticType.Light);

        if (soundManager != null && settings.edgeRattleVolume > 0f)
        {
            AudioClip clip = settings.edgeRattleClip != null ? settings.edgeRattleClip : PlaceholderRattle();
            soundManager.PlaySound(clip, settings.edgeRattleVolume);
        }
    }

    /// <summary>
    /// A short swelling train of filtered noise clicks - stands in for a real rattle until
    /// <see cref="StabilitySettings.edgeRattleClip"/> is assigned.
    /// </summary>
    private static AudioClip PlaceholderRattle()
    {
        if (placeholderRattle != null) return placeholderRattle;

        const int sampleRate = 22050;
        const float lengthSeconds = 0.32f;
        const int clicks = 9;

        int samples = Mathf.CeilToInt(sampleRate * lengthSeconds);
        int spacing = samples / clicks;
        int clickLength = Mathf.RoundToInt(sampleRate * 0.012f);
        var data = new float[samples];
        var rng = new System.Random(1337);
        float filtered = 0f;

        for (int c = 0; c < clicks; c++)
        {
            int start = c * spacing + rng.Next(0, spacing / 4);
            float amplitude = 0.55f * Mathf.Sin(Mathf.PI * (c + 0.5f) / clicks);

            for (int i = 0; i < clickLength && start + i < samples; i++)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += (noise - filtered) * 0.45f; // takes the hiss off
                data[start + i] += filtered * amplitude * Mathf.Exp(-6f * i / clickLength);
            }
        }

        placeholderRattle = AudioClip.Create("EdgeRattlePlaceholder", samples, 1, sampleRate, false);
        placeholderRattle.SetData(data, 0);
        return placeholderRattle;
    }

    private void OnObjectDropped(GameObject dropped)
    {
        // ObjectSpawner raises this before it starts waiting for the landing, so availability
        // still describes the stone that was just released.
        if (dropped == null || !enabled) return;

        float phase = spawnerHolder.SwingPhase;
        bool edge = ComputeAvailable() && IsPhaseInWindow(phase);
        bool sweet = edge && IsPhaseInSweetSpot(phase);

        if (introActive)
        {
            introDrops++;
            if (!edge && introDrops >= settings.edgeIntroMaxDrops) CompleteIntro("ignored");
        }

        if (!edge) return;

        var block = dropped.GetComponent<StackableObject>();
        if (block == null) return;

        block.MarkEdgeDrop(settings.edgeScoreMultiplier, sweet);
        pendingEdgeComboBefore = gameManager.CurrentCombo;
        OnEdgeDrop?.Invoke(block);

        HapticFeedback.Trigger(HapticFeedback.HapticType.Light);

        var data = RunEventData();
        data["tremor_before"] = TowerStability.Instance != null ? TowerStability.Instance.Tremor : 0f;
        data["sweet_spot"] = sweet;
        data["phase"] = Mathf.Abs(phase);
        GameAnalytics.Track("edge_drop", data);
    }

    private void OnObjectAddedToStack(StackableObject block)
    {
        if (block == null || !block.IsEdgeDrop || gameManager == null || gameManager.IsGameOver) return;

        TowerStability stability = TowerStability.Instance;

        bool brokeCombo = pendingEdgeComboBefore > 0 && gameManager.CurrentCombo == 0;
        RunEdgeLanded++;
        RunEdgeBonusPoints += EdgeBonus(gameManager.LastAwardedPoints, block.EdgeScoreMultiplier);
        if (brokeCombo) RunEdgeCombosBroken++;
        pendingEdgeComboBefore = 0;

        var data = RunEventData();
        data["accuracy"] = block.LandingAccuracy;
        data["physical_accuracy"] = block.PhysicalLandingAccuracy;
        data["sweet_spot"] = block.IsEdgeSweetSpot;
        data["points"] = gameManager.LastAwardedPoints;
        data["broke_combo"] = brokeCombo;
        GameAnalytics.Track("edge_landed", data);

        if (stability != null) stability.Highlight(introActive ? FollowupBannerHold : 0.9f);

        // In regular play the "Serpent's Edge xN" callout is a line on UIManager's landing
        // label, not a banner - a full-width strip here sat on top of the points/combo popup.
        // Only the FTUE follow-up still uses the banner, placed in the lower screen.
        if (!introActive) return;

        // The trembling warning outranks the follow-up. If the meter hasn't processed this
        // landing yet, its warning simply replaces the banner a moment later.
        if (stability == null || !stability.IsTrembling)
        {
            RunBanner.Show(
                LocalizationManager.Get("edge_intro_followup_title", FormatMultiplier()),
                LocalizationManager.Get("edge_intro_followup_body"),
                RunOverlayUI.Gold,
                FollowupBannerHold,
                settings.edgeIntroBannerYOffset);
        }

        CompleteIntro("taken");
    }

    // ---- First-time intro ----

    private void BeginIntro()
    {
        introActive = true;
        introDrops = 0;

        RunBanner.Show(
            LocalizationManager.Get("edge_intro_title"),
            LocalizationManager.Get("edge_intro_body", FormatMultiplier()),
            RunOverlayUI.Gold,
            IntroBannerHold,
            settings.edgeIntroBannerYOffset);

        // Point at the cost as well as the reward.
        TowerStability stability = TowerStability.Instance;
        if (stability != null) stability.Highlight(IntroBannerHold);

        GameAnalytics.Track("edge_intro_shown", RunEventData());
    }

    private void CompleteIntro(string outcome)
    {
        introActive = false;
        FtueState.MarkEdgeIntroSeen();
        GameAnalytics.Track("edge_intro_completed", new Dictionary<string, object> { { "outcome", outcome } });
    }

    private void ResetRun()
    {
        // An unfinished intro starts over next run rather than silently counting as seen.
        introActive = false;
        introDrops = 0;
        IsAvailable = false;
        IsOpen = false;
        IsSweet = false;
        wasOpen = false;
        wasSweet = false;

        RunEdgeLanded = 0;
        RunEdgeBonusPoints = 0;
        RunEdgeCombosBroken = 0;
        pendingEdgeComboBefore = 0;
        projectionKey[0] = projectionKey[1] = int.MinValue;

        if (TowerStability.Instance != null) TowerStability.Instance.SetEdgePreview(0f);
        if (uiRoot != null) uiRoot.SetActive(false);
    }

    private string FormatMultiplier()
    {
        return settings.edgeScoreMultiplier.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    private Dictionary<string, object> RunEventData()
    {
        return new Dictionary<string, object>
        {
            { "mode", gameManager != null ? gameManager.CurrentGameMode.ToString() : "unknown" },
            { "height", stackManager != null ? stackManager.GetStackCount() : 0 }
        };
    }

    // ---- Presentation ----

    private void BuildUI()
    {
        uiRoot = new GameObject("SerpentsEdgeUI");
        uiRoot.transform.SetParent(transform, false);

        var prefab = Resources.Load<GameObject>(ViewPrefabResourcePath);
        if (prefab != null)
        {
            var instantiated = Instantiate(prefab, uiRoot.transform, false);
            view = instantiated.GetComponent<RiskWindowView>();

            if (view == null || !view.IsUsable)
            {
                Debug.LogWarning($"[RiskWindow] Resources/{ViewPrefabResourcePath} has no usable " +
                                 "RiskWindowView (needs both zones) - using the code-built markers instead.");
                instantiated.SetActive(false);
                Destroy(instantiated);
                view = null;
            }
        }

        if (view == null)
        {
            var host = new GameObject("SerpentsEdgeCanvas");
            host.transform.SetParent(uiRoot.transform, false);
            view = RiskWindowView.BuildDefault(host, settings.edgeCanvasSortingOrder);
        }

        var canvas = view.GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? (RectTransform)canvas.transform : null;

        view.SetLabel(LocalizationManager.Get("edge_zone_label", FormatMultiplier()));
        uiRoot.SetActive(false);
    }

    private void UpdateUI(float phase)
    {
        if (uiRoot == null || view == null) return;

        if (uiRoot.activeSelf != IsAvailable) uiRoot.SetActive(IsAvailable);
        if (!IsAvailable || canvasRect == null) return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // The strips sit on the top stone's rim, directly under where an edge drop lands (the
        // drop is straight down), so they show the real landing spot - even when that is
        // past the edge of a drifted tower.
        StackableObject top = stackManager.GetTopObject();
        if (top == null)
        {
            uiRoot.SetActive(false);
            return;
        }

        Vector3 basePosition = spawnerHolder.SpawnerBasePosition;
        basePosition.y = (top.Collider != null ? top.Collider.bounds.max.y : top.transform.position.y)
                         + settings.edgeZoneSurfaceOffset;

        float inner = spawnerHolder.GetSwingOffsetX(settings.edgePhaseThreshold);
        float outer = spawnerHolder.GetSwingOffsetX(1f);
        int openSide = IsOpen ? (phase > 0f ? 1 : -1) : 0;
        float emphasis = introActive ? 1f : 0f;

        StackableObject stone = HangingStone();
        // Where the stone's centre would be with no swing, so each side's landing x is exact.
        float stoneRestX = stone != null ? stone.ColliderCenterX - spawnerHolder.GetSwingOffsetX(phase) : 0f;
        int safePoints = stone != null ? gameManager.PreviewPoints(stone.PreviewBaseScore(1f, 1f), 1f) : 0;

        for (int side = -1; side <= 1; side += 2)
        {
            // The true landing band is only ~0.12 units wide - too thin to read or to carry art -
            // so the strip is a fixed world width centred on it.
            float landing = side * (inner + outer) * 0.5f;
            float halfWidth = settings.edgeZoneWorldWidth * 0.5f;
            Vector2 a = WorldToCanvas(basePosition + new Vector3(landing - halfWidth, 0f, 0f));
            Vector2 b = WorldToCanvas(basePosition + new Vector3(landing + halfWidth, 0f, 0f));

            if (stone != null) UpdateProjection(side, stone, top, stoneRestX + landing, safePoints);

            float width = Mathf.Max(Mathf.Abs(b.x - a.x), settings.edgeZoneMinWidth);
            view.SetZone(side, (a + b) * 0.5f, width, settings.edgeZoneHeight, openSide == side,
                         openSide == side && IsSweet, emphasis);
        }
    }

    private StackableObject HangingStone()
    {
        GameObject current = objectSpawner != null ? objectSpawner.CurrentObject : null;
        if (current != projectedStoneObject)
        {
            projectedStoneObject = current;
            projectedStone = current != null ? current.GetComponent<StackableObject>() : null;
        }
        return projectedStone;
    }

    /// <summary>
    /// Works out what a sweet-spot edge drop on <paramref name="side"/> would score - it lands
    /// as a Perfect, so with the combo and run multipliers - and hands the view its caption and
    /// colour. That's the prize the player is timing for; missing the sweet spot scores the real
    /// (off-centre) landing instead, which the intro explains rather than a second number here.
    /// The caption string is only rebuilt when the value behind it changes.
    /// </summary>
    private void UpdateProjection(int side, StackableObject stone, StackableObject top, float landingX, int safePoints)
    {
        // Off the stone entirely is a miss - the sweet spot only upgrades a landing on it.
        bool miss = stone.AccuracyOn(top, landingX) <= 0f;
        int points = miss ? 0 : gameManager.PreviewPoints(stone.PreviewBaseScore(1f, settings.edgeScoreMultiplier), 1f);

        RiskWindowView.Worth worth =
            miss || points < safePoints ? RiskWindowView.Worth.Worse
            : points > safePoints ? RiskWindowView.Worth.Better
            : RiskWindowView.Worth.Neutral;

        int key = miss ? -1 : points;
        int slot = side < 0 ? 0 : 1;
        if (key == projectionKey[slot])
        {
            view.SetProjection(side, null, worth);
            return;
        }
        projectionKey[slot] = key;

        string text = miss ? LocalizationManager.Get("edge_proj_miss") : "+" + points;
        view.SetProjection(side, text, worth);
    }

    private Vector2 WorldToCanvas(Vector3 world)
    {
        Vector3 screen = mainCamera.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        return local;
    }
}
