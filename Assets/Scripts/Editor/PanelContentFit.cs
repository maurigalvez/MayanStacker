#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lays out the content of the panels the main-menu badges open (Jade Market, Missions, Path,
/// Expedition, Chest) so it breathes inside its stone card, and gives each one a way in:
///   • side margin — the list, rows and headings sit <see cref="SideInset"/> in from the card's
///     border on both sides (about a quarter of the card's width gone to margin in total), where
///     they used to sit 50 px from it and crowd the carved frame
///   • rows reflowed to the narrower width, taller where the copy needs a second line
///   • a "how this works" card at the top of every list (<see cref="PanelHowTo"/>), so the player is
///     told what the feature is and exactly how they take part
///   • the Market's own jade balance bar, which plays every purchase, and the purchase flash,
///     "Unlocked!" banner and theme hint on its rows
///   • every label at the 35 pt floor, auto-size floor included
///
/// Parts are found by the names the generators give them and everything is set to absolute values,
/// so it is safe to run again. <see cref="SpinsPanelStyle.Dress"/> calls it; TamalStacker ▸ UI ▸
/// Restyle Spins Panels re-runs it on the prefabs already in Resources/UI.
/// </summary>
public static class PanelContentFit
{
    /// <summary>Space between the card's border and the content, each side, in canvas units.</summary>
    public const float SideInset = 120f;

    private const string HowToName = "HowToCard";
    private const float MarketTop = 290f;           // below the title and the jade balance bar
    private const float MarketBarTop = 145f;
    private const int MarketFlightSorting = 4400;   // over the market panel (4300)
    // Tall enough to hold every Missions and Expedition row plus the how-to card (those lists don't
    // scroll), and short of the 1920 canvas so the card still has a margin top and bottom.
    private const float ListModalHeight = 1780f;
    private const float ListBottom = 180f;          // just above the close button

    // Reading sizes for these cards. 35 is the floor, not the target: the Maya fonts are narrow and
    // ModalFit shrinks the card on tall phones, so copy at the floor reads tiny.
    private const float RowTextSize = 44f;
    private const float RowHeadingSize = 46f;
    private const float RewardTextSize = 48f;
    private const float StatusTextSize = 44f;
    private const float HowToHeadlineSize = 48f;
    private const float HowToBodySize = 42f;
    private const float HowToExtraSize = 40f;

    private static readonly Color FlashTint = new Color(1f, 0.85f, 0.4f, 0f);

    public static void Apply(GameObject root)
    {
        var modal = root.transform.Find("Panel/Modal") as RectTransform;
        if (modal == null) return;

        var market = root.GetComponent<JadeMarketView>();
        var missions = root.GetComponent<MissionsView>();
        var path = root.GetComponent<PathView>();
        var expedition = root.GetComponent<ExpeditionView>();

        if ((missions != null || expedition != null || path != null) && modal.sizeDelta.y < ListModalHeight)
            modal.sizeDelta = new Vector2(modal.sizeDelta.x, ListModalHeight);

        float inner = modal.sizeDelta.x - SideInset * 2f;
        InsetChildren(modal, inner, market != null);

        if (missions != null) FitMissionsHeader(modal);
        if (path != null) FitPathHeader(modal);
        if (expedition != null) FitExpeditionHeader(modal, inner);

        if (market != null)
        {
            EnsureMarketBar(market, modal);
            FitMarketRow(root, inner);
        }
        if (missions != null) FitMissionsRow(root, inner);
        if (path != null) FitPathRow(root, inner);
        if (expedition != null) FitExpeditionRow(root, inner);

        if (market != null) AddHowTo(market, "content", "market_howto_title", "market_howto_body", false);
        if (missions != null) AddHowTo(missions, "content", "missions_howto_title", "missions_howto_body", false);
        if (expedition != null) AddHowTo(expedition, "content", "expedition_howto_title", "expedition_howto_body", false);
        if (path != null)
        {
            var card = AddHowTo(path, "content", "path_howto_title", "path_howto_body", true);
            var so = new SerializedObject(path);
            so.FindProperty("howTo").objectReferenceValue = card;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) UIHouseStyle.EnforceMinFontSize(label);
    }

