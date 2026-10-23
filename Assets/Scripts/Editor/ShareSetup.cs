#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the shareable result:
///   • Resources/ShareSettings.asset - switch + thresholds + store link. Kept if it exists.
///   • Resources/UI/ShareResult.prefab - the Share chip (<see cref="ShareResultView"/>), styled
///     through <see cref="UIHouseStyle"/>. Kept if it exists, so hand styling survives a re-run.
///
/// Menu: TamalStacker ▸ UI ▸ Create Share Result Assets
/// </summary>
public static class ShareSetup
{
    private const string SettingsPath = "Assets/Resources/" + ShareSettings.ResourcePath + ".asset";
    private const string PrefabPath = "Assets/Resources/UI/ShareResult.prefab";

    [MenuItem("TamalStacker/UI/Create Share Result Assets")]
    public static void CreateAssets()
    {
        EditorUtility.DisplayDialog("Share result", Ensure(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateAssetsBatch()
    {
        Ensure();
    }

    private static string Ensure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");

        string note = string.Empty;
        if (AssetDatabase.LoadAssetAtPath<ShareSettings>(SettingsPath) != null)
        {
            note += $"Kept {SettingsPath}. ";
        }
        else
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ShareSettings>(), SettingsPath);
            note += $"Created {SettingsPath}. ";
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            note += $"Kept {PrefabPath}.";
        }
        else
        {
            GameObject root = BuildChip();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            note += $"Created {PrefabPath}.";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ShareSetup] {note}");
        return note;
    }

    private static GameObject BuildChip()
    {
        var root = new GameObject("ShareResult",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ShareResultView));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ShareResultView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // The chip is the only raycast target: the rest of the canvas lets taps through.
        var chip = RunOverlayUI.CreateChild("Chip", root.transform);
        RunOverlayUI.Place(chip, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(420f, 110f));
        var image = chip.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(image, UIHouseStyle.Secondary);
        var button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UIHouseStyle.ApplyButton(button, UIHouseStyle.Secondary);

        var label = RunOverlayUI.CreateLabel("Label", chip, "Share", 40f, RunOverlayUI.Parchment);
        UIHouseStyle.ApplyLabel(label, UIHouseStyle.Body);
        label.color = RunOverlayUI.Parchment;
        label.fontSize = 40f;
        RunOverlayUI.Stretch(label.rectTransform);

        var so = new SerializedObject(root.GetComponent<ShareResultView>());
        so.FindProperty("chip").objectReferenceValue = chip;
        so.FindProperty("chipButton").objectReferenceValue = button;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }
}
#endif
