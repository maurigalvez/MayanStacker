#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Resources/UI/PowerShowcase.prefab — the pop-up that names a power the
/// moment it fires (see <see cref="PowerShowcaseView"/>).
///
/// Required: there is no code-built fallback, so without this prefab nothing is shown when a
/// power fires (the power itself still works).
///
/// Also bakes two plain white sprites it needs, once, into Resources/UI/Powers — the sun-ray
/// burst and a soft glow — so they are ordinary assets you can repaint or swap. The medallion
/// reuses the power meter's base and ring; the name plate and type come from the house style
/// (<see cref="UIHouseStyle"/>).
///
/// Non-destructive: it refuses to overwrite an existing prefab unless you confirm, and never
/// overwrites the baked sprites.
///
/// Menu: TamalStacker ▸ Powers ▸ Create Power Showcase Prefab
/// </summary>
public static class PowerShowcasePrefabSetup
{
    private const string PrefabPath = "Assets/Resources/" + PowerShowcaseView.PrefabResourcePath + ".prefab";
    private const string SpriteFolder = "Assets/Resources/UI/Powers";
    private const string RaysPath = SpriteFolder + "/PowerShowcase_Rays.png";
    private const string GlowPath = SpriteFolder + "/PowerShowcase_Glow.png";
    private const string DialogTitle = "Power Showcase";

    // Upper middle of the 1080x1920 canvas: clear of the power button and the Guide Lane
    // below, and of the swinging stone at the top.
    private static readonly Vector2 StagePosition = new Vector2(0f, 120f);
    private static readonly Vector2 StageSize = new Vector2(900f, 640f);

    private const float MedallionSize = 240f;
    private const float MedallionY = 70f;
    private static readonly Vector2 PlateSize = new Vector2(620f, 120f);
    private const float PlateY = -140f;
    private const float NameFontSize = 60f;

    [MenuItem("TamalStacker/Powers/Create Power Showcase Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog(DialogTitle,
                PrefabPath + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                "Replace", "Cancel"))
        {
            return;
        }

        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");
        EnsureFolder("Assets/Resources/UI", "Powers");

        Sprite raysSprite = EnsureSprite(RaysPath, BakeRays);
        Sprite glowSprite = EnsureSprite(GlowPath, BakeGlow);

        GameObject root = BuildHierarchy(raysSprite, glowSprite);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        string styleNote = UIHouseStyle.HasReference
            ? "Plate and type styled from " + UIHouseStyle.ReferencePrefabPath + "."
            : UIHouseStyle.ReferencePrefabPath + " was not found, so the MainMenu_UI sheet defaults were used.";

