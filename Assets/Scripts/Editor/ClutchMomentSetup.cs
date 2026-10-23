using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/ClutchMomentSettings.asset (near-miss and clutch moment tuning and the on/off switch).
/// Idempotent: an existing asset is kept as tuned.
///
/// Menu: TamalStacker ▸ Game Feel ▸ Create Clutch Moment Settings
/// </summary>
public static class ClutchMomentSetup
{
    private const string SettingsPath = "Assets/Resources/" + ClutchMomentSettings.ResourcePath + ".asset";

    [MenuItem("TamalStacker/Game Feel/Create Clutch Moment Settings")]
    public static void CreateSettings()
    {
        EditorUtility.DisplayDialog("Clutch moments", EnsureSettings(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateSettingsBatch()
    {
        EnsureSettings();
    }

    private static string EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<ClutchMomentSettings>(SettingsPath) != null)
        {
            string kept = $"ClutchMomentSettings already exists - kept as tuned ({SettingsPath}).";
            Debug.Log($"[ClutchMomentSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ClutchMomentSettings>(), SettingsPath);
        AssetDatabase.SaveAssets();
        string created = $"Created {SettingsPath}. Clear 'Enable Moments' on it to switch the feature off.";
        Debug.Log($"[ClutchMomentSetup] {created}");
        return created;
    }
}
