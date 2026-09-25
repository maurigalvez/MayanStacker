using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temple rule <see cref="LevelRule.RisingCenote"/>: sacred-well water climbs up the tower at a
/// steady pace. If it reaches the top stone, the run ends ("cenote"). A visible timer that needs
/// no number: the player builds to stay above it.
///
/// The water never lags more than <see cref="RisingCenoteSettings.maxLagStones"/> below the top,
/// so a fast builder still sees it on screen instead of leaving it behind. It pauses with the
/// game (scaled time) and stops the moment the temple is won.
///
/// Drawn in front of the stones, translucent, so the submerged part of the tower reads clearly
/// in a clip.
/// </summary>
public class RisingCenote : TempleRuleBehaviour
{
    private const int WaterSortingOrder = 11;     // in front of the stones
    private const float BodyAlpha = 0.5f;
    private const float FloodMarginStones = 0.3f; // flooded once the water is this close to the top
    private const float SurfaceHeightWorld = 0.35f;
    private const float ViewMargin = 3f;

    private static readonly Color WaterColor = new Color(0.12f, 0.55f, 0.55f, 1f);
    private static readonly Color WarningColor = new Color(0.75f, 0.32f, 0.2f, 1f);

    public override LevelRule Rule => LevelRule.RisingCenote;

    private RisingCenoteSettings settings;
    private SpriteRenderer body;
    private SpriteRenderer surface;
    private Camera cam;

    private bool rising;
    private float startAt;
    private float waterY;
    private float visibleAlpha;
    private bool warned;

    protected override void Build()
    {
        cam = Camera.main;

        body = CreateRenderer("CenoteWater", art.cenoteWater != null ? art.cenoteWater : WhiteSprite, WaterSortingOrder);
        if (art.cenoteWater != null) body.drawMode = SpriteDrawMode.Tiled;

        surface = CreateRenderer("CenoteSurface", art.cenoteSurface != null ? art.cenoteSurface : WhiteSprite, WaterSortingOrder + 1);
        if (art.cenoteSurface != null) surface.drawMode = SpriteDrawMode.Tiled;

        SetVisible(0f);
    }

    private SpriteRenderer CreateRenderer(string name, Sprite sprite, int order)
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
        settings = RuleActive ? level.cenoteSettings : null;
        rising = false;
        warned = false;
        waterY = GroundY() - 0.5f;
        if (!RuleActive)
        {
            visibleAlpha = 0f;
            SetVisible(0f);
            FadeOutLoop();
        }
    }

    protected override void OnRunEnd(bool won)
    {
        rising = false;
        FadeOutLoop();
    }

    protected override void OnStoneLanded(StackableObject stone)
    {
        if (settings == null || rising) return;

        // The clock starts once the foundation is down.
        rising = true;
        startAt = Time.time + settings.startDelaySeconds;
        SetLoop(art.cenoteLoop, 0.35f);
    }

    protected override void Update()
    {
        base.Update();
        if (!RuleActive || settings == null) return;

        float stone = StoneHeight();
        float top = TowerTopY();

        if (rising && RunLive && Time.time >= startAt)
        {
            waterY += settings.stonesPerSecond * stone * Time.deltaTime;

            // Never too far behind, so the pressure is always on screen.
            float floor = top - settings.maxLagStones * stone;
            if (waterY < floor) waterY = Mathf.Lerp(waterY, floor, 1f - Mathf.Exp(-2f * Time.deltaTime));

            float gap = (top - waterY) / stone;

            if (gap <= settings.warningStones && !warned)
            {
                warned = true;
                PlayOneShot(art.cenoteWarningSound, 1f);
                RunBanner.Show(LocalizationManager.Get("cenote_warning"), WarningColor, 1.1f, -180f);
                HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
            }
            else if (gap > settings.warningStones + 1f)
            {
                warned = false; // climbed clear: the next approach warns again
            }

            SetLoop(art.cenoteLoop, Mathf.Lerp(0.8f, 0.3f, Mathf.Clamp01(gap / settings.maxLagStones)));

            if (gap <= FloodMarginStones) Flood();
        }

        // Stays up after a flood or a win: the drowned (or saved) tower is the shot.
        visibleAlpha = Mathf.MoveTowards(visibleAlpha, 1f, Time.deltaTime * 2f);
        Place(top, stone);
    }

    private void Flood()
    {
        rising = false;
        PlayOneShot(art.cenoteFloodSound, 1f);
        if (cameraController != null) cameraController.Shake(0.3f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Heavy);

        GameAnalytics.Track("cenote_flooded", new Dictionary<string, object>
        {
            { "level", Level != null ? Level.levelNumber : 0 },
            { "height", stackManager != null ? stackManager.GetStackCount() : 0 }
        });

        if (gameManager != null) gameManager.GameOver("cenote");
    }

    private void Place(float top, float stone)
    {
        if (cam == null) cam = Camera.main;
        float halfWidth = 8f;
        float bottom = waterY - 20f;
        float centreX = 0f;
        if (cam != null && cam.orthographic)
        {
            halfWidth = cam.orthographicSize * cam.aspect + ViewMargin;
            bottom = Mathf.Min(waterY, cam.transform.position.y - cam.orthographicSize) - ViewMargin;
            centreX = cam.transform.position.x;
        }

        float height = Mathf.Max(0.01f, waterY - bottom);
        float width = halfWidth * 2f;

        // Warning colour as the water closes on the top stone.
        float gap = stone > 0f ? (top - waterY) / stone : 99f;
        float danger = settings != null ? Mathf.Clamp01(1f - (gap - FloodMarginStones) / Mathf.Max(0.1f, settings.warningStones)) : 0f;
        float pulse = danger > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) : 0f;
        Color c = Color.Lerp(WaterColor, WarningColor, danger * (0.5f + 0.5f * pulse));

        SizeRenderer(body, centreX, bottom + height * 0.5f, width, height);
        SizeRenderer(surface, centreX, waterY + Mathf.Sin(Time.time * 2.2f) * 0.04f, width, SurfaceHeightWorld);

        c.a = BodyAlpha * visibleAlpha;
        body.color = c;
        Color s = Color.Lerp(Color.white, c, 0.35f);
        s.a = 0.8f * visibleAlpha;
        surface.color = s;

        bool show = visibleAlpha > 0f;
        if (body.gameObject.activeSelf != show)
        {
            body.gameObject.SetActive(show);
            surface.gameObject.SetActive(show);
        }
    }

    private static void SizeRenderer(SpriteRenderer sr, float x, float y, float width, float height)
    {
        sr.transform.position = new Vector3(x, y, 0f);
        if (sr.drawMode == SpriteDrawMode.Tiled)
        {
            sr.transform.localScale = Vector3.one;
            sr.size = new Vector2(width, height);
        }
        else
        {
            Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            sr.transform.localScale = new Vector3(width / Mathf.Max(0.001f, size.x), height / Mathf.Max(0.001f, size.y), 1f);
        }
    }

    private void SetVisible(float a)
    {
        if (body == null) return;
        body.gameObject.SetActive(a > 0f);
        surface.gameObject.SetActive(a > 0f);
    }

    private float GroundY()
    {
        return stackManager != null ? stackManager.GetCurrentGroundLevel() : 0f;
    }
}