    // ── Margins ──

    private static void InsetChildren(RectTransform modal, float inner, bool market)
    {
        foreach (RectTransform child in modal)
        {
            bool stretched = child.anchorMin.x == 0f && child.anchorMax.x == 1f;
            bool list = child.name == "Scroll" || (child.name == "Content" && child.GetComponent<VerticalLayoutGroup>() != null);
            if (list && stretched)
            {
                child.offsetMin = new Vector2(SideInset, child.offsetMin.y);
                child.offsetMax = new Vector2(-SideInset, market ? -MarketTop : child.offsetMax.y);
                continue;
            }

            // Centred headings, status lines and the Path's bar: as wide as the list.
            bool centred = child.anchorMin.x == 0.5f && child.anchorMax.x == 0.5f;
            if (centred && child.sizeDelta.x > inner && child.GetComponent<Button>() == null)
            {
                child.sizeDelta = new Vector2(inner, child.sizeDelta.y);
                var label = child.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    // A long translation shrinks to the floor instead of running past the margin.
                    label.enableAutoSizing = true;
                    label.fontSizeMax = Mathf.Max(label.fontSize, UIHouseStyle.MinFontSize);
                    label.enableWordWrapping = false;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                }
            }
        }
    }

    // ── Market ──

    private static void EnsureMarketBar(JadeMarketView view, RectTransform modal)
    {
        var so = new SerializedObject(view);
        var barProp = so.FindProperty("balanceBar");

        var existing = modal.Find("JadeBalanceBar") as RectTransform;
        if (existing == null)
        {
            var art = MainMenuRailSetup.LoadArt();
            if (!MainMenuRailSetup.JadeArtReady(art))
            {
                Debug.LogWarning("[PanelContentFit] The Jade Market has no balance bar: assign the Jade emblem and counter " +
                                 "frame in the Main Menu Rail art, then run Restyle Spins Panels again.");
                return;
            }
            MainMenuRailSetup.PrepareJadeArt(art);
            existing = MainMenuRailSetup.CreateJadeBar(modal, art, "JadeBalanceBar", false, MarketFlightSorting);
        }

        existing.anchorMin = existing.anchorMax = new Vector2(0.5f, 1f);
        existing.pivot = new Vector2(0.5f, 1f);
        existing.anchoredPosition = new Vector2(0f, -MarketBarTop);

        barProp.objectReferenceValue = existing.GetComponent<JadeBalanceBar>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FitMarketRow(GameObject root, float inner)
    {
        var row = FindRow<JadeMarketRow>(root);
        if (row == null) return;

        SetHeight(row, inner, 170f);

        // Name over hint in one column between the swatch and the button.
        var column = row.Find("TextColumn") as RectTransform;
        if (column == null)
        {
            column = RunOverlayUI.CreateChild("TextColumn", row);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 2f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }
        column.anchorMin = Vector2.zero;
        column.anchorMax = Vector2.one;
        column.offsetMin = new Vector2(176f, 8f);
        column.offsetMax = new Vector2(-266f, -8f);

        var name = FindChild(row, "Name");
        if (name != null)
        {
            name.SetParent(column, false);
            var nameLabel = name.GetComponent<TextMeshProUGUI>();
            nameLabel.alignment = TextAlignmentOptions.Left;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = UIHouseStyle.MinFontSize;
            nameLabel.fontSizeMax = 40f;
            nameLabel.enableWordWrapping = true;
        }

        var hint = FindChild(row, "Hint") as RectTransform;
        TextMeshProUGUI hintLabel;
        if (hint == null)
        {
            hintLabel = RunOverlayUI.CreateLabel("Hint", column, "Also free after 7 temples (0/7)", 35f, RunOverlayUI.Gold);
            UIHouseStyle.ApplyLabel(hintLabel, UIHouseStyle.Body);
            hintLabel.color = RunOverlayUI.Gold;
            hintLabel.fontSize = 35f;
            hintLabel.enableAutoSizing = true;
            hintLabel.fontSizeMin = UIHouseStyle.MinFontSize;
            hintLabel.fontSizeMax = 35f;
            hintLabel.alignment = TextAlignmentOptions.Left;
            hintLabel.gameObject.SetActive(false);
        }
        else
        {
            hint.SetParent(column, false);
            hintLabel = hint.GetComponent<TextMeshProUGUI>();
        }

        var action = FindChild(row, "ActionButton");
        if (action != null)
        {
            action.sizeDelta = new Vector2(230f, 100f);
            action.anchoredPosition = new Vector2(-20f, 0f);
            var label = action.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = UIHouseStyle.MinFontSize;
                label.fontSizeMax = 40f;
                label.enableWordWrapping = false;
            }
        }

        // The gold wash that fades out over the row when a purchase lands.
        var flash = FindChild(row, "PurchaseFlash");
        if (flash == null)
        {
            flash = RunOverlayUI.CreateChild("PurchaseFlash", row);
            flash.gameObject.AddComponent<Image>();
        }
        flash.SetAsFirstSibling();
        flash.anchorMin = Vector2.zero;
        flash.anchorMax = Vector2.one;
        flash.offsetMin = flash.offsetMax = Vector2.zero;
        var flashImage = flash.GetComponent<Image>();
        SpinsPanelStyle.Slice(flashImage, SpinsPanelStyle.Load(SpinsPanelStyle.LeaderboardSheet, SpinsPanelStyle.HighlightSprite),
            SpinsPanelStyle.RowSliceScale, FlashTint);
        flash.gameObject.SetActive(false);

        // "Unlocked!" popping over the row.
        var banner = FindChild(row, "UnlockedBanner");
        TextMeshProUGUI bannerLabel;
        if (banner == null)
        {
            bannerLabel = RunOverlayUI.CreateLabel("UnlockedBanner", row, "Unlocked!", 56f, RunOverlayUI.Gold);
            UIHouseStyle.ApplyLabel(bannerLabel, UIHouseStyle.Title);
            bannerLabel.color = RunOverlayUI.Gold;
            bannerLabel.fontSize = 56f;
            bannerLabel.enableAutoSizing = true;
            bannerLabel.fontSizeMin = UIHouseStyle.MinFontSize;
            bannerLabel.fontSizeMax = 56f;
            bannerLabel.enableWordWrapping = false;
            RunOverlayUI.Place(bannerLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(inner - 40f, 90f));
        }
        else
        {
            bannerLabel = banner.GetComponent<TextMeshProUGUI>();
        }
        bannerLabel.transform.SetAsLastSibling();
        bannerLabel.gameObject.SetActive(false);

        var so = new SerializedObject(row.GetComponent<JadeMarketRow>());
        so.FindProperty("hintLabel").objectReferenceValue = hintLabel;
        so.FindProperty("purchaseFlash").objectReferenceValue = flashImage;
        so.FindProperty("unlockedBanner").objectReferenceValue = bannerLabel;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Headers ──

    private static void FitMissionsHeader(RectTransform modal)
    {
        FitLine(modal.Find("Count") as RectTransform, StatusTextSize);
        FitListBounds(modal.Find("Content") as RectTransform, -230f);
    }

    private static void FitPathHeader(RectTransform modal)
    {
        FitLine(modal.Find("Tier") as RectTransform, StatusTextSize);
        FitListBounds(modal.Find("Scroll") as RectTransform, -300f);
    }

    private static void FitExpeditionHeader(RectTransform modal, float inner)
    {
        // The summary runs past one line at a readable size in every language, so it gets two.
        var summary = modal.Find("Summary") as RectTransform;
        if (summary != null)
        {
            summary.anchoredPosition = new Vector2(0f, -195f);
            summary.sizeDelta = new Vector2(inner, 110f);
            FitLabel(summary, 42f);
        }
        var reset = modal.Find("Reset") as RectTransform;
        if (reset != null)
        {
            reset.anchoredPosition = new Vector2(0f, -280f);
            reset.sizeDelta = new Vector2(inner, 56f);
            FitLine(reset, 42f);
        }
        FitListBounds(modal.Find("Content") as RectTransform, -325f);
    }

    private static void FitListBounds(RectTransform list, float top)
    {
        if (list == null) return;
        list.offsetMin = new Vector2(list.offsetMin.x, ListBottom);
        list.offsetMax = new Vector2(list.offsetMax.x, top);
    }

    // ── Other rows ──

    private static void FitMissionsRow(GameObject root, float inner)
    {
        var row = FindRow<MissionRow>(root);
        if (row == null) return;

        SetHeight(row, inner, 280f);

        var description = FindChild(row, "Description");
        if (description != null)
        {
            description.anchorMin = Vector2.zero;
            description.anchorMax = Vector2.one;
            description.offsetMin = new Vector2(40f, 100f);
            description.offsetMax = new Vector2(-300f, -16f);
            FitLabel(description, RowTextSize);
            description.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        }

        var reward = FindChild(row, "Reward");
        if (reward != null)
        {
            reward.anchorMin = reward.anchorMax = new Vector2(1f, 1f);
            reward.pivot = new Vector2(1f, 1f);
            reward.anchoredPosition = new Vector2(-30f, -16f);
            reward.sizeDelta = new Vector2(260f, 66f);
            FitLine(reward, RewardTextSize);
        }

        // The swap button sits under the reward, so the description keeps the whole left side.
        var swap = FindChild(row, "RerollButton");
        if (swap != null)
        {
            swap.anchorMin = swap.anchorMax = new Vector2(1f, 1f);
            swap.pivot = new Vector2(1f, 1f);
            swap.anchoredPosition = new Vector2(-30f, -88f);
            swap.sizeDelta = new Vector2(260f, 80f);
            var label = swap.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                FitLine((RectTransform)label.transform, 40f);
                label.rectTransform.offsetMin = new Vector2(14f, label.rectTransform.offsetMin.y);
                label.rectTransform.offsetMax = new Vector2(-14f, label.rectTransform.offsetMax.y);
            }
        }

        FitLine(FindChild(row, "Progress"), 40f);
    }

    private static void FitPathRow(GameObject root, float inner)
    {
        var row = FindRow<PathRow>(root);
        if (row == null) return;

        SetHeight(row, inner, 150f);

        var tier = FindChild(row, "Tier");
        if (tier != null)
        {
            tier.anchorMin = Vector2.zero;
            tier.anchorMax = new Vector2(0.34f, 1f);
            tier.offsetMin = new Vector2(40f, 0f);
            tier.offsetMax = Vector2.zero;
            FitLine(tier, RowHeadingSize);
        }
        var reward = FindChild(row, "Reward");
        if (reward != null)
        {
            reward.anchorMin = new Vector2(0.34f, 0f);
            reward.anchorMax = Vector2.one;
            reward.offsetMin = new Vector2(0f, 8f);
            reward.offsetMax = new Vector2(-30f, -8f);
            FitLine(reward, RewardTextSize);
        }
    }

    /// <summary>Stage and reward share the top line, so the goal gets the row's whole width below them.</summary>
    private static void FitExpeditionRow(GameObject root, float inner)
    {
        var row = FindRow<ExpeditionRow>(root);
        if (row == null) return;

        SetHeight(row, inner, 146f);

        var stage = FindChild(row, "Stage");
        if (stage != null)
        {
            stage.anchorMin = new Vector2(0f, 0.5f);
            stage.anchorMax = new Vector2(0.45f, 1f);
            stage.offsetMin = new Vector2(30f, 0f);
            stage.offsetMax = new Vector2(0f, -10f);
            FitLine(stage, RowHeadingSize);
        }
        var reward = FindChild(row, "Reward");
        if (reward != null)
        {
            reward.anchorMin = new Vector2(0.45f, 0.5f);
            reward.anchorMax = Vector2.one;
            reward.offsetMin = Vector2.zero;
            reward.offsetMax = new Vector2(-30f, -10f);
            FitLine(reward, RewardTextSize);
        }
        var goal = FindChild(row, "Goal");
        if (goal != null)
        {
            goal.anchorMin = Vector2.zero;
            goal.anchorMax = new Vector2(1f, 0.5f);
            goal.offsetMin = new Vector2(30f, 10f);
            goal.offsetMax = new Vector2(-30f, 0f);
            FitLine(goal, RowTextSize);
        }
    }

    // ── How-to card ──

    private static PanelHowTo AddHowTo(Component view, string contentField, string headlineKey, string bodyKey, bool withExtra)
    {
        var viewSo = new SerializedObject(view);
        var content = viewSo.FindProperty(contentField).objectReferenceValue as RectTransform;
        if (content == null) return null;

        var card = content.Find(HowToName) as RectTransform;
        PanelHowTo howTo;
        if (card == null)
        {
            card = RunOverlayUI.CreateChild(HowToName, content);
            card.gameObject.AddComponent<Image>();
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 26, 26);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var headline = HowToLabel("Headline", card, UIHouseStyle.Title, HowToHeadlineSize, RunOverlayUI.Gold);
            var body = HowToLabel("Body", card, UIHouseStyle.Body, HowToBodySize, RunOverlayUI.Parchment);
            var extra = HowToLabel("Extra", card, UIHouseStyle.Title, HowToExtraSize, RunOverlayUI.Gold);
            extra.gameObject.SetActive(false);

            howTo = card.gameObject.AddComponent<PanelHowTo>();
            var so = new SerializedObject(howTo);
            so.FindProperty("headlineLabel").objectReferenceValue = headline;
            so.FindProperty("bodyLabel").objectReferenceValue = body;
            so.FindProperty("extraLabel").objectReferenceValue = withExtra ? extra : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            howTo = card.GetComponent<PanelHowTo>();
        }

        var keys = new SerializedObject(howTo);
        keys.FindProperty("headlineKey").stringValue = headlineKey;
        keys.FindProperty("bodyKey").stringValue = bodyKey;
        keys.ApplyModifiedPropertiesWithoutUndo();

        // Clears the slab's carved rim on the left, where the art is heaviest.
        card.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(42, 22, 26, 26);

        SetHowToSize(card, "Headline", HowToHeadlineSize);
        SetHowToSize(card, "Body", HowToBodySize);
        SetHowToSize(card, "Extra", HowToExtraSize);

        // The same inset slab the rows sit on, lit: reads as a plate above the list.
        SpinsPanelStyle.Slice(card.GetComponent<Image>(),
            SpinsPanelStyle.Load(SpinsPanelStyle.LeaderboardSheet, SpinsPanelStyle.HighlightSprite),
            SpinsPanelStyle.RowSliceScale, Color.white);
        card.SetSiblingIndex(0);
        return howTo;
    }

    private static TextMeshProUGUI HowToLabel(string name, Transform parent, string role, float size, Color color)
    {
        var label = RunOverlayUI.CreateLabel(name, parent, name, size, color);
        UIHouseStyle.ApplyLabel(label, role);
        label.color = color;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = true;
        label.enableAutoSizing = false;
        UIHouseStyle.EnforceMinFontSize(label);
        return label;
    }

    private static void SetHowToSize(RectTransform card, string name, float size)
    {
        var part = card.Find(name);
        var label = part != null ? part.GetComponent<TextMeshProUGUI>() : null;
        if (label != null) label.fontSize = size;
    }

    // ── Helpers ──

    private static RectTransform FindRow<T>(GameObject root) where T : Component
    {
        var component = root.GetComponentInChildren<T>(true);
        return component != null ? (RectTransform)component.transform : null;
    }

    private static RectTransform FindChild(RectTransform row, string name)
    {
        foreach (var t in row.GetComponentsInChildren<RectTransform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static void SetHeight(RectTransform row, float width, float height)
    {
        row.sizeDelta = new Vector2(width, height);
        var element = row.GetComponent<LayoutElement>();
        if (element != null) element.preferredHeight = height;
    }

    private static void FitLabel(RectTransform rect, float max)
    {
        var label = rect.GetComponent<TextMeshProUGUI>();
        if (label == null) return;
        label.enableAutoSizing = true;
        label.fontSizeMin = UIHouseStyle.MinFontSize;
        label.fontSizeMax = Mathf.Max(max, UIHouseStyle.MinFontSize);
        label.fontSize = label.fontSizeMax;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>A label that stays on one line: long copy shrinks towards the floor instead of wrapping.</summary>
    private static void FitLine(RectTransform rect, float max)
    {
        if (rect == null) return;
        FitLabel(rect, max);
        var label = rect.GetComponent<TextMeshProUGUI>();
        if (label == null) return;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }
}
#endif
