using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the pick-a-power screen (<see cref="PowerLoadoutScreen"/>).
///
/// Put this component on a prefab at Resources/UI/PowerLoadoutScreen and the screen uses it:
/// same behaviour, but layout, art, colours and fonts are edited in the prefab like any other
/// panel. Without the prefab (or with one missing the card template or PLAY button) the screen
/// builds this same layout in code.
///
/// Cards are cloned from <see cref="cardTemplate"/>, one per unlocked power, into
/// <see cref="cardsContainer"/> (a LayoutGroup there is honoured). Title, description and
/// button labels are filled from localization at runtime; what the prefab says is a placeholder.
///
/// Menu: TamalStacker ▸ Powers ▸ Create Loadout Screen Prefab generates a prefab matching the
/// code layout, as a starting point to restyle.
/// </summary>
public class PowerLoadoutView : MonoBehaviour
{
    public const string PrefabResourcePath = "UI/PowerLoadoutScreen";

    /// <summary>Sits above the menu and below the language picker (5000).</summary>
    public const int DefaultSortingOrder = 4500;

    [Header("Parts")]
    [Tooltip("Scaled in and out when the screen opens and closes. Defaults to this object.")]
    [SerializeField] private GameObject panel;

    [Tooltip("Full-screen tap-catcher behind the panel; tapping it backs out. Optional.")]
    [SerializeField] private Button backdropButton;

    [Tooltip("Headline (\"Choose a power\"). Optional.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Parent the cards are cloned into. Defaults to the template's parent.")]
    [SerializeField] private RectTransform cardsContainer;

    [Tooltip("Cloned once per unlocked power. Disabled at runtime; keep it enabled in the prefab so it stays easy to edit.")]
    [SerializeField] private PowerLoadoutCard cardTemplate;

    [Tooltip("What the selected power does. Optional.")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Tooltip("Confirms the pick and starts the run. Required.")]
    [SerializeField] private Button playButton;
    [SerializeField] private TextMeshProUGUI playLabel;

    [Tooltip("Backs out to the menu. Optional.")]
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI backLabel;

    [Header("Selected card")]
    [SerializeField] private Color selectedCardColor = RunOverlayUI.Jade;
    [SerializeField] private Color selectedIconColor = Color.white;
    [SerializeField] private Color selectedNameColor = RunOverlayUI.Parchment;
    [Tooltip("Tint the selected card's ring with the power's own accent colour instead of the colour below.")]
    [SerializeField] private bool ringUsesPowerAccent = true;
    [SerializeField] private Color selectedRingColor = RunOverlayUI.Gold;
    [SerializeField] private float selectedScale = 1f;

    [Header("Other cards")]
    [SerializeField] private Color unselectedCardColor = RunOverlayUI.Stone;
    [SerializeField] private Color unselectedIconColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color unselectedNameColor = RunOverlayUI.Muted;
    [SerializeField] private Color unselectedRingColor = new Color(0.33f, 0.27f, 0.2f, 1f);
    [SerializeField] private float unselectedScale = 0.9f;

    private readonly List<PowerLoadoutCard> cards = new List<PowerLoadoutCard>();

    public bool IsUsable => cardTemplate != null && cardTemplate.button != null && playButton != null;

    /// <summary>What the open/close animation scales.</summary>
    public GameObject PopupTarget => panel != null ? panel : gameObject;

