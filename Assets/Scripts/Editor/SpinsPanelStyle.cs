#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dresses the panels the main-menu badges open (Jade Market, Missions, Path, Expedition, Chest) in the
/// same authored art as the Achievements and Leaderboard lists, in place of the flat boxes they were
/// generated with:
///   • row              — Leaderboard sheet _7, the plain stone panel the Achievement entries sit on
///   • worn/done/reached — Leaderboard sheet _0, the lit slab with the thin inner rim, over the row
///   • colour swatch    — Leaderboard sheet _5, the dark inset slab, framing the swatch colours
///   • progress bar     — Achievement progress bar: gold fill (_1) under the spiral-capped frame (_0)
///
/// Parts are found by the names the generators give them, so wiring and text are untouched. The
/// generators call <see cref="Dress"/> on fresh builds; TamalStacker ▸ UI ▸ Restyle Spins Panels
/// re-dresses the prefabs already in Resources/UI. Safe to run again.
/// </summary>
public static class SpinsPanelStyle
{
    internal const string LeaderboardSheet = "Assets/Art/Textures/MayaStacker_Leaderboard_UI.png";
    private const string BarSheet = "Assets/Art/UI/MayanStacker_Achievement_ProgressBar.png";
    internal const string RowSprite = "MayaStacker_Leaderboard_UI_7";
    internal const string HighlightSprite = "MayaStacker_Leaderboard_UI_0";
    private const string FrameSprite = "MayaStacker_Leaderboard_UI_5";
    private const string BarFrameSprite = "MayanStacker_Achievement_ProgressBar_0";
    private const string BarFillSprite = "MayanStacker_Achievement_ProgressBar_1";

    // 9-slice borders (left, bottom, right, top), measured off the sheets. The bar borders sit past the
    // rounded ends of the frame's window and the fill's capsule, so only straight runs stretch.
    private static readonly Vector4 RowBorder = new Vector4(40f, 40f, 40f, 40f);
    private static readonly Vector4 BarFrameBorder = new Vector4(225f, 0f, 224f, 0f);
    private static readonly Vector4 BarFillBorder = new Vector4(72f, 0f, 74f, 0f);

    // Where the fill sprite lies inside the frame sprite, in frame pixels (both are 250-254 tall).
    private const float BarFrameHeightPx = 250f;
    private const float FillLeftPx = 153f;
    private const float FillRightPx = 147f;

    internal const float RowSliceScale = 1.5f;
    private const float FrameSliceScale = 2f;

    private static readonly string[] PrefabPaths =
    {
        "Assets/Resources/UI/JadeMarket.prefab",
        "Assets/Resources/UI/Missions.prefab",
        "Assets/Resources/UI/Path.prefab",
        "Assets/Resources/UI/Expedition.prefab",
        "Assets/Resources/UI/LoginChest.prefab",
    };

    private static readonly string[] MarkerNames = { "WornMarker", "DoneMarker", "ReachedMarker" };

