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
/// Also bakes two sprites it needs, once, into Resources/UI/Powers — a sun burst and a
/// shockwave ring — so they are ordinary assets you can repaint or swap. Both are drawn like
/// the game's icons: a flat white fill (tinted with the power's colour at runtime), one hard cel
/// shadow and a thick dark ink outline, no soft gradients. The medallion reuses the power
/// meter's base and ring; the name plate and type come from the house style
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
    private const string BurstPath = SpriteFolder + "/PowerShowcase_Burst.png";
    private const string ShockwavePath = SpriteFolder + "/PowerShowcase_Shockwave.png";

    // Same dark brown ink as the medallion ring and the effect icons.
    private static readonly Color32 Ink = new Color32(30, 20, 14, 255);
    // The shadowed side of each sun point, before the accent tint multiplies it.
    private static readonly Color32 CelShadow = new Color32(196, 196, 196, 255);
    private const string DialogTitle = "Power Showcase";

    // The stage is re-centred on the power button every time it plays (see PowerShowcaseView);
    // this is only where it sits in the prefab view: the default bottom-left button
    // (125,125 from the corner of the 1080x1920 canvas). Everything is sized to stay in that
    // corner, clear of the tower top and the held stone the power is acting on.
    private static readonly Vector2 StagePosition = new Vector2(-415f, -835f);
    private static readonly Vector2 StageSize = new Vector2(600f, 600f);

    // A touch bigger than the 160 button, so it reads as the button leaping up.
    private const float MedallionSize = 200f;
    private const float MedallionY = 0f;
    // Authored for a left-hand button: the plate sits just above the medallion and unrolls
    // from its left edge toward the centre; the view mirrors it for a right-hand button.
    private static readonly Vector2 PlateSize = new Vector2(440f, 96f);
    private const float PlateLeft = -MedallionSize * 0.5f + 8f;
    private const float PlateY = MedallionSize * 0.5f + 60f;
    private const float NameFontSize = 48f;

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

        Sprite burstSprite = EnsureSprite(BurstPath, BakeBurst);
        Sprite shockwaveSprite = EnsureSprite(ShockwavePath, BakeShockwave);

        GameObject root = BuildHierarchy(burstSprite, shockwaveSprite);
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
            "Burst and shockwave sprites: " + SpriteFolder + " (white fill and dark ink; the fill " +
            "takes each power's accent at runtime). Repaint them freely.\n\n" +
            "Timing, opacities and the slam are on the root's PowerShowcaseView. 'Stage' is " +
            "re-centred on the power button each time it plays; lay its children out around a " +
            "left-hand button (it mirrors for a right-hand one).\n\n" +
            "Keep this prefab: it is the only source of the showcase.", "OK");
    }

    private static GameObject BuildHierarchy(Sprite burstSprite, Sprite shockwaveSprite)
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
        Image rays = AddImage("Burst", stage, burstSprite, RunOverlayUI.Gold, burstCentre, new Vector2(420f, 420f));
        Image shockwave = AddImage("Shockwave", stage, shockwaveSprite, RunOverlayUI.Gold, burstCentre,
            new Vector2(MedallionSize, MedallionSize));

        Sprite ringSprite = Resources.Load<Sprite>(PowerMeterView.RingSpritePath);
        Sprite baseSprite = Resources.Load<Sprite>(PowerMeterView.BaseSpritePath);

        // Name plate: the house slab. Pivot on its button-side edge, so it unrolls out of the button.
        Image plateImage = AddImage("Plate", stage, null, Color.white, Vector2.zero, PlateSize);
        UIHouseStyle.ApplyImage(plateImage, UIHouseStyle.Modal);
        plateImage.raycastTarget = false;
        RectTransform plate = plateImage.rectTransform;
        plate.pivot = new Vector2(0f, 0.5f);
        plate.anchoredPosition = new Vector2(PlateLeft, PlateY);
        var plateGroup = plate.gameObject.AddComponent<CanvasGroup>();
        plateGroup.interactable = false;
        plateGroup.blocksRaycasts = false;

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
        view.Bind(canvas, stage, stageGroup, rays, shockwave,
            medallion, medallionGroup, ring, icon,
            plate, plateGroup, nameLabel);

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

    // ---- Burst: a carved sun, like the Eclipse sun on the effect icon sheet ----

    private const int BurstPoints = 12;          // long and short in turn
    private const float BurstLongTip = 0.97f;
    private const float BurstShortTip = 0.76f;
    private const float BurstValley = 0.58f;
    private const float InkWidth = 0.042f;       // of the half-size: ~9px at 420 on screen

    /// <summary>
    /// A sun of chunky triangular points in a thick ink outline. White fill with one hard cel
    /// shadow down one side of every point; the ink is baked dark so the accent tint leaves it
    /// dark, which keeps the burst readable on the Day, Sunset and Night skies.
    /// </summary>
    private static Color32[] BakeBurst(int size)
    {
        var poly = new Vector2[BurstPoints * 2];
        for (int i = 0; i < BurstPoints; i++)
        {
            float tip = i * 2f * Mathf.PI / BurstPoints;
            float valley = (i + 0.5f) * 2f * Mathf.PI / BurstPoints;
            float reach = i % 2 == 0 ? BurstLongTip : BurstShortTip;
            poly[i * 2] = new Vector2(Mathf.Cos(tip), Mathf.Sin(tip)) * reach;
            poly[i * 2 + 1] = new Vector2(Mathf.Cos(valley), Mathf.Sin(valley)) * BurstValley;
        }

        float aa = 1.5f / (size * 0.5f);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float d = EdgeDistance(p, poly);
                if (!Inside(p, poly)) d = -d;

                float alpha = Mathf.Clamp01(d / aa + 0.5f);
                if (alpha <= 0f) continue;

                // The shadow side: everything clockwise of each point's spine.
                float seg = (Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI) + 1f) * BurstPoints;
                float off = (seg - Mathf.Round(seg)) * (2f * Mathf.PI / BurstPoints) * p.magnitude;
                float lit = Mathf.Clamp01(off / aa + 0.5f);
                Color32 fill = Color32.Lerp(CelShadow, new Color32(255, 255, 255, 255), lit);

                float ink = Mathf.Clamp01((InkWidth - d) / aa + 0.5f);
                Color32 c = Color32.Lerp(fill, Ink, ink);
                c.a = (byte)(alpha * 255f);
                px[y * size + x] = c;
            }
        }
        return px;
    }

    /// <summary>A flat ring with ink on both edges; the fill takes the accent.</summary>
    private static Color32[] BakeShockwave(int size)
    {
        const float inner = 0.76f, outer = 0.97f, ink = 0.055f;
        float aa = 1.5f / (size * 0.5f);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float d = Mathf.Min(r - inner, outer - r); // distance in from the nearer edge

                float alpha = Mathf.Clamp01(d / aa + 0.5f);
                if (alpha <= 0f) continue;

                Color32 c = Color32.Lerp(new Color32(255, 255, 255, 255), Ink, Mathf.Clamp01((ink - d) / aa + 0.5f));
                c.a = (byte)(alpha * 255f);
                px[y * size + x] = c;
            }
        }
        return px;
    }

    private static float EdgeDistance(Vector2 p, Vector2[] poly)
    {
        float best = float.MaxValue;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
        }
        return best;
    }

    private static bool Inside(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
