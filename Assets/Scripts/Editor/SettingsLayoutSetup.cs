#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Re-lays out the MainMenu Settings panel on layout groups instead of hand-placed offsets.
///
/// The panel grew one setup tool at a time (notifications, identity, Remove Ads), and each
/// dropped its controls at absolute positions — some on the outer "Settings Panel" root, so
/// the purchase buttons hung below the slab. This tool gathers every control into one
/// "Content" column inside the slab:
///
///   Toggles   — Music, SFX, Daily Reminders (+ the "blocked" line): label left, round
///               control right, one size and one font for every row
///   Language  — the dropdown, dressed as a stone inset with a parchment list
///   Account   — name, sign-in status, Google sign-in button
///   Purchases — Restore (clay) | Remove Ads (jade), status line, Rate this game
///
/// Back / Save stay pinned to the slab's foot with the "Saved" line between them. Hidden
/// controls (blocked line, sign-in button, Restore once owned) collapse instead of leaving
/// holes, because layout groups skip inactive children.
///
/// The Main Menu Canvas is a prefab, and children of a prefab instance can't be re-parented
/// in the scene, so the work happens on the prefab asset in three steps:
///   1. controls the older setup tools added to the scene instance only (Remove Ads,
///      Restore, purchase status, their LocaleFontSwitchers and SettingsManager references)
///      are applied to the prefab, so the layout can own them;
///   2. the instance's remaining overrides inside the Settings panel — the old hand-placed
///      offsets — are reverted, so they can't sit on top of the new layout;
///   3. the prefab contents are laid out and saved.
///
/// Nothing is rewired: existing objects are re-parented, so SettingsManager /
/// PlayerIdentityView references stay as they are. Idempotent — re-running re-applies the
/// same layout.
///
/// Menu: TamalStacker ▸ UI ▸ Polish Settings Layout (MainMenu scene open)
/// </summary>
public static class SettingsLayoutSetup
{
    private const string Title = "Settings Layout";

    // Slab-space insets of the content column (the slab is ~737 x 1275).
    private const float SideInset = 80f;
    private const float TopInset = 175f;    // below the SETTINGS title
    private const float BottomInset = 235f; // above the Back / Save row

    private const float GroupSpacing = 22f;
    private const float RowSpacing = 8f;
    private const float RowHeight = 90f;
    private const float ControlSize = 90f;
    private const float ButtonHeight = 84f;
    private const float LineHeight = 36f;

    private const float FooterButtonSize = 150f;
    private const float FooterInset = 100f;
    private const float FooterFromBottom = 60f;

    private static readonly Color Parchment = DailyChallengeUISetup.Parchment;
    private static readonly Color Muted = DailyChallengeUISetup.Muted;
    private static readonly Color LabelGold = new Color(1f, 0.838f, 0.458f, 1f);
    private static readonly Color ListInk = new Color(0.25f, 0.13f, 0.03f, 1f);
    private static readonly Color ListHighlight = new Color(0.79f, 0.52f, 0.20f, 0.35f);

    private const string SheetPath = "Assets/Art/UI/MayaStacker_MainMenu_UI.png";
    private const string InsetSprite = "MayaStacker_MainMenu_UI_22";
    private const string ParchmentSprite = "MayaStacker_MainMenu_UI_23";

    [MenuItem("TamalStacker/UI/Polish Settings Layout")]
    public static void Setup()
    {
        var sceneSettings = Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        var sceneMenu = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        string assetPath = sceneMenu != null
            ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sceneMenu.gameObject)
            : null;
        if (sceneSettings == null || string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog(Title,
                "Couldn't find the Main Menu Canvas prefab instance with its SettingsManager.\n\n" +
                "Open the MainMenu scene first, then run this again.", "OK");
            return;
        }

        var scenePanel = new SerializedObject(sceneMenu).FindProperty("settingsPanel").objectReferenceValue as GameObject;
        if (scenePanel != null)
        {
            int pushed = PushSceneOnlyControls(sceneSettings, scenePanel.transform, assetPath);
            int reverted = RevertPanelOverrides(scenePanel.transform);
            Debug.Log($"[SettingsLayoutSetup] Applied {pushed} scene-only change(s) to the prefab, reverted {reverted} stale override(s).");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            if (!Layout(root))
            {
                EditorUtility.DisplayDialog(Title,
                    "The prefab has no Settings panel (MainMenuManager.settingsPanel ▸ Panel) or SettingsManager.", "OK");
                return;
            }
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        EditorSceneManager.MarkSceneDirty(sceneSettings.gameObject.scene);
        Debug.Log($"[SettingsLayoutSetup] Settings panel laid out in {assetPath}. Review it, then save the MainMenu scene.");
    }

