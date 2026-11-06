using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The first-run tutorial teaches three things and nothing else: the tap, the Perfect, and
/// Kukulkan. Each beat is gated on something the player actually does rather than on a timer.
///
///   Beat 1 — "Tap to drop": holds until the player drops their first stone.
///   Beat 2 — names the accuracy tier they just earned and what it's worth.
///   Beat 3 — on the next landing, promises that Kukulkan answers at the top of the temple.
///            <see cref="CapstonePayoff"/> keeps the promise on the first temple win.
///
/// An Offering Stone earned on this run gets its one line when it is earned. Everything else
/// waits: Tremor, powers, the Serpent's Edge and boons stay dormant through this run, then go
/// live one per run (<see cref="FtueState.PacedSystem"/>), each taught by its own lesson.
///
/// In a temple the tutorial runs to the end of the run, so the win resolves it. Anywhere else
/// there is no win to wait for, so it resolves after a few landings.
///
/// Each line holds for as long as its own copy takes to read rather than for a flat couple
/// of seconds, and a beat that arrives while the previous line is still being read waits its
/// turn instead of cutting it off — a fast combo used to blow past two lines before either
/// could be finished.
///
/// Self-bootstraps into any gameplay scene while FtueState says the tutorial is unresolved,
/// so it needs zero scene wiring. The beat lines speak through the <see cref="GuideLane"/>,
/// the same low strip every later lesson uses, so the tutorial never covers the stone it is
/// teaching. The Skip control comes from a prefab at Resources/UI/FtueTutorial (see
/// <see cref="FtueTutorialView"/>), or is built in code without it.
///
/// Skip appears from beat 2 onward — never during beat 1, because the single tap that
/// teaches the core verb is the one thing nobody should be able to skip past.
/// </summary>
public class FtueTutorial : MonoBehaviour
{
    /// <summary>Authored prefab that replaces the code-built presentation when present.</summary>
    public const string PrefabResourcePath = "UI/FtueTutorial";

    // How many landings end the tutorial in a mode with no temple to win.
    private const int MaxLandingsOutsideTemples = 6;

    // Beat lines hold for as long as their own copy needs to be read (see ReadingTime) —
    // these are the floor for that, not the whole hold. A flat 2s was short enough to miss
    // a line entirely on the run where it matters most.
    private const float BeatMinimumSeconds = 3.6f;

    // Nothing replaces a line that has been up for less than this, so a quick combo can't
    // wipe the previous beat off the screen before it has been read.
    private const float MinimumDwellSeconds = 2.5f;

    private static FtueTutorial instance;

    private UIManager uiManager;
    private GameManager gameManager;
    private StackManager stackManager;
    private ObjectSpawner objectSpawner;
    private LevelManager levelManager;

    private int currentBeat;
    private int landingsSeen;
    private bool resolved;
    private bool droppedThisTutorial;
    private bool offeringTaught;
    private GameObject skipButton;
    private FtueTutorialView view;

    // Both pacing values are overridable on the prefab, so the timing can be felt out in the
    // editor rather than recompiled.
    private float BeatMinimum => view != null ? view.BeatMinimumSeconds : BeatMinimumSeconds;
    private float MinimumDwell => view != null ? view.MinimumDwellSeconds : MinimumDwellSeconds;

    private Coroutine sayRoutine;
    private float messageShownAt;
    private bool messageVisible;
    private int lineHandle;

    #region Bootstrap

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
        if (!FtueState.NeedsTutorial) return;

        // Gameplay scenes only. FindFirstObjectByType rather than DependencyRegistry.Find
        // avoids a spurious "not found" warning in the menu.
        if (UnityEngine.Object.FindFirstObjectByType<GameManager>() == null) return;

        var go = new GameObject("FtueTutorial");
        instance = go.AddComponent<FtueTutorial>();
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

    #endregion

    private void Start()
    {
        uiManager = DependencyRegistry.Find<UIManager>();
        gameManager = DependencyRegistry.Find<GameManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        levelManager = DependencyRegistry.Find<LevelManager>();

        if (gameManager == null || uiManager == null)
        {
            // Nothing to attach to — fail open rather than blocking the run.
            Debug.LogWarning("[FTUE] Tutorial could not find GameManager/UIManager; skipping.");
            Destroy(gameObject);
            return;
        }

        BuildView();

        if (stackManager != null) stackManager.OnObjectAddedToStack += OnBlockLanded;
        if (objectSpawner != null) objectSpawner.OnObjectDropped += OnBlockDropped;

        if (levelManager != null) levelManager.OnLevelCompleted += OnLevelCompleted;

        gameManager.OnConsecutivePerfectHitsChanged += OnPerfectStreakChanged;
        gameManager.OnGameOver += OnGameOver;

        // The title animation owns the screen first; start once it clears.
        if (uiManager.IsTitleShowing)
        {
            uiManager.OnTitleFinished += BeginBeatOne;
        }
        else
        {
            BeginBeatOne();
        }
    }

