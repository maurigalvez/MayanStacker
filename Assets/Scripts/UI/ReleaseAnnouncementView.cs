using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "what's new" card shown once to returning players when a release lands (see
/// <see cref="ReleaseAnnouncement"/>): the new features in one glance, and the welcome-back chest
/// bonus with a button that goes straight to the chest.
///
/// Appears by itself on the main menu a moment after it settles - one prompt per visit, never over
/// the first-launch flow. Presentation comes only from the authored prefab at Resources/UI/ReleaseAnnouncement;
/// without it a warning names the menu that builds it and nothing opens (no code-built fallback), and the
/// release stays unseen so the card still arrives once the prefab exists.
/// Menu: TamalStacker ▸ UI ▸ Create Release Announcement Prefab.
/// </summary>
public class ReleaseAnnouncementView : MonoBehaviour
{
    public const int SortingOrder = 4400;
    public const string PrefabResourcePath = "UI/ReleaseAnnouncement";

    /// <summary>Highlight rows on the card. Copy lives at announce_row{n}_title / announce_row{n}_body.</summary>
    public const int RowCount = 5;

    private const float SettleSeconds = 1.0f;

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI[] rowTitles;
    [SerializeField] private TextMeshProUGUI[] rowBodies;
    [Tooltip("Thank-you block with the chest bonus. Hidden when the build has no bonus to offer.")]
    [SerializeField] private GameObject bonusBlock;
    [SerializeField] private TextMeshProUGUI bonusTitle;
    [SerializeField] private TextMeshProUGUI bonusBody;
    [SerializeField] private Button primaryButton;
    [SerializeField] private TextMeshProUGUI primaryLabel;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private TextMeshProUGUI secondaryLabel;

    private static ReleaseAnnouncementView instance;

    private bool showing;
    private bool closing;

    public static bool IsShowing => instance != null && instance.showing;

    /// <summary>
    /// Run on the main menu (<c>StartCoroutine</c>): shows the card once the menu has settled, if it is due
    /// and nothing else has taken the screen in the meantime.
    /// </summary>
    public static IEnumerator ShowWhenSettled()
    {
        ReleaseAnnouncement.Evaluate();
        if (!ReleaseAnnouncement.ShouldShow) yield break;

        float waited = 0f;
        while (waited < SettleSeconds) { waited += Time.unscaledDeltaTime; yield return null; }

        if (!ReleaseAnnouncement.ShouldShow || LoginChestView.IsShowing) yield break;
        Show();
    }

    public static bool Show()
    {
        if (instance == null) instance = Create();
        if (instance == null) return false;
        instance.Open();
        return true;
    }

    private static ReleaseAnnouncementView Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[ReleaseAnnouncement] Resources/{PrefabResourcePath} is missing - run " +
                             "TamalStacker > UI > Create Release Announcement Prefab. The card stays closed.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "ReleaseAnnouncementView";
        var view = go.GetComponent<ReleaseAnnouncementView>();
        if (view == null || !view.IsWired())
        {
            Debug.LogWarning($"[ReleaseAnnouncement] Resources/{PrefabResourcePath} is missing its view or references - " +
                             "rebuild it with TamalStacker > UI > Create Release Announcement Prefab. The card stays closed.");
            Destroy(go);
            return null;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        view.primaryButton.onClick.AddListener(view.OnPrimary);
        view.secondaryButton.onClick.AddListener(() => view.Close(null));
        view.panel.gameObject.SetActive(false);
        DontDestroyOnLoad(go);
        return view;
    }

    private bool IsWired()
    {
        if (panel == null || titleLabel == null || primaryButton == null || primaryLabel == null ||
            secondaryButton == null || secondaryLabel == null ||
            rowTitles == null || rowBodies == null || rowTitles.Length != RowCount || rowBodies.Length != RowCount)
            return false;
        for (int i = 0; i < RowCount; i++) if (rowTitles[i] == null || rowBodies[i] == null) return false;
        return true;
    }

    private void Open()
    {
        showing = true;
        closing = false;
        Refresh();
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged += Refresh;

        ReleaseAnnouncement.MarkShown();
        GameAnalytics.Track("release_announce_shown", new Dictionary<string, object>
        {
            { "version", ReleaseAnnouncement.Version },
            { "bonus", ReleaseAnnouncement.BonusOffered },
        });
        UIPopup.Show(panel.gameObject);
    }

    private void Refresh()
    {
        titleLabel.text = LocalizationManager.Get("announce_title", ReleaseAnnouncement.Version);
        for (int i = 0; i < RowCount; i++)
        {
            rowTitles[i].text = LocalizationManager.Get($"announce_row{i + 1}_title");
            rowBodies[i].text = LocalizationManager.Get($"announce_row{i + 1}_body");
        }

        bool bonus = ReleaseAnnouncement.BonusOffered;
        if (bonusBlock != null) bonusBlock.SetActive(bonus);
        if (bonus)
        {
            var s = LoginChestSettings.Active;
            if (bonusTitle != null) bonusTitle.text = LocalizationManager.Get("announce_bonus_title");
            if (bonusBody != null)
                bonusBody.text = LocalizationManager.Get("announce_bonus_body", s.returningBonusMultiplier, s.returningBonusDays);
        }

        // With a bonus the card sends the player to the chest; without one it is just read and closed.
        primaryLabel.text = LocalizationManager.Get(bonus ? "announce_open_chest" : "announce_continue");
        secondaryButton.gameObject.SetActive(bonus);
        secondaryLabel.text = LocalizationManager.Get("announce_later");
    }

    private void OnPrimary()
    {
        if (DependencyRegistry.TryFind<MainMenuSoundManager>(out var sound)) sound.PlayButtonClick();
        bool toChest = ReleaseAnnouncement.BonusOffered;
        if (toChest) GameAnalytics.Track("release_announce_open_chest");
        // Open the chest only after the card has finished leaving, so the two never overlap.
        Close(toChest ? (System.Action)(() => LoginChestView.Show()) : null);
    }

    private void Close(System.Action afterHidden)
    {
        if (!showing || closing) return;
        closing = true;
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= Refresh;
        UIPopup.Hide(panel.gameObject, () =>
        {
            showing = false;
            afterHidden?.Invoke();
        });
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= Refresh;
        if (instance == this) instance = null;
    }
}
