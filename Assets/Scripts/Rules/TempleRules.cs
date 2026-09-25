using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hosts the temple rules and names them to the player.
///
/// Self-bootstraps into any scene with a <see cref="LevelManager"/>, adds one component per
/// rule (each inert unless the current temple uses it), and at the start of an attempt shows
/// a banner naming the temple's rule(s) and what they do — the first attempt at a temple each
/// session, so a retry isn't slowed down by a banner the player has just read.
/// </summary>
public class TempleRules : MonoBehaviour
{
    private const float IntroDelaySeconds = 1.1f;
    private const float IntroHoldSeconds = 3.2f;
    private const float IntroYOffset = -180f; // below centre: the swing band is up top

    private static TempleRules instance;

    // Temples already introduced this session.
    private static readonly HashSet<int> introducedThisSession = new HashSet<int>();

    private GameManager gameManager;
    private LevelManager levelManager;
    private GameSoundManager soundManager;
    private Coroutine introRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInstance();

    private static void EnsureInstance()
    {
        if (instance != null) return;
        if (Object.FindFirstObjectByType<LevelManager>() == null) return;

        var go = new GameObject("TempleRules");
        instance = go.AddComponent<TempleRules>();

        // One child per rule, so each keeps its own looping AudioSource.
        AddRule<JungleWind>(go.transform);
        AddRule<RainSlick>(go.transform);
        AddRule<Earthquake>(go.transform);
        AddRule<RisingCenote>(go.transform);
        AddRule<Eclipse>(go.transform);
    }

    private static void AddRule<T>(Transform parent) where T : TempleRuleBehaviour
    {
        var child = new GameObject(typeof(T).Name);
        child.transform.SetParent(parent, false);
        child.AddComponent<T>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        gameManager = DependencyRegistry.Find<GameManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        soundManager = DependencyRegistry.Find<GameSoundManager>();

        if (gameManager != null)
        {
            gameManager.OnGameStart += OnRunStart;
            gameManager.OnGameRestart += OnRunStart;
        }

        OnRunStart();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        if (gameManager != null)
        {
            gameManager.OnGameStart -= OnRunStart;
            gameManager.OnGameRestart -= OnRunStart;
        }
    }

    private void OnRunStart()
    {
        if (gameManager == null || gameManager.CurrentGameMode != GameMode.StackerLevels) return;

        LevelData level = levelManager != null ? levelManager.CurrentLevel : null;
        if (level == null || level.rule == LevelRule.None && level.secondRule == LevelRule.None) return;
        if (!introducedThisSession.Add(level.levelNumber)) return;

        if (introRoutine != null) StopCoroutine(introRoutine);
        introRoutine = StartCoroutine(ShowIntro(level));
    }

    private IEnumerator ShowIntro(LevelData level)
    {
        yield return new WaitForSecondsRealtime(IntroDelaySeconds);
        introRoutine = null;

        if (gameManager == null || gameManager.IsGameOver) yield break;

        string title;
        string body;
        LevelRule first = level.rule != LevelRule.None ? level.rule : level.secondRule;
        LevelRule second = level.rule != LevelRule.None ? level.secondRule : LevelRule.None;

        if (second == LevelRule.None || second == first)
        {
            title = LocalizationManager.Get(NameKey(first));
            body = LocalizationManager.Get(DescKey(first));
        }
        else
        {
            title = LocalizationManager.Get("rule_pair_title",
                LocalizationManager.Get(NameKey(first)), LocalizationManager.Get(NameKey(second)));
            body = LocalizationManager.Get(ShortKey(first)) + "\n" + LocalizationManager.Get(ShortKey(second));
        }

        RunBanner.Show(title, body, RunOverlayUI.Gold, IntroHoldSeconds, IntroYOffset);

        TempleRuleArt art = TempleRuleArt.Current;
        if (art.ruleIntroSting != null && soundManager != null) soundManager.PlaySound(art.ruleIntroSting, 0.9f);

        GameAnalytics.Track("rule_intro_shown", new Dictionary<string, object>
        {
            { "level", level.levelNumber },
            { "rule", first.ToString() },
            { "second_rule", second.ToString() }
        });
    }

    /// <summary>Localization key stem for a rule, e.g. "rule_jungle_wind".</summary>
    public static string KeyStem(LevelRule rule)
    {
        switch (rule)
        {
            case LevelRule.JungleWind: return "rule_jungle_wind";
            case LevelRule.RainSlick: return "rule_rain_slick";
            case LevelRule.Earthquake: return "rule_earthquake";
            case LevelRule.RisingCenote: return "rule_rising_cenote";
            case LevelRule.Eclipse: return "rule_eclipse";
            default: return "rule_none";
        }
    }

    public static string NameKey(LevelRule rule) => KeyStem(rule) + "_name";
    public static string DescKey(LevelRule rule) => KeyStem(rule) + "_desc";
    public static string ShortKey(LevelRule rule) => KeyStem(rule) + "_short";

    /// <summary>Editor/debug: forget which temples were introduced, so the banners show again.</summary>
    public static void ResetSessionIntros() => introducedThisSession.Clear();
}
