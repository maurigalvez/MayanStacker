using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Temple rule <see cref="LevelRule.Earthquake"/>: at set heights the ground shakes, and the
/// player presses and holds anywhere to brace the tower.
///
/// Sequence: the stone lands → a HOLD prompt and a rumble (<see cref="EarthquakeSettings.warningSeconds"/>)
/// → the shaking, a jolt at a time, each one knocking the stones sideways (the top most, the
/// base least). Holding cuts every jolt to <see cref="EarthquakeSettings.bracedFactor"/>; letting
/// go takes the full jolt. Stones a Kukulkan shift locked are never jolted, so building
/// straight before the quake still pays.
///
/// While the quake runs, a full-screen hold surface sits over the playfield. It is a raycast
/// target, so InputManager — which never drops a stone for a press over UI — can't drop one,
/// and the press that braces can never also release the swinging stone. It stands aside while
/// the game is paused so the pause menu keeps working.
///
/// The jolt pattern is fixed (alternating, no randomness), so every player faces the same quake.
/// </summary>
public class Earthquake : TempleRuleBehaviour
{
    private const int CanvasSortingOrder = 3005; // above the HUD, below the power button
    private const float StartDelaySeconds = 0f; // next frame: the HOLD surface must be up before the next stone arms
    private const float EndHoldSeconds = 0.35f;    // surface stays up briefly so a held finger can lift

    private static readonly Color PromptColor = RunOverlayUI.Gold;
    private static readonly Color BracedColor = new Color(0.35f, 0.85f, 0.65f, 1f);

    public override LevelRule Rule => LevelRule.Earthquake;

    private enum Phase { Idle, Pending, Warning, Shaking, Ending }

    private EarthquakeSettings settings;
    private Phase phase = Phase.Idle;
    private float phaseTimer;
    private float nextJoltAt;
    private int joltIndex;
    private int quakesThisRun;
    private float bracedTime;
    private float shakeTime;
    private bool wasBraced;

    // UI
    private GameObject uiRoot;
    private CanvasGroup group;
    private HoldSurface surface;
    private TextMeshProUGUI prompt;
    private RectTransform iconRect;
    private Image icon;

    protected override void Build()
    {
        uiRoot = new GameObject("EarthquakeUI");
        uiRoot.transform.SetParent(transform, false);
        RunOverlayUI.CreateCanvas(uiRoot, CanvasSortingOrder, interactive: true);
        group = uiRoot.AddComponent<CanvasGroup>();

        // The hold surface: invisible, full screen, takes every press.
        RectTransform surfaceRect = RunOverlayUI.CreateChild("HoldSurface", uiRoot.transform);
        RunOverlayUI.Stretch(surfaceRect);
        var surfaceImage = surfaceRect.gameObject.AddComponent<Image>();
        surfaceImage.color = new Color(0f, 0f, 0f, 0.001f);
        surfaceImage.raycastTarget = true;
        surface = surfaceRect.gameObject.AddComponent<HoldSurface>();

        // Prompt below centre (the swing band is up top).
        iconRect = RunOverlayUI.CreateChild("HoldIcon", uiRoot.transform);
        RunOverlayUI.Place(iconRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(170f, 170f));
        icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = art.quakeHoldIcon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = art.quakeHoldIcon != null;

        prompt = RunOverlayUI.CreateLabel("Prompt", uiRoot.transform, string.Empty, 84f, PromptColor);
        RunOverlayUI.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -280f), new Vector2(1000f, 140f));
        prompt.fontStyle = FontStyles.Bold;

        uiRoot.SetActive(false);
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
        if (paused) return;

        if (!RunLive)
        {
            EndQuake();
            return;
        }

        float dt = Time.deltaTime;
        phaseTimer -= dt;
        bool braced = surface != null && surface.IsHeld;

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

        if (braced && !wasBraced && (phase == Phase.Warning || phase == Phase.Shaking))
        {
            PlayOneShot(art.quakeBraceSound, 0.9f);
            HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
        }
        wasBraced = braced;

        UpdatePrompt(braced);
    }

    private void BeginWarning()
    {
        phase = Phase.Warning;
        phaseTimer = settings.warningSeconds;
        quakesThisRun++;
        bracedTime = 0f;
        shakeTime = 0f;
        joltIndex = 0;

        group.alpha = 1f;
        uiRoot.SetActive(true);
        surface.ResetHold();

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
        if (surface != null) surface.ResetHold();
        wasBraced = false;
        FadeOutLoop();
    }

    /// <summary>
    /// One jolt: every unlocked stone gets a sideways kick, alternating direction, scaled by
    /// how high it sits (the base barely moves, the top swings) and by whether the player braces.
    /// </summary>
    private void Jolt(bool braced)
    {
        if (stackManager == null) return;

        float direction = (joltIndex % 2 == 0) ? 1f : -1f;
        joltIndex++;

        float factor = braced ? settings.bracedFactor : 1f;
        IReadOnlyList<StackableObject> stones = stackManager.StackObjects;
        int count = stones.Count;

        for (int i = 0; i < count; i++)
        {
            StackableObject stone = stones[i];
            if (stone == null || stackManager.IsStabilized(stone)) continue;

            Rigidbody2D rb = stone.GetComponent<Rigidbody2D>();
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) continue;

            float heightShare = count > 1 ? (float)i / (count - 1) : 1f;
            rb.linearVelocity += new Vector2(direction * settings.joltSpeed * factor * heightShare, 0f);
        }

        if (cameraController != null) cameraController.Shake(braced ? 0.12f : 0.4f);
        HapticFeedback.Trigger(braced ? HapticFeedback.HapticType.Light : HapticFeedback.HapticType.Heavy);
        PlayOneShot(art.quakeJoltSound, braced ? 0.5f : 1f);
    }

    private void UpdatePrompt(bool braced)
    {
        if (prompt == null) return;

        bool shaking = phase == Phase.Shaking;
        prompt.text = LocalizationManager.Get(braced ? "quake_braced" : "quake_hold");
        prompt.color = braced ? BracedColor : PromptColor;

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (shaking ? 14f : 8f));
        float scale = braced ? 0.92f : 1f + 0.08f * wave;
        prompt.rectTransform.localScale = Vector3.one * scale;
        if (iconRect != null) iconRect.localScale = Vector3.one * (braced ? 0.85f : 1f + 0.1f * wave);
        if (icon != null) icon.color = braced ? BracedColor : Color.white;

        if (phase == Phase.Ending) group.alpha = Mathf.Clamp01(phaseTimer / EndHoldSeconds);
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

    /// <summary>Full-screen press-and-hold target. Counts pointers so multi-touch can't flicker it.</summary>
    private class HoldSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private readonly HashSet<int> pressed = new HashSet<int>();

        public bool IsHeld => pressed.Count > 0;

        public void OnPointerDown(PointerEventData eventData) => pressed.Add(eventData.pointerId);

        public void OnPointerUp(PointerEventData eventData) => pressed.Remove(eventData.pointerId);

        public void ResetHold() => pressed.Clear();

        private void OnDisable() => pressed.Clear();
    }
}
