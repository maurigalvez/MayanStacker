#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sets up the temple-rule previews in one go:
///  - the art is the already-sliced sheet Art/UI/MayanStacker_Effects_Icons (rule badges are picked
///    in Resources/UI/TempleRuleIcons);
///  - one map effect prefab per rule at Resources/UI/MapRuleFx/MapRuleFx_&lt;Rule&gt;, played on the
///    selected temple on the level map (existing ones are kept unless you choose Replace);
///  - the rule strip and the locked-card parts added to the existing, already-styled
///    Resources/UI/PowerLoadoutScreen prefab. Nothing already in it is moved or restyled; parts it
///    already has are left alone.
///
/// Menu: TamalStacker ▸ Temples ▸ Set Up Rule Previews. Safe to run again.
/// </summary>
public static class TempleRulePreviewSetup
{
    private const string DialogTitle = "Rule Previews";
    private const string FxSheetPath = "Assets/Art/UI/MayanStacker_Effects_Icons.png";
    private const string FxPrefabFolder = "Assets/Resources/UI/MapRuleFx";
    private const string LoadoutPrefabPath = "Assets/Resources/" + PowerLoadoutView.PrefabResourcePath + ".prefab";

    private static readonly LevelRule[] Rules =
    {
        LevelRule.Earthquake, LevelRule.RainSlick, LevelRule.Eclipse, LevelRule.JungleWind, LevelRule.RisingCenote
    };

