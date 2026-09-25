using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The power meter: Perfect landings charge it, and a full meter lets the player fire the
/// equipped power with a tap.
///
/// Replaces the mid-run boon picker as the thing that gives a run decisions — but as a
/// physical event the player chooses the moment of, which reads in a shared clip where a
/// score multiplier never did. Charges come from play only; nothing here is ever sold.
///
/// Powers (what each does lives here; look and unlock live on <see cref="PowerDefinition"/>):
///  - Jaguar Slam: knocks the top stone back onto the centre of the one below.
///  - Quetzal Feather: slow motion — swing and fall — for the next few drops.
///  - Obsidian Blade: cuts off the part of the top stone hanging past the one below.
///  - Kukulkan's Call: fires the Kukulkan shift on demand.
///
/// Rules (all in <see cref="PowerSettings"/>):
///  - only Perfects charge; Good/Poor leave the meter alone (Good already breaks the combo);
///  - one stored charge, one equipped power, picked on the menu before the run
///    (see <see cref="PowerLoadoutScreen"/>) and kept for Retry / Next Level;
///  - the button arms after a short delay, so the tap that placed a stone can't fire it;
///  - tapping the button never drops a stone: it sits on a raycasting canvas, and
///    InputManager drops nothing when the press is over UI;
///  - hidden until the tutorial is done, and until a power is unlocked.
///
/// Self-bootstraps into gameplay scenes and builds its UI on its own overlay canvases,
/// so no scene or authored UI prefab is touched.
/// </summary>
public class PowerSystem : MonoBehaviour
{
    /// <summary>Authored meter prefab that replaces the code-built button when present.</summary>
    public const string MeterPrefabResourcePath = "UI/PowerMeter";

    private static PowerSystem instance;

    public static PowerSystem Instance => instance;

    /// <summary>0..maxStoredCharges. Whole numbers are stored charges.</summary>
    public float Charge => charge;

    public bool IsReady => charge >= 1f;

    /// <summary>The power the meter fires this run.</summary>
    public PowerId Equipped => equipped;

    public event System.Action<PowerId> OnPowerUsed;

    private PowerSettings settings;
    private GameManager gameManager;
    private StackManager stackManager;
    private LevelManager levelManager;
    private ObjectSpawner objectSpawner;
    private UIManager uiManager;
    private CameraController cameraController;
    private GameSoundManager soundManager;

    private float charge;
    private float armedAt;
    private bool unlocked; // cached per run: read from PlayerPrefs, not every frame
    private PowerId equipped;
    private bool slamming;

    // Quetzal Feather
    private int quetzalDropsLeft;
    private readonly List<Rigidbody2D> featherStones = new List<Rigidbody2D>();
    private readonly List<float> featherGravity = new List<float>();
    private float tintAlpha;
    private float quetzalShown; // eased remaining fraction the medallion ring shows

    private bool introActive;
    private int introDrops;
    private float highlightUntil;

    private GameObject uiRoot;
    private PowerMeterView view;
    private Image tint;

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

        var go = new GameObject("PowerSystem");
        instance = go.AddComponent<PowerSystem>();
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
        settings = PowerSettings.Current;

        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        uiManager = DependencyRegistry.Find<UIManager>();
        cameraController = DependencyRegistry.Find<CameraController>();
        soundManager = DependencyRegistry.Find<GameSoundManager>();

        if (stackManager != null) stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        if (objectSpawner != null) objectSpawner.OnObjectDropped += OnObjectDropped;
        if (levelManager != null) levelManager.OnLevelCompleted += OnLevelCompleted;

        if (gameManager != null)
        {
            gameManager.OnGameStart += ResetRun;
            gameManager.OnGameRestart += ResetRun;
            gameManager.OnGameOver += OnGameOver;
        }

        BuildUI();
        ResetRun();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        EndQuetzal();

        if (stackManager != null) stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        if (objectSpawner != null) objectSpawner.OnObjectDropped -= OnObjectDropped;
        if (levelManager != null) levelManager.OnLevelCompleted -= OnLevelCompleted;

