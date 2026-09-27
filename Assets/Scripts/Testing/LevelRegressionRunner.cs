#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Editor-only regression pass over the temples: loads each one the way the level map does,
/// plays it with a simple aiming bot and records what happened. Started by
/// <c>LevelRegressionCli</c> (batch mode, -executeMethod); never ships.
///
/// The bot plays like an attentive player rather than a perfect one: it drops when the
/// swinging stone is over the top stone (learning the drift from its own landings), holds to
/// brace while Cabracán's hold surface is up, and fires the equipped power whenever it's
/// ready. A level that topples is retried up to <see cref="Config.attempts"/> times; that's a
/// bot-skill signal, not a regression. Exceptions, stuck runs and broken unlocks are.
/// </summary>
public class LevelRegressionRunner : MonoBehaviour
{
    [System.Serializable]
    public class Config
    {
        public int fromLevel = 1;
        public int toLevel = 20;
        public int attempts = 3;
        public float attemptTimeoutSeconds = 420f;
        public float stuckSeconds = 45f;
        public bool freshProfile = true;
        [Tooltip("Std-dev of the bot's aim miss, as a fraction of the top stone's width. Perfect is " +
                 "within 0.1 widths, so 0.12 lands about 60% Perfect and nearly all the rest Good. 0 = all Perfect.")]
        public float aimError = 0.12f;
        public string outputDir = "Logs/Regression";
    }

    [System.Serializable]
    public class AttemptResult
    {
        public string outcome; // completed | toppled | stuck | timeout | load_failed
        public int heightReached;
        public int score;
        public int stars;
        public int drops;
        public int perfect;
        public int good;
        public int poor;
        public int powersFired;
        public List<string> powerLog = new List<string>(); // "Power@height" per use
        public int quakesBraced;
        public float seconds;
        public string endCause; // first "Game Over: ..." line the game logged
        public string note;
    }

    [System.Serializable]
    public class LevelResult
    {
        public int levelNumber;
        public string levelName;
        public string rules;
        public int requiredHeight;
        public float swingSpeed;
        public int twoStar;
        public int threeStar;
        public string equippedPower;
        public bool unlockedBeforePlay;
        public bool completed;
        public bool nextUnlockedAfter;
        public string powersUnlockedAfter;
        public List<AttemptResult> attempts = new List<AttemptResult>();
        public List<string> exceptions = new List<string>();
        public List<string> errors = new List<string>();
        public int warningCount;
        public List<string> problems = new List<string>();
        public bool Passed => problems.Count == 0;
    }

    [System.Serializable]
    private class Report
    {
        public string startedAt;
        public string unityVersion;
        public string gitHint;
        public int fromLevel;
        public int toLevel;
        public bool passed;
        public string abortReason;
        public List<LevelResult> levels = new List<LevelResult>();
    }

    private const string GameSceneName = "GameScene";
    private const int MaxLoggedMessages = 25;

    /// <summary>Set once the pass is done: 0 = all passed, 1 = regressions found, 2 = harness failure.</summary>
    public static int? ExitCode { get; private set; }
    public static string ReportPath { get; private set; }

    private Config config;
    private Report report;
    private LevelResult currentLevel;
    private AttemptResult currentAttempt;

    // Log capture, keyed per level. Unique messages only, so a spamming Update doesn't
    // bury the first real error.
    private readonly HashSet<string> seenMessages = new HashSet<string>();
    private int exceptionTotal;

    public static void Launch(Config config)
    {
        var go = new GameObject("LevelRegressionRunner");
        DontDestroyOnLoad(go);
        var runner = go.AddComponent<LevelRegressionRunner>();
        runner.config = config;
    }

    private void Awake()
    {
        Application.logMessageReceivedThreaded += OnLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceivedThreaded -= OnLog;
    }

