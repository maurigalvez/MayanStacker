#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the three Resources assets that switch on the run-content features:
///   - BlockVarietyTable — the weighted mix of special blocks
///   - AltitudeBandSet   — the named stretches of a tall Infinite run
///   - BoonSettings      — how often Kukulkan offers a choice
///
/// All three systems load their asset from Resources and do nothing at all when it's
/// missing, so the features ship off and this tool is what turns them on. Nothing else
/// needs wiring: no scene, prefab or UIManager field is touched by any of it.
///
/// Idempotent and non-destructive, matching DailyChallengeUISetup and FtueUISetup: an
/// asset that already exists is left exactly as the designer tuned it.
///
/// Menu: TamalStacker ▸ Run Content ▸ Create Missing Assets
/// </summary>
public static class RunContentSetup
{
    private const string ResourcesDir = "Assets/Resources";

    [MenuItem("TamalStacker/Run Content/Create Missing Assets")]
    public static void CreateMissingAssets()
    {
        EnsureResourcesFolder();

        var created = new List<string>();
        var skipped = new List<string>();

        CreateIfMissing<BlockVarietyTable>(BlockVarietyTable.ResourcePath, table => table.SeedDefaults(),
            created, skipped);

        CreateIfMissing<AltitudeBandSet>(AltitudeBandSet.ResourcePath, set => set.SeedDefaults(),
            created, skipped);

        // BoonSettings needs no seeding — its field defaults are the intended tuning.
        CreateIfMissing<BoonSettings>(BoonSettings.ResourcePath, null, created, skipped);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message;
        if (created.Count == 0)
        {
            message = $"Everything already exists - no changes made ({skipped.Count} asset(s) left untouched).";
        }
        else
        {
            message = $"Created {created.Count} asset(s):\n  {string.Join("\n  ", created)}\n\n" +
                      (skipped.Count > 0 ? $"Left {skipped.Count} existing asset(s) untouched.\n\n" : "") +
                      "Tune them in Assets/Resources. Each has a master switch at the top if you " +
                      "want to disable that feature without deleting the asset.\n\n" +
                      "Note: altitude bands drive the time of day, so leave StyleManager's " +
                      "'Auto Cycle By Height' OFF or the two will fight.";
        }

        Debug.Log($"[RunContentSetup] {message}");
        EditorUtility.DisplayDialog("Run Content", message, "OK");
    }

    /// <summary>
    /// Deletes the three assets, which switches every run-content feature back off and
    /// returns the game to its original rules. Confirmation required — this throws away
    /// any tuning done on them.
    /// </summary>
    [MenuItem("TamalStacker/Run Content/Delete Assets (Disable Features)")]
    public static void DeleteAssets()
    {
        if (!EditorUtility.DisplayDialog("Delete Run Content Assets",
            "Delete BlockVarietyTable, AltitudeBandSet and BoonSettings?\n\n" +
            "Block variety, altitude bands and boons all switch off and the game returns to " +
            "its original rules. Any tuning on these assets is lost.\n\n" +
            "To disable a single feature without losing tuning, clear its master switch instead.",
            "Delete", "Cancel"))
        {
            return;
        }

        int deleted = 0;
        foreach (string resourcePath in new[]
                 {
                     BlockVarietyTable.ResourcePath,
                     AltitudeBandSet.ResourcePath,
                     BoonSettings.ResourcePath
                 })
        {
            string assetPath = $"{ResourcesDir}/{resourcePath}.asset";
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) != null
                && AssetDatabase.DeleteAsset(assetPath))
            {
                deleted++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[RunContentSetup] Deleted {deleted} asset(s); run-content features are off.");
    }

    private static void CreateIfMissing<T>(string resourcePath, System.Action<T> seed,
        List<string> created, List<string> skipped) where T : ScriptableObject
    {
        string assetPath = $"{ResourcesDir}/{resourcePath}.asset";

        if (AssetDatabase.LoadAssetAtPath<T>(assetPath) != null)
        {
            skipped.Add(assetPath);
            return;
        }

        var asset = ScriptableObject.CreateInstance<T>();
        seed?.Invoke(asset);

        AssetDatabase.CreateAsset(asset, assetPath);
        created.Add(assetPath);
    }

    private static void EnsureResourcesFolder()
    {
        if (!Directory.Exists(ResourcesDir))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }
}
#endif
