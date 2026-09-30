#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using B = DailyUIBuilder;

/// <summary>
/// Generates Assets/Resources/UI/RitualBriefing.prefab — the pre-run popup of the Ritual of
/// The Sun (<see cref="RitualBriefingView"/>), dressed in the house style through
/// <see cref="UIHouseStyle"/> so nothing needs assigning by hand.
///
/// Layout, top to bottom on one stone slab that stretches with the screen width:
///   header    — RITUAL OF THE SUN · date · time left
///   ritual    — name, difficulty suns, tagline
///   THE GOAL  — "Stack 30 stones" + what breaks it
///   OMENS     — one row per rule / modifier / power (cloned from Omen Template)
///   STANDING  — rank + score, or how to enter
///   Back (clay, left) · PLAY (jade, right)
///
/// Icons are read at runtime by file name: rules from the TempleRuleIcons set (the
/// temple pre-play badges), modifiers from Resources/UI/Modifiers/Mod_<Modifier>_Icon, powers
/// from their PowerDefinition. Everything is saved visible for editing.
///
/// Menu: TamalStacker ▸ Daily Challenge ▸ Create Ritual Briefing Prefab
/// </summary>
public static class RitualBriefingPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/UI/RitualBriefing.prefab";
    private const string PipSpritePath = "Assets/Resources/UI/Powers/PowerMedallion_Base.png";

    private const float ModalHeight = 1680f;
    private const float Pad = 60f;

    [MenuItem("TamalStacker/Daily Challenge/Create Ritual Briefing Prefab")]
    public static void CreatePrefab()
    {
        if (!B.ConfirmReplace("Ritual Briefing Prefab", PrefabPath)) return;

        GameObject root = BuildHierarchy();
        B.SavePrefab(root, PrefabPath);

        EditorUtility.DisplayDialog("Ritual Briefing Prefab",
            "Created " + PrefabPath + ".\n\n" + B.StyleNote() + "\n\n" +
            "The Daily now opens with this popup (goal, omens, standing, PLAY). Text in the prefab " +
            "is a layout preview; the real copy comes from localization.\n\n" +
            "Omen icons load by file name: add modifier art as Resources/UI/Modifiers/Mod_<Modifier>_Icon.png " +
            "(e.g. Mod_HotStone_Icon); rules reuse the Resources/UI/TempleRuleIcons set.", "OK");
    }

    private static GameObject BuildHierarchy()
    {
        GameObject root = B.CreateCanvasRoot("RitualBriefing", RitualBriefingView.SortingOrder, true,
            typeof(RitualBriefingView));

        // ── Backdrop ──
        var backdropRect = B.CreateChild("Backdrop", root.transform);
        B.Stretch(backdropRect);
        var backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = RunOverlayUI.Backdrop;
        backdrop.raycastTarget = true;
        UIHouseStyle.ApplyImage(backdrop, UIHouseStyle.Backdrop);

        // ── Modal: stretches across with 40px margins so it fits 20:9 as well as 16:9 ──
        var modal = B.CreateChild("Modal", root.transform);
        modal.anchorMin = new Vector2(0f, 0.5f);
        modal.anchorMax = new Vector2(1f, 0.5f);
        modal.pivot = new Vector2(0.5f, 0.5f);
        modal.offsetMin = new Vector2(40f, -ModalHeight * 0.5f);
        modal.offsetMax = new Vector2(-40f, ModalHeight * 0.5f);
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);

        // ── Header ──
        var eyebrow = B.Label("Eyebrow", modal, UIHouseStyle.Title, "RITUAL OF THE SUN", 32f);
        B.TopRow(eyebrow.rectTransform, 56f, 44f, Pad);
        eyebrow.characterSpacing = 8f;

        var date = B.Label("Date", modal, UIHouseStyle.Body, "September 29, 2026", 26f, TextAlignmentOptions.Left);
        B.TopRow(date.rectTransform, 104f, 40f, Pad);
        date.color = RunOverlayUI.Muted;

        var timeLeft = B.Label("TimeLeft", modal, UIHouseStyle.Body, "Time Left: 05:12:33", 26f, TextAlignmentOptions.Right);
        B.TopRow(timeLeft.rectTransform, 104f, 40f, Pad);
        timeLeft.color = RunOverlayUI.Muted;

        // ── Ritual ──
        var ritualName = B.Label("RitualName", modal, UIHouseStyle.Title, "Wrath of Cabracán", 68f);
        B.TopRow(ritualName.rectTransform, 160f, 120f, Pad);

        var pipRow = B.CreateChild("Difficulty", modal);
        B.TopRow(pipRow, 288f, 44f, Pad);
        var pipLayout = pipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        pipLayout.childAlignment = TextAnchor.MiddleCenter;
        pipLayout.spacing = 14f;
        pipLayout.childControlWidth = false;
        pipLayout.childControlHeight = false;
        pipLayout.childForceExpandWidth = false;
        pipLayout.childForceExpandHeight = false;

        Sprite pipSprite = B.LoadSprite(PipSpritePath);
        var pips = new Image[5];
        for (int i = 0; i < pips.Length; i++)
        {
            var pip = B.CreateChild("Sun_" + (i + 1), pipRow);
            pip.sizeDelta = new Vector2(40f, 40f);
            pips[i] = pip.gameObject.AddComponent<Image>();
            pips[i].sprite = pipSprite;
            pips[i].preserveAspect = true;
            pips[i].raycastTarget = false;
            pips[i].color = i < 4 ? new Color(0.98f, 0.78f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.18f);
        }

        var tagline = B.Label("Tagline", modal, UIHouseStyle.Body, "The earth god wakes. Brace, or be buried.", 30f);
        B.TopRow(tagline.rectTransform, 344f, 80f, Pad);
        tagline.fontStyle = FontStyles.Italic;

        // ── 1 · The goal ──
        var goalHeader = SectionHeader("GoalHeader", modal, "THE GOAL", 452f);

        var goal = B.Label("Goal", modal, UIHouseStyle.Body, "Stack 35 stones", 48f);
        B.TopRow(goal.rectTransform, 496f, 70f, Pad);

        var goalNote = B.Label("GoalNote", modal, UIHouseStyle.Body, "The temple fights back. Every Perfect raises your score.", 26f);
        B.TopRow(goalNote.rectTransform, 566f, 56f, Pad);
        goalNote.color = RunOverlayUI.Muted;

        // ── 2 · Today's omens ──
        var omensHeader = SectionHeader("OmensHeader", modal, "TODAY'S OMENS", 646f);

        var omenList = B.CreateChild("OmenList", modal);
        B.TopRow(omenList, 692f, 600f, Pad - 16f);
        var listLayout = omenList.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.childAlignment = TextAnchor.UpperCenter;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        // The one row the view clones per omen (kept visible here as a layout preview; the view
        // hides it at runtime). Room for five rows, the most a ritual carries.
        RitualOmenRow template = BuildOmenRow(omenList, "Cabracán", "Hold BRACE when it shakes", RunOverlayUI.Jade);
        template.name = "Omen Template";

        // ── 3 · Your standing ──
        var standingHeader = SectionHeader("StandingHeader", modal, "YOUR STANDING", 1316f);

        var standing = B.Label("Standing", modal, UIHouseStyle.Title, "RANK #12", 58f);
        B.TopRow(standing.rectTransform, 1360f, 80f, Pad);

        var standingNote = B.Label("StandingNote", modal, UIHouseStyle.Body, "4,520 points  ·  3-day streak", 28f);
        B.TopRow(standingNote.rectTransform, 1440f, 50f, Pad);

        // ── Buttons ──
        var back = B.StyledButton("Back", modal, UIHouseStyle.Secondary, "Back", out TextMeshProUGUI backLabel);
        B.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(-190f, 100f), new Vector2(340f, 124f));

        var play = B.StyledButton("Play", modal, UIHouseStyle.Primary, "PLAY", out TextMeshProUGUI playLabel);
        B.Place((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(190f, 100f), new Vector2(340f, 124f));
        playLabel.fontSize = Mathf.Max(playLabel.fontSize, 46f);

        // ── Wire the view ──
        var view = root.GetComponent<RitualBriefingView>();
        var so = new SerializedObject(view);
        Set(so, "backdrop", backdrop);
        Set(so, "modal", modal);
        Set(so, "eyebrowLabel", eyebrow);
        Set(so, "dateLabel", date);
        Set(so, "timeLeftLabel", timeLeft);
        Set(so, "ritualNameLabel", ritualName);
        Set(so, "taglineLabel", tagline);
        Set(so, "goalHeaderLabel", goalHeader);
        Set(so, "goalLabel", goal);
        Set(so, "goalNoteLabel", goalNote);
        Set(so, "omensHeaderLabel", omensHeader);
        Set(so, "omenList", omenList);
        Set(so, "omenTemplate", template);
        Set(so, "standingHeaderLabel", standingHeader);
        Set(so, "standingLabel", standing);
        Set(so, "standingNoteLabel", standingNote);
        Set(so, "playButton", play);
        Set(so, "playLabel", playLabel);
        Set(so, "backButton", back);
        Set(so, "backLabel", backLabel);

        SerializedProperty pipProp = so.FindProperty("difficultyPips");
        pipProp.arraySize = pips.Length;
        for (int i = 0; i < pips.Length; i++) pipProp.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static TextMeshProUGUI SectionHeader(string name, RectTransform modal, string text, float top)
    {
        var label = B.Label(name, modal, UIHouseStyle.Title, text, 28f, TextAlignmentOptions.Left);
        B.TopRow(label.rectTransform, top, 40f, Pad);
        label.characterSpacing = 6f;
        return label;
    }

    /// <summary>One omen line: tinted stripe, icon, name over a one-line description.</summary>
    private static RitualOmenRow BuildOmenRow(RectTransform parent, string title, string description, Color accentColor)
    {
        var row = B.CreateChild("Omen", parent);
        var layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 112f;
        layout.minHeight = 112f;

        var band = row.gameObject.AddComponent<Image>();
        band.color = new Color(0f, 0f, 0f, 0.28f);
        band.raycastTarget = false;

        var accentRt = B.CreateChild("Accent", row);
        accentRt.anchorMin = new Vector2(0f, 0f);
        accentRt.anchorMax = new Vector2(0f, 1f);
        accentRt.pivot = new Vector2(0f, 0.5f);
        accentRt.offsetMin = new Vector2(0f, 8f);
        accentRt.offsetMax = new Vector2(10f, -8f);
        var accent = accentRt.gameObject.AddComponent<Image>();
        accent.color = accentColor;
        accent.raycastTarget = false;

        var iconRt = B.CreateChild("Icon", row);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(84f, 84f);
        iconRt.anchoredPosition = new Vector2(68f, 0f);
        var icon = iconRt.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.sprite = B.LoadSprite(PipSpritePath);

        var nameLabel = B.Label("Name", row, UIHouseStyle.Title, title, 36f, TextAlignmentOptions.BottomLeft);
        nameLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        nameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        nameLabel.rectTransform.offsetMin = new Vector2(126f, 0f);
        nameLabel.rectTransform.offsetMax = new Vector2(-20f, -8f);
        nameLabel.color = accentColor;

        var descLabel = B.Label("Description", row, UIHouseStyle.Body, description, 26f, TextAlignmentOptions.TopLeft);
        descLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        descLabel.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        descLabel.rectTransform.offsetMin = new Vector2(126f, 8f);
        descLabel.rectTransform.offsetMax = new Vector2(-20f, -2f);

        var omen = row.gameObject.AddComponent<RitualOmenRow>();
        var so = new SerializedObject(omen);
        Set(so, "accent", accent);
        Set(so, "icon", icon);
        Set(so, "nameLabel", nameLabel);
        Set(so, "descriptionLabel", descLabel);
        so.ApplyModifiedPropertiesWithoutUndo();

        return omen;
    }

    private static void Set(SerializedObject so, string property, Object value)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[RitualBriefingPrefabSetup] No field '{property}' on {so.targetObject.GetType().Name}.");
            return;
        }
        p.objectReferenceValue = value;
    }
}
#endif
