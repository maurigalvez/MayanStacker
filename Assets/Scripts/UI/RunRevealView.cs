using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The post-run Offering: a small jade chip over the Try Again card that opens for a bonus.
///
/// It never blocks anything. There is no backdrop, only the chip itself takes taps, and it opens
/// once. If the player leaves the card without tapping (Try Again, menu), the offering pays out
/// on its own as the card closes, so skipping never costs jade.
///
/// Presentation comes only from the authored prefab at Resources/UI/RunReveal; without it a
/// warning names the menu that builds it and the reveal is skipped (no code-built fallback).
/// Menu: TamalStacker ▸ UI ▸ Create Run Reveal Assets.
/// </summary>
public class RunRevealView : MonoBehaviour
{
    /// <summary>Above the game-over card; the chip is small and sits clear of the buttons.</summary>
    public const int SortingOrder = 3500;
    public const string PrefabResourcePath = "UI/RunReveal";

    [SerializeField] private RectTransform chip;
    [SerializeField] private Button chipButton;
    [SerializeField] private TextMeshProUGUI promptLabel;
    [SerializeField] private TextMeshProUGUI tierLabel;
    [SerializeField] private TextMeshProUGUI amountLabel;
    [Tooltip("Optional: the Double jade button, shown under the opened chip. Added by Create Run Reveal Assets.")]
    [SerializeField] private Button doubleButton;
    [SerializeField] private TextMeshProUGUI doubleLabel;

    private static RunRevealView instance;

    private int runJade;
    private int doubleAmount;
    private bool doubling;
    private bool doubled;

    private Func<bool> hostAlive;
    private RunRevealSettings.Tier rolled;
    private bool open;
    private bool opened;

    /// <summary>
    /// Offers the reveal for a finished run. <paramref name="cardAlive"/> reports whether the
    /// result card is still up; when it stops being, the offering resolves and the chip leaves.
    /// </summary>
    public static void Offer(int runJade, Func<bool> cardAlive)
    {
        var settings = RunRevealSettings.Active;
        if (settings == null || runJade < settings.minRunJade) return;

        if (instance == null) instance = Create();
        if (instance == null) return;

        instance.runJade = runJade;
        instance.Open(settings, cardAlive);
    }

    private static RunRevealView Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[RunReveal] Resources/{PrefabResourcePath} is missing - run " +
                             "TamalStacker > UI > Create Run Reveal Assets. The reveal is skipped.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "RunRevealView";
        var view = go.GetComponent<RunRevealView>();
        if (view == null || view.chip == null || view.chipButton == null || view.promptLabel == null ||
            view.tierLabel == null || view.amountLabel == null)
        {
            Debug.LogWarning($"[RunReveal] Resources/{PrefabResourcePath} is missing its view or references - " +
                             "rebuild it with TamalStacker > UI > Create Run Reveal Assets. The reveal is skipped.");
            Destroy(go);
            return null;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        view.chipButton.onClick.AddListener(view.OnTapped);
        if (view.doubleButton != null && view.doubleLabel != null)
        {
            view.doubleButton.onClick.AddListener(view.OnDoubleTapped);
            view.doubleButton.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"[RunReveal] Resources/{PrefabResourcePath} has no Double jade button - run " +
                             "TamalStacker > UI > Create Run Reveal Assets to add it. Double jade is skipped.");
            view.doubleButton = null;
        }

        view.chip.gameObject.SetActive(false);
        DontDestroyOnLoad(go);
        return view;
    }

    private void Open(RunRevealSettings settings, Func<bool> cardAlive)
    {
        // A previous run's offering that never resolved still pays out.
        if (open) Resolve(silent: true);

        hostAlive = cardAlive;
        rolled = settings.Roll(UnityEngine.Random.value);
        open = true;
        opened = false;
        doubling = false;
        doubled = false;
        if (doubleButton != null) doubleButton.gameObject.SetActive(false);

        promptLabel.text = LocalizationManager.Get("reveal_prompt");
        promptLabel.gameObject.SetActive(true);
        tierLabel.gameObject.SetActive(false);
        amountLabel.gameObject.SetActive(false);
        chipButton.interactable = true;

        UIPopup.Show(chip.gameObject);
        StopAllCoroutines();
        StartCoroutine(Watch());
    }

    private void OnTapped()
    {
        if (!open || opened) return;
        Resolve(silent: false);
    }

    /// <summary>Pays the rolled bonus. Silent when the card closed before the player opened it.</summary>
    private void Resolve(bool silent)
    {
        if (!open || opened) return;
        opened = true;

        if (rolled.bonusJade > 0) JadeWallet.Earn("reveal", rolled.bonusJade);
        if (silent) return;

        chipButton.interactable = false;
        promptLabel.gameObject.SetActive(false);

        tierLabel.text = LocalizationManager.Get(rolled.nameKey);
        tierLabel.gameObject.SetActive(true);
        amountLabel.text = rolled.bonusJade > 0
            ? LocalizationManager.Get("result_jade_line", rolled.bonusJade)
            : LocalizationManager.Get("reveal_plain_sub");
        amountLabel.gameObject.SetActive(true);

        HapticFeedback.Trigger(rolled.grand ? HapticFeedback.HapticType.Heavy : HapticFeedback.HapticType.Light);
        StartCoroutine(Punch(chip));
        OfferDouble();
    }

    private void OfferDouble()
    {
        var settings = RunRevealSettings.Active;
        if (doubleButton == null || settings == null || !settings.enableDoubleJade) return;

        doubleAmount = settings.DoubleAmount(runJade, rolled.bonusJade);
        if (doubleAmount <= 0 || !AdManager.CanOfferReward) return;

        doubleLabel.text = LocalizationManager.Get(
            RemoveAds.Owned ? "reveal_double_free" : "reveal_double_ad", doubleAmount);
        doubleButton.interactable = true;
        UIPopup.Show(doubleButton.gameObject);
    }

    private void OnDoubleTapped()
    {
        if (doubling || doubled) return;
        doubling = true;
        doubleButton.interactable = false;

        // The payout lands from the callback, so it still arrives if the card closes meanwhile.
        int amount = doubleAmount;
        var label = doubleLabel;
        var button = doubleButton;
        AdManager.RequestReward("double_jade", earned =>
        {
            doubling = false;
            if (!earned)
            {
                if (button != null) button.interactable = true;
                return;
            }

            doubled = true;
            JadeWallet.Earn("double_ad", amount);
            if (label != null) label.text = LocalizationManager.Get("reveal_double_done");
            HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
        });
    }

    private IEnumerator Watch()
    {
        // The card appears after a short beat; don't read "gone" before it has come.
        yield return new WaitForSecondsRealtime(0.25f);

        while (hostAlive != null && hostAlive()) yield return null;

        Resolve(silent: true);
        open = false;
        UIPopup.Hide(chip.gameObject);
        if (doubleButton != null) doubleButton.gameObject.SetActive(false);
    }

    private static IEnumerator Punch(RectTransform target)
    {
        float elapsed = 0f;
        const float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            target.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI));
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
