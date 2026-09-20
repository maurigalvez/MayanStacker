using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the Serpent's Edge markers: two golden strips on the top stone's
/// rim, under where a drop from either end of the swing lands, each with what a sweet-spot edge
/// drop there would score above it. The value stays small and quiet until the sweet spot opens,
/// then pops big (gold when it beats a safe Perfect) - big means "release now". Red = worse/miss.
/// Only the edge's own value is shown; a second "safe Perfect" number beside it read as clutter.
///
/// <see cref="RiskWindow"/> positions the strips every frame to follow the swing, so style
/// them (sprite, colour, label font) rather than laying them out. Drop this on a prefab at
/// Resources/UI/SerpentsEdge to replace the code-built markers; a prefab missing either zone
/// is discarded at runtime and the code-built markers are used.
///
/// Menu: TamalStacker ▸ UI ▸ Tremor Meter ▸ Create Serpent's Edge Prefab.
/// </summary>
public class RiskWindowView : MonoBehaviour
{
    /// <summary>How an edge drop on one side compares with a safe Perfect right now.</summary>
    public enum Worth
    {
        /// <summary>No projection yet - the plain multiplier look.</summary>
        Neutral,
        /// <summary>The edge scores more than a safe Perfect.</summary>
        Better,
        /// <summary>The edge scores less, breaks the combo, or misses the stack.</summary>
        Worse
    }

    [Header("Parts")]
    [Tooltip("Strip on the top stone's rim for a left-edge drop. Position and size are driven at runtime.")]
    [SerializeField] private RectTransform leftZone;

    [Tooltip("Strip on the top stone's rim for a right-edge drop. Position and size are driven at runtime.")]
    [SerializeField] private RectTransform rightZone;

    [SerializeField] private Image leftImage;
    [SerializeField] private Image rightImage;

    [Tooltip("Optional multiplier captions. Text is replaced from localization at runtime.")]
    [SerializeField] private TextMeshProUGUI leftLabel;
    [SerializeField] private TextMeshProUGUI rightLabel;

    [Header("Look")]
    [Tooltip("The side the stone isn't in: visible, but quiet.")]
    [SerializeField] private Color idleColor = new Color(0.79f, 0.64f, 0.29f, 0.3f);

    [Tooltip("The side the stone is in right now - releasing now is an edge drop.")]
    [SerializeField] private Color activeColor = new Color(0.95f, 0.78f, 0.35f, 0.95f);

    [SerializeField] private Color labelIdleColor = new Color(0.93f, 0.90f, 0.82f, 0.55f);
    [SerializeField] private Color labelActiveColor = new Color(1f, 0.96f, 0.85f, 1f);

    [Range(1f, 1.6f)]
    [SerializeField] private float activeScale = 1.2f;

    [Tooltip("Pulse speed used while the first-time intro is drawing attention to the markers.")]
    [SerializeField] private float emphasisPulseSpeed = 6f;

    [Header("With a sprite (pre-coloured art)")]
    [Tooltip("Tint for the idle side when the zone has a sprite. White keeps the art's own colours.")]
    [SerializeField] private Color artIdleColor = new Color(1f, 1f, 1f, 0.3f);

    [Tooltip("Tint for the live side when the zone has a sprite.")]
    [SerializeField] private Color artActiveColor = Color.white;

    [Tooltip("Extra scale the live strip breathes by (0.06 = +/-6%).")]
    [Range(0f, 0.2f)]
    [SerializeField] private float artActivePulse = 0.06f;

    [SerializeField] private float artActivePulseSpeed = 5f;

    [Header("Sweet spot flash")]
    [Tooltip("Strip tint while releasing now is a sweet-spot drop (lands as a Perfect). Multiplies " +
             "the art's own colours, so keep it at or near white; the plain bar uses it as its colour.")]
    [SerializeField] private Color sweetColor = new Color(1f, 0.97f, 0.8f, 1f);

    [Tooltip("Scale of the live strip during the sweet spot - a clear pop over the active size.")]
    [Range(1f, 2f)]
    [SerializeField] private float sweetScale = 1.45f;

    [Tooltip("Label colour during the sweet spot when the edge is worth no more than a safe Perfect. " +
             "A better edge uses the gold value colour instead.")]
    [SerializeField] private Color sweetLabelColor = Color.white;

