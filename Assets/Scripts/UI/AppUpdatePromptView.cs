using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player-facing surface for the Google Play in-app update flow.
///
/// Presentation comes from a prefab at Resources/UI/AppUpdatePrompt when one exists, so the
/// prompt can be restyled alongside the rest of the main menu UI. When that prefab is absent
/// it builds itself in code on its own overlay canvas, the same way <see cref="RunBanner"/>
/// and the boon picker do, so the feature still needs no scene wiring. Palette and font
/// come from <see cref="RunOverlayUI"/>, so either path reads as the same temple.
///
/// Three states, only ever one of them visible:
///
///   • <see cref="ShowModal"/> — the ask ("a new offering awaits"), and later the restart
///     prompt. Full-screen backdrop, because both are decisions.
///   • <see cref="ShowProgress"/> — a quiet strip along the bottom while the download runs.
///     A flexible update downloads in the background and the player is expected to keep
///     playing through it, so this must not block anything.
///   • <see cref="Hide"/> — nothing.
///
/// The backdrop is enabled only for the modal states. It is the only full-screen raycast
/// target here, so with it off a tap passes straight through to the menu behind.
///
/// Menu: TamalStacker ▸ Retention ▸ Create App Update Prompt Prefab generates a prefab that
/// matches the code-built layout, as a starting point to restyle.
/// </summary>
public class AppUpdatePromptView : MonoBehaviour
{
    /// <summary>Authored prefab that replaces the code-built layout when present.</summary>
    public const string PrefabResourcePath = "UI/AppUpdatePrompt";

    /// <summary>Above the authored UI (sorting order 0) and above the run banners.</summary>
    public const int SortingOrder = 400;

    [Header("Modal")]
    [Tooltip("Full-screen tap blocker behind the modal. Shown only while a decision is up.")]
    [SerializeField] private Image backdrop;

    [Tooltip("The decision slab: title, body and the two buttons live under it.")]
    [SerializeField] private RectTransform modal;

    [SerializeField] private TextMeshProUGUI modalTitle;
    [SerializeField] private TextMeshProUGUI modalBody;

    [Tooltip("\"Update\" / \"Restart\". Centred horizontally when the secondary button is hidden.")]
    [SerializeField] private Button primaryButton;
    [SerializeField] private TextMeshProUGUI primaryLabel;

    [Tooltip("\"Not now\". Hidden for the single-button restart prompt.")]
    [SerializeField] private Button secondaryButton;
    [SerializeField] private TextMeshProUGUI secondaryLabel;

    [Header("Download strip")]
    [Tooltip("Non-blocking bar shown while the update downloads in the background.")]
    [SerializeField] private RectTransform strip;
    [SerializeField] private TextMeshProUGUI stripLabel;

    [Tooltip("Progress fill. A Filled Image uses fillAmount; otherwise the rect's anchorMax.x " +
             "is scaled 0..1, so keep it left-anchored (anchorMin x 0, pivot x 0).")]
    [SerializeField] private RectTransform progressFill;

    private Action onPrimary;
    private Action onSecondary;

    private Image progressFillImage;
    private Vector2 primaryAuthoredPosition;

    /// <summary>
    /// Creates the view on a fresh persistent GameObject — from the authored prefab when
    /// there is a usable one, otherwise built in code.
    /// </summary>
    public static AppUpdatePromptView Create()
    {
        AppUpdatePromptView view = CreateFromPrefab();
        if (view == null)
        {
            var host = new GameObject("AppUpdatePromptView");
            RunOverlayUI.CreateCanvas(host, SortingOrder, interactive: true);
            view = host.AddComponent<AppUpdatePromptView>();
            view.Build();
        }

        DontDestroyOnLoad(view.gameObject);
        view.Initialize();
        return view;
    }

