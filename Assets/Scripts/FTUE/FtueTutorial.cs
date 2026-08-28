using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The first-run tutorial: three beats, each gated on something the player actually does
/// rather than on a timer.
///
///   Beat 1 — "Tap to drop": holds until the player drops their first stone.
///   Beat 2 — names the accuracy tier they just earned and what it's worth.
///   Beat 3 — at their first 2-combo, teases the Kukulkan shift.
///
/// Self-bootstraps into any gameplay scene while FtueState says the tutorial is unresolved,
/// so it needs zero scene wiring. It borrows UIManager's existing instruction label rather
/// than building its own, and creates only the Skip control at runtime.
///
/// Skip appears from beat 2 onward — never during beat 1, because the single tap that
/// teaches the core verb is the one thing nobody should be able to skip past.
/// </summary>
public class FtueTutorial : MonoBehaviour
{
    // How many landings to wait for a 2-combo before finishing anyway, so a player who
    // keeps landing Poor doesn't get stuck in a tutorial that never ends.
    private const int MaxLandingsAwaitingCombo = 5;

    private const float BeatHoldSeconds = 2.2f;
    private const float CompletionHoldSeconds = 2.0f;

    private static FtueTutorial instance;

    private UIManager uiManager;
    private GameManager gameManager;
    private StackManager stackManager;
    private ObjectSpawner objectSpawner;

    private int currentBeat;
    private int landingsSeen;
    private bool resolved;
    private bool droppedThisTutorial;
    private GameObject skipButton;

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
        if (Object.FindFirstObjectByType<GameManager>() == null) return;

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

        if (gameManager == null || uiManager == null)
        {
            // Nothing to attach to — fail open rather than blocking the run.
            Debug.LogWarning("[FTUE] Tutorial could not find GameManager/UIManager; skipping.");
            Destroy(gameObject);
            return;
        }

        if (stackManager != null) stackManager.OnObjectAddedToStack += OnBlockLanded;
        if (objectSpawner != null) objectSpawner.OnObjectDropped += OnBlockDropped;

        gameManager.OnComboChanged += OnComboChanged;
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

        ShowMessage(LocalizationManager.Get("ftue_beat_tap"));
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

        // Don't strand the player in beat 2 forever if they never chain two Perfects.
        if (currentBeat == 2 && landingsSeen >= MaxLandingsAwaitingCombo)
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

        ShowMessage(LocalizationManager.Get(key));
        StartCoroutine(HideAfter(BeatHoldSeconds));
    }

    private void OnComboChanged(int combo, float multiplier)
    {
        if (resolved || currentBeat != 2 || combo < 2) return;

        currentBeat = 3;
        GameAnalytics.TutorialStep(3);

        int required = gameManager != null ? gameManager.PerfectHitsRequired : 4;
        ShowMessage(LocalizationManager.Get("ftue_beat_kukulkan", required));
        StartCoroutine(CompleteAfter(CompletionHoldSeconds));
    }

    private IEnumerator CompleteAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        CompleteTutorial();
    }

    private IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (!resolved) HideMessage();
    }

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
        if (skipButton == null) return;
        Destroy(skipButton);
        skipButton = null;
    }

    #endregion

    #region Plumbing

    private void ShowMessage(string message)
    {
        if (uiManager != null) uiManager.ShowTutorialMessage(message);
    }

    private void HideMessage()
    {
        if (uiManager != null) uiManager.HideTutorialMessage();
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

        if (gameManager != null)
        {
            gameManager.OnComboChanged -= OnComboChanged;
            gameManager.OnGameOver -= OnGameOver;
        }

        if (uiManager != null) uiManager.OnTitleFinished -= BeginBeatOne;

        if (instance == this) instance = null;
    }

    #endregion
}
