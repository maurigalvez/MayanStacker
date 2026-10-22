using UnityEngine;

/// <summary>
/// Rolls and plays Swing Moods for the stone on the hook. Owned by <see cref="SpawnerHolder"/>,
/// which asks it for a speed factor and a sideways offset each frame and adds both to the swing.
///
/// A mood is announced first: the stone glows in the mood's colour for a beat (the cue), then
/// the twist fades in. Nothing here changes a landing window, and a mood lasts only until the
/// stone is dropped. Rolls draw from their own stream, seeded off the run's seed in temples and
/// the Daily, so a replay is the same run and the block-variety sequence is never disturbed.
/// </summary>
public class SwingMoodDirector
{
    private const int HistorySize = 96;

    private SwingMoodSettings settings;
    private bool settingsLoaded;
    private SwingMoodFx glow;
    private SwingMoodFx ghost;
    private bool fxLoaded;
    private bool fxWarned;
    private System.Random rng;

    private SwingMood mood = SwingMood.Steady;
    private Transform stone;
    private SpriteRenderer stoneRenderer;
    private float age;         // seconds since the stone spawned
    private float weight;      // 0..1, how far the mood has faded in
    private int rolledCount;
    private int calmStreak;    // stones since the last mood (or since the run began)

    // Where the stone has been, for the Echo ghost.
    private readonly Vector3[] posHistory = new Vector3[HistorySize];
    private readonly float[] rotHistory = new float[HistorySize];
    private readonly float[] timeHistory = new float[HistorySize];
    private int historyHead;
    private int historyCount;

    /// <summary>The mood on the stone that is hanging now.</summary>
    public SwingMood Current => mood;

    /// <summary>How many stones have rolled a real mood this run (diagnostics and the regression bot).</summary>
    public int RolledCount => rolledCount;

    /// <summary>Raised when a stone rolls a mood other than Steady.</summary>
    public System.Action<SwingMood> OnMoodRolled;

    public SwingMoodSettings Settings
    {
        get
        {
            if (!settingsLoaded)
            {
                settingsLoaded = true;
                settings = Resources.Load<SwingMoodSettings>(SwingMoodSettings.ResourcePath);
            }
            return settings != null && settings.enableMoods ? settings : null;
        }
    }

    /// <summary>Multiplier on how fast the swing advances this frame. 1 = unchanged.</summary>
    public float SpeedFactor { get; private set; } = 1f;

    /// <summary>World units the swing's centre is pushed sideways this frame. 0 = unchanged.</summary>
    public float OffsetX { get; private set; }

    /// <summary>Clears all per-run state. Called when a run starts or restarts.</summary>
    public void BeginRun()
    {
        EndStone();
        rolledCount = 0;
        calmStreak = 0;
        rng = RunRandom.CurrentSeed != 0 ? new System.Random(RunRandom.CurrentSeed ^ 0x5A17D00D) : null;
    }

    /// <summary>A new stone is on the hook: decide its mood.</summary>
    public void OnStoneSpawned(GameObject spawned, GameMode mode, int levelNumber, int stonesPlaced)
    {
        EndStone();
        SwingMoodSettings s = Settings;
        if (s == null || spawned == null || !s.AllowedIn(mode, levelNumber, stonesPlaced)) return;
        if (!ShouldRoll(s, stonesPlaced))
        {
            calmStreak++;
            return;
        }

        LoadFx();
        // Echo needs the ghost prefab; without it the mood is simply never rolled.
        SwingMood rolled = s.Pick(NextValue(), ghost != null);
        if (rolled == SwingMood.Steady)
        {
            calmStreak++;
            return;
        }

        calmStreak = 0;
        mood = rolled;
        stone = spawned.transform;
        stoneRenderer = spawned.GetComponentInChildren<SpriteRenderer>();
        age = 0f;
        weight = 0f;
        rolledCount++;
        OnMoodRolled?.Invoke(mood);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
    }

    /// <summary>
    /// Whether this stone gets a mood. With pacing on: a calm beat always follows a mood, the first
    /// mood is guaranteed shortly after moods open up, a long calm spell is broken, and otherwise the
    /// chance climbs with the height of the tower.
    /// </summary>
    private bool ShouldRoll(SwingMoodSettings s, int stonesPlaced)
    {
        float draw = NextValue(); // always drawn, so the stream stays aligned across settings
        if (!s.enablePacing) return draw < s.moodChance;

        if (rolledCount > 0 && calmStreak < s.calmStonesAfterMood) return false;
        if (rolledCount == 0 && stonesPlaced >= s.firstMoodStone + s.guaranteeFirstMoodWithin) return true;
        if (calmStreak >= s.maxCalmStones) return true;
        return draw < s.ChanceAt(stonesPlaced);
    }

    /// <summary>The stone was dropped (or the run ended): the mood is over.</summary>
    public void EndStone()
    {
        mood = SwingMood.Steady;
        stone = null;
        stoneRenderer = null;
        weight = 0f;
        age = 0f;
        SpeedFactor = 1f;
        OffsetX = 0f;
        historyCount = 0;
        if (glow != null) glow.gameObject.SetActive(false);
        if (ghost != null) ghost.gameObject.SetActive(false);
    }

