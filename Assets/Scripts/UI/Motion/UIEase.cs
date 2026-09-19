using UnityEngine;

/// <summary>Easing curves shared by the UI motion components. Input and output are 0..1.</summary>
public static class UIEase
{
    public static float OutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    public static float InQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    public static float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Rises from <paramref name="from"/> to <paramref name="peak"/> over the first
    /// <paramref name="peakAt"/> of the curve, then settles to <paramref name="to"/>.
    /// The overshoot is what makes a pop read as springy rather than a plain lerp.
    /// </summary>
    public static float Overshoot(float from, float peak, float to, float t, float peakAt = 0.6f)
    {
        t = Mathf.Clamp01(t);
        if (t < peakAt) return Mathf.LerpUnclamped(from, peak, OutCubic(t / peakAt));
        return Mathf.LerpUnclamped(peak, to, SmoothStep((t - peakAt) / (1f - peakAt)));
    }
}
