#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Resources/UI/RemoveAdsOffer.prefab — the editable face of the offer card
/// Settings shows before Google Play's purchase sheet (price, one-time, what it removes).
///
/// Wired to <see cref="RemoveAdsOfferView"/> and dressed in the house style through
/// <see cref="UIHouseStyle"/> (slab, button sprites, tints and fonts borrowed from the
/// hand-styled App Update prompt), so nothing needs assigning by hand. The layout itself
/// comes from RemoveAdsOfferView, not the reference: the card is taller, to fit the price line.
///
/// Non-destructive: it refuses to overwrite an existing prefab unless you confirm.
///
/// Menu: TamalStacker ▸ UI ▸ Create Remove Ads Offer Prefab
/// </summary>
public static class RemoveAdsOfferPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/UI/RemoveAdsOffer.prefab";

    [MenuItem("TamalStacker/UI/Create Remove Ads Offer Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Remove Ads Offer Prefab",
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

        EditorUtility.DisplayDialog("Remove Ads Offer Prefab",
            "Created " + PrefabPath + ".\n\n" + styleNote + "\n\n" +
            "Everything is left visible for editing — the view hides it at runtime. The copy " +
            "shown is only a preview; the real text and Play's price are filled in at runtime.\n\n" +
            "Delete the prefab to go back to the code-built layout.", "OK");
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("RemoveAdsOffer",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RemoveAdsOfferView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = RemoveAdsOfferView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // ── Backdrop ── blocks the Settings panel behind the card.
        var backdropRect = CreateChild("Backdrop", root.transform);
        Stretch(backdropRect);
        var backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = RunOverlayUI.Backdrop;
        backdrop.raycastTarget = true;
        UIHouseStyle.ApplyImage(backdrop, UIHouseStyle.Backdrop);

        // ── Modal ── styled from the reference, sized for the extra price line.
        var modal = CreateChild("Modal", root.transform);
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);
        Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, RemoveAdsOfferView.ModalSize);

        var title = CreateLabel("Title", modal, "Remove Ads", 52f, RunOverlayUI.Gold);
        UIHouseStyle.ApplyLabel(title, UIHouseStyle.Title);
        Place(title.rectTransform, new Vector2(0.5f, 1f), RemoveAdsOfferView.TitlePosition, RemoveAdsOfferView.TitleSize);

        // The price wears the title's look (gold, display font) one step smaller.
        var price = CreateLabel("Price", modal, "$2.99 · one-time purchase", 42f, RunOverlayUI.Gold);
        UIHouseStyle.ApplyLabel(price, UIHouseStyle.Title);
        price.enableAutoSizing = true;
        price.fontSizeMin = 26f;
        price.fontSizeMax = 42f;
        Place(price.rectTransform, new Vector2(0.5f, 1f), RemoveAdsOfferView.PricePosition, RemoveAdsOfferView.PriceSize);

        var body = CreateLabel("Body", modal,
            "No more ads between runs.\nPay once. No subscription.\nLinked to your Google Play account, so it comes back on a new device.",
            30f, RunOverlayUI.Parchment);
        UIHouseStyle.ApplyLabel(body, UIHouseStyle.Body);
        Place(body.rectTransform, new Vector2(0.5f, 0.5f), RemoveAdsOfferView.BodyPosition, RemoveAdsOfferView.BodySize);

        // Buying is the action the player came here for: primary (jade, right). "Not now"
        // takes the secondary (clay, left) slot.
        var decline = CreateButton("Decline", modal, "Not now", RunOverlayUI.Clay, out var declineLabel);
        UIHouseStyle.ApplyButton(decline, UIHouseStyle.Secondary);
        Place((RectTransform)decline.transform, new Vector2(0.5f, 0f), RemoveAdsOfferView.DeclinePosition, RemoveAdsOfferView.ButtonSize);

        var buy = CreateButton("Buy", modal, "Buy · $2.99", RunOverlayUI.Jade, out var buyLabel);
        UIHouseStyle.ApplyButton(buy, UIHouseStyle.Primary);
        Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), RemoveAdsOfferView.BuyPosition, RemoveAdsOfferView.ButtonSize);

        var so = new SerializedObject(root.GetComponent<RemoveAdsOfferView>());
        so.FindProperty("backdrop").objectReferenceValue = backdrop;
        so.FindProperty("modal").objectReferenceValue = modal;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("priceLabel").objectReferenceValue = price;
        so.FindProperty("bodyLabel").objectReferenceValue = body;
        so.FindProperty("buyButton").objectReferenceValue = buy;
        so.FindProperty("buyLabel").objectReferenceValue = buyLabel;
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