    // ── Steps 1 & 2: scene instance → prefab ────────────────────────────────

    private static readonly string[] SceneOnlyFields =
        { "removeAdsButton", "removeAdsLabel", "restorePurchasesButton", "purchaseStatusText" };

    /// <summary>
    /// Applies the Settings controls that exist only on the scene instance to the prefab,
    /// along with the SettingsManager fields that point at them.
    /// </summary>
    private static int PushSceneOnlyControls(SettingsManager settings, Transform panel, string assetPath)
    {
        int pushed = 0;
        var so = new SerializedObject(settings);

        foreach (string field in SceneOnlyFields)
        {
            var target = so.FindProperty(field)?.objectReferenceValue as Component;
            if (target == null) continue;
            // The outermost added object: applying it brings its children along.
            GameObject added = null;
            for (Transform t = target.transform; t != null && t != panel.parent; t = t.parent)
            {
                if (PrefabUtility.IsAddedGameObjectOverride(t.gameObject)) added = t.gameObject;
            }
            if (added == null) continue;
            PrefabUtility.ApplyAddedGameObject(added, assetPath, InteractionMode.AutomatedAction);
            pushed++;
        }

        // Font switchers are real work from the localization pass; any other added
        // component in the panel is leftover layout scaffolding, reverted in step 2.
        foreach (var component in panel.GetComponentsInChildren<LocaleFontSwitcher>(true))
        {
            if (!PrefabUtility.IsAddedComponentOverride(component)) continue;
            PrefabUtility.ApplyAddedComponent(component, assetPath, InteractionMode.AutomatedAction);
            pushed++;
        }

        so.Update();
        foreach (string field in SceneOnlyFields)
        {
            var prop = so.FindProperty(field);
            if (prop == null || !prop.prefabOverride) continue;
            PrefabUtility.ApplyPropertyOverride(prop, assetPath, InteractionMode.AutomatedAction);
            pushed++;
        }
        return pushed;
    }

    /// <summary>
    /// Reverts every override left inside the Settings panel so the prefab's layout shows
    /// through. The panel's own GameObject is skipped — its active flag is scene state.
    /// </summary>
    private static int RevertPanelOverrides(Transform panel)
    {
        int reverted = 0;

        foreach (Transform child in panel.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == panel) continue;
            if (!PrefabUtility.IsAddedGameObjectOverride(child.gameObject)) continue;
            Undo.DestroyObjectImmediate(child.gameObject);
            reverted++;
        }