    [MenuItem("TamalStacker/Temples/Set Up Rule Previews")]
    public static void SetUp()
    {
        var report = new StringBuilder();

        bool replace = false;
        bool anyExisting = false;
        foreach (LevelRule rule in Rules)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FxPrefabPath(rule)) != null) anyExisting = true;
        }
        if (anyExisting)
        {
            replace = EditorUtility.DisplayDialog(DialogTitle,
                "Some map effect prefabs already exist in " + FxPrefabFolder + ".\n\n" +
                "Replace them with fresh ones? Any tuning done to them will be lost.",
                "Replace", "Keep mine");
        }

        EnsureFolder(FxPrefabFolder);
        foreach (LevelRule rule in Rules) BuildFxPrefab(rule, replace, report);

        AddLoadoutParts(report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(DialogTitle, report.ToString() +
            "\nTune an effect: open its prefab, select the root, and edit the MapRuleFx motes " +
            "(positions are in the 200×200 reference box; Size In Buttons scales the whole effect).\n" +
            "Effect art comes from the slices of " + FxSheetPath + ".",
            "OK");
    }

    // ---- Map effect prefabs ----

    private static string FxPrefabPath(LevelRule rule) => FxPrefabFolder + "/MapRuleFx_" + rule + ".prefab";

    // Slices of the effects sheet, by what they are.
    private const string FxDust = "MayanStacker_Effects_Icons_12";
    private const string FxRainStreak = "MayanStacker_Effects_Icons_11";
    private const string FxBubble = "MayanStacker_Effects_Icons_13";
    private const string FxLeaf = "MayanStacker_Effects_Icons_10";
    private const string FxWater = "MayanStacker_Effects_Icons_14";
    private const string FxSun = "MayanStacker_Effects_Icons_15";
    private const string FxMoon = "MayanStacker_Effects_Icons_16";
    private const string FxGust = "MayanStacker_Effects_Icons_17";

    private static Sprite FxSprite(string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(FxSheetPath))
        {
            if (asset is Sprite sprite && sprite.name == name) return sprite;
        }
        Debug.LogWarning("TempleRulePreviewSetup: no sprite " + name + " in " + FxSheetPath);
        return null;
    }

    private static void BuildFxPrefab(LevelRule rule, bool replace, StringBuilder report)
    {
        string path = FxPrefabPath(rule);
        if (!replace && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            report.AppendLine("Kept " + path + ".");
            return;
        }

        var root = new GameObject("MapRuleFx_" + rule, typeof(RectTransform));
        try
        {
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(200f, 200f);
            // Its own canvas: moving the motes re-batches only this effect, not the whole map.
            root.AddComponent<Canvas>();

            var fx = root.AddComponent<MapRuleFx>();
            var motes = new List<MapRuleFx.Mote>();

            switch (rule)
            {
                case LevelRule.Earthquake: BuildQuake(fx, motes); break;
                case LevelRule.RainSlick: BuildRain(fx, motes); break;
                case LevelRule.JungleWind: BuildWind(fx, motes); break;
                case LevelRule.RisingCenote: BuildCenote(fx, motes); break;
                case LevelRule.Eclipse: BuildEclipse(fx, motes); break;
            }

            fx.motes = motes.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            report.AppendLine(success ? "Created " + path + "." : "FAILED to save " + path + ".");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static MapRuleFx.Mote Mote(Transform parent, string name, string sprite, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        var image = go.AddComponent<Image>();
        image.sprite = FxSprite(sprite);
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return new MapRuleFx.Mote { target = rt, graphic = image };
    }

    private static void BuildQuake(MapRuleFx fx, List<MapRuleFx.Mote> motes)
    {
        fx.shakeTemple = true;
        fx.shakeAmplitude = 8f;
        fx.shakeEvery = 2.2f;
        fx.shakeLength = 0.45f;

        // Dust kicked up at the temple's base on each burst (same period as the shake).
        Color dust = new Color(0.86f, 0.74f, 0.58f, 0.9f);
        float[] xs = { -48f, 0f, 48f };
        for (int i = 0; i < xs.Length; i++)
        {
            MapRuleFx.Mote m = Mote(fx.transform, "Dust" + i, FxDust, new Vector2(46f, 46f), dust);
            m.from = new Vector2(xs[i], -64f);
            m.to = new Vector2(xs[i] * 1.35f, -48f);
            m.period = 2.2f;
            m.activeShare = 0.4f;
            m.scaleFrom = 0.4f;
            m.scaleTo = 1.4f;
            m.alphaFrom = 1f;
            m.alphaTo = 0f;
            m.fadeIn = 0.05f;
            m.fadeOut = 0f;
            motes.Add(m);
        }
    }

    private static void BuildRain(MapRuleFx fx, List<MapRuleFx.Mote> motes)
    {

        Color rain = new Color(0.78f, 0.9f, 1f, 0.85f);
        float[] xs = { -80f, -50f, -20f, 10f, 40f, 70f, 95f };
        float[] phases = { 0f, 0.55f, 0.25f, 0.8f, 0.4f, 0.1f, 0.65f };
        for (int i = 0; i < xs.Length; i++)
        {
            MapRuleFx.Mote m = Mote(fx.transform, "Streak" + i, FxRainStreak, new Vector2(6f, 30f), rain);
            m.target.localRotation = Quaternion.Euler(0f, 0f, -12f); // trail leans back up the fall
            m.from = new Vector2(xs[i] + 20f, 100f);
            m.to = new Vector2(xs[i] - 20f, -90f);
            m.period = 0.7f;
            m.phase = phases[i];
            m.fadeIn = 0.1f;
            m.fadeOut = 0.15f;
            motes.Add(m);
        }
    }

    private static void BuildWind(MapRuleFx fx, List<MapRuleFx.Mote> motes)
    {
        Color gust = new Color(1f, 1f, 1f, 0.8f); // the art is already coloured
        float[] gustYs = { 40f, -20f };
        for (int i = 0; i < gustYs.Length; i++)
        {
            MapRuleFx.Mote m = Mote(fx.transform, "Gust" + i, FxGust, new Vector2(70f, 32f), gust);
            m.from = new Vector2(-90f, gustYs[i]);
            m.to = new Vector2(60f, gustYs[i]);
            m.period = 1.6f;
            m.phase = i * 0.5f;
            m.fadeIn = 0.3f;
            m.fadeOut = 0.3f;
            motes.Add(m);
        }

        float[] leafYs = { 60f, 10f, -40f };
        for (int i = 0; i < leafYs.Length; i++)
        {
            MapRuleFx.Mote m = Mote(fx.transform, "Leaf" + i, FxLeaf, new Vector2(24f, 24f), Color.white);
            m.from = new Vector2(-110f, leafYs[i]);
            m.to = new Vector2(110f, leafYs[i] - 12f);
            m.period = 2.4f;
            m.phase = i / 3f;
            m.wobble = 12f;
            m.wobbleCycles = 1.5f;
            m.spin = -360f;
            m.fadeIn = 0.12f;
            m.fadeOut = 0.12f;
            motes.Add(m);
        }
    }

    private static void BuildCenote(MapRuleFx fx, List<MapRuleFx.Mote> motes)
    {
        // Back wave first (drawn behind), then the front wave, then bubbles.
        MapRuleFx.Mote back = Mote(fx.transform, "WaterBack", FxWater, new Vector2(220f, 82f), new Color(0.62f, 0.8f, 0.9f, 0.6f));
        back.target.GetComponent<Image>().preserveAspect = false;
        back.from = new Vector2(10f, -100f);
        back.to = new Vector2(10f, -76f);
        back.period = 3f;
        back.phase = 0.5f;
        back.pingPong = true;
        back.smooth = true;
        motes.Add(back);

        MapRuleFx.Mote front = Mote(fx.transform, "WaterFront", FxWater, new Vector2(220f, 82f), new Color(1f, 1f, 1f, 0.85f));
        front.target.GetComponent<Image>().preserveAspect = false;
        front.from = new Vector2(0f, -106f);
        front.to = new Vector2(0f, -70f);
        front.period = 3f;
        front.pingPong = true;
        front.smooth = true;
        motes.Add(front);

        float[] xs = { -40f, 15f, 55f };
        for (int i = 0; i < xs.Length; i++)
        {
            MapRuleFx.Mote m = Mote(fx.transform, "Bubble" + i, FxBubble, new Vector2(12f, 12f), new Color(1f, 1f, 1f, 0.8f));
            m.from = new Vector2(xs[i], -84f);
            m.to = new Vector2(xs[i] + 4f, -24f);
            m.period = 1.8f;
            m.phase = i / 3f;
            m.wobble = 3f;
            m.wobbleCycles = 2f;
            m.fadeIn = 0.1f;
            m.fadeOut = 0.3f;
            motes.Add(m);
        }
    }

    private static void BuildEclipse(MapRuleFx fx, List<MapRuleFx.Mote> motes)
    {
        const float period = 4f;

        // The temple dims as the moon covers the sun, in step with it.
        MapRuleFx.Mote shade = Mote(fx.transform, "Shade", FxDust, new Vector2(170f, 170f), new Color(0.05f, 0.03f, 0.1f, 1f));
        shade.from = shade.to = Vector2.zero;
        shade.period = period;
        shade.pingPong = true;
        shade.smooth = true;
        shade.alphaFrom = 0f;
        shade.alphaTo = 0.55f;
        motes.Add(shade);

        MapRuleFx.Mote sun = Mote(fx.transform, "Sun", FxSun, new Vector2(60f, 60f), Color.white);
        sun.from = sun.to = new Vector2(0f, 80f);
        sun.period = period;
        sun.fadeIn = 0f;
        sun.fadeOut = 0f;
        motes.Add(sun);

        MapRuleFx.Mote moon = Mote(fx.transform, "Moon", FxMoon, new Vector2(52f, 52f), Color.white);
        moon.from = new Vector2(-60f, 92f);
        moon.to = new Vector2(0f, 80f);
        moon.period = period;
        moon.pingPong = true;
        moon.smooth = true;
        moon.alphaFrom = 0f;
        moon.alphaTo = 1f;
        motes.Add(moon);
    }

    // ---- Loadout prefab parts ----

    private static void AddLoadoutParts(StringBuilder report)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LoadoutPrefabPath) == null)
        {
            report.AppendLine("No " + LoadoutPrefabPath + " - create it with TamalStacker ▸ Powers ▸ " +
                              "Create Loadout Screen Prefab (it already includes the rule strip and lock parts).");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(LoadoutPrefabPath);
        try
        {
            var view = contents.GetComponentInChildren<PowerLoadoutView>(true);
            if (view == null)
            {
                report.AppendLine(LoadoutPrefabPath + " has no PowerLoadoutView; left untouched.");
                return;
            }

            var so = new SerializedObject(view);
            bool changed = false;

            SerializedProperty sectionProp = so.FindProperty("rulesSection");
            SerializedProperty entryProp = so.FindProperty("ruleEntryTemplate");
            if (sectionProp.objectReferenceValue == null || entryProp.objectReferenceValue == null)
            {
                var panelObj = so.FindProperty("panel").objectReferenceValue as GameObject;
                var panel = (RectTransform)(panelObj != null ? panelObj.transform : view.transform);

                GameObject strip = PowerLoadoutView.BuildRuleStrip(panel, out TempleRuleBriefEntry entry);
                StyleRuleStrip(strip, entry);
                sectionProp.objectReferenceValue = strip;
                entryProp.objectReferenceValue = entry;
                changed = true;
                report.AppendLine("Added the rule strip above the loadout panel (Panel/RulesSection).");
            }
            else
            {
                report.AppendLine("Loadout screen already has a rule strip; left as is.");
            }

            var cardTemplate = so.FindProperty("cardTemplate").objectReferenceValue as PowerLoadoutCard;
            if (cardTemplate != null && cardTemplate.lockedMarker == null && cardTemplate.lockText == null)
            {
                PowerLoadoutView.BuildLockParts(cardTemplate);
                StyleLockText(cardTemplate.lockText);
                EditorUtility.SetDirty(cardTemplate);
                changed = true;
                report.AppendLine("Added the lock badge and \"Beat Level N\" line to the card template.");
            }
            else if (cardTemplate != null)
            {
                report.AppendLine("Card template already has lock parts; left as is.");
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, LoadoutPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void StyleRuleStrip(GameObject strip, TempleRuleBriefEntry entry)
    {
        var slab = strip.GetComponent<Image>();
        if (slab != null)
        {
            UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);
            slab.raycastTarget = false;
        }

        if (entry.nameText != null)
        {
            UIHouseStyle.ApplyLabel(entry.nameText, UIHouseStyle.Title);
            entry.nameText.fontSize = 40f;
            entry.nameText.enableAutoSizing = false;
            entry.nameText.alignment = TextAlignmentOptions.BottomLeft;
        }

        if (entry.shortText != null)
        {
            UIHouseStyle.ApplyLabel(entry.shortText, UIHouseStyle.Body);
            entry.shortText.fontSize = 28f;
            entry.shortText.enableAutoSizing = false;
            entry.shortText.alignment = TextAlignmentOptions.TopLeft;
        }
    }

    private static void StyleLockText(TextMeshProUGUI label)
    {
        if (label == null) return;
        UIHouseStyle.ApplyLabel(label, UIHouseStyle.Title);
        label.fontSize = 26f;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
#endif