    private IEnumerator Start()
    {
        if (config == null) config = new Config();
        Application.targetFrameRate = 60;
        Application.runInBackground = true;

        report = new Report
        {
            startedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            unityVersion = Application.unityVersion,
            fromLevel = config.fromLevel,
            toLevel = config.toLevel
        };

        if (config.freshProfile)
        {
            // The wrapper script backs PlayerPrefs up and restores them afterwards; refuse to
            // wipe a developer's real prefs without that safety net.
            if (System.Environment.GetEnvironmentVariable("TAMAL_REGRESSION_PREFS_BACKED_UP") != "1")
            {
                Finish(2, "freshProfile needs the wrapper script (Tools/run-level-regression.ps1), which backs up PlayerPrefs first.");
                yield break;
            }
            PrepareFreshProfile();
            // Starting mid-map: the earlier temples count as cleared, as for a real player
            // who got here.
            for (int k = 1; k < config.fromLevel; k++) ForceClear(k);
        }

        yield return BootThroughMainMenu();

        for (int n = config.fromLevel; n <= config.toLevel; n++)
        {
            currentLevel = new LevelResult { levelNumber = n };
            report.levels.Add(currentLevel);
            seenMessages.Clear();

            yield return PlayLevel(n);

            if (!currentLevel.completed)
            {
                // Keep covering the later temples: grant this one as if cleared (flagged in the
                // report) so the next unlocks, with the power a clear would have given.
                currentLevel.problems.Add("Later temples reached via a forced unlock of this one");
                ForceClear(n);
            }
        }

        report.passed = report.abortReason == null && report.levels.TrueForAll(l => l.Passed);
        Finish(report.passed ? 0 : 1, null);
    }

    private void PrepareFreshProfile()
    {
        PlayerPrefs.DeleteAll();
        // Language pick and the first-run tutorial are their own flows; the temples are what
        // this pass covers.
        FtueState.MarkLanguageChosen();
        FtueState.Tutorial = FtueState.TutorialState.Completed;
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Loads MainMenu first so the managers it creates for the whole session (localization,
    /// theme, ...) exist in GameScene as they do for players. The online services are
    /// stripped before their Start runs, so the pass never logs in, shows ads or posts scores.
    /// </summary>
    private IEnumerator BootThroughMainMenu()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += StripOnlineServices;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");

        float until = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < until) yield return null;

        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= StripOnlineServices;
    }

    private static void StripOnlineServices(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // sceneLoaded fires after Awake and before Start. PlayFab logs in from Start only (or a
        // manual retry button), so a disabled manager is exactly an offline player's session.
        // It stays registered because it hooked sceneLoaded in Awake; destroying it would leave
        // that handler pointing at a dead object. Ads are removed outright.
        Strip<PlayFabManager>(destroy: false);
        Strip<LeaderboardManager>(destroy: false);
        Strip<AdManager>(destroy: true);
    }

