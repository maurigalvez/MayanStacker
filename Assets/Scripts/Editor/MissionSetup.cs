#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/MissionSettings.asset with the launch mission pool. Idempotent: an existing
/// asset is kept, so tuning survives a re-run.
///
/// Menu: TamalStacker ▸ Economy ▸ Create Mission Settings
/// </summary>
public static class MissionSetup
{
    private const string SettingsPath = "Assets/Resources/" + MissionSettings.ResourcePath + ".asset";

    [MenuItem("TamalStacker/Economy/Create Mission Settings")]
    public static void CreateAssets()
    {
        EditorUtility.DisplayDialog("Missions", Ensure(), "OK");
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
        if (AssetDatabase.LoadAssetAtPath<MissionSettings>(SettingsPath) != null)
        {
            note = $"Kept {SettingsPath}.";
        }
        else
        {
            var s = ScriptableObject.CreateInstance<MissionSettings>();
            s.pool = DefaultPool();
            AssetDatabase.CreateAsset(s, SettingsPath);
            note = $"Created {SettingsPath} with {s.pool.Length} missions.";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MissionSetup] {note}");
        return note;
    }

    private static MissionSettings.Template T(string id, MissionSettings.Tier tier, MissionSettings.Kind kind,
        MissionSettings.ModeFilter mode, int target, int reward)
    {
        return new MissionSettings.Template { id = id, tier = tier, kind = kind, mode = mode, target = target, reward = reward };
    }

    // Rewards are ~1/3 of a typical ~25 jade run for easy goals, up to roughly a full run for hard
    // ones, so a day of three missions is worth about two runs of jade.
    private static MissionSettings.Template[] DefaultPool()
    {
        var E = MissionSettings.Tier.Easy;
        var M = MissionSettings.Tier.Medium;
        var H = MissionSettings.Tier.Hard;
        var K = MissionSettings.Kind.Runs;
        var any = MissionSettings.ModeFilter.Any;

        return new[]
        {
            T("runs_3", E, K, any, 3, 8),
            T("stones_15", E, MissionSettings.Kind.StonesInRun, any, 15, 8),
            T("perfects_3", E, MissionSettings.Kind.PerfectsInRun, any, 3, 8),
            T("temple_runs_2", E, K, MissionSettings.ModeFilter.Temples, 2, 8),

            T("perfects_6", M, MissionSettings.Kind.PerfectsInRun, any, 6, 15),
            T("combo_4", M, MissionSettings.Kind.ComboInRun, any, 4, 15),
            T("stones_25_infinite", M, MissionSettings.Kind.StonesInRun, MissionSettings.ModeFilter.Infinite, 25, 15),
            T("perfects_total_15", M, MissionSettings.Kind.PerfectsTotal, any, 15, 15),
            T("stones_20_temple", M, MissionSettings.Kind.StonesInRun, MissionSettings.ModeFilter.Temples, 20, 15),

            T("stones_40_infinite", H, MissionSettings.Kind.StonesInRun, MissionSettings.ModeFilter.Infinite, 40, 25),
            T("perfects_10", H, MissionSettings.Kind.PerfectsInRun, any, 10, 25),
            T("surge_1", H, MissionSettings.Kind.SunSurgesTotal, any, 1, 25),
            T("edge_3", H, MissionSettings.Kind.EdgeLandingsTotal, any, 3, 25),
            T("daily_run", H, K, MissionSettings.ModeFilter.Daily, 1, 25),
        };
    }
}
#endif
