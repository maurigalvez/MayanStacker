using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hot Stone's countdown: a ring that rides above the swinging stone and burns down until the
/// stone drops by itself. Goes from parchment to ember in the last stretch and throbs faster,
/// so the panic reads in a silent clip.
///
/// Authored prefab at Resources/UI/HotStoneTimer (TamalStacker ▸ Daily Challenge ▸ Create Hot
/// Stone Timer Prefab). Driven by <see cref="RunModifierEffects"/>; it only fills in position,
/// fill, colour and the seconds.
/// </summary>
public class HotStoneTimerView : MonoBehaviour
{
    public const string PrefabResourcePath = "UI/HotStoneTimer";

    [SerializeField] private Canvas canvas;
    [Tooltip("Moved over the stone each frame.")]
    [SerializeField] private RectTransform timer;
    [Tooltip("Radial-filled image: 1 = full time left.")]
    [SerializeField] private Image ring;
    [Tooltip("Optional: swings left, e.g. \"2\".")]
    [SerializeField] private TextMeshProUGUI secondsLabel;

    [Header("Look")]
    [Tooltip("World units above the stone's centre.")]
    [SerializeField] private float worldOffsetY = 0.9f;
    [SerializeField] private Color calmColor = new Color(0.96f, 0.9f, 0.75f, 1f);
    [SerializeField] private Color hotColor = new Color(0.95f, 0.35f, 0.15f, 1f);
    [Tooltip("Seconds left at which the ring starts turning hot.")]
    [SerializeField] private float hotFromSeconds = 1.2f;
    [SerializeField] private float pulseScale = 0.12f;

    private Camera cam;

    public bool IsUsable => canvas != null && timer != null && ring != null;

    public void Hide()
    {
        if (timer != null && timer.gameObject.activeSelf) timer.gameObject.SetActive(false);
    }

    /// <summary>
    /// Places the ring over <paramref name="stone"/> and shows <paramref name="fraction"/> of the
    /// fuse left. The label counts swings (what the fuse is made of); the heat reads seconds, so
    /// the last stretch glows the same however fast the swing is.
    /// </summary>
    public void Track(Transform stone, float fraction, float secondsLeft, float swingsLeft)
    {
        if (!IsUsable || stone == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (!timer.gameObject.activeSelf) timer.gameObject.SetActive(true);

        Vector3 screen = cam.WorldToScreenPoint(stone.position + Vector3.up * worldOffsetY);
        var parent = (RectTransform)timer.parent;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out Vector2 local))
        {
            timer.anchoredPosition = local;
        }

        ring.fillAmount = fraction;

        float heat = hotFromSeconds > 0f ? 1f - Mathf.Clamp01(secondsLeft / hotFromSeconds) : 0f;
        Color c = Color.Lerp(calmColor, hotColor, heat);
        ring.color = c;

        // Throbs faster as it burns down.
        float speed = Mathf.Lerp(4f, 16f, heat);
        float s = 1f + pulseScale * heat * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed));
        timer.localScale = new Vector3(s, s, 1f);

        if (secondsLabel != null)
        {
            secondsLabel.text = Mathf.CeilToInt(swingsLeft).ToString();
            secondsLabel.color = c;
        }
    }
}
