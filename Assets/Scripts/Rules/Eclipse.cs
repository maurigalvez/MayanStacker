using UnityEngine;

/// <summary>
/// Temple rule <see cref="LevelRule.Eclipse"/>: at set heights the moon slides over the sun and
/// the scene goes dark for a few seconds (totality). Only the swinging stone and the top stone
/// stay lit, so the player drops by the stones alone, without the rest of the tower to read.
///
/// Sequence: a stone lands at a trigger height → warning (the sun in the corner darkens, the
/// scene dims) → totality (<see cref="EclipseSettings.totalitySeconds"/>) → the light returns.
/// Drops stay allowed throughout. A light sky dim stays on for the whole temple so the rule reads.
///
/// The old design (a permanent dark band below the lit top) was invisible: the camera only
/// shows ~2.5 stones below the top, and the band started further down than that.
/// </summary>
public class Eclipse : TempleRuleBehaviour
{
    private const int ShadeSortingOrder = 15; // above stones (4-5), water (11-12), leaves (12)
    private const int SunSortingOrder = 16;
    private const int LitStoneBoost = 20;     // lifts lit stones above the shade
    private const float FadeInSeconds = 1.4f;
    private const float LightReturnSeconds = 0.6f;
    private const float SunSize = 2.4f;
    private const float ViewMargin = 3f;

    private static readonly Color ShadeColor = new Color(0.02f, 0.02f, 0.05f, 1f);
    private static readonly Color LitTint = new Color(1f, 0.86f, 0.6f, 1f); // warm corona light

    public override LevelRule Rule => LevelRule.Eclipse;

    private enum Phase { Idle, Warning, Totality, Returning }

    private EclipseSettings settings;
    private SpriteRenderer shade;
    private SpriteRenderer sun;
    private Camera cam;

    private Phase phase = Phase.Idle;
    private float phaseTimer;
    private float presence;       // 0..1 the eclipse's base presence (sky dim + sun)
    private float targetPresence;
    private float totality;       // 0..1 how dark the scene is right now

    private GameObject swingingStone;
    private SpriteRenderer[] litRenderers = new SpriteRenderer[0];
    private Color[] litColors = new Color[0];
    private int[] litOrders = new int[0];

    protected override void Build()
    {
        cam = Camera.main;
        shade = Create("EclipseShade", WhiteSprite, ShadeSortingOrder);
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
        RestoreLitStones();
        phase = Phase.Idle;
        presence = 0f;
        totality = 0f;
        targetPresence = RuleActive ? 1f : 0f;
        swingingStone = null;
        SetActive(RuleActive);
        FadeOutLoop();
    }

    protected override void OnRunEnd(bool won)
    {
        if (!RuleActive) return;
        // The sun returns for the capstone; a loss stays dim.
        EndTotality();
        if (won) targetPresence = 0f;
    }

    protected override void OnStoneSpawned(GameObject stone) => swingingStone = stone;

    protected override void OnStoneLanded(StackableObject stone)
    {
        if (settings == null || stackManager == null || phase != Phase.Idle) return;

        int height = stackManager.GetStackCount();
        if (height < settings.firstAtHeight) return;
        if ((height - settings.firstAtHeight) % Mathf.Max(2, settings.everyNStones) != 0) return;

        LevelData level = Level;
        if (level != null && height >= level.requiredStackHeight - 1) return;

        phase = Phase.Warning;
        phaseTimer = settings.warningSeconds;
        PlayOneShot(art.eclipseSting, 0.9f);
        SetLoop(art.eclipseLoop, 0.5f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
    }

    protected override void Update()
    {
        base.Update();
        if (!RuleActive || settings == null) return;

        bool paused = uiManager != null && uiManager.IsPaused;
        float udt = Time.unscaledDeltaTime;

        float speed = targetPresence > presence ? 1f / FadeInSeconds : 1f / LightReturnSeconds;
        presence = Mathf.MoveTowards(presence, targetPresence, speed * udt);

        if (!paused && phase != Phase.Idle)
        {
            if (phase != Phase.Returning && !RunLive) EndTotality();
            phaseTimer -= Time.deltaTime;

            switch (phase)
            {
                case Phase.Warning:
                    totality = 1f - Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, settings.warningSeconds));
                    if (phaseTimer <= 0f) BeginTotality();
                    break;
                case Phase.Totality:
                    totality = 1f;
                    if (phaseTimer <= 0f) EndTotality();
                    break;
                case Phase.Returning:
                    totality = Mathf.Clamp01(phaseTimer / LightReturnSeconds);
                    if (phaseTimer <= 0f) { totality = 0f; phase = Phase.Idle; }
                    break;
            }
        }

