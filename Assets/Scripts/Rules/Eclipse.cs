using UnityEngine;

/// <summary>
/// Temple rule <see cref="LevelRule.Eclipse"/>: at set heights the moon slides over the sun for a
/// few seconds (totality) and the top of the tower fades away
/// (<see cref="EclipseSettings.hiddenTopStones"/> stones, down to
/// <see cref="EclipseSettings.ghostAlpha"/>). Stones that land during totality fade too, so the
/// player drops onto a target they have to remember; the swinging stone stays solid and warm-lit,
/// and the rest of the tower stays solid as a reference.
///
/// Sequence: a stone lands at a trigger height → warning (the sun in the corner darkens, the
/// top starts to fade, the moon slides over the sun) → totality (<see cref="EclipseSettings.totalitySeconds"/>) → the light
/// returns. Drops stay allowed throughout. A light sky dim stays on for the whole temple.
///
/// The sun hangs far back in the sky: behind Kukulkan and the stones, in front of the night
/// shade so it still glows when the sky goes dark. It turns slowly over a soft sunburst that
/// flares into a corona at totality, and the moon (smaller than the sun's disc) leaves a gold ring.
///
/// History: a permanent dark band below the top was invisible (the camera only shows ~2.5 stones
/// below the top); a near-black shade over everything but the top read as neither "dark" nor
/// "ghosted"; fading everything but the top added nothing, since the top is all a drop needs.
/// </summary>
public class Eclipse : TempleRuleBehaviour
{
    private const int ShadeSortingOrder = 15; // above stones (4-5), water (11-12), leaves (12)
    private const int NightShadeSortingOrder = 3; // over sky/mountains/clouds/ground (-1..3), under stones (4-5)
    // The sky group shares the night shade's order; depth decides the rest (orthographic sprites
    // of equal order sort by z, nearer drawn later): shade at the back, then rays, sun, moon, and
    // Kukulkan's neck (order 3, z 0) and head (order 4) in front of them all.
    private const int SkySortingOrder = NightShadeSortingOrder;
    private const float NightShadeZ = 2f;
    private const float RaysZ = 1.6f;
    private const float SunZ = 1.4f;
    private const float MoonZ = 1.2f;
    private const int LitStoneBoost = 20;     // lifts lit stones above the shade
    private const float FadeInSeconds = 1.4f;
    private const float LightReturnSeconds = 0.6f;
    private const float SunSize = 2f;
    private const float TotalitySwell = 0.1f;  // the sun grows this share at totality
    private const float SunSpinDegrees = 6f;   // per second, clockwise
    private const float RaysSpinDegrees = 4f;  // per second, the other way
    private const float RaysShare = 2.4f;      // sunburst size, in sun sizes
    private const float RaysCoronaGrow = 0.35f; // extra rays size at totality
    private const float RaysIdleAlpha = 0.35f;  // rays outside totality; full at totality
    // Moon size and where it starts, as a share of the sun's size, matching the map preview
    // (MapRuleFx_Eclipse: sun 60, moon 36, moon slides in from (-60, +12)). Smaller than the
    // sun's disc, so a gold ring stays at totality.
    private const float MoonShare = 36f / 60f;
    private static readonly Vector2 MoonStart = new Vector2(-1f, 0.2f);
    private const float ViewMargin = 3f;

    private static readonly Color ShadeColor = new Color(0.02f, 0.02f, 0.05f, 1f);
    private static readonly Color LitTint = new Color(1f, 0.86f, 0.6f, 1f); // warm corona light

    public override LevelRule Rule => LevelRule.Eclipse;

    private enum Phase { Idle, Warning, Totality, Returning }

    private EclipseSettings settings;
    private SpriteRenderer shade;
    private SpriteRenderer nightShade;
    private SpriteRenderer sun;
    private SpriteRenderer moon; // null when no moon art is wired
    private SpriteRenderer rays; // null when no rays art is wired
    private Camera cam;
    private float spin;          // seconds the sun has been turning (wraps on a whole turn of both)

    private Phase phase = Phase.Idle;
    private float phaseTimer;
    private float presence;       // 0..1 the eclipse's base presence (sky dim + sun)
    private float targetPresence;
    private float totality;       // 0..1 how dark the scene is right now

