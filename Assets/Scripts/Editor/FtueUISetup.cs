#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool that wires the two labels added by the FTUE/retention work:
///   - UIManager.nextGoalText   — the "one reason to retry" line on the game-over panel
///   - UIManager.dailyStreakText — the Daily streak on the Daily result card
///
/// Both fields are null-safe in code, so the game runs fine unwired — this just saves the
/// hand-wiring. Idempotent and non-destructive, same contract as DailyChallengeUISetup:
/// already-assigned fields are left alone and objects are found by name before being
/// created, so re-running never duplicates anything.
///
/// Menu: TamalStacker ▸ FTUE ▸ Set Up Game Scene UI
/// </summary>
public static class FtueUISetup
{
    [MenuItem("TamalStacker/FTUE/Set Up Game Scene UI")]
    public static void SetupGameSceneUI()
    {
        var ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("FTUE UI",
                "No UIManager found in the open scene.\n\nOpen the GameScene first, then run this again.", "OK");
            return;
        }

        var so = new SerializedObject(ui);
        int created = 0, skipped = 0;

        var goPanelProp = so.FindProperty("gameOverPanel");
        var gameOverPanel = goPanelProp != null ? goPanelProp.objectReferenceValue as GameObject : null;

        if (gameOverPanel == null)
        {
            EditorUtility.DisplayDialog("FTUE UI",
                "UIManager.gameOverPanel isn't assigned, so there's nowhere to put the new labels.\n\n" +
                "Assign the game-over panel first, then run this again.", "OK");
            return;
        }

        Transform panel = gameOverPanel.transform;

        // The next-goal line sits low on the panel, under the score and the restart button
        // area — it's a closing thought, not a headline.
        DailyChallengeUISetup.EnsureText(so, "nextGoalText", panel, "NextGoalText",
            "2 blocks from your best.", 28, DailyChallengeUISetup.Parchment,
            DailyChallengeUISetup.PlaceBottom(210f, 700f, 50f), ref created, ref skipped);

        // The streak sits with the other Daily result-card fields; it's hidden automatically
        // for every non-Daily mode.
        DailyChallengeUISetup.EnsureText(so, "dailyStreakText", panel, "DailyStreakText",
            "4-day streak", 32, DailyChallengeUISetup.Gold,
            DailyChallengeUISetup.PlaceBottom(265f, 700f, 54f), ref created, ref skipped);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        string msg = created == 0
            ? $"FTUE UI: everything was already wired — no changes made ({skipped} field(s) left untouched)."
            : $"FTUE UI: created & wired {created} new label(s); left {skipped} already-wired field(s) untouched.\n\nStyle to taste, then save the scene.";

        Debug.Log($"[FtueUISetup] {msg}");
        EditorUtility.DisplayDialog("FTUE UI", msg, "OK");
    }

    /// <summary>
    /// Clears onboarding, streak and reminder state so the first-run experience can be
    /// tested again without uninstalling. Leaves level progress and settings alone.
    /// </summary>
    [MenuItem("TamalStacker/FTUE/Reset First-Run State")]
    public static void ResetFirstRunState()
    {
        if (!EditorUtility.DisplayDialog("Reset First-Run State",
            "Clear tutorial progress, session/run counters, the ad grace period, the Daily streak\n" +
            "and the record of which special blocks have introduced themselves?\n\n" +
            "Level progress and settings are not touched.", "Reset", "Cancel"))
        {
            return;
        }

        FtueState.ResetAll();
        DailyStreak.ResetAll();

        // Forget which special blocks have introduced themselves, so the teaching banners
        // can be tested again rather than only ever firing on a fresh install.
        BlockCodex.ResetAll();

        Debug.Log("[FtueUISetup] First-run state cleared. Next play session will run the FTUE from the top.");
    }
}
#endif
