using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One power's card on the loadout screen. Lives on the card template inside
/// <see cref="PowerLoadoutView"/>'s prefab and is cloned once per power (locked ones too, so the
/// player sees what's coming and which level unlocks it), so styling the template styles every card.
///
/// Only <see cref="button"/> is required; every other part is optional and simply skipped
/// when left empty. Selected/unselected colours live on the view, not here.
/// </summary>
public class PowerLoadoutCard : MonoBehaviour
{
    [Tooltip("Tapping the card selects this power.")]
    public Button button;

    [Tooltip("Card slab, tinted by the view's selected/unselected card colours. Optional.")]
    public Image background;

    [Tooltip("The power's icon. Hidden when the power has no icon.")]
    public Image icon;

    [Tooltip("Medallion ring, tinted with the power's accent colour when selected. Optional.")]
    public Image ring;

    [Tooltip("The power's name, filled from localization at runtime. Optional.")]
    public TextMeshProUGUI nameText;

    [Tooltip("Shown only on the selected card (e.g. a glow or a check). Optional.")]
    public GameObject selectedMarker;

    [Tooltip("Shown only while the power is locked (e.g. a lock badge). Optional.")]
    public GameObject lockedMarker;

    [Tooltip("\"Beat level N\" on a locked card, filled from localization. Optional.")]
    public TextMeshProUGUI lockText;

    [System.NonSerialized] public PowerId id;
    [System.NonSerialized] public Color accent;
    [System.NonSerialized] public bool locked;
}
