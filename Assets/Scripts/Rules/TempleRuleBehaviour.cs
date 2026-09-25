using UnityEngine;

/// <summary>
/// Shared plumbing for one temple rule: the references every rule needs, the run lifecycle,
/// and whether the current attempt runs this rule at all.
///
/// A rule only ever runs in temple levels whose <see cref="LevelData"/> names it (first or
/// second rule), so Infinite and the Daily stay exactly as they were. Subclasses override the
/// hooks they care about; everything else is inert.
///
/// Created by <see cref="TempleRules"/>, never placed in a scene.
/// </summary>
public abstract class TempleRuleBehaviour : MonoBehaviour
{
    /// <summary>The rule this component implements.</summary>
    public abstract LevelRule Rule { get; }

    protected GameManager gameManager;
    protected LevelManager levelManager;
    protected StackManager stackManager;
    protected ObjectSpawner objectSpawner;
    protected CameraController cameraController;
    protected GameSoundManager soundManager;
    protected UIManager uiManager;

    protected TempleRuleArt art;

    /// <summary>True while this attempt runs the rule (set at run start).</summary>
    protected bool RuleActive { get; private set; }

    protected LevelData Level => levelManager != null ? levelManager.CurrentLevel : null;

    /// <summary>The run is live: started, not over, not won.</summary>
    protected bool RunLive =>
        RuleActive
        && gameManager != null && gameManager.IsGameActive && !gameManager.IsGameOver
        && (levelManager == null || !levelManager.IsLevelComplete);

    private AudioSource loopSource;
    private float loopTarget;
    private float loopBaseVolume = 1f;

    protected virtual void Start()
    {
        gameManager = DependencyRegistry.Find<GameManager>();
        levelManager = DependencyRegistry.Find<LevelManager>();
        stackManager = DependencyRegistry.Find<StackManager>();
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();
        cameraController = DependencyRegistry.Find<CameraController>();
        soundManager = DependencyRegistry.Find<GameSoundManager>();
        uiManager = DependencyRegistry.Find<UIManager>();
        art = TempleRuleArt.Current;

        if (gameManager != null)
        {
            gameManager.OnGameStart += HandleRunStart;
            gameManager.OnGameRestart += HandleRunStart;
            gameManager.OnGameOver += HandleRunEnd;
        }

        if (levelManager != null) levelManager.OnLevelCompleted += HandleLevelCompleted;
        if (stackManager != null) stackManager.OnObjectAddedToStack += HandleStoneLanded;
        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned += HandleStoneSpawned;
            objectSpawner.OnObjectDropped += HandleStoneDropped;
        }

        Build();
        HandleRunStart();
    }

    protected virtual void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStart -= HandleRunStart;
            gameManager.OnGameRestart -= HandleRunStart;
            gameManager.OnGameOver -= HandleRunEnd;
        }

        if (levelManager != null) levelManager.OnLevelCompleted -= HandleLevelCompleted;
        if (stackManager != null) stackManager.OnObjectAddedToStack -= HandleStoneLanded;
        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned -= HandleStoneSpawned;
            objectSpawner.OnObjectDropped -= HandleStoneDropped;
        }
    }

    private void HandleRunStart()
    {
        LevelData level = Level;
        RuleActive = gameManager != null
                     && gameManager.CurrentGameMode == GameMode.StackerLevels
                     && level != null
                     && level.HasRule(Rule);

        OnRunStart(level);
    }

    private void HandleRunEnd() => OnRunEnd(won: false);

    private void HandleLevelCompleted(int stars, int score, bool firstCompletion) => OnRunEnd(won: true);

    private void HandleStoneLanded(StackableObject stone)
    {
        if (RunLive && stone != null) OnStoneLanded(stone);
    }

    private void HandleStoneSpawned(GameObject go)
    {
        if (RunLive && go != null) OnStoneSpawned(go);
    }

    private void HandleStoneDropped(GameObject go)
    {
        if (RunLive && go != null) OnStoneDropped(go);
    }

    // ---- Hooks ----

    /// <summary>Builds visuals once. They should start hidden.</summary>
    protected virtual void Build() { }

    /// <summary>Every attempt start. <see cref="RuleActive"/> is already set.</summary>
    protected virtual void OnRunStart(LevelData level) { }

    /// <summary>Game over or the temple completed. Stop and fade out.</summary>
    protected virtual void OnRunEnd(bool won) { }

    protected virtual void OnStoneLanded(StackableObject stone) { }
    protected virtual void OnStoneSpawned(GameObject stone) { }
    protected virtual void OnStoneDropped(GameObject stone) { }

    // ---- Helpers ----

    protected void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip != null && soundManager != null) soundManager.PlaySound(clip, volume);
    }

    /// <summary>
    /// Starts (or retargets) this rule's looping ambience. Volume follows the player's SFX
    /// setting and eases toward <paramref name="volume"/>; 0 fades it out.
    /// </summary>
    protected void SetLoop(AudioClip clip, float volume)
    {
        if (clip == null) return;

        if (loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.loop = true;
            loopSource.playOnAwake = false;
            loopSource.volume = 0f;
        }

        if (loopSource.clip != clip)
        {
            loopSource.clip = clip;
            loopSource.Play();
        }
        else if (!loopSource.isPlaying && volume > 0f)
        {
            loopSource.Play();
        }

        loopBaseVolume = 1f;
        loopTarget = Mathf.Clamp01(volume);
    }

    protected void FadeOutLoop() => loopTarget = 0f;

    protected virtual void Update()
    {
        if (loopSource == null) return;

        float sfx = soundManager != null ? soundManager.GetSFXVolume() : 1f;
        bool paused = uiManager != null && uiManager.IsPaused;
        float target = paused ? 0f : loopTarget * loopBaseVolume * sfx;

        loopSource.volume = Mathf.MoveTowards(loopSource.volume, target, Time.unscaledDeltaTime * 0.8f);
        if (loopSource.volume <= 0f && loopTarget <= 0f && loopSource.isPlaying) loopSource.Stop();
    }

    /// <summary>The top of the stack in world units (top stone's collider, else the stack top).</summary>
    protected float TowerTopY()
    {
        StackableObject top = stackManager != null ? stackManager.GetTopObject() : null;
        if (top != null && top.Collider != null) return top.Collider.bounds.max.y;
        return stackManager != null ? stackManager.GetStackTopY() : 0f;
    }

    /// <summary>One stone's height in world units, from the top stone (1 when there is none).</summary>
    protected float StoneHeight()
    {
        StackableObject top = stackManager != null ? stackManager.GetTopObject() : null;
        return top != null && top.Collider != null ? Mathf.Max(0.1f, top.Collider.bounds.size.y) : 1f;
    }

    protected float TowerCentreX()
    {
        StackableObject top = stackManager != null ? stackManager.GetTopObject() : null;
        return top != null ? top.ColliderCenterX : 0f;
    }

    /// <summary>A 1x1 white sprite (1 world unit at scale 1) for code-drawn placeholders.</summary>
    protected static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false, true);
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }
            return whiteSprite;
        }
    }

    private static Sprite whiteSprite;
}
