using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Offers the player a choice of blessings every few blocks.
///
/// This is the piece that gives the run decisions rather than only execution. Every N
/// blocks the game pauses and puts three boons up; whichever is taken bends the rest of
/// the run. Two players who reach the same height will have got there differently.
///
/// Self-bootstraps and builds its picker in code on its own canvas, so no existing scene,
/// prefab or UIManager field is touched. With no BoonSettings asset in Resources nothing
/// ever appears.
/// </summary>
public class BoonSystem : MonoBehaviour
{
    private static BoonSystem instance;

    /// <summary>Authored prefab that replaces the code-built picker when present.</summary>
    public const string PickerPrefabResourcePath = "UI/BoonPicker";

    /// <summary>Authored prefab that replaces the code-built explainer when present.</summary>
    public const string IntroPrefabResourcePath = "UI/BoonIntro";

    /// <summary>True while the picker is up, so other systems can hold off.</summary>
    public static bool IsChoosing { get; private set; }

    private BoonSettings settings;
    private GameManager gameManager;
    private StackManager stackManager;
    private UIManager uiManager;

    private Canvas pickerCanvas;
    private GameObject pickerRoot;
    private readonly List<Button> optionButtons = new List<Button>();
    private Coroutine openRoutine;
    private Coroutine armRoutine;

    // The offers rolled for the picker that is opening. Held here so the first-run intro
    // can sit in front of them without the roll changing between the two screens.
    private List<BoonId> pendingOffers;

    // Heights already offered at, so a stack that loses and regains a block can't
    // re-trigger the same offer.
    private readonly HashSet<int> offeredHeights = new HashSet<int>();

    // Heights already warned at, for the same reason.
    private readonly HashSet<int> telegraphedHeights = new HashSet<int>();

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