        EditorUtility.DisplayDialog(DialogTitle,
            "Created " + PrefabPath + ".\n\n" + styleNote + "\n\n" +
            "Rays and glow sprites: " + SpriteFolder + " (white; tinted with each power's accent " +
            "at runtime). Repaint them freely.\n\n" +
            "Timing, opacities and the slam are on the root's PowerShowcaseView. Move 'Stage' to " +
            "move the whole showcase.\n\n" +
            "Keep this prefab: it is the only source of the showcase.", "OK");
    }

    private static GameObject BuildHierarchy(Sprite raysSprite, Sprite glowSprite)
    {
        var root = new GameObject("PowerShowcase", typeof(Canvas), typeof(CanvasScaler));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // No GraphicRaycaster: the showcase must never swallow the tap that drops a stone.
        // Just above the power meter's canvas, which it rises out of.
        canvas.sortingOrder = PowerSettings.Current != null ? PowerSettings.Current.canvasSortingOrder + 5 : 3015;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        Vector2 centre = new Vector2(0.5f, 0.5f);

        RectTransform stage = RunOverlayUI.CreateChild("Stage", root.transform);
        RunOverlayUI.Place(stage, centre, StagePosition, StageSize);
        var stageGroup = stage.gameObject.AddComponent<CanvasGroup>();
        stageGroup.interactable = false;
        stageGroup.blocksRaycasts = false;

        Vector2 burstCentre = new Vector2(0f, MedallionY);

        Color stone = RunOverlayUI.Stone;
        Image shade = AddImage("Shade", stage, glowSprite, new Color(stone.r, stone.g, stone.b, 1f), burstCentre, new Vector2(760f, 760f));
        Image glow = AddImage("Glow", stage, glowSprite, RunOverlayUI.Gold, burstCentre, new Vector2(620f, 620f));
        Image rays = AddImage("Rays", stage, raysSprite, RunOverlayUI.Gold, burstCentre, new Vector2(820f, 820f));

        Sprite ringSprite = Resources.Load<Sprite>(PowerMeterView.RingSpritePath);
        Sprite baseSprite = Resources.Load<Sprite>(PowerMeterView.BaseSpritePath);

        Image shockwave = AddImage("Shockwave", stage, ringSprite != null ? ringSprite : glowSprite,
            RunOverlayUI.Gold, burstCentre, new Vector2(MedallionSize, MedallionSize));

        // Light lines out of the plate's ends; pivots on the plate edge so they grow outward.
        float plateHalf = PlateSize.x * 0.5f;
        RectTransform sparkLeft = AddSpark("SparkLeft", stage, glowSprite, new Vector2(-plateHalf + 20f, PlateY), 1f);
        RectTransform sparkRight = AddSpark("SparkRight", stage, glowSprite, new Vector2(plateHalf - 20f, PlateY), 0f);

        // Name plate: the house slab, clipped so the glint stays on the stone.
        Image plateImage = AddImage("Plate", stage, null, Color.white, new Vector2(0f, PlateY), PlateSize);
        UIHouseStyle.ApplyImage(plateImage, UIHouseStyle.Modal);
        plateImage.raycastTarget = false;
        RectTransform plate = plateImage.rectTransform;
        var plateGroup = plate.gameObject.AddComponent<CanvasGroup>();
        plateGroup.interactable = false;
        plateGroup.blocksRaycasts = false;
        plate.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI nameLabel = RunOverlayUI.CreateLabel("Name", plate, "Jaguar Slam", NameFontSize, RunOverlayUI.Gold);
        RunOverlayUI.Stretch(nameLabel.rectTransform);
        nameLabel.rectTransform.offsetMin = new Vector2(36f, 10f);
        nameLabel.rectTransform.offsetMax = new Vector2(-36f, -10f);
        UIHouseStyle.ApplyLabel(nameLabel, UIHouseStyle.Title);
        nameLabel.fontSize = NameFontSize;
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMax = NameFontSize;
        nameLabel.fontSizeMin = NameFontSize * 0.55f;
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
        nameLabel.margin = Vector4.zero;
        nameLabel.raycastTarget = false;

        // The glint runs over the name, not under it.
        Image shineImage = AddImage("Shine", plate, glowSprite, new Color(1f, 0.97f, 0.85f, 0.55f), Vector2.zero, new Vector2(80f, 240f));
        shineImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);

        // Medallion: the meter's own disc and ring, so it reads as the button leaping up.
        Image medallionImage = AddImage("Medallion", stage, baseSprite, baseSprite != null ? Color.white : stone,
            burstCentre, new Vector2(MedallionSize, MedallionSize));
        medallionImage.preserveAspect = true;
        RectTransform medallion = medallionImage.rectTransform;
        var medallionGroup = medallion.gameObject.AddComponent<CanvasGroup>();
        medallionGroup.interactable = false;
        medallionGroup.blocksRaycasts = false;

        Image ring = AddImage("Ring", medallion, ringSprite, RunOverlayUI.Gold, Vector2.zero, Vector2.zero);
        RunOverlayUI.Stretch(ring.rectTransform);
        ring.enabled = ringSprite != null;

        Image icon = AddImage("Icon", medallion, Resources.Load<Sprite>(PowerDefinition.IconResourcePath(PowerId.JaguarSlam)),
            Color.white, Vector2.zero, Vector2.zero);
        RunOverlayUI.Stretch(icon.rectTransform);
        // Same inset as the meter's icon, scaled to this medallion.
        float k = MedallionSize / 160f;
        icon.rectTransform.offsetMin = new Vector2(34f, 32f) * k;
        icon.rectTransform.offsetMax = new Vector2(-34f, -36f) * k;
        icon.preserveAspect = true;

        var view = root.AddComponent<PowerShowcaseView>();
        view.Bind(canvas, stage, stageGroup, shade, glow, rays, shockwave,
            medallion, medallionGroup, ring, icon,
            plate, plateGroup, nameLabel, shineImage.rectTransform, sparkLeft, sparkRight);

        return root;
    }

    private static Image AddImage(string name, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size)
    {
        RectTransform rt = RunOverlayUI.CreateChild(name, parent);
        RunOverlayUI.Place(rt, new Vector2(0.5f, 0.5f), position, size);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform AddSpark(string name, Transform parent, Sprite sprite, Vector2 position, float pivotX)
    {
        Image image = AddImage(name, parent, sprite, new Color(1f, 0.9f, 0.6f, 0.9f), position, new Vector2(300f, 14f));
        RectTransform rt = image.rectTransform;
        rt.pivot = new Vector2(pivotX, 0.5f);
        return rt;
    }

    // ---- Baked sprites ----

    private delegate Color32[] Baker(int size);

    private static Sprite EnsureSprite(string path, Baker bake)
    {
        if (!File.Exists(path))
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(bake(size));
            tex.Apply(false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Sixteen tapering sun rays, long and short in turn, white on transparent.</summary>
    private static Color32[] BakeRays(int size)
    {
        const int count = 16;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);

                float seg = (Mathf.Atan2(ny, nx) / (2f * Mathf.PI) + 1f) * count;
                int index = Mathf.RoundToInt(seg) % count;
                float offset = Mathf.Abs(seg - Mathf.Round(seg)); // 0..0.5 from the ray's spine

                float length = index % 2 == 0 ? 1f : 0.7f;
                float halfWidth = 0.2f * Mathf.Clamp01(1f - 0.75f * r / length);
                float beam = 1f - Mathf.SmoothStep(halfWidth * 0.55f, halfWidth, offset);
                float reach = 1f - Mathf.SmoothStep(0.3f * length, length, r);
                float core = Mathf.SmoothStep(0.08f, 0.22f, r); // the medallion covers the middle

                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(beam * reach * core) * 255f));
            }
        }
        return px;
    }

    /// <summary>A soft round glow, white on transparent.</summary>
    private static Color32[] BakeGlow(int size)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny));
                float a = 1f - Mathf.SmoothStep(0f, 1f, r);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        }
        return px;
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
