using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector-authored face of the first-run tutorial.
///
/// The tutorial used to speak through UIManager's instruction label and build its Skip
/// control in code, which meant the very first thing a new player reads could not be
/// styled — no font choice, no framing, no placement. Drop this component on a prefab at
/// Resources/UI/FtueTutorial and <see cref="FtueTutorial"/> uses it instead: same beats,
/// same timing, but the type and layout are editable like every other panel.
///
/// Nothing here is required. A missing message label falls back to UIManager's instruction
/// line, a missing Skip button falls back to the code-built corner control, and if the
/// prefab is absent entirely the tutorial behaves exactly as it did before.
///
/// Menu: TamalStacker ▸ FTUE ▸ Create Tutorial Prefab generates a prefab matching the
/// code-built layout, as a starting point to restyle.
/// </summary>
public class FtueTutorialView : MonoBehaviour
{
    [Header("Message")]
    [Tooltip("The tutorial line itself. Copy comes from the localization table at runtime, " +
             "so whatever is authored here is only a preview. Set the intended font here.")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Tooltip("Optional frame/banner shown and hidden with the message. Leave empty to " +
             "toggle the label's own object instead.")]
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

    /// <summary>True when this prefab can show the tutorial copy itself.</summary>
    public bool HasMessageLabel => messageText != null;

    /// <summary>True when this prefab carries its own Skip control.</summary>
    public bool HasSkipButton => skipButton != null;

    private void Awake()
    {
        // Hidden until the tutorial has something to say; the prefab is authored visible so
        // it stays easy to look at while styling.
        SetMessageVisible(false);
        SetSkipVisible(false);

        if (skipButton != null) skipButton.onClick.AddListener(HandleSkip);
    }

    private void OnDestroy()
    {
        if (skipButton != null) skipButton.onClick.RemoveListener(HandleSkip);
    }

    /// <summary>Registers the tutorial's skip handler. Passing null detaches it.</summary>
    public void SetSkipHandler(Action handler) => onSkip = handler;

    public void ShowMessage(string message)
    {
        if (messageText == null) return;
        messageText.text = message;
        SetMessageVisible(true);
    }

    public void HideMessage() => SetMessageVisible(false);

    public void SetSkipVisible(bool visible)
    {
        if (skipButton == null) return;

        if (skipLabel != null && visible) skipLabel.text = LocalizationManager.Get("ftue_skip");
        skipButton.gameObject.SetActive(visible);
    }

    private void SetMessageVisible(bool visible)
    {
        if (messagePanel != null) messagePanel.SetActive(visible);
        else if (messageText != null) messageText.gameObject.SetActive(visible);
    }

    private void HandleSkip() => onSkip?.Invoke();
}
