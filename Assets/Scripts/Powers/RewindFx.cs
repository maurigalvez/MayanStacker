using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the Tzolk'in Rewind looks and sounds like: time running backwards.
///
///  - The rewound stones play their drop in reverse, top stone first. Each one flies back up
///    to the hook it fell from, decelerating the way a falling stone accelerates, turning
///    jade and fading, and leaves a trail of after-images behind.
///  - A Tzolk'in calendar wheel turns counter-clockwise over the screen, fast then slowing,
///    under a warm "old parchment" wash with jade edges.
///  - A reverse swell (a sound that grows and cuts off, like a cymbal played backwards) plays
///    until an authored SFX_Power_TzolkinRewind is wired onto the power.
///
/// Presentation only: <see cref="PowerSystem"/> has already taken the stones off the stack
/// and restored the run when this starts. Everything runs on unscaled time, so the hit-stop
/// that opens it doesn't stretch it. Built in code on its own non-interactive canvas; the
/// wheel is the power's own icon (the calendar with the jade arrow), with a code-drawn wheel
/// as the fallback when the power has no icon.
/// </summary>
public class RewindFx : MonoBehaviour
{
    private const float TrailInterval = 0.05f;
    private const float TrailLife = 0.3f;

    private static Sprite wheelSprite;
    private static Sprite edgeSprite;
    private static AudioClip placeholderSound;

    private PowerSettings settings;
    private GameObject overlayRoot;
    private Image wash;
    private Image edges;
    private RectTransform wheelRect;
    private Image wheel;
    private bool wheelTinted; // the code-drawn wheel is white and takes the power's colour; the icon keeps its own
    private Coroutine screenRoutine;

    /// <summary>Seconds until the last stone has finished flying back up.</summary>
    public static float StonesDuration(PowerSettings s, int stones) =>
        stones <= 0 ? 0f : s.rewindStagger * (stones - 1) + s.rewindStoneSeconds;

    public void Init(PowerSettings powerSettings, int sortingOrder)
    {
        settings = powerSettings;
        BuildOverlay(sortingOrder);
    }

    /// <summary>
    /// Plays the rewind. <paramref name="stones"/> are already detached from the stack (top
    /// first); this animates and then destroys them. <paramref name="hook"/> is where they
    /// fly back to.
    /// </summary>
    public void Play(List<StackableObject> stones, Vector3 hook, Color accent, Sprite icon, AudioClip sound,
        float volume, GameSoundManager soundManager)
    {
        for (int i = 0; i < stones.Count; i++)
        {
            if (stones[i] != null) StartCoroutine(RewindStone(stones[i], hook, accent, i * settings.rewindStagger));
        }

        // The medallion's icon, blown up and spun backwards: the same calendar the player just
        // tapped is what turns time back.
        wheelTinted = icon == null;
        wheel.sprite = wheelTinted ? WheelSprite() : icon;

        if (screenRoutine != null) StopCoroutine(screenRoutine);
        screenRoutine = StartCoroutine(ScreenRoutine(accent));

        if (soundManager != null)
        {
            soundManager.PlaySound(sound != null ? sound : PlaceholderSound(), sound != null ? volume : 0.8f);
        }
    }