        foreach (var component in panel.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.gameObject == panel.gameObject) continue;
            if (!PrefabUtility.IsAddedComponentOverride(component)) continue;
            Undo.DestroyObjectImmediate(component);
            reverted++;
        }

        GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(panel.gameObject);
        foreach (var change in PrefabUtility.GetObjectOverrides(instanceRoot))
        {
            if (!(change.instanceObject is Component component)) continue;
            if (component.gameObject == panel.gameObject || !component.transform.IsChildOf(panel)) continue;
            change.Revert(InteractionMode.AutomatedAction);
            reverted++;
        }
        return reverted;
    }

    private static GameObject FindOrCreate(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // ── Step 3: layout inside the prefab ────────────────────────────────────

    private static bool Layout(GameObject root)
    {
        var menu = root.GetComponentInChildren<MainMenuManager>(true);
        var settings = root.GetComponentInChildren<SettingsManager>(true);
        var panelGo = menu != null
            ? new SerializedObject(menu).FindProperty("settingsPanel").objectReferenceValue as GameObject
            : null;
        Transform slab = panelGo != null ? panelGo.transform.Find("Panel") : null;
        if (settings == null || slab == null) return false;

        var so = new SerializedObject(settings);
        T Ref<T>(string field) where T : Object => so.FindProperty(field)?.objectReferenceValue as T;

        // ── Column ──────────────────────────────────────────────────────────
        var content = Container(slab, "Content", GroupSpacing);
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(SideInset, BottomInset);
        contentRect.offsetMax = new Vector2(-SideInset, -TopInset);

        // ── Toggles ─────────────────────────────────────────────────────────
        var toggles = Container(content.transform, "ToggleGroup", RowSpacing);
        int order = 0;

        TMP_FontAsset rowFont = null;
        foreach (string rowName in new[] { "Master Volume", "Music Volume", "Sfx Volume" })
        {
            Transform row = FindChild(slab, content.transform, rowName);
            if (row == null) continue;
            var label = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (rowFont == null && label != null) rowFont = label.font;
            var mute = row.GetComponentInChildren<Button>(true);
            Adopt(row, toggles.transform, order++);
            StyleRow(row, label, mute != null ? (RectTransform)mute.transform : null, rowFont);
        }

        var notifications = Ref<Toggle>("notificationsToggle");
        if (notifications != null)
        {
            Transform row = notifications.transform;
            Adopt(row, toggles.transform, order++);
            var label = row.Find("Label")?.GetComponent<TextMeshProUGUI>();
            StyleRow(row, label, notifications.targetGraphic != null ? notifications.targetGraphic.rectTransform : null, rowFont);
        }

        var blocked = Ref<TextMeshProUGUI>("notificationsStatusText");
        if (blocked != null)
        {
            Adopt(blocked.transform, toggles.transform, order++);
            StyleLine(blocked, 20, 28, Muted, TextAlignmentOptions.MidlineLeft, wrap: true);
            Size(blocked.gameObject, LineHeight + 16f);
        }

        // ── Language ────────────────────────────────────────────────────────
        var dropdown = Ref<TMP_Dropdown>("languageDropdown");
        if (dropdown != null)
        {
            Adopt(dropdown.transform, content.transform, 1);
            StyleDropdown(dropdown);
            Size(dropdown.gameObject, RowHeight + 6f);
        }

        // ── Account ─────────────────────────────────────────────────────────
        var identity = slab.GetComponentInChildren<PlayerIdentityView>(true);
        if (identity != null)
        {
            Adopt(identity.transform, content.transform, 2);
            var group = Stack(identity.gameObject, 6f);
            group.padding = new RectOffset(0, 0, 0, 0);

            var idSo = new SerializedObject(identity);
            var name = idSo.FindProperty("nameText").objectReferenceValue as TextMeshProUGUI;
            var status = idSo.FindProperty("statusText").objectReferenceValue as TextMeshProUGUI;
            var signIn = idSo.FindProperty("googleSignInButton").objectReferenceValue as Button;

            if (name != null)
            {
                StyleLine(name, 30, 46, LabelGold, TextAlignmentOptions.Center, wrap: false);
                Size(name.gameObject, 52f);
            }
            if (status != null)
            {
                StyleLine(status, 20, 28, Muted, TextAlignmentOptions.Center, wrap: false);
                Size(status.gameObject, LineHeight);
            }
            if (signIn != null)
            {
                Size(signIn.gameObject, ButtonHeight);
                StyleButtonLabel(signIn);
            }
        }

        // ── Purchases ───────────────────────────────────────────────────────
        var purchases = Container(content.transform, "PurchaseGroup", RowSpacing);
        purchases.transform.SetSiblingIndex(3);

        var buttonRow = FindOrCreate(purchases.transform, "PurchaseRow");
        buttonRow.transform.SetSiblingIndex(0);
        var rowLayout = buttonRow.GetComponent<HorizontalLayoutGroup>() ?? buttonRow.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = true;
        Size(buttonRow, ButtonHeight);

        // Secondary left, primary right — the house order for button pairs.
        int purchaseIndex = 0;
        foreach (string field in new[] { "restorePurchasesButton", "removeAdsButton" })
        {
            var button = Ref<Button>(field);
            if (button == null) continue;
            Adopt(button.transform, buttonRow.transform, purchaseIndex++);
            var element = Size(button.gameObject, ButtonHeight);
            element.preferredWidth = 1f;
            element.flexibleWidth = 1f;
            StyleButtonLabel(button);
        }

        var purchaseStatus = Ref<TextMeshProUGUI>("purchaseStatusText");
        if (purchaseStatus != null)
        {
            Adopt(purchaseStatus.transform, purchases.transform, 1);
            StyleLine(purchaseStatus, 18, 24, Parchment, TextAlignmentOptions.Center, wrap: true);
            Size(purchaseStatus.gameObject, LineHeight);
        }

        var rate = Ref<Button>("rateGameButton");
        if (rate != null)
        {
            Adopt(rate.transform, purchases.transform, 2);
            Size(rate.gameObject, ButtonHeight);
            StyleButtonLabel(rate);
        }

        // ── Footer ──────────────────────────────────────────────────────────
        var back = new SerializedObject(menu).FindProperty("backFromSettingsButton").objectReferenceValue as Button;
        if (back != null) PlaceFooter((RectTransform)back.transform, left: true);
        var save = Ref<Button>("applyButton");
        if (save != null) PlaceFooter((RectTransform)save.transform, left: false);

        var saved = Ref<TextMeshProUGUI>("savedFeedbackText");
        if (saved != null)
        {
            // Between Back and Save, so the confirmation lands where the thumb just was.
            var rect = saved.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            float inner = FooterInset + FooterButtonSize + 10f;
            rect.offsetMin = new Vector2(inner, FooterFromBottom);
            rect.offsetMax = new Vector2(-inner, FooterFromBottom + FooterButtonSize);
            rect.anchoredPosition = new Vector2(0f, FooterFromBottom + FooterButtonSize * 0.5f);
            saved.alignment = TextAlignmentOptions.Center;
            saved.enableAutoSizing = true;
            saved.fontSizeMin = 24;
            saved.fontSizeMax = 44;
        }

        return true;
    }

    // ── Containers ──────────────────────────────────────────────────────────

    /// <summary>A stretch-width vertical stack, created once and reused on re-runs.</summary>
    private static GameObject Container(Transform parent, string name, float spacing)
    {
        var go = FindOrCreate(parent, name);
        Stack(go, spacing);
        return go;
    }

    private static VerticalLayoutGroup Stack(GameObject go, float spacing)
    {
        var layout = go.GetComponent<VerticalLayoutGroup>() ?? go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    /// <summary>
    /// Fixes a child's height for its parent stack. The LayoutElement outranks TMP's and
    /// Image's own preferred sizes, which would otherwise size rows to their text or sprite.
    /// </summary>
    private static LayoutElement Size(GameObject go, float height)
    {
        var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
        return element;
    }

    private static void Adopt(Transform child, Transform parent, int index)
    {
        if (child.parent != parent) child.SetParent(parent, false);
        child.localScale = Vector3.one;
        child.SetSiblingIndex(Mathf.Min(index, parent.childCount - 1));
    }

    /// <summary>Finds a row by name whether it still sits on the slab or was moved on a previous run.</summary>
    private static Transform FindChild(Transform slab, Transform content, string name)
    {
        Transform direct = slab.Find(name);
        if (direct != null) return direct;
        foreach (Transform group in content)
        {
            Transform found = group.Find(name);
            if (found != null) return found;
        }
        return null;
    }

    // ── Rows ────────────────────────────────────────────────────────────────

    /// <summary>Label fills the row from the left; the round control sits flush right, same size on every row.</summary>
    private static void StyleRow(Transform row, TextMeshProUGUI label, RectTransform control, TMP_FontAsset font)
    {
        Size(row.gameObject, RowHeight);

        if (control != null)
        {
            control.anchorMin = control.anchorMax = new Vector2(1f, 0.5f);
            control.pivot = new Vector2(1f, 0.5f);
            control.sizeDelta = new Vector2(ControlSize, ControlSize);
            control.anchoredPosition = Vector2.zero;
            control.localScale = Vector3.one;
        }

        if (label != null)
        {
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(-(ControlSize + 20f), 0f);
            rect.localScale = Vector3.one;

            if (font != null) label.font = font;
            label.color = Parchment;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 30;
            label.fontSizeMax = 60;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
        }
    }

    private static void StyleLine(TextMeshProUGUI text, float min, float max, Color color,
        TextAlignmentOptions alignment, bool wrap)
    {
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
        text.margin = Vector4.zero;
    }

    /// <summary>Same label inset and size range on every wide button, so they read as one set.</summary>
    private static void StyleButtonLabel(Button button)
    {
        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) return;
        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(24f, 14f);
        rect.offsetMax = new Vector2(-24f, -18f); // the sprite's lip is at the bottom
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18;
        label.fontSizeMax = 34;
        label.raycastTarget = false;
    }

    private static void PlaceFooter(RectTransform rect, bool left)
    {
        float x = left ? 0f : 1f;
        rect.anchorMin = rect.anchorMax = new Vector2(x, 0f);
        rect.pivot = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(FooterButtonSize, FooterButtonSize);
        rect.anchoredPosition = new Vector2(left ? FooterInset : -FooterInset, FooterFromBottom);
    }

    // ── Language dropdown ───────────────────────────────────────────────────

    /// <summary>
    /// Stone inset with a gold arrow for the closed field, parchment sheet with brown ink for
    /// the open list — instead of Unity's default white boxes.
    /// </summary>
    private static void StyleDropdown(TMP_Dropdown dropdown)
    {
        Sprite inset = LoadSheetSprite(InsetSprite);
        Sprite parchment = LoadSheetSprite(ParchmentSprite);

        var field = dropdown.GetComponent<Image>();
        if (field != null && inset != null)
        {
            field.sprite = inset;
            field.type = Image.Type.Sliced;
            field.pixelsPerUnitMultiplier = 2f;
            field.color = Color.white;
        }

        var caption = dropdown.captionText as TextMeshProUGUI;
        if (caption != null)
        {
            var rect = caption.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(34f, 12f);
            rect.offsetMax = new Vector2(-(ControlSize + 10f), -12f);
            caption.color = Parchment;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 24;
            caption.fontSizeMax = 50;
        }

        var arrow = dropdown.transform.Find("Arrow") as RectTransform;
        if (arrow != null)
        {
            arrow.anchorMin = arrow.anchorMax = new Vector2(1f, 0.5f);
            arrow.pivot = new Vector2(1f, 0.5f);
            arrow.sizeDelta = new Vector2(56f, 56f);
            arrow.anchoredPosition = new Vector2(-28f, 0f);
            var arrowImage = arrow.GetComponent<Image>();
            if (arrowImage != null)
            {
                arrowImage.color = LabelGold;
                arrowImage.preserveAspect = true;
            }
        }

        var template = dropdown.template;
        if (template == null) return;

        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = new Vector2(0f, 4f);
        template.sizeDelta = new Vector2(0f, 400f);
        var templateImage = template.GetComponent<Image>();
        if (templateImage != null && parchment != null)
        {
            templateImage.sprite = parchment;
            templateImage.type = Image.Type.Sliced;
            templateImage.color = Color.white;
        }

        // The viewport was anchored to a single corner (width −17), which clips the open
        // list to nothing; stretch it over the list, leaving room for the scrollbar.
        var scroll = template.GetComponent<ScrollRect>();
        var viewport = scroll != null ? scroll.viewport : template.Find("Viewport") as RectTransform;
        if (viewport != null)
        {
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.offsetMin = new Vector2(10f, 10f);
            viewport.offsetMax = new Vector2(-28f, -10f);
        }

        const float itemHeight = 72f;
        var contentRect = scroll != null ? scroll.content : null;
        if (contentRect != null)
        {
            contentRect.sizeDelta = new Vector2(0f, itemHeight);
        }

        var item = dropdown.itemText != null ? dropdown.itemText.transform.parent as RectTransform : null;
        if (item != null)
        {
            item.sizeDelta = new Vector2(0f, itemHeight);

            var itemBg = item.Find("Item Background")?.GetComponent<Image>();
            var toggle = item.GetComponent<Toggle>();
            if (itemBg != null)
            {
                itemBg.color = new Color(1f, 1f, 1f, 0f);
            }
            if (toggle != null)
            {
                // The item graphic is transparent at rest; the tint only shows on hover / press.
                var colors = toggle.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0f);
                colors.highlightedColor = ListHighlight;
                colors.pressedColor = ListHighlight;
                colors.selectedColor = ListHighlight;
                colors.colorMultiplier = 1f;
                toggle.colors = colors;
            }

            var check = item.Find("Item Checkmark") as RectTransform;
            if (check != null)
            {
                check.anchorMin = check.anchorMax = new Vector2(0f, 0.5f);
                check.sizeDelta = new Vector2(30f, 30f);
                check.anchoredPosition = new Vector2(24f, 0f);
                var checkImage = check.GetComponent<Image>();
                if (checkImage != null)
                {
                    checkImage.color = ListInk;
                }
            }
        }

        var itemText = dropdown.itemText as TextMeshProUGUI;
        if (itemText != null)
        {
            var rect = itemText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(52f, 6f);
            rect.offsetMax = new Vector2(-10f, -6f);
            itemText.color = ListInk;
            itemText.alignment = TextAlignmentOptions.MidlineLeft;
            itemText.textWrappingMode = TextWrappingModes.NoWrap;
            itemText.enableAutoSizing = true;
            itemText.fontSizeMin = 22;
            itemText.fontSizeMax = 40;
        }

        var bar = scroll != null ? scroll.verticalScrollbar : null;
        if (bar != null)
        {
            var barImage = bar.GetComponent<Image>();
            if (barImage != null)
            {
                barImage.color = new Color(ListInk.r, ListInk.g, ListInk.b, 0.15f);
            }
            if (bar.handleRect != null && bar.handleRect.TryGetComponent(out Image handle))
            {
                handle.color = new Color(ListInk.r, ListInk.g, ListInk.b, 0.6f);
            }
        }
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
