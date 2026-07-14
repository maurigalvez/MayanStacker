#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor tool that creates and wires the Daily Challenge UI (pre-run briefing,
/// game-over outcome/countdown labels, and the menu button subtitle) into the
/// currently open scenes — without touching or duplicating existing UI.
///
/// It is idempotent and non-destructive:
///   - Any UIManager/MainMenuManager field that is ALREADY assigned is left alone.
///   - New objects are only created for fields that are still empty, and are found
///     by name first, so re-running never produces duplicates.
///
/// Menu: TamalStacker ▸ Daily Challenge ▸ …
/// </summary>
public static class DailyChallengeUISetup
{
    // Palette (Mayan temple: jade = honored, clay = broken). Restyle freely afterward.
    private static readonly Color Jade = new Color(0.09f, 0.42f, 0.34f, 1f);
    private static readonly Color Clay = new Color(0.66f, 0.25f, 0.16f, 1f);
    private static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    private static readonly Color Parchment = new Color(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Color Backdrop = new Color(0.06f, 0.05f, 0.04f, 0.92f);

    [MenuItem("TamalStacker/Daily Challenge/Set Up Game Scene UI")]
    public static void SetupGameSceneUI()
    {
        var ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Daily Challenge UI",
                "No UIManager found in the open scene.\n\nOpen the GameScene first, then run this again.", "OK");
            return;
        }

        var so = new SerializedObject(ui);
        int created = 0, skipped = 0;

