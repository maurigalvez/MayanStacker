#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lays the main-menu feature chips out as two rails of badge medallions flanking the logo on the
/// Main Menu panel (inside the Main Menu Canvas prefab):
///
///   left:  Chest · Missions · Expedition        right:  Path · Market · Remove Ads
///
/// Each medallion is just a badge icon (from <see cref="MenuRailArt"/>; Remove Ads keeps its own) —
/// no text. The five feature chips also get a jade call-to-action blob (<see cref="MenuRailCue"/>)
/// on the badge corner that pops, pulses and hops the badge while there is something new to do.
/// The chip scripts are kept; only their visuals are rebuilt. The rails run between the top of
/// the screen and the Temple Stacker button, centred vertically. The logo keeps the spot it lands
/// on and is only scaled down, enough to sit between the rails on a 9:21 phone. Once the jade icon
/// is assigned, a jade balance bar sits top centre between the rails and opens the Market.
///
///   • TamalStacker ▸ UI ▸ Main Menu Rail ▸ Select Art Slots — creates/selects MenuRailArt.asset.
///   • TamalStacker ▸ UI ▸ Main Menu Rail ▸ Build Rail — rebuilds the rail. Refuses while any of
///     the five badges is unassigned. Safe to run again after swapping art.
/// </summary>
public static class MainMenuRailSetup
{
    private const string MainMenuPrefabPath = "Assets/Prefabs/Main Menu/Main Menu Canvas.prefab";
    private const string ArtFolder = "Assets/Art/UI/MenuRail";
    private const string ArtPath = ArtFolder + "/MenuRailArt.asset";
    private const string SheetPath = "Assets/Art/UI/MayaStacker_MainMenu_UI.png";
    private const string CueSprite = "MayaStacker_MainMenu_UI_14";   // the plain gold coin, tinted jade
    private const string LeftRailName = "MenuRailLeft";
    private const string RightRailName = "MenuRailRight";
    private const string OldRailName = "MenuRail";   // the single right rail of the first build
    private const string RemoveAdsChipName = "RemoveAdsChip";

    // ── Rail geometry, in the 1080x1920 reference (the canvas matches height) ──
    private const float RailWidth = 130f;
    private const float RailSideMargin = 16f;
    private const float RailTop = 105f;          // clear of punch-hole status bars
    private const float RailGapAbovePlay = 10f;
    private const float ItemHeight = 130f;
    private const float ItemSpacing = 26f;
    private const float IconSize = 124f;
    private const float CueSize = 46f;           // the call-to-action blob on a badge's top-right corner
    private static readonly Vector2 CueOffset = new Vector2(-4f, -4f);   // from the badge's top-right corner

    // ── Logo: keeps its landing position; scaled to fit between the rails on a 9:21 phone (823 wide) ──
    private const float LogoScale = 0.5f;

    // ── Jade balance bar: top centre, between the rails, under the version text, above the logo ──
    private const string JadeBarName = "JadeBalanceBar";
    private const float JadeBarTop = 110f;
    private static readonly Vector2 JadeEmblemSize = new Vector2(186f, 122f);   // the emblem art is 1.53:1
    private static readonly Vector2 JadeFrameSize = new Vector2(280f, 84f);
    private const float JadeFrameOverlap = 60f;     // how far the frame's left stud runs under the emblem
    private const float JadeFrameStudPx = 180f;     // the frame art's end ornaments, sliced off as borders
    private const int JadeFlightSorting = 4400;     // over the Login Chest panel (4300) the jade flies out of
    private const int JadeArtMaxSize = 1024;

    private struct Entry
    {
        public string chip, titleKey, labelField;
        public bool left;
        public System.Func<MenuRailArt, Sprite> icon;
    }

    private static readonly Entry[] Entries =
    {
        new Entry { chip = "LoginChestChip", titleKey = "login_chest_chip", labelField = "statusLabel", left = true, icon = a => a.chest },
        new Entry { chip = "MissionsChip", titleKey = "missions_chip", labelField = "countLabel", left = true, icon = a => a.missions },
        new Entry { chip = "ExpeditionChip", titleKey = "expedition_chip", labelField = "countLabel", left = true, icon = a => a.expedition },
        new Entry { chip = "PathChip", titleKey = "path_chip", labelField = "countLabel", left = false, icon = a => a.path },
        new Entry { chip = "JadeMarketChip", titleKey = "market_chip", labelField = "balanceLabel", left = false, icon = a => a.market },
    };

