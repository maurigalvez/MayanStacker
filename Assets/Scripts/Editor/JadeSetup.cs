using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/JadeSettings.asset (jade earn rates and the on/off switch).
/// Idempotent: an existing asset is kept as tuned.
///
/// Menu: TamalStacker ▸ Economy ▸ Create Jade Settings
/// </summary>
public static class JadeSetup
{
    private const string SettingsPath = "Assets/Resources/" + JadeSettings.ResourcePath + ".asset";

    [MenuItem("TamalStacker/Economy/Create Jade Settings")]
    public static void CreateSettings()
    {
        EditorUtility.DisplayDialog("Jade wallet", EnsureSettings(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateSettingsBatch()
    {
        EnsureSettings();
    }

    private static string EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<JadeSettings>(SettingsPath) != null)
        {
            string kept = $"JadeSettings already exists - kept as tuned ({SettingsPath}).";
            Debug.Log($"[JadeSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<JadeSettings>(), SettingsPath);
        AssetDatabase.SaveAssets();
        string created = $"Created {SettingsPath}. Clear 'Enable Jade' on it to switch earning off.";
        Debug.Log($"[JadeSetup] {created}");
        return created;
    }
}
