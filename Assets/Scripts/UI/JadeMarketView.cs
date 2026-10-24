using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Jade Market: spend jade on cosmetic colours (Kukulkan's light, landing dust) and choose
/// which one is worn. Nothing sold here touches a score, a window or a speed, so ranked boards
/// stay fair.
///
/// Opened from the main-menu chip (<see cref="JadeMarketMenuChip"/>). Presentation comes only
/// from the authored prefab at Resources/UI/JadeMarket; without it a warning names the menu that
/// builds it and nothing opens (no code-built fallback). Menu: TamalStacker ▸ UI ▸ Create Jade
/// Market Prefab. Rows are cloned from the prefab's own row/header templates.
/// </summary>
public class JadeMarketView : MonoBehaviour
{
    public const int SortingOrder = 4300;
    public const string PrefabResourcePath = "UI/JadeMarket";

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI balanceLabel;
    [Tooltip("The jade emblem and count. When set it replaces the plain balance label and plays every spend.")]
    [SerializeField] private JadeBalanceBar balanceBar;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI closeLabel;
    [Tooltip("Parent of the cloned rows (the scroll content).")]
    [SerializeField] private RectTransform content;
    [SerializeField] private JadeMarketRow rowTemplate;
    [SerializeField] private TextMeshProUGUI headerTemplate;

    private static JadeMarketView instance;

    private readonly List<JadeMarketRow> rows = new List<JadeMarketRow>();
    private bool built;
    private bool showing;

    /// <summary>True when there is something to sell: cosmetics and jade are both switched on.</summary>
    public static bool Available => CosmeticCatalog.Active != null && JadeSettings.Active != null;

    public static bool IsShowing => instance != null && instance.showing;

    public static bool Show()
    {
        if (!Available) return false;
        if (instance == null) instance = Create();
        if (instance == null) return false;
        instance.Open();
        return true;
    }

    private static JadeMarketView Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[JadeMarket] Resources/{PrefabResourcePath} is missing - run " +
                             "TamalStacker > UI > Create Jade Market Prefab. The market stays closed.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "JadeMarketView";
        var view = go.GetComponent<JadeMarketView>();
        if (view == null || view.panel == null || view.titleLabel == null || view.balanceLabel == null ||
            view.closeButton == null || view.content == null || view.headerTemplate == null ||
            view.rowTemplate == null || !view.rowTemplate.IsComplete)
        {
            Debug.LogWarning($"[JadeMarket] Resources/{PrefabResourcePath} is missing its view or references - " +
                             "rebuild it with TamalStacker > UI > Create Jade Market Prefab. The market stays closed.");
            Destroy(go);
            return null;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        view.rowTemplate.gameObject.SetActive(false);
        if (view.balanceBar != null) view.balanceLabel.gameObject.SetActive(false);
        view.headerTemplate.gameObject.SetActive(false);
        view.closeButton.onClick.AddListener(view.Close);
        view.panel.gameObject.SetActive(false);
        DontDestroyOnLoad(go);
        return view;
    }

    private void Open()
    {
        if (!built) Build();

        titleLabel.text = LocalizationManager.Get("market_title");
        if (closeLabel != null) closeLabel.text = LocalizationManager.Get("market_close");
        RefreshAll();

        JadeWallet.OnBalanceChanged += OnBalance;
        CosmeticStore.OnChanged += RefreshAll;
        showing = true;
        UIPopup.Show(panel.gameObject);
    }

    private void Close()
    {
        if (!showing) return;
        showing = false;
        JadeWallet.OnBalanceChanged -= OnBalance;
        CosmeticStore.OnChanged -= RefreshAll;
        UIPopup.Hide(panel.gameObject);
    }

    private void Build()
    {
        built = true;
        CosmeticCatalog catalog = CosmeticCatalog.Active;
        if (catalog == null) return;

        // Sky themes lead: they are what the temple map used to hold, and the biggest change to buy.
        var order = new[] { CosmeticKind.Theme, CosmeticKind.KukulkanShift, CosmeticKind.LandingBurst };
        foreach (CosmeticKind kind in order)
        {
            var header = Instantiate(headerTemplate, content);
            header.gameObject.SetActive(true);
            header.text = LocalizationManager.Get("market_section_" + kind);

            foreach (CosmeticItem item in catalog.items)
            {
                if (item == null || item.kind != kind || string.IsNullOrEmpty(item.id)) continue;
                var row = Instantiate(rowTemplate, content);
                row.gameObject.SetActive(true);
                row.Bind(item, OnRowPressed);
                rows.Add(row);
            }
        }
    }

    private void OnRowPressed(JadeMarketRow row, CosmeticItem item)
    {
        DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound);

        // Already owned: just wear it.
        if (CosmeticStore.IsOwned(item))
        {
            CosmeticStore.Equip(item);
            row.PlayWorn();
            HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
            if (sound != null) sound.PlayButtonClick();
            return;
        }

        // Can't afford it: say what is missing instead of doing nothing.
        if (JadeWallet.Balance < item.price)
        {
            row.PlayRefused();
            JadeBalanceBar.Nudge(LocalizationManager.Get("market_need_more", item.price - JadeWallet.Balance));
            HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
            return;
        }

        // Pay, then let the jade visibly leave the balance and land on the purchase. The bar holds its
        // number across the payment so the count ticks down with the pieces instead of snapping.
        JadeBalanceBar.HoldCount();
        if (!CosmeticStore.TryBuy(item))
        {
            JadeBalanceBar.ReleaseHold();
            HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
            return;
        }

        CosmeticStore.Equip(item);
        if (sound != null) sound.PlayButtonClick();
        JadeBalanceBar.SpendTo(row.PriceAnchor, item.price, row.PlayPurchased);
    }

    private void OnBalance(int _) => RefreshAll();

    private void RefreshAll()
    {
        int balance = JadeWallet.Balance;
        balanceLabel.text = LocalizationManager.Get("market_balance", balance);
        foreach (JadeMarketRow row in rows) row.Refresh(balance);
    }

    private void OnDestroy()
    {
        JadeWallet.OnBalanceChanged -= OnBalance;
        CosmeticStore.OnChanged -= RefreshAll;
        if (instance == this) instance = null;
    }
}
