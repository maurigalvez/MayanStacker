using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The lead-in card that stands in front of Google's in-app review sheet.
///
/// Play's sheet arrives in Google's chrome, with no warning, over whatever is on screen —
/// and, far more often than not, does not arrive at all: the API accepts the request,
/// reports success and draws nothing, because of quota, an account that already reviewed,
/// or an install Play did not make. Asking here first means the automatic prompt is a
/// moment the game owns: the player answers a card in the game's own voice, and only a
/// "yes" spends the one chance Play might give us.
///
/// The copy may not ask what the player thinks of the game. Play's In-App Review terms
/// forbid preceding the sheet with an opinion question ("enjoying it?", "would you give us
/// five stars?") and forbid routing an unhappy answer somewhere else. So the card asks to
/// do something — help other stackers find the temple — and neither button branches on a
/// sentiment.
///
/// Built in code on its own overlay canvas, like <see cref="AppUpdatePromptView"/> and the
/// boon picker, so it needs no scene wiring and no authored prefab to exist.
/// </summary>
public class ReviewPromptView : MonoBehaviour
{
    /// <summary>
    /// Above the game-over and level-complete UI (authored canvases sit at 0) and above the
    /// boon picker, but below the tutorial and language screens, which must never be buried.
    /// </summary>
    public const int SortingOrder = 4500;

    private Image backdrop;
    private RectTransform modal;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;
    private Button confirmButton;
    private TextMeshProUGUI confirmLabel;
    private Button declineButton;
    private TextMeshProUGUI declineLabel;

    private Action<bool> onAnswered;

    /// <summary>Guards against a double tap landing on both buttons in the same frame.</summary>
    private bool answered;

    public static ReviewPromptView Create()
    {
        var host = new GameObject("ReviewPromptView");
        RunOverlayUI.CreateCanvas(host, SortingOrder, interactive: true);
        var view = host.AddComponent<ReviewPromptView>();
        view.Build();
        DontDestroyOnLoad(host);
        return view;
    }

    private void Build()
    {
        Transform root = transform;

        // Full-screen tap blocker. Without it a tap meant for the card lands on the
        // game-over panel's Try Again behind it.
        var backdropRect = RunOverlayUI.CreateChild("Backdrop", root);
        RunOverlayUI.Stretch(backdropRect);
        backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = RunOverlayUI.Backdrop;
        backdrop.raycastTarget = true;

        modal = RunOverlayUI.CreateChild("Modal", root);
        RunOverlayUI.Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;

        titleLabel = RunOverlayUI.CreateLabel("Title", modal, string.Empty, 52f, RunOverlayUI.Gold);
        RunOverlayUI.Place(titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(800f, 120f));

        bodyLabel = RunOverlayUI.CreateLabel("Body", modal, string.Empty, 32f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(bodyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(800f, 220f));

        // "Not now" is a real button of the same size as "Sure", deliberately. It ends the
        // ask for good, and a button that ends something permanently should not be the small
        // one the player has to hunt for.
        confirmButton = RunOverlayUI.CreateButton("Confirm", modal, string.Empty, RunOverlayUI.Jade, out confirmLabel);
        RunOverlayUI.Place((RectTransform)confirmButton.transform, new Vector2(0.5f, 0f), new Vector2(220f, 100f), new Vector2(400f, 110f));

        declineButton = RunOverlayUI.CreateButton("Decline", modal, string.Empty, RunOverlayUI.Clay, out declineLabel);
        RunOverlayUI.Place((RectTransform)declineButton.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 100f), new Vector2(400f, 110f));

        confirmButton.onClick.AddListener(() => Answer(true));
        declineButton.onClick.AddListener(() => Answer(false));

        backdrop.gameObject.SetActive(false);
        modal.gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows the card. <paramref name="answeredCallback"/> runs once, with true for "Sure"
    /// and false for "Not now", after the card has finished scaling away — so Play's sheet
    /// never opens underneath a modal that is still leaving.
    /// </summary>
    public void Show(Action<bool> answeredCallback)
    {
        onAnswered = answeredCallback;
        answered = false;

        titleLabel.text = LocalizationManager.Get("review_prompt_title");
        bodyLabel.text = LocalizationManager.Get("review_prompt_body");
        confirmLabel.text = LocalizationManager.Get("review_prompt_confirm");
        declineLabel.text = LocalizationManager.Get("review_prompt_decline");

        UIPopup.Show(backdrop.gameObject);
        UIPopup.Show(modal.gameObject);
    }

    /// <summary>Closes the card without answering. Used on teardown; raises nothing.</summary>
    public void Hide()
    {
        answered = true;
        onAnswered = null;
        UIPopup.Hide(modal.gameObject);
        UIPopup.Hide(backdrop.gameObject);
    }

    private void Answer(bool accepted)
    {
        if (answered) return;
        answered = true;

        Action<bool> callback = onAnswered;
        onAnswered = null;

        UIPopup.Hide(backdrop.gameObject);
        UIPopup.Hide(modal.gameObject, () => callback?.Invoke(accepted));
    }
}
