#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates the editable versions of the two boon surfaces:
///   - Assets/Resources/UI/BoonPicker.prefab — the three-card choice
///   - Assets/Resources/UI/BoonIntro.prefab  — the one-time explainer before the first offer
///
/// Both existed only as code, so they were the last screens that couldn't be restyled with
/// the rest of the UI. This writes out prefabs reproducing the code-built layouts, wired to
/// <see cref="BoonPickerView"/> / <see cref="BoonIntroView"/>; from there they are ordinary
/// prefabs to open and redress, and <see cref="BoonSystem"/> picks them up automatically.
///
/// Non-destructive, matching <see cref="LanguageSelectPrefabSetup"/>: it refuses to
/// overwrite an existing prefab unless you confirm, so styling already done is never
/// silently clobbered.
///
/// Menu: TamalStacker ▸ Run Content ▸ Create Boon UI Prefabs
/// </summary>
public static class BoonUISetup
{
    private const string PickerPath = "Assets/Resources/UI/BoonPicker.prefab";
    private const string IntroPath = "Assets/Resources/UI/BoonIntro.prefab";

    // Same Mayan temple palette RunOverlayUI uses, so a generated prefab is a faithful
    // starting point rather than a different-looking placeholder.
    private static readonly Color Backdrop = new Color(0.06f, 0.05f, 0.04f, 0.92f);
    private static readonly Color Stone = new Color(0.13f, 0.16f, 0.15f, 1f);
    private static readonly Color Jade = new Color(0.09f, 0.42f, 0.34f, 1f);
    private static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    private static readonly Color Parchment = new Color(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Color Muted = new Color(0.72f, 0.68f, 0.58f, 1f);

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    // Matches BoonSystem's code-built card metrics, so switching between the two layouts
    // is not also a change of proportions.
    private const float CardWidth = 820f;
    private const float CardHeight = 240f;
    private const float CardGap = 30f;

    [MenuItem("TamalStacker/Run Content/Create Boon UI Prefabs")]
    public static void CreatePrefabs()
    {
        var written = new List<string>();

        if (ShouldWrite(PickerPath, "Boon picker"))
        {
            EnsureResourcesSubfolder("UI");
            Save(BuildPicker(), PickerPath);
            written.Add(PickerPath);
        }

        if (ShouldWrite(IntroPath, "Boon intro"))
        {
            EnsureResourcesSubfolder("UI");
            Save(BuildIntro(), IntroPath);
            written.Add(IntroPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (written.Count == 0)
        {
            EditorUtility.DisplayDialog("Boon UI", "Nothing was written.", "OK");
            return;
        }

        var picker = AssetDatabase.LoadAssetAtPath<GameObject>(PickerPath);
        if (picker != null)
        {
            Selection.activeObject = picker;
            EditorGUIUtility.PingObject(picker);
        }

        EditorUtility.DisplayDialog("Boon UI",
            "Created:\n  " + string.Join("\n  ", written) + "\n\n" +
            "Open them to restyle. The three cards are cloned from CardTemplate at runtime, " +
            "so styling that one card styles them all — and its Name label is re-tinted per " +
            "boon with that blessing's accent colour.\n\n" +
            "All copy is overwritten from localization at runtime; the text authored in the " +
            "prefab is only there so you can see what you are laying out.\n\n" +
            "Delete a prefab to go back to that surface's code-built layout.", "OK");
    }

    [MenuItem("TamalStacker/Run Content/Delete Boon UI Prefabs")]
    public static void DeletePrefabs()
    {
        if (!EditorUtility.DisplayDialog("Boon UI",
                "Delete the boon picker and intro prefabs?\n\n" +
                "Both surfaces go back to their code-built layouts. Any styling done to the " +
                "prefabs is lost.",
                "Delete", "Cancel"))
        {
            return;
        }

        var deleted = new List<string>();
        foreach (string path in new[] { PickerPath, IntroPath })
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null && AssetDatabase.DeleteAsset(path))
            {
                deleted.Add(path);
            }
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Boon UI",
            deleted.Count == 0
                ? "Nothing to delete - neither prefab exists."
                : "Deleted:\n  " + string.Join("\n  ", deleted),
            "OK");
    }

    // ---- Picker ----

    private static GameObject BuildPicker()
    {
        GameObject root = CreateScreen("BoonPicker");
        var view = root.AddComponent<BoonPickerView>();

        var title = CreateLabel("Title", root.transform, "Kukulkan Offers", 60f, Gold);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(900f, 90f));

        var subtitle = CreateLabel("Subtitle", root.transform, "Choose one blessing.", 28f, Muted);
        Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(900f, 60f));

