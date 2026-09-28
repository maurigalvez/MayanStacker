using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temple rule <see cref="LevelRule.Earthquake"/>: at set heights the ground shakes, and the
/// player holds the BRACE button to keep the tower still.
///
/// Sequence: the stone lands → the BRACE button (<see cref="BraceButtonView"/>; restyle and move
/// it with the Resources/UI/BraceButton prefab) appears with a rumble, its ring filling over
/// <see cref="EarthquakeSettings.warningSeconds"/> → the shaking, a jolt at a time, while the
/// ring drains to show how long is left to hold. The button's own word ("HOLD TO BRACE") is the
/// only instruction; there is no separate headline.
///
/// Brace OR drop: while BRACE is held nothing drops (<see cref="InputManager.DropsBlockedByRule"/>,
/// also enforced in <see cref="ObjectSpawner.DropCurrentObject"/> so a buffered tap can't slip through),
/// and every jolt only shakes the camera. Let go and a tap anywhere else drops as usual, but
/// each jolt slides the stones out, each one its own way for the whole quake, the top most and
/// the base least, so a straight tower comes apart. The swinging head also wobbles for the whole quake
/// (<see cref="SwingModifiers.ShakeOffset"/>), so building through it costs aim.
/// Stones a Kukulkan shift locked never slide, so building straight before the quake still pays.
///
/// Each quake gives every stone a direction, left or right, seeded by temple, quake number and
/// the stone's height in the stack, so every
/// player faces the same quake.
/// </summary>
public class Earthquake : TempleRuleBehaviour
{
    private const int CanvasSortingOrder = 3005; // above the HUD, below the power button
    private const float StartDelaySeconds = 0f;
    private const float EndHoldSeconds = 0.35f;    // button stays up briefly so a held finger can lift
    private const float SlideSeconds = 0.09f;      // one jolt's slide; well under the jolt interval

    private static readonly Color BracedColor = new Color(0.35f, 0.85f, 0.65f, 1f);

    public override LevelRule Rule => LevelRule.Earthquake;

    private enum Phase { Idle, Pending, Warning, Shaking, Ending }

    private EarthquakeSettings settings;
    private Phase phase = Phase.Idle;
    private float phaseTimer;
    private float nextJoltAt;
    private int quakesThisRun;
    private int slideSeed;
    private float bracedTime;
    private float shakeTime;
    private bool wasBraced;

    // UI
    private GameObject uiRoot;
    private CanvasGroup group;
    private BraceButtonView brace;

    protected override void Build()
    {
        uiRoot = new GameObject("EarthquakeUI");
        uiRoot.transform.SetParent(transform, false);
        RunOverlayUI.CreateCanvas(uiRoot, CanvasSortingOrder, interactive: true);
        group = uiRoot.AddComponent<CanvasGroup>();

        // The BRACE button: the authored prefab when there's a usable one, else the code layout.
        brace = BuildBraceFromPrefab() ?? BraceButtonView.BuildDefault(uiRoot.transform);
        brace.Init(art.quakeHoldIcon);

        uiRoot.SetActive(false);
    }

    /// <summary>
    /// Instantiates Resources/UI/BraceButton under the quake canvas, if there is a usable one.
    /// Null means the code-built button is used instead.
    /// </summary>
    private BraceButtonView BuildBraceFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(BraceButtonView.PrefabResourcePath);
        if (prefab == null) return null;

        GameObject instance = Instantiate(prefab, uiRoot.transform, false);

        // The prefab's root is usually its own canvas, whose size and scale were driven in
        // the editor. Nested, nothing drives them, so fill the quake canvas explicitly or the
        // button's anchors would measure from a stale rect.
        if (instance.transform is RectTransform rootRect)
        {
            RunOverlayUI.Stretch(rootRect);
            rootRect.localScale = Vector3.one;
            rootRect.anchoredPosition3D = Vector3.zero;
        }

        BraceButtonView view = instance.GetComponentInChildren<BraceButtonView>(true);
        if (view != null && view.IsUsable) return view;