    /// <summary>Fills in the texts and clones one card per power in <paramref name="powers"/>.</summary>
    public void Populate(List<PowerDefinition> powers, Action<PowerId> onCard, Action onPlay, Action onBack)
    {
        if (titleText != null) titleText.text = LocalizationManager.Get("power_loadout_title");
        if (playLabel != null) playLabel.text = LocalizationManager.Get("power_loadout_play");
        if (backLabel != null) backLabel.text = LocalizationManager.Get("power_loadout_back");

        playButton.onClick.AddListener(() => onPlay());
        if (backButton != null) backButton.onClick.AddListener(() => onBack());
        if (backdropButton != null) backdropButton.onClick.AddListener(() => onBack());

        Transform parent = cardsContainer != null ? (Transform)cardsContainer : cardTemplate.transform.parent;
        cardTemplate.gameObject.SetActive(false);

        foreach (PowerDefinition def in powers)
        {
            PowerLoadoutCard card = Instantiate(cardTemplate, parent);
            card.name = "Power_" + def.id;
            card.gameObject.SetActive(true);
            card.id = def.id;
            card.accent = def.accentColor;

            if (card.icon != null)
            {
                card.icon.sprite = def.icon;
                card.icon.enabled = def.icon != null;
            }
            if (card.nameText != null) card.nameText.text = LocalizationManager.Get(def.nameKey);

            PowerId id = def.id;
            card.button.onClick.AddListener(() => onCard(id));
            cards.Add(card);
        }
    }

    /// <summary>Marks <paramref name="id"/> as the picked card and shows its description.</summary>
    public void SetSelected(PowerId id, string description)
    {
        foreach (PowerLoadoutCard c in cards)
        {
            bool on = c.id == id;
            c.transform.localScale = Vector3.one * (on ? selectedScale : unselectedScale);
            if (c.background != null) c.background.color = on ? selectedCardColor : unselectedCardColor;
            if (c.icon != null) c.icon.color = on ? selectedIconColor : unselectedIconColor;
            if (c.ring != null) c.ring.color = on ? (ringUsesPowerAccent ? c.accent : selectedRingColor) : unselectedRingColor;
            if (c.nameText != null) c.nameText.color = on ? selectedNameColor : unselectedNameColor;
            if (c.selectedMarker != null) c.selectedMarker.SetActive(on);
        }

        if (descriptionText != null) descriptionText.text = description;
    }

    // ---- Default layout ----

    /// <summary>
    /// Builds the default screen on <paramref name="host"/>: its own interactive overlay canvas,
    /// a dimmed backdrop and a centred panel. Used by the runtime fallback and by the editor
    /// prefab tool, so a fresh prefab starts identical to the code layout.
    /// </summary>
    public static PowerLoadoutView BuildDefault(GameObject host)
    {
        RunOverlayUI.CreateCanvas(host, DefaultSortingOrder, interactive: true);
        var view = host.AddComponent<PowerLoadoutView>();

        // Full-bleed backdrop; eats taps so the menu behind can't be hit.
        RectTransform backdrop = RunOverlayUI.CreateChild("Backdrop", host.transform);
        RunOverlayUI.Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        view.backdropButton = backdrop.gameObject.AddComponent<Button>();
        view.backdropButton.transition = Selectable.Transition.None;
        backdrop.gameObject.AddComponent<NoPressScale>();

        // Stretches across the screen with a margin, so four cards still fit a narrow phone.
        RectTransform panel = RunOverlayUI.CreateChild("Panel", host.transform);
        panel.anchorMin = new Vector2(0f, 0.5f);
        panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.offsetMin = new Vector2(40f, -500f);
        panel.offsetMax = new Vector2(-40f, 500f);
        panel.gameObject.AddComponent<Image>().color = RunOverlayUI.Backdrop;
        // Swallows taps on the panel so they don't reach the backdrop's "back".
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        panel.gameObject.AddComponent<NoPressScale>();
        view.panel = panel.gameObject;

        view.titleText = RunOverlayUI.CreateLabel("Title", panel, "Choose a power", 60f, RunOverlayUI.Gold);
        StretchRow(view.titleText.rectTransform, -85f, 90f, 30f);

        RectTransform container = RunOverlayUI.CreateChild("Cards", panel);
        StretchRow(container, -320f, 300f, 30f);
        var row = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 20f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;   // cards shrink toward minWidth when space runs out
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        view.cardsContainer = container;
        view.cardTemplate = BuildDefaultCard(container);

        view.descriptionText = RunOverlayUI.CreateLabel("Description", panel,
            "What the selected power does.", 32f, RunOverlayUI.Parchment);
        StretchRow(view.descriptionText.rectTransform, -580f, 170f, 50f);

        view.playButton = RunOverlayUI.CreateButton("Play", panel, "PLAY", RunOverlayUI.Jade, out view.playLabel);
        view.playLabel.fontSize = 56f;
        RunOverlayUI.Place((RectTransform)view.playButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(460f, 140f));

        view.backButton = RunOverlayUI.CreateButton("Back", panel, "Back", Color.clear, out view.backLabel);
        view.backLabel.fontSize = 34f;
        view.backLabel.color = RunOverlayUI.Muted;
        RunOverlayUI.Place((RectTransform)view.backButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(300f, 80f));

        return view;
    }

