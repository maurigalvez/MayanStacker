using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the first-run tutorial's Skip control.
///
/// The beat lines themselves no longer live here: they speak through the
/// <see cref="GuideLane"/>, the low strip every lesson shares, so the tutorial can't sit in
/// the fall path. This prefab now carries the Skip button and the tutorial's pacing values.
///
/// Nothing here is required. A missing Skip button falls back to the code-built corner
/// control, and if the prefab is absent entirely the tutorial still runs.
///
/// Menu: TamalStacker ▸ FTUE ▸ Create Tutorial Prefab generates a prefab matching the
/// code-built layout, as a starting point to restyle.
/// </summary>
public class FtueTutorialView : MonoBehaviour
{
    [Header("Legacy message (hidden)")]
    [Tooltip("Left over from when the tutorial drew its own banner mid-screen. Hidden at " +
             "runtime; safe to delete from the prefab. Style the Guide Lane prefab instead.")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Tooltip("Left over from when the tutorial drew its own banner mid-screen. Hidden at " +
             "runtime; safe to delete from the prefab.")]
    [SerializeField] private GameObject messagePanel;

    [Header("Skip")]
    [Tooltip("Shown from beat 2 onward — never during beat 1, because the single tap that " +
             "teaches the core verb is the one thing nobody should be able to skip past.")]
    [SerializeField] private Button skipButton;

    [Tooltip("Label inside the Skip button. Filled from the 'ftue_skip' key at runtime.")]
    [SerializeField] private TextMeshProUGUI skipLabel;

    [Header("Pacing")]
    [Tooltip("Floor for how long a beat line stays up. The actual hold grows with the " +
             "length of the line, so longer copy — and longer translations — get more time.")]
    [SerializeField] private float beatMinimumSeconds = 3.6f;

    [Tooltip("Shortest time a line is guaranteed on screen before the next beat may replace " +
             "it. Stops a quick combo from wiping a line before it has been read.")]
    [SerializeField] private float minimumDwellSeconds = 2.5f;

    private Action onSkip;

    /// <summary>Floor for a beat's on-screen time; the tutorial adds reading time on top.</summary>
    public float BeatMinimumSeconds => beatMinimumSeconds;

    /// <summary>How long a line is protected from being replaced by the next beat.</summary>
    public float MinimumDwellSeconds => minimumDwellSeconds;

    /// <summary>True when this prefab carries its own Skip control.</summary>
    public bool HasSkipButton => skipButton != null;

    private void Awake()
    {
        // The old mid-screen message is never shown again; older prefabs still carry it
        // authored visible, so switch it off before anyone sees it.
        if (messagePanel != null) messagePanel.SetActive(false);
        else if (messageText != null) messageText.gameObject.SetActive(false);

        SetSkipVisible(false);

        if (skipButton != null) skipButton.onClick.AddListener(HandleSkip);
    }

    private void OnDestroy()
    {
        if (skipButton != null) skipButton.onClick.RemoveListener(HandleSkip);
    }

    /// <summary>Registers the tutorial's skip handler. Passing null detaches it.</summary>
    public void SetSkipHandler(Action handler) => onSkip = handler;

    public void SetSkipVisible(bool visible)
    {
        if (skipButton == null) return;

        if (skipLabel != null && visible) skipLabel.text = LocalizationManager.Get("ftue_skip");
        skipButton.gameObject.SetActive(visible);
    }

    private void HandleSkip() => onSkip?.Invoke();
}
