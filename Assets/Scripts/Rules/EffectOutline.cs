using UnityEngine;

/// <summary>
/// A dark outline behind a light effect, so rain, spray and wind read against every sky —
/// the pale Day theme as well as Sunset and Night. The fill carries the effect on dark skies,
/// the outline carries it on light ones; nothing changes per theme.
///
/// For sprites the outline is a second renderer drawn one sorting order below the fill, using
/// the same sprite grown by a fixed world-space margin on every side (so thin streaks get the
/// same edge as wide leaves, and it works for any sprite dropped into TempleRuleArt).
/// </summary>
public static class EffectOutline
{
    /// <summary>Carved-stone brown, not black, so it sits in the temple palette.</summary>
    public static readonly Color Color = new Color(0.14f, 0.09f, 0.06f, 0.6f);

    /// <summary>World units the outline shows past the fill on each side.</summary>
    public const float Pad = 0.022f;

    /// <summary>Adds the outline renderer as a child of <paramref name="fill"/>, hidden until synced.</summary>
    public static SpriteRenderer AddBehind(SpriteRenderer fill)
    {
        var go = new GameObject("Outline");
        go.transform.SetParent(fill.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = fill.sprite;
        sr.color = new Color(Color.r, Color.g, Color.b, 0f);
        sr.enabled = false;
        Sync(fill, sr, 0f);
        return sr;
    }

    /// <summary>
    /// Matches the outline to its fill: sprite, sorting, enabled state and the margin at the
    /// fill's current scale. <paramref name="fade"/> (0..1) is how visible the fill is right now.
    /// Call after changing the fill's scale or sorting.
    /// </summary>
    public static void Sync(SpriteRenderer fill, SpriteRenderer outline, float fade)
    {
        if (outline == null) return;
        outline.sprite = fill.sprite;
        outline.sortingLayerID = fill.sortingLayerID;
        outline.sortingOrder = fill.sortingOrder - 1;
        outline.enabled = fill.enabled;
        outline.color = new Color(Color.r, Color.g, Color.b, Color.a * Mathf.Clamp01(fade));

        if (fill.sprite == null) return;
        Vector3 size = fill.sprite.bounds.size;
        Vector3 world = fill.transform.lossyScale;
        float w = Mathf.Abs(size.x * world.x);
        float h = Mathf.Abs(size.y * world.y);
        outline.transform.localScale = new Vector3(
            w > 0.0001f ? 1f + 2f * Pad / w : 1f,
            h > 0.0001f ? 1f + 2f * Pad / h : 1f,
            1f);
    }
}
