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
/// in a clip. With water art wired it is layered like the map preview (MapRuleFx_RisingCenote):
/// a tinted copy of the water behind the front one, bobbing a little higher, and bubbles rising
/// inside it. Without art, <see cref="CenoteWater"/> draws vector water.
/// </summary>
public class RisingCenote : TempleRuleBehaviour
{
    private const int WaterSortingOrder = 11;     // in front of the stones
    private const float FloodMarginStones = 0.3f; // flooded once the water is this close to the top
    private const float SurfaceHeightWorld = 0.35f;
    private const float ViewMargin = 3f;

    // Water art layering, matching the map preview (back tint/alpha, front alpha, bubble alpha).
    private const float FrontAlpha = 0.6f;
    private const float BackAlpha = 0.45f;
    private const float BubbleAlpha = 0.8f;
    private const float CrestStones = 0.6f;       // height of the sprite's crest (top border), in stones
    private const float BackRiseStones = 0.18f;   // the back water sits this much higher
    private const float BobStones = 0.04f;
    private const float BobSeconds = 3f;          // map preview period
    private const int BubbleCount = 5;
    private const float BubbleStones = 0.18f;
    private const float BubbleRiseStones = 1.4f;
    private const float BubbleSeconds = 1.8f;     // map preview period

    private static readonly Color BackTint = new Color(0.62f, 0.8f, 0.9f, 1f);
    private static readonly Color WarningColor = new Color(0.75f, 0.32f, 0.2f, 1f);

    public override LevelRule Rule => LevelRule.RisingCenote;

    private RisingCenoteSettings settings;
    private SpriteRenderer body;
    private SpriteRenderer back;
    private SpriteRenderer surface;                 // only when a separate surface sprite is wired
    private SpriteRenderer[] bubbles = new SpriteRenderer[0];
    private CenoteWater vectorWater;
    private Camera cam;

    private bool rising;
    private float startAt;
    private float waterY;
    private float visibleAlpha;
    private bool warned;

    protected override void Build()
    {
        cam = Camera.main;

        // No water art wired: draw flat vector water in code.
        if (art.cenoteWater == null)
        {
            var go = new GameObject("CenoteWater");
            go.transform.SetParent(transform, false);
            vectorWater = go.AddComponent<CenoteWater>();
            vectorWater.Build(WaterSortingOrder);
            return;
        }

        // Sliced: the crest (top border) keeps its height, the body below stretches down.
        back = CreateRenderer("CenoteWaterBack", art.cenoteWater, WaterSortingOrder - 1);
        back.drawMode = SpriteDrawMode.Sliced;
        body = CreateRenderer("CenoteWater", art.cenoteWater, WaterSortingOrder);
        body.drawMode = SpriteDrawMode.Sliced;

        if (art.cenoteSurface != null)
        {
            surface = CreateRenderer("CenoteSurface", art.cenoteSurface, WaterSortingOrder + 1);
            surface.drawMode = SpriteDrawMode.Tiled;
        }

        if (art.cenoteBubble != null)
        {
            bubbles = new SpriteRenderer[BubbleCount];
            for (int i = 0; i < BubbleCount; i++)
                bubbles[i] = CreateRenderer("CenoteBubble" + i, art.cenoteBubble, WaterSortingOrder + 1);
        }

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

        float width = halfWidth * 2f;

        // Warning colour as the water closes on the top stone.
        float gap = stone > 0f ? (top - waterY) / stone : 99f;
        float danger = settings != null ? Mathf.Clamp01(1f - (gap - FloodMarginStones) / Mathf.Max(0.1f, settings.warningStones)) : 0f;
        float pulse = danger > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) : 0f;
        float tint = danger * (0.5f + 0.5f * pulse);

        if (vectorWater != null)
        {
            vectorWater.SetShown(visibleAlpha > 0f);
            if (visibleAlpha > 0f)
                vectorWater.Draw(centreX, halfWidth, waterY, bottom, stone, tint, WarningColor, visibleAlpha);
            return;
        }

        SetVisible(visibleAlpha);
        if (visibleAlpha <= 0f) return;

        // The water art's own colours, pulled toward the warning colour as the water closes in.
        Color warm = Color.Lerp(Color.white, WarningColor, tint * 0.7f);
        float bob = Mathf.Sin(Time.time * Mathf.PI * 2f / BobSeconds);