        // ── Briefing panel (parented under the same Canvas as the HUD) ──
        Transform canvas = ResolveCanvas(so);
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Daily Challenge UI",
                "Couldn't find a Canvas to parent the briefing panel under.", "OK");
            return;
        }

        GameObject panel = ResolvePanel(so, "dailyBriefingPanel", canvas, "DailyBriefingPanel", ref created, ref skipped);
        if (panel != null)
        {
            Transform p = panel.transform;
            EnsureText(so, "dailyBriefingModifierNameText", p, "ModifierName", "Speed Run", 54, Gold, PlaceCentered(230, 640, 80), ref created, ref skipped);
            EnsureText(so, "dailyBriefingDescriptionText", p, "Description", "The spawner swings faster. Time your drops carefully.", 30, Parchment, PlaceCentered(110, 680, 140), ref created, ref skipped);
            EnsureText(so, "dailyBriefingTargetText", p, "Target", "Stack 30 blocks", 34, Parchment, PlaceCentered(-20, 640, 60), ref created, ref skipped);
            EnsureText(so, "dailyBriefingSubtitleText", p, "Subtitle", "One run. One modifier. Same for everyone today.", 24, new Color(0.72f, 0.68f, 0.58f, 1f), PlaceCentered(-90, 700, 60), ref created, ref skipped);
            EnsureButton(so, "dailyBriefingBeginButton", p, "BeginButton", "Begin the Ritual", PlaceCentered(-210, 380, 96), ref created, ref skipped);
        }

        // ── Game-over panel additions ──
        var goPanelProp = so.FindProperty("gameOverPanel");
        Transform goParent = (goPanelProp != null && goPanelProp.objectReferenceValue is GameObject gop) ? gop.transform : canvas;
        EnsureText(so, "dailyOutcomeText", goParent, "DailyOutcomeHeadline", "Ritual Complete", 48, Jade, PlaceTop(70, 640, 80), ref created, ref skipped);
        EnsureText(so, "dailyResetCountdownText", goParent, "DailyResetCountdown", "Next ritual in 00:00:00", 22, new Color(0.72f, 0.68f, 0.58f, 1f), PlaceBottom(50, 640, 44), ref created, ref skipped);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        if (panel != null) Selection.activeGameObject = panel;

        Report("Game Scene UI", created, skipped);
    }

    [MenuItem("TamalStacker/Daily Challenge/Set Up Main Menu UI")]
    public static void SetupMainMenuUI()
    {
        var menu = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Daily Challenge UI",
                "No MainMenuManager found in the open scene.\n\nOpen the MainMenu scene first, then run this again.", "OK");
            return;
        }

        var so = new SerializedObject(menu);
        int created = 0, skipped = 0;

        // Parent the subtitle under the Daily button (falls back to the main menu panel).
        Transform parent = null;
        var btnProp = so.FindProperty("dailyChallengeButton");
        if (btnProp != null && btnProp.objectReferenceValue is Button btn) parent = btn.transform;
        if (parent == null)
        {
            var panelProp = so.FindProperty("mainMenuPanel");
            if (panelProp != null && panelProp.objectReferenceValue is GameObject mp) parent = mp.transform;
        }

        if (parent == null)
        {
            EditorUtility.DisplayDialog("Daily Challenge UI",
                "Couldn't find the Daily Challenge button or main menu panel to parent the subtitle under.", "OK");
            return;
        }

        EnsureText(so, "dailyChallengeButtonSubtitle", parent, "DailySubtitle",
            "One run. One modifier. Same for everyone today.", 20, new Color(0.72f, 0.68f, 0.58f, 1f),
            HangBelow(6, 420, 40), ref created, ref skipped);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);

        Report("Main Menu UI", created, skipped);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Resolution helpers
    // ─────────────────────────────────────────────────────────────────────

    private static Transform ResolveCanvas(SerializedObject so)
    {
        foreach (string field in new[] { "gameOverPanel", "gameUI" })
        {
            var prop = so.FindProperty(field);
            if (prop != null && prop.objectReferenceValue is GameObject go)
            {
                var c = go.GetComponentInParent<Canvas>(true);
                if (c != null) return c.transform;
            }
        }
        var any = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        return any != null ? any.transform : null;
    }

    /// <summary>Reuse an already-wired panel; otherwise find-by-name or create a full-screen overlay under the canvas.</summary>
    private static GameObject ResolvePanel(SerializedObject so, string field, Transform canvas, string name, ref int created, ref int skipped)
    {
        var prop = so.FindProperty(field);
        if (prop == null) return null;
        if (prop.objectReferenceValue is GameObject existing) { skipped++; return existing; }

        GameObject go = FindOrCreate(canvas, name, out bool wasCreated);
        var rt = go.GetComponent<RectTransform>();
        Stretch(rt);

        // Opaque backdrop so the HUD behind is hidden and taps don't fall through.
        var backdrop = FindOrCreate(go.transform, "Backdrop", out _);
        var brt = backdrop.GetComponent<RectTransform>();
        Stretch(brt);
        var img = backdrop.GetComponent<Image>() ?? Undo.AddComponent<Image>(backdrop);
        img.color = Backdrop;
        img.raycastTarget = true;

        prop.objectReferenceValue = go;
        if (wasCreated) created++; else skipped++;
        return go;
    }

    private static void EnsureText(SerializedObject so, string field, Transform parent, string name,
        string sample, int fontSize, Color color, System.Action<RectTransform> place, ref int created, ref int skipped)
    {
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"[DailyChallengeUISetup] Field '{field}' not found on {so.targetObject.GetType().Name}."); return; }
        if (prop.objectReferenceValue != null) { skipped++; return; }

        var t = CreateText(parent, name, sample, fontSize, color, place);
        prop.objectReferenceValue = t;
        created++;
    }

    private static void EnsureButton(SerializedObject so, string field, Transform parent, string name,
        string label, System.Action<RectTransform> place, ref int created, ref int skipped)
    {
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"[DailyChallengeUISetup] Field '{field}' not found."); return; }
        if (prop.objectReferenceValue != null) { skipped++; return; }

        GameObject go = FindOrCreate(parent, name, out _);
        var rt = go.GetComponent<RectTransform>();
        place(rt);
        var img = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
        img.color = Jade;
        var btn = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);

        var text = CreateText(go.transform, "Text", label, 34, Parchment, null);
        Stretch(text.GetComponent<RectTransform>());
        text.alignment = TextAlignmentOptions.Center;

        prop.objectReferenceValue = btn;
        created++;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string sample, int fontSize, Color color, System.Action<RectTransform> place)
    {
        GameObject go = FindOrCreate(parent, name, out _);
        var t = go.GetComponent<TextMeshProUGUI>() ?? Undo.AddComponent<TextMeshProUGUI>(go);
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = sample;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        place?.Invoke(go.GetComponent<RectTransform>());
        return t;
    }

    private static GameObject FindOrCreate(Transform parent, string name, out bool created)
    {
        var existing = parent.Find(name);
        if (existing != null) { created = false; return existing.gameObject; }
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        created = true;
        return go;
    }

    // ── RectTransform placement ──

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static System.Action<RectTransform> PlaceCentered(float y, float w, float h) => rt =>
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.localScale = Vector3.one;
    };

    private static System.Action<RectTransform> PlaceTop(float yFromTop, float w, float h) => rt =>
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, -yFromTop);
        rt.localScale = Vector3.one;
    };

    private static System.Action<RectTransform> PlaceBottom(float yFromBottom, float w, float h) => rt =>
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, yFromBottom);
        rt.localScale = Vector3.one;
    };

    private static System.Action<RectTransform> HangBelow(float gap, float w, float h) => rt =>
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, -gap);
        rt.localScale = Vector3.one;
    };

    private static void Report(string what, int created, int skipped)
    {
        string msg = created == 0
            ? $"{what}: everything was already wired — no changes made ({skipped} field(s) left untouched)."
            : $"{what}: created & wired {created} new object(s); left {skipped} already-wired field(s) untouched.\n\nStyle to taste, then save the scene.";
        Debug.Log($"[DailyChallengeUISetup] {msg}");
        EditorUtility.DisplayDialog("Daily Challenge UI", msg, "OK");
    }
}
#endif