    private GameObject swingingStone;
    private SpriteRenderer[] litRenderers = new SpriteRenderer[0];
    private Color[] litColors = new Color[0];
    private int[] litOrders = new int[0];

    // The top stones, faded during the eclipse. Only alpha is touched, so tints other
    // systems put on the stones survive.
    private struct GhostStone
    {
        public StackableObject stone;
        public SpriteRenderer[] renderers;
        public float[] alphas;
    }
    private readonly System.Collections.Generic.List<GhostStone> ghosts = new System.Collections.Generic.List<GhostStone>();

    protected override void Build()
    {
        cam = Camera.main;
        shade = Create("EclipseShade", WhiteSprite, ShadeSortingOrder);
        nightShade = Create("EclipseNightShade", WhiteSprite, NightShadeSortingOrder);
        if (art.eclipseRays != null) rays = Create("EclipseRays", art.eclipseRays, SkySortingOrder);
        sun = Create("EclipseSun", art.eclipseSun != null ? art.eclipseSun : RulePlaceholderArt.EclipsedSun, SkySortingOrder);
        // Same sun + moon slices as the map preview: the moon slides over the sun as totality
        // comes on and back off as the light returns.
        if (art.eclipseSun != null && art.eclipseMoon != null)
            moon = Create("EclipseMoon", art.eclipseMoon, SkySortingOrder);
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
        RestoreGhosts();
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

        // The omen is on from the moon's approach until the light starts to return.
        OmenWindow.Set(Rule, RunLive && (phase == Phase.Warning || phase == Phase.Totality));

        if (phase == Phase.Idle) RestoreGhosts();
        else GhostTower(Mathf.Lerp(1f, settings.ghostAlpha, totality));

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

    /// <summary>Lifts the swinging stone above the shade, tinted like corona light.</summary>
    private void LightStones()
    {
        if (swingingStone == null || Contains(swingingStone)) return;

        RestoreLitStones();
        litRenderers = swingingStone.GetComponentsInChildren<SpriteRenderer>();
        litColors = new Color[litRenderers.Length];
        litOrders = new int[litRenderers.Length];
        for (int i = 0; i < litRenderers.Length; i++)
        {
            SpriteRenderer sr = litRenderers[i];
            litColors[i] = sr.color;
            litOrders[i] = sr.sortingOrder;
            sr.sortingOrder += LitStoneBoost;
            Color c = sr.color;
            sr.color = new Color(c.r * LitTint.r, c.g * LitTint.g, c.b * LitTint.b, c.a);
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
            // RGB only: alpha belongs to the spawner's arm fade and the ghosting, so restoring a
            // snapshot taken mid-fade would leave the held stone near-invisible.
            Color saved = litColors[i];
            sr.color = new Color(saved.r, saved.g, saved.b, sr.color.a);
        }
        litRenderers = new SpriteRenderer[0];
    }

    /// <summary>
    /// Fades the top <see cref="EclipseSettings.hiddenTopStones"/> stones to
    /// <paramref name="alphaScale"/>. A faded stone stays faded until the light returns, so stones
    /// landing during totality stack up as ghosts instead of revealing the one below.
    /// </summary>
    private void GhostTower(float alphaScale)
    {
        if (stackManager == null) return;
        var stack = stackManager.StackObjects;

        // A stone that left the tower (fell, Rewind) gets its opacity back.
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            GhostStone g = ghosts[i];
            if (g.stone == null || !stackManager.IsInStack(g.stone))
            {
                RestoreGhost(g);
                ghosts.RemoveAt(i);
            }
        }

        for (int i = Mathf.Max(0, stack.Count - Mathf.Max(1, settings.hiddenTopStones)); i < stack.Count; i++)
        {
            StackableObject stone = stack[i];
            if (stone != null && !IsGhosted(stone)) ghosts.Add(MakeGhost(stone));
        }

        for (int i = 0; i < ghosts.Count; i++)
        {
            GhostStone g = ghosts[i];
            for (int j = 0; j < g.renderers.Length; j++)
            {
                SpriteRenderer sr = g.renderers[j];
                if (sr == null) continue;
                Color c = sr.color;
                c.a = g.alphas[j] * alphaScale;
                sr.color = c;
            }
        }
    }

