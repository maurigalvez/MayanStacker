using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "Remove Ads" offer card shown before Google Play's purchase sheet, so the player sees
/// exactly what they're paying and what they get before any charge: Play's localized price,
/// that it's a one-time purchase (not a subscription), that it removes the ads between runs,
/// and that it stays with their Google Play account. Only "Buy" continues to Play.
///
/// The copy promises the ads *between runs* (the interstitials), deliberately not "all ads",
/// so optional rewarded ads can be added later without breaking what buyers paid for.
///
/// Presentation comes from a prefab at Resources/UI/RemoveAdsOffer when one exists, styled
/// like the rest of the temple UI. Without it the card builds itself in code on its own
/// overlay canvas, like <see cref="QuitConfirmView"/>, so it needs no scene wiring.
///
/// Menu: TamalStacker ▸ UI ▸ Create Remove Ads Offer Prefab generates that prefab in the
/// house style (see UIHouseStyle).
/// </summary>
public class RemoveAdsOfferView : MonoBehaviour
{
    /// <summary>Above the menu and its panels, below the quit confirm.</summary>
    public const int SortingOrder = 4400;

    /// <summary>Authored prefab that replaces the code-built layout when present.</summary>
    public const string PrefabResourcePath = "UI/RemoveAdsOffer";

    [Tooltip("Full-screen tap blocker behind the card.")]
    [SerializeField] private Image backdrop;
    [SerializeField] private RectTransform modal;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [Tooltip("\"$2.99 · one-time purchase\" — the price, shown before anything else.")]
    [SerializeField] private TextMeshProUGUI priceLabel;
    [Tooltip("What the purchase does: no ads between runs, no subscription, tied to the Play account.")]
    [SerializeField] private TextMeshProUGUI bodyLabel;
    [Tooltip("\"Buy · $2.99\" — opens Google Play's purchase sheet.")]
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI buyLabel;
    [Tooltip("\"Not now\" — closes the card.")]
    [SerializeField] private Button declineButton;
    [SerializeField] private TextMeshProUGUI declineLabel;

    private static RemoveAdsOfferView instance;

    private bool showing;
    private bool answered;
    private Action onBuy;
    private string source;

    public static bool IsShowing => instance != null && instance.showing;

    /// <summary>
    /// Shows the offer for <paramref name="price"/> (Play's localized price string). Callers
    /// must not open it without a price: the whole point is that it's stated before the charge.
    /// <paramref name="buy"/> runs only if the player taps Buy, after the card has closed.
    /// <paramref name="source"/> names the placement that opened it, for analytics. Callers go
    /// through <see cref="RemoveAdsOffer.TryOpen"/> rather than calling this directly.
    /// </summary>
    public static void Show(string price, string source, Action buy)
    {
        if (string.IsNullOrEmpty(price))
        {
            Debug.LogWarning("[RemoveAdsOffer] No price to show — not opening the offer.");
            return;
        }

        if (instance == null) instance = Create();
        instance.Open(price, source, buy);
    }

    private static RemoveAdsOfferView Create()
    {
        RemoveAdsOfferView view = CreateFromPrefab();
        if (view == null)
        {
            var host = new GameObject("RemoveAdsOfferView");
            RunOverlayUI.CreateCanvas(host, SortingOrder, interactive: true);
            view = host.AddComponent<RemoveAdsOfferView>();
            view.Build();
        }

        view.Initialize();
        DontDestroyOnLoad(view.gameObject);
        return view;
    }