    #region Beats

    private void BeginBeatOne()
    {
        if (uiManager != null) uiManager.OnTitleFinished -= BeginBeatOne;
        if (resolved || currentBeat >= 1) return;

        currentBeat = 1;
        FtueState.Tutorial = FtueState.TutorialState.InProgress;
        GameAnalytics.TutorialStep(1);

        // The tutorial run teaches the basics and nothing else: no feature lesson joins in.
        GuideLane.ReserveRun("tutorial");

        // No timer on this one: it stays up until the player actually taps.
        SayUntilAction(LocalizationManager.Get("ftue_beat_tap"));
    }

    private void OnBlockDropped(GameObject droppedObject)
    {
        if (resolved) return;

        FtueState.MarkFirstDrop();

        if (currentBeat != 1 || droppedThisTutorial) return;
        droppedThisTutorial = true;

        // The core verb is taught. Skip becomes available from here on.
        CreateSkipButton();
        HideMessage();
    }

    private void OnBlockLanded(StackableObject landedObject)
    {
        if (resolved || landedObject == null) return;

        landingsSeen++;

        if (currentBeat == 1)
        {
            BeginBeatTwo(landedObject.LandingAccuracy);
            return;
        }

        if (currentBeat == 2)
        {
            BeginFinaleBeat();
            return;
        }

        if (gameManager.CurrentGameMode != GameMode.StackerLevels && landingsSeen >= MaxLandingsOutsideTemples)
        {
            CompleteTutorial();
        }
    }

    private void BeginBeatTwo(float accuracy)
    {
        currentBeat = 2;
        GameAnalytics.TutorialStep(2);

        string key = accuracy >= 0.9f ? "ftue_beat_perfect"
                   : accuracy >= 0.6f ? "ftue_beat_good"
                   : "ftue_beat_poor";

        Say(LocalizationManager.Get(key));
    }

    /// <summary>
    /// Beat 3: gives the run its goal and sets up the payoff. In a temple the promise is kept on
    /// the first win; elsewhere nothing would keep it, so this beat stays quiet.
    /// </summary>
    private void BeginFinaleBeat()
    {
        currentBeat = 3;
        if (gameManager.CurrentGameMode != GameMode.StackerLevels) return;

        GameAnalytics.TutorialStep(3);
        Say(LocalizationManager.Get("ftue_beat_finale"));
    }

    /// <summary>
    /// A player good enough to chain the streak on run one has earned an Offering Stone, and the
    /// stone needs its line now: its own lesson can't speak during the tutorial.
    /// </summary>
    private void OnPerfectStreakChanged(int streak)
    {
        if (resolved || offeringTaught || !gameManager.OfferingEarned) return;

        offeringTaught = true;
        Say(LocalizationManager.Get("ftue_beat_kukulkan", gameManager.PerfectHitsRequired));
    }

    private void OnLevelCompleted(int stars, int score, bool showCodexPopup) => CompleteTutorial();

    private void CompleteTutorial()
    {
        if (resolved) return;
        resolved = true;

        FtueState.Tutorial = FtueState.TutorialState.Completed;
        GameAnalytics.TutorialCompleted();

        Cleanup();
    }

    /// <summary>
    /// The run ended mid-tutorial. Leave the tutorial unresolved so it picks up again on
    /// the retry — but tear down the UI so it doesn't sit on top of the game-over panel.
    /// </summary>
    private void OnGameOver()
    {
        if (resolved) return;
        HideMessage();
        DestroySkipButton();
    }

    #endregion

    #region Skip

    private void OnSkipPressed()
    {
        if (resolved) return;
        resolved = true;

        FtueState.Tutorial = FtueState.TutorialState.Skipped;
        GameAnalytics.TutorialSkipped(currentBeat);

        Cleanup();
    }