    [Header("Value focus")]
    [Tooltip("Value size before the sweet spot. Kept small and quiet so a big number never reads " +
             "as \"release now\" while releasing would still land as a Good.")]
    [Range(0.3f, 1f)]
    [SerializeField] private float waitLabelScale = 0.6f;

    [Tooltip("Alpha multiplier on the value before the sweet spot.")]
    [Range(0f, 1f)]
    [SerializeField] private float waitLabelAlpha = 0.6f;

    [Tooltip("Value size during the sweet spot - the one moment the number is big.")]
    [Range(1f, 2.5f)]
    [SerializeField] private float sweetLabelScale = 1.6f;

    [Tooltip("Extra scale the value overshoots by as the sweet spot opens, settling over the pop duration.")]
    [Range(0f, 1f)]
    [SerializeField] private float sweetLabelPop = 0.35f;

    [SerializeField] private float sweetLabelPopDuration = 0.12f;

    [Header("Value preview")]
    [Tooltip("Label colour when the edge beats a safe Perfect.")]
    [SerializeField] private Color betterLabelColor = new Color(1f, 0.82f, 0.36f, 1f);

    [Tooltip("Label colour when the edge loses points, breaks the combo or misses.")]
    [SerializeField] private Color worseLabelColor = new Color(0.94f, 0.45f, 0.36f, 1f);

    [Tooltip("Strip tint multiplier when the edge is the worse choice (art keeps its carving, just reddened).")]
    [SerializeField] private Color worseZoneTint = new Color(1f, 0.55f, 0.5f, 1f);

    [Tooltip("Alpha of a projected value on the side the stone isn't in - both rims stay readable.")]
    [Range(0f, 1f)]
    [SerializeField] private float projectionIdleAlpha = 0.8f;

    private Worth leftWorth;
    private Worth rightWorth;
    private bool leftWasSweet;
    private bool rightWasSweet;
    private float leftSweetAt;
    private float rightSweetAt;

    /// <summary>True when this view can actually show both markers.</summary>
    public bool IsUsable => leftZone != null && rightZone != null;

    private void Awake()
    {
        // A projection can carry a second "breaks xN" line; grow it upward, away from the strip.
        AnchorLabelBottom(leftLabel);
        AnchorLabelBottom(rightLabel);
    }

    private static void AnchorLabelBottom(TextMeshProUGUI label)
    {
        if (label == null) return;
        label.verticalAlignment = VerticalAlignmentOptions.Bottom;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>
    /// Replaces one side's caption with its projected value and colours the side by how it
    /// compares with a safe Perfect. Call only when the text changes - it assigns a string.
    /// </summary>
    public void SetProjection(int side, string text, Worth worth)
    {
        TextMeshProUGUI label = side < 0 ? leftLabel : rightLabel;
        if (label != null && !string.IsNullOrEmpty(text)) label.text = text;
        if (side < 0) leftWorth = worth; else rightWorth = worth;
    }

    public void SetLabel(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (leftLabel != null) leftLabel.text = text;
        if (rightLabel != null) rightLabel.text = text;
    }

    /// <summary>
    /// Places one marker, in the local space of this view's canvas. <paramref name="side"/> is
    /// -1 for left, 1 for right. <paramref name="sweet"/> flashes the live marker: releasing now
    /// lands as a Perfect. <paramref name="emphasis"/> (0-1) pulses an idle marker so the intro
    /// can point at it. Called every frame, so it stays allocation-free.
    /// </summary>
    public void SetZone(int side, Vector2 center, float width, float height, bool active, bool sweet, float emphasis)
    {
        RectTransform zone = side < 0 ? leftZone : rightZone;
        if (zone == null) return;

        Image image = side < 0 ? leftImage : rightImage;
        TextMeshProUGUI label = side < 0 ? leftLabel : rightLabel;

        // Art keeps its own proportions and colours; the plain placeholder bar is tinted gold.
        Sprite sprite = image != null ? image.sprite : null;
        bool hasArt = sprite != null && sprite.rect.width > 0f;
        if (hasArt) height = width * sprite.rect.height / sprite.rect.width;

        zone.anchorMin = zone.anchorMax = new Vector2(0.5f, 0.5f);
        zone.pivot = new Vector2(0.5f, 0.5f);
        zone.anchoredPosition = center;
        zone.sizeDelta = new Vector2(width, height);

        float k = active ? 1f : 0f;
        if (!active && emphasis > 0f)
        {
            k = emphasis * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * emphasisPulseSpeed)) * 0.7f;
        }

