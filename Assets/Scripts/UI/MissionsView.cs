using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Today's three missions: goal, progress and jade reward. Read-only; progress is counted by
/// <see cref="DailyMissions"/> when a run ends.
///
/// Opened from the main-menu chip (<see cref="MissionsMenuChip"/>). Presentation comes only from
/// the authored prefab at Resources/UI/Missions; without it a warning names the menu that builds
/// it and nothing opens (no code-built fallback). Menu: TamalStacker ▸ UI ▸ Create Missions Prefab.
/// Rows are cloned from the prefab's own row template.
/// </summary>
public class MissionsView : MonoBehaviour
{
    public const int SortingOrder = 4300;
    public const string PrefabResourcePath = "UI/Missions";

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI countLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI closeLabel;
    [Tooltip("Parent of the cloned rows.")]
    [SerializeField] private RectTransform content;
    [SerializeField] private MissionRow rowTemplate;

    private static MissionsView instance;

    private readonly List<MissionRow> rows = new List<MissionRow>();
    private bool showing;

    /// <summary>True when missions are switched on and the asset exists.</summary>
    public static bool Available => MissionSettings.Active != null;

    public static bool IsShowing => instance != null && instance.showing;

    /// <summary>Missions finished today, and how many there are; for the menu chip.</summary>
    public static void Count(out int done, out int total)
    {
        done = 0;
        var today = DailyMissions.GetToday();
        total = today.Count;
        foreach (var entry in today)
        {
            if (entry.done) done++;
        }
    }

    public static bool Show()
    {
        if (!Available) return false;
        if (instance == null) instance = Create();
        if (instance == null) return false;
        instance.Open();
        return true;
    }

    private static MissionsView Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Missions] Resources/{PrefabResourcePath} is missing - run " +
                             "TamalStacker > UI > Create Missions Prefab. The missions panel stays closed.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "MissionsView";
        var view = go.GetComponent<MissionsView>();
        if (view == null || view.panel == null || view.titleLabel == null || view.countLabel == null ||
            view.closeButton == null || view.content == null || view.rowTemplate == null ||
            !view.rowTemplate.IsComplete)
        {
            Debug.LogWarning($"[Missions] Resources/{PrefabResourcePath} is missing its view or references - " +
                             "rebuild it with TamalStacker > UI > Create Missions Prefab. The missions panel stays closed.");
            Destroy(go);
            return null;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        view.rowTemplate.gameObject.SetActive(false);
        view.closeButton.onClick.AddListener(view.Close);
        view.panel.gameObject.SetActive(false);
        DontDestroyOnLoad(go);
        return view;
    }

    private void Open()
    {
        titleLabel.text = LocalizationManager.Get("missions_title");
        if (closeLabel != null) closeLabel.text = LocalizationManager.Get("missions_close");
        Rebuild();

        DailyMissions.OnMissionCompleted += OnCompleted;
        showing = true;
        UIPopup.Show(panel.gameObject);
    }

    private void Close()
    {
        if (!showing) return;
        showing = false;
        DailyMissions.OnMissionCompleted -= OnCompleted;
        UIPopup.Hide(panel.gameObject);
    }

    private void OnCompleted(MissionSettings.Template _) => Rebuild();

    private void OnRerollTapped(int slot)
    {
        AdManager.RequestReward("mission_reroll", earned =>
        {
            // An ad closed early owes nothing; the rows rebuild so the button re-arms.
            if (earned && DailyMissions.Reroll(slot)) HapticFeedback.Trigger(HapticFeedback.HapticType.Medium);
            if (this != null && showing) Rebuild();
        });
    }

    private void Rebuild()
    {
        foreach (MissionRow old in rows)
        {
            if (old != null) Destroy(old.gameObject);
        }
        rows.Clear();

        var today = DailyMissions.GetToday();
        int done = 0;
        bool swapOffered = DailyMissions.CanReroll && AdManager.CanOfferReward;
        for (int i = 0; i < today.Count; i++)
        {
            var entry = today[i];
            var row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.Bind(entry);
            int slot = i;
            row.BindReroll(swapOffered && !entry.done, () => OnRerollTapped(slot));
            rows.Add(row);
            if (entry.done) done++;
        }
        countLabel.text = LocalizationManager.Get("missions_count", done, today.Count);
    }

    private void OnDestroy()
    {
        DailyMissions.OnMissionCompleted -= OnCompleted;
        if (instance == this) instance = null;
    }
}
