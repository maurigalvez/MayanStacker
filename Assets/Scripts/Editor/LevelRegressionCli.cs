using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batch-mode entry for the temple regression pass. Enters Play mode, hands off to
/// <see cref="LevelRegressionRunner"/>, and exits the editor with its result code.
///
/// Run through Tools/run-level-regression.ps1 (it backs up and restores PlayerPrefs), or:
///   Unity.exe -batchmode -nographics -projectPath . -executeMethod LevelRegressionCli.Run
///             [-regressionFrom 1] [-regressionTo 20] [-regressionAttempts 3] [-regressionAimError 0.12] [-regressionOut Logs/Regression]
/// Don't pass -quit: the editor has to keep running while Play mode does.
/// Exit codes: 0 pass, 1 regressions found, 2 harness failure or timeout.
/// </summary>
[InitializeOnLoad]
public static class LevelRegressionCli
{
    private const string ConfigKey = "TamalStacker.Regression.Config";
    private const string DeadlineKey = "TamalStacker.Regression.Deadline";

    static LevelRegressionCli()
    {
        // Entering Play mode reloads the domain; pick the run back up from SessionState.
        if (string.IsNullOrEmpty(SessionState.GetString(ConfigKey, string.Empty))) return;

        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
    }

    public static void Run()
    {
        var config = new LevelRegressionRunner.Config();
        string[] args = Environment.GetCommandLineArgs();
        config.fromLevel = IntArg(args, "-regressionFrom", config.fromLevel);
        config.toLevel = IntArg(args, "-regressionTo", config.toLevel);
        config.attempts = IntArg(args, "-regressionAttempts", config.attempts);
        config.outputDir = StringArg(args, "-regressionOut", config.outputDir);
        string aim = StringArg(args, "-regressionAimError", null);
        if (aim != null && float.TryParse(aim, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float aimError))
            config.aimError = aimError;

        // Generous ceiling for the whole pass: every attempt timing out, plus loading.
        int levelCount = Mathf.Max(1, config.toLevel - config.fromLevel + 1);
        double budget = levelCount * config.attempts * (config.attemptTimeoutSeconds + 30f) + 300f;

        SessionState.SetString(ConfigKey, JsonUtility.ToJson(config));
        SessionState.SetString(DeadlineKey, (EditorApplication.timeSinceStartup + budget).ToString("R"));

        Debug.Log($"[Regression] Temples {config.fromLevel}-{config.toLevel}, {config.attempts} attempt(s) each.");

        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;

        // Start from an empty scene; the runner loads GameScene itself, like the level map does.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;

        string json = SessionState.GetString(ConfigKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        LevelRegressionRunner.Launch(JsonUtility.FromJson<LevelRegressionRunner.Config>(json));
    }

    private static void Watch()
    {
        if (LevelRegressionRunner.ExitCode.HasValue)
        {
            Exit(LevelRegressionRunner.ExitCode.Value);
            return;
        }

        if (double.TryParse(SessionState.GetString(DeadlineKey, string.Empty), out double deadline)
            && EditorApplication.timeSinceStartup > deadline)
        {
            Debug.LogError("[Regression] Whole-run deadline passed; aborting.");
            Exit(2);
        }
    }

    private static void Exit(int code)
    {
        EditorApplication.update -= Watch;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        SessionState.EraseString(ConfigKey);
        SessionState.EraseString(DeadlineKey);
        Debug.Log($"[Regression] Exiting with code {code}. Report: {LevelRegressionRunner.ReportPath}");

        // Only close the editor in batch mode; from an interactive session just stop playing.
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.isPlaying = false;
    }

    private static int IntArg(string[] args, string name, int fallback)
    {
        string s = StringArg(args, name, null);
        return s != null && int.TryParse(s, out int v) ? v : fallback;
    }

    private static string StringArg(string[] args, string name, string fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return fallback;
    }
}
