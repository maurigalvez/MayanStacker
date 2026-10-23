using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One line of the market: a colour swatch, the item's name and a Buy / Wear button.</summary>
public class JadeMarketRow : MonoBehaviour
{
    [SerializeField] private Image swatch;
    [SerializeField] private Image swatch2;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionLabel;
    [Tooltip("Marks the row that is currently worn.")]
    [SerializeField] private Image wornMarker;

    public bool IsComplete => swatch != null && nameLabel != null && actionButton != null && actionLabel != null;

    private CosmeticItem item;

    public void Bind(CosmeticItem bound, System.Action<CosmeticItem> onPressed)
    {
        item = bound;
        swatch.color = bound.color;
        if (swatch2 != null) swatch2.color = bound.kind == CosmeticKind.LandingBurst ? bound.color2 : bound.color;
        if (bound.icon != null)
        {
            swatch.sprite = bound.icon;
            swatch.color = Color.white;
        }
        nameLabel.text = LocalizationManager.Get(bound.nameKey);
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => onPressed(item));
    }

    public void Refresh(int balance)
    {
        bool owned = CosmeticStore.IsOwned(item);
        bool worn = owned && CosmeticStore.Equipped(item.kind) == item;

        if (wornMarker != null) wornMarker.enabled = worn;
        actionButton.interactable = !worn && (owned || balance >= item.price);
        if (worn) actionLabel.text = LocalizationManager.Get("market_equipped");
        else if (owned) actionLabel.text = LocalizationManager.Get("market_equip");
        else actionLabel.text = LocalizationManager.Get("market_buy", item.price);
    }
}

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

        foreach (CosmeticKind kind in System.Enum.GetValues(typeof(CosmeticKind)))
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

    private void OnRowPressed(CosmeticItem item)
    {
        if (!CosmeticStore.IsOwned(item) && !CosmeticStore.TryBuy(item))
        {
            HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
            return;
        }

        CosmeticStore.Equip(item);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
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
