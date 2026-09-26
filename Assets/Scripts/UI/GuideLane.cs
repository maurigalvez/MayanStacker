using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one place a run teaches anything.
///
/// Teaching used to be spread across a dozen systems that each put their own text wherever
/// they liked: the tutorial's panel in the fall path, the Serpent's Edge lesson dead
/// centre, the Tremor lesson over the meter it was pointing at — all sharing one banner
/// where the last caller simply replaced whoever was mid-sentence. Several could land in
/// the same run, on top of the stone the player was aiming.
///
/// Every lesson now goes through here, and three rules keep it out of the way:
///   • One place. A slim see-through strip low on the screen, under the top of the tower
///     and above the power button (see <see cref="GuideLaneView"/>). Nothing in it covers
///     the swing, the fall or the landing.
///   • One lesson per run. The first new thing a run meets is taught; anything else is
///     refused and simply asks again next run (callers only mark a lesson seen when it is
///     accepted). The tutorial run teaches only the tutorial.
///   • Calm beats only. Nothing appears while a stone is in the air: a line asked for
///     mid-drop waits for the landing, and a line never replaces one that is still being
///     read.
///
/// Announcements that aren't lessons — altitude bands, objectives, the tremor warning —
/// stay on <see cref="RunBanner"/>.
///
/// Self-hosting and non-interactive, like RunBanner: it needs no scene wiring and can
/// never swallow a tap.
/// </summary>
public class GuideLane : MonoBehaviour
{
    /// <summary>Authored prefab that replaces the code-built strip when present.</summary>
    public const string PrefabResourcePath = "UI/GuideLane";

    /// <summary>Above the HUD and the run banner (400), below the tutorial's Skip (4900).</summary>
    public const int SortingOrder = 450;

    // A line on screen for less than this is never replaced, whatever arrives.
    private const float MinimumDwellSeconds = 2f;

    // Floor for a lesson's hold; the real hold grows with the copy (ReadingTime).
    private const float MinimumHoldSeconds = 2.4f;

    // A stone that never reports a landing (it fell off the stack) mustn't hold lines forever.
    private const float MaxFlightSeconds = 3f;

    private const float FadeInSeconds = 0.3f;
    private const float FadeOutSeconds = 0.4f;

    private static GuideLane instance;

    private sealed class Line
    {
        public int handle;
        public string title;
        public string body;
        public Color accent;
        public float hold; // <= 0: stays until hidden by handle
        public int runKey; // -1: not tied to a run
    }

    private GuideLaneView view;
    private readonly List<Line> pending = new List<Line>();
    private Line current;
    private float shownAt;
    private float alpha;
    private int nextHandle = 1;

    // Which run's lesson slot is taken, and by what. Keyed by FtueState.LifetimeRuns, which
    // GameManager bumps before OnGameStart, so a new run frees the slot without depending
    // on the order run-start listeners happen to be called in.
    private int slotRunKey = -1;
    private string slotLessonId;

    private bool stoneInFlight;
    private float droppedAt;

    private GameManager gameManager;
    private ObjectSpawner objectSpawner;
    private StackManager stackManager;
    private LevelManager levelManager;

    private static int RunKey => FtueState.LifetimeRuns;

    #region Public API

    /// <summary>
    /// True when <paramref name="lessonId"/> may teach this run. Cheap and allocation-free,
    /// so per-frame callers can check before building their copy.
    /// </summary>
    public static bool CanTeach(string lessonId)
    {
        if (FtueState.NeedsTutorial) return false;

        GuideLane lane = Ensure();
        if (lane == null) return false;
        return lane.slotRunKey != RunKey || lane.slotLessonId == lessonId;
    }

    /// <summary>
    /// Teaches a lesson if this run still has room for it. Returns false when it was
    /// refused — the caller should then leave the lesson unseen so it asks again next run.
    /// The same <paramref name="lessonId"/> may speak more than once in its run (a lesson
    /// and its follow-up).
    /// </summary>
    public static bool TryTeach(string lessonId, string title, string body, Color accent, float holdSeconds)
    {
        if (string.IsNullOrEmpty(title) || !CanTeach(lessonId)) return false;

        instance.slotRunKey = RunKey;
        instance.slotLessonId = lessonId;
        instance.Enqueue(title, body, accent, instance.HoldFor(title, body, holdSeconds), lesson: true);
        return true;
    }

    /// <summary>
    /// Takes this run's lesson slot without saying anything yet — the tutorial does this so
    /// no feature lesson lands on the run that is already teaching the basics.
    /// </summary>
    public static void ReserveRun(string lessonId)
    {
        GuideLane lane = Ensure();
        if (lane == null) return;

        lane.slotRunKey = RunKey;
        lane.slotLessonId = lessonId;
    }

    /// <summary>
    /// Shows a line in the lane outside the one-lesson budget — a lesson's own follow-up
    /// beats, or a brief callout that belongs in this spot. <paramref name="holdSeconds"/>
    /// of zero or less keeps it up until <see cref="Hide"/>. Returns a handle for Hide.
    /// </summary>
    public static int Say(string title, string body, Color accent, float holdSeconds)
    {
        if (string.IsNullOrEmpty(title)) return 0;

        GuideLane lane = Ensure();
        if (lane == null) return 0;

        float hold = holdSeconds > 0f ? holdSeconds : 0f;
        return lane.Enqueue(title, body, accent, hold, lesson: false);
    }

