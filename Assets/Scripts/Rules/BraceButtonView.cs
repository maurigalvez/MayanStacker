using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of Cabracán's BRACE button.
///
/// <see cref="Earthquake"/> builds a round medallion in code (the power meter's disc and
/// ring, bottom-left above the power button). Put this component on a prefab at
/// Resources/UI/BraceButton and the quake uses that instead: same rules and timing, but
/// placement, art, colours and text are authored like any other panel. Move the button by
/// moving this object's RectTransform in the prefab.
///
/// Only <see cref="holdTarget"/> is required. A prefab without one is discarded at runtime
/// and the code-built button is used, so a broken prefab can never leave a quake unbraceable.
///
/// Menu: TamalStacker ▸ Temples ▸ Create Brace Button Prefab generates one matching the code layout.
/// </summary>
public class BraceButtonView : MonoBehaviour
{
    /// <summary>The optional prefab, under Resources.</summary>
    public const string PrefabResourcePath = "UI/BraceButton";

    // Bottom-left, above the power medallion (125,125 / 160): off the tower, in thumb reach.
    public static readonly Vector2 DefaultAnchor = new Vector2(0f, 0f);
    public static readonly Vector2 DefaultPosition = new Vector2(170f, 380f); // nudged so the bigger disc + word clear the medallion and screen edge
    public static readonly Vector2 DefaultSize = new Vector2(263f, 263f); // 25% up from 210: a bigger, easier hold

    private static readonly Color RingTrackColor = new Color(0.33f, 0.27f, 0.2f, 1f);

    [Header("Parts")]
    [Tooltip("What the player presses and holds. Must be a raycast-target graphic, which is " +
             "also what stops the press from dropping a stone. Required.")]
    [SerializeField] private Graphic holdTarget;

    [Tooltip("The timer. A Filled image has its fill amount driven: it fills while the quake " +
             "winds up, then drains while the ground shakes. Optional.")]
    [SerializeField] private Image ring;

    [Tooltip("Optional. Left without a sprite, it shows the rule art's quake hold icon " +
             "(TempleRuleArt), and hides when there is none.")]
    [SerializeField] private Image icon;

    [Tooltip("Seconds left to hold, e.g. 1.8. Optional.")]
    [SerializeField] private TextMeshProUGUI secondsLabel;

    [Tooltip("When the icon ends up hidden (no sprite anywhere), move the seconds label to the " +
             "middle of its parent so the disc doesn't look half empty.")]
    [SerializeField] private bool centreSecondsWithoutIcon = true;

    [Tooltip("The button's word, and the quake's only instruction. Optional; its text is replaced " +
             "from localization at runtime (quake_brace_button, quake_braced while held). Pressing " +
             "it braces too.")]
    [SerializeField] private TextMeshProUGUI label;

    [Tooltip("Pulses while waiting to be held and squashes while held. Defaults to this object.")]
    [SerializeField] private RectTransform pulseTarget;

    [Header("Colours")]
    [Tooltip("Ring and word while not held.")]
    [SerializeField] private Color readyColor = RunOverlayUI.Gold;

    [Tooltip("Ring, word and icon while held.")]
    [SerializeField] private Color bracedColor = new Color(0.35f, 0.85f, 0.65f, 1f);

    [Tooltip("Icon tint while not held.")]
    [SerializeField] private Color iconColor = Color.white;

    [Header("Motion")]
    [SerializeField] private float warningPulseSpeed = 8f;
    [SerializeField] private float shakingPulseSpeed = 14f;
    [Range(0f, 0.3f)]
    [SerializeField] private float pulseScale = 0.06f;
    [Range(0.5f, 1f)]
    [SerializeField] private float heldScale = 0.92f;

    private HoldTracker hold;
    private HoldTracker labelHold;

    public bool IsUsable => holdTarget != null;

    /// <summary>The pressed object; the regression bot drives it with pointer events.</summary>
    public GameObject HoldTarget => holdTarget != null ? holdTarget.gameObject : null;

    public bool IsHeld => (hold != null && hold.IsHeld) || (labelHold != null && labelHold.IsHeld);

    /// <summary>Wires the hold tracking and fills in the default icon. Call once, after building.</summary>
    public void Init(Sprite ruleIcon)
    {
        if (pulseTarget == null) pulseTarget = (RectTransform)transform;

        if (holdTarget != null)
        {
            holdTarget.raycastTarget = true;
            hold = holdTarget.GetComponent<HoldTracker>();
            if (hold == null) hold = holdTarget.gameObject.AddComponent<HoldTracker>();
        }

        if (icon != null)
        {
            if (icon.sprite == null) icon.sprite = ruleIcon;
            icon.enabled = icon.sprite != null;
            icon.raycastTarget = false;
        }

        bool iconShown = icon != null && icon.enabled;
        if (!iconShown && centreSecondsWithoutIcon && secondsLabel != null)
        {
            secondsLabel.rectTransform.anchoredPosition = new Vector2(secondsLabel.rectTransform.anchoredPosition.x, 0f);
        }

        if (ring != null) ring.raycastTarget = false;
        if (secondsLabel != null) secondsLabel.raycastTarget = false;

        // The word is part of the button: pressing it braces too, and (being a raycast target)
        // it can never let the press through to drop a stone, even where it sits off the disc.
        if (label != null)
        {
            label.raycastTarget = true;
            labelHold = label.GetComponent<HoldTracker>();
            if (labelHold == null) labelHold = label.gameObject.AddComponent<HoldTracker>();
        }
    }

