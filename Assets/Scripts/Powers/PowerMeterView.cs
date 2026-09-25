using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the power meter.
///
/// <see cref="PowerSystem"/> builds a round medallion in code on its own overlay canvas: an
/// obsidian disc, a ring that fills clockwise as Perfects land, and the power's icon in the
/// middle. It sits in the bottom-left corner on the same line as the Kukulkan medallion, so
/// the bottom of the screen reads as one control strip and nothing floats over the tower.
///
/// Put this component on a prefab at Resources/UI/PowerMeter and the meter uses that instead:
/// same rules and timing, but frame, colours, icon and placement are authored like any other
/// panel. The prefab's own placement wins over PowerSettings' button placement.
///
/// Only the button and a fill RectTransform are required. A prefab missing either is
/// discarded at runtime and the code-built button is used, so a broken prefab can never
/// hide a charge the player earned.
///
/// Menu: TamalStacker ▸ Powers ▸ Create Meter Prefab generates one matching the code layout.
/// </summary>
public class PowerMeterView : MonoBehaviour
{
    /// <summary>Default medallion art, under Resources.</summary>
    public const string BaseSpritePath = "UI/Powers/PowerMedallion_Base";
    public const string RingSpritePath = "UI/Powers/PowerMedallion_Ring";

    [Header("Parts")]
    [Tooltip("Fires the power. Must be on a raycast-target graphic so the tap never also drops a stone.")]
    [SerializeField] private Button button;

    [Tooltip("Shows the charge. A Filled image (e.g. Radial 360) has its fill amount driven; " +
             "any other image grows upward from the bottom of its parent, with its anchors and " +
             "size driven at runtime - style it, don't lay it out.")]
    [SerializeField] private RectTransform fill;

    [Tooltip("Recoloured as the meter charges. Optional.")]
    [SerializeField] private Image fillImage;

    [Tooltip("The power's icon. Optional; hidden when the power has no icon.")]
    [SerializeField] private Image icon;

    [Tooltip("The power's name. Optional; text is replaced from localization at runtime.")]
    [SerializeField] private TextMeshProUGUI label;

    [Tooltip("Scaled while ready. Defaults to this object.")]
    [SerializeField] private RectTransform pulseTarget;

    [Header("Fill (rising mode only)")]
    [SerializeField] private float fillPadding = 8f;

    [Header("Colours")]
    [SerializeField] private Color chargingColor = RunOverlayUI.Jade;
    [SerializeField] private Color readyFlashColor = RunOverlayUI.Parchment;

    [Tooltip("Icon tint while the meter is still charging, so a lit icon means 'tap me'.")]
    [SerializeField] private Color iconChargingColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Ready pulse")]
    [SerializeField] private float pulseSpeed = 6f;
    [Range(0f, 0.3f)]
    [SerializeField] private float pulseScale = 0.08f;

    [Tooltip("Extra pulse while the first-time intro is pointing at the button.")]
    [Range(0f, 0.4f)]
    [SerializeField] private float highlightScale = 0.16f;

    [Header("Active (Quetzal Feather timer)")]
    [Tooltip("Slow breathe while a lasting power is running - calmer than the ready pulse, so it " +
             "doesn't read as 'tap me'.")]
    [SerializeField] private float activeBreatheSpeed = 2.4f;
    [Range(0f, 0.2f)]
    [SerializeField] private float activeBreatheScale = 0.04f;

    private static readonly Color RingTrackColor = new Color(0.33f, 0.27f, 0.2f, 1f);

    private Color readyColor = RunOverlayUI.Gold;

    public bool IsUsable => button != null && fill != null;

    public Button Button => button;