    /// <summary>Ends the screen effect at once (run over, scene leaving). Stones still finish and clean up.</summary>
    public void StopScreen()
    {
        if (screenRoutine != null) StopCoroutine(screenRoutine);
        screenRoutine = null;
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    // ---- Stones ----

    private IEnumerator RewindStone(StackableObject stone, Vector3 hook, Color accent, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (stone == null) yield break;

        Transform t = stone.transform;
        SpriteRenderer main = stone.SpriteRenderer;
        SpriteRenderer[] renderers = stone.GetComponentsInChildren<SpriteRenderer>(true);
        var startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            startColors[i] = renderers[i].color;
            // Rewound stones pass in front of the tower on their way up, never behind it.
            renderers[i].sortingOrder += 30;
        }

        Vector3 start = t.position;
        Quaternion startRotation = t.rotation;
        // The hook holds the next stone; aim just under it so the two don't overlap at the end.
        Vector3 end = new Vector3(hook.x, Mathf.Max(start.y + 1f, hook.y - 0.4f), start.z);
        float duration = Mathf.Max(0.05f, settings.rewindStoneSeconds);
        float elapsed = 0f;
        float nextTrail = 0f;

        while (elapsed < duration)
        {
            if (stone == null) yield break;

            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / duration);

            // A fall in reverse: fast off the tower, slowing as it nears the hook.
            float rise = 1f - (1f - k) * (1f - k);
            t.position = Vector3.LerpUnclamped(start, end, rise);
            t.rotation = Quaternion.Slerp(startRotation, Quaternion.identity, rise);

            // Jade first, then gone: the stone is being taken back out of time.
            float tintK = Mathf.Clamp01(k * 2.2f);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, k));
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = Color.Lerp(startColors[i], accent, tintK * 0.65f);
                c.a = startColors[i].a * alpha;
                renderers[i].color = c;
            }

            if (main != null && elapsed >= nextTrail && k < 0.85f)
            {
                nextTrail = elapsed + TrailInterval;
                SpawnAfterImage(main, accent, 0.45f * alpha);
            }

            yield return null;
        }

        if (stone != null) Destroy(stone.gameObject);
    }

    private void SpawnAfterImage(SpriteRenderer source, Color accent, float alpha)
    {
        if (source.sprite == null || alpha <= 0.02f) return;

        var go = new GameObject("RewindAfterImage");
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.lossyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;
        Color c = accent;
        c.a = alpha;
        sr.color = c;

        StartCoroutine(FadeAfterImage(sr, alpha));
    }

    private static IEnumerator FadeAfterImage(SpriteRenderer sr, float alpha)
    {
        float elapsed = 0f;
        while (elapsed < TrailLife && sr != null)
        {
            elapsed += Time.unscaledDeltaTime;
            Color c = sr.color;
            c.a = alpha * (1f - elapsed / TrailLife);
            sr.color = c;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    // ---- Screen ----

    private IEnumerator ScreenRoutine(Color accent)
    {
        overlayRoot.SetActive(true);

        float duration = Mathf.Max(0.2f, settings.rewindEffectSeconds);
        float elapsed = 0f;
        float angle = 0f;

        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            float k = Mathf.Clamp01(elapsed / duration);

            // Quick in, long out.
            float fade = Mathf.Min(Mathf.Clamp01(k / 0.12f), 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, k)));

            // Counter-clockwise (positive z in UI), spinning hard and winding down: the days
            // turning back.
            float speed = Mathf.Lerp(720f, 60f, Mathf.SmoothStep(0f, 1f, k));
            angle += speed * dt;
            wheelRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            wheelRect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.08f, k);

            Color w = settings.rewindWash;
            w.a *= fade;
            wash.color = w;

            Color e = accent;
            e.a = 0.55f * fade;
            edges.color = e;

            Color wc = wheelTinted ? accent : Color.white;
            wc.a = (wheelTinted ? 0.32f : 0.45f) * fade;
            wheel.color = wc;

            yield return null;
        }

        overlayRoot.SetActive(false);
        screenRoutine = null;
    }

    private void BuildOverlay(int sortingOrder)
    {
        overlayRoot = new GameObject("RewindOverlay");
        overlayRoot.transform.SetParent(transform, false);
        RunOverlayUI.CreateCanvas(overlayRoot, sortingOrder, interactive: false);

        RectTransform washRect = RunOverlayUI.CreateChild("Wash", overlayRoot.transform);
        RunOverlayUI.Stretch(washRect);
        wash = washRect.gameObject.AddComponent<Image>();
        wash.raycastTarget = false;

        RectTransform edgeRect = RunOverlayUI.CreateChild("Edges", overlayRoot.transform);
        RunOverlayUI.Stretch(edgeRect);
        edges = edgeRect.gameObject.AddComponent<Image>();
        edges.sprite = EdgeSprite();
        edges.raycastTarget = false;

        // 70% of the screen width, square, a little above centre (over the top of the tower).
        wheelRect = RunOverlayUI.CreateChild("CalendarWheel", overlayRoot.transform);
        wheelRect.anchorMin = new Vector2(0.15f, 0.55f);
        wheelRect.anchorMax = new Vector2(0.85f, 0.55f);
        wheelRect.pivot = new Vector2(0.5f, 0.5f);
        wheelRect.anchoredPosition = Vector2.zero;
        wheelRect.sizeDelta = Vector2.zero;
        var fitter = wheelRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        fitter.aspectRatio = 1f;
        wheel = wheelRect.gameObject.AddComponent<Image>();
        wheel.sprite = WheelSprite();
        wheel.preserveAspect = true;
        wheel.raycastTarget = false;

        overlayRoot.SetActive(false);
    }

    // ---- Code-made art and sound (placeholders until authored ones exist) ----

    /// <summary>
    /// A Tzolk'in wheel: an outer band of 20 day-sign blocks, 13 number dots inside it, an
    /// inner band and a hub. White, so the power's colour tints it. Used when the power has no icon.
    /// </summary>
    private static Sprite WheelSprite()
    {
        if (wheelSprite != null) return wheelSprite;

        const int s = 256;
        const float aa = 1.5f / (s * 0.5f); // about a pixel and a half of soft edge
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f;
                float ny = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float a01 = (Mathf.Atan2(ny, nx) / (2f * Mathf.PI) + 1f) % 1f;

                // Outer band, cut into 20 blocks.
                float band = Band(r, 0.8f, 0.97f, aa);
                float slot = a01 * 20f;
                float gap = Mathf.Abs(slot - Mathf.Round(slot)) * (2f * Mathf.PI * 0.885f / 20f); // arc distance to a cut
                band *= Mathf.Clamp01((gap - 0.02f) / aa);

                // 13 dots, the day numbers.
                float slot13 = a01 * 13f;
                float da = (slot13 - Mathf.Floor(slot13) - 0.5f) * (2f * Mathf.PI * 0.68f / 13f);
                float dr = r - 0.68f;
                float dots = Mathf.Clamp01((0.055f - Mathf.Sqrt(da * da + dr * dr)) / aa);

                float inner = Band(r, 0.47f, 0.55f, aa);
                float hub = Mathf.Clamp01((0.14f - r) / aa);

                float a = Mathf.Max(Mathf.Max(band, dots), Mathf.Max(inner, hub));
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return wheelSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    private static float Band(float r, float inner, float outer, float aa) =>
        Mathf.Clamp01((r - inner) / aa) * Mathf.Clamp01((outer - r) / aa);

    /// <summary>Soft edge glow, clear in the middle.</summary>
    private static Sprite EdgeSprite()
    {
        if (edgeSprite != null) return edgeSprite;

        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f;
                float ny = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny) / 1.41421f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, r));
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return edgeSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// A reverse swell: filtered noise and a rising tone that grow louder and stop dead, the
    /// way a sound played backwards does.
    /// </summary>
    private static AudioClip PlaceholderSound()
    {
        if (placeholderSound != null) return placeholderSound;

        const int rate = 44100;
        const float seconds = 0.75f;
        int n = Mathf.RoundToInt(rate * seconds);
        var data = new float[n];
        var rng = new System.Random(2026);
        float lp = 0f;
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float k = (float)i / n;
            float env = Mathf.Pow(k, 2.4f) * Mathf.Clamp01((1f - k) / 0.02f); // swell, then a hard stop
            float cutoff = Mathf.Lerp(0.02f, 0.35f, k);
            lp += cutoff * ((float)(rng.NextDouble() * 2.0 - 1.0) - lp);

            float freq = Mathf.Lerp(180f, 720f, k * k);
            phase += 2f * Mathf.PI * freq / rate;
            float tone = Mathf.Sin(phase) * 0.35f + Mathf.Sin(phase * 1.5f) * 0.15f;

            data[i] = (lp * 0.9f + tone) * env * 0.6f;
        }

        placeholderSound = AudioClip.Create("SFX_TzolkinRewind_Placeholder", n, 1, rate, false);
        placeholderSound.SetData(data, 0);
        return placeholderSound;
    }
}
