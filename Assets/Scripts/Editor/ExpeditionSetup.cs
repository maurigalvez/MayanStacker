#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/ExpeditionSettings.asset with the five Weekly Expedition stages. Idempotent:
/// an existing asset is kept, so tuning survives a re-run.
///
/// Menu: TamalStacker ▸ Economy ▸ Create Expedition Settings
/// </summary>
public static class ExpeditionSetup
{
    private const string SettingsPath = "Assets/Resources/" + ExpeditionSettings.ResourcePath + ".asset";

    [MenuItem("TamalStacker/Economy/Create Expedition Settings")]
    public static void CreateAssets()
    {
        EditorUtility.DisplayDialog("Weekly Expedition", Ensure(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateAssetsBatch()
    {
        Ensure();
    }

    private static string Ensure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        string note;
        if (AssetDatabase.LoadAssetAtPath<ExpeditionSettings>(SettingsPath) != null)
        {
            note = $"Kept {SettingsPath}.";
        }
        else
        {
            var s = ScriptableObject.CreateInstance<ExpeditionSettings>();
            s.stages = DefaultStages();
            AssetDatabase.CreateAsset(s, SettingsPath);
            note = $"Created {SettingsPath} with {s.stages.Length} stages.";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ExpeditionSetup] {note}");
        return note;
    }

    // Proposals (Q1). Stages ramp from "just play" to a Daily finish, so a casual player who plays
    // a few runs a day clears the week; ~140 jade for the week plus the 50 finale.
    private static ExpeditionSettings.Stage[] DefaultStages()
    {
        return new[]
        {
            Stage("first_steps", MissionSettings.Kind.Runs, MissionSettings.ModeFilter.Any, 4, 15),
            Stage("perfect_hands", MissionSettings.Kind.PerfectsTotal, MissionSettings.ModeFilter.Any, 30, 20),
            Stage("tall_tower", MissionSettings.Kind.StonesInRun, MissionSettings.ModeFilter.Infinite, 25, 25),
            Stage("sun_calls", MissionSettings.Kind.SunSurgesTotal, MissionSettings.ModeFilter.Any, 2, 30),
            Stage("daily_rite", MissionSettings.Kind.Runs, MissionSettings.ModeFilter.Daily, 1, 40)
        };
    }

    private static ExpeditionSettings.Stage Stage(string id, MissionSettings.Kind kind,
        MissionSettings.ModeFilter mode, int target, int reward)
    {
        return new ExpeditionSettings.Stage { id = id, kind = kind, mode = mode, target = target, reward = reward };
    }
}
#endif