    private static PowerLoadoutCard BuildDefaultCard(RectTransform container)
    {
        const float iconSize = 150f;

        Button button = RunOverlayUI.CreateButton("CardTemplate", container, string.Empty,
            RunOverlayUI.Stone, out TextMeshProUGUI generated);
        DestroyImmediate(generated.gameObject);

        var rect = (RectTransform)button.transform;
        rect.sizeDelta = new Vector2(230f, 300f);
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = 170f;
        layout.preferredWidth = 230f;
        layout.preferredHeight = 300f;

        var card = rect.gameObject.AddComponent<PowerLoadoutCard>();
        card.button = button;
        card.background = (Image)button.targetGraphic;

        // The same medallion the meter uses in-run, so the pick reads as "this button".
        Sprite baseSprite = Resources.Load<Sprite>(PowerMeterView.BaseSpritePath);
        Sprite ringSprite = Resources.Load<Sprite>(PowerMeterView.RingSpritePath);

        RectTransform medallion = RunOverlayUI.CreateChild("Medallion", rect);
        RunOverlayUI.Place(medallion, new Vector2(0.5f, 1f), new Vector2(0f, -20f - iconSize * 0.5f), new Vector2(iconSize, iconSize));
        if (baseSprite != null)
        {
            var disc = medallion.gameObject.AddComponent<Image>();
            disc.sprite = baseSprite;
            disc.preserveAspect = true;
            disc.raycastTarget = false;
        }

        if (ringSprite != null)
        {
            RectTransform ringRect = RunOverlayUI.CreateChild("Ring", medallion);
            RunOverlayUI.Stretch(ringRect);
            card.ring = ringRect.gameObject.AddComponent<Image>();
            card.ring.sprite = ringSprite;
            card.ring.preserveAspect = true;
            card.ring.raycastTarget = false;
        }

        RectTransform iconRect = RunOverlayUI.CreateChild("Icon", medallion);
        RunOverlayUI.Stretch(iconRect);
        float inset = iconSize * 0.21f;
        iconRect.offsetMin = new Vector2(inset, inset);
        iconRect.offsetMax = new Vector2(-inset, -inset);
        card.icon = iconRect.gameObject.AddComponent<Image>();
        card.icon.preserveAspect = true;
        card.icon.raycastTarget = false;

        card.nameText = RunOverlayUI.CreateLabel("Name", rect, "Power", 30f, RunOverlayUI.Parchment);
        RectTransform nameRect = card.nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0f);
        nameRect.pivot = new Vector2(0.5f, 0.5f);
        nameRect.anchoredPosition = new Vector2(0f, 55f);
        nameRect.sizeDelta = new Vector2(-20f, 90f);

        return card;
    }

    /// <summary>A full-width row under the panel's top edge, inset by <paramref name="margin"/>.</summary>
    private static void StretchRow(RectTransform rt, float centreY, float height, float margin)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, centreY);
        rt.sizeDelta = new Vector2(-margin * 2f, height);
    }
}
