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

    [Header("Scroll hints")]
    [Tooltip("Shown while there are more cards off the left edge. Hint only, not tappable. Built in code (gold chevron) when empty.")]
    [SerializeField] private RectTransform leftArrow;
    [Tooltip("Shown while there are more cards off the right edge. Hint only, not tappable. Built in code (gold chevron) when empty.")]
    [SerializeField] private RectTransform rightArrow;
    [Tooltip("How far the arrows drift outward and back, in pixels, so they read as \"swipe\".")]
    [SerializeField] private float arrowBob = 10f;

    private readonly List<PowerLoadoutCard> cards = new List<PowerLoadoutCard>();

    private CanvasGroup leftArrowGroup;
    private CanvasGroup rightArrowGroup;
    private float leftArrowBaseX;
    private float rightArrowBaseX;

    // The strip the cards scroll in; made at runtime around the card row if the prefab has none.
    private ScrollRect cardsScroll;

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
        cardsScroll = parent.GetComponentInParent<ScrollRect>(true);
        if (cardsScroll == null) cardsScroll = MakeScrollable((RectTransform)parent);
        SetUpArrows();
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

    /// <summary>Scrolls the card strip so <paramref name="id"/>'s card sits as close to the middle as it can.</summary>
    public void ScrollTo(PowerId id)
    {
        if (cardsScroll == null || cardsScroll.content == null) return;

        RectTransform content = cardsScroll.content;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float overflow = content.rect.width - cardsScroll.viewport.rect.width;
        if (overflow <= 0f) return; // everything fits; the strip stays centred

        foreach (PowerLoadoutCard c in cards)
        {
            if (c.id != id) continue;
            // Card centre measured from the content's left edge, minus half a viewport = scroll offset.
            float cardCentre = ((RectTransform)c.transform).anchoredPosition.x;
            float offset = cardCentre - cardsScroll.viewport.rect.width * 0.5f;
            cardsScroll.horizontalNormalizedPosition = Mathf.Clamp01(offset / overflow);
            return;
        }
    }

    /// <summary>
    /// Readies the scroll hints. A prefab's own arrows win; otherwise gold chevrons are laid
    /// over the strip's edges. Both start hidden, and LateUpdate shows each one only while
    /// there are cards off that edge, so with everything fitting neither ever appears.
    /// </summary>
    private void SetUpArrows()
    {
        if ((leftArrow == null || rightArrow == null) && cardsScroll != null)
        {
            RectTransform lane = BuildArrowLane((RectTransform)cardsScroll.transform);
            if (leftArrow == null) leftArrow = (RectTransform)lane.Find("LeftArrow");
            if (rightArrow == null) rightArrow = (RectTransform)lane.Find("RightArrow");
        }

        leftArrowGroup = PrepareArrow(leftArrow, out leftArrowBaseX);
        rightArrowGroup = PrepareArrow(rightArrow, out rightArrowBaseX);
    }

    private static CanvasGroup PrepareArrow(RectTransform arrow, out float baseX)
    {
        baseX = 0f;
        if (arrow == null) return null;

        baseX = arrow.anchoredPosition.x;
        var group = arrow.GetComponent<CanvasGroup>();
        if (group == null) group = arrow.gameObject.AddComponent<CanvasGroup>();
        // A hint, never a target: drags that start on it still reach the strip.
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;
        return group;
    }

    private void LateUpdate()
    {
        if (cardsScroll == null || cardsScroll.content == null || cardsScroll.viewport == null) return;

        float overflow = cardsScroll.content.rect.width - cardsScroll.viewport.rect.width;
        bool canScroll = overflow > 1f;
        float position = cardsScroll.horizontalNormalizedPosition;

        // Unscaled so the hint never freezes if the screen opens while time is paused.
        float step = Time.unscaledDeltaTime * 8f;
        float drift = (Mathf.Sin(Time.unscaledTime * 4f) * 0.5f + 0.5f) * arrowBob;

        UpdateArrow(leftArrow, leftArrowGroup, canScroll && position > 0.01f, leftArrowBaseX - drift, step);
        UpdateArrow(rightArrow, rightArrowGroup, canScroll && position < 0.99f, rightArrowBaseX + drift, step);
    }

    private static void UpdateArrow(RectTransform arrow, CanvasGroup group, bool show, float x, float step)
    {
        if (arrow == null || group == null) return;

        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, step);
        Vector2 pos = arrow.anchoredPosition;
        pos.x = x;
        arrow.anchoredPosition = pos;
    }

    /// <summary>
    /// A rect matching <paramref name="strip"/> with a chevron at each end, laid on top of it.
    /// Its own sibling rather than a child of the strip, so the strip's mask can't clip it and
    /// its layout group can't move it.
    /// </summary>
    private static RectTransform BuildArrowLane(RectTransform strip)
    {
        RectTransform lane = RunOverlayUI.CreateChild("ScrollHints", strip.parent);
        lane.SetSiblingIndex(strip.GetSiblingIndex() + 1);
        lane.anchorMin = strip.anchorMin;
        lane.anchorMax = strip.anchorMax;
        lane.pivot = strip.pivot;
        lane.anchoredPosition = strip.anchoredPosition;
        lane.sizeDelta = strip.sizeDelta;

        BuildArrow("LeftArrow", lane, left: true);
        BuildArrow("RightArrow", lane, left: false);
        return lane;
    }

    private static void BuildArrow(string name, RectTransform lane, bool left)
    {
        const float size = 72f;
        const float inset = 6f;

        RectTransform arrow = RunOverlayUI.CreateChild(name, lane);
        Vector2 anchor = new Vector2(left ? 0f : 1f, 0.5f);
        arrow.anchorMin = anchor;
        arrow.anchorMax = anchor;
        // Pivot on the inner side: the left arrow is mirrored with scale.x = -1, which flips
        // its pivot too, so both end up hugging their own edge.
        arrow.pivot = new Vector2(1f, 0.5f);
        arrow.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);
        arrow.anchoredPosition = new Vector2(left ? inset : -inset, 0f);
        arrow.sizeDelta = new Vector2(size, size);

        var image = arrow.gameObject.AddComponent<Image>();
        image.sprite = ChevronSprite();
        image.color = RunOverlayUI.Gold;
        image.raycastTarget = false;

        // Reads over a light card as well as over the dark panel.
        var shadow = arrow.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(3f, -3f);
    }

    private static Sprite chevronSprite;

    /// <summary>A soft-edged right-pointing chevron, drawn once in code (the UI kit has no arrow).</summary>
    private static Sprite ChevronSprite()
    {
        if (chevronSprite != null) return chevronSprite;

        const int size = 64;
        const float halfThickness = 5.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 top = new Vector2(22f, 54f);
        Vector2 tip = new Vector2(44f, 32f);
        Vector2 bottom = new Vector2(22f, 10f);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(DistanceToSegment(p, top, tip), DistanceToSegment(p, tip, bottom));
                float a = Mathf.Clamp01(halfThickness + 0.5f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        chevronSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        chevronSprite.name = "LoadoutChevron";
        return chevronSprite;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>
    /// Turns the card row into a horizontal scroll strip: a masked viewport takes the row's
    /// place, and the row becomes content that grows to fit every card at its preferred width.
    /// While the cards fit, the strip stays centred and doesn't move.
    /// </summary>
    private static ScrollRect MakeScrollable(RectTransform row)
    {
        RectTransform viewport = RunOverlayUI.CreateChild("CardsViewport", row.parent);
        viewport.SetSiblingIndex(row.GetSiblingIndex());
        viewport.anchorMin = row.anchorMin;
        viewport.anchorMax = row.anchorMax;
        viewport.pivot = row.pivot;
        viewport.anchoredPosition = row.anchoredPosition;
        viewport.sizeDelta = row.sizeDelta;
        viewport.gameObject.AddComponent<RectMask2D>();
        // Invisible hit area so drags in the gaps between cards still scroll.
        viewport.gameObject.AddComponent<Image>().color = Color.clear;

        row.SetParent(viewport, false);
        row.anchorMin = new Vector2(0.5f, 0f);
        row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = Vector2.zero;
        row.sizeDelta = Vector2.zero;

        // Cards keep their preferred width instead of squeezing toward minWidth.
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
        }
        var fitter = row.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = row;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.inertia = true;
        return scroll;
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

        // Stretches across the screen with a margin; cards that don't fit scroll sideways.
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
        row.childControlWidth = true;   // cards get their preferred width; Populate makes the row scroll
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