    private static AppUpdatePromptView CreateFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null) return null;

        if (prefab.GetComponent<AppUpdatePromptView>() == null)
        {
            Debug.LogWarning($"[AppUpdate] Resources/{PrefabResourcePath} has no AppUpdatePromptView " +
                             "component - using the code-built layout instead.");
            return null;
        }

        var instance = Instantiate(prefab);
        instance.name = "AppUpdatePromptView";
        var view = instance.GetComponent<AppUpdatePromptView>();

        // Every piece is needed by one state or another; a half-wired prefab would throw
        // mid-flow, in front of the player, so refuse it up front instead.
        if (view.modal == null || view.modalTitle == null || view.modalBody == null ||
            view.primaryButton == null || view.primaryLabel == null ||
            view.strip == null || view.stripLabel == null || view.progressFill == null)
        {
            Debug.LogWarning($"[AppUpdate] Resources/{PrefabResourcePath} is missing required references - " +
                             "using the code-built layout instead.");
            Destroy(instance);
            return null;
        }

        return view;
    }

    /// <summary>Code-built fallback layout. Mirrored by AppUpdatePromptPrefabSetup.</summary>
    private void Build()
    {
        Transform root = transform;

        // ── Backdrop ──
        var backdropRect = RunOverlayUI.CreateChild("Backdrop", root);
        RunOverlayUI.Stretch(backdropRect);
        backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = RunOverlayUI.Backdrop;
        backdrop.raycastTarget = true;

        // ── Modal ──
        modal = RunOverlayUI.CreateChild("Modal", root);
        RunOverlayUI.Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;

        modalTitle = RunOverlayUI.CreateLabel("Title", modal, string.Empty, 52f, RunOverlayUI.Gold);
        RunOverlayUI.Place(modalTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(800f, 120f));

        modalBody = RunOverlayUI.CreateLabel("Body", modal, string.Empty, 32f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(modalBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(800f, 220f));

        primaryButton = RunOverlayUI.CreateButton("Primary", modal, string.Empty, RunOverlayUI.Jade, out primaryLabel);
        RunOverlayUI.Place((RectTransform)primaryButton.transform, new Vector2(0.5f, 0f), new Vector2(220f, 100f), new Vector2(400f, 110f));

        secondaryButton = RunOverlayUI.CreateButton("Secondary", modal, string.Empty, RunOverlayUI.Clay, out secondaryLabel);
        RunOverlayUI.Place((RectTransform)secondaryButton.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 100f), new Vector2(400f, 110f));

        // ── Download strip ──
        strip = RunOverlayUI.CreateChild("DownloadStrip", root);
        RunOverlayUI.Place(strip, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 120f));
        var stripBg = strip.gameObject.AddComponent<Image>();
        stripBg.color = RunOverlayUI.Stone;
        // A background strip that ate taps would swallow menu presses behind it.
        stripBg.raycastTarget = false;

        stripLabel = RunOverlayUI.CreateLabel("Label", strip, string.Empty, 28f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(stripLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(840f, 48f));

        var track = RunOverlayUI.CreateChild("Track", strip);
        RunOverlayUI.Place(track, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(840f, 16f));
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.45f);
        trackImage.raycastTarget = false;

        progressFill = RunOverlayUI.CreateChild("Fill", track);
        // Left-anchored so scaling anchorMax.x grows the bar rightward from 0 to full width.
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.pivot = new Vector2(0f, 0.5f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        var fillImage = progressFill.gameObject.AddComponent<Image>();
        fillImage.color = RunOverlayUI.Gold;
        fillImage.raycastTarget = false;
    }

    /// <summary>
    /// Shared by both paths: hooks the buttons and starts every state hidden. The prefab
    /// keeps its pieces active so they stay easy to edit; they are switched off here.
    /// </summary>
    private void Initialize()
    {
        primaryButton.onClick.AddListener(() => CloseModalThen(onPrimary));
        if (secondaryButton != null) secondaryButton.onClick.AddListener(() => CloseModalThen(onSecondary));

        primaryAuthoredPosition = ((RectTransform)primaryButton.transform).anchoredPosition;

        progressFillImage = progressFill.GetComponent<Image>();
        if (progressFillImage != null && progressFillImage.type != Image.Type.Filled) progressFillImage = null;

        if (backdrop != null) backdrop.gameObject.SetActive(false);
        modal.gameObject.SetActive(false);
        strip.gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows a decision. Pass a null <paramref name="secondaryText"/> for a single-button
    /// modal — used by the restart prompt, which the player may simply ignore by tapping
    /// nothing, but which has no meaningful "no".
    /// </summary>
    public void ShowModal(string title, string body, string primaryText, Action primary,
        string secondaryText, Action secondary)
    {
        onPrimary = primary;
        onSecondary = secondary;

        modalTitle.text = title;
        modalBody.text = body;
        primaryLabel.text = primaryText;

        bool hasSecondary = secondaryButton != null && !string.IsNullOrEmpty(secondaryText);
        if (secondaryButton != null) secondaryButton.gameObject.SetActive(hasSecondary);
        if (hasSecondary && secondaryLabel != null) secondaryLabel.text = secondaryText;

        // Centre the single button rather than leaving it sitting off to one side. Only x
        // moves, so an authored vertical position survives.
        ((RectTransform)primaryButton.transform).anchoredPosition = hasSecondary
            ? primaryAuthoredPosition
            : new Vector2(0f, primaryAuthoredPosition.y);

        strip.gameObject.SetActive(false);
        if (backdrop != null) UIPopup.Show(backdrop.gameObject);
        UIPopup.Show(modal.gameObject);
    }

    /// <summary>
    /// Scales the modal away first, then runs the player's choice — so whatever that choice
    /// shows next (the download strip, the restart prompt) never appears under a modal that
    /// is still leaving.
    /// </summary>
    private void CloseModalThen(Action action)
    {
        if (backdrop != null) UIPopup.Hide(backdrop.gameObject);
        UIPopup.Hide(modal.gameObject, action);
    }

    /// <summary>Shows the background-download strip. <paramref name="progress"/> is 0..1.</summary>
    public void ShowProgress(string text, float progress)
    {
        stripLabel.text = text;

        float clamped = Mathf.Clamp01(progress);
        if (progressFillImage != null) progressFillImage.fillAmount = clamped;
        else progressFill.anchorMax = new Vector2(clamped, progressFill.anchorMax.y);

        UIPopup.Hide(modal.gameObject);
        if (backdrop != null) UIPopup.Hide(backdrop.gameObject);
        strip.gameObject.SetActive(true);
    }

    /// <summary>Hides every state. The view stays alive for the next one.</summary>
    public void Hide()
    {
        onPrimary = null;
        onSecondary = null;
        UIPopup.Hide(modal.gameObject);
        if (backdrop != null) UIPopup.Hide(backdrop.gameObject);
        strip.gameObject.SetActive(false);
    }
}
