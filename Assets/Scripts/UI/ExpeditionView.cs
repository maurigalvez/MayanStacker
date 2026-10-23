using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Weekly Expedition: the five stages, which one is in play, its progress, the week's badge
/// count and the time left. Read-only; progress is counted by <see cref="WeeklyExpedition"/> when a
/// run ends.
///
/// Opened from the main-menu chip (<see cref="ExpeditionMenuChip"/>). Presentation comes only from
/// the authored prefab at Resources/UI/Expedition; without it a warning names the menu that builds
/// it and nothing opens (no code-built fallback). Menu: TamalStacker ▸ UI ▸ Create Expedition Prefab.
/// Rows are cloned from the prefab's own row template.
/// </summary>
public class ExpeditionView : MonoBehaviour
{
    public const int SortingOrder = 4300;
    public const string PrefabResourcePath = "UI/Expedition";

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [Tooltip("Badges earned and the finale reward.")]
    [SerializeField] private TextMeshProUGUI summaryLabel;
    [Tooltip("Time left in the week.")]
    [SerializeField] private TextMeshProUGUI resetLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI closeLabel;
    [Tooltip("Parent of the cloned rows.")]
    [SerializeField] private RectTransform content;
    [SerializeField] private ExpeditionRow rowTemplate;

    private static ExpeditionView instance;

    private readonly List<ExpeditionRow> rows = new List<ExpeditionRow>();
    private bool showing;

    /// <summary>True when the Expedition is switched on and the asset exists.</summary>
    public static bool Available => WeeklyExpedition.Available;

    public static bool IsShowing => instance != null && instance.showing;

    public static bool Show()
    {
        if (!Available) return false;
        if (instance == null) instance = Create();
        if (instance == null) return false;
        instance.Open();
        return true;
    }

    private static ExpeditionView Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Expedition] Resources/{PrefabResourcePath} is missing - run " +
                             "TamalStacker > UI > Create Expedition Prefab. The Expedition panel stays closed.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "ExpeditionView";
        var view = go.GetComponent<ExpeditionView>();
        if (view == null || view.panel == null || view.titleLabel == null || view.summaryLabel == null ||
            view.resetLabel == null || view.closeButton == null || view.content == null ||
            view.rowTemplate == null || !view.rowTemplate.IsComplete)
        {
            Debug.LogWarning($"[Expedition] Resources/{PrefabResourcePath} is missing its view or references - " +
                             "rebuild it with TamalStacker > UI > Create Expedition Prefab. The Expedition panel stays closed.");
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
        titleLabel.text = LocalizationManager.Get("expedition_title");
        if (closeLabel != null) closeLabel.text = LocalizationManager.Get("expedition_close");
        Rebuild();

        WeeklyExpedition.OnStageCompleted += OnStage;
        showing = true;
        UIPopup.Show(panel.gameObject);
    }

    private void Close()
    {
        if (!showing) return;
        showing = false;
        WeeklyExpedition.OnStageCompleted -= OnStage;
        UIPopup.Hide(panel.gameObject);
    }

    private void OnStage(int stage, int jade) => Rebuild();

    private void Rebuild()
    {
        foreach (ExpeditionRow old in rows)
        {
            if (old != null) Destroy(old.gameObject);
        }
        rows.Clear();

        var settings = ExpeditionSettings.Active;
        if (settings == null) return;

        int current = WeeklyExpedition.Stage;
        int progress = WeeklyExpedition.Progress;
        for (int i = 0; i < settings.stages.Length; i++)
        {
            var row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.Bind(i + 1, settings.stages[i], Mathf.Clamp(current - i, -1, 1), progress);
            rows.Add(row);
        }

        summaryLabel.text = WeeklyExpedition.WeekComplete
            ? LocalizationManager.Get("expedition_week_done", WeeklyExpedition.Badges)
            : LocalizationManager.Get("expedition_summary", WeeklyExpedition.Badges, settings.finaleReward);
        RefreshReset();
    }

    private void Update()
    {
        if (showing) RefreshReset();
    }

    private void RefreshReset()
    {
        long seconds = WeeklyExpedition.SecondsUntilReset;
        resetLabel.text = LocalizationManager.Get("expedition_resets", seconds / 86400, seconds % 86400 / 3600);
    }

    private void OnDestroy()
    {
        WeeklyExpedition.OnStageCompleted -= OnStage;
        if (instance == this) instance = null;
    }
}