    /// <summary>
    /// Builds the default meter on <paramref name="host"/>: its own interactive overlay
    /// canvas and a round medallion with a radial ring fill. Used by the runtime fallback and
    /// by the editor prefab tool, so a fresh prefab starts identical to the code layout.
    /// </summary>
    public static PowerMeterView BuildDefault(GameObject host, PowerSettings settings)
    {
        // Interactive: the button has to take the tap, and a raycaster under it is also what
        // tells InputManager not to drop a stone for the same press.
        RunOverlayUI.CreateCanvas(host, settings.canvasSortingOrder, interactive: true);

        Sprite baseSprite = Resources.Load<Sprite>(BaseSpritePath);
        Sprite ringSprite = Resources.Load<Sprite>(RingSpritePath);

        Button button = RunOverlayUI.CreateButton("PowerButton", host.transform, string.Empty,
            baseSprite != null ? Color.white : RunOverlayUI.Backdrop, out TextMeshProUGUI generatedLabel);
        Object.DestroyImmediate(generatedLabel.gameObject);

        var buttonRect = button.GetComponent<RectTransform>();
        RunOverlayUI.Place(buttonRect, settings.buttonAnchor, settings.buttonPosition, settings.buttonSize);

        var disc = (Image)button.targetGraphic;
        disc.sprite = baseSprite;
        disc.preserveAspect = true;

        // A disabled button keeps its colour: the ring already says whether it's ready.
        var colors = button.colors;
        colors.disabledColor = Color.white;
        button.colors = colors;

        RectTransform fill;
        Image fillImage;
        if (ringSprite != null)
        {
            // Dim track under the ring, so an empty meter still reads as a meter.
            RectTransform track = RunOverlayUI.CreateChild("RingTrack", buttonRect);
            RunOverlayUI.Stretch(track);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = ringSprite;
            trackImage.color = RingTrackColor;
            trackImage.raycastTarget = false;

            fill = RunOverlayUI.CreateChild("Fill", buttonRect);
            RunOverlayUI.Stretch(fill);
            fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = ringSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = true;
            fillImage.fillAmount = 0f;
        }
        else
        {
            // Art missing: the plain rising fill still shows the charge.
            fill = RunOverlayUI.CreateChild("Fill", buttonRect);
            fillImage = fill.gameObject.AddComponent<Image>();
        }
        fillImage.raycastTarget = false;

        RectTransform iconRect = RunOverlayUI.CreateChild("Icon", buttonRect);
        RunOverlayUI.Stretch(iconRect);
        iconRect.offsetMin = new Vector2(34f, 32f);
        iconRect.offsetMax = new Vector2(-34f, -36f);
        var icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // Only shown when the power has no icon.
        var label = RunOverlayUI.CreateLabel("Label", buttonRect, string.Empty, 24f, RunOverlayUI.Parchment);
        RunOverlayUI.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(26f, 26f);
        label.rectTransform.offsetMax = new Vector2(-26f, -26f);

        var view = buttonRect.gameObject.AddComponent<PowerMeterView>();
        view.Bind(button, fill, fillImage, icon, label);
        return view;
    }

    /// <summary>Wires the parts built in code.</summary>
    public void Bind(Button button, RectTransform fill, Image fillImage, Image icon, TextMeshProUGUI label)
    {
        this.button = button;
        this.fill = fill;
        this.fillImage = fillImage;
        this.icon = icon;
        this.label = label;
    }

    public void SetContent(string powerName, Sprite iconSprite, Color accent)
    {
        readyColor = accent;

        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.gameObject.SetActive(iconSprite != null);
        }

        // With an icon the name is redundant on the button; without one it is the button.
        if (label != null)
        {
            label.text = powerName;
            label.gameObject.SetActive(iconSprite == null || icon == null);
        }
    }

    /// <param name="charge">0..1 progress toward the next charge (1 = ready).</param>
    /// <param name="ready">A charge is stored.</param>
    /// <param name="armed">Ready and past the arm delay, so tappable.</param>
    /// <param name="highlight">0..1 extra "look here" pulse.</param>
    public void SetState(float charge, bool ready, bool armed, float highlight)
    {
        if (button != null) button.interactable = armed;

        ApplyFill(charge);

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);

        if (fillImage != null)
        {
            fillImage.color = ready
                ? Color.Lerp(readyColor, readyFlashColor, armed ? wave * 0.35f : 0f)
                : chargingColor;
        }

        if (icon != null) icon.color = ready ? Color.white : iconChargingColor;

        RectTransform target = pulseTarget != null ? pulseTarget : (RectTransform)transform;
        float scale = 1f;
        if (armed) scale += pulseScale * wave;
        scale += highlightScale * Mathf.Clamp01(highlight) * wave;
        target.localScale = Vector3.one * scale;
    }

    /// <summary>
    /// A lasting power is running: the ring becomes its timer, in the power's colour, draining
    /// as it runs out. The button is locked and the icon stays lit.
    /// </summary>
    /// <param name="remaining">0..1 of the power's duration left.</param>
    public void SetActiveState(float remaining)
    {
        if (button != null) button.interactable = false;

        ApplyFill(remaining);

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * activeBreatheSpeed);

        if (fillImage != null) fillImage.color = Color.Lerp(readyColor, readyFlashColor, wave * 0.2f);
        if (icon != null) icon.color = Color.white;

        RectTransform target = pulseTarget != null ? pulseTarget : (RectTransform)transform;
        target.localScale = Vector3.one * (1f + activeBreatheScale * wave);
    }

    private void ApplyFill(float amount)
    {
        if (fillImage != null && fillImage.type == Image.Type.Filled)
        {
            fillImage.fillAmount = Mathf.Clamp01(amount);
        }
        else if (fill != null)
        {
            var parent = fill.parent as RectTransform;
            float innerHeight = parent != null ? Mathf.Max(0f, parent.rect.height - 2f * fillPadding) : 0f;

            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 0f);
            fill.pivot = new Vector2(0.5f, 0f);
            fill.anchoredPosition = new Vector2(0f, fillPadding);
            fill.sizeDelta = new Vector2(-2f * fillPadding, innerHeight * Mathf.Clamp01(amount));
        }
    }
}
