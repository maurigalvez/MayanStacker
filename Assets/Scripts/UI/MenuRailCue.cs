using UnityEngine;

/// <summary>
/// The "something is waiting here" cue on a main-menu rail medallion: a small jade blob that pops
/// onto the badge's corner and pulses, while the badge itself gives a little hop every few
/// seconds. The medallions carry no text, so this is how the player learns which one to tap.
///
/// The chip script decides when to light it (<see cref="Set"/>) and remembers what the player has
/// already looked at with <see cref="Seen"/> / <see cref="MarkSeen"/>.
///
/// Built into each chip by TamalStacker ▸ UI ▸ Main Menu Rail ▸ Build Rail.
/// </summary>
public class MenuRailCue : MonoBehaviour
{
    private const string SeenPrefix = "rail_seen_";
    private const float PopTime = 0.28f;
    private const float HopPeriod = 2.6f;
    private const float HopTime = 0.55f;
    private const float HopHeight = 9f;

    [SerializeField] private RectTransform dot;
    [SerializeField] private RectTransform badge;

    private bool lit;
    private float litAt;
    private Vector2 badgeHome;
    private bool homeKnown;

    public static int Seen(string key) => PlayerPrefs.GetInt(SeenPrefix + key, 0);

    public static void MarkSeen(string key, int value)
    {
        PlayerPrefs.SetInt(SeenPrefix + key, value);
    }

    public void Set(bool on)
    {
        if (on == lit) return;
        lit = on;
        if (on) litAt = Time.unscaledTime;
        else Rest();
    }

    private void OnEnable()
    {
        if (!homeKnown && badge != null)
        {
            badgeHome = badge.anchoredPosition;
            homeKnown = true;
        }
        if (dot != null) dot.gameObject.SetActive(lit);
    }

    private void OnDisable()
    {
        Rest();
    }

    private void Update()
    {
        if (!lit || dot == null) return;

        float t = Time.unscaledTime - litAt;
        dot.gameObject.SetActive(true);

        // Pop in with an overshoot, then breathe.
        float pop = Mathf.Clamp01(t / PopTime);
        float overshoot = 1f + 2.2f * Mathf.Pow(pop - 1f, 3f) + 1.2f * Mathf.Pow(pop - 1f, 2f);
        float pulse = 1f + 0.12f * Mathf.Sin(t * 5f);
        float scale = (pop < 1f ? overshoot : 1f) * pulse;
        dot.localScale = new Vector3(scale, scale, 1f);

        if (badge == null) return;
        float phase = t % HopPeriod;
        float hop = phase < HopTime ? Mathf.Sin(phase / HopTime * Mathf.PI) * HopHeight : 0f;
        badge.anchoredPosition = badgeHome + new Vector2(0f, hop);
    }

    private void Rest()
    {
        if (dot != null) dot.gameObject.SetActive(false);
        if (badge != null && homeKnown) badge.anchoredPosition = badgeHome;
    }
}
