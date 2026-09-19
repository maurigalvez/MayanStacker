using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scale-in / scale-out for popups and callouts, replacing a bare SetActive.
///
///   • <see cref="Show"/> — activates the target and pops it in (scale up past 1, settle,
///     fade in). No-op if it is already visible, so repeated calls don't re-pop.
///   • <see cref="Hide"/> — shrinks and fades the target, THEN deactivates it and runs
///     <c>onHidden</c>. Pass the action a close button triggers (resume, restart, load a
///     scene) as <c>onHidden</c> so it happens after the popup has left.
///
/// Everything runs on unscaled time, because the pause menu and boon picker open with
/// Time.timeScale at 0. While closing, the target's CanvasGroup is non-interactable, so a
/// double tap can't fire its action twice; the backdrop still blocks taps to the game.
///
/// A root Canvas can't be scaled (Unity drives its transform), so for those the children
/// are moved under a stretched "PopupScaleRoot" child once, and that is what scales.
/// </summary>
public static class UIPopup
{
    private const string ScaleRootName = "PopupScaleRoot";

    private sealed class Tween
    {
        public Transform scaleTarget;
        public CanvasGroup group;
        public Vector3 baseScale;
        public bool hiding;
        public Action onHidden;
        public Coroutine routine;
    }

    // Only targets with a tween in flight live here; entries are removed when it finishes.
    private static readonly Dictionary<GameObject, Tween> tweens = new Dictionary<GameObject, Tween>();

    /// <summary>Length of the open animation, for callers that sequence after it.</summary>
    public static float PopInDuration => UIMotionSettings.Current.popInDuration;

    /// <summary>True while <paramref name="target"/> is playing its close animation.</summary>
    public static bool IsHiding(GameObject target)
    {
        return target != null && tweens.TryGetValue(target, out Tween tween) && tween.hiding;
    }

    /// <summary>
    /// Activates and pops in <paramref name="target"/> unless it is already showing.
    /// Interrupting a close reverses it (and drops that close's pending action).
    /// Returns true when an open animation started.
    /// </summary>
    public static bool Show(GameObject target)
    {
        if (target == null) return false;

        if (tweens.TryGetValue(target, out Tween tween))
        {
            if (!tween.hiding) return false;
        }
        else if (target.activeSelf)
        {
            return false;
        }

        return PopIn(target);
    }

    /// <summary>
    /// Pops in <paramref name="target"/> even if it is already active — for UI that is
    /// instantiated or built in code and is visible from the frame it's created.
    /// </summary>
    public static bool PopIn(GameObject target)
    {
        if (target == null) return false;

        bool reversing = tweens.TryGetValue(target, out Tween existing) && existing.hiding;
        Tween tween = Acquire(target);

        float startFactor = reversing ? CurrentFactor(tween) : UIMotionSettings.Current.popInStartScale;
        float startAlpha = reversing ? tween.group.alpha : 0f;

        tween.hiding = false;
        tween.onHidden = null;
        target.SetActive(true);

        UIMotionRunner runner = UIMotionRunner.Instance;
        if (runner == null || !target.activeInHierarchy)
        {
            // Nothing on screen to animate; just leave it in its resting state.
            Finish(target, tween);
            return false;
        }

        tween.group.interactable = true;
        tween.routine = runner.StartCoroutine(PopInRoutine(target, tween, startFactor, startAlpha));
        return true;
    }

    /// <summary>
    /// Shrinks and fades <paramref name="target"/>, then deactivates it and invokes
    /// <paramref name="onHidden"/>. If the target is null, already hidden, or not visible in
    /// the hierarchy, it is hidden at once and <paramref name="onHidden"/> runs immediately.
    /// If the target is destroyed mid-close, <paramref name="onHidden"/> is not invoked.
    /// </summary>
    public static void Hide(GameObject target, Action onHidden = null)
    {
        if (target == null)
        {
            onHidden?.Invoke();
            return;
        }

        bool hasTween = tweens.TryGetValue(target, out Tween tween);

        if (hasTween && tween.hiding)
        {
            // Already closing: whatever this caller wanted happens when that close ends.
            tween.onHidden += onHidden;
            return;
        }

        UIMotionRunner runner = UIMotionRunner.Instance;
        if (runner == null || !target.activeInHierarchy)
        {
            if (hasTween)
            {
                StopRoutine(tween);
                Finish(target, tween);
            }
            target.SetActive(false);
            onHidden?.Invoke();
            return;
        }

        float startFactor = hasTween ? CurrentFactor(tween) : 1f;
        float startAlpha = hasTween ? tween.group.alpha : 1f;

        tween = Acquire(target);
        tween.hiding = true;
        tween.onHidden = onHidden;
        tween.group.interactable = false;
        tween.routine = runner.StartCoroutine(PopOutRoutine(target, tween, startFactor, startAlpha));
    }

