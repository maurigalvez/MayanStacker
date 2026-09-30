using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the selected temple's rule preview (<see cref="MapRuleFx"/>) on the level map.
///
/// Only the selected temple ever has an effect: selecting another one moves it, and a paired
/// temple (21-27) plays both of its rules together. Each rule's prefab is instantiated once,
/// the first time it's needed, and then reused, so browsing the map never loads or allocates.
/// A missing prefab logs once and shows nothing.
/// </summary>
public class MapRulePreview
{
    private readonly Dictionary<LevelRule, MapRuleFx> instances = new Dictionary<LevelRule, MapRuleFx>();
    private readonly HashSet<LevelRule> missing = new HashSet<LevelRule>();
    private readonly List<LevelRule> rules = new List<LevelRule>(2);
    private readonly List<MapRuleFx> showing = new List<MapRuleFx>(2);

    /// <summary>Shows <paramref name="level"/>'s rule effects on <paramref name="button"/>; clears them for a temple without a rule.</summary>
    public void Show(RectTransform button, LevelData level)
    {
        Hide();
        if (button == null) return;

        TempleRuleIcons.RulesOf(level, rules);
        foreach (LevelRule rule in rules)
        {
            MapRuleFx fx = Get(rule);
            if (fx == null) continue;
            fx.Attach(button);
            showing.Add(fx);
        }
    }

    public void Hide()
    {
        foreach (MapRuleFx fx in showing)
        {
            if (fx != null) fx.Detach();
        }
        showing.Clear();
    }

    /// <summary>Destroys the cached effects (the map's buttons are being rebuilt).</summary>
    public void Clear()
    {
        Hide();
        foreach (MapRuleFx fx in instances.Values)
        {
            if (fx != null) Object.Destroy(fx.gameObject);
        }
        instances.Clear();
    }

    private MapRuleFx Get(LevelRule rule)
    {
        if (instances.TryGetValue(rule, out MapRuleFx fx) && fx != null) return fx;
        if (missing.Contains(rule)) return null;

        var prefab = Resources.Load<MapRuleFx>(MapRuleFx.ResourcePath(rule));
        if (prefab == null)
        {
            missing.Add(rule);
            Debug.LogWarning($"[MapRulePreview] No map effect at Resources/{MapRuleFx.ResourcePath(rule)} - " +
                             "run TamalStacker ▸ Temples ▸ Set Up Rule Previews.");
            return null;
        }

        fx = Object.Instantiate(prefab);
        fx.name = prefab.name;
        fx.gameObject.SetActive(false);
        instances[rule] = fx;
        return fx;
    }
}
