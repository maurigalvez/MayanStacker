using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the two assets Swing Moods needs: Resources/SwingMoodSettings.asset (tuning and the
/// on/off switch) and Resources/Fx/SwingMoodFx.prefab (the glow / Echo ghost sprite).
///
/// Idempotent and non-destructive: an asset that already exists is left as tuned or styled, so
/// the user can swap the prefab's sprite for real art and re-run this safely.
///
/// Menu: TamalStacker ▸ Swing Moods ▸ Create Assets
/// </summary>
public static class SwingMoodSetup
{
    private const string SettingsPath = "Assets/Resources/" + SwingMoodSettings.ResourcePath + ".asset";
    private const string PrefabPath = "Assets/Resources/" + SwingMoodFx.ResourcePath + ".prefab";

    [MenuItem("TamalStacker/Swing Moods/Create Assets")]
    public static void CreateAssets()
    {
        string message = CreateIfMissing();
        EditorUtility.DisplayDialog("Swing Moods", message, "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI (<c>-executeMethod SwingMoodSetup.CreateAssetsBatch</c>).</summary>
    public static void CreateAssetsBatch()
    {
        CreateIfMissing();
    }

    private static string CreateIfMissing()
    {
        string settingsLine = EnsureSettings();
        string prefabLine = EnsurePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return settingsLine + "\n" + prefabLine;
    }

    private static string EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<SwingMoodSettings>(SettingsPath) != null)
        {
            string kept = $"SwingMoodSettings already exists - kept as tuned ({SettingsPath}).";
            Debug.Log($"[SwingMoodSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SwingMoodSettings>(), SettingsPath);
        string created = $"Created {SettingsPath}. Clear 'Enable Moods' on it to switch the feature off.";
        Debug.Log($"[SwingMoodSetup] {created}");
        return created;
    }

    private static string EnsurePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            string kept = $"SwingMoodFx prefab already exists - kept as styled ({PrefabPath}).";
            Debug.Log($"[SwingMoodSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Fx"))
            AssetDatabase.CreateFolder("Assets/Resources", "Fx");

        var root = new GameObject("SwingMoodFx");
        var renderer = root.AddComponent<SpriteRenderer>();
        // Soft round placeholder from Unity's built-ins; swap it for authored glow art on the prefab.
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        renderer.color = new Color(1f, 1f, 1f, 0f);
        var fx = root.AddComponent<SwingMoodFx>();
        var so = new SerializedObject(fx);
        so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        if (saved == null)
        {
            const string failed = "Could not save the SwingMoodFx prefab.";
            Debug.LogError($"[SwingMoodSetup] {failed}");
            return failed;
        }

        string created = $"Created {PrefabPath}.";
        Debug.Log($"[SwingMoodSetup] {created}");
        return created;
    }
}
