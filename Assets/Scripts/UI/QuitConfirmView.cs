using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Leave the temple?" — asked when the Android back button is pressed on the main menu's
/// root screen, so a stray back press doesn't close the app.
///
/// Presentation comes from a prefab at Resources/UI/QuitConfirm when one exists, styled like
/// the rest of the temple UI. Without it the card builds itself in code on its own overlay
/// canvas, like <see cref="ReviewPromptView"/>, so it needs no scene wiring.
///
/// Menu: TamalStacker ▸ UI ▸ Create Quit Confirm Prefab generates that prefab in the house
/// style (see UIHouseStyle).
/// </summary>
public class QuitConfirmView : MonoBehaviour
{
    /// <summary>Above the menu and its popups, below the tutorial and language screens.</summary>
    public const int SortingOrder = 4500;

    /// <summary>Authored prefab that replaces the code-built layout when present.</summary>
    public const string PrefabResourcePath = "UI/QuitConfirm";

    [Tooltip("Full-screen tap blocker behind the card.")]
    [SerializeField] private Image backdrop;
    [SerializeField] private RectTransform modal;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI bodyLabel;
    [Tooltip("\"Leave\" — quits the app.")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmLabel;
    [Tooltip("\"Stay\" — closes the card.")]
    [SerializeField] private Button declineButton;
    [SerializeField] private TextMeshProUGUI declineLabel;

    private static QuitConfirmView instance;

    private bool showing;
    private bool answered;

    public static bool IsShowing => instance != null && instance.showing;

    /// <summary>Shows the card, creating it on first use. Does nothing if it's already up.</summary>
    public static void Show()
    {
        if (instance == null) instance = Create();
        instance.Open();
    }

    private static QuitConfirmView Create()
    {
        QuitConfirmView view = CreateFromPrefab();
        if (view == null)
        {
            var host = new GameObject("QuitConfirmView");
            RunOverlayUI.CreateCanvas(host, SortingOrder, interactive: true);
            view = host.AddComponent<QuitConfirmView>();
            view.Build();
        }

        view.Initialize();
        DontDestroyOnLoad(view.gameObject);
        return view;
    }

    private static QuitConfirmView CreateFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null) return null;

        if (prefab.GetComponent<QuitConfirmView>() == null)
        {
            Debug.LogWarning($"[QuitConfirm] Resources/{PrefabResourcePath} has no QuitConfirmView " +
                             "component - using the code-built layout instead.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "QuitConfirmView";
        var view = go.GetComponent<QuitConfirmView>();

        if (view.backdrop == null || view.modal == null || view.titleLabel == null ||
            view.bodyLabel == null || view.confirmButton == null || view.confirmLabel == null ||
            view.declineButton == null || view.declineLabel == null)
        {
            Debug.LogWarning($"[QuitConfirm] Resources/{PrefabResourcePath} is missing required references - " +
                             "using the code-built layout instead.");
            Destroy(go);
            return null;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        return view;
    }

    /// <summary>
    /// Wires the buttons and hides everything. Shared by both paths; the authored prefab is
    /// saved with everything visible so it can be edited.
    /// </summary>
    private void Initialize()
    {
        confirmButton.onClick.AddListener(() => Answer(true));
        declineButton.onClick.AddListener(() => Answer(false));

        backdrop.gameObject.SetActive(false);
        modal.gameObject.SetActive(false);
    }

    /// <summary>Code-built fallback layout. Mirrored by QuitConfirmPrefabSetup.</summary>
    private void Build()
    {
        Transform root = transform;

        var backdropRect = RunOverlayUI.CreateChild("Backdrop", root);
        RunOverlayUI.Stretch(backdropRect);
        backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = RunOverlayUI.Backdrop;
        backdrop.raycastTarget = true;

        modal = RunOverlayUI.CreateChild("Modal", root);
        RunOverlayUI.Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 480f));
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;

        titleLabel = RunOverlayUI.CreateLabel("Title", modal, string.Empty, 52f, RunOverlayUI.Gold);
        RunOverlayUI.Place(titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(800f, 110f));

        bodyLabel = RunOverlayUI.CreateLabel("Body", modal, string.Empty, 32f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(bodyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(800f, 140f));

        // Staying is the safe answer, so it gets the primary (jade, right) slot
        confirmButton = RunOverlayUI.CreateButton("Confirm", modal, string.Empty, RunOverlayUI.Clay, out confirmLabel);
        RunOverlayUI.Place((RectTransform)confirmButton.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 100f), new Vector2(400f, 110f));

        declineButton = RunOverlayUI.CreateButton("Decline", modal, string.Empty, RunOverlayUI.Jade, out declineLabel);
        RunOverlayUI.Place((RectTransform)declineButton.transform, new Vector2(0.5f, 0f), new Vector2(220f, 100f), new Vector2(400f, 110f));
    }

    private void Open()
    {
        if (showing) return;
        showing = true;
        answered = false;

        titleLabel.text = LocalizationManager.Get("quit_confirm_title");
        bodyLabel.text = LocalizationManager.Get("quit_confirm_body");
        confirmLabel.text = LocalizationManager.Get("quit_confirm_leave");
        declineLabel.text = LocalizationManager.Get("quit_confirm_stay");

        UIPopup.Show(backdrop.gameObject);
        UIPopup.Show(modal.gameObject);

        // Back again while the card is up means "stay"
        BackButton.Register(OnBack);
    }

    private bool OnBack()
    {
        Answer(false);
        return true;
    }

    private void Answer(bool leave)
    {
        if (answered) return;
        answered = true;
        BackButton.Unregister(OnBack);

        UIPopup.Hide(backdrop.gameObject);
        UIPopup.Hide(modal.gameObject, () =>
        {
            showing = false;
            if (leave) Quit();
        });
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        BackButton.Unregister(OnBack);
        if (instance == this) instance = null;
    }
}
