using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Path of Kukulkan: current tier, XP bar and the full tier list with what each pays. Read-only;
/// XP and payouts happen when a run ends (<see cref="PathOfKukulkan"/>).
///
/// Opened from the main-menu chip (<see cref="PathMenuChip"/>). Presentation comes only from the
/// authored prefab at Resources/UI/Path; without it a warning names the menu that builds it and
/// nothing opens (no code-built fallback). Menu: TamalStacker ▸ UI ▸ Create Path Prefab.
/// Rows are cloned from the prefab's own row template.
/// </summary>
public class PathView : MonoBehaviour
{
    public const int SortingOrder = 4300;
    public const string PrefabResourcePath = "UI/Path";

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI tierLabel;
    [Tooltip("Stretched child of the bar track; its right anchor is moved to show XP progress.")]
    [SerializeField] private RectTransform barFill;
    [SerializeField] private TextMeshProUGUI barLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI closeLabel;
    [SerializeField] private ScrollRect scroll;
    [Tooltip("Parent of the cloned rows.")]
    [SerializeField] private RectTransform content;
    [SerializeField] private PathRow rowTemplate;

    private static PathView instance;

    private readonly List<PathRow> rows = new List<PathRow>();
    private bool showing;

    /// <summary>True when the Path is switched on and the asset exists.</summary>
    public static bool Available => PathSettings.Active != null;

    public static bool IsShowing => instance != null && instance.showing;

    public static bool Show()
    {
        if (!Available) return false;
        if (instance == null) instance = Create();
        if (instance == null) return false;
        instance.Open();
        return true;
    }

    private static PathView Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Path] Resources/{PrefabResourcePath} is missing - run " +
                             "TamalStacker > UI > Create Path Prefab. The Path panel stays closed.");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "PathView";
        var view = go.GetComponent<PathView>();
        if (view == null || view.panel == null || view.titleLabel == null || view.tierLabel == null ||
            view.barFill == null || view.barLabel == null || view.closeButton == null ||
            view.content == null || view.rowTemplate == null || !view.rowTemplate.IsComplete)
        {
            Debug.LogWarning($"[Path] Resources/{PrefabResourcePath} is missing its view or references - " +
                             "rebuild it with TamalStacker > UI > Create Path Prefab. The Path panel stays closed.");
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
        titleLabel.text = LocalizationManager.Get("path_title");
        if (closeLabel != null) closeLabel.text = LocalizationManager.Get("path_close");
        Rebuild();

        PathOfKukulkan.OnTierReached += OnTier;
        showing = true;
        UIPopup.Show(panel.gameObject);
    }

    private void Close()
    {
        if (!showing) return;
        showing = false;
        PathOfKukulkan.OnTierReached -= OnTier;
        UIPopup.Hide(panel.gameObject);
    }

    private void OnTier(int tier, int jade) => Rebuild();

    private void Rebuild()
    {
        foreach (PathRow old in rows)
        {
            if (old != null) Destroy(old.gameObject);
        }
        rows.Clear();

        var settings = PathSettings.Active;
        if (settings == null) return;

        int reached = PathOfKukulkan.Tier;
        for (int i = 0; i < settings.TierCount; i++)
        {
            var row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.Bind(i + 1, settings.tiers[i], i + 1 <= reached);
            rows.Add(row);
        }

        tierLabel.text = LocalizationManager.Get("path_tier_count", reached, settings.TierCount);
        PathOfKukulkan.Progress(out int into, out int needed);
        if (needed <= 0)
        {
            barFill.anchorMax = new Vector2(1f, 1f);
            barLabel.text = LocalizationManager.Get("path_maxed");
        }
        else
        {
            barFill.anchorMax = new Vector2(Mathf.Clamp01(into / (float)needed), 1f);
            barLabel.text = LocalizationManager.Get("path_xp_progress", into, needed);
        }

        // Open on the next reward to earn: the tier after the last reached, kept near the top.
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float travel = content.rect.height - ((RectTransform)scroll.viewport).rect.height;
            if (travel > 0f && rows.Count > 1)
            {
                float t = Mathf.Clamp01(Mathf.Min(reached, rows.Count - 1) / (float)(rows.Count - 1));
                scroll.verticalNormalizedPosition = 1f - t;
            }
        }
    }

    private void OnDestroy()
    {
        PathOfKukulkan.OnTierReached -= OnTier;
        if (instance == this) instance = null;
    }
}
