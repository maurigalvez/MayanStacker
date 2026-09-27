#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Resources/UI/BraceButton.prefab — the editable face of Cabracán's BRACE
/// button (<see cref="BraceButtonView"/>).
///
/// Built from the same hierarchy the quake falls back to in code
/// (<see cref="BraceButtonView.BuildDefault"/>), inside a 1080×1920 canvas so the button can be
/// placed against the real screen in prefab mode. Move it by moving the BraceButton object;
/// restyle the disc, ring, icon and labels freely. Only the BraceButtonView's Hold Target is
/// required.
///
/// Non-destructive: it refuses to overwrite an existing prefab unless you confirm.
///
/// Menu: TamalStacker ▸ Temples ▸ Create Brace Button Prefab
/// </summary>
public static class BraceButtonPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/" + BraceButtonView.PrefabResourcePath + ".prefab";
    private const string DialogTitle = "Brace Button Prefab";

    [MenuItem("TamalStacker/Temples/Create Brace Button Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog(DialogTitle,
                PrefabPath + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                "Replace", "Cancel"))
        {
            return;
        }

        TempleRulesSetup.EnsureFolder("Assets/Resources/UI");

        GameObject root = BuildHierarchy();
        try
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
            {
                Debug.LogError("[BraceButtonPrefabSetup] Failed to save " + PrefabPath);
                return;
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        EditorUtility.DisplayDialog(DialogTitle,
            "Created " + PrefabPath + ".\n\n" +
            "Open it and move the BraceButton object to change where it appears (the canvas is " +
            "1080×1920, the same as the game). Keep it off the tower and clear of the power " +
            "button bottom-left and the Tremor meter on the right.\n\n" +
            "Colours, pulse and the optional parts are on the BraceButtonView component. The " +
            "\"2.4\" and \"BRACE\" text are only a preview.\n\n" +
            "Delete the prefab to go back to the code-built button.", "OK");
    }

    [MenuItem("TamalStacker/Temples/Delete Brace Button Prefab")]
    public static void DeletePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            EditorUtility.DisplayDialog(DialogTitle, "There is no " + PrefabPath + " to delete.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(DialogTitle,
                "Delete " + PrefabPath + "?\n\nAny styling done to it will be lost. The game falls " +
                "back to the code-built button.", "Delete", "Cancel"))
        {
            return;
        }

        AssetDatabase.DeleteAsset(PrefabPath);
        AssetDatabase.Refresh();
    }

    private static GameObject BuildHierarchy()
    {
        // A canvas of its own so prefab mode shows the real screen. At runtime it nests under
        // the quake's canvas: sorting, scaling and fading come from there, and the raycaster
        // here is what lets the nested button take the press.
        var root = new GameObject("BraceButtonCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        BraceButtonView view = BraceButtonView.BuildDefault(root.transform);

        // Preview the rule's hold icon when it's wired; otherwise keep the empty slot hidden
        // (the game fills it from the rule art at runtime).
        Image icon = view.transform.Find("Icon")?.GetComponent<Image>();
        if (icon != null)
        {
            Sprite ruleIcon = TempleRuleArt.Current != null ? TempleRuleArt.Current.quakeHoldIcon : null;
            icon.sprite = ruleIcon;
            icon.enabled = ruleIcon != null;
        }

        return root;
    }
}
#endif
