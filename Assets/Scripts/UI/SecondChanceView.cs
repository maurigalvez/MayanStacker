using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Second Chance: the moment a temple stone misses the tower, the run holds and Kukulkan offers
/// to give that stone back. Accepting plays a rewarded ad (free for Remove Ads owners); declining,
/// or letting the timer run out, ends the run exactly as a miss always did.
///
/// <see cref="TryOffer"/> is called from <see cref="GameManager.GameOver"/> before the run is
/// committed as over. It only takes the miss when every guard passes, so a refusal costs nothing:
/// no ad plays and no state changes.
///
/// Presentation comes only from the authored prefab at Resources/UI/SecondChance; without it a
/// warning names the menu that builds it and the miss ends the run as before (no code-built
/// fallback). Menu: TamalStacker ▸ UI ▸ Create Second Chance Assets.
/// </summary>
public class SecondChanceView : MonoBehaviour
{
    /// <summary>Above the game HUD, below nothing the run needs: the run is held while it is up.</summary>
    public const int SortingOrder = 3600;
    public const string PrefabResourcePath = "UI/SecondChance";

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI subLabel;
    [SerializeField] private Button acceptButton;
    [SerializeField] private TextMeshProUGUI acceptLabel;
    [SerializeField] private Button declineButton;
    [SerializeField] private TextMeshProUGUI declineLabel;
    [Tooltip("Optional: fills as the offer runs out.")]
    [SerializeField] private Image timerFill;

    private static SecondChanceView instance;
    private static int offersThisRun;

    private GameManager gameManager;
    private string cause;
    private StackableObject culprit;
    private float savedTimeScale = 1f;
    private bool holding;
    private bool offering;
    private bool adPlaying;

    /// <summary>Called at every run start so the offer allowance is per run.</summary>
    public static void BeginRun() => offersThisRun = 0;

    /// <summary>True while an offer is up; further miss reports are swallowed, not re-offered.</summary>
    public static bool IsOffering => instance != null && instance.offering;

    /// <summary>
    /// Offers a second chance for a miss. Returns true when the miss is being held for the player's
    /// answer (the caller must not end the run); false when the run should end as usual.
    /// </summary>
    public static bool TryOffer(GameManager manager, string missCause, StackableObject missedStone)
    {
        if (IsOffering) return true;

        var settings = SecondChanceSettings.Active;
        if (settings == null || manager == null) return false;
        if (manager.CurrentGameMode != GameMode.StackerLevels || missCause != "missed_stack") return false;
        if (offersThisRun >= settings.offersPerRun) return false;

        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager == null || stackManager.GetStackCount() < settings.minStones) return false;

        // Only offered when the reward can actually be delivered.
        if (!AdManager.CanOfferReward) return false;

        if (instance == null) instance = Create();
        if (instance == null) return false;

        offersThisRun++;
        instance.Begin(settings, manager, missCause, missedStone);
        return true;
    }

    private static SecondChanceView Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[SecondChance] Resources/{PrefabResourcePath} is missing - run " +
                             "TamalStacker > UI > Create Second Chance Assets. The offer is skipped.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "SecondChanceView";
        var view = go.GetComponent<SecondChanceView>();
        if (view == null || view.panel == null || view.acceptButton == null || view.acceptLabel == null ||
            view.declineButton == null || view.declineLabel == null || view.titleLabel == null || view.subLabel == null)
        {
            Debug.LogWarning($"[SecondChance] Resources/{PrefabResourcePath} is missing its view or references - " +
                             "rebuild it with TamalStacker > UI > Create Second Chance Assets. The offer is skipped.");
            Destroy(go);
            return null;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        view.acceptButton.onClick.AddListener(view.OnAccept);
        view.declineButton.onClick.AddListener(view.OnDecline);
        view.panel.gameObject.SetActive(false);
        DontDestroyOnLoad(go);
        return view;
    }

    private void Begin(SecondChanceSettings settings, GameManager manager, string missCause, StackableObject missedStone)
    {
        gameManager = manager;
        cause = missCause;
        culprit = missedStone;
        offering = true;
        adPlaying = false;

        // Hold the run: timers, water and tremor all stand still while the player decides.
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        holding = true;

        titleLabel.text = LocalizationManager.Get("second_chance_title");
        subLabel.text = LocalizationManager.Get("second_chance_sub");
        acceptLabel.text = LocalizationManager.Get(RemoveAds.Owned ? "second_chance_free" : "second_chance_ad");
        declineLabel.text = LocalizationManager.Get("second_chance_decline");
        acceptButton.interactable = true;
        declineButton.interactable = true;
        if (timerFill != null) timerFill.fillAmount = 1f;

        HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
        UIPopup.Show(panel.gameObject);
        StopAllCoroutines();
        StartCoroutine(Countdown(settings.offerSeconds));
    }

    private IEnumerator Countdown(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f)
        {
            // The ad owns the screen while it plays; the clock waits for it.
            if (!adPlaying) remaining -= Time.unscaledDeltaTime;
            if (timerFill != null) timerFill.fillAmount = Mathf.Clamp01(remaining / seconds);
            yield return null;
        }
        OnDecline();
    }

    private void OnAccept()
    {
        if (!offering || adPlaying) return;
        adPlaying = true;
        acceptButton.interactable = false;

        AdManager.RequestReward("second_chance", earned =>
        {
            adPlaying = false;
            if (!offering) return;

            if (!earned)
            {
                // Closed early or no ad: nothing is owed, the offer stays open until its timer.
                acceptButton.interactable = true;
                return;
            }
            Resolve(accepted: true);
        });
    }

    private void OnDecline()
    {
        if (!offering || adPlaying) return;
        Resolve(accepted: false);
    }

    private void Resolve(bool accepted)
    {
        offering = false;
        StopAllCoroutines();
        Release();
        UIPopup.Hide(panel.gameObject);

        var manager = gameManager;
        var stone = culprit;
        gameManager = null;
        culprit = null;

        if (manager == null) return;
        if (accepted) manager.ResumeAfterSecondChance(stone);
        else manager.GameOver(cause);
    }

    private void Release()
    {
        if (!holding) return;
        holding = false;
        Time.timeScale = savedTimeScale;
    }

    private void OnDestroy()
    {
        Release();
        if (instance == this) instance = null;
    }
}
