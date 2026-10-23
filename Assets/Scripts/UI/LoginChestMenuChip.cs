using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The main-menu "CHEST" chip: says whether today's login chest is waiting and opens the
/// <see cref="LoginChestView"/>. Faded out (not deactivated, so it comes back) when the chest or
/// jade is off.
///
/// Built into the Main Menu Canvas prefab by TamalStacker ▸ Economy ▸ Add Login Chest Menu Chip.
/// </summary>
[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class LoginChestMenuChip : MonoBehaviour
{
    [Tooltip("\"Ready!\" while today's chest is waiting, otherwise the cycle day.")]
    [SerializeField] private TextMeshProUGUI statusLabel;

    private Button button;
    private CanvasGroup group;

    private void Awake()
    {
        button = GetComponent<Button>();
        group = GetComponent<CanvasGroup>();
        button.onClick.AddListener(OnPressed);
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        // Cheap, and keeps the chip honest if the menu sits open across UTC midnight or the panel claims.
        if (statusLabel != null && group.alpha > 0f) UpdateStatus();
    }

    private void Refresh()
    {
        bool show = LoginChestView.Available;
        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
        if (show) UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (statusLabel == null) return;
        string text = LoginChest.CanClaim
            ? LocalizationManager.Get("login_chest_ready")
            : LocalizationManager.Get("login_chest_chip_day", Mathf.Max(1, LoginChest.CycleDay(LoginChest.DisplayStreak)), LoginChest.CycleLength);
        if (statusLabel.text != text) statusLabel.text = text;
    }

    private void OnPressed()
    {
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        LoginChestView.Show();
    }
}