        // Keep the swinging stone lit through totality, including each new one.
        if (phase == Phase.Totality || phase == Phase.Warning) LightStones();

        Layout();
    }

    private void BeginTotality()
    {
        phase = Phase.Totality;
        phaseTimer = settings.totalitySeconds;
        SetLoop(art.eclipseLoop, 0.9f);
        if (cameraController != null) cameraController.Shake(0.15f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
    }

    private void EndTotality()
    {
        if (phase == Phase.Idle || phase == Phase.Returning) return;
        phase = Phase.Returning;
        phaseTimer = LightReturnSeconds * Mathf.Clamp01(totality);
        RestoreLitStones();
        FadeOutLoop();
    }

    /// <summary>Lifts the swinging and top stones above the shade, tinted like corona light.</summary>
    private void LightStones()
    {
        StackableObject top = stackManager != null ? stackManager.GetTopObject() : null;
        GameObject topGo = top != null ? top.gameObject : null;

        bool swingLit = swingingStone == null || Contains(swingingStone);
        bool topLit = topGo == null || Contains(topGo);
        if (swingLit && topLit) return;

        RestoreLitStones();
        var list = new System.Collections.Generic.List<SpriteRenderer>();
        if (swingingStone != null) list.AddRange(swingingStone.GetComponentsInChildren<SpriteRenderer>());
        if (topGo != null && topGo != swingingStone) list.AddRange(topGo.GetComponentsInChildren<SpriteRenderer>());

        litRenderers = list.ToArray();
        litColors = new Color[litRenderers.Length];
        litOrders = new int[litRenderers.Length];
        for (int i = 0; i < litRenderers.Length; i++)
        {
            SpriteRenderer sr = litRenderers[i];
            litColors[i] = sr.color;
            litOrders[i] = sr.sortingOrder;
            sr.sortingOrder += LitStoneBoost;
            sr.color = sr.color * LitTint;
        }
    }

    private bool Contains(GameObject go)
    {
        for (int i = 0; i < litRenderers.Length; i++)
            if (litRenderers[i] != null && litRenderers[i].transform.IsChildOf(go.transform)) return true;
        return false;
    }

    private void RestoreLitStones()
    {
        for (int i = 0; i < litRenderers.Length; i++)
        {
            SpriteRenderer sr = litRenderers[i];
            if (sr == null) continue;
            sr.sortingOrder = litOrders[i];
            sr.color = litColors[i];
        }
        litRenderers = new SpriteRenderer[0];
    }

    private void Layout()
    {
        if (cam == null) cam = Camera.main;
        float halfW = 8f, halfH = 12f;
        Vector3 c = Vector3.zero;
        if (cam != null && cam.orthographic)
        {
            halfH = cam.orthographicSize + ViewMargin;
            halfW = cam.orthographicSize * cam.aspect + ViewMargin;
            c = cam.transform.position;
        }

        Vector2 size = shade.sprite.bounds.size;
        shade.transform.position = new Vector3(c.x, c.y, 0f);
        shade.transform.localScale = new Vector3(halfW * 2f / size.x, halfH * 2f / size.y, 1f);

        float alpha = Mathf.Max(settings.skyDim * presence, settings.darkness * totality);
        Color sc = ShadeColor;
        sc.a = alpha;
        shade.color = sc;

        // The eclipsed sun hangs in the upper corner; it swells a little at totality.
        Vector2 sunSize = sun.sprite != null ? (Vector2)sun.sprite.bounds.size : Vector2.one;
        float sunScale = SunSize * (1f + 0.25f * totality) / Mathf.Max(0.001f, Mathf.Max(sunSize.x, sunSize.y));
        sun.transform.localScale = new Vector3(sunScale, sunScale, 1f);
        sun.transform.position = new Vector3(c.x + (halfW - ViewMargin) * 0.55f, c.y + (halfH - ViewMargin) * 0.62f, 0f);
        sun.color = new Color(1f, 1f, 1f, presence);

        if (presence <= 0f && targetPresence <= 0f && phase == Phase.Idle) SetActive(false);
    }

    private void SetActive(bool on)
    {
        if (shade == null) return;
        shade.gameObject.SetActive(on);
        sun.gameObject.SetActive(on);
    }
}
