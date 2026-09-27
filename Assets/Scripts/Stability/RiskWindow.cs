using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Serpent's Edge: a risk choice on every drop.
///
/// Release the stone over either edge band of the top stone - one band each side, a set
/// accuracy off its centre, so it follows the tower wherever it drifts - and it scores
/// <see cref="StabilitySettings.edgeScoreMultiplier"/> times its base points - but it adds
/// <see cref="StabilitySettings.edgeStrain"/> tremor on top of whatever its landing earned, and
/// forfeits a Perfect's relief.
///
/// The edge is a timing test. The band is narrow - from
/// <see cref="StabilitySettings.edgeBandOuterAccuracy"/> to
/// <see cref="StabilitySettings.edgeSweetSpotAccuracy"/> - and a stone released over it lands
/// in the top score tier wherever it lands on the stone, at the current combo multiplier. It
/// is not a Perfect: it neither grows nor breaks the combo, the Perfect streak or the power meter.
/// Released anywhere else it's an ordinary drop - no edge, no x3 - so the rim's number is
/// exactly what an edge drop pays, never a fraction of it. A release up to
/// <see cref="StabilitySettings.edgeGraceSeconds"/> after the stone leaves a lit rim still
/// counts: the cue reaches the player late, and the rim that flashed must pay.
///
/// What the player sees and hears:
///  - golden strips on the top stone's rim exactly as wide as the band, flashing while
///    releasing counts - on the tower, because that is where the player looks to aim. Each
///    shows what an edge drop there scores;
///  - a rattle and a light haptic tick each time the stone swings into the window;
///  - a pulsing ghost segment on the Tremor meter showing what the drop will cost;
///  - a short callout and a meter pulse when an edge stone lands.
///
/// The window stays shut during the tutorial run, while the temple trembles (that stone must
/// be Perfect), below <see cref="StabilitySettings.edgeMinStackHeight"/>, and whenever the
/// Tremor meter isn't running - the tremor is the price, so no meter means no window.
/// Each side also stays shut (its strip hidden) while the swing can't carry the stone over its
/// sweet spot - a tower drifted far to one side loses the far side until it drifts back.
///
/// First time it opens for a player it runs a short, action-gated intro (FtueState tracks it).
///
/// Self-bootstraps into gameplay scenes like TowerStability; touches no scene or authored UI.
/// </summary>
public class RiskWindow : MonoBehaviour, IRunRewindable
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

    /// <summary>True when releasing right now would be a sweet-spot edge drop (scored as a Serpent's Edge landing).</summary>
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

    // Per side (0 = left, 1 = right): can the swing reach that side's sweet spot? See IsSideReachable.
    private readonly bool[] sideReachable = new bool[2];

    // The side the stone is over right now (-1 left, 1 right), 0 while the window is shut.
    private int openSide;

    // The last rim the player saw lit, for the release grace (see StabilitySettings.edgeGraceSeconds):
    // which stone and when.
    private StackableObject lastLitStone;
    private float lastLitTime = float.NegativeInfinity;

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

    // The guide lane's lesson id: the intro and its follow-up share one run's lesson slot.
    private const string EdgeLessonId = "serpents_edge";

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
        RunRewind.Register(this);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        RunRewind.Unregister(this);

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

    // The band's accuracy cutoffs, kept in order so a mistuned asset can't invert the band or
    // put the sweet spot outside it.
    private float BandOuterAccuracy => settings.edgeBandOuterAccuracy;
    private float SweetSpotAccuracy => Mathf.Max(settings.edgeSweetSpotAccuracy, BandOuterAccuracy);

    private static int Slot(int side) => side < 0 ? 0 : 1;

    private StackableObject TopStone()
    {
        StackableObject top = stackManager != null ? stackManager.GetTopObject() : null;
        return top != null && top.Collider != null ? top : null;
    }

    /// <summary>
    /// Which side's band a stone centred at <paramref name="x"/> would land in (-1 left, 1
    /// right, 0 neither). The whole band is the sweet spot - every edge drop pays the rim's
    /// number - so <paramref name="sweet"/> is true whenever a side is returned. The band is
    /// measured from the top stone, so it follows the tower wherever it drifts. The drop is
    /// straight down, so the stone's x at release is its landing x. A landing that would be a
    /// Perfect anyway never counts, whatever the asset's tuning.
    /// </summary>
    private int EdgeSideAt(StackableObject stone, StackableObject top, float x, out bool sweet)
    {
        sweet = false;
        float accuracy = stone.AccuracyOn(top, x);
        if (stone.IsPerfectAccuracy(accuracy)) return 0;
        if (accuracy > SweetSpotAccuracy || accuracy < BandOuterAccuracy) return 0;

        sweet = true;
        return x >= top.Collider.bounds.center.x ? 1 : -1;
    }

    /// <summary>
    /// World distance from the top stone's centre at which <paramref name="stone"/> lands with
    /// <paramref name="accuracy"/>. Accuracy falls off linearly with distance, so one probe
    /// gives the slope without duplicating StackableObject's width maths.
    /// </summary>
    private static float OffsetForAccuracy(StackableObject stone, StackableObject top, float accuracy)
    {
        const float probe = 0.5f;
        float probeAccuracy = stone.AccuracyOn(top, top.Collider.bounds.center.x + probe);
        float maxDistance = probeAccuracy < 1f ? probe / (1f - probeAccuracy) : probe;
        return (1f - accuracy) * maxDistance;
    }

    /// <summary>
    /// A side is only offered while the swing can carry the stone over its sweet spot. The
    /// band follows the tower, but the swing doesn't, so a tower that drifts far enough
    /// sideways puts one side out of reach - that side hides until the tower comes back.
    /// </summary>
    private bool IsSideReachable(StackableObject stone, StackableObject top, int side, float phase)
    {
        // Where the stone's centre would be with no swing, and how far the swing carries it.
        float restX = stone.ColliderCenterX - spawnerHolder.GetSwingOffsetX(phase);
        float reach = Mathf.Abs(spawnerHolder.GetSwingOffsetX(1f));

        float topX = top.Collider.bounds.center.x;
        float a = topX + side * OffsetForAccuracy(stone, top, SweetSpotAccuracy);
        float b = topX + side * OffsetForAccuracy(stone, top, BandOuterAccuracy);

        return Mathf.Max(a, b) >= restX - reach && Mathf.Min(a, b) <= restX + reach;
    }

    private void Update()
    {
        IsAvailable = ComputeAvailable();
        float phase = spawnerHolder != null ? spawnerHolder.SwingPhase : 0f;

        StackableObject stone = IsAvailable ? HangingStone() : null;
        StackableObject top = stone != null ? TopStone() : null;
        sideReachable[0] = top != null && IsSideReachable(stone, top, -1, phase);
        sideReachable[1] = top != null && IsSideReachable(stone, top, 1, phase);

        bool sweet = false;
        int side = top != null ? EdgeSideAt(stone, top, stone.ColliderCenterX, out sweet) : 0;
        IsOpen = side != 0 && sideReachable[Slot(side)];
        IsSweet = IsOpen && sweet;
        openSide = IsOpen ? side : 0;

        if (IsOpen)
        {
            lastLitStone = stone;
            lastLitTime = Time.unscaledTime;
        }

        // The band is the sweet spot, so entering it is the timing cue: the rim flashes
        // (UpdateUI) and the rattle and haptic tick fire together.
        if (IsOpen && !wasOpen) PlayEntryCue();
        wasOpen = IsOpen;

        // Waits for a run whose lesson slot is still free; CanTeach is cheap enough per frame.
        // Not while both sides are shut - the lesson would point at rims that aren't there.
        if (IsAvailable && (sideReachable[0] || sideReachable[1]) &&
            !introActive && !FtueState.HasSeenEdgeIntro && GuideLane.CanTeach(EdgeLessonId))
        {
            BeginIntro();
        }

        TowerStability stability = TowerStability.Instance;
        if (stability != null) stability.SetEdgePreview(IsOpen ? settings.edgeStrain : 0f);

        UpdateUI(stone, top);
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

        var block = dropped.GetComponent<StackableObject>();
        StackableObject top = block != null && ComputeAvailable() ? TopStone() : null;

        // Reachability is taken from the last Update - the rims the player was looking at.
        // Re-deriving it here would read SwingPhase after SpawnerHolder's drop handler has
        // already randomised it.
        int side = top != null ? EdgeSideAt(block, top, block.ColliderCenterX, out _) : 0;
        bool edge = side != 0 && sideReachable[Slot(side)];

        // Grace: the cue reaches the player late, so a release just after the stone left a lit
        // rim still pays what that rim showed. Same stone only - the rim was lit for it.
        bool graced = false;
        if (!edge && top != null && block == lastLitStone &&
            Time.unscaledTime - lastLitTime <= settings.edgeGraceSeconds)
        {
            edge = true;
            graced = true;
        }

        // The whole band is the sweet spot, so every edge drop - graced or not - scores as an edge landing.
        bool sweet = edge;

        if (introActive)
        {
            introDrops++;
            if (!edge && introDrops >= settings.edgeIntroMaxDrops) CompleteIntro("ignored");
        }

        if (!edge) return;

        block.MarkEdgeDrop(settings.edgeScoreMultiplier, sweet);
        pendingEdgeComboBefore = gameManager.CurrentCombo;
        OnEdgeDrop?.Invoke(block);

        HapticFeedback.Trigger(HapticFeedback.HapticType.Light);

        var data = RunEventData();
        data["tremor_before"] = TowerStability.Instance != null ? TowerStability.Instance.Tremor : 0f;
        data["sweet_spot"] = sweet;
        data["release_accuracy"] = block.AccuracyOn(top, block.ColliderCenterX);
        data["graced"] = graced;
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
        // label. Only the lesson's follow-up speaks, in the guide lane.
        if (!introActive) return;

        // The trembling warning outranks the follow-up. If the meter hasn't processed this
        // landing yet, its warning simply replaces the banner a moment later.
        if (stability == null || !stability.IsTrembling)
        {
            // Only a scored edge landing (the sweet spot) earns the xN; anything else was an
            // ordinary landing, so the follow-up teaches the timing instead of cheering.
            bool perfect = block.ScoredAsEdge;
            GuideLane.TryTeach(EdgeLessonId,
                LocalizationManager.Get(perfect ? "edge_intro_followup_title" : "edge_intro_early_title", FormatMultiplier()),
                LocalizationManager.Get(perfect ? "edge_intro_followup_body" : "edge_intro_early_body", FormatMultiplier()),
                RunOverlayUI.Gold,
                FollowupBannerHold);
        }

        CompleteIntro("taken");
    }

    // ---- First-time intro ----

    private void BeginIntro()
    {
        if (!GuideLane.TryTeach(EdgeLessonId,
                LocalizationManager.Get("edge_intro_title"),
                LocalizationManager.Get("edge_intro_body", FormatMultiplier()),
                RunOverlayUI.Gold,
                IntroBannerHold))
        {
            return;
        }

        introActive = true;
        introDrops = 0;

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

        RunEdgeLanded = 0;
        RunEdgeBonusPoints = 0;
        RunEdgeCombosBroken = 0;
        pendingEdgeComboBefore = 0;
        projectionKey[0] = projectionKey[1] = int.MinValue;
        sideReachable[0] = sideReachable[1] = false;
        openSide = 0;
        lastLitStone = null;
        lastLitTime = float.NegativeInfinity;

        if (TowerStability.Instance != null) TowerStability.Instance.SetEdgePreview(0f);
        if (uiRoot != null) uiRoot.SetActive(false);
    }

    // ---- Tzolk'in Rewind ----

    private struct RewindState
    {
        public int landed;
        public int bonusPoints;
        public int combosBroken;
    }

    object IRunRewindable.CaptureRewindState() => new RewindState
    {
        landed = RunEdgeLanded,
        bonusPoints = RunEdgeBonusPoints,
        combosBroken = RunEdgeCombosBroken
    };

    /// <summary>The result-card receipt forgets rewound edge landings; the rim is re-projected on the lower tower.</summary>
    void IRunRewindable.RestoreRewindState(object state)
    {
        if (!(state is RewindState s)) return;

        RunEdgeLanded = s.landed;
        RunEdgeBonusPoints = s.bonusPoints;
        RunEdgeCombosBroken = s.combosBroken;
        pendingEdgeComboBefore = 0;
        projectionKey[0] = projectionKey[1] = int.MinValue;
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

    private void UpdateUI(StackableObject stone, StackableObject top)
    {
        if (uiRoot == null || view == null) return;

        if (uiRoot.activeSelf != IsAvailable) uiRoot.SetActive(IsAvailable);
        if (!IsAvailable || canvasRect == null) return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // The strips sit on the top stone's rim over each side's band, exactly as wide as it,
        // so they move with the tower and never promise the value outside it.
        if (stone == null || top == null)
        {
            uiRoot.SetActive(false);
            return;
        }

        float topX = top.Collider.bounds.center.x;
        float rimY = top.Collider.bounds.max.y + settings.edgeZoneSurfaceOffset;
        float innerOffset = OffsetForAccuracy(stone, top, SweetSpotAccuracy);
        float outerOffset = OffsetForAccuracy(stone, top, BandOuterAccuracy);
        float bandOffset = (innerOffset + outerOffset) * 0.5f;
        float halfWidth = Mathf.Abs(outerOffset - innerOffset) * 0.5f;
        float emphasis = introActive ? 1f : 0f;
        int safePoints = gameManager.PreviewPoints(stone.PreviewBaseScore(1f, 1f), 1f);

        for (int side = -1; side <= 1; side += 2)
        {
            bool reachable = sideReachable[Slot(side)];
            view.SetZoneVisible(side, reachable);
            if (!reachable) continue;

            float landing = topX + side * bandOffset;
            Vector2 a = WorldToCanvas(new Vector3(landing - halfWidth, rimY, top.transform.position.z));
            Vector2 b = WorldToCanvas(new Vector3(landing + halfWidth, rimY, top.transform.position.z));

            UpdateProjection(side, stone, top, landing, safePoints);

            float width = Mathf.Abs(b.x - a.x);
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
    /// Works out what a sweet-spot edge drop on <paramref name="side"/> would score - the top
    /// tier at the current combo multiplier (it doesn't grow the combo) and the run multipliers -
    /// and hands the view its caption and colour. That's the prize the player is timing for; missing the sweet spot scores the real
    /// (off-centre) landing instead, which the intro explains rather than a second number here.
    /// The caption string is only rebuilt when the value behind it changes.
    /// </summary>
    private void UpdateProjection(int side, StackableObject stone, StackableObject top, float landingX, int safePoints)
    {
        // Off the stone entirely is a miss - the sweet spot only upgrades a landing on it.
        bool miss = stone.AccuracyOn(top, landingX) <= 0f;
        int points = miss ? 0 : gameManager.PreviewEdgePoints(stone.PreviewBaseScore(1f, settings.edgeScoreMultiplier));

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
