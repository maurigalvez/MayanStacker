using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the tremor meter.
///
/// <see cref="TowerStability"/> builds its bar in code on its own overlay canvas. Drop this
/// component on a prefab at Resources/UI/TowerStabilityMeter and the meter uses that instead:
/// same rules and timing, but the frame, colours, font and placement are authored like any
/// other panel. The prefab's own placement wins over StabilitySettings' bar placement.
///
/// Only a fill RectTransform and its Image are required. A prefab missing either is discarded
/// at runtime and the code-built bar is used, so a broken prefab can never hide the meter.
///
/// Menu: TamalStacker ▸ UI ▸ Set Up Tremor Meter UI generates a prefab matching the code-built
/// layout, as a starting point to restyle.
/// </summary>
public class TowerStabilityView : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The track the fill grows inside. Pulses while the temple trembles. Optional - " +
             "without it nothing pulses.")]
    [SerializeField] private RectTransform bar;

    [Tooltip("Grows upward from the bottom of its parent as tremor rises. Its anchors and size " +
             "are driven at runtime - style it, don't lay it out.")]
    [SerializeField] private RectTransform fill;

    [Tooltip("Recoloured by tremor level.")]
    [SerializeField] private Image fillImage;

    [Tooltip("Optional caption. Text is replaced from localization at runtime.")]
    [SerializeField] private TextMeshProUGUI label;

    [Header("Fill")]
    [Tooltip("Gap between the fill and the edges of its parent track.")]
    [SerializeField] private float fillPadding = 6f;

    [Header("Colours")]
    [SerializeField] private Color stillColor = RunOverlayUI.Jade;
    [SerializeField] private Color strainColor = RunOverlayUI.Gold;
    [SerializeField] private Color dangerColor = RunOverlayUI.Clay;
    [SerializeField] private Color trembleFlashColor = RunOverlayUI.Parchment;

    [Tooltip("Tremor at which the fill has fully turned from still to strain colour.")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float strainPoint = 0.6f;

    [Header("Trembling")]
    [SerializeField] private float pulseSpeed = 14f;
    [Range(0f, 0.3f)]
    [SerializeField] private float pulseScale = 0.06f;
    [SerializeField] private bool flashWhileTrembling = true;

    [Header("Serpent's Edge cost preview")]
    [Tooltip("Ghost segment above the fill showing the tremor a Serpent's Edge drop would add. " +
             "Optional - created at runtime inside the fill's track when unassigned. Its " +
             "anchors and size are driven at runtime.")]
    [SerializeField] private RectTransform previewFill;

    [SerializeField] private Image previewImage;
    [SerializeField] private Color previewColor = new Color(0.79f, 0.64f, 0.29f, 0.6f);

    private float previewAmount;

    /// <summary>True when this prefab can actually show the meter.</summary>
    public bool IsUsable => fill != null && fillImage != null && fill.parent is RectTransform;

    /// <summary>Sets the caption. Ignores empty text so a missing loc key can't blank it.</summary>
    public void SetLabel(string text)
    {
        if (label != null && !string.IsNullOrEmpty(text)) label.text = text;
    }

    /// <summary>Tremor (0-1) to preview above the fill; 0 hides the preview.</summary>
    public void SetPreview(float amount)
    {
        previewAmount = Mathf.Clamp01(amount);
    }

    public void SetFill(float amount, bool trembling) => SetFill(amount, trembling, 0f);

    /// <summary>
    /// Draws the meter at <paramref name="amount"/> (0-1). <paramref name="highlight"/> (0-1)
    /// pulses the bar to point the player at it. Called every frame, so it stays
    /// allocation-free.
    /// </summary>
    public void SetFill(float amount, bool trembling, float highlight)
    {
        if (!IsUsable) return;

        amount = Mathf.Clamp01(amount);

        var track = (RectTransform)fill.parent;
        float innerHeight = Mathf.Max(0f, track.rect.height - 2f * fillPadding);

        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(1f, 0f);
        fill.pivot = new Vector2(0.5f, 0f);
        fill.anchoredPosition = new Vector2(0f, fillPadding);
        fill.sizeDelta = new Vector2(-2f * fillPadding, innerHeight * amount);

        Color color = amount < strainPoint
            ? Color.Lerp(stillColor, strainColor, amount / strainPoint)
            : Color.Lerp(strainColor, dangerColor, (amount - strainPoint) / (1f - strainPoint));

        float pulse = trembling ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed) : 0f;

        if (trembling && flashWhileTrembling)
        {
            color = Color.Lerp(dangerColor, trembleFlashColor, pulse * 0.5f);
        }

        if (highlight > 0f)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * 0.6f);
            pulse = Mathf.Max(pulse, Mathf.Clamp01(highlight) * wave * 2f);
        }

        if (bar != null)
        {
            bar.localScale = Vector3.one * (1f + pulseScale * pulse);
        }

        fillImage.color = color;

        DrawPreview(track, innerHeight, amount);
    }

    private void DrawPreview(RectTransform track, float innerHeight, float amount)
    {
        if (previewAmount <= 0f)
        {
            if (previewFill != null && previewFill.gameObject.activeSelf) previewFill.gameObject.SetActive(false);
            return;
        }

        if (!EnsurePreview(track)) return;
        if (!previewFill.gameObject.activeSelf) previewFill.gameObject.SetActive(true);

        float top = Mathf.Min(1f, amount + previewAmount);

        previewFill.anchorMin = new Vector2(0f, 0f);
        previewFill.anchorMax = new Vector2(1f, 0f);
        previewFill.pivot = new Vector2(0.5f, 0f);
        previewFill.anchoredPosition = new Vector2(0f, fillPadding + innerHeight * amount);
        previewFill.sizeDelta = new Vector2(-2f * fillPadding, innerHeight * (top - amount));

        Color c = previewColor;
        c.a *= 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f));
        previewImage.color = c;
    }

    /// <summary>
    /// Builds the preview segment when the prefab predates it, so an existing meter prefab
    /// still shows the Serpent's Edge cost without being regenerated.
    /// </summary>
    private bool EnsurePreview(RectTransform track)
    {
        if (previewFill == null)
        {
            var go = new GameObject("EdgePreview", typeof(RectTransform));
            go.transform.SetParent(track, false);
            previewFill = (RectTransform)go.transform;
            previewFill.SetSiblingIndex(fill.GetSiblingIndex() + 1);
        }

        if (previewImage == null)
        {
            previewImage = previewFill.GetComponent<Image>();
            if (previewImage == null) previewImage = previewFill.gameObject.AddComponent<Image>();
            previewImage.raycastTarget = false;
        }

        return true;
    }
}