    private static IEnumerator PopInRoutine(GameObject target, Tween tween, float startFactor, float startAlpha)
    {
        UIMotionSettings s = UIMotionSettings.Current;
        float duration = Mathf.Max(0.01f, s.popInDuration);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            if (target == null || tween.scaleTarget == null)
            {
                tweens.Remove(target);
                yield break;
            }

            float p = t / duration;
            // Alpha is fully in well before the scale settles, so the overshoot reads as solid.
            Apply(tween,
                UIEase.Overshoot(startFactor, s.popInPeakScale, 1f, p),
                Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(p * 2.5f)));
            yield return null;
        }

        Finish(target, tween);
    }

    private static IEnumerator PopOutRoutine(GameObject target, Tween tween, float startFactor, float startAlpha)
    {
        UIMotionSettings s = UIMotionSettings.Current;
        float duration = Mathf.Max(0.01f, s.popOutDuration);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            if (target == null || tween.scaleTarget == null)
            {
                tweens.Remove(target);
                yield break;
            }

            float p = t / duration;
            Apply(tween,
                Mathf.Lerp(startFactor, s.popOutEndScale, UIEase.InQuad(p)),
                Mathf.Lerp(startAlpha, 0f, p));
            yield return null;
        }

        Action onHidden = tween.onHidden;

        // Restore the authored state before deactivating, so the next open starts clean.
        // Same frame as SetActive(false), so the restored frame is never rendered.
        Finish(target, tween);
        target.SetActive(false);

        onHidden?.Invoke();
    }

    /// <summary>Returns the in-flight tween for the target (stopped), or a fresh one.</summary>
    private static Tween Acquire(GameObject target)
    {
        if (tweens.TryGetValue(target, out Tween tween))
        {
            StopRoutine(tween);
            return tween;
        }

        Transform scaleTarget = ResolveScaleTarget(target);
        if (!target.TryGetComponent(out CanvasGroup group))
        {
            group = target.AddComponent<CanvasGroup>();
        }

        tween = new Tween
        {
            scaleTarget = scaleTarget,
            group = group,
            baseScale = scaleTarget.localScale
        };
        tweens[target] = tween;
        return tween;
    }

    private static void Apply(Tween tween, float factor, float alpha)
    {
        tween.scaleTarget.localScale = tween.baseScale * factor;
        tween.group.alpha = alpha;
    }

    private static void Finish(GameObject target, Tween tween)
    {
        if (tween.scaleTarget != null) tween.scaleTarget.localScale = tween.baseScale;
        if (tween.group != null)
        {
            tween.group.alpha = 1f;
            tween.group.interactable = true;
        }
        tween.routine = null;
        tween.hiding = false;
        tween.onHidden = null;
        tweens.Remove(target);
    }

    private static void StopRoutine(Tween tween)
    {
        if (tween.routine == null) return;
        UIMotionRunner runner = UIMotionRunner.Instance;
        if (runner != null) runner.StopCoroutine(tween.routine);
        tween.routine = null;
    }

    private static float CurrentFactor(Tween tween)
    {
        if (tween.scaleTarget == null || Mathf.Approximately(tween.baseScale.x, 0f)) return 1f;
        return tween.scaleTarget.localScale.x / tween.baseScale.x;
    }

    private static Transform ResolveScaleTarget(GameObject target)
    {
        if (!target.TryGetComponent(out Canvas canvas) || !IsRootCanvas(canvas))
        {
            return target.transform;
        }

        Transform root = target.transform;
        if (root.childCount == 1 && root.GetChild(0).name == ScaleRootName)
        {
            return root.GetChild(0);
        }

        var wrapperGo = new GameObject(ScaleRootName, typeof(RectTransform));
        wrapperGo.layer = target.layer;

        var wrapper = (RectTransform)wrapperGo.transform;
        wrapper.SetParent(root, false);
        wrapper.anchorMin = Vector2.zero;
        wrapper.anchorMax = Vector2.one;
        wrapper.offsetMin = Vector2.zero;
        wrapper.offsetMax = Vector2.zero;

        // The wrapper was appended last, so child 0 is never the wrapper while more than one
        // child remains. Moving in index order keeps the draw order intact, and the wrapper
        // fills the canvas exactly, so every child's anchors resolve to the same rect.
        while (root.childCount > 1)
        {
            root.GetChild(0).SetParent(wrapper, false);
        }

        return wrapper;
    }

    private static bool IsRootCanvas(Canvas canvas)
    {
        Transform parent = canvas.transform.parent;
        return parent == null || parent.GetComponentInParent<Canvas>(true) == null;
    }
}
