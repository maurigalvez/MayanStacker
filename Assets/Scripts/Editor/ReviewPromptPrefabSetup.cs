#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Resources/UI/ReviewPrompt.prefab — the editable face of the review
/// lead-in card that appears after a personal best or a three-star temple clear.
///
/// The card used to exist only as code, in flat placeholder colours. This writes a prefab
/// wired to <see cref="ReviewPromptView"/> and dressed in the house style: slab, button
/// sprites, tints and fonts are all borrowed from the hand-styled App Update prompt through
/// <see cref="UIHouseStyle"/>, so nothing needs assigning by hand. ReviewPromptView picks the
/// prefab up automatically at runtime.
///
/// Non-destructive: it refuses to overwrite an existing prefab unless you confirm, so
/// styling already done is never silently clobbered.
///
/// Menu: TamalStacker ▸ Retention ▸ Create Review Prompt Prefab
/// </summary>
public static class ReviewPromptPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/UI/ReviewPrompt.prefab";

    [MenuItem("TamalStacker/Retention/Create Review Prompt Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Review Prompt Prefab",
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

        EditorUtility.DisplayDialog("Review Prompt Prefab",
            "Created " + PrefabPath + ".\n\n" + styleNote + "\n\n" +
            "Everything is left visible for editing — the view hides it at runtime. The copy " +
            "shown is only a preview; the real text comes from the localization table.\n\n" +
            "Delete the prefab to go back to the code-built layout.", "OK");
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("ReviewPrompt",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ReviewPromptView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ReviewPromptView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // ── Backdrop ── blocks the game-over panel's Try Again behind the card.
        var backdropRect = CreateChild("Backdrop", root.transform);
        Stretch(backdropRect);
        var backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = RunOverlayUI.Backdrop;
        backdrop.raycastTarget = true;
        UIHouseStyle.ApplyImage(backdrop, UIHouseStyle.Backdrop);

        // ── Modal ──
        var modal = CreateChild("Modal", root.transform);
        Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
        UIHouseStyle.ApplyRect(modal, UIHouseStyle.Modal);
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);

        var title = CreateLabel("Title", modal, "Help Others Find the Temple?", 52f, RunOverlayUI.Gold);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(800f, 120f));
        UIHouseStyle.ApplyRect(title.rectTransform, UIHouseStyle.Title);
        UIHouseStyle.ApplyLabel(title, UIHouseStyle.Title);

        var body = CreateLabel("Body", modal,
            "A word on the Play Store is how new stackers find this place. It opens right here — your run stays where it is.",
            32f, RunOverlayUI.Parchment);
        Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(800f, 220f));
        UIHouseStyle.ApplyRect(body.rectTransform, UIHouseStyle.Body);
        UIHouseStyle.ApplyLabel(body, UIHouseStyle.Body);

        // "Not now" stays the same size as "Sure" — it ends the ask for good, and that button
        // should not be the small one the player has to hunt for.
        var confirm = CreateButton("Confirm", modal, "Of Course", RunOverlayUI.Jade, out var confirmLabel);
        Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(220f, 100f), new Vector2(400f, 110f));
        UIHouseStyle.ApplyRect((RectTransform)confirm.transform, UIHouseStyle.Primary);
        UIHouseStyle.ApplyButton(confirm, UIHouseStyle.Primary);

        var decline = CreateButton("Decline", modal, "Not Now", RunOverlayUI.Clay, out var declineLabel);
        Place((RectTransform)decline.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 100f), new Vector2(400f, 110f));
        UIHouseStyle.ApplyRect((RectTransform)decline.transform, UIHouseStyle.Secondary);
        UIHouseStyle.ApplyButton(decline, UIHouseStyle.Secondary);

        var so = new SerializedObject(root.GetComponent<ReviewPromptView>());
        so.FindProperty("backdrop").objectReferenceValue = backdrop;
        so.FindProperty("modal").objectReferenceValue = modal;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("bodyLabel").objectReferenceValue = body;
        so.FindProperty("confirmButton").objectReferenceValue = confirm;
        so.FindProperty("confirmLabel").objectReferenceValue = confirmLabel;
        so.FindProperty("declineButton").objectReferenceValue = decline;
        so.FindProperty("declineLabel").objectReferenceValue = declineLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
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

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float size, Color color)
    {
        var rt = CreateChild(name, parent);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = true;

        // The game's own font, then the CJK switcher — in that order, because the switcher
        // caches whatever font it finds as the label's "original" when it wakes up.
        if (RunOverlayUI.DisplayFont != null) label.font = RunOverlayUI.DisplayFont;
        else if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        rt.gameObject.AddComponent<LocaleFontSwitcher>();

        return label;
    }

    private static Button CreateButton(string name, Transform parent, string text, Color background,
        out TextMeshProUGUI label)
    {
        var rt = CreateChild(name, parent);

        var image = rt.gameObject.AddComponent<Image>();
        image.color = background;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        label = CreateLabel("Label", rt, text, 34f, RunOverlayUI.Parchment);
        Stretch(label.rectTransform);

        return button;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;
    }
}
#endif
