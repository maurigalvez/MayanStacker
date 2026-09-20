#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor tool that builds and wires the "who am I playing as" UI into the MainMenu scene:
///
///   • main menu header: "Playing as Jade Itzel#1A2B", always on screen;
///   • Settings panel: the name, "Signed in with Google Play" / "Saved on this device only",
///     and a "Sign in with Google Play" button that only shows when sign-in failed;
///   • Leaderboard panel: a pinned row under the list with the player's own rank and best
///     score, or "No score yet".
///
/// Same contract as the other setup tools in this folder: idempotent and non-destructive.
/// Anything already built or wired is left alone and re-running creates no duplicates.
/// Placeholder styling uses the palette from <see cref="DailyChallengeUISetup"/>; restyle
/// in the scene afterwards.
///
/// Menu: TamalStacker ▸ Account ▸ Set Up Player Identity UI
/// </summary>
public static class PlayerIdentityUISetup
{
    private const string Title = "Player Identity UI";

    [MenuItem("TamalStacker/Account/Set Up Player Identity UI")]
    public static void Setup()
    {
        var menu = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (menu == null)
        {
            EditorUtility.DisplayDialog(Title,
                "No MainMenuManager found in the open scene.\n\nOpen the MainMenu scene first, then run this again.", "OK");
            return;
        }

        var menuSo = new SerializedObject(menu);
        int created = 0, skipped = 0;

        var mainPanel = menuSo.FindProperty("mainMenuPanel").objectReferenceValue as GameObject;
        if (mainPanel != null) EnsureMenuHeader(mainPanel.transform, ref created, ref skipped);
        else Debug.LogWarning("[PlayerIdentityUISetup] MainMenuManager.mainMenuPanel is not assigned; skipped the header.");

        var settingsPanel = menuSo.FindProperty("settingsPanel").objectReferenceValue as GameObject;
        if (settingsPanel != null) EnsureSettingsSection(settingsPanel.transform, ref created, ref skipped);
        else Debug.LogWarning("[PlayerIdentityUISetup] MainMenuManager.settingsPanel is not assigned; skipped Settings.");

        var leaderboardPanel = menuSo.FindProperty("leaderboardPanel").objectReferenceValue as GameObject;
        var leaderboard = leaderboardPanel != null ? leaderboardPanel.GetComponent<LeaderboardPanel>() : null;
        if (leaderboard != null) EnsureLeaderboardRow(leaderboard, ref created, ref skipped);
        else Debug.LogWarning("[PlayerIdentityUISetup] No LeaderboardPanel on MainMenuManager.leaderboardPanel; skipped the pinned row.");

        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        DailyChallengeUISetup.Report(Title, created, skipped);
    }

    private static void EnsureMenuHeader(Transform panel, ref int created, ref int skipped)
    {
        if (panel.GetComponentInChildren<PlayerIdentityView>(true) != null) { skipped++; return; }

        GameObject root = DailyChallengeUISetup.FindOrCreate(panel, "PlayerIdentity", out _);
        DailyChallengeUISetup.PlaceTop(24f, 900f, 56f)(root.GetComponent<RectTransform>());

        var name = CreateNameText(root.transform, 34);
        DailyChallengeUISetup.Stretch(name.rectTransform);

        var view = root.GetComponent<PlayerIdentityView>() ?? Undo.AddComponent<PlayerIdentityView>(root);
        var so = new SerializedObject(view);
        so.FindProperty("nameText").objectReferenceValue = name;
        so.ApplyModifiedProperties();
        created++;
    }

