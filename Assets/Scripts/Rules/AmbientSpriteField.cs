using UnityEngine;

/// <summary>
/// A pool of sprites drifting across the camera's view: leaves for Jungle Wind, streaks for
/// Rain-slick Stones. Each sprite wraps around the view as it leaves it, so the field never
/// thins out or needs spawning, and nothing is allocated after <see cref="Build"/>.
///
/// Presentation only — cosmetic randomness here never touches the seeded run stream.
/// </summary>
public class AmbientSpriteField : MonoBehaviour
{
    private SpriteRenderer[] items;
    private Vector2[] offsets;   // per-item speed jitter (x) and spin (y)
    private float[] phases;
    private Camera cam;

    /// <summary>World velocity of the field. The rule sets this every frame.</summary>
    public Vector2 Velocity { get; set; }

    /// <summary>0..1 how much of the field shows. Items fade in and out as a whole.</summary>
    public float Density { get; set; }

    public bool Spin { get; set; }

    /// <summary>Stretch items along their velocity (rain streaks).</summary>
    public bool AlignToVelocity { get; set; }

    private Color tint = Color.white;
    private float alpha;

    public void Build(Sprite sprite, int count, float size, Color color, int sortingOrder)
    {
        cam = Camera.main;
        tint = color;
        items = new SpriteRenderer[count];
        offsets = new Vector2[count];
        phases = new float[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Item");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(color.r, color.g, color.b, 0f);

            float spriteSize = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
            float s = size / Mathf.Max(0.0001f, spriteSize) * Random.Range(0.7f, 1.3f);
            go.transform.localScale = new Vector3(s, s, 1f);

            items[i] = sr;
            offsets[i] = new Vector2(Random.Range(0.75f, 1.25f), Random.Range(-240f, 240f));
            phases[i] = Random.value * 10f;
        }

        Scatter();
        gameObject.SetActive(false);
    }

    /// <summary>Spreads every item across the current view.</summary>
    public void Scatter()
    {
        if (items == null) return;
        GetView(out Vector2 min, out Vector2 max);
        for (int i = 0; i < items.Length; i++)
        {
            items[i].transform.position = new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), 0f);
        }
    }

    private void GetView(out Vector2 min, out Vector2 max)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            min = new Vector2(-6f, -10f);
            max = new Vector2(6f, 10f);
            return;
        }

        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        // A margin so items enter from off-screen instead of popping in at the edge.
        min = new Vector2(c.x - w - 1f, c.y - h - 1f);
        max = new Vector2(c.x + w + 1f, c.y + h + 1f);
    }

    private void Update()
    {
        if (items == null) return;

        alpha = Mathf.MoveTowards(alpha, Mathf.Clamp01(Density), Time.deltaTime * 1.5f);
        if (alpha <= 0f && Density <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        GetView(out Vector2 min, out Vector2 max);
        Vector2 size = max - min;
        float dt = Time.deltaTime;

        for (int i = 0; i < items.Length; i++)
        {
            Transform t = items[i].transform;
            Vector2 v = Velocity * offsets[i].x;
            phases[i] += dt;

            // A gentle flutter across the flow, so leaves don't move like a conveyor.
            Vector2 flutter = Spin ? new Vector2(0f, Mathf.Sin(phases[i] * 2.3f) * 0.6f) : Vector2.zero;
            Vector3 p = t.position + (Vector3)((v + flutter) * dt);

            // Wrap around the view.
            if (p.x < min.x) p.x += size.x; else if (p.x > max.x) p.x -= size.x;
            if (p.y < min.y) p.y += size.y; else if (p.y > max.y) p.y -= size.y;
            t.position = p;

            if (Spin) t.Rotate(0f, 0f, offsets[i].y * dt);
            else if (AlignToVelocity && v.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + 90f;
                t.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            // Items fade in by index, so a low density shows fewer items rather than faint ones.
            float share = (i + 1f) / items.Length;
            float a = Mathf.Clamp01((alpha - share) * items.Length + 1f);
            items[i].color = new Color(tint.r, tint.g, tint.b, tint.a * a);
        }
    }

    /// <summary>Shows the field; items fade in toward <see cref="Density"/>.</summary>
    public void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            Scatter();
        }
    }
}
