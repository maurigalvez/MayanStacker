using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes a Button or Toggle feel physically pressed: squashes while held, springs past full
/// size on release, then settles. On a Toggle the checkmark also pops in / shrinks out.
///
/// Added automatically to every Button and Toggle by <see cref="UIPressScaleAutoAttach"/>;
/// add <see cref="NoPressScale"/> to a control to exclude it.
///
/// The squash multiplies whatever scale the control already has, and adopts any scale
/// another script writes mid-press as its new base — so controls that pulse or resize
/// themselves (e.g. level buttons) keep doing so underneath the press.
/// </summary>
[DisallowMultipleComponent]
public class UIPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Selectable selectable;
    private Toggle toggle;

    private Vector3 baseScale;
    private Vector3 lastWrittenScale;
    private float factor = 1f;

    private bool held;
    private PointerEventData pressEvent;
    private int lastReleaseFrame = -1;

    private Coroutine pressRoutine;
    private Coroutine checkmarkRoutine;
    private Vector3 checkmarkBaseScale = Vector3.one;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        toggle = selectable as Toggle;

        if (toggle != null && toggle.graphic != null)
        {
            checkmarkBaseScale = toggle.graphic.rectTransform.localScale;
        }
    }

    private void OnEnable()
    {
        if (toggle != null) toggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnDisable()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(OnToggleValueChanged);

        // Coroutines die with the object; put the scale back so it doesn't reappear squashed.
        if (pressRoutine != null)
        {
            if (transform.localScale == lastWrittenScale) transform.localScale = baseScale;
            pressRoutine = null;
        }

        if (checkmarkRoutine != null)
        {
            if (toggle != null && toggle.graphic != null)
            {
                toggle.graphic.rectTransform.localScale = checkmarkBaseScale;
            }
            checkmarkRoutine = null;
        }

        factor = 1f;
        held = false;
        pressEvent = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (selectable != null && !selectable.IsInteractable()) return;

        held = true;
        pressEvent = eventData;

        if (pressRoutine == null)
        {
            baseScale = transform.localScale;
            lastWrittenScale = baseScale;
            factor = 1f;
        }
        else
        {
            // Re-pressed mid-spring: squash again from wherever it is.
            StopCoroutine(pressRoutine);
        }

        pressRoutine = StartCoroutine(PressRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        lastReleaseFrame = Time.frameCount;
        held = false;
        pressEvent = null;
    }

    private IEnumerator PressRoutine()
    {
        UIMotionSettings s = UIMotionSettings.Current;
        bool cancelled = false;
        float from = factor;
        float t = 0f;

        while (held)
        {
            // A press that turns into a scroll isn't a click; ease back without the bounce.
            // Checked by polling rather than IBeginDragHandler, which would steal the drag
            // from a parent ScrollRect.
            if (pressEvent != null && pressEvent.dragging)
            {
                held = false;
                pressEvent = null;
                cancelled = true;
                break;
            }

            t += Time.unscaledDeltaTime;
            float p = s.pressDuration > 0f ? t / s.pressDuration : 1f;
            ApplyScale(Mathf.Lerp(from, s.pressScale, UIEase.OutCubic(p)));
            yield return null;
        }

        if (cancelled)
        {
            yield return TweenFactor(factor, 1f, s.cancelReturnDuration, easeOut: false);
        }
        else
        {
            float up = s.releaseDuration * 0.4f;
            yield return TweenFactor(factor, s.releasePeakScale, up, easeOut: true);
            yield return TweenFactor(factor, 1f, s.releaseDuration - up, easeOut: false);
        }

        ApplyScale(1f);
        pressRoutine = null;
    }

    private IEnumerator TweenFactor(float from, float to, float duration, bool easeOut)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = t / duration;
            ApplyScale(Mathf.LerpUnclamped(from, to, easeOut ? UIEase.OutCubic(p) : UIEase.SmoothStep(p)));
            yield return null;
        }
        ApplyScale(to);
    }

    private void ApplyScale(float newFactor)
    {
        // Another script wrote the scale since we last did: treat its value as the new base.
        if (transform.localScale != lastWrittenScale) baseScale = transform.localScale;

        factor = newFactor;
        lastWrittenScale = baseScale * newFactor;
        transform.localScale = lastWrittenScale;
    }

    private void OnToggleValueChanged(bool isOn)
    {
        // Only the player's own tap pops the mark (the Toggle flips in OnPointerClick, the
        // same frame as pointer-up). A value restored from settings at load stays still.
        if (Time.frameCount != lastReleaseFrame || toggle.graphic == null) return;

        if (checkmarkRoutine != null) StopCoroutine(checkmarkRoutine);
        checkmarkRoutine = StartCoroutine(CheckmarkRoutine(isOn));
    }

    private IEnumerator CheckmarkRoutine(bool isOn)
    {
        UIMotionSettings s = UIMotionSettings.Current;
        RectTransform mark = toggle.graphic.rectTransform;
        float duration = Mathf.Max(0.01f, s.checkmarkPopDuration);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = t / duration;
            float f = isOn
                ? UIEase.Overshoot(0f, s.checkmarkPopPeak, 1f, p)
                : Mathf.Lerp(1f, 0f, UIEase.InQuad(p));
            mark.localScale = checkmarkBaseScale * f;
            yield return null;
        }

        // When turning off, the Toggle has already faded the mark out, so restoring its
        // scale here is invisible and leaves it ready for the next pop.
        mark.localScale = checkmarkBaseScale;
        checkmarkRoutine = null;
    }
}