    private static void EnsureSettingsSection(Transform panel, ref int created, ref int skipped)
    {
        if (panel.GetComponentInChildren<PlayerIdentityView>(true) != null) { skipped++; return; }

        GameObject root = DailyChallengeUISetup.FindOrCreate(panel, "AccountSection", out _);
        DailyChallengeUISetup.PlaceTop(140f, 760f, 230f)(root.GetComponent<RectTransform>());

        var name = CreateNameText(root.transform, 36);
        DailyChallengeUISetup.PlaceTop(0f, 760f, 56f)(name.rectTransform);

        var status = DailyChallengeUISetup.CreateText(root.transform, "Status", "Saved on this device only", 26,
            DailyChallengeUISetup.Muted, DailyChallengeUISetup.PlaceTop(60f, 760f, 40f));

        GameObject buttonGo = DailyChallengeUISetup.FindOrCreate(root.transform, "GoogleSignInButton", out _);
        DailyChallengeUISetup.PlaceTop(120f, 560f, 90f)(buttonGo.GetComponent<RectTransform>());
        var image = buttonGo.GetComponent<Image>() ?? Undo.AddComponent<Image>(buttonGo);
        image.color = DailyChallengeUISetup.Jade;
        var button = buttonGo.GetComponent<Button>() ?? Undo.AddComponent<Button>(buttonGo);
        button.targetGraphic = image;

        var label = DailyChallengeUISetup.CreateText(buttonGo.transform, "Text", "Sign in with Google Play", 32,
            DailyChallengeUISetup.Parchment, null);
        DailyChallengeUISetup.Stretch(label.rectTransform);
        SetLocalizationKey(label.gameObject, "settings_google_sign_in");

        var view = root.GetComponent<PlayerIdentityView>() ?? Undo.AddComponent<PlayerIdentityView>(root);
        var so = new SerializedObject(view);
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("googleSignInButton").objectReferenceValue = button;
        so.ApplyModifiedProperties();
        created++;
    }

    private static void EnsureLeaderboardRow(LeaderboardPanel leaderboard, ref int created, ref int skipped)
    {
        var so = new SerializedObject(leaderboard);
        var rowProp = so.FindProperty("playerRow");
        if (rowProp.objectReferenceValue != null) { skipped++; return; }

        var prefab = so.FindProperty("leaderboardEntryPrefab").objectReferenceValue as GameObject;
        if (prefab == null || prefab.GetComponent<LeaderboardEntryUI>() == null)
        {
            Debug.LogWarning("[PlayerIdentityUISetup] LeaderboardPanel has no entry prefab with a LeaderboardEntryUI; skipped the pinned row.");
            return;
        }

        Transform existing = leaderboard.transform.Find("YourRow");
        GameObject row = existing != null
            ? existing.gameObject
            : (GameObject)PrefabUtility.InstantiatePrefab(prefab, leaderboard.transform);
        if (existing == null)
        {
            row.name = "YourRow";
            Undo.RegisterCreatedObjectUndo(row, "Create YourRow");
        }

        // Same height as a list row, pinned just above the bottom of the panel
        var rect = row.GetComponent<RectTransform>();
        float height = rect.rect.height > 1f ? rect.rect.height : 90f;
        DailyChallengeUISetup.PlaceBottom(40f, 900f, height)(rect);
        row.SetActive(false); // LeaderboardPanel shows it once a board has loaded

        rowProp.objectReferenceValue = row.GetComponent<LeaderboardEntryUI>();
        so.ApplyModifiedProperties();
        created++;
    }

    private static TextMeshProUGUI CreateNameText(Transform parent, int fontSize)
    {
        var text = DailyChallengeUISetup.CreateText(parent, "Name", "Playing as Jade Itzel", fontSize,
            DailyChallengeUISetup.Gold, null);
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;

        // Names can be Chinese or Japanese in any language: keep the display font for Latin
        // names and let the CJK fallback draw the rest
        var switcher = text.GetComponent<LocaleFontSwitcher>() ?? Undo.AddComponent<LocaleFontSwitcher>(text.gameObject);
        var so = new SerializedObject(switcher);
        so.FindProperty("keepOriginalFont").boolValue = true;
        so.ApplyModifiedProperties();
        return text;
    }

    private static void SetLocalizationKey(GameObject go, string key)
    {
        var localized = go.GetComponent<LocalizedText>() ?? Undo.AddComponent<LocalizedText>(go);
        var so = new SerializedObject(localized);
        var prop = so.FindProperty("localizationKey");
        if (prop != null)
        {
            prop.stringValue = key;
            so.ApplyModifiedProperties();
        }
    }
}
#endif
