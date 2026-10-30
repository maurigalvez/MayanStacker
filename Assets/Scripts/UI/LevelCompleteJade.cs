using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The jade row on the level-complete panel: emblem plus "+N jade", counted up from +0 after the
/// score, with the same clink, chime, punch and haptic as the Try Again card's jade row.
///
/// Shows everything the win paid (the run itself and stars won for the first time), read from
/// <see cref="JadeWallet.RunEarned"/>. Anything paid while the panel is up adds to the line live
/// and floats its own "+N", so no jade lands unseen. A win that paid nothing keeps the row hidden
/// (by alpha: the object stays active so it keeps listening).
///
/// Also owns the Path line above it ("Path +46 XP"), which fades in first, so a win closes the
/// Path's loop the way the Try Again card does. Missions and the Path pay right after the win,
/// before the count starts, so their jade is counted up with the rest.
///
/// Lives on the row. Built and wired by TamalStacker ▸ UI ▸ Set Up Level Complete Jade.
/// </summary>
public class LevelCompleteJade : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI amountText;
    [Tooltip("\"Path +46 XP\" line above the row. Hidden when the win earned no Path XP.")]
    [SerializeField] private TextMeshProUGUI pathText;
    [SerializeField] private Color pathColor = new Color(0.09f, 0.57f, 0.44f, 1f);       // jade, as on the Try Again card
    [SerializeField] private Color jadeColor = new Color(0.45f, 0.95f, 0.70f, 1f);      // bright jade
    [Tooltip("Clinks as the line counts up. Falls back to the score tick when empty.")]
    [SerializeField] private AudioClip jadeClink;
    [Tooltip("Plays when the line lands, and again when more jade arrives while the panel is up.")]
    [SerializeField] private AudioClip jadeChime;

    [Header("Timing (unscaled seconds)")]
    [SerializeField] private float fadeDuration = 0.22f;
    [SerializeField] private float countDuration = 0.7f;
    [SerializeField] private float punchScale = 1.25f;
    [SerializeField] private float punchDuration = 0.3f;
    [SerializeField] private float tickInterval = 0.06f;
    [SerializeField] private float startPitch = 1.0f;
    [SerializeField] private float endPitch = 1.6f;
    [Tooltip("How far a late \"+N\" floats up off the line, in the row's units.")]
    [SerializeField] private float floatRise = 160f;

    private GameSoundManager sound;
    private CanvasGroup group;
    private int shown;
    private bool counted;

    private RectTransform Row => (RectTransform)transform;

    private void OnEnable()
    {
        JadeWallet.OnBalanceChanged += OnJadeChanged;
    }

    private void OnDisable()
    {
        JadeWallet.OnBalanceChanged -= OnJadeChanged;
    }

    /// <summary>Hides the row before the panel pops in, so last win's amount never flashes.</summary>
    public void Clear()
    {
        StopAllCoroutines();
        counted = false;
        shown = 0;
        // Hidden by alpha, not deactivated, so it keeps listening for jade while the panel is up.
        SetVisible(0f);
        if (pathText != null) pathText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Reveals the row and counts the win's jade up. Yields at once when the win paid nothing;
    /// the row then stays hidden unless jade arrives while the panel is up.
    /// </summary>
    public IEnumerator Play(GameSoundManager soundManager)
    {
        sound = soundManager;
        counted = false;
        shown = 0;

        string pathLine = RunResult.PathLine();
        if (pathText != null && !string.IsNullOrEmpty(pathLine))
        {
            pathText.text = pathLine;
            pathText.color = pathColor;
            pathText.gameObject.SetActive(true);
            yield return FadeIn(pathText.rectTransform);
        }

        if (JadeWallet.RunEarned <= 0 || amountText == null)
        {
            counted = true;
            yield break;
        }

        SetVisible(0f);
        amountText.color = jadeColor;
        amountText.text = Format(0);

        float elapsed = 0f;
        float nextTick = 0f;
        while (elapsed < countDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / countDuration);
            SetVisible(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDuration)));

            int value = Mathf.FloorToInt(Mathf.Lerp(0f, JadeWallet.RunEarned, EaseOut(t)));
            if (value != shown)
            {
                shown = value;
                amountText.text = Format(shown);
            }

            if (sound != null && elapsed >= nextTick && shown < JadeWallet.RunEarned)
            {
                float pitch = Mathf.Lerp(startPitch, endPitch, t);
                if (jadeClink != null) sound.PlaySoundWithRandomPitch(jadeClink, pitch, pitch);
                else sound.PlayScoreCountSound(pitch);
                nextTick = elapsed + Mathf.Max(0.01f, tickInterval);
            }
            yield return null;
        }

        SetVisible(1f);
        counted = true;
        Land(0);
    }

    private void OnJadeChanged(int balance)
    {
        // Only once the count is done; a win that paid nothing reveals the row here.
        if (!counted || amountText == null || JadeWallet.RunEarned <= shown) return;

        int gained = JadeWallet.RunEarned - shown;
        amountText.color = jadeColor;
        SetVisible(1f);
        Land(gained);
    }

    /// <summary>Shows the win's total. <paramref name="gained"/> above 0 also floats "+N" off the row.</summary>
    private void Land(int gained)
    {
        shown = JadeWallet.RunEarned;
        amountText.text = Format(shown);

        if (sound != null && jadeChime != null) sound.PlaySound(jadeChime, 1f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
        if (isActiveAndEnabled)
        {
            StartCoroutine(Punch());
            if (gained > 0) StartCoroutine(FloatDelta("+" + gained));
        }
    }

    private IEnumerator Punch()
    {
        float elapsed = 0f;
        while (elapsed < punchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / punchDuration);
            // Up and back in one arc.
            Row.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        Row.localScale = Vector3.one;
    }

    /// <summary>Floats a copy of the amount, reading "+N", up off the row and fades it out.</summary>
    private IEnumerator FloatDelta(string text)
    {
        var label = Instantiate(amountText, Row.parent);
        label.text = text;
        label.color = jadeColor;
        var rect = label.rectTransform;
        rect.anchorMin = Row.anchorMin;
        rect.anchorMax = Row.anchorMax;
        rect.pivot = Row.pivot;
        rect.sizeDelta = new Vector2(Row.rect.width, amountText.rectTransform.rect.height);
        Vector2 start = Row.anchoredPosition;
        rect.localScale = Vector3.one;

        const float duration = 1.1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = start + new Vector2(0f, floatRise * EaseOut(t));
            var c = jadeColor;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            label.color = c;
            yield return null;
        }
        Destroy(label.gameObject);
    }

    private IEnumerator FadeIn(RectTransform line)
    {
        var lineGroup = line.GetComponent<CanvasGroup>();
        if (lineGroup == null) lineGroup = line.gameObject.AddComponent<CanvasGroup>();
        lineGroup.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOut(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDuration)));
            lineGroup.alpha = t;
            line.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, t);
            yield return null;
        }
        lineGroup.alpha = 1f;
        line.localScale = Vector3.one;
    }

    private void SetVisible(float amount)
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = amount;
        group.blocksRaycasts = false;
        Row.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, amount);
    }

    private static string Format(int value)
    {
        return LocalizationManager.Get("result_jade_line", value);
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}
