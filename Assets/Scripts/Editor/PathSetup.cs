#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/PathSettings.asset with the 30-tier Path of Kukulkan. Idempotent: an existing
/// asset is kept, so tuning survives a re-run.
///
/// Menu: TamalStacker ▸ Economy ▸ Create Path Settings
/// </summary>
public static class PathSetup
{
    private const string SettingsPath = "Assets/Resources/" + PathSettings.ResourcePath + ".asset";

    [MenuItem("TamalStacker/Economy/Create Path Settings")]
    public static void CreateAssets()
    {
        EditorUtility.DisplayDialog("Path of Kukulkan", Ensure(), "OK");
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
        if (AssetDatabase.LoadAssetAtPath<PathSettings>(SettingsPath) != null)
        {
            note = $"Kept {SettingsPath}.";
        }
        else
        {
            var s = ScriptableObject.CreateInstance<PathSettings>();
            s.tiers = DefaultTiers();
            AssetDatabase.CreateAsset(s, SettingsPath);
            note = $"Created {SettingsPath} with {s.tiers.Length} tiers.";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PathSetup] {note}");
        return note;
    }

    // Proposals (Q1). 10 jade a tier, 25 on every fifth, 60 at the top; the 10th and 20th tiers
    // also hand over a cosmetic from the market. The 30th tier's exclusive stone skin is art-gated
    // (Q6), so it pays jade only for now.
    private static PathSettings.Tier[] DefaultTiers()
    {
        var tiers = new PathSettings.Tier[30];
        for (int i = 0; i < tiers.Length; i++)
        {
            int tier = i + 1;
            tiers[i].jade = tier == 30 ? 60 : tier % 5 == 0 ? 25 : 10;
        }
        tiers[9].cosmeticId = "burst_jade";
        tiers[19].cosmeticId = "shift_jade";
        return tiers;
    }
}
#endif
