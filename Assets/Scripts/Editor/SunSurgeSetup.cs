using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the two assets Sun Surge needs: Resources/SunSurgeSettings.asset (tuning and the on/off
/// switch) and Resources/Fx/SunSurgeTrail.prefab (the gold trail that follows each swinging stone
/// during a surge). Idempotent and non-destructive: an existing asset is left as tuned or styled.
///
/// Menu: TamalStacker ▸ Game Feel ▸ Create Sun Surge Assets
/// </summary>
public static class SunSurgeSetup
{
    private const string SettingsPath = "Assets/Resources/" + SunSurgeSettings.ResourcePath + ".asset";
    private const string PrefabPath = "Assets/Resources/" + SunSurge.TrailResourcePath + ".prefab";

    [MenuItem("TamalStacker/Game Feel/Create Sun Surge Assets")]
    public static void CreateAssets()
    {
        EditorUtility.DisplayDialog("Sun Surge", CreateIfMissing(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateAssetsBatch()
    {
        CreateIfMissing();
    }

    private static string CreateIfMissing()
    {
        string a = EnsureSettings();
        string b = EnsurePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return a + "\n" + b;
    }

    private static string EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<SunSurgeSettings>(SettingsPath) != null)
        {
            string kept = $"SunSurgeSettings already exists - kept as tuned ({SettingsPath}).";
            Debug.Log($"[SunSurgeSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SunSurgeSettings>(), SettingsPath);
        string created = $"Created {SettingsPath}. Clear 'Enable Surge' on it to switch the feature off.";
        Debug.Log($"[SunSurgeSetup] {created}");
        return created;
    }

    private static string EnsurePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            string kept = $"SunSurgeTrail prefab already exists - kept as styled ({PrefabPath}).";
            Debug.Log($"[SunSurgeSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Fx"))
            AssetDatabase.CreateFolder("Assets/Resources", "Fx");

        var root = new GameObject("SunSurgeTrail");
        var trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.45f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 0f));
        // Sun-gold head fading to clay-orange, transparent at the tail.
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0f),
                new GradientColorKey(new Color(0.9f, 0.45f, 0.15f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;
        trail.sortingOrder = 5;
        trail.sharedMaterial = EnsureMaterial();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        if (saved == null)
        {
            const string failed = "Could not save the SunSurgeTrail prefab.";
            Debug.LogError($"[SunSurgeSetup] {failed}");
            return failed;
        }

        string created = $"Created {PrefabPath}.";
        Debug.Log($"[SunSurgeSetup] {created}");
        return created;
    }

    /// <summary>The trail's material must be an asset, or the prefab saves it as a missing reference.</summary>
    private static Material EnsureMaterial()
    {
        const string matPath = "Assets/Resources/Fx/SunSurgeTrail.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existing != null) return existing;

        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("[SunSurgeSetup] Sprites/Default shader not found; the trail will have no material.");
            return null;
        }
        AssetDatabase.CreateAsset(new Material(shader), matPath);
        return AssetDatabase.LoadAssetAtPath<Material>(matPath);
    }
}
