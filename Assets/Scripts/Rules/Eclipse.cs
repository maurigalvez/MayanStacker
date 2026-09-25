using UnityEngine;

/// <summary>
/// Temple rule <see cref="LevelRule.Eclipse"/>: the sun goes dark and only the top of the tower
/// stays lit. The player can see where the next stone must land, but not how the tower below
/// leans — they have to remember it.
///
/// Three world-space layers, all in front of the stones: solid dark below the lit band, a soft
/// ramp into the light, and a light dim over the sky and swing. The lit band follows the top
/// of the tower. Winning the temple brings the sun back, under the capstone.
/// </summary>
public class Eclipse : TempleRuleBehaviour
{
    private const int ShadeSortingOrder = 15; // above stones (4-5), water (11-12), leaves (12)
    private const int SunSortingOrder = 16;
    private const float FadeInSeconds = 1.4f;
    private const float FadeOutSeconds = 1.0f;
    private const float SunSize = 2.4f;
    private const float ViewMargin = 3f;

    private static readonly Color ShadeColor = new Color(0.02f, 0.02f, 0.05f, 1f);

    public override LevelRule Rule => LevelRule.Eclipse;

    private EclipseSettings settings;
    private SpriteRenderer dark;
    private SpriteRenderer ramp;
    private SpriteRenderer sky;
    private SpriteRenderer sun;
    private Camera cam;

    private float strength;       // 0..1 how far the eclipse has come in
    private float targetStrength;
    private float litEdgeY;       // smoothed bottom of the lit band

    protected override void Build()
    {
        cam = Camera.main;
        dark = Create("EclipseDark", WhiteSprite, ShadeSortingOrder);
        ramp = Create("EclipseRamp", RulePlaceholderArt.VerticalRamp, ShadeSortingOrder);
        sky = Create("EclipseSky", WhiteSprite, ShadeSortingOrder);
        sun = Create("EclipseSun", art.eclipseSun != null ? art.eclipseSun : RulePlaceholderArt.EclipsedSun, SunSortingOrder);
        SetActive(false);
    }

    private SpriteRenderer Create(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    protected override void OnRunStart(LevelData level)
    {
        settings = RuleActive ? level.eclipseSettings : null;
        strength = 0f;
        targetStrength = RuleActive ? 1f : 0f;
        litEdgeY = TowerTopY() - 3f;

        if (RuleActive)
        {
            SetActive(true);
            PlayOneShot(art.eclipseSting, 0.9f);
            SetLoop(art.eclipseLoop, 0.4f);
        }
        else
        {
            SetActive(false);
            FadeOutLoop();
        }
    }

    protected override void OnRunEnd(bool won)
    {
        if (!RuleActive) return;

        // The sun returns for the capstone; a loss stays in the dark.
        if (won) targetStrength = 0f;
        FadeOutLoop();
    }

    protected override void Update()
    {
        base.Update();
        if (!RuleActive || settings == null) return;

        float speed = targetStrength > strength ? 1f / FadeInSeconds : 1f / FadeOutSeconds;
        strength = Mathf.MoveTowards(strength, targetStrength, speed * Time.unscaledDeltaTime);

        if (cam == null) cam = Camera.main;
        float halfW = 8f, halfH = 12f;
        Vector3 c = Vector3.zero;
        if (cam != null && cam.orthographic)
        {
            halfH = cam.orthographicSize + ViewMargin;
            halfW = cam.orthographicSize * cam.aspect + ViewMargin;
            c = cam.transform.position;
        }

        float stone = StoneHeight();
        float targetEdge = TowerTopY() - settings.litStones * stone;
        litEdgeY = Mathf.Lerp(litEdgeY, targetEdge, 1f - Mathf.Exp(-4f * Time.deltaTime));

        float rampHeight = Mathf.Max(0.1f, settings.fadeStones * stone);
        float rampTop = litEdgeY;
        float rampBottom = litEdgeY - rampHeight;
        float viewBottom = c.y - halfH;
        float viewTop = c.y + halfH;
        float width = halfW * 2f;

        // Solid dark from the bottom of the view up to the ramp.
        float darkHeight = Mathf.Max(0.01f, rampBottom - viewBottom);
        Size(dark, c.x, viewBottom + darkHeight * 0.5f, width, darkHeight);
        Size(ramp, c.x, (rampTop + rampBottom) * 0.5f, width, rampHeight);
        Size(sky, c.x, c.y, width, viewTop - viewBottom);

        float shade = settings.darkness * strength;
        dark.color = WithAlpha(ShadeColor, shade);
        ramp.color = WithAlpha(ShadeColor, shade);
        sky.color = WithAlpha(ShadeColor, settings.skyDim * strength);

        // The eclipsed sun hangs in the upper corner of the view.
        Vector2 sunSize = sun.sprite != null ? (Vector2)sun.sprite.bounds.size : Vector2.one;
        float sunScale = SunSize / Mathf.Max(0.001f, Mathf.Max(sunSize.x, sunSize.y));
        sun.transform.localScale = new Vector3(sunScale, sunScale, 1f);
        sun.transform.position = new Vector3(c.x + (halfW - ViewMargin) * 0.55f, c.y + (halfH - ViewMargin) * 0.62f, 0f);
        sun.color = WithAlpha(Color.white, strength);

        if (strength <= 0f && targetStrength <= 0f) SetActive(false);
    }

    private static void Size(SpriteRenderer sr, float x, float y, float width, float height)
    {
        Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        sr.transform.position = new Vector3(x, y, 0f);
        sr.transform.localScale = new Vector3(width / Mathf.Max(0.001f, size.x), height / Mathf.Max(0.001f, size.y), 1f);
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    private void SetActive(bool on)
    {
        if (dark == null) return;
        dark.gameObject.SetActive(on);
        ramp.gameObject.SetActive(on);
        sky.gameObject.SetActive(on);
        sun.gameObject.SetActive(on);
    }
}