    /// <summary>Takes a line down (or cancels it if it hasn't appeared yet).</summary>
    public static void Hide(int handle)
    {
        if (handle == 0 || instance == null) return;
        instance.Remove(handle);
    }

    #endregion

    #region Host

    private static GuideLane Ensure()
    {
        if (instance != null) return instance;

        var go = new GameObject("GuideLane");
        instance = go.AddComponent<GuideLane>();
        instance.Build();
        return instance;
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

    private void Build()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            // The prefab carries its own Canvas; this host only owns its lifetime.
            GameObject authored = Instantiate(prefab, transform, false);
            view = authored.GetComponent<GuideLaneView>();
            if (view != null && view.IsUsable)
            {
                view.SetAlpha(0f);
                return;
            }

            Debug.LogWarning($"[GuideLane] Resources/{PrefabResourcePath} has no usable GuideLaneView " +
                             "- using the code-built strip instead.");
            Destroy(authored);
        }

        RunOverlayUI.CreateCanvas(gameObject, SortingOrder, interactive: false);
        view = GuideLaneView.BuildDefault(gameObject, out _);
        view.SetAlpha(0f);
    }

    private void Start()
    {
        gameManager = DependencyRegistry.Find<GameManager>();
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        stackManager = DependencyRegistry.Find<StackManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();

        if (gameManager != null) gameManager.OnGameOver += OnGameOver;
        if (objectSpawner != null) objectSpawner.OnObjectDropped += OnObjectDropped;
        if (stackManager != null) stackManager.OnObjectAddedToStack += OnObjectLanded;
        if (levelManager != null) levelManager.OnLevelCompleted += OnLevelCompleted;
    }

    private void OnDestroy()
    {
        if (gameManager != null) gameManager.OnGameOver -= OnGameOver;
        if (objectSpawner != null) objectSpawner.OnObjectDropped -= OnObjectDropped;
        if (stackManager != null) stackManager.OnObjectAddedToStack -= OnObjectLanded;
        if (levelManager != null) levelManager.OnLevelCompleted -= OnLevelCompleted;

        if (instance == this) instance = null;
    }

    #endregion

    #region Lines

    private float HoldFor(string title, string body, float requested)
    {
        string copy = string.IsNullOrEmpty(body) ? title : title + " " + body;
        return ReadingTime.For(copy, Mathf.Max(requested, MinimumHoldSeconds));
    }

    private int Enqueue(string title, string body, Color accent, float hold, bool lesson)
    {
        var line = new Line
        {
            handle = nextHandle++,
            title = title,
            body = body,
            accent = accent,
            hold = hold,
            // Only lessons belong to a run. Other lines may be said across a restart on
            // purpose: the FTUE's forgiven topple announces itself as the new run begins.
            runKey = lesson ? RunKey : -1
        };
        pending.Add(line);
        return line.handle;
    }

    private void Remove(int handle)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (pending[i].handle == handle) pending.RemoveAt(i);
        }

        if (current != null && current.handle == handle) current = null;
    }

    private void OnObjectDropped(GameObject dropped)
    {
        stoneInFlight = true;
        droppedAt = Time.unscaledTime;
    }

    private void OnObjectLanded(StackableObject landed) => stoneInFlight = false;

    // A completed temple goes straight to its result panel; no lesson should sit over it.
    private void OnLevelCompleted(int stars, int score, bool firstCompletion) => OnGameOver();

    private void OnGameOver()
    {
        // Nothing left over from this run should surface on the result card or the next run.
        pending.Clear();
        current = null;
        stoneInFlight = false;
    }

    private bool IsCalm => !stoneInFlight || Time.unscaledTime - droppedAt > MaxFlightSeconds;

    /// <summary>
    /// Unscaled throughout: lines must survive hit-stop, the Kukulkan slow-motion and a
    /// paused game without stalling half-faded.
    /// </summary>
    private void Update()
    {
        float now = Time.unscaledTime;

        // A timed line that has been read makes way.
        if (current != null && current.hold > 0f && now - shownAt >= FadeInSeconds + current.hold)
        {
            current = null;
        }

        // Lines from a run that has ended are dropped rather than shown late.
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (pending[i].runKey >= 0 && pending[i].runKey != RunKey) pending.RemoveAt(i);
        }

        if (pending.Count > 0 && IsCalm)
        {
            // Something is waiting: let the current line finish its minimum dwell, then fade
            // it out; the next line fades in once the strip is clear.
            if (current != null && now - shownAt >= MinimumDwellSeconds) current = null;

            if (current == null && alpha <= 0f)
            {
                current = pending[0];
                pending.RemoveAt(0);
                shownAt = now;
                view.Show(current.title, current.body, current.accent);
            }
        }

        float target = current != null ? 1f : 0f;
        if (!Mathf.Approximately(alpha, target))
        {
            float step = Time.unscaledDeltaTime / (target > alpha ? FadeInSeconds : FadeOutSeconds);
            alpha = Mathf.MoveTowards(alpha, target, step);
            view.SetAlpha(alpha);
        }
    }

    #endregion
}