    [MenuItem("TamalStacker/UI/Restyle Spins Panels")]
    public static void RestyleMenu()
    {
        EditorUtility.DisplayDialog("Spins Panels", Restyle(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static string RestyleBatch() => Restyle();

    private static string Restyle()
    {
        string missing = PrepareSheets();
        if (missing != null) return missing;

        var done = new List<string>();
        foreach (string path in PrefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Dress(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                done.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();

        string note = done.Count > 0 ? "Restyled " + string.Join(", ", done) + "." : "No Spins panel prefabs were found.";
        Debug.Log($"[SpinsPanelStyle] {note}");
        return note;
    }

    /// <summary>Dresses every row, swatch and progress bar under a panel root.</summary>
    public static void Dress(GameObject root)
    {
        if (PrepareSheets() != null) return;

        var rows = new List<RectTransform>();
        var bars = new List<RectTransform>();
        foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (t.name == "RowTemplate") rows.Add(t);
            else if (t.name == "BarTrack") bars.Add(t);
        }
        foreach (var row in rows) DressRow(row);
        foreach (var bar in bars) DressBar(bar);

        // The cards are 900-940 wide, which clips on phones narrower than 9:16.
        var modal = root.transform.Find("Panel/Modal");
        if (modal != null && modal.GetComponent<ModalFit>() == null) modal.gameObject.AddComponent<ModalFit>();

        // Content margins, row layouts, the how-to card and the 35 pt text floor.
        PanelContentFit.Apply(root);

        // The scroll viewport's near-invisible catcher stays a catcher, just fully clear.
        foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (t.name != "Viewport") continue;
            var image = t.GetComponent<Image>();
            if (image != null && image.sprite == null) image.color = Color.clear;
        }
    }

    private static void DressRow(RectTransform row)
    {
        var bg = row.GetComponent<Image>();
        if (bg != null) Slice(bg, Load(LeaderboardSheet, RowSprite), RowSliceScale, Color.white);

        foreach (string markerName in MarkerNames)
        {
            var marker = row.Find(markerName) as RectTransform;
            if (marker == null) continue;
            marker.SetSiblingIndex(0);
            Stretch(marker, Vector2.zero, Vector2.zero);
            var image = marker.GetComponent<Image>();
            if (image != null) Slice(image, Load(LeaderboardSheet, HighlightSprite), RowSliceScale, Color.white);
        }

        var swatch = row.Find("Swatch") as RectTransform;
        if (swatch != null) DressSwatches(row, swatch, row.Find("Swatch2") as RectTransform);
    }

    /// <summary>Seats the swatch (or the pair, for two-colour dust) in a dark inset slab.</summary>
    private static void DressSwatches(RectTransform row, RectTransform swatch, RectTransform swatch2)
    {
        const float frameX = 30f;
        var frameSize = new Vector2(140f, 106f);
        const float pad = 16f;

        var frame = row.Find("SwatchFrame") as RectTransform;
        if (frame == null)
        {
            frame = RunOverlayUI.CreateChild("SwatchFrame", row);
            frame.gameObject.AddComponent<Image>().raycastTarget = false;
        }
        frame.anchorMin = frame.anchorMax = new Vector2(0f, 0.5f);
        frame.pivot = new Vector2(0f, 0.5f);
        frame.anchoredPosition = new Vector2(frameX, 0f);
        frame.sizeDelta = frameSize;
        int markerCount = 0;
        foreach (string markerName in MarkerNames) if (row.Find(markerName) != null) markerCount++;
        frame.SetSiblingIndex(markerCount);
        Slice(frame.GetComponent<Image>(), Load(LeaderboardSheet, FrameSprite), FrameSliceScale, Color.white);

        float innerWidth = frameSize.x - pad * 2f;
        float innerHeight = frameSize.y - pad * 2f;
        float half = swatch2 != null ? innerWidth * 0.5f : innerWidth;
        Seat(swatch, frame, pad, half, innerHeight);
        if (swatch2 != null) Seat(swatch2, frame, pad + half, half, innerHeight);
    }

    private static void Seat(RectTransform swatch, RectTransform frame, float x, float width, float height)
    {
        swatch.SetParent(frame, false);
        swatch.anchorMin = swatch.anchorMax = new Vector2(0f, 0.5f);
        swatch.pivot = new Vector2(0f, 0.5f);
        swatch.anchoredPosition = new Vector2(x, 0f);
        swatch.sizeDelta = new Vector2(width, height);
        var image = swatch.GetComponent<Image>();
        if (image != null) image.preserveAspect = true;
    }

    /// <summary>
    /// Track → (FillArea → BarFill) + Frame + label. The view keeps moving BarFill's right anchor;
    /// FillArea lines the fill sprite up under the frame's window.
    /// </summary>
    private static void DressBar(RectTransform track)
    {
        float height = track.rect.height > 0f ? track.rect.height : track.sizeDelta.y;
        if (height <= 0f) height = 60f;
        float frameScale = BarFrameHeightPx / height;

        var trackImage = track.GetComponent<Image>();
        if (trackImage != null) Object.DestroyImmediate(trackImage);

        var fill = track.Find("BarFill") as RectTransform;
        if (fill == null)
        {
            var area0 = track.Find("FillArea");
            fill = area0 != null ? area0.Find("BarFill") as RectTransform : null;
        }
        if (fill == null) return;

        var area = track.Find("FillArea") as RectTransform;
        if (area == null) area = RunOverlayUI.CreateChild("FillArea", track);
        area.SetSiblingIndex(0);
        Stretch(area, new Vector2(FillLeftPx / frameScale, 0f), new Vector2(-FillRightPx / frameScale, 0f));

        float progress = fill.anchorMax.x;
        fill.SetParent(area, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(progress, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImage = fill.GetComponent<Image>();
        if (fillImage != null) Slice(fillImage, Load(BarSheet, BarFillSprite), frameScale, Color.white);

        var frame = track.Find("Frame") as RectTransform;
        if (frame == null)
        {
            frame = RunOverlayUI.CreateChild("Frame", track);
            frame.gameObject.AddComponent<Image>().raycastTarget = false;
        }
        frame.SetSiblingIndex(1);
        Stretch(frame, Vector2.zero, Vector2.zero);
        Slice(frame.GetComponent<Image>(), Load(BarSheet, BarFrameSprite), frameScale, Color.white);

        // Progress text reads over the frame's window.
        foreach (var label in track.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label.transform.parent != track) continue;
            label.transform.SetAsLastSibling();
            var rect = label.rectTransform;
            Stretch(rect, new Vector2(FillLeftPx / frameScale, 0f), new Vector2(-FillRightPx / frameScale, 0f));
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = Mathf.Max(label.fontSizeMax, label.fontSize);
        }
    }

    internal static void Slice(Image image, Sprite sprite, float scale, Color tint)
    {
        if (image == null || sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = scale;
        image.preserveAspect = false;
        image.color = tint;
        image.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    /// <returns>Null when both sheets are ready, or what is missing.</returns>
    private static string PrepareSheets()
    {
        if (AssetImporter.GetAtPath(LeaderboardSheet) == null) return LeaderboardSheet + " was not found.";
        if (AssetImporter.GetAtPath(BarSheet) == null) return BarSheet + " was not found.";
        SetBorders(LeaderboardSheet, new Dictionary<string, Vector4>
        {
            { RowSprite, RowBorder },
            { HighlightSprite, RowBorder },
        });
        SetBorders(BarSheet, new Dictionary<string, Vector4>
        {
            { BarFrameSprite, BarFrameBorder },
            { BarFillSprite, BarFillBorder },
        });
        foreach (var check in new[] { (LeaderboardSheet, RowSprite), (LeaderboardSheet, HighlightSprite),
                                      (LeaderboardSheet, FrameSprite), (BarSheet, BarFrameSprite), (BarSheet, BarFillSprite) })
        {
            if (Load(check.Item1, check.Item2) == null) return check.Item2 + " was not found in " + check.Item1 + ".";
        }
        return null;
    }

    /// <summary>
    /// Gives slices a 9-slice border and the sheet a Full Rect mesh (sliced sprites need it), through
    /// the sprite editor data so the sheet stays Multiple. Images that draw these slices Simple are
    /// unaffected by a border.
    /// </summary>
    private static void SetBorders(string sheet, Dictionary<string, Vector4> borders)
    {
        var importer = AssetImporter.GetAtPath(sheet) as TextureImporter;
        if (importer == null) return;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        bool changed = false;
        SpriteRect[] rects = provider.GetSpriteRects();
        foreach (SpriteRect rect in rects)
        {
            if (!borders.TryGetValue(rect.name, out Vector4 border) || rect.border == border) continue;
            rect.border = border;
            changed = true;
        }
        if (changed)
        {
            provider.SetSpriteRects(rects);
            provider.Apply();
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            changed = true;
        }

        if (changed) importer.SaveAndReimport();
    }

    internal static Sprite Load(string sheet, string spriteName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sheet))
        {
            if (asset is Sprite sprite && sprite.name == spriteName) return sprite;
        }
        return null;
    }
}
#endif