    /// <summary>
    /// Builds a quiet corner Skip control. Created in code so the feature needs no scene
    /// wiring; styled to sit back rather than compete with the instruction copy.
    /// </summary>
    private void CreateSkipButton()
    {
        // The authored prefab brings its own Skip control; just reveal it.
        if (view != null && view.HasSkipButton)
        {
            view.SetSkipVisible(true);
            return;
        }

        if (skipButton != null || uiManager == null) return;

        Transform root = uiManager.UIRoot;
        if (root == null) return;

        skipButton = new GameObject("FtueSkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
        skipButton.transform.SetParent(root, false);

        var rect = skipButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-28f, 28f);
        rect.sizeDelta = new Vector2(150f, 62f);

        var image = skipButton.GetComponent<Image>();
        image.color = new Color(0.06f, 0.09f, 0.08f, 0.55f); // deep stone, translucent

        var labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(skipButton.transform, false);

        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = LocalizationManager.Get("ftue_skip");
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 30f;
        label.color = new Color(0.85f, 0.89f, 0.85f, 0.9f);
        label.raycastTarget = false;

        // Inherit the instruction label's font so Skip matches the game's type, including
        // the CJK switching handled by LocaleFontSwitcher.
        var reference = root.GetComponentInChildren<TextMeshProUGUI>();
        if (reference != null && reference.font != null) label.font = reference.font;
        labelObj.AddComponent<LocaleFontSwitcher>();

        skipButton.GetComponent<Button>().onClick.AddListener(OnSkipPressed);
    }

    private void DestroySkipButton()
    {
        if (view != null) view.SetSkipVisible(false);

        if (skipButton == null) return;
        Destroy(skipButton);
        skipButton = null;
    }

    #endregion

    #region Plumbing

    /// <summary>
    /// Instantiates the authored prefab, if there is one. Anything it leaves unassigned
    /// falls through to the code path, so a half-authored prefab is still usable.
    /// </summary>
    private void BuildView()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null) return;

        // The prefab carries its own Canvas, so it renders correctly parented to this
        // otherwise-empty host object, which owns its lifetime.
        var viewObject = Instantiate(prefab, transform, false);
        view = viewObject.GetComponent<FtueTutorialView>();
        if (view == null)
        {
            Debug.LogWarning($"[FTUE] Resources/{PrefabResourcePath} has no FtueTutorialView " +
                             "component - using the code-built presentation instead.");
            Destroy(viewObject);
            return;
        }

        view.SetSkipHandler(OnSkipPressed);
    }

    /// <summary>
    /// Shows a line and leaves it there. Used for beat one, which is dismissed by the
    /// player's tap rather than by a timer.
    /// </summary>
    private void SayUntilAction(string message)
    {
        CancelPendingMessage();
        DisplayMessage(message);
    }

    /// <summary>
    /// Shows a line for as long as it takes to read, then hides it and runs
    /// <paramref name="onRead"/>. If the previous beat is still being read, this one waits
    /// its turn rather than cutting it off mid-sentence.
    /// </summary>
    private void Say(string message, Action onRead = null, Action onShown = null)
    {
        CancelPendingMessage();
        sayRoutine = StartCoroutine(SayRoutine(message, onRead, onShown));
    }

    private IEnumerator SayRoutine(string message, Action onRead, Action onShown)
    {
        float shownFor = Time.unscaledTime - messageShownAt;
        if (messageVisible && shownFor < MinimumDwell)
        {
            yield return new WaitForSecondsRealtime(MinimumDwell - shownFor);
        }

        DisplayMessage(message);
        onShown?.Invoke();

        // Realtime, so a hit-stop or a pause can't eat the read.
        yield return new WaitForSecondsRealtime(ReadingTime.For(message, BeatMinimum));

        // Cleared first: HideMessage cancels the pending line, and this coroutine is it.
        sayRoutine = null;
        if (resolved) yield break;

        HideMessage();
        onRead?.Invoke();
    }

    private void CancelPendingMessage()
    {
        if (sayRoutine == null) return;
        StopCoroutine(sayRoutine);
        sayRoutine = null;
    }

    private void DisplayMessage(string message)
    {
        messageVisible = true;
        messageShownAt = Time.unscaledTime;

        // Every beat speaks through the guide lane, low on the screen, so the lesson never
        // sits between the swinging stone and the tower it is teaching the player to hit.
        // The tutorial keeps its own timing; the lane just holds the line until told.
        GuideLane.Hide(lineHandle);
        lineHandle = GuideLane.Say(message, null, RunOverlayUI.Parchment, 0f);
    }

    private void HideMessage()
    {
        CancelPendingMessage();
        messageVisible = false;

        GuideLane.Hide(lineHandle);
        lineHandle = 0;
    }

    private void Cleanup()
    {
        HideMessage();
        DestroySkipButton();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (stackManager != null) stackManager.OnObjectAddedToStack -= OnBlockLanded;
        if (objectSpawner != null) objectSpawner.OnObjectDropped -= OnBlockDropped;
        if (levelManager != null) levelManager.OnLevelCompleted -= OnLevelCompleted;

        if (gameManager != null)
        {
            gameManager.OnConsecutivePerfectHitsChanged -= OnPerfectStreakChanged;
            gameManager.OnGameOver -= OnGameOver;
        }

        if (uiManager != null) uiManager.OnTitleFinished -= BeginBeatOne;

        if (instance == this) instance = null;
    }

    #endregion
}