    public void ResetHold()
    {
        if (hold != null) hold.ResetHold();
        if (labelHold != null) labelHold.ResetHold();
    }

    /// <param name="ringFill">0..1 for the timer ring.</param>
    /// <param name="secondsLeft">Shown when not null.</param>
    /// <param name="shaking">Faster pulse while the ground shakes.</param>
    public void Render(float ringFill, float? secondsLeft, bool braced, bool shaking)
    {
        Color accent = braced ? bracedColor : readyColor;

        if (ring != null)
        {
            ring.fillAmount = ringFill;
            ring.color = accent;
        }

        if (secondsLabel != null) secondsLabel.text = secondsLeft.HasValue ? secondsLeft.Value.ToString("0.0") : string.Empty;

        if (label != null)
        {
            // The button carries the only instruction: "HOLD TO BRACE", then "BRACED!" while held.
            label.text = LocalizationManager.Get(braced ? "quake_braced" : "quake_brace_button");
            label.color = accent;
        }

        if (icon != null) icon.color = braced ? bracedColor : iconColor;

        if (pulseTarget != null)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (shaking ? shakingPulseSpeed : warningPulseSpeed));
            pulseTarget.localScale = Vector3.one * (braced ? heldScale : 1f + pulseScale * wave);
        }
    }

    /// <summary>
    /// Builds the default button under <paramref name="parent"/> (which must be on a canvas
    /// with a GraphicRaycaster). Used by the runtime fallback and by the editor prefab tool,
    /// so a fresh prefab starts identical to the code layout.
    /// </summary>
    public static BraceButtonView BuildDefault(Transform parent)
    {
        Sprite baseSprite = Resources.Load<Sprite>(PowerMeterView.BaseSpritePath);
        Sprite ringSprite = Resources.Load<Sprite>(PowerMeterView.RingSpritePath);

        RectTransform root = RunOverlayUI.CreateChild("BraceButton", parent);
        RunOverlayUI.Place(root, DefaultAnchor, DefaultPosition, DefaultSize);
        var view = root.gameObject.AddComponent<BraceButtonView>();

        var disc = root.gameObject.AddComponent<Image>();
        disc.sprite = baseSprite;
        disc.preserveAspect = true;
        disc.color = baseSprite != null ? Color.white : RunOverlayUI.Backdrop;
        disc.raycastTarget = true;
        view.holdTarget = disc;

        if (ringSprite != null)
        {
            RectTransform track = RunOverlayUI.CreateChild("RingTrack", root);
            RunOverlayUI.Stretch(track);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = ringSprite;
            trackImage.color = RingTrackColor;
            trackImage.raycastTarget = false;

            RectTransform ringRect = RunOverlayUI.CreateChild("Ring", root);
            RunOverlayUI.Stretch(ringRect);
            var ring = ringRect.gameObject.AddComponent<Image>();
            ring.sprite = ringSprite;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = false;
            ring.raycastTarget = false;
            view.ring = ring;
        }

        RectTransform iconRect = RunOverlayUI.CreateChild("Icon", root);
        RunOverlayUI.Place(iconRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 27.5f), new Vector2(120f, 120f));
        var icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        view.icon = icon;

        TextMeshProUGUI seconds = RunOverlayUI.CreateLabel("Seconds", root, "2.4", 55f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(seconds.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(225f, 75f));
        seconds.fontStyle = FontStyles.Bold;
        view.secondsLabel = seconds;

        // The word sits under the disc, so the button says what it does before it's pressed.
        TextMeshProUGUI word = RunOverlayUI.CreateLabel("Label", root, "HOLD TO BRACE", 40f, RunOverlayUI.Gold);
        RunOverlayUI.Place(word.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(330f, 56f));
        word.fontStyle = FontStyles.Bold;
        view.label = word;

        view.pulseTarget = root;
        return view;
    }

    /// <summary>Press-and-hold tracking. Counts pointers so multi-touch can't flicker it.</summary>
    public class HoldTracker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private readonly HashSet<int> pressed = new HashSet<int>();

        public bool IsHeld => pressed.Count > 0;

        public void OnPointerDown(PointerEventData eventData) => pressed.Add(eventData.pointerId);

        public void OnPointerUp(PointerEventData eventData) => pressed.Remove(eventData.pointerId);

        public void ResetHold() => pressed.Clear();

        private void OnDisable() => pressed.Clear();
    }
}