    private bool IsGhosted(StackableObject stone)
    {
        for (int i = 0; i < ghosts.Count; i++)
            if (ghosts[i].stone == stone) return true;
        return false;
    }

    private static GhostStone MakeGhost(StackableObject stone)
    {
        SpriteRenderer[] renderers = stone.GetComponentsInChildren<SpriteRenderer>();
        var alphas = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) alphas[i] = renderers[i].color.a;
        return new GhostStone { stone = stone, renderers = renderers, alphas = alphas };
    }

    private static void RestoreGhost(GhostStone g)
    {
        for (int j = 0; j < g.renderers.Length; j++)
        {
            SpriteRenderer sr = g.renderers[j];
            if (sr == null) continue;
            Color c = sr.color;
            c.a = g.alphas[j];
            sr.color = c;
        }
    }

    private void RestoreGhosts()
    {
        for (int i = 0; i < ghosts.Count; i++) RestoreGhost(ghosts[i]);
        ghosts.Clear();
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

        // Front shade: the light, temple-long mood dim over everything. Night shade: behind the
        // stones, it darkens the sky for exactly as long as the eclipse lasts, so its start, length
        // and end read at a glance without hiding the tower.
        FitToView(shade, c, halfW, halfH, settings.skyDim * presence, 0f);
        FitToView(nightShade, c, halfW, halfH, settings.totalityDim * totality, NightShadeZ);

        // The sun hangs high in the sky behind Kukulkan, turning slowly; it swells a little at
        // totality. Scaled time, so it holds still while paused.
        spin = Mathf.Repeat(spin + Time.deltaTime, 360f * 60f);
        float sunWorld = SunSize * (1f + TotalitySwell * totality);
        Vector2 sunPos = new Vector2(c.x + (halfW - ViewMargin) * 0.55f, c.y + (halfH - ViewMargin) * 0.62f);
        Place(sun, sunPos, sunWorld, SunZ, -spin * SunSpinDegrees);
        sun.color = new Color(1f, 1f, 1f, presence);

        // The sunburst turns the other way and flares into a corona at totality.
        if (rays != null)
        {
            float pulse = 1f + 0.04f * Mathf.Sin(spin * 2.2f);
            Place(rays, sunPos, sunWorld * RaysShare * (1f + RaysCoronaGrow * totality) * pulse, RaysZ, spin * RaysSpinDegrees);
            rays.color = new Color(1f, 1f, 1f, presence * Mathf.Lerp(RaysIdleAlpha, 1f, totality));
        }

        if (moon != null)
        {
            float along = totality * totality * (3f - 2f * totality); // eased, like the preview
            Vector2 off = MoonStart * sunWorld * (1f - along);
            Place(moon, sunPos + off, sunWorld * MoonShare, MoonZ, 0f);
            moon.color = new Color(1f, 1f, 1f, presence * along);
        }

        if (presence <= 0f && targetPresence <= 0f && phase == Phase.Idle) SetActive(false);
    }

    /// <summary>Centres <paramref name="sr"/> at <paramref name="pos"/>, its longest side <paramref name="worldSize"/>.</summary>
    private static void Place(SpriteRenderer sr, Vector2 pos, float worldSize, float z, float degrees)
    {
        Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        float scale = worldSize / Mathf.Max(0.001f, Mathf.Max(size.x, size.y));
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y, z), Quaternion.Euler(0f, 0f, degrees));
    }

    private static void FitToView(SpriteRenderer sr, Vector3 center, float halfW, float halfH, float alpha, float z)
    {
        Vector2 size = sr.sprite.bounds.size;
        sr.transform.position = new Vector3(center.x, center.y, z);
        sr.transform.localScale = new Vector3(halfW * 2f / size.x, halfH * 2f / size.y, 1f);
        Color sc = ShadeColor;
        sc.a = alpha;
        sr.color = sc;
    }

    private void SetActive(bool on)
    {
        if (shade == null) return;
        shade.gameObject.SetActive(on);
        nightShade.gameObject.SetActive(on);
        sun.gameObject.SetActive(on);
        if (moon != null) moon.gameObject.SetActive(on);
        if (rays != null) rays.gameObject.SetActive(on);
    }
}