    private static RemoveAdsOfferView CreateFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null) return null;

        if (prefab.GetComponent<RemoveAdsOfferView>() == null)
        {
            Debug.LogWarning($"[RemoveAdsOffer] Resources/{PrefabResourcePath} has no RemoveAdsOfferView " +
                             "component - using the code-built layout instead.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "RemoveAdsOfferView";
        var view = go.GetComponent<RemoveAdsOfferView>();

        if (view.backdrop == null || view.modal == null || view.titleLabel == null ||
            view.priceLabel == null || view.bodyLabel == null || view.buyButton == null ||
            view.buyLabel == null || view.declineButton == null || view.declineLabel == null)
        {
            Debug.LogWarning($"[RemoveAdsOffer] Resources/{PrefabResourcePath} is missing required references - " +
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
        buyButton.onClick.AddListener(() => Answer(true));
        declineButton.onClick.AddListener(() => Answer(false));

        backdrop.gameObject.SetActive(false);
        modal.gameObject.SetActive(false);
    }

    /// <summary>Code-built fallback layout. Mirrored by RemoveAdsOfferPrefabSetup.</summary>
    private void Build()
    {
        Transform root = transform;

        var backdropRect = RunOverlayUI.CreateChild("Backdrop", root);
        RunOverlayUI.Stretch(backdropRect);
        backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = RunOverlayUI.Backdrop;
        backdrop.raycastTarget = true;

        modal = RunOverlayUI.CreateChild("Modal", root);
        RunOverlayUI.Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, ModalSize);
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;

        titleLabel = RunOverlayUI.CreateLabel("Title", modal, string.Empty, 52f, RunOverlayUI.Gold);
        RunOverlayUI.Place(titleLabel.rectTransform, new Vector2(0.5f, 1f), TitlePosition, TitleSize);

        priceLabel = RunOverlayUI.CreateLabel("Price", modal, string.Empty, 42f, RunOverlayUI.Gold);
        RunOverlayUI.Place(priceLabel.rectTransform, new Vector2(0.5f, 1f), PricePosition, PriceSize);

        bodyLabel = RunOverlayUI.CreateLabel("Body", modal, string.Empty, 30f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(bodyLabel.rectTransform, new Vector2(0.5f, 0.5f), BodyPosition, BodySize);

        declineButton = RunOverlayUI.CreateButton("Decline", modal, string.Empty, RunOverlayUI.Clay, out declineLabel);
        RunOverlayUI.Place((RectTransform)declineButton.transform, new Vector2(0.5f, 0f), DeclinePosition, ButtonSize);

        buyButton = RunOverlayUI.CreateButton("Buy", modal, string.Empty, RunOverlayUI.Jade, out buyLabel);
        RunOverlayUI.Place((RectTransform)buyButton.transform, new Vector2(0.5f, 0f), BuyPosition, ButtonSize);
    }

    // Shared with RemoveAdsOfferPrefabSetup so both layouts stay identical.
    public static readonly Vector2 ModalSize = new Vector2(900f, 760f);
    public static readonly Vector2 TitlePosition = new Vector2(0f, -95f);
    public static readonly Vector2 TitleSize = new Vector2(800f, 100f);
    public static readonly Vector2 PricePosition = new Vector2(0f, -185f);
    public static readonly Vector2 PriceSize = new Vector2(800f, 70f);
    public static readonly Vector2 BodyPosition = new Vector2(0f, 20f);
    public static readonly Vector2 BodySize = new Vector2(780f, 250f);
    public static readonly Vector2 ButtonSize = new Vector2(400f, 110f);
    public static readonly Vector2 DeclinePosition = new Vector2(-220f, 100f);
    public static readonly Vector2 BuyPosition = new Vector2(220f, 100f);

    private void Open(string price, string openedFrom, Action buy)
    {
        if (showing) return;
        showing = true;
        answered = false;
        onBuy = buy;
        source = openedFrom;

        titleLabel.text = LocalizationManager.Get("remove_ads_offer_title");
        priceLabel.text = LocalizationManager.Get("remove_ads_offer_price", price);
        bodyLabel.text = LocalizationManager.Get("remove_ads_offer_body");
        buyLabel.text = LocalizationManager.Get("remove_ads_offer_buy", price);
        declineLabel.text = LocalizationManager.Get("remove_ads_offer_decline");

        UIPopup.Show(backdrop.gameObject);
        UIPopup.Show(modal.gameObject);

        GameAnalytics.Purchase(RemoveAds.ProductId, "offer_shown", source);

        // Back while the card is up means "not now"
        BackButton.Register(OnBack);
    }

    private bool OnBack()
    {
        Answer(false);
        return true;
    }

    private void Answer(bool buy)
    {
        if (answered) return;
        answered = true;
        BackButton.Unregister(OnBack);

        if (!buy) GameAnalytics.Purchase(RemoveAds.ProductId, "offer_declined", source);

        Action pending = buy ? onBuy : null;
        onBuy = null;

        UIPopup.Hide(backdrop.gameObject);
        UIPopup.Hide(modal.gameObject, () =>
        {
            showing = false;
            pending?.Invoke();
        });
    }

    private void OnDestroy()
    {
        BackButton.Unregister(OnBack);
        if (instance == this) instance = null;
    }
}
