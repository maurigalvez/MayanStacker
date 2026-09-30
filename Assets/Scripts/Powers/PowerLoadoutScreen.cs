using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The pre-play screen, shown in the main menu between tapping a Play entry point
/// (Infinite, a temple, "next temple", or Next Level on the result card, which comes back to
/// the menu for it) and the run loading: see the temple's rule, pick a power, then PLAY.
///
/// Every power has a card, locked ones included (dimmed, "Beat level N"), so the player
/// always sees what's coming. A temple with a rule shows its icon(s) above the cards.
///
/// The pick is saved through <see cref="PowerUnlocks.Equip"/>, so Retry from the result card
/// keeps it without asking again.
///
/// <see cref="ShowOrContinue"/> skips the screen only when powers are off for the mode
/// (the Daily) or the tutorial is still running.
///
/// This class is the behaviour; the look is <see cref="PowerLoadoutView"/>, taken from the
/// prefab at Resources/UI/PowerLoadoutScreen when there is one and built in code otherwise,
/// like <see cref="LanguageSelectScreen"/>. No scene or menu prefab is touched.
/// </summary>
public class PowerLoadoutScreen : MonoBehaviour
{
    private static PowerLoadoutScreen instance;

    private PowerSettings settings;
    private PowerLoadoutView view;
    private GameMode mode;
    private Action onPlay;
    private Action onBack;
    private PowerId selected;
    private bool closing;

    public static bool IsShowing => instance != null;

    /// <summary>
    /// Shows the pre-play screen for <paramref name="mode"/> (and <paramref name="level"/>, whose
    /// rules it names; null for Infinite), otherwise runs <paramref name="play"/> straight away.
    /// <paramref name="back"/> runs (after the screen closes) if the player backs out instead.
    /// </summary>
    public static void ShowOrContinue(GameMode mode, LevelData level, Action play, Action back = null)
    {
        if (!ShouldShow(mode))
        {
            play?.Invoke();
            return;
        }

        if (instance != null) return; // a second tap on a Play button while it's already up

        var go = new GameObject("PowerLoadoutScreen");
        instance = go.AddComponent<PowerLoadoutScreen>();
        instance.mode = mode;
        instance.onPlay = play;
        instance.onBack = back;
        instance.Build(mode == GameMode.StackerLevels ? level : null);
    }

    private static bool ShouldShow(GameMode mode)
    {
        PowerSettings settings = PowerSettings.Current;
        if (settings == null || !settings.AppliesTo(mode)) return false;
        return !(settings.suppressDuringTutorial && FtueState.NeedsTutorial);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(LevelData level)
    {
        settings = PowerSettings.Current;
        selected = PowerUnlocks.Equipped;

        var defs = new List<PowerDefinition>();
        var locked = new HashSet<PowerId>();
        int owned = 0;
        foreach (PowerId id in (PowerId[])Enum.GetValues(typeof(PowerId)))
        {
            defs.Add(settings.Get(id));
            if (PowerUnlocks.IsUnlocked(id)) owned++;
            else locked.Add(id);
        }

        var rules = new List<LevelRule>(2);
        TempleRuleIcons.RulesOf(level, rules);

        view = BuildFromPrefab();
        if (view == null)
        {
            var host = new GameObject("PowerLoadout");
            host.transform.SetParent(transform, false);
            view = PowerLoadoutView.BuildDefault(host);
        }

        view.Populate(defs, locked, rules, OnCardClicked, OnPlayClicked, OnBackClicked);
        Select(selected);
        view.ScrollTo(selected);
        UIPopup.PopIn(view.PopupTarget);

        GameAnalytics.Track("power_loadout_shown", new Dictionary<string, object>
        {
            { "mode", mode.ToString() },
            { "owned", owned },
            { "level", level != null ? level.levelNumber : 0 },
            { "rules", rules.Count }
        });
    }

    /// <summary>
    /// Instantiates the authored prefab, if there is one. Returns null when no usable prefab
    /// exists, so the code-built layout is used instead.
    /// </summary>
    private PowerLoadoutView BuildFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(PowerLoadoutView.PrefabResourcePath);
        if (prefab == null) return null;

        // The prefab carries its own Canvas; this host only owns the lifetime.
        GameObject instantiated = Instantiate(prefab, transform, false);
        var authored = instantiated.GetComponentInChildren<PowerLoadoutView>(true);
        if (authored != null && authored.IsUsable) return authored;

        Debug.LogWarning($"[PowerLoadout] Resources/{PowerLoadoutView.PrefabResourcePath} has no usable " +
                         "PowerLoadoutView (needs a card template with a Button, and a PLAY button) - " +
                         "using the code-built layout instead.");
        // Hide first: Destroy is deferred, and a half-built screen shouldn't flash for a frame.
        instantiated.SetActive(false);
        Destroy(instantiated);
        return null;
    }

    // ---- Interaction ----

    private void OnCardClicked(PowerId id)
    {
        if (closing || id == selected) return;
        HapticFeedback.Trigger(HapticFeedback.HapticType.Light);
        Select(id);
    }

    private void Select(PowerId id)
    {
        selected = id;
        view.SetSelected(id, settings.Describe(id));
    }

    private void OnPlayClicked()
    {
        if (closing) return;
        closing = true;

        PowerId previous = PowerUnlocks.Equipped;
        PowerUnlocks.Equip(selected);

        GameAnalytics.Track("power_equipped", new Dictionary<string, object>
        {
            { "power", selected.ToString() },
            { "changed", previous != selected },
            { "mode", mode.ToString() }
        });

        // Straight into the load: the scene change tears this screen down with the menu.
        Action play = onPlay;
        Destroy(gameObject);
        play?.Invoke();
    }

    private void OnBackClicked()
    {
        if (closing) return;
        closing = true;

        Action back = onBack;
        UIPopup.Hide(view.PopupTarget, () =>
        {
            Destroy(gameObject);
            back?.Invoke();
        });
    }
}