        var go = new GameObject("BoonSystem");
        instance = go.AddComponent<BoonSystem>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        settings = Resources.Load<BoonSettings>(BoonSettings.ResourcePath);
    }

    private void Start()
    {
        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        uiManager = DependencyRegistry.Find<UIManager>();

        if (gameManager != null)
        {
            gameManager.OnGameStart += OnGameStart;
            gameManager.OnGameRestart += OnRunEnded;
            gameManager.OnGameOver += OnRunEnded;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack += OnObjectAddedToStack;
        }
    }

    private void OnGameStart()
    {
        offeredHeights.Clear();
        telegraphedHeights.Clear();
        ActiveBoons.ResetRun();

        // resumeGame: true because a run starting must never inherit a frozen clock. In
        // practice the picker is already closed by the time this fires, but getting it
        // wrong would leave the game stuck at timeScale 0 with no way out.
        ClosePicker(resumeGame: true);
    }

    private void OnRunEnded()
    {
        // A run can end while the picker is open only in odd cases (a modifier ending the
        // run on the landing that triggered the offer), but if it does, the picker must
        // not be left holding time at zero over the game-over screen.
        ClosePicker(resumeGame: true);
    }

    private void OnObjectAddedToStack(StackableObject stackableObject)
    {
        if (!BoonsActive()) return;
        if (IsChoosing || openRoutine != null) return;
        if (stackManager == null) return;

        int height = stackManager.GetStackCount();

        if (settings.ShouldOfferAt(height))
        {
            if (offeredHeights.Contains(height)) return;

            offeredHeights.Add(height);
            openRoutine = StartCoroutine(OpenAfterDelay());
            return;
        }

        TryTelegraph(height);
    }

    /// <summary>
    /// Warns that Kukulkan is about to interrupt, once per height.
    ///
    /// A picker that appears unannounced turns the run's rhythm against the player: they
    /// are mid-tap-cadence, the game stops, and the tap already on its way lands on a card
    /// they never read. The warning gives the offer a run-up, so stopping is something the
    /// player is expecting rather than something that happens to them.
    ///
    /// The countdown is derived from <see cref="BoonSettings.BlocksUntilOffer"/> rather
    /// than tracked separately, so retuning the cadence retunes the warning with it.
    /// </summary>
    private void TryTelegraph(int height)
    {
        if (settings.telegraphLeadBlocks <= 0) return;

        int remaining = settings.BlocksUntilOffer(height);
        if (remaining <= 0 || remaining > settings.telegraphLeadBlocks) return;
        if (!telegraphedHeights.Add(height)) return;

        string text = remaining == 1
            ? LocalizationManager.Get("boon_telegraph_next")
            : LocalizationManager.Get("boon_telegraph_soon", remaining);

        RunBanner.Show(text, RunOverlayUI.Gold, holdSeconds: 1.1f);
    }

    /// <summary>
    /// Waits out any hit-stop or Kukulkan slow-motion before freezing time.
    ///
    /// Without this the picker can open during GameFeelManager's time-scale dip, whose
    /// coroutine would then restore timeScale to 1 underneath the open picker and let the
    /// game run on behind it.
    /// </summary>
    private IEnumerator OpenAfterDelay()
    {
        yield return new WaitForSecondsRealtime(settings.openDelaySeconds);

        openRoutine = null;

        // Conditions can change during the delay - the run may have ended, or the player
        // may have opened the pause menu.
        if (!BoonsActive()) yield break;
        if (uiManager != null && uiManager.IsPaused) yield break;

        OpenPicker();
    }

    private bool BoonsActive()
    {
        if (settings == null || gameManager == null) return false;
        if (!settings.AppliesTo(gameManager.CurrentGameMode)) return false;
        if (!gameManager.IsGameActive || gameManager.IsGameOver) return false;

        // Gated on the tutorial, not on FtueState.IsInFtue. IsInFtue stays true until a
        // temple is completed, and only LevelManager can mark that - so a player who opens
        // Infinite Stacker and stays there would never leave the FTUE window and would
        // never be offered a boon, let alone the explainer that introduces them.
        if (settings.suppressDuringTutorial && FtueState.NeedsTutorial) return false;

        // Never interrupt a level that's already been completed.
        var levelManager = DependencyRegistry.Find<LevelManager>();
        if (levelManager != null && levelManager.IsLevelComplete) return false;

        return true;
    }

    // ---- Picker ----

    private void OpenPicker()
    {
        List<BoonId> offers = PickOffers(settings.offerCount);
        if (offers.Count == 0) return;

        pendingOffers = offers;

        IsChoosing = true;
        Time.timeScale = 0f;

        // The very first offer arrives with no explanation of what a boon is or why the
        // game stopped. Explain it once, in front of the cards, then never again.
        if (settings.showIntroOnFirstOffer && !FtueState.HasSeenBoonIntro)
        {
            BuildIntro();
            ArmAfterDelay();

            GameAnalytics.Track("boon_intro_shown", new Dictionary<string, object>
            {
                { "height", stackManager != null ? stackManager.GetStackCount() : 0 },
                { "mode", gameManager != null ? gameManager.CurrentGameMode.ToString() : "unknown" }
            });
            return;
        }

        ShowOffers();
    }

    private void ShowOffers()
    {
        BuildPicker(pendingOffers);
        ArmAfterDelay();

        GameAnalytics.Track("boon_offered", new Dictionary<string, object>
        {
            { "height", stackManager != null ? stackManager.GetStackCount() : 0 },
            { "options", string.Join(",", pendingOffers) }
        });
    }

    private void OnIntroAcknowledged()
    {
        FtueState.MarkBoonIntroSeen();
        GameAnalytics.Track("boon_intro_acknowledged");

        ShowOffers();
    }

    // ---- Arming ----

    /// <summary>
    /// Holds every control on the surface untappable for a beat after it appears.
    ///
    /// The picker opens on the back of a tap, and on a stacker the next tap is usually
    /// already coming. Without this, that tap lands on whichever card sits under the
    /// player's thumb and the choice is made before the screen has been read.
    /// </summary>
    private void ArmAfterDelay()
    {
        if (armRoutine != null) StopCoroutine(armRoutine);
        armRoutine = StartCoroutine(ArmRoutine());
    }

    private IEnumerator ArmRoutine()
    {
        SetOptionsInteractable(false);

        // Realtime: the clock is stopped while the picker is up.
        yield return new WaitForSecondsRealtime(settings.armDelaySeconds);

        SetOptionsInteractable(true);
        armRoutine = null;
    }

    private void SetOptionsInteractable(bool interactable)
    {
        for (int i = 0; i < optionButtons.Count; i++)
        {
            if (optionButtons[i] != null) optionButtons[i].interactable = interactable;
        }
    }

    /// <summary>
    /// Picks distinct boons at random, up to <paramref name="count"/>.
    ///
    /// Drawn from <see cref="RunRandom"/> so a level or a Daily offers the same blessings
    /// at the same heights to every attempt and every player — the offer is part of what
    /// makes a run learnable, and re-rolling it would put the interesting choice back
    /// under luck.
    /// </summary>
    private List<BoonId> PickOffers(int count)
    {
        var pool = new List<BoonId>(BoonDefinition.All);
        var offers = new List<BoonId>();

        count = Mathf.Min(count, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int index = RunRandom.Range(0, pool.Count);
            offers.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return offers;
    }

    /// <summary>
    /// The one-time explanation that precedes a player's first offer: what just stopped
    /// the run, what a blessing is, and that the clock is not running while they read.
    /// </summary>
    private void BuildIntro()
    {
        DestroyPicker();

        pickerRoot = new GameObject("BoonIntro");
        pickerRoot.transform.SetParent(transform, false);

        if (BuildIntroFromPrefab()) return;

        BuildIntroInCode();
    }

    /// <summary>
    /// Instantiates the authored explainer, if there is one, and lets it fill itself in.
    /// Returns false when no usable prefab exists, so the caller falls back to code.
    /// </summary>
    private bool BuildIntroFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(IntroPrefabResourcePath);
        if (prefab == null) return false;

        // The prefab carries its own Canvas, so it renders correctly parented to the
        // otherwise-empty host object that owns the lifetime.
        var authored = Instantiate(prefab, pickerRoot.transform, false);
        var view = authored.GetComponent<BoonIntroView>();

        if (view == null)
        {
            Debug.LogWarning($"[Boon] Resources/{IntroPrefabResourcePath} has no BoonIntroView " +
                             "component - using the code-built layout instead.");
            Discard(authored);
            return false;
        }

        Button button = view.Populate(
            LocalizationManager.Get("boon_intro_title"),
            LocalizationManager.Get("boon_intro_body"),
            LocalizationManager.Get("boon_intro_note"),
            LocalizationManager.Get("boon_intro_continue"),
            OnIntroAcknowledged);

        // A prefab with no continue button would pause the run with no way out. Better a
        // plain code-built explainer than a soft lock.
        if (button == null)
        {
            Discard(authored);
            return false;
        }

        optionButtons.Add(button);
        return true;
    }

    /// <summary>
    /// Drops a prefab instance that turned out to be unusable. Deactivated first because
    /// Destroy only takes effect at the end of the frame, and a broken canvas must not be
    /// visible over the code-built layout replacing it — not even for one frame.
    /// </summary>
    private static void Discard(GameObject authored)
    {
        authored.SetActive(false);
        Destroy(authored);
    }

    private void BuildIntroInCode()
    {
        pickerCanvas = RunOverlayUI.CreateCanvas(pickerRoot, sortingOrder: 4000, interactive: true);

        var backdrop = RunOverlayUI.CreateChild("Backdrop", pickerRoot.transform);
        RunOverlayUI.Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = RunOverlayUI.Backdrop;

        var title = RunOverlayUI.CreateLabel("Title", pickerRoot.transform,
            LocalizationManager.Get("boon_intro_title"), 60f, RunOverlayUI.Gold);
        RunOverlayUI.Place(title.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(900f, 90f));

        var body = RunOverlayUI.CreateLabel("Body", pickerRoot.transform,
            LocalizationManager.Get("boon_intro_body"), 34f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(body.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 60f), new Vector2(820f, 420f));

        var note = RunOverlayUI.CreateLabel("Note", pickerRoot.transform,
            LocalizationManager.Get("boon_intro_note"), 28f, RunOverlayUI.Muted);
        RunOverlayUI.Place(note.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(0f, -220f), new Vector2(820f, 120f));

        Button button = RunOverlayUI.CreateButton("Continue", pickerRoot.transform,
            LocalizationManager.Get("boon_intro_continue"), RunOverlayUI.Jade, out _);
        RunOverlayUI.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
            new Vector2(0f, 420f), new Vector2(560f, 140f));

        button.onClick.AddListener(OnIntroAcknowledged);

        // Armed by the same delay as the cards, so the tap that placed the stone can't
        // skip past the explanation.
        optionButtons.Add(button);
    }

    private void BuildPicker(List<BoonId> offers)
    {
        DestroyPicker();

        pickerRoot = new GameObject("BoonPicker");
        pickerRoot.transform.SetParent(transform, false);

        if (BuildPickerFromPrefab(offers)) return;

        BuildPickerInCode(offers);
    }

    /// <summary>
    /// Instantiates the authored picker, if there is one, and lets it fill itself in.
    /// Returns false when no usable prefab exists, so the caller falls back to code.
    /// </summary>
    private bool BuildPickerFromPrefab(List<BoonId> offers)
    {
        var prefab = Resources.Load<GameObject>(PickerPrefabResourcePath);
        if (prefab == null) return false;

        var authored = Instantiate(prefab, pickerRoot.transform, false);
        var view = authored.GetComponent<BoonPickerView>();

        if (view == null)
        {
            Debug.LogWarning($"[Boon] Resources/{PickerPrefabResourcePath} has no BoonPickerView " +
                             "component - using the code-built layout instead.");
            Discard(authored);
            return false;
        }

        List<Button> cards = view.Populate(
            LocalizationManager.Get("boon_title"),
            LocalizationManager.Get("boon_subtitle"),
            offers,
            OnBoonChosen);

        // A picker with no cards would pause the run with nothing to tap.
        if (cards.Count == 0)
        {
            Discard(authored);
            return false;
        }

        optionButtons.Clear();
        optionButtons.AddRange(cards);
        return true;
    }

    private void BuildPickerInCode(List<BoonId> offers)
    {
        // Above everything, and interactive - this one is meant to take the tap.
        pickerCanvas = RunOverlayUI.CreateCanvas(pickerRoot, sortingOrder: 4000, interactive: true);

        var backdrop = RunOverlayUI.CreateChild("Backdrop", pickerRoot.transform);
        RunOverlayUI.Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = RunOverlayUI.Backdrop;

        var title = RunOverlayUI.CreateLabel("Title", pickerRoot.transform,
            LocalizationManager.Get("boon_title"), 60f, RunOverlayUI.Gold);
        RunOverlayUI.Place(title.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(0f, -280f), new Vector2(900f, 90f));

        var subtitle = RunOverlayUI.CreateLabel("Subtitle", pickerRoot.transform,
            LocalizationManager.Get("boon_subtitle"), 28f, RunOverlayUI.Muted);
        RunOverlayUI.Place(subtitle.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(0f, -360f), new Vector2(900f, 60f));

        // Cards stack vertically and are centred as a group, so two, three or four
        // options all stay balanced on screen.
        const float cardHeight = 240f;
        const float cardGap = 30f;
        float totalHeight = offers.Count * cardHeight + (offers.Count - 1) * cardGap;
        float cursorY = totalHeight * 0.5f - cardHeight * 0.5f;

        optionButtons.Clear();
        for (int i = 0; i < offers.Count; i++)
        {
            BoonId id = offers[i];
            BoonDefinition def = BoonDefinition.For(id);

            Button button = RunOverlayUI.CreateButton($"Boon_{id}", pickerRoot.transform,
                string.Empty, RunOverlayUI.Stone, out TextMeshProUGUI buttonLabel);

            // The generated label is replaced by a name/description pair, so the card can
            // say what the boon does rather than only what it's called.
            buttonLabel.gameObject.SetActive(false);

            RunOverlayUI.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0f, cursorY), new Vector2(820f, cardHeight));

            var name = RunOverlayUI.CreateLabel("Name", button.transform,
                LocalizationManager.Get(def.nameKey), 44f, def.accentColor);
            RunOverlayUI.Place(name.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(760f, 60f));

            var desc = RunOverlayUI.CreateLabel("Description", button.transform,
                LocalizationManager.Get(def.descriptionKey), 28f, RunOverlayUI.Parchment);
            RunOverlayUI.Place(desc.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f), new Vector2(740f, 110f));

            BoonId captured = id;
            button.onClick.AddListener(() => OnBoonChosen(captured));

            optionButtons.Add(button);
            cursorY -= cardHeight + cardGap;
        }
    }

    private void OnBoonChosen(BoonId id)
    {
        int height = stackManager != null ? stackManager.GetStackCount() : 0;

        // Close first: granting can trigger the Kukulkan shift, whose slow-motion needs a
        // running clock to play out.
        ClosePicker(resumeGame: true);

        ActiveBoons.Grant(id);

        GameAnalytics.Track("boon_chosen", new Dictionary<string, object>
        {
            { "boon", id.ToString() },
            { "height", height }
        });
    }

    private void ClosePicker(bool resumeGame)
    {
        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        if (armRoutine != null)
        {
            StopCoroutine(armRoutine);
            armRoutine = null;
        }

        pendingOffers = null;
        DestroyPicker();

        if (IsChoosing)
        {
            IsChoosing = false;

            // Only this system froze time, so only this system unfreezes it. The pause menu
            // is unreachable while the picker is up, so there's no other owner to defer to.
            if (resumeGame) Time.timeScale = 1f;
        }
    }

    private void DestroyPicker()
    {
        optionButtons.Clear();

        if (pickerRoot != null)
        {
            Destroy(pickerRoot);
            pickerRoot = null;
            pickerCanvas = null;
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStart -= OnGameStart;
            gameManager.OnGameRestart -= OnRunEnded;
            gameManager.OnGameOver -= OnRunEnded;
        }

        if (stackManager != null)
        {
            stackManager.OnObjectAddedToStack -= OnObjectAddedToStack;
        }

        if (instance == this)
        {
            // Never leave the game frozen because this object went away.
            if (IsChoosing) Time.timeScale = 1f;
            IsChoosing = false;
            instance = null;
        }
    }
}
