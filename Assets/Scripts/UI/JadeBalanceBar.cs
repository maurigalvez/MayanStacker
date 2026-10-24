using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The jade balance at the top of the main menu: the jade emblem on its pedestal and the count in a
/// gold frame. When jade is won in the menu (an opened chest), loose jade pieces burst out of where
/// it was won, arc into the emblem one after another, the emblem jolts as each lands and the count
/// ticks up with it — the last piece lands the exact balance. Each landing clinks a step higher and
/// the last one chimes, through <see cref="MainMenuSoundManager"/> so the SFX setting applies.
///
///   • <see cref="HoldCount"/> before granting, then <see cref="FlyFrom"/> with where it came from.
///   • <see cref="HoldCount"/> before paying, then <see cref="SpendTo"/> with what it bought: the pieces
///     leave the emblem one by one, the count ticks down with each, and they land on the purchase.
///   • Not enough jade: <see cref="Nudge"/> shakes the bar and floats what is missing.
///   • Any other balance change (a cloud merge) just rolls the number, no pieces.
///
/// Every gain or spend also floats a "+N" / "−N" off the count, so the change is always readable.
/// The pieces are clones of the authored templates under <see cref="flightLayer"/>, a nested canvas
/// sorted above the popups so they read over a closing panel. Tapping and hiding when jade is off
/// are the <see cref="JadeMarketMenuChip"/> on the same object.
///
/// More than one bar can exist (the menu's, and the Jade Market's own); the one enabled last, which
/// is the one on top, plays the animation and the others just catch up to the balance.
///
/// Built into the Main Menu Canvas prefab by TamalStacker ▸ UI ▸ Main Menu Rail ▸ Build Rail, and into
/// the Jade Market prefab by TamalStacker ▸ UI ▸ Create Jade Market Prefab.
/// </summary>
public class JadeBalanceBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countLabel;
    [SerializeField] private RectTransform emblem;
    [Tooltip("Nested canvas the flying pieces live on, sorted above the popups.")]
    [SerializeField] private RectTransform flightLayer;
    [Tooltip("Inactive piece images to clone, one picked at random per piece.")]
    [SerializeField] private Image[] shardTemplates;
    [Tooltip("Inactive label to clone for the floating \"+N\" / \"−N\" and the \"need more\" nudge.")]
    [SerializeField] private TextMeshProUGUI deltaTemplate;
    [SerializeField] private Color earnColor = new Color(0.45f, 0.95f, 0.70f, 1f);
    [SerializeField] private Color spendColor = new Color(1f, 0.72f, 0.38f, 1f);

    [Header("Pieces")]
    [SerializeField] private int minPieces = 8;
    [SerializeField] private int maxPieces = 12;
    [Tooltip("Jade per piece before the count is capped by Max Pieces.")]
    [SerializeField] private int jadePerPiece = 2;
    [SerializeField] private Vector2 pieceSize = new Vector2(78f, 108f);

    [Header("Timing (unscaled seconds)")]
    [SerializeField] private float burstDuration = 0.32f;
    [SerializeField] private float burstRadius = 210f;
    [SerializeField] private float hangTime = 0.12f;
    [SerializeField] private float flightDuration = 0.5f;
    [SerializeField] private float stagger = 0.07f;
    [SerializeField] private float rollDuration = 0.35f;

    [Header("Landing")]
    [SerializeField] private float emblemPunch = 0.16f;
    [SerializeField] private float finalPunch = 0.32f;
    [SerializeField] private float emblemWobble = 7f;
    [SerializeField] private float countPunch = 0.22f;

    [Header("Sound")]
    [Tooltip("A short jade clink, played as each piece lands, climbing in pitch.")]
    [SerializeField] private AudioClip pieceClink;
    [Tooltip("A richer chime for the last piece, the one that lands the exact balance.")]
    [SerializeField] private AudioClip finalChime;
    [SerializeField, Range(0f, 1f)] private float clinkVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float chimeVolume = 1f;
    [SerializeField] private float clinkPitchStart = 0.95f;
    [SerializeField] private float clinkPitchStep = 0.045f;
    [SerializeField] private float clinkPitchMax = 1.45f;

    private static readonly List<JadeBalanceBar> bars = new List<JadeBalanceBar>();
    private static bool held;

    private readonly List<RectTransform> livePieces = new List<RectTransform>();
    private int shown;
    private Coroutine flight;
    private Coroutine roll;
    private Coroutine emblemRoutine;
    private Coroutine countRoutine;
    private Vector3 emblemScale = Vector3.one;
    private Vector3 countScale = Vector3.one;

    /// <summary>The bar on top: the one enabled last.</summary>
    private static JadeBalanceBar Top
    {
        get
        {
            for (int i = bars.Count - 1; i >= 0; i--)
                if (bars[i] != null && bars[i].isActiveAndEnabled) return bars[i];
            return null;
        }
    }

    /// <summary>True when a bar is on screen to show the change.</summary>
    public static bool IsVisible => Top != null;

    /// <summary>Freezes the shown count across the next balance change, until <see cref="FlyFrom"/>, <see cref="SpendTo"/> or <see cref="ReleaseHold"/>.</summary>
    public static void HoldCount() => held = true;

    /// <summary>Ends a hold that nothing will animate (a purchase that was refused).</summary>
    public static void ReleaseHold()
    {
        held = false;
        foreach (var bar in bars)
            if (bar != null && bar.isActiveAndEnabled) bar.CatchUp();
    }

    /// <summary>
    /// Plays the pieces flying from <paramref name="source"/> into the emblem and rolls the count up to
    /// the balance. Releases the hold either way; without a visible bar it just ends the hold.
    /// </summary>
    public static void FlyFrom(RectTransform source, int amount)
    {
        held = false;
        var top = Top;
        if (top == null) return;
        foreach (var bar in bars)
            if (bar != null && bar != top && bar.isActiveAndEnabled) bar.CatchUp();
        top.StartFlight(source, amount);
    }

    /// <summary>
    /// Plays <paramref name="amount"/> jade leaving the emblem for <paramref name="target"/> (what was
    /// bought), the count ticking down as the pieces go. <paramref name="onArrive"/> runs when the last
    /// piece lands, or at once when no bar is on screen. Releases the hold either way.
    /// </summary>
    public static void SpendTo(RectTransform target, int amount, System.Action onArrive)
    {
        held = false;
        var top = Top;
        foreach (var bar in bars)
            if (bar != null && bar != top && bar.isActiveAndEnabled) bar.CatchUp();
        if (top == null)
        {
            onArrive?.Invoke();
            return;
        }
        top.StartSpend(target, amount, onArrive);
    }

    /// <summary>The player can't afford it: the bar shakes and floats <paramref name="message"/>.</summary>
    public static void Nudge(string message)
    {
        var top = Top;
        if (top == null) return;
        top.PlayNudge(message);
    }

    private void Awake()
    {
        if (emblem != null) emblemScale = emblem.localScale;
        if (countLabel != null) countScale = countLabel.rectTransform.localScale;
        foreach (var template in shardTemplates)
            if (template != null) template.gameObject.SetActive(false);
        if (deltaTemplate != null) deltaTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        bars.Remove(this);
        bars.Add(this);
        JadeWallet.OnBalanceChanged += OnBalance;
        SetShown(JadeWallet.Balance);
    }

    private void OnDisable()
    {
        JadeWallet.OnBalanceChanged -= OnBalance;
        bars.Remove(this);
        StopAllCoroutines();
        flight = roll = emblemRoutine = countRoutine = null;
        foreach (var piece in livePieces) if (piece != null) Destroy(piece.gameObject);
        livePieces.Clear();
        if (flightLayer != null)
        {
            // Floating "+N" / "−N" clones are active; the authored templates are not.
            foreach (Transform child in flightLayer)
                if (child.gameObject.activeSelf) Destroy(child.gameObject);
        }
        if (emblem != null)
        {
            emblem.localScale = emblemScale;
            emblem.localRotation = Quaternion.identity;
        }
        if (countLabel != null) countLabel.rectTransform.localScale = countScale;
    }

    private void OnBalance(int balance)
    {
        if (held || flight != null) return;
        if (roll != null) StopCoroutine(roll);
        roll = StartCoroutine(Roll(shown, balance));
    }

    /// <summary>Snaps the count to the balance. For a bar that sat under the one that animated.</summary>
    private void CatchUp()
    {
        if (flight != null) return;
        if (roll != null) { StopCoroutine(roll); roll = null; }
        SetShown(JadeWallet.Balance);
    }

    private void SetShown(int value)
    {
        shown = value;
        if (countLabel != null) countLabel.text = value.ToString();
    }

    private void StartFlight(RectTransform source, int amount)
    {
        if (roll != null) { StopCoroutine(roll); roll = null; }
        if (flight != null) StopCoroutine(flight);
        if (source == null || flightLayer == null || emblem == null || !HasTemplate())
        {
            roll = StartCoroutine(Roll(shown, JadeWallet.Balance));
            return;
        }
        flight = StartCoroutine(Flight(source, amount));
    }

    private bool HasTemplate()
    {
        foreach (var t in shardTemplates) if (t != null) return true;
        return false;
    }

    private IEnumerator Flight(RectTransform source, int amount)
    {
        int from = shown;
        int to = JadeWallet.Balance;
        int pieces = Mathf.Clamp(Mathf.CeilToInt(amount / (float)Mathf.Max(1, jadePerPiece)), minPieces, maxPieces);

        Vector2 start = ToFlightLocal(source);
        Vector2 target = ToFlightLocal(emblem);
        int landed = 0;
        ShowDelta("+" + amount, earnColor);

        for (int i = 0; i < pieces; i++)
        {
            var piece = Spawn(start);
            livePieces.Add(piece);
            float angle = (i / (float)pieces) * Mathf.PI * 2f + Random.Range(-0.35f, 0.35f);
            Vector2 burst = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * burstRadius * Random.Range(0.55f, 1f);
            float delay = hangTime + i * stagger;
            float spin = Random.Range(-260f, 260f);
            StartCoroutine(Piece(piece, start, burst, target, delay, spin, () =>
            {
                landed++;
                bool last = landed == pieces;
                SetShown(last ? to : from + Mathf.RoundToInt((to - from) * (landed / (float)pieces)));
                Land(last, landed);
            }));
        }

        while (landed < pieces) yield return null;
        livePieces.Clear();
        flight = null;
        if (shown != JadeWallet.Balance) SetShown(JadeWallet.Balance);
    }

    private void StartSpend(RectTransform target, int amount, System.Action onArrive)
    {
        if (roll != null) { StopCoroutine(roll); roll = null; }
        if (flight != null) StopCoroutine(flight);
        if (target == null || flightLayer == null || emblem == null || !HasTemplate())
        {
            roll = StartCoroutine(Roll(shown, JadeWallet.Balance));
            ShowDelta("−" + amount, spendColor);
            onArrive?.Invoke();
            return;
        }
        flight = StartCoroutine(SpendFlight(target, amount, onArrive));
    }

    /// <summary>
    /// The pieces peel off the emblem one after another, each one knocking the count down and
    /// clinking lower than the last, and arc onto the purchase. The last one lands with a chime.
    /// </summary>
    private IEnumerator SpendFlight(RectTransform target, int amount, System.Action onArrive)
    {
        int from = shown;
        int to = JadeWallet.Balance;
        int pieces = Mathf.Clamp(Mathf.CeilToInt(amount / (float)Mathf.Max(1, jadePerPiece * 4)), minPieces - 2, maxPieces);

        Vector2 start = ToFlightLocal(emblem);
        Vector2 end = ToFlightLocal(target);
        int arrived = 0;
        ShowDelta("−" + amount, spendColor);

        for (int i = 0; i < pieces; i++)
        {
            int index = i;
            var piece = Spawn(start);
            livePieces.Add(piece);
            float spin = Random.Range(-320f, 320f);
            StartCoroutine(SpendPiece(piece, start, end, index * stagger, spin,
                () =>
                {
                    // Leaving: the count steps down and the emblem gives a small recoil.
                    SetShown(index == pieces - 1 ? to : from + Mathf.RoundToInt((to - from) * ((index + 1) / (float)pieces)));
                    Leave(index, pieces);
                },
                () =>
                {
                    arrived++;
                    Arrive(target, arrived == pieces);
                    if (arrived == pieces) onArrive?.Invoke();
                }));
        }

        while (arrived < pieces) yield return null;
        livePieces.Clear();
        flight = null;
        if (shown != JadeWallet.Balance) SetShown(JadeWallet.Balance);
    }

    private IEnumerator SpendPiece(RectTransform piece, Vector2 start, Vector2 end, float delay, float spin,
        System.Action onLeave, System.Action onArrive)
    {
        piece.localScale = Vector3.zero;
        float wait = 0f;
        while (wait < delay) { wait += Time.unscaledDeltaTime; yield return null; }

        onLeave();

        Vector2 mid = (start + end) * 0.5f;
        Vector2 normal = Vector2.Perpendicular(end - start).normalized;
        if (normal.x < 0f) normal = -normal;
        Vector2 control = mid + normal * Vector2.Distance(start, end) * 0.3f;
        float baseAngle = piece.localEulerAngles.z;
        float t = 0f;
        while (t < flightDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / flightDuration);
            float eased = k * k * (3f - 2f * k);
            float u = 1f - eased;
            piece.anchoredPosition = u * u * start + 2f * u * eased * control + eased * eased * end;
            // Pops up from the emblem, then settles to a smaller stone as it nears the goods.
            float scale = k < 0.2f ? EaseOutBack(k / 0.2f) : Mathf.Lerp(1f, 0.55f, (k - 0.2f) / 0.8f);
            piece.localScale = Vector3.one * scale;
            piece.localRotation = Quaternion.Euler(0f, 0f, baseAngle + spin * k);
            yield return null;
        }

        Destroy(piece.gameObject);
        onArrive();
    }

    private void Leave(int index, int pieces)
    {
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound))
        {
            float pitch = Mathf.Max(0.7f, clinkPitchMax - clinkPitchStep * index) + Random.Range(-0.015f, 0.015f);
            sound.PlaySound(pieceClink, clinkVolume * 0.8f, pitch);
        }
        if (emblemRoutine != null) StopCoroutine(emblemRoutine);
        emblemRoutine = StartCoroutine(Punch(emblem, emblemScale, -emblemPunch * 0.8f, 0.2f, emblemWobble * 0.5f));
        if (countLabel != null)
        {
            if (countRoutine != null) StopCoroutine(countRoutine);
            countRoutine = StartCoroutine(Punch(countLabel.rectTransform, countScale, -countPunch * 0.8f, 0.18f, 0f));
        }
        HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
    }

    private void Arrive(RectTransform target, bool last)
    {
        if (last && DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound) && finalChime != null)
            sound.PlaySound(finalChime, chimeVolume);
        StartCoroutine(Punch(target, target.localScale, last ? 0.12f : 0.04f, last ? 0.3f : 0.14f, 0f));
        if (last) HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
    }

    private void PlayNudge(string message)
    {
        ShowDelta(message, spendColor);
        if (emblemRoutine != null) StopCoroutine(emblemRoutine);
        emblemRoutine = StartCoroutine(Shake(emblem, 16f, 0.34f));
        if (countLabel != null)
        {
            if (countRoutine != null) StopCoroutine(countRoutine);
            countRoutine = StartCoroutine(Shake(countLabel.rectTransform, 12f, 0.34f));
        }
    }

    /// <summary>A quick left-right shake that dies away, the "no" of a refused purchase.</summary>
    private static IEnumerator Shake(RectTransform target, float amount, float duration)
    {
        Vector2 home = target.anchoredPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            target.anchoredPosition = home + new Vector2(Mathf.Sin(k * 38f) * amount * (1f - k), 0f);
            yield return null;
        }
        target.anchoredPosition = home;
    }

    /// <summary>Floats <paramref name="text"/> up off the count and fades it. Nothing without an authored template.</summary>
    private void ShowDelta(string text, Color color)
    {
        if (deltaTemplate == null || flightLayer == null || countLabel == null) return;
        var label = Instantiate(deltaTemplate, flightLayer);
        label.gameObject.SetActive(true);
        label.text = text;
        label.color = color;
        label.raycastTarget = false;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = ToFlightLocal(countLabel.rectTransform) + new Vector2(0f, -86f);
        StartCoroutine(Float(label));
    }

    private IEnumerator Float(TextMeshProUGUI label)
    {
        const float duration = 1.15f;
        var rect = label.rectTransform;
        Vector2 start = rect.anchoredPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            rect.anchoredPosition = start + new Vector2(0f, 110f * EaseOutCubic(k));
            rect.localScale = Vector3.one * (k < 0.15f ? EaseOutBack(k / 0.15f) : 1f);
            var c = label.color;
            c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            label.color = c;
            yield return null;
        }
        Destroy(label.gameObject);
    }

    private RectTransform Spawn(Vector2 at)
    {
        Image template = null;
        while (template == null) template = shardTemplates[Random.Range(0, shardTemplates.Length)];
        var image = Instantiate(template, flightLayer);
        image.gameObject.SetActive(true);
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = pieceSize * Random.Range(0.8f, 1.15f);
        rect.anchoredPosition = at;
        rect.localScale = Vector3.zero;
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        return rect;
    }

    private IEnumerator Piece(RectTransform piece, Vector2 start, Vector2 burst, Vector2 target, float delay,
        float spin, System.Action onLanded)
    {
        float t = 0f;
        float startAngle = piece.localEulerAngles.z;
        // Out: pop from the source to a ring around it.
        while (t < burstDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / burstDuration);
            piece.anchoredPosition = Vector2.LerpUnclamped(start, burst, EaseOutCubic(k));
            piece.localScale = Vector3.one * EaseOutBack(k);
            piece.localRotation = Quaternion.Euler(0f, 0f, startAngle + spin * k * 0.4f);
            yield return null;
        }

        // Hang: a short float in place, so the burst reads before the pull.
        float wait = Mathf.Max(0f, delay - burstDuration);
        float hung = 0f;
        while (hung < wait)
        {
            hung += Time.unscaledDeltaTime;
            piece.anchoredPosition = burst + Vector2.up * Mathf.Sin(hung * 9f) * 4f;
            yield return null;
        }

        // In: an arc up and over into the emblem, accelerating, shrinking as it lands.
        Vector2 from = piece.anchoredPosition;
        Vector2 mid = (from + target) * 0.5f;
        Vector2 normal = Vector2.Perpendicular(target - from).normalized;
        if (normal.y < 0f) normal = -normal;
        Vector2 control = mid + normal * Vector2.Distance(from, target) * 0.25f;
        float baseAngle = piece.localEulerAngles.z;
        t = 0f;
        while (t < flightDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseInCubic(Mathf.Clamp01(t / flightDuration));
            float u = 1f - k;
            piece.anchoredPosition = u * u * from + 2f * u * k * control + k * k * target;
            piece.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, k);
            piece.localRotation = Quaternion.Euler(0f, 0f, baseAngle + spin * k);
            yield return null;
        }

        Destroy(piece.gameObject);
        onLanded();
    }

    private void Land(bool last, int landed)
    {
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound))
        {
            // Each clink a step higher, so the run of landings climbs into the chime.
            float pitch = Mathf.Min(clinkPitchMax, clinkPitchStart + clinkPitchStep * (landed - 1)) + Random.Range(-0.015f, 0.015f);
            if (!last || finalChime == null) sound.PlaySound(pieceClink, clinkVolume, pitch);
            if (last) sound.PlaySound(finalChime, chimeVolume);
        }

        if (emblemRoutine != null) StopCoroutine(emblemRoutine);
        emblemRoutine = StartCoroutine(Punch(emblem, emblemScale, last ? finalPunch : emblemPunch,
            last ? 0.42f : 0.22f, emblemWobble * (last ? 1.6f : 1f)));
        if (countLabel != null)
        {
            if (countRoutine != null) StopCoroutine(countRoutine);
            countRoutine = StartCoroutine(Punch(countLabel.rectTransform, countScale, last ? countPunch * 1.5f : countPunch,
                0.2f, 0f));
        }
        HapticFeedback.Trigger(last ? HapticFeedback.HapticType.Medium : HapticFeedback.HapticType.Light);
    }

    /// <summary>Jumps up by <paramref name="amount"/> and springs back, with a decaying wobble.</summary>
    private static IEnumerator Punch(RectTransform target, Vector3 baseScale, float amount, float duration, float wobble)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            // Fast rise (first 20%), then a damped spring back to rest.
            float rise = k < 0.2f ? EaseOutCubic(k / 0.2f) : 1f;
            float settle = k < 0.2f ? 1f : Mathf.Exp(-6f * (k - 0.2f)) * Mathf.Cos((k - 0.2f) * 14f);
            float s = 1f + amount * rise * settle;
            target.localScale = baseScale * s;
            target.localRotation = Quaternion.Euler(0f, 0f, wobble * Mathf.Sin(k * 22f) * (1f - k));
            yield return null;
        }
        target.localScale = baseScale;
        target.localRotation = Quaternion.identity;
    }

    private IEnumerator Roll(int from, int to)
    {
        float t = 0f;
        while (t < rollDuration)
        {
            t += Time.unscaledDeltaTime;
            SetShown(Mathf.RoundToInt(Mathf.Lerp(from, to, EaseOutCubic(Mathf.Clamp01(t / rollDuration)))));
            yield return null;
        }
        SetShown(to);
        roll = null;
    }

    private Vector2 ToFlightLocal(RectTransform rect)
    {
        var sourceCanvas = rect.GetComponentInParent<Canvas>();
        Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? sourceCanvas.rootCanvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, rect.TransformPoint(rect.rect.center));

        var layerCanvas = flightLayer.GetComponentInParent<Canvas>();
        Camera layerCamera = layerCanvas != null && layerCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? layerCanvas.rootCanvas.worldCamera : null;
        // The layer's pivot is its centre and the pieces anchor there, so this is their anchoredPosition.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(flightLayer, screen, layerCamera, out Vector2 local);
        return local;
    }

    private static float EaseOutCubic(float k) { k = 1f - k; return 1f - k * k * k; }
    private static float EaseInCubic(float k) => k * k * k;
    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float m = k - 1f;
        return 1f + c3 * m * m * m + c1 * m * m;
    }
}
