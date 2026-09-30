#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hierarchy helpers shared by the Daily Challenge prefab generators (Ritual Briefing, Hot
/// Stone Timer). Same building blocks as the other generators, plus top-anchored rows that
/// stretch with the modal, because the briefing has to fit a 20:9 phone as well as 16:9.
/// Looks come from <see cref="UIHouseStyle"/>; only layout is decided here.
/// </summary>
internal static class DailyUIBuilder
{
    public static GameObject CreateCanvasRoot(string name, int sortingOrder, bool interactive, params System.Type[] extra)
    {
        var types = new System.Collections.Generic.List<System.Type> { typeof(Canvas), typeof(CanvasScaler) };
        if (interactive) types.Add(typeof(GraphicRaycaster));
        types.AddRange(extra);

        var root = new GameObject(name, types.ToArray());

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        return root;
    }

    public static RectTransform CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    /// <summary>A label styled as <paramref name="role"/>, then sized for its slot.</summary>
    public static TextMeshProUGUI Label(string name, Transform parent, string role, string text,
        float size, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var rt = CreateChild(name, parent);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.color = role == UIHouseStyle.Title ? RunOverlayUI.Gold : RunOverlayUI.Parchment;
        if (RunOverlayUI.DisplayFont != null) label.font = RunOverlayUI.DisplayFont;
        else if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;

        UIHouseStyle.ApplyLabel(label, role);

        // The borrowed look, this slot's size. Auto-size down so long translations still fit.
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMax = size;
        label.fontSizeMin = Mathf.Round(size * 0.6f);
        label.alignment = alignment;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;

        rt.gameObject.AddComponent<LocaleFontSwitcher>();
        return label;
    }

    public static Button StyledButton(string name, Transform parent, string role, string text, out TextMeshProUGUI label)
    {
        var rt = CreateChild(name, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = role == UIHouseStyle.Primary ? RunOverlayUI.Jade : RunOverlayUI.Clay;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var labelRt = CreateChild("Label", rt);
        label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 40f;
        label.color = RunOverlayUI.Parchment;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (RunOverlayUI.DisplayFont != null) label.font = RunOverlayUI.DisplayFont;
        Stretch(labelRt);
        labelRt.gameObject.AddComponent<LocaleFontSwitcher>();

        UIHouseStyle.ApplyButton(button, role);
        return button;
    }

    /// <summary>
    /// A row across the top of <paramref name="rt"/>'s parent: <paramref name="top"/> px down
    /// from the top edge, <paramref name="height"/> tall, inset <paramref name="padX"/> each side.
    /// </summary>
    public static void TopRow(RectTransform rt, float top, float height, float padX)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(padX, -top - height);
        rt.offsetMax = new Vector2(-padX, -top);
        rt.localScale = Vector3.one;
    }

    public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }

    /// <summary>True when it's fine to write <paramref name="path"/> (new, or the user said Replace).</summary>
    public static bool ConfirmReplace(string title, string path)
    {
        return AssetDatabase.LoadAssetAtPath<Object>(path) == null ||
               EditorUtility.DisplayDialog(title,
                   path + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                   "Replace", "Cancel");
    }

    public static void SavePrefab(GameObject root, string path)
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    public static string StyleNote()
    {
        return UIHouseStyle.HasReference
            ? "Styled from " + UIHouseStyle.ReferencePrefabPath + "."
            : UIHouseStyle.ReferencePrefabPath + " was not found, so the MainMenu_UI sheet defaults were used.";
    }
}
#endif
