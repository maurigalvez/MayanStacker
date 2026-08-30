using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// A transient line of text across the middle of the screen: fades in, holds, fades out.
///
/// Used for anything that needs to tell the player something mid-run without owning a
/// piece of the HUD — the altitude band they just reached, the objective a temple is
/// asking for, the block they have never seen before. It builds itself on a
/// non-interactive overlay canvas, so it can never swallow the tap that drops a block and
/// never needs wiring into a scene.
///
/// One shared instance: a second banner would overlap the first, so a new message simply
/// replaces whatever was showing.
///
/// Presentation comes from a prefab at Resources/UI/RunBanner (see <see cref="RunBannerView"/>)
/// when there is one, so the banner can carry the game's font and framing instead of TMP's
/// defaults; without it the code-built pair of labels is used exactly as before.
/// </summary>
public class RunBanner : MonoBehaviour
{
    /// <summary>Authored prefab that replaces the code-built banner when present.</summary>
    public const string PrefabResourcePath = "UI/RunBanner";

    private static RunBanner instance;

    private RunBannerView view;
    private TextMeshProUGUI label;
    private TextMeshProUGUI subtitle;
    private Coroutine routine;

    /// <summary>
    /// Shows <paramref name="text"/> for a moment. Safe to call with an empty string
    /// (does nothing) and safe to call again before the previous banner has finished.
    /// </summary>
    public static void Show(string text, Color color, float holdSeconds = 1.6f, float yOffset = 260f)
    {
        if (string.IsNullOrEmpty(text)) return;

        RunBanner banner = Ensure();
        if (banner == null) return;

        banner.Display(text, null, color, holdSeconds, yOffset);
    }

    /// <summary>
    /// Shows a headline with a quieter line beneath it — a name, and what that name means.
    /// For anything the player is meeting for the first time, where the name alone would
    /// tell them nothing.
    /// </summary>
    public static void Show(string title, string subtitleText, Color color,
        float holdSeconds = 1.6f, float yOffset = 260f)
    {
        if (string.IsNullOrEmpty(title)) return;

        RunBanner banner = Ensure();
        if (banner == null) return;

        banner.Display(title, subtitleText, color, holdSeconds, yOffset);
    }

    private static RunBanner Ensure()
    {
        if (instance != null) return instance;

        var go = new GameObject("RunBanner");
        instance = go.AddComponent<RunBanner>();
        instance.Build();
        return instance;
    }

    private void Build()
    {
        if (BuildFromPrefab()) return;

        // Above the HUD so it reads clearly; non-interactive so input passes through.
        RunOverlayUI.CreateCanvas(gameObject, sortingOrder: 400, interactive: false);

        label = RunOverlayUI.CreateLabel("BannerText", transform, "", 60f, RunOverlayUI.Gold);
        RunOverlayUI.Place(label.rectTransform,
            anchor: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 260f),
            size: new Vector2(920f, 160f));

        subtitle = RunOverlayUI.CreateLabel("BannerSubtitle", transform, "", 32f, RunOverlayUI.Parchment);
        RunOverlayUI.Place(subtitle.rectTransform,
            anchor: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 165f),
            size: new Vector2(860f, 120f));
        subtitle.gameObject.SetActive(false);

        SetAlpha(0f);
    }

    /// <summary>
    /// Instantiates the authored prefab, if there is one. Returns false when no usable
    /// prefab exists, so Build falls back to the code-built labels.
    /// </summary>
    private bool BuildFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null) return false;

        // The prefab carries its own Canvas, so it renders correctly parented to this
        // otherwise-empty host object, which owns its lifetime.
        var instantiated = Instantiate(prefab, transform, false);
        view = instantiated.GetComponent<RunBannerView>();

        if (view == null || !view.IsUsable)
        {
            Debug.LogWarning($"[RunBanner] Resources/{PrefabResourcePath} has no usable RunBannerView " +
                             "component - using the code-built banner instead.");
            Destroy(instantiated);
            view = null;
            return false;
        }

        SetAlpha(0f);
        return true;
    }

    private void Display(string text, string subtitleText, Color color, float holdSeconds, float yOffset)
    {
        if (view != null)
        {
            view.Show(text, subtitleText, color);
            view.SetVerticalOffset(yOffset);

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FadeRoutine(holdSeconds));
            return;
        }

        if (label == null) return;

        label.text = text;
        label.color = color;

        Vector2 position = label.rectTransform.anchoredPosition;
        position.y = yOffset;
        label.rectTransform.anchoredPosition = position;

        // The subtitle hangs a fixed distance under the headline, so the pair reads as one
        // block of text wherever the caller decided to place the banner.
        if (subtitle != null)
        {
            bool hasSubtitle = !string.IsNullOrEmpty(subtitleText);
            subtitle.text = hasSubtitle ? subtitleText : string.Empty;
            subtitle.gameObject.SetActive(hasSubtitle);

            Vector2 subtitlePosition = subtitle.rectTransform.anchoredPosition;
            subtitlePosition.y = yOffset - 95f;
            subtitle.rectTransform.anchoredPosition = subtitlePosition;
        }

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeRoutine(holdSeconds));
    }

    /// <summary>
    /// Unscaled throughout: the banner has to survive hit-stop, the Kukulkan slow-motion
    /// and a fully paused game (the boon picker) without stalling half-faded.
    /// </summary>
    private IEnumerator FadeRoutine(float holdSeconds)
    {
        const float fadeIn = 0.35f;
        const float fadeOut = 0.6f;

        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Clamp01(t / fadeIn));
            yield return null;
        }
        SetAlpha(1f);

        yield return new WaitForSecondsRealtime(holdSeconds);

        t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(t / fadeOut));
            yield return null;
        }
        SetAlpha(0f);

        routine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (view != null)
        {
            view.SetAlpha(alpha);
            return;
        }

        if (label != null)
        {
            Color c = label.color;
            c.a = alpha;
            label.color = c;
        }

        if (subtitle != null)
        {
            Color c = subtitle.color;
            c.a = alpha;
            subtitle.color = c;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
