using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Share chip over the result card: one tap opens the system share sheet with a short brag
/// ("I stacked 18 stones in Mayan Stacker - a Clutch Save!") and the store link.
///
/// It never blocks anything: only the chip takes taps, and it leaves with the card. Presentation
/// comes only from the authored prefab at Resources/UI/ShareResult; without it a warning names the
/// menu that builds it and the chip is skipped (no code-built fallback). Text only for now - an
/// image share needs a FileProvider in the Android manifest (see ralph/QUESTIONS.md Q7).
/// Menu: TamalStacker ▸ UI ▸ Create Share Result Assets.
/// </summary>
public class ShareResultView : MonoBehaviour
{
    /// <summary>Above the game-over card, level with the Offering chip.</summary>
    public const int SortingOrder = 3500;
    public const string PrefabResourcePath = "UI/ShareResult";

    [SerializeField] private RectTransform chip;
    [SerializeField] private Button chipButton;
    [SerializeField] private TextMeshProUGUI label;

    private static ShareResultView instance;

    private string message;
    private System.Func<bool> hostAlive;

    /// <summary>
    /// Offers the chip for a finished run. <paramref name="cardAlive"/> reports whether the result
    /// card is still up; when it stops being, the chip leaves.
    /// </summary>
    public static void Offer(RunResult.Card card, int maxCombo, System.Func<bool> cardAlive)
    {
        var settings = ShareSettings.Active;
        if (settings == null) return;
        if (!int.TryParse(card.height, out int stones) || stones < settings.minStones) return;

        if (instance == null) instance = Create();
        if (instance == null) return;

        instance.message = RunHighlight.BuildMessage(card, maxCombo, settings);
        instance.hostAlive = cardAlive;
        instance.label.text = LocalizationManager.Get("share_button");
        UIPopup.Show(instance.chip.gameObject);
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.Watch());
    }

    private static ShareResultView Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Share] Resources/{PrefabResourcePath} is missing - run " +
                             "TamalStacker > UI > Create Share Result Assets. The Share chip is skipped.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "ShareResultView";
        var view = go.GetComponent<ShareResultView>();
        if (view == null || view.chip == null || view.chipButton == null || view.label == null)
        {
            Debug.LogWarning($"[Share] Resources/{PrefabResourcePath} is missing its view or references - " +
                             "rebuild it with TamalStacker > UI > Create Share Result Assets. The Share chip is skipped.");
            Destroy(go);
            return null;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        view.chipButton.onClick.AddListener(view.OnTapped);
        view.chip.gameObject.SetActive(false);
        DontDestroyOnLoad(go);
        return view;
    }

    private void OnTapped()
    {
        if (string.IsNullOrEmpty(message)) return;
        HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
        ShareText(message);
    }

    private IEnumerator Watch()
    {
        // The card appears after a short beat; don't read "gone" before it has come.
        yield return new WaitForSecondsRealtime(0.25f);
        while (hostAlive != null && hostAlive()) yield return null;
        UIPopup.Hide(chip.gameObject);
    }

    private static void ShareText(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>(
                    "createChooser", intent, LocalizationManager.Get("share_chooser")))
                {
                    activity.Call("startActivity", chooser);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Share] Could not open the share sheet: {e.Message}");
        }
#else
        Debug.Log($"[Share] (no share sheet off-device) {text}");
#endif
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