        if (gameManager != null)
        {
            gameManager.OnGameStart -= ResetRun;
            gameManager.OnGameRestart -= ResetRun;
            gameManager.OnGameOver -= OnGameOver;
        }
    }

    // ---- Rules ----

    /// <summary>The meter belongs on screen in this mode, for this player.</summary>
    private bool AppliesToPlayer()
    {
        if (settings == null || gameManager == null) return false;
        if (!settings.AppliesTo(gameManager.CurrentGameMode)) return false;
        if (settings.suppressDuringTutorial && FtueState.NeedsTutorial) return false;
        return unlocked;
    }

    /// <summary>The meter is counting landings right now.</summary>
    private bool IsLive()
    {
        if (!AppliesToPlayer()) return false;
        if (!gameManager.IsGameActive || gameManager.IsGameOver) return false;
        if (levelManager != null && levelManager.IsLevelComplete) return false;
        return true;
    }

    private bool IsArmed => IsReady && Time.unscaledTime >= armedAt;

    /// <summary>
    /// Whether a tap would fire right now. A pause, a picker or a power already in motion make
    /// every power unavailable; each power adds what it needs (see <see cref="PowerCanFire"/>).
    /// Trembling does not: a power there is a dramatic save, and the next landing still has
    /// to be Perfect.
    /// </summary>
    public bool CanFire
    {
        get
        {
            if (!IsLive() || !IsArmed || slamming) return false;
            if (uiManager != null && uiManager.IsPaused) return false;
            if (BoonSystem.IsChoosing) return false;
            if (Time.timeScale == 0f) return false;
            return PowerCanFire(equipped);
        }
    }

    private bool PowerCanFire(PowerId id)
    {
        bool stoneInAir = objectSpawner != null && objectSpawner.WaitingForLanding;
        int height = stackManager != null ? stackManager.GetStackCount() : 0;

        switch (id)
        {
            case PowerId.QuetzalFeather:
                // Works on the drops to come, so a stone in the air is fine.
                return !QuetzalActive;

            case PowerId.ObsidianBlade:
                return height >= 2 && !stoneInAir
                       && stackManager.GetTopOverhang() >= settings.bladeMinOverhang;

            case PowerId.KukulkansCall:
                return height >= 2 && !stoneInAir;

            case PowerId.JaguarSlam:
            default:
                return height >= 2 && !stoneInAir;
        }
    }

    private void OnObjectAddedToStack(StackableObject block)
    {
        if (block == null) return;

        // Read before the landing clears its own stone: the last feathered landing is still
        // part of the feather.
        bool featherLanding = QuetzalActive;

        RestoreFeatherGravity(block.GetComponent<Rigidbody2D>());

        if (!IsLive()) return;

        // The meter doesn't build while the feather is active: the medallion is busy showing
        // how long it has left, and slow-mo Perfects would refill it too cheaply.
        if (featherLanding) return;

        // The foundation lands on the ground and has nothing to be centred on.
        if (stackManager.GetStackCount() <= 1) return;

        bool perfect = block.LandingAccuracy >= gameManager.PerfectThreshold;
        bool wasReady = IsReady;

        if (perfect)
        {
            charge = Mathf.Min(settings.maxStoredCharges, charge + 1f / settings.perfectsToFill);

            // Float steps (0.25 x 4) can land a hair under 1.
            if (charge > 0.999f && charge < 1f) charge = 1f;
        }
        else if (settings.nonPerfectDrains && !IsReady)
        {
            charge = 0f;
        }

        if (!wasReady && IsReady) OnMeterFilled();
    }

    private void OnMeterFilled()
    {
        armedAt = Time.unscaledTime + settings.armDelaySeconds;
        HapticFeedback.Trigger(HapticFeedback.HapticType.Light);

        GameAnalytics.Track("power_meter_full", RunEventData());

        if (!introActive && !PowerUnlocks.HasSeenIntro(equipped)) BeginIntro();
    }

    // ---- Firing ----

    private void OnButtonClicked()
    {
        if (!CanFire) return;
        Fire(equipped);
    }

    private void Fire(PowerId id)
    {
        PowerDefinition def = settings.Get(id);

        // Read before the power acts: the event describes the tower the player chose to save.
        var data = RunEventData();
        data["power"] = id.ToString();
        data["tremor"] = TowerStability.Instance != null ? TowerStability.Instance.Tremor : 0f;
        data["trembling"] = TowerStability.Instance != null && TowerStability.Instance.IsTrembling;

        bool fired;
        switch (id)
        {
            case PowerId.QuetzalFeather: fired = FireQuetzalFeather(def); break;
            case PowerId.ObsidianBlade: fired = FireObsidianBlade(def, data); break;
            case PowerId.KukulkansCall: fired = FireKukulkansCall(def); break;
            case PowerId.JaguarSlam:
            default: fired = FireJaguarSlam(def); break;
        }

        if (!fired) return;

        charge = Mathf.Max(0f, charge - 1f);

        // No score of its own and no Perfect: every stone keeps the landing it earned.
        RunBanner.Show(LocalizationManager.Get(def.nameKey), def.accentColor, 0.9f, settings.introBannerYOffset);

        GameAnalytics.Track("power_used", data);
        OnPowerUsed?.Invoke(id);

        if (introActive) CompleteIntro("used");
    }

    private bool FireJaguarSlam(PowerDefinition def)
    {
        if (!stackManager.SlamTopStone(settings.slamDuration, settings.slamHop, () => OnSlamImpact(def)))
        {
            return false;
        }

        slamming = true;
        return true;
    }

    /// <summary>The paw lands: every channel at once, so it reads in a silent clip too.</summary>
    private void OnSlamImpact(PowerDefinition def)
    {
        slamming = false;

        if (cameraController != null)
        {
            cameraController.Shake(settings.slamShake);
            if (settings.slamZoom > 0f) cameraController.PunchZoom(settings.slamZoom, 0.35f);
        }

        GameFeelManager.HitStop(settings.slamHitStop);
        GameFeelManager.Flash(settings.slamFlash, 0.25f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Heavy);
        PlayFireSound(def);
    }

    /// <summary>
    /// Slow motion for the next few drops: the swing slows through <see cref="SwingModifiers"/>
    /// and each stone dropped under the feather falls with less gravity. Game time itself is
    /// untouched, so hit-stop, pause and the Kukulkan slow-mo keep working as they always have.
    /// </summary>
    private bool FireQuetzalFeather(PowerDefinition def)
    {
        quetzalDropsLeft = settings.quetzalDrops;
        quetzalShown = 1f; // the full ready ring turns straight into the full duration ring
        SwingModifiers.SpeedScale = settings.quetzalSwingScale;

        Color flash = def.accentColor;
        flash.a = 0.3f;
        GameFeelManager.Flash(flash, 0.35f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
        PlayFireSound(def);
        return true;
    }

    private bool FireObsidianBlade(PowerDefinition def, Dictionary<string, object> data)
    {
        if (!stackManager.SliceTopOverhang(settings.bladeMinOverhang, out float removed)) return false;

        data["cut_width"] = removed;

        if (cameraController != null) cameraController.Shake(settings.bladeShake);
        GameFeelManager.HitStop(settings.bladeHitStop);
        GameFeelManager.Flash(settings.bladeFlash, 0.2f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Heavy);
        PlayFireSound(def);
        return true;
    }

    /// <summary>
    /// The ordinary Kukulkan shift, on demand: same straighten, slow-mo, flash and sting as the
    /// earned one, so it never looks like a different thing depending on how it was triggered.
    /// </summary>
    private bool FireKukulkansCall(PowerDefinition def)
    {
        if (gameManager == null || !gameManager.IsGameActive || gameManager.IsGameOver) return false;

        gameManager.TriggerKukulkanShift();
        HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
        PlayFireSound(def);
        return true;
    }

    private void PlayFireSound(PowerDefinition def)
    {
        if (soundManager != null && def.fireSound != null)
        {
            soundManager.PlaySound(def.fireSound, def.fireSoundVolume);
        }
    }

    // ---- Quetzal Feather ----

    /// <summary>
    /// From the tap until the last feathered stone lands. A missed stone ends the run, which
    /// clears the list through <see cref="EndQuetzal"/>; destroyed stones are pruned anyway so
    /// the meter can never stay locked.
    /// </summary>
    private bool QuetzalActive
    {
        get
        {
            if (quetzalDropsLeft > 0) return true;
            for (int i = featherStones.Count - 1; i >= 0; i--)
            {
                if (featherStones[i] != null) continue;
                featherStones.RemoveAt(i);
                featherGravity.RemoveAt(i);
            }
            return featherStones.Count > 0;
        }
    }

    /// <summary>Feathered drops left, 0..1, for the medallion ring.</summary>
    private float QuetzalRemaining => settings.quetzalDrops > 0
        ? Mathf.Clamp01((float)quetzalDropsLeft / settings.quetzalDrops)
        : 0f;

    private void OnObjectDropped(GameObject dropped)
    {
        if (quetzalDropsLeft > 0 && dropped != null)
        {
            var rb = dropped.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                featherStones.Add(rb);
                featherGravity.Add(rb.gravityScale);
                rb.gravityScale *= settings.quetzalFallScale;
            }

            quetzalDropsLeft--;
            // The swing speeds back up as soon as the last feathered stone leaves it.
            if (quetzalDropsLeft <= 0) SwingModifiers.SpeedScale = 1f;
        }

        if (!introActive) return;

        introDrops++;
        if (introDrops >= settings.introMaxDrops) CompleteIntro("ignored");
    }

    /// <summary>A feathered stone has landed: it settles with normal weight from here on.</summary>
    private void RestoreFeatherGravity(Rigidbody2D rb)
    {
        if (rb == null) return;
        int i = featherStones.IndexOf(rb);
        if (i < 0) return;

        rb.gravityScale = featherGravity[i];
        featherStones.RemoveAt(i);
        featherGravity.RemoveAt(i);
    }

    private void EndQuetzal()
    {
        quetzalDropsLeft = 0;
        SwingModifiers.SpeedScale = 1f;

        for (int i = 0; i < featherStones.Count; i++)
        {
            if (featherStones[i] != null) featherStones[i].gravityScale = featherGravity[i];
        }
        featherStones.Clear();
        featherGravity.Clear();
    }

    // ---- First-time intro ----

    /// <summary>
    /// The first full meter with a power explains it: a banner naming the power and what it
    /// does, and the button pulsing hard. Non-blocking — it completes when the player uses the
    /// power, or after a few drops without, like the Serpent's Edge intro. The arm delay on the
    /// button means the tap already on its way can't spend the charge unread.
    /// </summary>
    private void BeginIntro()
    {
        PowerDefinition def = settings.Get(equipped);

        introActive = true;
        introDrops = 0;
        highlightUntil = Time.unscaledTime + 3.4f;

        RunBanner.Show(
            LocalizationManager.Get("power_intro_title", LocalizationManager.Get(def.nameKey)),
            // {0} is the Quetzal Feather's drop count; the other descriptions have no slot.
            LocalizationManager.Get(def.descriptionKey, settings.quetzalDrops),
            def.accentColor,
            3.4f,
            settings.introBannerYOffset);

        var data = RunEventData();
        data["power"] = def.id.ToString();
        GameAnalytics.Track("power_intro_shown", data);
    }

    private void CompleteIntro(string outcome)
    {
        introActive = false;
        highlightUntil = 0f;
        PowerUnlocks.MarkIntroSeen(equipped);

        GameAnalytics.Track("power_intro_completed", new Dictionary<string, object>
        {
            { "power", equipped.ToString() },
            { "outcome", outcome }
        });
    }

    // ---- Unlocking ----

    private void OnLevelCompleted(int stars, int score, bool showCodexPopup)
    {
        EndQuetzal();

        if (stars <= 0 || levelManager == null || levelManager.CurrentLevel == null) return;

        int levelNumber = levelManager.CurrentLevel.levelNumber;
        foreach (PowerId id in PowerUnlocks.PowersUnlockedBy(levelNumber))
        {
            if (!PowerUnlocks.Unlock(id)) continue;

            // A new power is equipped straight away, so the next run tries it.
            PowerUnlocks.Equip(id);
            unlocked = PowerUnlocks.HasEquippedPower;

            GameAnalytics.Track("power_unlocked", new Dictionary<string, object>
            {
                { "power", id.ToString() },
                { "level", levelNumber }
            });

            // After the capstone's flash has peaked, before the result panel arrives.
            StartCoroutine(AnnounceUnlock(settings.Get(id)));
        }
    }

    private IEnumerator AnnounceUnlock(PowerDefinition def)
    {
        yield return new WaitForSecondsRealtime(0.9f);

        RunBanner.Show(
            LocalizationManager.Get("power_unlocked_title", LocalizationManager.Get(def.nameKey)),
            LocalizationManager.Get("power_unlocked_body"),
            def.accentColor,
            2.4f);
    }

    // ---- Run lifecycle ----

    private void ResetRun()
    {
        charge = 0f;
        armedAt = 0f;
        slamming = false;
        EndQuetzal();

        unlocked = PowerUnlocks.HasEquippedPower;
        equipped = PowerUnlocks.Equipped;

        // An unfinished intro starts over next run rather than silently counting as seen.
        introActive = false;
        introDrops = 0;
        highlightUntil = 0f;

        RefreshContent();
    }

    private void OnGameOver()
    {
        introActive = false;
        highlightUntil = 0f;
        EndQuetzal();
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

    private void Update()
    {
        if (uiRoot == null) return;

        bool visible = IsLive();
        if (uiRoot.activeSelf != visible) uiRoot.SetActive(visible);

        UpdateTint();

        if (!visible || view == null) return;

        // While the feather is active the medallion is its timer: the ring drains one step per
        // feathered drop instead of showing charge.
        if (QuetzalActive)
        {
            quetzalShown = Mathf.MoveTowards(quetzalShown, QuetzalRemaining, Time.unscaledDeltaTime * 2.5f);
            view.SetActiveState(quetzalShown);
            return;
        }

        float highlight = Mathf.Clamp01((highlightUntil - Time.unscaledTime) * 2f);
        float progress = IsReady ? 1f : charge - Mathf.Floor(charge);
        view.SetState(progress, IsReady, CanFire, highlight);
    }

    /// <summary>The Quetzal Feather's screen-edge tint, eased in and out.</summary>
    private void UpdateTint()
    {
        if (tint == null) return;

        tintAlpha = Mathf.MoveTowards(tintAlpha, quetzalDropsLeft > 0 ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        bool on = tintAlpha > 0f;
        if (tint.gameObject.activeSelf != on) tint.gameObject.SetActive(on);
        if (!on) return;

        Color c = settings.quetzalTint;
        float breathe = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.4f);
        c.a *= tintAlpha * breathe;
        tint.color = c;
    }

    private void RefreshContent()
    {
        if (view == null || settings == null) return;

        PowerDefinition def = settings.Get(equipped);
        view.SetContent(LocalizationManager.Get(def.nameKey), def.icon, def.accentColor);
    }

    private void BuildUI()
    {
        uiRoot = new GameObject("PowerMeterUI");
        uiRoot.transform.SetParent(transform, false);

        if (!BuildFromPrefab()) BuildInCode();

        view.Button.onClick.AddListener(OnButtonClicked);
        uiRoot.SetActive(false);

        BuildTint();
    }

    /// <summary>A full-screen edge vignette on its own non-interactive canvas, under the HUD's taps.</summary>
    private void BuildTint()
    {
        var host = new GameObject("PowerTint");
        host.transform.SetParent(transform, false);
        RunOverlayUI.CreateCanvas(host, settings.canvasSortingOrder - 20, interactive: false);

        RectTransform rect = RunOverlayUI.CreateChild("Vignette", host.transform);
        RunOverlayUI.Stretch(rect);
        tint = rect.gameObject.AddComponent<Image>();
        tint.sprite = VignetteSprite();
        tint.raycastTarget = false;
        tint.gameObject.SetActive(false);
    }

    private static Sprite VignetteSprite()
    {
        Sprite authored = Resources.Load<Sprite>("UI/Powers/Power_QuetzalFeather_Vignette");
        if (authored != null) return authored;

        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f;
                float ny = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny) / 1.41421f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r));
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Instantiates the authored meter, if there is one. Returns false when no usable prefab
    /// exists, so the code-built button is used instead.
    /// </summary>
    private bool BuildFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(MeterPrefabResourcePath);
        if (prefab == null) return false;

        var instantiated = Instantiate(prefab, uiRoot.transform, false);
        view = instantiated.GetComponentInChildren<PowerMeterView>();

        if (view == null || !view.IsUsable)
        {
            Debug.LogWarning($"[PowerSystem] Resources/{MeterPrefabResourcePath} has no usable " +
                             "PowerMeterView (needs Button + Fill) - using the code-built meter instead.");
            // Hide first: Destroy is deferred, and a half-built canvas shouldn't flash for a frame.
            instantiated.SetActive(false);
            Destroy(instantiated);
            view = null;
            return false;
        }

        return true;
    }

    private void BuildInCode()
    {
        view = PowerMeterView.BuildDefault(uiRoot, settings);
    }
}
