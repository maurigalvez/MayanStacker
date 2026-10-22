using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/TempoSettings.asset, which switches on the swing's learner stones and
/// intra-run ramp. The swing does nothing different when the asset is missing, so this tool is
/// what turns the feature on; clear <c>enableTempo</c> on the asset to switch it back off.
///
/// Idempotent and non-destructive: an asset that already exists is left as tuned.
///
/// Menu: TamalStacker ▸ Tempo ▸ Create Settings Asset
/// </summary>
public static class TempoSetup
{
    private const string AssetPath = "Assets/Resources/" + TempoSettings.ResourcePath + ".asset";

    [MenuItem("TamalStacker/Tempo/Create Settings Asset")]
    public static void CreateSettingsAsset()
    {
        string message = CreateIfMissing();
        EditorUtility.DisplayDialog("Tempo", message, "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI (<c>-executeMethod TempoSetup.CreateSettingsAssetBatch</c>).</summary>
    public static void CreateSettingsAssetBatch()
    {
        CreateIfMissing();
    }

    private static string CreateIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<TempoSettings>(AssetPath) != null)
        {
            string kept = $"TempoSettings already exists - kept as tuned ({AssetPath}).";
            Debug.Log($"[TempoSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var asset = ScriptableObject.CreateInstance<TempoSettings>();
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string created = $"Created {AssetPath}. Clear 'Enable Tempo' on it to switch the feature off.";
        Debug.Log($"[TempoSetup] {created}");
        return created;
    }
}
