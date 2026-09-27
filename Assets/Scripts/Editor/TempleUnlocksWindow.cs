using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opens chosen temples in the editor to test their rules without earning them. Backed by
/// <see cref="DebugTempleUnlocks"/> (EditorPrefs, never saved to progress or the cloud).
/// The level map reads unlocks when it's built, so reopen it (or re-enter play mode) after a change.
/// </summary>
public class TempleUnlocksWindow : EditorWindow
{
    private static readonly LevelRule[] Rules =
    {
        LevelRule.Earthquake, LevelRule.RainSlick, LevelRule.Eclipse, LevelRule.JungleWind, LevelRule.RisingCenote
    };

    private readonly List<LevelData> levels = new List<LevelData>();
    private Vector2 scroll;

    [MenuItem("TamalStacker/Temples/Debug/Temple Unlocks...")]
    public static void Open()
    {
        GetWindow<TempleUnlocksWindow>("Temple Unlocks");
    }

    private void OnEnable()
    {
        LoadLevels();
    }

    private void OnFocus()
    {
        LoadLevels();
    }

    private void LoadLevels()
    {
        levels.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:LevelData"))
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
            if (level != null) levels.Add(level);
        }
        levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Editor-only test unlocks. Real stars aren't touched, and beating a temple opened this way " +
            "saves no progress. Reopen the level map after changing these.",
            MessageType.Info);

        EditorGUILayout.LabelField("Unlock the first temple with…", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        foreach (LevelRule rule in Rules)
        {
            if (GUILayout.Button(RuleName(rule))) UnlockFirstWith(rule);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Unlock every temple with a rule")) UnlockAllWithRules();
        if (GUILayout.Button($"Clear all ({DebugTempleUnlocks.Count})")) DebugTempleUnlocks.Clear();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (LevelData level in levels)
        {
            bool forced = DebugTempleUnlocks.IsForced(level.levelNumber);
            string label = $"{level.levelNumber,2}  {level.levelName}";
            EditorGUILayout.BeginHorizontal();
            bool now = EditorGUILayout.ToggleLeft(label, forced, GUILayout.Width(240));
            EditorGUILayout.LabelField(RulesLabel(level), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (now != forced) DebugTempleUnlocks.SetForced(level.levelNumber, now);
        }
        EditorGUILayout.EndScrollView();
    }

    private void UnlockFirstWith(LevelRule rule)
    {
        foreach (LevelData level in levels)
        {
            if (level.rule == rule && level.secondRule == LevelRule.None)
            {
                DebugTempleUnlocks.SetForced(level.levelNumber, true);
                Debug.Log($"[TempleUnlocks] Opened temple {level.levelNumber} {level.levelName} ({RuleName(rule)}).");
                return;
            }
        }
        Debug.LogWarning($"[TempleUnlocks] No single-rule temple uses {rule}.");
    }

    private void UnlockAllWithRules()
    {
        foreach (LevelData level in levels)
        {
            if (level.rule != LevelRule.None || level.secondRule != LevelRule.None)
            {
                DebugTempleUnlocks.SetForced(level.levelNumber, true);
            }
        }
    }

    private static string RulesLabel(LevelData level)
    {
        if (level.rule == LevelRule.None) return "—";
        return level.secondRule == LevelRule.None
            ? RuleName(level.rule)
            : RuleName(level.rule) + " + " + RuleName(level.secondRule);
    }

    private static string RuleName(LevelRule rule)
    {
        switch (rule)
        {
            case LevelRule.Earthquake: return "Cabracán";
            case LevelRule.RainSlick: return "Rain";
            case LevelRule.Eclipse: return "Eclipse";
            case LevelRule.JungleWind: return "Wind";
            case LevelRule.RisingCenote: return "Cenote";
            default: return rule.ToString();
        }
    }
}
