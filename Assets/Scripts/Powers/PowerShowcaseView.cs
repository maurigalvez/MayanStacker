using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The moment a power fires: its medallion slams down onto the power button that was tapped, an
/// inked ring and a sun burst in the power's colour break out from it, and its name unrolls on a
/// stone plate just above the button. About a second and a half, then it shrinks back into the
/// button and fades.
///
/// Drawn like the rest of the game's art: flat cel fills inside a thick ink outline, opaque, no
/// soft glows, glints or light streaks. The burst turns in steps ("on twos") rather than a smooth
/// spin, the way hand-drawn effects do.
///
/// It lives in the button's corner on purpose: the power acts on the tower top and the held
/// stone, so nothing here may cross the middle of the screen while that happens. The stage
/// follows the button wherever it is placed, and the plate mirrors to open toward the centre
/// when the button sits on the right.
///
/// Its own thing, not a lesson: the <see cref="GuideLane"/> is a quiet caption that teaches;
/// this is a celebration of a choice the player just made, and it can play mid-lesson.
///
/// Presentation only — the prefab (Resources/UI/PowerShowcase) is the only source of the look,
/// nothing is built at runtime. <see cref="PowerSystem"/> plays it and cuts it at run end.
/// Non-interactive (no raycaster, no raycast targets), so it can never swallow a drop.
/// Unscaled time throughout, so hit-stop and the Kukulkan slow-mo don't stall it.
///
/// Menu: TamalStacker ▸ Powers ▸ Create Power Showcase Prefab generates the styled prefab.
/// </summary>
public class PowerShowcaseView : MonoBehaviour
{
    public const string PrefabResourcePath = "UI/PowerShowcase";

    [Header("Parts")]
    [Tooltip("Idle while nothing is playing, so the showcase costs nothing between powers.")]
    [SerializeField] private Canvas canvas;
    [Tooltip("Everything below; swells and fades as one on the way out.")]
    [SerializeField] private RectTransform stage;
    [SerializeField] private CanvasGroup stageGroup;

    [Tooltip("Inked sun burst behind the medallion; its ink outline keeps it readable on every sky.")]
    [SerializeField] private Image rays;
    [SerializeField] private Image shockwave;

    [SerializeField] private RectTransform medallion;
    [SerializeField] private CanvasGroup medallionGroup;
    [SerializeField] private Image ring;
    [SerializeField] private Image icon;

    [SerializeField] private RectTransform plate;
    [SerializeField] private CanvasGroup plateGroup;
    [SerializeField] private TextMeshProUGUI nameLabel;

    [Header("Look")]
    [Tooltip("Tint the name with the power's accent. Off keeps the authored colour.")]
    [SerializeField] private bool tintNameWithAccent = true;

    [Header("Timing (seconds, unscaled)")]
    [Tooltip("The medallion falling onto the screen; the burst goes off when it lands.")]
    [SerializeField] private float slamSeconds = 0.2f;
    [Tooltip("From the tap until it starts to leave.")]
    [SerializeField] private float holdSeconds = 1.25f;
    [SerializeField] private float exitSeconds = 0.3f;
    [Tooltip("Degrees per second; the burst turns faster as it leaves.")]
    [SerializeField] private float raysSpin = 40f;
    [Tooltip("How often the burst's turn updates. 12 = animated on twos, like hand-drawn effects; " +
             "0 turns it smoothly.")]
    [SerializeField] private float raysStepsPerSecond = 12f;

    [Header("Motion")]
    [Tooltip("How big the medallion starts before it slams down to size. Keep it small: a big " +
             "start covers the tower top for the first frames.")]
    [SerializeField] private float slamFromScale = 1.6f;
    [Tooltip("How far the shockwave ring grows, as a multiple of its authored size.")]
    [SerializeField] private float shockwaveScale = 2.2f;
    [Tooltip("Extra letter spacing the name starts with before it snaps tight.")]
    [SerializeField] private float nameSpacingFrom = 30f;

    private const float ShockwaveSeconds = 0.45f;
    private const float RaysInSeconds = 0.35f;
    private const float PlateDelay = 0.04f;
    private const float PlateInSeconds = 0.28f;
    private const float NameInSeconds = 0.35f;

