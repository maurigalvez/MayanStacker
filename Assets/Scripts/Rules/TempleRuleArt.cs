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
    [Tooltip("The water: a wavy crest on top of the body, drawn 9-sliced (set the sprite's borders so " +
             "the crest sits in the top border). Drawn twice, a tinted copy behind the front one, like the " +
             "map preview. Null = code-drawn vector water.")]
    public Sprite cenoteWater;
    [Tooltip("Optional extra surface line along the top of the water, tiled. Leave empty when the " +
             "water sprite has its own crest.")]
    public Sprite cenoteSurface;
    [Tooltip("One bubble, rising inside the water. Null = no bubbles.")]
    public Sprite cenoteBubble;
    public AudioClip cenoteLoop;
    [Tooltip("Played once when the water gets close to the top stone.")]
    public AudioClip cenoteWarningSound;
    [Tooltip("Played when the water reaches the top stone and the run ends.")]
    public AudioClip cenoteFloodSound;

    [Header("Eclipse")]
    [Tooltip("The sun hung in the sky. With a moon set, draw the plain sun; without one, the already-eclipsed sun.")]
    public Sprite eclipseSun;
    [Tooltip("The moon that slides over the sun as totality comes on. Null = the sun sprite alone.")]
    public Sprite eclipseMoon;
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