    private static void Strip<T>(bool destroy) where T : MonoBehaviour
    {
        foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Debug.Log($"[Regression] Offline: {(destroy ? "removed" : "disabled")} {typeof(T).Name} on {c.gameObject.name}");
            if (destroy) DestroyImmediate(c);
            else c.enabled = false;
        }
    }

    /// <summary>
    /// Marks a temple cleared with 1 star and unlocks + equips the powers it grants, the way
    /// a real clear does (unlocking a power auto-equips it).
    /// </summary>
    private static void ForceClear(int levelNumber)
    {
        if (PlayerPrefs.GetInt($"Level_{levelNumber}_Stars", 0) < 1)
            PlayerPrefs.SetInt($"Level_{levelNumber}_Stars", 1);
        foreach (PowerId id in PowerUnlocks.PowersUnlockedBy(levelNumber))
        {
            PowerUnlocks.Unlock(id);
            PowerUnlocks.Equip(id);
        }
        PlayerPrefs.Save();
    }

    #region Level loop

    private IEnumerator PlayLevel(int levelNumber)
    {
        for (int attempt = 1; attempt <= config.attempts; attempt++)
        {
            var result = new AttemptResult();
            currentLevel.attempts.Add(result);
            currentAttempt = result;
            yield return PlayAttempt(levelNumber, attempt, result);

            if (result.outcome == "completed")
            {
                currentLevel.completed = true;
                break;
            }
            if (result.outcome == "load_failed") break;
        }

        if (!currentLevel.completed)
        {
            currentLevel.problems.Add($"Not completed in {currentLevel.attempts.Count} attempt(s): " +
                string.Join(", ", currentLevel.attempts.ConvertAll(a => a.outcome)));
        }
        if (currentLevel.exceptions.Count > 0)
        {
            currentLevel.problems.Add($"{currentLevel.exceptions.Count} unique exception(s)");
        }
        foreach (var a in currentLevel.attempts)
        {
            if (a.outcome == "stuck" || a.outcome == "timeout")
                currentLevel.problems.Add($"Attempt {a.outcome}: {a.note}");
        }
        if (!currentLevel.unlockedBeforePlay)
        {
            currentLevel.problems.Add("Temple was locked when the bot reached it (unlock chain broken)");
        }
        if (currentLevel.completed && !currentLevel.nextUnlockedAfter && levelNumber < TotalLevelsHint)
        {
            currentLevel.problems.Add($"Temple {levelNumber + 1} still locked after completing {levelNumber}");
        }
    }

    private int TotalLevelsHint = int.MaxValue;

    private IEnumerator PlayAttempt(int levelNumber, int attempt, AttemptResult result)
    {
        float started = Time.realtimeSinceStartup;

        SceneLoader.LoadGameScene(GameSceneName, GameMode.StackerLevels, levelNumber - 1);

        // Wait for the scene and the level to come up.
        LevelManager levelManager = null;
        GameManager gameManager = null;
        float waitUntil = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < waitUntil)
        {
            yield return null;
            levelManager = DependencyRegistry.Find<LevelManager>();
            gameManager = DependencyRegistry.Find<GameManager>();
            if (levelManager != null && gameManager != null && levelManager.CurrentLevel != null
                && gameManager.IsGameActive)
                break;
        }

        if (levelManager == null || gameManager == null || levelManager.CurrentLevel == null)
        {
            result.outcome = "load_failed";
            result.note = "GameScene/LevelManager/level didn't come up within 20s";
            currentLevel.problems.Add(result.note);
            yield break;
        }

        LevelData level = levelManager.CurrentLevel;
        TotalLevelsHint = levelManager.TotalLevels;
        if (attempt == 1)
        {
            currentLevel.levelName = level.levelName;
            currentLevel.requiredHeight = level.requiredStackHeight;
            currentLevel.swingSpeed = level.swingSpeedMultiplier;
            currentLevel.twoStar = level.twoStarScore;
            currentLevel.threeStar = level.threeStarScore;
            currentLevel.rules = level.secondRule != LevelRule.None
                ? $"{level.rule} + {level.secondRule}"
                : level.rule.ToString();
            currentLevel.unlockedBeforePlay = levelManager.IsLevelUnlocked(levelNumber);
            currentLevel.equippedPower = PowerUnlocks.HasEquippedPower ? PowerUnlocks.Equipped.ToString() : "none";

            if (level.levelNumber != levelNumber)
            {
                currentLevel.problems.Add($"Level list order: index {levelNumber - 1} holds levelNumber {level.levelNumber}");
            }
        }

        var bot = new Bot(result, config.aimError, levelNumber * 1000 + attempt);
        bot.Attach();

        bool completed = false, failed = false;
        System.Action<int, int, bool> onCompleted = (stars, score, first) =>
        {
            completed = true;
            result.stars = stars;
            result.score = score;
        };
        System.Action onFailed = () => failed = true;
        System.Action onGameOver = () => { if (!levelManager.IsLevelComplete) failed = true; };
        levelManager.OnLevelCompleted += onCompleted;
        levelManager.OnLevelFailed += onFailed;
        gameManager.OnGameOver += onGameOver;

        float lastProgressAt = Time.realtimeSinceStartup;
        int lastProgressMark = 0;

        while (!completed && !failed)
        {
            bot.Tick();

            int mark = bot.Drops * 1000 + bot.Landings;
            if (mark != lastProgressMark || bot.Bracing)
            {
                lastProgressMark = mark;
                lastProgressAt = Time.realtimeSinceStartup;
            }

            float now = Time.realtimeSinceStartup;
            if (now - lastProgressAt > config.stuckSeconds)
            {
                result.outcome = "stuck";
                result.note = bot.Describe();
                break;
            }
            if (now - started > config.attemptTimeoutSeconds)
            {
                result.outcome = "timeout";
                result.note = bot.Describe();
                break;
            }
            yield return null;
        }

        var stack = DependencyRegistry.Find<StackManager>();
        result.heightReached = stack != null ? stack.GetStackCount() : 0;
        if (!completed) result.score = gameManager.CurrentScore;

        if (completed)
        {
            result.outcome = "completed";
            // Let the capstone and result card run: their exceptions count too.
            float settle = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < settle) yield return null;

            currentLevel.nextUnlockedAfter = levelManager.IsLevelUnlocked(levelNumber + 1);
            var unlocked = PowerUnlocks.PowersUnlockedBy(levelNumber);
            var names = new List<string>();
            foreach (var id in unlocked)
            {
                bool ok = PowerUnlocks.IsUnlocked(id);
                names.Add(ok ? id.ToString() : id + " (NOT unlocked)");
                if (!ok) currentLevel.problems.Add($"Power {id} should unlock after temple {levelNumber} but didn't");
            }
            currentLevel.powersUnlockedAfter = string.Join(", ", names);
        }
        else if (failed)
        {
            result.outcome = "toppled";
            result.note = $"height {result.heightReached}/{level.requiredStackHeight}" +
                          (result.endCause != null ? $" - {result.endCause}" : string.Empty);
            float settle = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < settle) yield return null;
        }

        bot.Detach();
        if (levelManager != null)
        {
            levelManager.OnLevelCompleted -= onCompleted;
            levelManager.OnLevelFailed -= onFailed;
        }
        if (gameManager != null) gameManager.OnGameOver -= onGameOver;

        result.seconds = Mathf.Round((Time.realtimeSinceStartup - started) * 10f) / 10f;
        Debug.Log($"[Regression] Temple {levelNumber} attempt {attempt}: {result.outcome} " +
                  $"h={result.heightReached} score={result.score} stars={result.stars} ({result.seconds}s)");
    }

    #endregion

    #region Bot

    /// <summary>
    /// Plays one attempt. Aims at the top stone's collider centre, corrected by the average
    /// drift between where it let go and where stones actually landed (the drop impulse is
    /// random, wind pushes), so it's good but not flawless.
    /// </summary>
    private class Bot
    {
        private const float BaseTolerance = 0.04f;
        private const int PointerId = 7331;

        private readonly AttemptResult result;
        private ObjectSpawner spawner;
        private StackManager stack;
        private GameManager game;
        private LevelManager levels;
        private UIManager ui;
        private PowerMeterView meter;
        private Ground ground;

        private float bias;
        private float lastDropX;
        private StackableObject lastDropped;
        private float armedSince = -1f;
        private bool lastLandingPerfect = true;
        private bool lastLandingPoor;
        private GameObject holdSurface;
        private int frame;

        public int Drops => result.drops;
        public int Landings { get; private set; }
        public bool Bracing => holdSurface != null;

        private readonly float aimError;
        private readonly System.Random rng;
        private float aimOffset;

        public Bot(AttemptResult result, float aimError, int seed)
        {
            this.result = result;
            this.aimError = aimError;
            rng = new System.Random(seed);
            aimOffset = NextAimOffset();
        }

        /// <summary>Normal(0, aimError) via Box-Muller, in stone widths, clamped inside the half-width.</summary>
        private float NextAimOffset()
        {
            if (aimError <= 0f) return 0f;
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double n = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
            return Mathf.Clamp((float)n * aimError, -0.45f, 0.45f);
        }

        private float TopWidth()
        {
            StackableObject top = stack != null ? stack.GetTopObject() : null;
            if (top != null && top.Collider != null) return top.Collider.bounds.size.x;
            return spawner != null ? spawner.ObjectSize.x : 1f;
        }

        public void Attach()
        {
            spawner = DependencyRegistry.Find<ObjectSpawner>();
            stack = DependencyRegistry.Find<StackManager>();
            game = DependencyRegistry.Find<GameManager>();
            levels = DependencyRegistry.Find<LevelManager>();
            ui = DependencyRegistry.Find<UIManager>();
            ground = stack != null ? stack.GetGround() : null;

            if (spawner != null) spawner.OnObjectDropped += OnDropped;
            if (stack != null) stack.OnObjectAddedToStack += OnLanded;
            if (PowerSystem.Instance != null) PowerSystem.Instance.OnPowerUsed += OnPowerUsed;
        }

        public void Detach()
        {
            ReleaseBrace();
            if (spawner != null) spawner.OnObjectDropped -= OnDropped;
            if (stack != null) stack.OnObjectAddedToStack -= OnLanded;
            if (PowerSystem.Instance != null) PowerSystem.Instance.OnPowerUsed -= OnPowerUsed;
        }

        public void Tick()
        {
            frame++;
            if (spawner == null || game == null) return;
            if (ui != null && ui.IsPaused) return;

            // Cabracán: the BRACE button is only up while a quake is on. The bot braces for
            // the whole quake, and bracing blocks drops (brace OR drop), so it doesn't drop.
            if (frame % 3 == 0) UpdateBrace();
            if (holdSurface != null) return;

            TryFirePower();
            TryDrop();
        }

        private void UpdateBrace()
        {
            BraceButtonView view = Object.FindFirstObjectByType<BraceButtonView>(); // active only
            GameObject surface = view != null ? view.HoldTarget : null;
            if (surface != null && holdSurface == null)
            {
                holdSurface = surface;
                ExecuteEvents.Execute(surface, MakePointer(), ExecuteEvents.pointerDownHandler);
                result.quakesBraced++;
            }
            else if (surface == null && holdSurface != null)
            {
                ReleaseBrace();
            }
        }

        private void ReleaseBrace()
        {
            if (holdSurface != null)
                ExecuteEvents.Execute(holdSurface, MakePointer(), ExecuteEvents.pointerUpHandler);
            holdSurface = null;
        }

        private static PointerEventData MakePointer() =>
            new PointerEventData(EventSystem.current) { pointerId = PointerId };

        private void TryFirePower()
        {
            PowerSystem powers = PowerSystem.Instance;
            if (powers == null || !powers.CanFire) return;
            if (!WouldPlayerFire(powers.Equipped)) return;
            if (meter == null) meter = Object.FindFirstObjectByType<PowerMeterView>();
            if (meter == null || meter.Button == null || !meter.Button.interactable) return;
            meter.Button.onClick.Invoke();
            result.powerLog.Add($"{powers.Equipped}@{(stack != null ? stack.GetStackCount() : 0)}");
        }

        /// <summary>
        /// A full meter is saved for the moment it fixes something, as a player would. Firing
        /// on every full meter isn't play: Rewind would undo three stones per four Perfects.
        /// </summary>
        private bool WouldPlayerFire(PowerId power)
        {
            TowerStability stability = TowerStability.Instance;
            bool trembling = stability != null && stability.IsTrembling;

            switch (power)
            {
                case PowerId.JaguarSlam:
                    // Re-centres the top stone: worth it once that stone sits off the Perfect band.
                    return TopOffsetWidths() > PerfectBandWidths();

                case PowerId.QuetzalFeather:
                    // Slows the next drops: reach for it when the swing just beat you.
                    return trembling || !lastLandingPerfect;

                case PowerId.TzolkinRewind:
                    // Undoes three stones: only for a bad landing or a tower about to go.
                    return trembling || lastLandingPoor;

                case PowerId.KukulkansCall:
                    // Straightens the whole stack: for a leaning or trembling tower.
                    return trembling || LeanWidths() > 0.25f;

                default:
                    return false;
            }
        }

        /// <summary>Half the Perfect window, in stone widths (accuracy is measured over (w1+w2)/2).</summary>
        private float PerfectBandWidths() => 1f - game.PerfectThreshold;

        /// <summary>How far the top stone sits off the one below, in stone widths.</summary>
        private float TopOffsetWidths()
        {
            var stones = stack != null ? stack.StackObjects : null;
            if (stones == null || stones.Count < 2) return 0f;
            StackableObject top = stones[stones.Count - 1], below = stones[stones.Count - 2];
            if (top == null || below == null) return 0f;
            return Mathf.Abs(top.ColliderCenterX - below.ColliderCenterX) / Mathf.Max(0.01f, TopWidth());
        }

        /// <summary>Horizontal drift from the foundation to the top stone, in stone widths.</summary>
        private float LeanWidths()
        {
            var stones = stack != null ? stack.StackObjects : null;
            if (stones == null || stones.Count < 2) return 0f;
            StackableObject top = stones[stones.Count - 1], bottom = stones[0];
            if (top == null || bottom == null) return 0f;
            return Mathf.Abs(top.ColliderCenterX - bottom.ColliderCenterX) / Mathf.Max(0.01f, TopWidth());
        }

        private void TryDrop()
        {
            GameObject current = spawner.CurrentObject;
            if (current == null || spawner.WaitingForLanding || !spawner.IsCurrentObjectArmed)
            {
                armedSince = -1f;
                return;
            }
            if (game.IsGameOver || (levels != null && levels.IsLevelComplete)) return;

            if (armedSince < 0f) armedSince = Time.realtimeSinceStartup;

            var stone = current.GetComponent<StackableObject>();
            float x = stone != null ? stone.ColliderCenterX : current.transform.position.x;
            float target = TargetX();

            // A stone that never lines up (a wide lateral offset, say) still gets dropped:
            // widen the window the longer it waits.
            float waited = Time.realtimeSinceStartup - armedSince;
            float tolerance = BaseTolerance + Mathf.Max(0f, waited - 6f) * 0.05f;

            if (Mathf.Abs(x + bias - (target + aimOffset * TopWidth())) <= tolerance)
            {
                lastDropX = x;
                aimOffset = NextAimOffset();
                spawner.DropCurrentObject();
            }
        }

        private float TargetX()
        {
            StackableObject top = stack != null ? stack.GetTopObject() : null;
            if (top != null) return top.ColliderCenterX;
            return ground != null ? ground.transform.position.x : 0f;
        }

        private void OnDropped(GameObject obj)
        {
            result.drops++;
            lastDropped = obj != null ? obj.GetComponent<StackableObject>() : null;
        }

        private void OnLanded(StackableObject stone)
        {
            Landings++;
            if (stone == null) return;

            if (stone == lastDropped)
            {
                float drift = stone.ColliderCenterX - lastDropX;
                bias = Mathf.Clamp(Mathf.Lerp(bias, drift, 0.3f), -0.6f, 0.6f);
            }

            float acc = stone.LandingAccuracy;
            lastLandingPerfect = acc >= game.PerfectThreshold;
            lastLandingPoor = acc < game.GoodThreshold;
            if (acc >= game.PerfectThreshold) result.perfect++;
            else if (acc >= game.GoodThreshold) result.good++;
            else result.poor++;
        }

        private void OnPowerUsed(PowerId id) => result.powersFired++;

        public string Describe()
        {
            GameObject current = spawner != null ? spawner.CurrentObject : null;
            return $"timeScale={Time.timeScale:0.##}, paused={(ui != null && ui.IsPaused)}, " +
                   $"current={(current != null)}, waitingForLanding={spawner?.WaitingForLanding}, " +
                   $"armed={spawner?.IsCurrentObjectArmed}, gameOver={game?.IsGameOver}, " +
                   $"height={stack?.GetStackCount()}, bracing={Bracing}, drops={result.drops}, landings={Landings}";
        }
    }

    #endregion

    #region Logs and report

    private void OnLog(string message, string stackTrace, LogType type)
    {
        LevelResult level = currentLevel;
        if (level == null) return;

        lock (seenMessages)
        {
            if (type == LogType.Warning)
            {
                level.warningCount++;
                return;
            }
            if (type == LogType.Log)
            {
                AttemptResult attempt = currentAttempt;
                if (attempt != null && attempt.endCause == null && message.StartsWith("Game Over:"))
                    attempt.endCause = message;
                return;
            }

            string firstFrame = FirstLine(stackTrace);
            string key = type + "|" + message + "|" + firstFrame;
            if (!seenMessages.Add(key)) return;

            string entry = string.IsNullOrEmpty(firstFrame) ? message : $"{message}  @ {firstFrame}";
            if (type == LogType.Exception)
            {
                exceptionTotal++;
                if (level.exceptions.Count < MaxLoggedMessages) level.exceptions.Add(entry);
            }
            else if (level.errors.Count < MaxLoggedMessages)
            {
                level.errors.Add($"[{type}] {entry}");
            }
        }
    }

    private static string FirstLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        int nl = s.IndexOf('\n');
        return (nl >= 0 ? s.Substring(0, nl) : s).Trim();
    }

    private void Finish(int code, string harnessError)
    {
        if (harnessError != null)
        {
            report.abortReason = harnessError;
            report.passed = false;
            Debug.LogError("[Regression] " + harnessError);
        }

        try
        {
            string dir = config.outputDir;
            Directory.CreateDirectory(dir);
            string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string json = Path.Combine(dir, $"regression-{stamp}.json");
            string md = Path.Combine(dir, $"regression-{stamp}.md");
            File.WriteAllText(json, JsonUtility.ToJson(report, true));
            File.WriteAllText(md, BuildMarkdown());
            ReportPath = Path.GetFullPath(md);
            Debug.Log($"[Regression] Report: {ReportPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Regression] Couldn't write report: " + e.Message);
            code = 2;
        }

        ExitCode = code;
    }

    private string BuildMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Temple regression {report.fromLevel}-{report.toLevel}");
        sb.AppendLine();
        sb.AppendLine($"Run {report.startedAt}, Unity {report.unityVersion}. Verdict: **{(report.passed ? "PASS" : "FAIL")}**");
        if (!string.IsNullOrEmpty(report.abortReason)) sb.AppendLine($"\n> {report.abortReason}");
        sb.AppendLine();
        sb.AppendLine("| # | Temple | Rules | Power | Result | Tries | Height | Score (2★/3★) | ★ | P/G/Poor | Powers | Quakes | Exc | Err | Time |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var l in report.levels)
        {
            AttemptResult last = l.attempts.Count > 0 ? l.attempts[l.attempts.Count - 1] : new AttemptResult();
            int powers = 0, quakes = 0; float secs = 0;
            foreach (var a in l.attempts) { powers += a.powersFired; quakes += a.quakesBraced; secs += a.seconds; }
            sb.AppendLine($"| {l.levelNumber} | {l.levelName} | {l.rules} | {l.equippedPower} | " +
                          $"{(l.Passed ? "pass" : "**FAIL**")} ({last.outcome}) | {l.attempts.Count} | " +
                          $"{last.heightReached}/{l.requiredHeight} | {last.score} ({l.twoStar}/{l.threeStar}) | {last.stars} | " +
                          $"{last.perfect}/{last.good}/{last.poor} | {powers} | {quakes} | {l.exceptions.Count} | {l.errors.Count} | {secs:0}s |");
        }

        sb.AppendLine();
        sb.AppendLine("## Power use (power@stack height, per attempt)");
        foreach (var l in report.levels)
        {
            var uses = l.attempts.ConvertAll(a => a.powerLog.Count > 0 ? string.Join(" ", a.powerLog) : "none");
            sb.AppendLine($"- {l.levelNumber}. {l.equippedPower}: {string.Join(" | ", uses)}");
        }

        foreach (var l in report.levels)
        {
            if (l.problems.Count == 0 && l.exceptions.Count == 0 && l.errors.Count == 0) continue;
            sb.AppendLine();
            sb.AppendLine($"## {l.levelNumber}. {l.levelName}");
            foreach (var p in l.problems) sb.AppendLine($"- Problem: {p}");
            if (!string.IsNullOrEmpty(l.powersUnlockedAfter)) sb.AppendLine($"- Powers unlocked after: {l.powersUnlockedAfter}");
            foreach (var a in l.attempts)
                if (!string.IsNullOrEmpty(a.note)) sb.AppendLine($"- Attempt ({a.outcome}): {a.note}");
            foreach (var e in l.exceptions) sb.AppendLine($"- Exception: `{e}`");
            foreach (var e in l.errors) sb.AppendLine($"- Error: `{e}`");
        }
        return sb.ToString();
    }

    #endregion
}
#endif