    /// <summary>
    /// Advances the mood by <paramref name="dt"/>. <paramref name="phase"/> is the swing's sine
    /// (-1 left edge, 1 right edge) as it stands before this frame's step.
    /// </summary>
    public void Tick(float dt, float phase)
    {
        SwingMoodSettings s = Settings;
        if (mood == SwingMood.Steady || s == null || stone == null)
        {
            SpeedFactor = 1f;
            OffsetX = 0f;
            return;
        }

        age += dt;
        float active = Mathf.Max(0f, age - s.cueSeconds);
        weight = Mathf.Clamp01(active / Mathf.Max(0.01f, s.rampSeconds));

        float speed = 1f;
        float offset = 0f;
        switch (mood)
        {
            case SwingMood.Quickening:
                speed = 1f + s.quickeningGain * Mathf.Clamp01(active / s.quickeningSeconds);
                break;
            case SwingMood.Lurch:
                speed = 1f - (1f - s.lurchEdgeSpeed) * Mathf.Pow(Mathf.Abs(phase), s.lurchSharpness);
                break;
            case SwingMood.Drift:
                offset = Mathf.Sin(active * s.driftRate) * s.driftAmplitude * weight;
                break;
        }
        SpeedFactor = Mathf.Lerp(1f, speed, weight);
        OffsetX = offset;

        UpdateFx(s);
    }

    private void UpdateFx(SwingMoodSettings s)
    {
        Color tint = s.ColorOf(mood);

        if (mood == SwingMood.Echo) RecordHistory(stone);

        if (glow != null)
        {
            SpriteRenderer r = glow.Renderer;
            bool cueing = age < s.cueSeconds;
            // The cue pulses; once the mood is running the glow settles to a faint steady ring.
            float pulse = cueing ? 0.65f + 0.35f * Mathf.Sin(age * 18f) : 1f;
            float alpha = (cueing ? s.cueGlowAlpha : Mathf.Lerp(s.cueGlowAlpha, s.activeGlowAlpha, weight)) * pulse;
            glow.gameObject.SetActive(true);
            glow.transform.position = stone.position;
            glow.transform.rotation = stone.rotation;
            float size = cueing ? 1.25f + 0.1f * Mathf.Sin(age * 18f) : 1.2f;
            glow.transform.localScale = stone.lossyScale * size;
            if (r != null)
            {
                if (stoneRenderer != null)
                {
                    r.sortingLayerID = stoneRenderer.sortingLayerID;
                    r.sortingOrder = stoneRenderer.sortingOrder - 1;
                }
                r.color = new Color(tint.r, tint.g, tint.b, alpha);
            }
        }

        if (mood == SwingMood.Echo && ghost != null) UpdateGhost(s, tint);
    }

    private void UpdateGhost(SwingMoodSettings s, Color tint)
    {
        SpriteRenderer r = ghost.Renderer;
        if (r == null || stoneRenderer == null || historyCount < 2)
        {
            ghost.gameObject.SetActive(false);
            return;
        }

        float target = Time.time - s.echoDelay;
        // Walk back from the newest sample to the one at or before the target time.
        int idx = (historyHead - 1 + HistorySize) % HistorySize;
        for (int i = 0; i < historyCount - 1; i++)
        {
            if (timeHistory[idx] <= target) break;
            idx = (idx - 1 + HistorySize) % HistorySize;
        }

        ghost.gameObject.SetActive(true);
        ghost.transform.position = posHistory[idx];
        ghost.transform.rotation = Quaternion.Euler(0f, 0f, rotHistory[idx]);
        ghost.transform.localScale = stone.lossyScale;
        r.sprite = stoneRenderer.sprite;
        r.flipX = stoneRenderer.flipX;
        r.sortingLayerID = stoneRenderer.sortingLayerID;
        r.sortingOrder = stoneRenderer.sortingOrder - 1;
        r.color = new Color(tint.r, tint.g, tint.b, s.echoAlpha * weight);
    }

    private void RecordHistory(Transform t)
    {
        posHistory[historyHead] = t.position;
        rotHistory[historyHead] = t.eulerAngles.z;
        timeHistory[historyHead] = Time.time;
        historyHead = (historyHead + 1) % HistorySize;
        historyCount = Mathf.Min(historyCount + 1, HistorySize);
    }

    private float NextValue()
    {
        return rng != null ? (float)rng.NextDouble() : Random.value;
    }

    private void LoadFx()
    {
        if (fxLoaded) return;
        fxLoaded = true;
        SwingMoodFx prefab = Resources.Load<SwingMoodFx>(SwingMoodFx.ResourcePath);
        if (prefab == null)
        {
            if (!fxWarned)
            {
                fxWarned = true;
                Debug.LogWarning("[SwingMoods] Fx prefab missing: run TamalStacker > Swing Moods > Create Fx Prefab. Moods roll without a glow and Echo is skipped.");
            }
            return;
        }
        glow = Object.Instantiate(prefab);
        glow.name = "SwingMoodGlow";
        glow.gameObject.SetActive(false);
        ghost = Object.Instantiate(prefab);
        ghost.name = "SwingMoodEcho";
        ghost.gameObject.SetActive(false);
    }
}
