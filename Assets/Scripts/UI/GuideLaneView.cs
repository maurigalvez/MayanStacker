using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the <see cref="GuideLane"/> — the one slim strip every
/// teaching line uses during a run: the tutorial beats, and the first-time lessons for the
/// Tremor meter, the Serpent's Edge, powers, temple rules and special blocks.
///
/// The strip lives low on the screen, under the top of the tower and above the power
/// button, so a lesson never covers the swinging stone, its fall, or where it lands.
///
/// Nothing here is required beyond a title label. The prefab (Resources/UI/GuideLane) is the
/// only source of the strip; nothing is built in code at runtime, so style it there.
///
/// Menu: TamalStacker ▸ UI ▸ Create Guide Lane Prefab generates a house-styled prefab.
/// </summary>
public class GuideLaneView : MonoBehaviour
{
    /// <summary>Bottom-centre anchored, so the lane keeps its gap to the tower on any aspect.</summary>
    public static readonly Vector2 LaneAnchor = new Vector2(0.5f, 0f);

    /// <summary>
    /// Centre of the strip, up from the bottom of the 1920-tall canvas. The tower's top sits
    /// about 400 up (camera height offset 7 at orthographic size 12) and the power button's
    /// top at 205, so a 150-tall strip centred here fits between the two.
    /// </summary>
    public static readonly Vector2 LanePosition = new Vector2(0f, 285f);

    /// <summary>
    /// Narrower than the 864 units a 20:9 phone is wide, so the ends of the strip stay
    /// clear of the Tremor meter on the right and the power button on the left.
    /// </summary>
    public static readonly Vector2 LaneSize = new Vector2(700f, 150f);

    public const float TitleFontSize = 38f;
    public const float BodyFontSize = 28f;

    // See-through slab: the lane should read as a caption over the scene, not a wall.
    public const float StripOpacity = 0.72f;

    [Header("Content")]
    [Tooltip("Headline — the name of the thing being taught, or the whole line when there is " +
             "no body. Set the intended font here.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Quieter line beneath it. Hidden when a line has no body; the strip's layout " +
             "group then centres the title on its own.")]
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Behaviour")]
    [Tooltip("Fades the whole strip, backing included.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Tint the headline with the colour the caller passes — a block's own colour, " +
             "a power's accent — so the words and the thing on screen are obviously about " +
             "each other. Turn off to keep the authored colour.")]
    [SerializeField] private bool tintTitleWithAccent = true;

    /// <summary>True when this view can actually show a line.</summary>
    public bool IsUsable => titleText != null;

    private void Awake() => SetAlpha(0f);

    /// <summary>Fills the strip in. An empty <paramref name="body"/> hides that line.</summary>
    public void Show(string title, string body, Color accent)
    {
        if (titleText != null)
        {
            titleText.text = title;
            if (tintTitleWithAccent) titleText.color = new Color(accent.r, accent.g, accent.b, titleText.color.a);
        }

        if (bodyText != null)
        {
            bool hasBody = !string.IsNullOrEmpty(body);
            bodyText.text = hasBody ? body : string.Empty;
            bodyText.gameObject.SetActive(hasBody);
        }
    }

    /// <summary>Drives the fade. Called every frame while fading, so it stays allocation-free.</summary>
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null) canvasGroup.alpha = alpha;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Builds the starting lane hierarchy on <paramref name="root"/>, which must already carry
    /// a Canvas. Editor-only: the prefab generator's starting point, never a runtime fallback.
    /// </summary>
    public static GuideLaneView BuildDefault(GameObject root, out Image strip)
    {
        var group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false; // a lesson must never swallow the tap that drops a stone

        RectTransform stripRect = RunOverlayUI.CreateChild("Strip", root.transform);
        RunOverlayUI.Place(stripRect, LaneAnchor, LanePosition, LaneSize);

        strip = stripRect.gameObject.AddComponent<Image>();
        Color stone = RunOverlayUI.Stone;
        strip.color = new Color(stone.r, stone.g, stone.b, StripOpacity);
        strip.raycastTarget = false;

        var layout = stripRect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.padding = new RectOffset(28, 28, 12, 12);
        layout.spacing = 2f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = RunOverlayUI.CreateLabel("Title", stripRect, "Tremor", TitleFontSize, RunOverlayUI.Gold);
        TextMeshProUGUI body = RunOverlayUI.CreateLabel("Body", stripRect,
            "Every sloppy stone shakes the tower.", BodyFontSize, RunOverlayUI.Parchment);

        var view = root.AddComponent<GuideLaneView>();
        view.titleText = title;
        view.bodyText = body;
        view.canvasGroup = group;
        return view;
    }
#endif

    /// <summary>Editor hook: lets the prefab generator restyle the labels it was given.</summary>
    public TextMeshProUGUI TitleLabel => titleText;

    /// <summary>Editor hook: lets the prefab generator restyle the labels it was given.</summary>
    public TextMeshProUGUI BodyLabel => bodyText;
}