    private bool playing;
    private float elapsed;
    private float raysAngle;
    private float raysShownAngle;
    private float raysStepTimer;
    private Color accent = Color.white;
    private float lastSpacing = float.NaN;

    private Vector2 stageHome;
    private Vector2 placedHome;
    private Vector2 medallionHome;
    private Vector2 plateHome, plateHomePivot;
    private bool homesRead;

    /// <summary>True when this view has what it needs to play.</summary>
    public bool IsUsable => canvas != null && stage != null && nameLabel != null;

    private void Awake()
    {
        ReadHomes();
        Stop();
    }

    private void ReadHomes()
    {
        if (homesRead) return;
        homesRead = true;
        if (stage != null) stageHome = stage.anchoredPosition;
        if (medallion != null) medallionHome = medallion.anchoredPosition;
        if (plate != null) { plateHome = plate.anchoredPosition; plateHomePivot = plate.pivot; }
    }

    /// <summary>
    /// Puts the stage on <paramref name="anchor"/> (the power button) and opens the plate toward
    /// the centre of the screen: authored for a left-hand button, mirrored for a right-hand one.
    /// Without an anchor the stage stays where the prefab put it.
    /// </summary>
    private void PlaceOn(RectTransform anchor)
    {
        Vector2 home = stageHome;
        bool mirror = false;

        if (anchor != null)
        {
            Canvas from = anchor.GetComponentInParent<Canvas>();
            Camera cam = from != null && from.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? from.rootCanvas.worldCamera
                : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, anchor.TransformPoint(anchor.rect.center));
            var root = (RectTransform)canvas.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local))
            {
                home = local;
                mirror = local.x > 0f;
            }
        }

        placedHome = home;
        Mirror(plate, plateHome, plateHomePivot, mirror);
    }

    private static void Mirror(RectTransform rt, Vector2 home, Vector2 homePivot, bool mirror)
    {
        if (rt == null) return;
        rt.pivot = mirror ? new Vector2(1f - homePivot.x, homePivot.y) : homePivot;
        rt.anchoredPosition = mirror ? new Vector2(-home.x, home.y) : home;
    }

    /// <summary>
    /// Plays the showcase from the top on <paramref name="anchor"/> (the button that fired it).
    /// Firing again mid-play restarts it.
    /// </summary>
    public void Play(string powerName, Sprite iconSprite, Color accentColor, RectTransform anchor = null)
    {
        ReadHomes();
        PlaceOn(anchor);

        accent = accentColor;
        nameLabel.text = powerName;
        if (tintNameWithAccent) nameLabel.color = new Color(accent.r, accent.g, accent.b, nameLabel.color.a);

        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
        if (ring != null) ring.color = accent;

        elapsed = 0f;
        raysAngle = Random.Range(0f, 360f); // never the same burst twice in a row
        raysShownAngle = raysAngle;
        raysStepTimer = 0f;
        lastSpacing = float.NaN;
        playing = true;
        canvas.enabled = true;
        Apply(0f, 0f);
    }

    /// <summary>Cuts it immediately — the run ended under it.</summary>
    public void Stop()
    {
        playing = false;
        if (canvas != null) canvas.enabled = false;
        if (stageGroup != null) stageGroup.alpha = 0f;
    }

    private void Update()
    {
        if (!playing) return;

        float dt = Time.unscaledDeltaTime;
        elapsed += dt;
        if (elapsed >= holdSeconds + exitSeconds)
        {
            Stop();
            return;
        }
        Apply(elapsed, dt);
    }

    private void Apply(float t, float dt)
    {
        float impact = slamSeconds;
        float sinceImpact = t - impact;

        // Leaving: everything shrinks back into the button and fades together.
        float exit = exitSeconds > 0f ? Mathf.Clamp01((t - holdSeconds) / exitSeconds) : 0f;
        stage.localScale = Vector3.one * (1f - 0.35f * EaseInCubic(exit));
        stage.anchoredPosition = placedHome;
        if (stageGroup != null) stageGroup.alpha = 1f - exit * exit;

        // Medallion: falls from huge onto the screen, then springs to size and floats.
        if (medallion != null)
        {
            float s;
            if (sinceImpact < 0f)
            {
                float k = impact > 0f ? t / impact : 1f;
                s = Mathf.Lerp(slamFromScale, 0.88f, k * k);
            }
            else
            {
                s = Mathf.LerpUnclamped(0.88f, 1f, EaseOutBack(Mathf.Clamp01(sinceImpact / 0.25f)));
            }
            medallion.localScale = Vector3.one * s;
            float bob = sinceImpact > 0f ? Mathf.Sin(sinceImpact * 5f) * 6f : 0f;
            medallion.anchoredPosition = medallionHome + Vector2.up * bob;
            if (medallionGroup != null) medallionGroup.alpha = impact > 0f ? Mathf.Clamp01(t / (impact * 0.5f)) : 1f;
        }

        // Shockwave: one inked ring out from the landing. It thins out rather than fading to a
        // ghost, so it stays a solid drawn shape until it is gone.
        if (shockwave != null)
        {
            float u = sinceImpact / ShockwaveSeconds;
            bool on = u >= 0f && u < 1f;
            shockwave.enabled = on;
            if (on)
            {
                shockwave.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, shockwaveScale, EaseOutCubic(u));
                shockwave.color = WithAlpha(accent, u < 0.75f ? 1f : 1f - (u - 0.75f) / 0.25f);
            }
        }

        // Burst: pops out on the landing, opaque, and turns in steps — faster on the way out.
        if (rays != null)
        {
            float u = Mathf.Clamp01(sinceImpact / RaysInSeconds);
            raysAngle -= raysSpin * (1f + 2f * exit) * dt;
            raysStepTimer += dt;
            float step = raysStepsPerSecond > 0f ? 1f / raysStepsPerSecond : 0f;
            if (raysStepTimer >= step)
            {
                raysStepTimer = step > 0f ? raysStepTimer % step : 0f;
                raysShownAngle = raysAngle;
            }
            rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, raysShownAngle);
            rays.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(u));
            rays.color = WithAlpha(accent, sinceImpact < 0f ? 0f : 1f);
        }

        // Plate: unrolls out of the button's side with a little overshoot, rolls back in on exit.
        float plateT = sinceImpact - PlateDelay;
        if (plate != null)
        {
            float u = Mathf.Clamp01(plateT / PlateInSeconds);
            float roll = 1f - EaseInCubic(exit);
            plate.localScale = new Vector3(Mathf.Max(0f, EaseOutBack(u) * roll), Mathf.Lerp(0.5f, 1f, EaseOutCubic(u)), 1f);
            if (plateGroup != null) plateGroup.alpha = Mathf.Clamp01(u * 3f);
        }

        // Name: letters snap together from wide spacing.
        {
            float u = Mathf.Clamp01((plateT - 0.06f) / NameInSeconds);
            float spacing = Mathf.Lerp(nameSpacingFrom, 0f, EaseOutCubic(u));
            if (!Mathf.Approximately(spacing, lastSpacing))
            {
                nameLabel.characterSpacing = spacing;
                lastSpacing = spacing;
            }
            nameLabel.alpha = u;
        }
    }

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static float EaseOutCubic(float x)
    {
        float i = 1f - x;
        return 1f - i * i * i;
    }

    private static float EaseInCubic(float x) => x * x * x;

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + c1 * m * m;
    }

#if UNITY_EDITOR
    /// <summary>Editor hook: the prefab generator wires the parts it built.</summary>
    public void Bind(Canvas canvas, RectTransform stage, CanvasGroup stageGroup,
        Image rays, Image shockwave,
        RectTransform medallion, CanvasGroup medallionGroup, Image ring, Image icon,
        RectTransform plate, CanvasGroup plateGroup, TextMeshProUGUI nameLabel)
    {
        this.canvas = canvas;
        this.stage = stage;
        this.stageGroup = stageGroup;
        this.rays = rays;
        this.shockwave = shockwave;
        this.medallion = medallion;
        this.medallionGroup = medallionGroup;
        this.ring = ring;
        this.icon = icon;
        this.plate = plate;
        this.plateGroup = plateGroup;
        this.nameLabel = nameLabel;
    }
#endif
}
