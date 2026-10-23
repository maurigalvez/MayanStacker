using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One line of the market: a colour swatch, the item's name and a Buy / Wear button.
/// Lives in its own file because Unity only binds a MonoBehaviour whose class name matches the file.
/// </summary>
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
