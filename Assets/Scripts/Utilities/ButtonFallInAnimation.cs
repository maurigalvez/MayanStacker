using System.Collections;
using UnityEngine;

public class ButtonFallInAnimation : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform[] buttons;

    [Header("Logo")]
    [SerializeField] private RectTransform logo;

    [Header("Fall Settings")]
    [SerializeField] private Vector2 startOffset = new Vector2(0, 1500);
    [SerializeField] private float fallDuration = 0.6f;
    [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Stagger")]
    [SerializeField] private float staggerDelay = 0.08f;

    [Header("Logo Land Impact")]
    [SerializeField] private float impactScaleMultiplier = 1.15f;
    [SerializeField] private float impactDuration = 0.15f;

    [Header("Logo Hover Idle")]
    [SerializeField] private float hoverAmplitude = 15f;
    [SerializeField] private float hoverSpeed = 1.5f;

    [Header("Button Shake (after logo lands)")]
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeIntensity = 10f;
    [SerializeField] private float shakeSpeed = 40f;

    [Header("Options")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool useUnscaledTime = false;

    private Vector2[] initialPositions;
    private Vector2 logoInitialPosition;
    private Vector3 logoBaseScale;
    private Coroutine playCoroutine;
    private Coroutine hoverCoroutine;

    private void Awake()
    {
        if (buttons != null && buttons.Length > 0)
        {
            initialPositions = new Vector2[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                initialPositions[i] = buttons[i].anchoredPosition;
                buttons[i].anchoredPosition = initialPositions[i] + startOffset;
            }
        }

        if (logo != null)
        {
            logoInitialPosition = logo.anchoredPosition;
            logoBaseScale = logo.localScale;
            logo.anchoredPosition = logoInitialPosition + startOffset;
        }
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Play()
    {
        if (playCoroutine != null)
            StopCoroutine(playCoroutine);

        playCoroutine = StartCoroutine(PlaySequence());
    }

    public void Stop()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        if (hoverCoroutine != null)
        {
            StopCoroutine(hoverCoroutine);
            hoverCoroutine = null;
        }

        StopAllCoroutines();

        if (buttons != null && initialPositions != null)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                    buttons[i].anchoredPosition = initialPositions[i];
            }
        }

        if (logo != null)
            logo.anchoredPosition = logoInitialPosition;
    }

    private IEnumerator PlaySequence()
    {
        // Phase 1: Fall in buttons
        int remaining = 0;

        if (buttons != null && buttons.Length > 0)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                {
                    Debug.LogWarning($"ButtonFallInAnimation: Null entry at index {i}, skipping.");
                    continue;
                }

                remaining++;
                StartCoroutine(FallElement(buttons[i], initialPositions[i], () => remaining--));

                if (staggerDelay > 0 && i < buttons.Length - 1)
                {
                    if (useUnscaledTime)
                        yield return new WaitForSecondsRealtime(staggerDelay);
                    else
                        yield return new WaitForSeconds(staggerDelay);
                }
            }
        }

        // Phase 2: Logo falls in (after last button starts, with stagger delay)
        if (logo != null)
        {
            if (remaining > 0 && staggerDelay > 0)
            {
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(staggerDelay);
                else
                    yield return new WaitForSeconds(staggerDelay);
            }

            remaining++;
            StartCoroutine(FallElement(logo, logoInitialPosition, () => remaining--));

            // Start the scale impact so it peaks right when the logo lands
            float impactLeadTime = fallDuration - impactDuration * 0.5f;
            if (impactLeadTime > 0)
            {
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(impactLeadTime);
                else
                    yield return new WaitForSeconds(impactLeadTime);
            }

            StartCoroutine(LogoImpact());
        }

        while (remaining > 0)
            yield return null;

        // Phase 4: Button shake
        if (buttons != null && buttons.Length > 0)
            yield return ShakeButtons();

        // Phase 5: Logo hover idle (runs forever)
        if (logo != null)
            hoverCoroutine = StartCoroutine(HoverLogo());

        playCoroutine = null;
    }

    private IEnumerator FallElement(RectTransform element, Vector2 targetPosition, System.Action onComplete)
    {
        Vector2 from = targetPosition + startOffset;
        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += delta;

            float t = Mathf.Clamp01(elapsed / fallDuration);
            float curveT = fallCurve.Evaluate(t);
            element.anchoredPosition = Vector2.LerpUnclamped(from, targetPosition, curveT);

            yield return null;
        }

        element.anchoredPosition = targetPosition;
        onComplete?.Invoke();
    }

    private IEnumerator ShakeButtons()
    {
        Vector2[] shakeBasePositions = new Vector2[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                shakeBasePositions[i] = buttons[i].anchoredPosition;
        }

        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += delta;

            float decay = 1f - Mathf.Clamp01(elapsed / shakeDuration);
            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            float offsetX = Mathf.Sin(time * shakeSpeed) * shakeIntensity * decay;
            float offsetY = Mathf.Cos(time * shakeSpeed * 0.7f) * shakeIntensity * decay * 0.5f;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                    buttons[i].anchoredPosition = shakeBasePositions[i] + new Vector2(offsetX, offsetY);
            }

            yield return null;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].anchoredPosition = shakeBasePositions[i];
        }
    }

    private IEnumerator LogoImpact()
    {
        Vector3 expanded = new Vector3(
            logoBaseScale.x,
            logoBaseScale.y * (2f - impactScaleMultiplier),
            logoBaseScale.z);

        float half = impactDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += delta;
            float t = Mathf.Clamp01(elapsed / half);
            logo.localScale = Vector3.Lerp(logoBaseScale, expanded, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += delta;
            float t = Mathf.Clamp01(elapsed / half);
            logo.localScale = Vector3.Lerp(expanded, logoBaseScale, t);
            yield return null;
        }

        logo.localScale = logoBaseScale;
    }

    private IEnumerator HoverLogo()
    {
        float time = 0f;

        while (true)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            time += delta;

            float offsetY = Mathf.Sin(time * hoverSpeed) * hoverAmplitude;
            logo.anchoredPosition = logoInitialPosition + new Vector2(0, offsetY);

            yield return null;
        }
    }
}
