using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/TerraceSettings.asset (terrace milestone tuning and the on/off switch).
/// Idempotent: an existing asset is kept as tuned.
///
/// Menu: TamalStacker ▸ Game Feel ▸ Create Terrace Settings
/// </summary>
public static class TerraceSetup
{
    private const string SettingsPath = "Assets/Resources/" + TerraceSettings.ResourcePath + ".asset";

    [MenuItem("TamalStacker/Game Feel/Create Terrace Settings")]
    public static void CreateSettings()
    {
        EditorUtility.DisplayDialog("Terrace milestones", EnsureSettings(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateSettingsBatch()
    {
        EnsureSettings();
    }

    private static string EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<TerraceSettings>(SettingsPath) != null)
        {
            string kept = $"TerraceSettings already exists - kept as tuned ({SettingsPath}).";
            Debug.Log($"[TerraceSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TerraceSettings>(), SettingsPath);
        AssetDatabase.SaveAssets();
        string created = $"Created {SettingsPath}. Clear 'Enable Terraces' on it to switch the feature off.";
        Debug.Log($"[TerraceSetup] {created}");
        return created;
    }
}
