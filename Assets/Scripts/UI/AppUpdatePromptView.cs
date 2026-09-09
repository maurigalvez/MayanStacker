using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player-facing surface for the Google Play in-app update flow.
///
/// Built in code on its own overlay canvas, the same way <see cref="RunBanner"/> and the
/// boon picker are, so the feature needs no scene wiring and no serialized fields: turn it
/// off and none of this ever exists. Palette and font come from <see cref="RunOverlayUI"/>,
/// so it reads as the same temple as the rest of the game.
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
/// </summary>
public class AppUpdatePromptView : MonoBehaviour
{
    /// <summary>Above the authored UI (sorting order 0) and above the run banners.</summary>
    private const int SortingOrder = 400;

    private Image backdrop;

    private RectTransform modal;
    private TextMeshProUGUI modalTitle;
    private TextMeshProUGUI modalBody;
    private Button primaryButton;
    private TextMeshProUGUI primaryLabel;
    private Button secondaryButton;
    private TextMeshProUGUI secondaryLabel;

    private RectTransform strip;
    private TextMeshProUGUI stripLabel;
    private RectTransform progressFill;

    private Action onPrimary;
    private Action onSecondary;

    /// <summary>Creates the view on a fresh persistent GameObject.</summary>
    public static AppUpdatePromptView Create()
    {
        var host = new GameObject("AppUpdatePromptView");
        DontDestroyOnLoad(host);

        RunOverlayUI.CreateCanvas(host, SortingOrder, interactive: true);
        var view = host.AddComponent<AppUpdatePromptView>();
        view.Build();
        return view;
    }

    private void Build()
    {
        Transform root = transform;

        // ── Backdrop ──
        var backdropRect = RunOverlayUI.CreateChild("Backdrop", root);
        RunOverlayUI.Stretch(backdropRect);
        backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = RunOverlayUI.Backdrop;
        backdrop.raycastTarget = true;
        backdropRect.gameObject.SetActive(false);

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
        primaryButton.onClick.AddListener(() => onPrimary?.Invoke());

        secondaryButton = RunOverlayUI.CreateButton("Secondary", modal, string.Empty, RunOverlayUI.Clay, out secondaryLabel);
        RunOverlayUI.Place((RectTransform)secondaryButton.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 100f), new Vector2(400f, 110f));
        secondaryButton.onClick.AddListener(() => onSecondary?.Invoke());

        modal.gameObject.SetActive(false);

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

        bool hasSecondary = !string.IsNullOrEmpty(secondaryText);
        secondaryButton.gameObject.SetActive(hasSecondary);
        if (hasSecondary) secondaryLabel.text = secondaryText;

        // Centre the single button rather than leaving it sitting off to one side.
        var primaryRect = (RectTransform)primaryButton.transform;
        RunOverlayUI.Place(primaryRect, new Vector2(0.5f, 0f),
            new Vector2(hasSecondary ? 220f : 0f, 100f), new Vector2(400f, 110f));

        strip.gameObject.SetActive(false);
        backdrop.gameObject.SetActive(true);
        modal.gameObject.SetActive(true);
    }

    /// <summary>Shows the background-download strip. <paramref name="progress"/> is 0..1.</summary>
    public void ShowProgress(string text, float progress)
    {
        stripLabel.text = text;
        progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

        modal.gameObject.SetActive(false);
        backdrop.gameObject.SetActive(false);
        strip.gameObject.SetActive(true);
    }

    /// <summary>Hides every state. The view stays alive for the next one.</summary>
    public void Hide()
    {
        onPrimary = null;
        onSecondary = null;
        modal.gameObject.SetActive(false);
        backdrop.gameObject.SetActive(false);
        strip.gameObject.SetActive(false);
    }
}
