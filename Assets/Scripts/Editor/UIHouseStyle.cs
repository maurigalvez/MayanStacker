#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The house look for generated modal UI, shared by every prefab-setup tool.
///
/// Rather than hard-code sprites and tints into each generator (and leave every new prefab
/// grey until someone assigns them by hand), the look is read from one hand-styled reference
/// prefab — <see cref="ReferencePrefabPath"/>, the App Update prompt. Restyle that prefab and
/// every prefab generated afterwards follows it.
///
/// The reference exposes these named parts, and a generator asks for them by role:
///   Backdrop   — full-screen dimmer
///   Modal      — the stone slab the card sits on
///   Title      — gold heading
///   Body       — parchment copy
///   Primary    — the "yes" button (jade), with its Label
///   Secondary  — the "no" button (clay), with its Label
///
/// When the reference is missing a part, the fallback is the same sprites from the
/// MainMenu_UI sheet it was styled with, so a generated prefab is never plain grey boxes.
/// </summary>
public static class UIHouseStyle
{
    public const string ReferencePrefabPath = "Assets/Resources/UI/AppUpdatePrompt.prefab";

    public const string Backdrop = "Backdrop";
    public const string Modal = "Modal";
    public const string Title = "Title";
    public const string Body = "Body";
    public const string Primary = "Primary";
    public const string Secondary = "Secondary";

    private const string SheetPath = "Assets/Art/UI/MayaStacker_MainMenu_UI.png";
    private const string FallbackSlabSprite = "MayaStacker_MainMenu_UI_22";
    private const string FallbackButtonSprite = "MayaStacker_MainMenu_UI_0";

    // Tints the reference was styled with, used only when it can't be read.
    private static readonly Color FallbackPrimaryTint = new Color(0.259f, 1f, 0.824f, 1f);
    private static readonly Color FallbackSecondaryTint = new Color(0.792f, 0.300f, 0.191f, 1f);

    private static GameObject Reference => AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePrefabPath);

    /// <summary>True when the reference prefab exists; generators can warn when it doesn't.</summary>
    public static bool HasReference => Reference != null;

    /// <summary>Copies the reference part's anchors, pivot, position and size.</summary>
    public static void ApplyRect(RectTransform target, string role)
    {
        var source = Find(role) as RectTransform;
        if (source == null) return;
        CopyRect(source, target);
    }

    /// <summary>Styles an Image as the reference part (sprite, slicing, tint, material).</summary>
    public static void ApplyImage(Image target, string role)
    {
        Transform part = Find(role);
        var source = part != null ? part.GetComponent<Image>() : null;
        if (source != null)
        {
            bool raycast = target.raycastTarget;
            EditorUtility.CopySerialized(source, target);
            target.raycastTarget = raycast;
            return;
        }

        switch (role)
        {
            case Modal:
                target.sprite = LoadSheetSprite(FallbackSlabSprite);
                target.type = Image.Type.Sliced;
                target.color = Color.white;
                break;
            case Primary:
                target.sprite = LoadSheetSprite(FallbackButtonSprite);
                target.color = FallbackPrimaryTint;
                break;
            case Secondary:
                target.sprite = LoadSheetSprite(FallbackButtonSprite);
                target.color = FallbackSecondaryTint;
                break;
        }
    }

    /// <summary>
    /// Styles a label as the reference part — font, size, colour, auto-size, margins — and
    /// then puts back the text it had, since only the look is borrowed.
    /// </summary>
    public static void ApplyLabel(TextMeshProUGUI target, string role)
    {
        Transform part = Find(role);
        var source = part != null ? part.GetComponent<TextMeshProUGUI>() : null;
        if (source == null) return;

        string text = target.text;
        EditorUtility.CopySerialized(source, target);
        target.text = text;
        target.raycastTarget = false;
    }

    /// <summary>
    /// Styles a button, its image and its label as the reference part. Only the look is
    /// copied: the target graphic stays the button's own image and no listeners come across.
    /// </summary>
    public static void ApplyButton(Button target, string role)
    {
        var image = target.GetComponent<Image>();
        if (image != null) ApplyImage(image, role);

        Transform part = Find(role);
        var source = part != null ? part.GetComponent<Button>() : null;
        if (source != null)
        {
            target.transition = source.transition;
            target.colors = source.colors;
            target.spriteState = source.spriteState;
        }
        target.targetGraphic = image;

        var label = target.GetComponentInChildren<TextMeshProUGUI>(true);
        Transform sourceLabel = part != null ? part.Find("Label") : null;
        if (label != null && sourceLabel != null)
        {
            var sourceText = sourceLabel.GetComponent<TextMeshProUGUI>();
            if (sourceText != null)
            {
                string text = label.text;
                EditorUtility.CopySerialized(sourceText, label);
                label.text = text;
                label.raycastTarget = false;
            }
            CopyRect((RectTransform)sourceLabel, label.rectTransform);
        }
    }

    private static Transform Find(string role)
    {
        GameObject reference = Reference;
        if (reference == null) return null;
        return FindDeep(reference.transform, role);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition = source.anchoredPosition;
        target.localScale = source.localScale;
    }

    private static Sprite LoadSheetSprite(string spriteName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
        {
            if (asset is Sprite sprite && sprite.name == spriteName) return sprite;
        }
        return null;
    }
}
#endif
