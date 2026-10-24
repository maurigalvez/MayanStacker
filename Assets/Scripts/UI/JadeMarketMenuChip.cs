using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The main-menu "MARKET" chip: shows the jade balance and opens the <see cref="JadeMarketView"/>.
/// Faded out (not deactivated, so it comes back) when cosmetics or jade are switched off.
///
/// Built into the Main Menu Canvas prefab by TamalStacker ▸ Economy ▸ Add Jade Market Menu Chip.
/// </summary>
[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class JadeMarketMenuChip : MonoBehaviour
{
    [Tooltip("Shows the current jade balance.")]
    [SerializeField] private TextMeshProUGUI balanceLabel;

    private Button button;
    private const string SeenKey = "market";

    private CanvasGroup group;
    private MenuRailCue cue;

    private void Awake()
    {
        button = GetComponent<Button>();
        group = GetComponent<CanvasGroup>();
        cue = GetComponent<MenuRailCue>();
        button.onClick.AddListener(OnPressed);
    }

    private void OnEnable()
    {
        JadeWallet.OnBalanceChanged += OnBalance;
        MenuRailPacing.RevealChanged += Refresh;
        Refresh();
    }

    // OnEnable can run before LocalizationManager.Awake on a fresh scene load, which leaves the raw
    // key on the label; Start runs after every Awake, and the language hook keeps it current.
    private void Start()
    {
        Refresh();
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged += Refresh;
        StartCoroutine(MenuRailPacing.AnnounceLater(MenuRailPacing.Feature.Market));
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= Refresh;
    }

    private void OnDisable()
    {
        JadeWallet.OnBalanceChanged -= OnBalance;
        MenuRailPacing.RevealChanged -= Refresh;
    }

    private void OnBalance(int _) => Refresh();

    private void Refresh()
    {
        bool show = JadeMarketView.Available && MenuRailPacing.IsRevealed(MenuRailPacing.Feature.Market);
        MenuRailItem.SetShown(group, show);
        if (show && cue != null)
        {
            // Lit when more is within reach than the last time the player looked.
            int affordable = AffordableCount();
            if (MenuRailCue.Seen(SeenKey) > affordable) MenuRailCue.MarkSeen(SeenKey, affordable);
            cue.Set(affordable > MenuRailCue.Seen(SeenKey) || MenuRailPacing.IsFresh(MenuRailPacing.Feature.Market));
        }
        if (balanceLabel != null) balanceLabel.text = LocalizationManager.Get("market_balance", JadeWallet.Balance);
    }

    private static int AffordableCount()
    {
        var catalog = CosmeticCatalog.Active;
        if (catalog == null) return 0;
        int balance = JadeWallet.Balance, count = 0;
        foreach (CosmeticItem item in catalog.items)
            if (item != null && item.price > 0 && item.price <= balance && !CosmeticStore.IsOwned(item)) count++;
        return count;
    }

    private void OnPressed()
    {
        MenuRailCue.MarkSeen(SeenKey, AffordableCount());
        MenuRailPacing.Introduced(MenuRailPacing.Feature.Market);
        if (cue != null) cue.Set(false);
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        JadeMarketView.Show();
    }
}