        // A layout group positions the cards here, instead of the hand-computed offsets the
        // code path uses — one less thing to re-derive when the cards are restyled. Sized
        // for the default three offers; the group re-centres itself if that changes.
        const int defaultOfferCount = 3;
        float stackHeight = defaultOfferCount * CardHeight + (defaultOfferCount - 1) * CardGap;

        var cards = CreateChild("Cards", root.transform);
        Place(cards, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, stackHeight));

        var layout = cards.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = CardGap;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        Button template = BuildCardTemplate(cards);

        var so = new SerializedObject(view);
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subtitleText").objectReferenceValue = subtitle;
        so.FindProperty("optionsContainer").objectReferenceValue = cards;
        so.FindProperty("optionTemplate").objectReferenceValue = template;
        so.FindProperty("nameChild").stringValue = "Name";
        so.FindProperty("descriptionChild").stringValue = "Description";
        so.FindProperty("tintNameWithAccent").boolValue = true;
        so.FindProperty("accentGraphicChild").stringValue = "Accent";
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    /// <summary>
    /// The one card every offer is cloned from. Child names here are the contract with
    /// <see cref="BoonPickerView"/> — rename one and update the matching field on the view.
    /// </summary>
    private static Button BuildCardTemplate(Transform parent)
    {
        var card = CreateChild("CardTemplate", parent);
        card.sizeDelta = new Vector2(CardWidth, CardHeight);

        var image = card.gameObject.AddComponent<Image>();
        image.color = Stone;

        var button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        // Tinted per boon at runtime, so the four blessings stay colour-coded even after
        // the card is restyled. Delete it and clear the view's Accent Graphic Child if the
        // styling doesn't want a stripe.
        // Stretched across the card rather than given a fixed width: the layout group
        // drives card width, so a hard-coded stripe would stop matching the moment the
        // container is resized.
        var accent = CreateChild("Accent", card);
        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.sizeDelta = new Vector2(0f, 10f);
        accent.anchoredPosition = Vector2.zero;
        accent.gameObject.AddComponent<Image>().color = Gold;

        var name = CreateLabel("Name", card, "Serpent's Favor", 44f, Gold);
        Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(760f, 60f));

        var description = CreateLabel("Description", card,
            "Double points for the next 5 stones.", 28f, Parchment);
        Place(description.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(740f, 110f));

        return button;
    }

    // ---- Intro ----

    private static GameObject BuildIntro()
    {
        GameObject root = CreateScreen("BoonIntro");
        var view = root.AddComponent<BoonIntroView>();

        var title = CreateLabel("Title", root.transform, "The Blessings of Kukulkan", 60f, Gold);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(900f, 90f));

        var body = CreateLabel("Body", root.transform,
            "Every few stones, Kukulkan halts your climb and offers three blessings. " +
            "Take one - a wider stone, doubled points, a temple set straight.",
            34f, Parchment);
        Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(820f, 420f));

        var note = CreateLabel("Note", root.transform,
            "The climb waits while you choose, and a warning appears before every offering.",
            28f, Muted);
        Place(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(820f, 120f));

        var continueRect = CreateChild("Continue", root.transform);
        Place(continueRect, new Vector2(0.5f, 0f), new Vector2(0f, 420f), new Vector2(560f, 140f));

        var continueImage = continueRect.gameObject.AddComponent<Image>();
        continueImage.color = Jade;

        var continueButton = continueRect.gameObject.AddComponent<Button>();
        continueButton.targetGraphic = continueImage;

        var continueLabel = CreateLabel("Label", continueRect, "I am ready", 34f, Parchment);
        Stretch(continueLabel.rectTransform);

        var so = new SerializedObject(view);
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("noteText").objectReferenceValue = note;
        so.FindProperty("continueButton").objectReferenceValue = continueButton;
        so.FindProperty("continueLabel").objectReferenceValue = continueLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    // ---- Shared ----

    /// <summary>
    /// A root carrying its own overlay canvas, matching the one RunOverlayUI builds. The
    /// prefab renders correctly wherever BoonSystem parents it.
    /// </summary>
    private static GameObject CreateScreen(string name)
    {
        var root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // Full-bleed backdrop, which also eats taps so the block underneath can't be dropped
        // through the picker.
        var backdrop = CreateChild("Backdrop", root.transform);
        Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = Backdrop;

        return root;
    }

    private static bool ShouldWrite(string path, string label)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return true;

        return EditorUtility.DisplayDialog(label,
            path + " already exists.\n\nReplace it? Any styling done to it will be lost.",
            "Replace", "Keep");
    }

    private static void Save(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
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

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, string text,
        float size, Color color)
    {
        var rt = CreateChild(name, parent);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;

        // Labels are decoration; never let them intercept a tap meant for the card.
        label.raycastTarget = false;

        return label;
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
