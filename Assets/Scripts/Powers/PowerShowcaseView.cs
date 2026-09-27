using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The moment a power fires: its medallion slams onto the screen, a shockwave and a burst of
/// sun rays in the power's colour break out from it, and its name stamps onto a stone plate
/// underneath, with a glint running across. About a second and a half, then it swells and
/// fades away.
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

    [Tooltip("Dark soft halo behind the rays, so light colours still read on the Day sky.")]
    [SerializeField] private Image shade;
    [SerializeField] private Image glow;
    [SerializeField] private Image rays;
    [SerializeField] private Image shockwave;

    [SerializeField] private RectTransform medallion;
    [SerializeField] private CanvasGroup medallionGroup;
    [SerializeField] private Image ring;
    [SerializeField] private Image icon;

    [SerializeField] private RectTransform plate;
    [SerializeField] private CanvasGroup plateGroup;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private RectTransform shine;
    [SerializeField] private RectTransform sparkLeft;
    [SerializeField] private RectTransform sparkRight;

    [Header("Look")]
    [Tooltip("Tint the name with the power's accent. Off keeps the authored colour.")]
    [SerializeField] private bool tintNameWithAccent = true;
    [Range(0f, 1f)] [SerializeField] private float shadeOpacity = 0.45f;
    [Range(0f, 1f)] [SerializeField] private float raysOpacity = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float glowOpacity = 0.45f;

    [Header("Timing (seconds, unscaled)")]
    [Tooltip("The medallion falling onto the screen; the burst goes off when it lands.")]
    [SerializeField] private float slamSeconds = 0.2f;
    [Tooltip("From the tap until it starts to leave.")]
    [SerializeField] private float holdSeconds = 1.25f;
    [SerializeField] private float exitSeconds = 0.3f;
    [Tooltip("Degrees per second; the rays spin faster as it leaves.")]
    [SerializeField] private float raysSpin = 40f;

    [Header("Motion")]
    [Tooltip("How big the medallion starts before it slams down to size.")]
    [SerializeField] private float slamFromScale = 2.6f;
    [Tooltip("Extra letter spacing the name starts with before it snaps tight.")]
    [SerializeField] private float nameSpacingFrom = 30f;

    private const float ShockwaveSeconds = 0.45f;
    private const float RaysInSeconds = 0.35f;
    private const float PlateDelay = 0.04f;
    private const float PlateInSeconds = 0.28f;
    private const float NameInSeconds = 0.35f;
    private const float ShineDelay = 0.3f;
    private const float ShineSeconds = 0.45f;

    private bool playing;
    private float elapsed;
    private float raysAngle;
    private Color accent = Color.white;
    private Color shadeColor;
    private float lastSpacing = float.NaN;

    private Vector2 stageHome;
    private Vector2 medallionHome;
    private Vector2 shineHome;
    private bool homesRead;

    /// <summary>True when this view has what it needs to play.</summary>
    public bool IsUsable => canvas != null && stage != null && nameLabel != null;

    private void Awake()
    {
        ReadHomes();
        if (shade != null) shadeColor = shade.color;
        Stop();
    }

    private void ReadHomes()
    {
        if (homesRead) return;
        homesRead = true;
        if (stage != null) stageHome = stage.anchoredPosition;
        if (medallion != null) medallionHome = medallion.anchoredPosition;
        if (shine != null) shineHome = shine.anchoredPosition;
    }

    /// <summary>Plays the showcase from the top. Firing again mid-play restarts it.</summary>
    public void Play(string powerName, Sprite iconSprite, Color accentColor)
    {
        ReadHomes();

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

        // Leaving: everything swells a touch, drifts up and fades together.
        float exit = exitSeconds > 0f ? Mathf.Clamp01((t - holdSeconds) / exitSeconds) : 0f;
        stage.localScale = Vector3.one * (1f + 0.12f * EaseOutCubic(exit));
        stage.anchoredPosition = stageHome + Vector2.up * (30f * EaseOutCubic(exit));
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

        // Dark halo first, so the burst has something to read against.
        if (shade != null)
        {
            Color c = shadeColor;
            c.a = shadeOpacity * EaseOutCubic(Mathf.Clamp01(t / 0.2f));
            shade.color = c;
        }

        // Shockwave: one ring out from the landing.
        if (shockwave != null)
        {
            float u = sinceImpact / ShockwaveSeconds;
            bool on = u >= 0f && u < 1f;
            shockwave.enabled = on;
            if (on)
            {
                shockwave.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 3f, EaseOutCubic(u));
                shockwave.color = WithAlpha(accent, 0.9f * (1f - u));
            }
        }

        // Rays: burst out on the landing and keep turning, faster on the way out.
        if (rays != null)
        {
            float u = Mathf.Clamp01(sinceImpact / RaysInSeconds);
            raysAngle -= raysSpin * (1f + 2f * exit) * dt;
            rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, raysAngle);
            rays.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(u));
            rays.color = WithAlpha(accent, raysOpacity * u);
        }

        // Glow: flares on the landing, settles into a soft breathing light.
        if (glow != null)
        {
            float a = sinceImpact < 0f
                ? 0f
                : glowOpacity * (0.8f + 1.2f * Mathf.Exp(-6f * sinceImpact)) * (0.92f + 0.08f * Mathf.Sin(sinceImpact * 6f));
            glow.color = WithAlpha(accent, Mathf.Clamp01(a));
        }

        // Plate: unrolls sideways from the centre with a little overshoot.
        float plateT = sinceImpact - PlateDelay;
        if (plate != null)
        {
            float u = Mathf.Clamp01(plateT / PlateInSeconds);
            plate.localScale = new Vector3(Mathf.Max(0f, EaseOutBack(u)), Mathf.Lerp(0.5f, 1f, EaseOutCubic(u)), 1f);
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

        // Sparks: two light lines shoot out from the plate's ends.
        float sparkU = EaseOutCubic(Mathf.Clamp01(plateT / 0.32f));
        if (sparkLeft != null) sparkLeft.localScale = new Vector3(sparkU, 1f, 1f);
        if (sparkRight != null) sparkRight.localScale = new Vector3(sparkU, 1f, 1f);

        // Glint: one sweep across the plate once the name has landed.
        if (shine != null && plate != null)
        {
            float u = (plateT - ShineDelay) / ShineSeconds;
            bool on = u >= 0f && u <= 1f;
            if (shine.gameObject.activeSelf != on) shine.gameObject.SetActive(on);
            if (on)
            {
                float reach = plate.rect.width * 0.5f + shine.rect.width;
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-reach, reach, EaseInOutQuad(u)), shineHome.y);
            }
        }
    }

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static float EaseOutCubic(float x)
    {
        float i = 1f - x;
        return 1f - i * i * i;
    }

    private static float EaseInOutQuad(float x) => x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) * 0.5f;

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
        Image shade, Image glow, Image rays, Image shockwave,
        RectTransform medallion, CanvasGroup medallionGroup, Image ring, Image icon,
        RectTransform plate, CanvasGroup plateGroup, TextMeshProUGUI nameLabel,
        RectTransform shine, RectTransform sparkLeft, RectTransform sparkRight)
    {
        this.canvas = canvas;
        this.stage = stage;
        this.stageGroup = stageGroup;
        this.shade = shade;
        this.glow = glow;
        this.rays = rays;
        this.shockwave = shockwave;
        this.medallion = medallion;
        this.medallionGroup = medallionGroup;
        this.ring = ring;
        this.icon = icon;
        this.plate = plate;
        this.plateGroup = plateGroup;
        this.nameLabel = nameLabel;
        this.shine = shine;
        this.sparkLeft = sparkLeft;
        this.sparkRight = sparkRight;
    }
#endif
}
