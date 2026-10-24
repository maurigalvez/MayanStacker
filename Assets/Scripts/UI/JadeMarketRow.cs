using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One line of the market: a swatch (or the theme's picture), the item's name, a hint for themes
/// that also unlock free, and a Buy / Wear button. A purchase lands here: the jade flies onto the
/// <see cref="PriceAnchor"/>, then the row flashes, pops and announces "Unlocked".
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
    [Tooltip("Second line under the name: how a theme can also be earned free. Optional.")]
    [SerializeField] private TextMeshProUGUI hintLabel;
    [Tooltip("Gold wash over the row that fades out when a purchase lands. Optional.")]
    [SerializeField] private Image purchaseFlash;
    [Tooltip("\"Unlocked!\" that pops over the row when a purchase lands. Optional.")]
    [SerializeField] private TextMeshProUGUI unlockedBanner;
    [Tooltip("Tint of the Buy button while the player can't afford it yet.")]
    [SerializeField] private Color unaffordableTint = new Color(0.62f, 0.62f, 0.62f, 1f);

    public bool IsComplete => swatch != null && nameLabel != null && actionButton != null && actionLabel != null;

    /// <summary>Where flying jade lands: the price button.</summary>
    public RectTransform PriceAnchor => actionButton != null ? (RectTransform)actionButton.transform : null;

    public CosmeticItem Item => item;

    private CosmeticItem item;
    private Coroutine celebrate;
    private Coroutine shake;

    private Vector3 rowScale = Vector3.one;
    private Vector3 swatchScale = Vector3.one;

    private void Awake()
    {
        rowScale = transform.localScale;
        if (swatch != null) swatchScale = swatch.transform.localScale;
        if (purchaseFlash != null) purchaseFlash.gameObject.SetActive(false);
        if (unlockedBanner != null) unlockedBanner.gameObject.SetActive(false);
    }

    public void Bind(CosmeticItem bound, System.Action<JadeMarketRow, CosmeticItem> onPressed)
    {
        item = bound;
        swatch.color = bound.color;
        if (swatch2 != null) swatch2.color = bound.kind == CosmeticKind.LandingBurst ? bound.color2 : bound.color;
        if (bound.icon != null)
        {
            swatch.sprite = bound.icon;
            swatch.color = Color.white;
            if (swatch2 != null) swatch2.gameObject.SetActive(false);

            // A picture (a sky theme) takes the whole frame, not the half a colour pair would use.
            var rect = swatch.rectTransform;
            if (rect.parent is RectTransform frame && rect.anchoredPosition.x >= 0f)
                rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, frame.rect.width - rect.anchoredPosition.x * 2f), rect.sizeDelta.y);
        }
        nameLabel.text = LocalizationManager.Get(bound.nameKey);
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => onPressed(this, item));
    }

    public void Refresh(int balance)
    {
        bool owned = CosmeticStore.IsOwned(item);
        bool worn = owned && CosmeticStore.Equipped(item.kind) == item;

        if (wornMarker != null) wornMarker.enabled = worn;

        // Stays tappable while unaffordable: the press explains what is missing instead of doing nothing.
        actionButton.interactable = !worn;
        var graphic = actionButton.targetGraphic;
        if (graphic != null) graphic.color = !owned && balance < item.price ? unaffordableTint : Color.white;

        if (worn) actionLabel.text = LocalizationManager.Get("market_equipped");
        else if (owned) actionLabel.text = LocalizationManager.Get("market_equip");
        else actionLabel.text = LocalizationManager.Get("market_buy", item.price);

        RefreshHint(owned);
    }

    private void RefreshHint(bool owned)
    {
        if (hintLabel == null) return;
        int temples = item.kind == CosmeticKind.Theme ? ThemeManager.TemplesToUnlock(item.theme) : 0;
        bool show = !owned && temples > 0;
        hintLabel.gameObject.SetActive(show);
        if (!show) return;

        int cleared = DependencyRegistry.TryFind<ThemeManager>(out var themes) ? themes.GetCompletedLevelCountPublic() : 0;
        hintLabel.text = LocalizationManager.Get("market_theme_hint", temples, Mathf.Min(cleared, temples));
    }

    /// <summary>The "not enough jade" no: the row gives a quick shake.</summary>
    public void PlayRefused()
    {
        if (shake != null) StopCoroutine(shake);
        shake = StartCoroutine(Shake());
    }

    /// <summary>The purchase has landed: flash, pop the swatch, announce it.</summary>
    public void PlayPurchased()
    {
        if (celebrate != null) StopCoroutine(celebrate);
        celebrate = StartCoroutine(Celebrate());
    }

    /// <summary>Wearing something already owned: a smaller pop on the swatch.</summary>
    public void PlayWorn()
    {
        StartCoroutine(Pop((RectTransform)swatch.transform, 0.22f, 0.3f));
    }

    private IEnumerator Shake()
    {
        // The button, not the row: the list's layout owns the row's position.
        var rect = PriceAnchor;
        Vector2 home = rect.anchoredPosition;
        float t = 0f;
        const float duration = 0.3f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            rect.anchoredPosition = home + new Vector2(Mathf.Sin(k * 40f) * 12f * (1f - k), 0f);
            yield return null;
        }
        rect.anchoredPosition = home;
    }

    private IEnumerator Celebrate()
    {
        var rect = (RectTransform)transform;
        StartCoroutine(Pop((RectTransform)swatch.transform, 0.45f, 0.5f));

        if (purchaseFlash != null) purchaseFlash.gameObject.SetActive(true);
        if (unlockedBanner != null)
        {
            unlockedBanner.text = LocalizationManager.Get("market_unlocked");
            unlockedBanner.gameObject.SetActive(true);
        }

        const float duration = 1.1f;
        float t = 0f;
        Vector3 baseScale = rect.localScale;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            float spring = 1f + 0.07f * Mathf.Exp(-7f * k) * Mathf.Cos(k * 22f);
            rect.localScale = baseScale * spring;

            if (purchaseFlash != null)
            {
                var c = purchaseFlash.color;
                c.a = 0.55f * (1f - k) * (1f - k);
                purchaseFlash.color = c;
            }
            if (unlockedBanner != null)
            {
                var bannerRect = unlockedBanner.rectTransform;
                float pop = k < 0.18f ? EaseOutBack(k / 0.18f) : 1f;
                bannerRect.localScale = Vector3.one * pop;
                var c = unlockedBanner.color;
                c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                unlockedBanner.color = c;
            }
            yield return null;
        }

        rect.localScale = baseScale;
        if (purchaseFlash != null) purchaseFlash.gameObject.SetActive(false);
        if (unlockedBanner != null)
        {
            var c = unlockedBanner.color;
            c.a = 1f;
            unlockedBanner.color = c;
            unlockedBanner.gameObject.SetActive(false);
        }
        celebrate = null;
    }

    private static IEnumerator Pop(RectTransform target, float amount, float duration)
    {
        Vector3 baseScale = target.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = 1f + amount * Mathf.Exp(-5f * k) * Mathf.Sin(k * Mathf.PI * 2.4f) ;
            target.localScale = baseScale * s;
            target.localRotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(k * 18f) * (1f - k));
            yield return null;
        }
        target.localScale = baseScale;
        target.localRotation = Quaternion.identity;
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float m = k - 1f;
        return 1f + c3 * m * m * m + c1 * m * m;
    }

    private void OnDisable()
    {
        // A row hidden mid-celebration must not stay scaled or flashing.
        if (purchaseFlash != null) purchaseFlash.gameObject.SetActive(false);
        if (unlockedBanner != null) unlockedBanner.gameObject.SetActive(false);
        celebrate = shake = null;
        transform.localScale = rowScale;
        if (swatch != null)
        {
            swatch.transform.localScale = swatchScale;
            swatch.transform.localRotation = Quaternion.identity;
        }
    }
}