        // The crest's top edge sits on waterY; the body runs down past the screen bottom.
        float scale = CrestScale(body.sprite, stone);
        float frontTop = waterY + bob * BobStones * stone;
        PlaceSliced(body, centreX, frontTop, bottom, width, scale);
        body.color = new Color(warm.r, warm.g, warm.b, FrontAlpha * visibleAlpha);

        // The back copy rides a little higher, out of step, shifted so the crests don't line up.
        float backTop = waterY + (BackRiseStones - bob * BobStones) * stone;
        PlaceSliced(back, centreX + stone * 0.7f, backTop, bottom, width, scale);
        back.color = new Color(BackTint.r * warm.r, BackTint.g * warm.g, BackTint.b * warm.b, BackAlpha * visibleAlpha);

        if (surface != null)
        {
            SizeRenderer(surface, centreX, frontTop, width, SurfaceHeightWorld);
            surface.color = new Color(warm.r, warm.g, warm.b, 0.8f * visibleAlpha);
        }

        // Bubbles rise inside the water to just under the crest, spread across the view.
        float visibleHalf = Mathf.Max(0.5f, halfWidth - ViewMargin);
        for (int i = 0; i < bubbles.Length; i++)
        {
            SpriteRenderer b = bubbles[i];
            float t = Mathf.Repeat(Time.time / BubbleSeconds + i * 0.37f, 1f);
            float x = centreX + visibleHalf * (-0.8f + 1.6f * (i + 0.5f) / bubbles.Length)
                      + Mathf.Sin(t * Mathf.PI * 2f) * 0.08f * stone;
            float y = frontTop - (CrestStones * 0.5f + BubbleRiseStones * (1f - t)) * stone;
            Vector2 size = b.sprite.bounds.size;
            float bs = BubbleStones * stone / Mathf.Max(0.001f, Mathf.Max(size.x, size.y));
            b.transform.position = new Vector3(x, y, 0f);
            b.transform.localScale = new Vector3(bs, bs, 1f);
            float fade = Mathf.Min(1f, Mathf.Min(t, 1f - t) / 0.15f);
            b.color = new Color(1f, 1f, 1f, BubbleAlpha * fade * visibleAlpha);
        }
    }

    /// <summary>Scale that makes the sprite's crest (its top border, or the whole sprite without
    /// borders) <see cref="CrestStones"/> stones tall.</summary>
    private static float CrestScale(Sprite sprite, float stone)
    {
        float crest = sprite.border.w > 0f ? sprite.border.w / sprite.pixelsPerUnit : sprite.bounds.size.y;
        return CrestStones * stone / Mathf.Max(0.001f, crest);
    }

    /// <summary>Lays a 9-sliced sprite from <paramref name="top"/> down to <paramref name="bottom"/>, any pivot.</summary>
    private static void PlaceSliced(SpriteRenderer sr, float x, float top, float bottom, float width, float scale)
    {
        // Never shorter than the crest + bottom edge, or the slices squash (water below the screen).
        float borders = (sr.sprite.border.y + sr.sprite.border.w) / sr.sprite.pixelsPerUnit * scale;
        float height = Mathf.Max(0.01f, borders, top - bottom);
        bottom = top - height;
        Vector2 pivotShare = new Vector2(sr.sprite.pivot.x / sr.sprite.rect.width, sr.sprite.pivot.y / sr.sprite.rect.height);
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.size = new Vector2(width / scale, height / scale);
        sr.transform.position = new Vector3(x + (pivotShare.x - 0.5f) * width, bottom + pivotShare.y * height, 0f);
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
        bool show = a > 0f;
        if (vectorWater != null) vectorWater.SetShown(show);
        if (body == null || body.gameObject.activeSelf == show) return;
        body.gameObject.SetActive(show);
        back.gameObject.SetActive(show);
        if (surface != null) surface.gameObject.SetActive(show);
        for (int i = 0; i < bubbles.Length; i++) bubbles[i].gameObject.SetActive(show);
    }

    private float GroundY()
    {
        return stackManager != null ? stackManager.GetCurrentGroundLevel() : 0f;
    }
}
