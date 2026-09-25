using UnityEngine;

/// <summary>
/// Art and sound for the temple rules, in one optional asset at Resources/TempleRuleArt.
///
/// Every field is optional: a rule with no art draws a code-made placeholder and a rule with
/// no sound stays silent, so the rules play before any asset exists. The editor menu
/// TamalStacker ▸ Temples ▸ Wire Rule Art &amp; Audio fills this from Assets/Art/Rules/ by file
/// name, so dropping new files in and running it is the whole hookup.
/// </summary>
[CreateAssetMenu(fileName = "TempleRuleArt", menuName = "TamalStacker/Temple Rule Art", order = 7)]
public class TempleRuleArt : ScriptableObject
{
    public const string ResourcePath = "TempleRuleArt";

    [Header("Rule intro banner")]
    [Tooltip("Played with the banner that names the temple's rule at the start of an attempt.")]
    public AudioClip ruleIntroSting;

    [Header("Ball-court ring")]
    [Tooltip("Far half of the hoop (drawn behind the stones). Null = code-drawn ring.")]
    public Sprite ringBack;
    [Tooltip("Near half of the hoop (drawn in front of the stones). Null = code-drawn ring.")]
    public Sprite ringFront;
    public AudioClip ringPassSound;

    [Header("Jungle wind")]
    [Tooltip("One leaf, blown across the screen by the particle system.")]
    public Sprite windLeaf;
    public AudioClip windLoop;
    [Tooltip("Played when the gust changes direction.")]
    public AudioClip windGustSound;

    [Header("Rain-slick stones")]
    [Tooltip("One rain streak. Null = a thin code-drawn streak.")]
    public Sprite rainStreak;
    public AudioClip rainLoop;
    [Tooltip("Played when a stone slides.")]
    public AudioClip rainSlideSound;

    [Header("Earthquake")]
    [Tooltip("Hand/palm icon on the HOLD prompt.")]
    public Sprite quakeHoldIcon;
    [Tooltip("Looped under the warning and the shaking.")]
    public AudioClip quakeRumbleLoop;
    [Tooltip("Played on each jolt.")]
    public AudioClip quakeJoltSound;
    [Tooltip("Played the moment the player starts bracing.")]
    public AudioClip quakeBraceSound;

    [Header("Rising cenote")]
    [Tooltip("Water body. Tiled horizontally, stretched vertically. Null = flat code colour.")]
    public Sprite cenoteWater;
    [Tooltip("Wavy surface line along the top of the water.")]
    public Sprite cenoteSurface;
    public AudioClip cenoteLoop;
    [Tooltip("Played once when the water gets close to the top stone.")]
    public AudioClip cenoteWarningSound;
    [Tooltip("Played when the water reaches the top stone and the run ends.")]
    public AudioClip cenoteFloodSound;

    [Header("Eclipse")]
    [Tooltip("The eclipsed sun (dark disc + corona), hung in the sky.")]
    public Sprite eclipseSun;
    [Tooltip("Played as the light goes out at the start of the attempt.")]
    public AudioClip eclipseSting;
    [Tooltip("Optional low drone under the eclipse.")]
    public AudioClip eclipseLoop;

    private static TempleRuleArt current;
    private static bool loaded;

    /// <summary>The Resources asset, or an empty instance (every field null) when there is none.</summary>
    public static TempleRuleArt Current
    {
        get
        {
            if (!loaded || current == null)
            {
                current = Resources.Load<TempleRuleArt>(ResourcePath);
                if (current == null) current = CreateInstance<TempleRuleArt>();
                loaded = true;
            }
            return current;
        }
    }
}
