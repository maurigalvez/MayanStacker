using UnityEngine;

/// <summary>
/// Look of the aim glyphs: a small carved mark on the centre of the top stone and a matching
/// one on the underside of the hanging stone. They replace the old drop-guide line, which
/// projected the landing point and turned a Perfect into a reaction test. The glyphs only mark
/// both centres - the player still judges the drop.
///
/// Optional: without an asset at Resources/AimGlyphSettings the glyphs use these defaults and
/// a code-drawn diamond. They are the same for every player on purpose (leaderboard fairness),
/// so there is no per-player toggle.
/// </summary>
[CreateAssetMenu(fileName = "AimGlyphSettings", menuName = "TamalStacker/Aim Glyph Settings", order = 6)]
public class AimGlyphSettings : ScriptableObject
{
    /// <summary>The Resources path GameFeelManager loads this from.</summary>
    public const string ResourcePath = "AimGlyphSettings";

    [Header("Art")]
    [Tooltip("Glyph carved on the centre of the top stone. Empty = code-drawn diamond.")]
    public Sprite blockGlyph;

    [Tooltip("Glyph on the underside of the hanging stone. Empty = the block glyph, flipped.")]
    public Sprite stoneGlyph;

    public bool showOnStone = true;

    [Header("Look")]
    [Tooltip("Width of a glyph in world units. Stones are 4 units wide.")]
    public float glyphWorldSize = 0.45f;

    public Color glyphColor = new Color(1f, 0.93f, 0.7f, 0.55f);

    [Header("Theme tint (multiplied over Glyph Color)")]
    [Tooltip("White leaves the glyph as Glyph Color. A tint can only darken or shift the hue.")]
    public Color dayTint = Color.white;
    public Color sunsetTint = Color.white;
    public Color nightTint = Color.white;

    /// <summary>The tint for <paramref name="theme"/>; unknown themes use the day tint.</summary>
    public Color TintFor(GameTheme theme)
    {
        switch (theme)
        {
            case GameTheme.Sunset: return sunsetTint;
            case GameTheme.Night: return nightTint;
            default: return dayTint;
        }
    }

    [Header("Placement (world units)")]
    [Tooltip("How far below the top stone's upper surface the glyph's centre sits.")]
    public float blockInset = 0.2f;

    [Tooltip("How far above the hanging stone's lower surface the glyph's centre sits.")]
    public float stoneInset = 0.2f;

    [Tooltip("Drawn this many sorting orders above the stone it sits on.")]
    public int sortingOrderOffset = 1;
}