        Debug.LogWarning($"[Earthquake] Resources/{BraceButtonView.PrefabResourcePath} has no usable " +
                         "BraceButtonView (needs a Hold Target) - using the code-built button instead.");
        // Hide first: Destroy is deferred, and a half-built button shouldn't flash for a frame.
        instance.SetActive(false);
        Destroy(instance);
        return null;
    }

    protected override void OnRunStart(LevelData level)
    {
        settings = RuleActive ? level.quakeSettings : null;
        quakesThisRun = 0;
        EndQuake();
    }

    protected override void OnRunEnd(bool won)
    {
        if (phase == Phase.Idle) return;
        EndQuake();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (phase != Phase.Idle) EndQuake();
    }

    protected override void OnStoneLanded(StackableObject stone)
    {
        if (settings == null || stackManager == null || phase != Phase.Idle) return;

        int height = stackManager.GetStackCount();
        if (height < settings.firstAtHeight) return;
        if ((height - settings.firstAtHeight) % Mathf.Max(2, settings.everyNStones) != 0) return;

        // Never on or just before the final stone: the capstone locks the tower then.
        LevelData level = Level;
        if (level != null && height >= level.requiredStackHeight - 1) return;

        phase = Phase.Pending;
        phaseTimer = StartDelaySeconds;
    }

    protected override void Update()
    {
        base.Update();
        if (phase == Phase.Idle) return;

        bool paused = uiManager != null && uiManager.IsPaused;
        // The pause menu must stay reachable: stand aside, and freeze the quake with the game.
        if (uiRoot.activeSelf == paused && phase != Phase.Pending) uiRoot.SetActive(!paused);
        if (paused)
        {
            InputManager.DropsBlockedByRule = false;
            return;
        }

        if (!RunLive)
        {
            EndQuake();
            return;
        }

        float dt = Time.deltaTime;
        phaseTimer -= dt;
        bool braced = brace != null && brace.IsHeld && phase != Phase.Pending;

        switch (phase)
        {
            case Phase.Pending:
                if (phaseTimer <= 0f) BeginWarning();
                break;

            case Phase.Warning:
                if (cameraController != null && Time.frameCount % 6 == 0) cameraController.Shake(0.06f);
                if (phaseTimer <= 0f) BeginShaking();
                break;

            case Phase.Shaking:
                shakeTime += dt;
                if (braced) bracedTime += dt;
                if (Time.time >= nextJoltAt)
                {
                    Jolt(braced);
                    nextJoltAt = Time.time + 1f / Mathf.Max(0.5f, settings.joltsPerSecond);
                }
                if (phaseTimer <= 0f) BeginEnding();
                break;

            case Phase.Ending:
                if (phaseTimer <= 0f) EndQuake();
                break;
        }

        if (phase == Phase.Idle) return; // EndQuake ran this frame

        // Brace OR drop: a held brace keeps a second finger from releasing the stone.
        InputManager.DropsBlockedByRule = braced;
        SwingModifiers.ShakeOffset = HeadShake();

        if (braced && !wasBraced && (phase == Phase.Warning || phase == Phase.Shaking))
        {
            PlayOneShot(art.quakeBraceSound, 0.9f);
            HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
        }
        wasBraced = braced;

        UpdateUI(braced);
    }

    private void BeginWarning()
    {
        phase = Phase.Warning;
        phaseTimer = settings.warningSeconds;
        quakesThisRun++;
        bracedTime = 0f;
        shakeTime = 0f;

        // Same temple, same quake number → same slides for every player.
        int levelNumber = Level != null ? Level.levelNumber : 0;
        slideSeed = levelNumber * 7919 + quakesThisRun * 104729;

        group.alpha = 1f;
        uiRoot.SetActive(true);
        brace.ResetHold();

        SetLoop(art.quakeRumbleLoop, 0.55f);
        HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);

        GameAnalytics.Track("quake_started", EventData());
    }

    private void BeginShaking()
    {
        phase = Phase.Shaking;
        phaseTimer = settings.quakeSeconds;
        nextJoltAt = Time.time;
        SetLoop(art.quakeRumbleLoop, 1f);
    }

    private void BeginEnding()
    {
        phase = Phase.Ending;
        phaseTimer = EndHoldSeconds;
        FadeOutLoop();

        var data = EventData();
        data["braced_share"] = shakeTime > 0f ? Mathf.Clamp01(bracedTime / shakeTime) : 0f;
        GameAnalytics.Track("quake_survived", data);

        RunBanner.Show(LocalizationManager.Get("quake_survived"), BracedColor, 1.1f, -180f);
    }

    private void EndQuake()
    {
        phase = Phase.Idle;
        if (uiRoot != null) uiRoot.SetActive(false);
        if (brace != null) brace.ResetHold();
        wasBraced = false;
        InputManager.DropsBlockedByRule = false;
        SwingModifiers.ShakeOffset = 0f;
        FadeOutLoop();
    }

    /// <summary>
    /// One jolt. Braced: only the camera shakes. Not braced: every unlocked stone slides a
    /// step out its own direction (fixed for the quake), scaled by how high it sits.
    /// </summary>
    private void Jolt(bool braced)
    {
        if (cameraController != null) cameraController.Shake(braced ? 0.12f : 0.4f);
        HapticFeedback.Trigger(braced ? HapticFeedback.HapticType.Light : HapticFeedback.HapticType.Heavy);
        PlayOneShot(art.quakeJoltSound, braced ? 0.5f : 1f);

        if (braced || stackManager == null) return;

        IReadOnlyList<StackableObject> stones = stackManager.StackObjects;
        int count = stones.Count;

        for (int i = 1; i < count; i++) // the foundation never moves
        {
            StackableObject stone = stones[i];
            if (stone == null || stackManager.IsStabilized(stone)) continue;

            Rigidbody2D rb = stone.GetComponent<Rigidbody2D>();
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) continue; // mid-move or detached

            float heightShare = count > 1 ? (float)i / (count - 1) : 1f;
            float strength = 0.6f + 0.4f * Hash01(slideSeed + 1, i);
            float direction = Hash01(slideSeed, i) < 0.5f ? -1f : 1f; // same for this stone all quake

            stackManager.ShoveStone(stone, direction * settings.slidePerJolt * strength * heightShare, SlideSeconds);
        }
    }

    /// <summary>The head's sideways wobble: builds through the warning, full while shaking, fades out.</summary>
    private float HeadShake()
    {
        float strength;
        switch (phase)
        {
            case Phase.Warning:
                strength = 0.35f * (1f - Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, settings.warningSeconds)));
                break;
            case Phase.Shaking:
                strength = 1f;
                break;
            case Phase.Ending:
                strength = Mathf.Clamp01(phaseTimer / EndHoldSeconds);
                break;
            default:
                return 0f;
        }

        // Two unrelated frequencies, so the wobble can't be timed like the swing.
        float t = Time.time;
        float wave = 0.65f * Mathf.Sin(t * 23f) + 0.35f * Mathf.Sin(t * 37f + 1.3f);
        return wave * settings.headShake * strength;
    }

    private void UpdateUI(bool braced)
    {
        if (brace == null) return;

        bool shaking = phase == Phase.Shaking;

        // The ring is the timer: it fills while the quake winds up, then drains while it shakes.
        float fill;
        float? secondsLeft;
        switch (phase)
        {
            case Phase.Warning:
                fill = 1f - Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, settings.warningSeconds));
                secondsLeft = settings.quakeSeconds;
                break;
            case Phase.Shaking:
                fill = Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, settings.quakeSeconds));
                secondsLeft = Mathf.Max(0f, phaseTimer);
                break;
            default:
                fill = 0f;
                secondsLeft = null;
                break;
        }
        brace.Render(fill, secondsLeft, braced, shaking);

        if (phase == Phase.Ending) group.alpha = Mathf.Clamp01(phaseTimer / EndHoldSeconds);
    }

    /// <summary>Stable 0..1 value for (seed, index). No allocation, same on every device.</summary>
    private static float Hash01(int seed, int index)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)index * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    private Dictionary<string, object> EventData()
    {
        return new Dictionary<string, object>
        {
            { "level", Level != null ? Level.levelNumber : 0 },
            { "height", stackManager != null ? stackManager.GetStackCount() : 0 },
            { "quake", quakesThisRun }
        };
    }
}
