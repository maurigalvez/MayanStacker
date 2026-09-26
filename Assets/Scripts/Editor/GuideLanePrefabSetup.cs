#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Resources/UI/GuideLane.prefab — the editable face of the
/// <see cref="GuideLane"/>, the low strip every lesson in a run speaks through.
///
/// Built from the same hierarchy the lane falls back to in code
/// (<see cref="GuideLaneView.BuildDefault"/>), then dressed in the house style through
/// <see cref="UIHouseStyle"/>: the stone slab, and the title and body type of the App Update
/// prompt. The slab is kept see-through and the type sized for a caption, not a modal —
/// this sits over live gameplay.
///
/// Non-destructive: it refuses to overwrite an existing prefab unless you confirm.
///
/// Menu: TamalStacker ▸ UI ▸ Create Guide Lane Prefab
/// </summary>
public static class GuideLanePrefabSetup
{
    private const string PrefabPath = "Assets/Resources/UI/GuideLane.prefab";

    [MenuItem("TamalStacker/UI/Create Guide Lane Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Guide Lane Prefab",
                PrefabPath + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                "Replace", "Cancel"))
        {
            return;
        }

        EnsureResourcesSubfolder("UI");

        GameObject root = BuildHierarchy();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        string styleNote = UIHouseStyle.HasReference
            ? "Styled from " + UIHouseStyle.ReferencePrefabPath + "."
            : UIHouseStyle.ReferencePrefabPath + " was not found, so the MainMenu_UI sheet defaults were used.";

        EditorUtility.DisplayDialog("Guide Lane Prefab",
            "Created " + PrefabPath + ".\n\n" + styleNote + "\n\n" +
            "Keep the strip low and narrow: it sits under the top of the tower and above the " +
            "power button, and its ends stay clear of the Tremor meter. The copy shown is only " +
            "a preview.\n\n" +
            "Delete the prefab to go back to the code-built strip.", "OK");
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("GuideLane", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // No GraphicRaycaster: a lesson must never swallow the tap that drops a stone.
        canvas.sortingOrder = GuideLane.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        GuideLaneView view = GuideLaneView.BuildDefault(root, out Image strip);

        // House look, then the lane's own constraints on top of it.
        UIHouseStyle.ApplyImage(strip, UIHouseStyle.Modal);
        strip.raycastTarget = false;
        Color tint = strip.color;
        strip.color = new Color(tint.r, tint.g, tint.b, tint.a * GuideLaneView.StripOpacity);

        UIHouseStyle.ApplyLabel(view.TitleLabel, UIHouseStyle.Title);
        UIHouseStyle.ApplyLabel(view.BodyLabel, UIHouseStyle.Body);
        FitCaption(view.TitleLabel, GuideLaneView.TitleFontSize);
        FitCaption(view.BodyLabel, GuideLaneView.BodyFontSize);

        return root;
    }

    /// <summary>
    /// The reference type is sized for a modal card; a caption over gameplay needs to be
    /// smaller, and centred, and allowed to shrink for a long translation.
    /// </summary>
    private static void FitCaption(TMPro.TextMeshProUGUI label, float size)
    {
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMax = size;
        label.fontSizeMin = size * 0.7f;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        label.margin = Vector4.zero;
        label.raycastTarget = false;
    }

    private static void EnsureResourcesSubfolder(string child)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources/" + child))
        {
            AssetDatabase.CreateFolder("Assets/Resources", child);
        }
    }
}
#endif
