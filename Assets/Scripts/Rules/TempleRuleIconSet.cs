using UnityEngine;

/// <summary>
/// The temple-rule badges and the locked-power badge, picked from the effects sheet
/// (Art/UI/MayanStacker_Effects_Icons). One asset at Resources/UI/TempleRuleIcons, read by
/// <see cref="TempleRuleIcons"/>; swap the art by pointing a field at another sprite.
/// </summary>
public class TempleRuleIconSet : ScriptableObject
{
    public const string ResourcePath = "UI/TempleRuleIcons";

    public Sprite earthquake;
    public Sprite rainSlick;
    public Sprite eclipse;
    public Sprite jungleWind;
    public Sprite risingCenote;

    [Tooltip("The lock over a power card the player hasn't earned yet.")]
    public Sprite powerLocked;

    public Sprite For(LevelRule rule)
    {
        switch (rule)
        {
            case LevelRule.Earthquake: return earthquake;
            case LevelRule.RainSlick: return rainSlick;
            case LevelRule.Eclipse: return eclipse;
            case LevelRule.JungleWind: return jungleWind;
            case LevelRule.RisingCenote: return risingCenote;
            default: return null;
        }
    }
}
