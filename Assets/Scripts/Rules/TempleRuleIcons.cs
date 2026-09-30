using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The badge for each temple rule, shown on the pre-play screen so the player sees what the
/// temple does before tapping PLAY.
///
/// The sprites are picked in the <see cref="TempleRuleIconSet"/> asset at
/// Resources/UI/TempleRuleIcons (slices of the effects sheet). A missing asset or icon logs once
/// and shows nothing; there is no code-drawn stand-in.
/// </summary>
public static class TempleRuleIcons
{
    private static TempleRuleIconSet set;
    private static bool loaded;
    private static readonly HashSet<LevelRule> warned = new HashSet<LevelRule>();

    private static TempleRuleIconSet Set
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                set = Resources.Load<TempleRuleIconSet>(TempleRuleIconSet.ResourcePath);
                if (set == null)
                    Debug.LogWarning($"[TempleRuleIcons] No icon set at Resources/{TempleRuleIconSet.ResourcePath}.");
            }
            return set;
        }
    }

    /// <summary>The icon for <paramref name="rule"/>, or null (logged once) when it isn't set.</summary>
    public static Sprite For(LevelRule rule)
    {
        if (rule == LevelRule.None) return null;

        Sprite sprite = Set != null ? Set.For(rule) : null;
        if (sprite == null && Set != null && warned.Add(rule))
            Debug.LogWarning($"[TempleRuleIcons] No icon for {rule} in Resources/{TempleRuleIconSet.ResourcePath}.");
        return sprite;
    }

    /// <summary>The lock badge for a power card the player hasn't earned yet, or null.</summary>
    public static Sprite PowerLocked() => Set != null ? Set.powerLocked : null;

    /// <summary>
    /// The temple's rules in play order (first, then second), without None or a repeat.
    /// Fills <paramref name="into"/>; empty for a temple without a rule.
    /// </summary>
    public static void RulesOf(LevelData level, List<LevelRule> into)
    {
        into.Clear();
        if (level == null) return;
        if (level.rule != LevelRule.None) into.Add(level.rule);
        if (level.secondRule != LevelRule.None && level.secondRule != level.rule) into.Add(level.secondRule);
    }
}