    [MenuItem("TamalStacker/UI/Main Menu Rail/Select Art Slots")]
    public static void SelectArt()
    {
        var art = EnsureArt();
        Selection.activeObject = art;
        EditorGUIUtility.PingObject(art);
    }

    [MenuItem("TamalStacker/UI/Main Menu Rail/Build Rail")]
    public static void BuildRail()
    {
        EditorUtility.DisplayDialog("Main Menu Rail", Build(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static string BuildBatch() => Build();

    private static string Build()
    {
        var art = EnsureArt();
        var missing = new List<string>();
        foreach (var e in Entries)
            if (e.icon(art) == null) missing.Add(e.chip.Replace("Chip", ""));
        if (missing.Count > 0)
        {
            Selection.activeObject = art;
            return "Not built — these badges are still unassigned in " + ArtPath + ":\n\n  " +
                   string.Join(", ", missing) +
                   "\n\nAssign all five (square, transparent, Remove Ads badge style) and run Build Rail again.";
        }
        foreach (var e in Entries) PrepareIcon(e.icon(art));
        bool jadeReady = art.jade != null && art.jadeCounterFrame != null;
        if (jadeReady)
        {
            PrepareIcon(art.jade, JadeArtMaxSize, Vector4.zero);
            PrepareIcon(art.jadeCounterFrame, JadeArtMaxSize, new Vector4(JadeFrameStudPx, 0f, JadeFrameStudPx, 0f));
            if (art.jadeShards != null)
                foreach (var shard in art.jadeShards) if (shard != null) PrepareIcon(shard, 512, Vector4.zero);
        }

        Sprite cueSprite = LoadSheetSprite(CueSprite);
        if (cueSprite == null) return CueSprite + " was not found in " + SheetPath + ".";

        string result;
        GameObject root = PrefabUtility.LoadPrefabContents(MainMenuPrefabPath);
        try
        {
            result = BuildInto(root, art, cueSprite);
            if (result == null) PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (result != null)
        {
            Debug.LogError($"[MainMenuRailSetup] {result}");
            return result;
        }
        AssetDatabase.SaveAssets();
        string done = "Built the main-menu rail (" + Entries.Length + " features + Remove Ads) and refitted the logo in " +
                      MainMenuPrefabPath + ".";
        if (!jadeReady)
            done += "\n\nThe jade balance bar was skipped: assign the Jade emblem and counter frame in " + ArtPath +
                    " and run Build Rail again.";
        else if (art.jadeShards == null || System.Array.TrueForAll(art.jadeShards, x => x == null))
            done += "\n\nNo jade pieces are assigned, so chest jade counts up without flying in.";
        Debug.Log($"[MainMenuRailSetup] {done}");
        return done;
    }

    /// <returns>Null on success, or the reason nothing was built.</returns>
    private static string BuildInto(GameObject root, MenuRailArt art, Sprite cueSprite)
    {
        var menu = root.GetComponentInChildren<MainMenuManager>(true);
        var panelObject = menu != null
            ? new SerializedObject(menu).FindProperty("mainMenuPanel").objectReferenceValue as GameObject
            : null;
        if (panelObject == null) return "The prefab has no Main Menu panel (MainMenuManager.mainMenuPanel is unassigned).";
        Transform panel = panelObject.transform;

        var play = panel.Find("Play") as RectTransform;
        var logo = panel.Find("LOGO") as RectTransform;
        if (play == null || logo == null) return "The Main Menu panel has no Play button or LOGO to fit the rail around.";

        var chips = new List<RectTransform>();
        foreach (var e in Entries)
        {
            var chip = FindChip(panel, e.chip);
            if (chip == null) return $"{e.chip} was not found — add it with its TamalStacker ▸ Economy menu first.";
            chips.Add(chip);
        }
        var removeAds = FindChip(panel, RemoveAdsChipName);
        if (removeAds == null) return $"{RemoveAdsChipName} was not found — add it with TamalStacker ▸ Monetization ▸ Add Remove Ads Menu Chip first.";

        // ── Rails ── one per side, from under the status bar to just above the Play button.
        float playTop = play.anchoredPosition.y + play.sizeDelta.y * (1f - play.pivot.y);
        var leftRail = Rail(panel, LeftRailName, true, playTop);
        var rightRail = Rail(panel, RightRailName, false, playTop);

        Color jade = ReadTint(UIHouseStyle.Primary, RunOverlayUI.Jade);

        for (int i = 0; i < Entries.Length; i++)
        {
            var e = Entries[i];
            var chip = chips[i];
            Medallion(chip, e.left ? leftRail : rightRail, e.icon(art), cueSprite, jade);

            // The medallions carry no text: the icon says what it is, the cue says there is something to do.
            foreach (var mb in chip.GetComponents<MonoBehaviour>())
            {
                var so = new SerializedObject(mb);
                var labelProp = so.FindProperty(e.labelField);
                if (labelProp == null) continue;
                labelProp.objectReferenceValue = null;
                var pillProp = so.FindProperty("statusPill");
                if (pillProp != null) pillProp.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Remove Ads keeps its own badge; it closes the right rail, without a cue.
        var adsIcon = removeAds.Find("Icon") != null ? removeAds.Find("Icon").GetComponent<Image>().sprite : null;
        if (adsIcon == null) return $"{RemoveAdsChipName} has no Icon sprite to keep.";
        Medallion(removeAds, rightRail, adsIcon, null, jade);

        for (int i = 0; i < chips.Count; i++) chips[i].SetAsLastSibling();
        removeAds.SetAsLastSibling();

        var oldRail = panel.Find(OldRailName);
        if (oldRail != null) Object.DestroyImmediate(oldRail.gameObject);

        // ── Logo ── same landing spot (ButtonFallInAnimation reads it at start), smaller.
        logo.localScale = new Vector3(LogoScale, LogoScale, logo.localScale.z);

        if (art.jade != null && art.jadeCounterFrame != null) JadeBar(panel, art);

        return null;
    }

    /// <summary>
    /// The jade balance, always in view at the top of the menu: the jade emblem on its pedestal over
    /// the left end of the gold counter frame, the count in the frame's parchment window. Carries a
    /// second Market chip (opens the Market on tap, hides when jade is off) and the
    /// <see cref="JadeBalanceBar"/> that flies chest jade into the emblem and counts it up.
    /// </summary>
    private static void JadeBar(Transform panel, MenuRailArt art)
    {
        var old = panel.Find(JadeBarName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var bar = CreateJadeBar(panel, art, JadeBarName, true, JadeFlightSorting);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = new Vector2(0f, -JadeBarTop);
        bar.SetAsLastSibling();
    }

    /// <summary>The menu rail's art asset, or null when it hasn't been created. Read by other panels that show the jade bar.</summary>
    internal static MenuRailArt LoadArt() => AssetDatabase.LoadAssetAtPath<MenuRailArt>(ArtPath);

    /// <summary>True when the emblem and counter frame are assigned, so a jade bar can be built.</summary>
    internal static bool JadeArtReady(MenuRailArt art) => art != null && art.jade != null && art.jadeCounterFrame != null;

    /// <summary>Sets the jade bar sprites up for UI use (sizes, the frame's sliced ends). Safe to repeat.</summary>
    internal static void PrepareJadeArt(MenuRailArt art)
    {
        if (!JadeArtReady(art)) return;
        PrepareIcon(art.jade, JadeArtMaxSize, Vector4.zero);
        PrepareIcon(art.jadeCounterFrame, JadeArtMaxSize, new Vector4(JadeFrameStudPx, 0f, JadeFrameStudPx, 0f));
        if (art.jadeShards != null)
            foreach (var shard in art.jadeShards) if (shard != null) PrepareIcon(shard, 512, Vector4.zero);
    }

    /// <summary>
    /// Builds one jade balance bar under <paramref name="parent"/> and returns its rect; the caller
    /// positions it. The menu's carries the Market tap; a panel's own bar does not.
    /// </summary>
    internal static RectTransform CreateJadeBar(Transform parent, MenuRailArt art, string name, bool marketChip, int flightSorting)
    {
        var bar = RunOverlayUI.CreateChild(name, parent);
        bar.sizeDelta = new Vector2(JadeEmblemSize.x - JadeFrameOverlap + JadeFrameSize.x, JadeEmblemSize.y);

        // A clear catcher so the emblem and frame are one tap target.
        var catcher = bar.gameObject.AddComponent<Image>();
        catcher.color = Color.clear;
        catcher.raycastTarget = true;
        bar.gameObject.AddComponent<CanvasGroup>();

        // ── Counter frame ── right of the emblem, its left stud tucked under it.
        var frameRect = RunOverlayUI.CreateChild("Frame", bar);
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(1f, 0.5f);
        frameRect.pivot = new Vector2(1f, 0.5f);
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.sizeDelta = JadeFrameSize;
        var frame = frameRect.gameObject.AddComponent<Image>();
        frame.sprite = art.jadeCounterFrame;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = art.jadeCounterFrame.rect.height / JadeFrameSize.y;
        frame.raycastTarget = false;

        if (marketChip)
        {
            var button = bar.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.ColorTint;
        }

        // Keeps the house Title face (gold, with its outline) so the count matches every other number.
        var count = RunOverlayUI.CreateLabel("Count", frameRect, "0", 46f, RunOverlayUI.Gold);
        UIHouseStyle.ApplyLabel(count, UIHouseStyle.Title);
        count.fontSize = 46f;
        count.enableAutoSizing = true;
        count.fontSizeMin = 22f;
        count.fontSizeMax = 46f;
        count.alignment = TextAlignmentOptions.Center;
        count.textWrappingMode = TextWrappingModes.NoWrap;
        count.overflowMode = TextOverflowModes.Overflow;
        var countRect = count.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        float stud = JadeFrameStudPx / frame.pixelsPerUnitMultiplier;
        countRect.offsetMin = new Vector2(Mathf.Max(stud, JadeFrameOverlap) + 4f, 8f);
        countRect.offsetMax = new Vector2(-stud - 4f, -8f);

        // ── Emblem ── last so it sits over the frame's end; pivot centred so it punches in place.
        var emblemRect = RunOverlayUI.CreateChild("Emblem", bar);
        emblemRect.anchorMin = emblemRect.anchorMax = new Vector2(0f, 0.5f);
        emblemRect.pivot = new Vector2(0.5f, 0.5f);
        emblemRect.anchoredPosition = new Vector2(JadeEmblemSize.x * 0.5f, 0f);
        emblemRect.sizeDelta = JadeEmblemSize;
        var emblem = emblemRect.gameObject.AddComponent<Image>();
        emblem.sprite = art.jade;
        emblem.preserveAspect = true;
        emblem.raycastTarget = false;

        // ── Flight layer ── a nested canvas over the popups for the pieces flying in from a chest.
        var layer = RunOverlayUI.CreateChild("FlightLayer", bar);
        layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 0.5f);
        layer.pivot = new Vector2(0.5f, 0.5f);
        layer.anchoredPosition = Vector2.zero;
        layer.sizeDelta = Vector2.zero;
        var layerCanvas = layer.gameObject.AddComponent<Canvas>();
        layerCanvas.overrideSorting = true;
        layerCanvas.sortingOrder = flightSorting;

        var templates = new List<Image>();
        if (art.jadeShards != null)
        {
            for (int i = 0; i < art.jadeShards.Length; i++)
            {
                if (art.jadeShards[i] == null) continue;
                var piece = RunOverlayUI.CreateChild("Piece_" + art.jadeShards[i].name, layer);
                piece.anchorMin = piece.anchorMax = new Vector2(0.5f, 0.5f);
                piece.sizeDelta = new Vector2(58f, 82f);
                var image = piece.gameObject.AddComponent<Image>();
                image.sprite = art.jadeShards[i];
                image.preserveAspect = true;
                image.raycastTarget = false;
                piece.gameObject.SetActive(false);
                templates.Add(image);
            }
        }

        if (marketChip)
        {
            var chip = bar.gameObject.AddComponent<JadeMarketMenuChip>();
            var chipSo = new SerializedObject(chip);
            chipSo.FindProperty("balanceLabel").objectReferenceValue = null;
            chipSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // The floating "+N" / "−N" and the "need more" nudge, cloned from this at runtime.
        var delta = RunOverlayUI.CreateLabel("DeltaTemplate", layer, "+0", 46f, RunOverlayUI.Gold);
        UIHouseStyle.ApplyLabel(delta, UIHouseStyle.Title);
        delta.fontSize = 46f;
        delta.alignment = TextAlignmentOptions.Center;
        delta.textWrappingMode = TextWrappingModes.NoWrap;
        delta.overflowMode = TextOverflowModes.Overflow;
        delta.enableAutoSizing = false;
        UIHouseStyle.EnforceMinFontSize(delta);
        delta.rectTransform.sizeDelta = new Vector2(560f, 70f);
        delta.gameObject.SetActive(false);

        var view = bar.gameObject.AddComponent<JadeBalanceBar>();
        var so = new SerializedObject(view);
        so.FindProperty("countLabel").objectReferenceValue = count;
        so.FindProperty("emblem").objectReferenceValue = emblemRect;
        so.FindProperty("flightLayer").objectReferenceValue = layer;
        var list = so.FindProperty("shardTemplates");
        list.arraySize = templates.Count;
        for (int i = 0; i < templates.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = templates[i];
        so.FindProperty("deltaTemplate").objectReferenceValue = delta;
        so.FindProperty("pieceClink").objectReferenceValue = art.jadeClink;
        so.FindProperty("finalChime").objectReferenceValue = art.jadeChime;
        so.ApplyModifiedPropertiesWithoutUndo();
        return bar;
    }

    /// <summary>One side's column: items centred vertically between the status bar and the Play button.</summary>
    private static RectTransform Rail(Transform panel, string name, bool left, float playTop)
    {
        var rail = panel.Find(name) as RectTransform;
        if (rail == null) rail = RunOverlayUI.CreateChild(name, panel);
        float side = left ? 0f : 1f;
        rail.anchorMin = new Vector2(side, 0f);
        rail.anchorMax = new Vector2(side, 1f);
        rail.pivot = new Vector2(side, 1f);
        float x = left ? RailSideMargin : -RailSideMargin - RailWidth;
        rail.offsetMin = new Vector2(x, playTop + RailGapAbovePlay);
        rail.offsetMax = new Vector2(x + RailWidth, -RailTop);
        rail.SetAsLastSibling();

        var layout = rail.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = rail.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = ItemSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(0, 0, 0, 0);
        return rail;
    }

    /// <summary>Rebuilds one chip's visuals as a medallion inside the rail.</summary>
    private static void Medallion(RectTransform chip, RectTransform rail, Sprite icon, Sprite cueSprite, Color cueTint)
    {
        chip.SetParent(rail, false);
        chip.localScale = Vector3.one;
        chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 1f);
        chip.pivot = new Vector2(0.5f, 1f);
        chip.sizeDelta = new Vector2(RailWidth, ItemHeight);

        for (int i = chip.childCount - 1; i >= 0; i--) Object.DestroyImmediate(chip.GetChild(i).gameObject);
        var button = chip.GetComponent<Button>();
        var oldImage = chip.GetComponent<Image>();
        if (oldImage != null)
        {
            if (button != null) button.targetGraphic = null;
            Object.DestroyImmediate(oldImage);
        }

        var element = chip.GetComponent<LayoutElement>();
        if (element == null) element = chip.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = ItemHeight;
        element.minHeight = ItemHeight;
        element.ignoreLayout = false;

        // ── Badge ── doubles as the button's target graphic.
        var iconRect = RunOverlayUI.CreateChild("Icon", chip);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(IconSize, IconSize);
        var image = iconRect.gameObject.AddComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = true;
        if (button != null)
        {
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
        }

        // A transparent hit area so the whole medallion is tappable.
        var hit = chip.GetComponent<Graphic>();
        if (hit == null)
        {
            var catcher = chip.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            catcher.raycastTarget = true;
        }

        // The remaining medallions with something to say (not Remove Ads) get the call-to-action blob.
        var oldCue = chip.GetComponent<MenuRailCue>();
        if (oldCue != null) Object.DestroyImmediate(oldCue);
        if (cueSprite == null) return;

        // ── Call-to-action blob ── on the badge's top-right corner; MenuRailCue pops, pulses and hops it.
        var dotRect = RunOverlayUI.CreateChild("Cue", chip);
        dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 1f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = new Vector2(IconSize * 0.5f - CueSize * 0.5f, -CueSize * 0.5f) + CueOffset;
        dotRect.sizeDelta = new Vector2(CueSize, CueSize);
        var dot = dotRect.gameObject.AddComponent<Image>();
        dot.sprite = cueSprite;
        dot.color = cueTint;
        dot.preserveAspect = true;
        dot.raycastTarget = false;
        dotRect.gameObject.SetActive(false);

        var cue = chip.gameObject.AddComponent<MenuRailCue>();
        var cueSo = new SerializedObject(cue);
        cueSo.FindProperty("dot").objectReferenceValue = dotRect;
        cueSo.FindProperty("badge").objectReferenceValue = iconRect;
        cueSo.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Chips live on the panel before the first build and in a rail after it.</summary>
    private static RectTransform FindChip(Transform panel, string name)
    {
        var direct = panel.Find(name) as RectTransform;
        if (direct != null) return direct;
        foreach (var railName in new[] { LeftRailName, RightRailName, OldRailName })
        {
            var rail = panel.Find(railName);
            var chip = rail != null ? rail.Find(name) as RectTransform : null;
            if (chip != null) return chip;
        }
        return null;
    }

    /// <summary>The house tint for a button role, read from the reference prefab.</summary>
    private static Color ReadTint(string role, Color fallback)
    {
        var probe = new GameObject("TintProbe", typeof(RectTransform), typeof(Image));
        try
        {
            var image = probe.GetComponent<Image>();
            image.color = fallback;
            UIHouseStyle.ApplyImage(image, role);
            return image.color;
        }
        finally
        {
            Object.DestroyImmediate(probe);
        }
    }

    private static MenuRailArt EnsureArt()
    {
        var art = AssetDatabase.LoadAssetAtPath<MenuRailArt>(ArtPath);
        if (art != null) return art;
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Art/UI", "MenuRail");
        art = ScriptableObject.CreateInstance<MenuRailArt>();
        AssetDatabase.CreateAsset(art, ArtPath);
        AssetDatabase.SaveAssets();
        return art;
    }

    /// <summary>Imports a badge as a single UI sprite (no mips, alpha kept, 256 max) like the No Ads icon.</summary>
    private static void PrepareIcon(Sprite sprite) => PrepareIcon(sprite, 256, Vector4.zero);

    /// <summary>
    /// Same, at <paramref name="maxSize"/>, with a 9-slice <paramref name="border"/> (source pixels;
    /// zero for none). A bordered sprite gets a Full Rect mesh, which slicing needs.
    /// </summary>
    private static void PrepareIcon(Sprite sprite, int maxSize, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
        if (importer == null || importer.spriteImportMode == SpriteImportMode.Multiple) return;

        bool changed = false;
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; changed = true; }
        if (border != Vector4.zero)
        {
            if (importer.spriteBorder != border) { importer.spriteBorder = border; changed = true; }
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                changed = true;
            }
        }
        if (changed) importer.SaveAndReimport();
    }

    /// <summary>
    /// The English string for a key, so the prefab reads right in the editor too (LocalizedText
    /// swaps in the player's language at runtime).
    /// </summary>
    private static string English(string key)
    {
        var file = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Localization/en.json");
        if (file == null) return "";
        var match = System.Text.RegularExpressions.Regex.Match(file.text,
            "\"key\"\\s*:\\s*\"" + System.Text.RegularExpressions.Regex.Escape(key) + "\"\\s*,\\s*\"value\"\\s*:\\s*\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static Sprite LoadSheetSprite(string spriteName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
            if (asset is Sprite sprite && sprite.name == spriteName) return sprite;
        return null;
    }
}
#endif