        Worth worth = side < 0 ? leftWorth : rightWorth;
        Color zoneTint = worth == Worth.Worse ? worseZoneTint : Color.white;

        // The sweet spot is a hard flash rather than a blend: it lasts a fraction of a second
        // and is the moment to tap, so it must read instantly. No breathe, which would blur it.
        float grow = sweet ? sweetScale : Mathf.Lerp(1f, activeScale, k);
        float zoneScale = 1f;
        if (hasArt)
        {
            // Uniform scale so the carving doesn't stretch, plus a soft breathe while live.
            zoneScale = active && !sweet ? grow * (1f + artActivePulse * Mathf.Sin(Time.unscaledTime * artActivePulseSpeed)) : grow;
            zone.localScale = new Vector3(zoneScale, zoneScale, 1f);
            image.color = (sweet ? sweetColor : Color.Lerp(artIdleColor, artActiveColor, k)) * zoneTint;
        }
        else
        {
            zone.localScale = new Vector3(1f, grow, 1f);
            if (image != null) image.color = (sweet ? sweetColor : Color.Lerp(idleColor, activeColor, k)) * zoneTint;
        }

        // Remember when this side's sweet spot opened, for the value's pop-in.
        bool wasSweet = side < 0 ? leftWasSweet : rightWasSweet;
        if (sweet && !wasSweet)
        {
            if (side < 0) leftSweetAt = Time.unscaledTime; else rightSweetAt = Time.unscaledTime;
        }
        if (side < 0) leftWasSweet = sweet; else rightWasSweet = sweet;

        if (label != null)
        {
            // The value is only big and gold in the sweet spot - the moment to release. Before
            // that it stays small and neutral: a big gold number read as "release now" while a
            // release there still lands as a Good. A worse edge or miss stays red (a "don't"
            // cue), just small.
            float labelScale;
            if (sweet)
            {
                label.color = worth == Worth.Better ? betterLabelColor
                    : worth == Worth.Worse ? worseLabelColor
                    : sweetLabelColor;

                float since = Time.unscaledTime - (side < 0 ? leftSweetAt : rightSweetAt);
                float pop = sweetLabelPopDuration > 0f ? 1f - Mathf.Clamp01(since / sweetLabelPopDuration) : 0f;
                labelScale = sweetLabelScale + sweetLabelPop * pop;
            }
            else
            {
                Color c;
                if (worth == Worth.Worse)
                {
                    Color idle = worseLabelColor;
                    idle.a *= projectionIdleAlpha;
                    c = Color.Lerp(idle, worseLabelColor, k);
                }
                else
                {
                    c = Color.Lerp(labelIdleColor, labelActiveColor, k);
                }
                c.a *= waitLabelAlpha;
                label.color = c;
                labelScale = waitLabelScale;
            }
            // The label is a child of the zone: divide out the zone's scale so only labelScale applies.
            label.rectTransform.localScale = Vector3.one * (labelScale / zoneScale);
        }
    }

    /// <summary>
    /// Builds the default markers on <paramref name="host"/>: a non-interactive overlay canvas
    /// plus both zones, wired to a new view. Used at runtime when there is no prefab, and by
    /// the editor tool to generate one, so the two can never drift apart.
    /// </summary>
    public static RiskWindowView BuildDefault(GameObject host, int sortingOrder)
    {
        RunOverlayUI.CreateCanvas(host, sortingOrder, interactive: false);

        var view = host.AddComponent<RiskWindowView>();
        view.leftZone = BuildZone("LeftEdge", host.transform, out view.leftImage, out view.leftLabel);
        view.rightZone = BuildZone("RightEdge", host.transform, out view.rightImage, out view.rightLabel);

        // AddComponent ran Awake before the labels existed.
        AnchorLabelBottom(view.leftLabel);
        AnchorLabelBottom(view.rightLabel);
        return view;
    }

    private static RectTransform BuildZone(string name, Transform parent, out Image image, out TextMeshProUGUI label)
    {
        var zone = RunOverlayUI.CreateChild(name, parent);
        RunOverlayUI.Place(zone, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 22f));

        image = zone.gameObject.AddComponent<Image>();
        image.color = RunOverlayUI.Gold;
        image.raycastTarget = false;

        label = RunOverlayUI.CreateLabel("Multiplier", zone, "x3", 40f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(180f, 56f));
        label.textWrappingMode = TextWrappingModes.NoWrap;

        return zone;
    }
}
