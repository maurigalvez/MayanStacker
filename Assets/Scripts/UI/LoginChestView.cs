using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The daily login chest panel: which day of the cycle it is, what today's chest holds, what
/// tomorrow's will, and one Open button.
///
/// Opened from the main-menu chip (<see cref="LoginChestMenuChip"/>); never pops up on its own, so
/// the menu stays quiet for a returning player. Presentation comes only from the authored prefab at
/// Resources/UI/LoginChest; without it a warning names the menu that builds it and nothing opens
/// (no code-built fallback). Menu: TamalStacker ▸ UI ▸ Create Login Chest Prefab.
/// </summary>
public class LoginChestView : MonoBehaviour
{
    public const int SortingOrder = 4300;
    public const string PrefabResourcePath = "UI/LoginChest";

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI dayLabel;
    [SerializeField] private TextMeshProUGUI rewardLabel;
    [SerializeField] private TextMeshProUGUI nextLabel;
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI claimLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI closeLabel;

    private static LoginChestView instance;

    private bool showing;

    public static bool Available => LoginChest.Available;

    public static bool IsShowing => instance != null && instance.showing;

    public static bool Show()
    {
        if (!Available) return false;
        if (instance == null) instance = Create();
        if (instance == null) return false;
        instance.Open();
        return true;
    }

    private static LoginChestView Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[LoginChest] Resources/{PrefabResourcePath} is missing - run " +
                             "TamalStacker > UI > Create Login Chest Prefab. The chest panel stays closed.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "LoginChestView";
        var view = go.GetComponent<LoginChestView>();
        if (view == null || view.panel == null || view.titleLabel == null || view.dayLabel == null ||
            view.rewardLabel == null || view.nextLabel == null || view.claimButton == null ||
            view.claimLabel == null || view.closeButton == null)
        {
            Debug.LogWarning($"[LoginChest] Resources/{PrefabResourcePath} is missing its view or references - " +
                             "rebuild it with TamalStacker > UI > Create Login Chest Prefab. The chest panel stays closed.");
            Destroy(go);
            return null;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        view.claimButton.onClick.AddListener(view.OnClaim);
        view.closeButton.onClick.AddListener(view.Close);
        view.panel.gameObject.SetActive(false);
        DontDestroyOnLoad(go);
        return view;
    }

    private void Open()
    {
        titleLabel.text = LocalizationManager.Get("login_chest_title");
        if (closeLabel != null) closeLabel.text = LocalizationManager.Get("login_chest_close");
        Refresh();
        showing = true;
        UIPopup.Show(panel.gameObject);
    }

    private void Close()
    {
        if (!showing) return;
        showing = false;
        UIPopup.Hide(panel.gameObject);
    }

    private void Refresh()
    {
        bool canClaim = LoginChest.CanClaim;
        int streak = LoginChest.DisplayStreak;
        int next = canClaim ? streak : streak + 1;

        dayLabel.text = LocalizationManager.Get("login_chest_day", Mathf.Max(1, LoginChest.CycleDay(Mathf.Max(1, streak))), LoginChest.CycleLength);
        rewardLabel.text = canClaim
            ? LocalizationManager.Get("login_chest_reward", LoginChest.RewardFor(streak))
            : LocalizationManager.Get("login_chest_opened");
        nextLabel.text = LocalizationManager.Get("login_chest_next", LoginChest.RewardFor(next + (canClaim ? 1 : 0)));

        claimButton.interactable = canClaim;
        claimLabel.text = LocalizationManager.Get(canClaim ? "login_chest_open" : "login_chest_back");
    }

    private void OnClaim()
    {
        if (LoginChest.Claim() > 0)
        {
            HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
            if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
