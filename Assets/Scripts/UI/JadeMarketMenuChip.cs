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
    private CanvasGroup group;

    private void Awake()
    {
        button = GetComponent<Button>();
        group = GetComponent<CanvasGroup>();
        button.onClick.AddListener(OnPressed);
    }

    private void OnEnable()
    {
        JadeWallet.OnBalanceChanged += OnBalance;
        Refresh();
    }

    private void OnDisable()
    {
        JadeWallet.OnBalanceChanged -= OnBalance;
    }

    private void OnBalance(int _) => Refresh();

    private void Refresh()
    {
        bool show = JadeMarketView.Available;
        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
        if (balanceLabel != null) balanceLabel.text = LocalizationManager.Get("market_balance", JadeWallet.Balance);
    }

    private void OnPressed()
    {
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        JadeMarketView.Show();
    }
}
