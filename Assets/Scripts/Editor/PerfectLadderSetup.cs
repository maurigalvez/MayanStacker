using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/PerfectLadderSettings.asset, which switches on the Perfect ladder (marimba
/// scale chime + rising landing juice). Idempotent; an existing asset is kept as tuned.
///
/// Menu: TamalStacker ▸ Game Feel ▸ Create Perfect Ladder Settings
/// </summary>
public static class PerfectLadderSetup
{
    private const string AssetPath = "Assets/Resources/" + PerfectLadderSettings.ResourcePath + ".asset";

    [MenuItem("TamalStacker/Game Feel/Create Perfect Ladder Settings")]
    public static void CreateSettingsAsset()
    {
        EditorUtility.DisplayDialog("Perfect ladder", CreateIfMissing(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateSettingsAssetBatch()
    {
        CreateIfMissing();
    }

    private static string CreateIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<PerfectLadderSettings>(AssetPath) != null)
        {
            string kept = $"PerfectLadderSettings already exists - kept as tuned ({AssetPath}).";
            Debug.Log($"[PerfectLadderSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PerfectLadderSettings>(), AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string created = $"Created {AssetPath}. Clear 'Enable Ladder' on it to switch the feature off.";
        Debug.Log($"[PerfectLadderSetup] {created}");
        return created;
    }
}
