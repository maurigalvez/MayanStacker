using TMPro;
using UnityEngine;

/// <summary>
/// Inspector-authored face of the mid-run banner — the line that names the altitude band
/// you just reached, the objective a temple is asking for, or the block you have never seen
/// before ("Jade Sliver — narrow, but scores triple").
///
/// <see cref="RunBanner"/> builds that banner in code, which means it has no design-time
/// font and no framing: it renders in whatever TMP defaults to. Drop this component on a
/// prefab at Resources/UI/RunBanner and the banner uses that instead — same fades, same
/// timing, but the type, colour and framing are authored like every other panel.
///
/// Nothing here is required beyond a title label; a missing subtitle simply means
/// description lines are dropped, and if the prefab is absent the code-built banner is used.
///
/// Menu: TamalStacker ▸ UI ▸ Create Run Banner Prefab generates a prefab matching the
/// code-built layout, as a starting point to restyle.
/// </summary>
public class RunBannerView : MonoBehaviour
{
    [Header("Content")]
    [Tooltip("Headline — the name of the thing being announced. Set the intended font here.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Quieter line beneath it, saying what that name means. Hidden when the caller " +
             "has no description to show.")]
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Tooltip("Everything that moves together when the banner is placed. Leave empty to move " +
             "this object itself.")]
    [SerializeField] private RectTransform content;

    [Header("Behaviour")]
    [Tooltip("Fades the banner in and out. Without one, the labels' own alpha is faded, " +
             "which leaves any frame or backing image at full opacity.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Let callers place the banner vertically (block intros sit lower than altitude " +
             "banners, so they don't cover the block being described). Turn this off to pin " +
             "the banner exactly where it is authored.")]
    [SerializeField] private bool followCallerPlacement = true;

    [Tooltip("Tint the headline with the colour the caller passes — a block's own colour, " +
             "so the words and the thing on screen are obviously about each other. Turn off " +
             "to keep the authored colour.")]
    [SerializeField] private bool tintTitleWithAccent = true;

    /// <summary>True when this prefab can actually show a banner.</summary>
    public bool IsUsable => titleText != null;

    private RectTransform Body => content != null ? content : (RectTransform)transform;

    private void Awake() => SetAlpha(0f);

    /// <summary>Fills the banner in. An empty <paramref name="subtitle"/> hides that line.</summary>
    public void Show(string title, string subtitle, Color accent)
    {
        if (titleText != null)
        {
            titleText.text = title;
            if (tintTitleWithAccent) titleText.color = new Color(accent.r, accent.g, accent.b, titleText.color.a);
        }

        if (subtitleText != null)
        {
            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            subtitleText.text = hasSubtitle ? subtitle : string.Empty;
            subtitleText.gameObject.SetActive(hasSubtitle);
        }
    }

    /// <summary>Moves the banner to the height the caller asked for, if that is allowed.</summary>
    public void SetVerticalOffset(float y)
    {
        if (!followCallerPlacement) return;

        RectTransform body = Body;
        Vector2 position = body.anchoredPosition;
        position.y = y;
        body.anchoredPosition = position;
    }

    /// <summary>Drives the fade. Called every frame while fading, so it stays allocation-free.</summary>
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            return;
        }

        SetLabelAlpha(titleText, alpha);
        SetLabelAlpha(subtitleText, alpha);
    }

    private static void SetLabelAlpha(TextMeshProUGUI label, float alpha)
    {
        if (label == null) return;
        Color c = label.color;
        c.a = alpha;
        label.color = c;
    }
}
