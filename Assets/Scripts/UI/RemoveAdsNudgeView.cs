using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The quiet "Tired of ads? Remove them" line that appears on the result card right after an
/// interstitial closes — the one moment the offer is most relevant. Tapping it opens the
/// same <see cref="RemoveAdsOfferView"/> as Settings, so price and terms still come first.
///
/// When it shows is decided by <see cref="RemoveAdsOffer"/> (after a few ads, at most once a
/// session); this view only listens for <see cref="RemoveAdsOffer.NudgeDue"/>. It never
/// appears over a live run: it waits a beat after the ad, re-checks, and leaves as soon as
/// the next run starts or the scene changes.
///
/// Presentation comes from a prefab at Resources/UI/RemoveAdsNudge when one exists (menu:
/// TamalStacker ▸ UI ▸ Create Remove Ads Nudge Prefab, which dresses it in the house style
/// with the No Ads icon). Without it a plain code-built strip is used, like the other overlays.
/// </summary>
public class RemoveAdsNudgeView : MonoBehaviour
{
    /// <summary>Above the result card, below the offer card it opens.</summary>
    public const int SortingOrder = 4300;

    public const string PrefabResourcePath = "UI/RemoveAdsNudge";

    [Tooltip("The tappable strip; shown and hidden with the popup motion.")]
    [SerializeField] private RectTransform nudge;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [Tooltip("Pause after the ad closes, so the player sees the result card before the nudge.")]
    [SerializeField] private float appearDelay = 0.35f;

    private static RemoveAdsNudgeView instance;

    private bool showing;
    private Coroutine appearRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        RemoveAdsOffer.NudgeDue -= OnNudgeDue;
        RemoveAdsOffer.NudgeDue += OnNudgeDue;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnNudgeDue()
    {
        if (instance == null) instance = Create();
        instance.Appear();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null) instance.Dismiss();
    }

    private static RemoveAdsNudgeView Create()
    {
        RemoveAdsNudgeView view = CreateFromPrefab();
        if (view == null)
        {
            var host = new GameObject("RemoveAdsNudgeView");
            RunOverlayUI.CreateCanvas(host, SortingOrder, interactive: true);
            view = host.AddComponent<RemoveAdsNudgeView>();
            view.Build();
        }

        view.button.onClick.AddListener(view.OnTapped);
        view.nudge.gameObject.SetActive(false);
        DontDestroyOnLoad(view.gameObject);
        return view;
    }

    private static RemoveAdsNudgeView CreateFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null) return null;

        if (prefab.GetComponent<RemoveAdsNudgeView>() == null)
        {
            Debug.LogWarning($"[RemoveAdsNudge] Resources/{PrefabResourcePath} has no RemoveAdsNudgeView " +
                             "component - using the code-built layout instead.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "RemoveAdsNudgeView";
        var view = go.GetComponent<RemoveAdsNudgeView>();

        if (view.nudge == null || view.button == null || view.label == null)
        {
            Debug.LogWarning($"[RemoveAdsNudge] Resources/{PrefabResourcePath} is missing required references - " +
                             "using the code-built layout instead.");
            Destroy(go);
            return null;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        return view;
    }

    // Shared with RemoveAdsNudgePrefabSetup so both layouts stay identical. Top of the screen,
    // clear of the result card and its buttons.
    public static readonly Vector2 NudgeAnchor = new Vector2(0.5f, 1f);
    public static readonly Vector2 NudgePosition = new Vector2(0f, -230f);
    public static readonly Vector2 NudgeSize = new Vector2(760f, 140f);
    public static readonly Vector2 IconSize = new Vector2(120f, 120f);
    public static readonly float IconInset = 20f;

    /// <summary>Code-built fallback layout: a stone strip with the line of text, no icon.</summary>
    private void Build()
    {
        nudge = RunOverlayUI.CreateChild("Nudge", transform);
        RunOverlayUI.Place(nudge, NudgeAnchor, NudgePosition, NudgeSize);
        var slab = nudge.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        button = nudge.gameObject.AddComponent<Button>();
        button.targetGraphic = slab;

        label = RunOverlayUI.CreateLabel("Label", nudge, string.Empty, 36f, RunOverlayUI.Parchment);
        RunOverlayUI.Stretch(label.rectTransform);
    }

    private void Appear()
    {
        if (showing || appearRoutine != null) return;
        appearRoutine = StartCoroutine(AppearAfterDelay());
    }

    private IEnumerator AppearAfterDelay()
    {
        yield return new WaitForSecondsRealtime(appearDelay);
        appearRoutine = null;

        // The player may already be back in a run, or the store may have gone away.
        if (RunIsLive() || !RemoveAdsOffer.Available) yield break;

        label.text = LocalizationManager.Get("remove_ads_nudge");
        showing = true;
        UIPopup.Show(nudge.gameObject);
        GameAnalytics.Purchase(RemoveAds.ProductId, "nudge_shown", RemoveAdsOffer.SourceResultCard);
    }

    private void Update()
    {
        if (showing && (RemoveAds.Owned || RunIsLive())) Dismiss();
    }

    private void OnTapped()
    {
        if (!showing) return;
        Dismiss();
        RemoveAdsOffer.TryOpen(RemoveAdsOffer.SourceResultCard);
    }

    private void Dismiss()
    {
        if (appearRoutine != null)
        {
            StopCoroutine(appearRoutine);
            appearRoutine = null;
        }

        if (!showing) return;
        showing = false;
        UIPopup.Hide(nudge.gameObject);
    }

    private static bool RunIsLive()
    {
        return DependencyRegistry.TryFind<GameManager>(out var game) && game.IsGameActive;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
