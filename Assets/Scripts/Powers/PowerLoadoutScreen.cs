using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The pick-a-power screen, shown in the main menu between tapping a Play entry point
/// (Infinite, a temple, "next temple") and the run loading: pick a power, then PLAY.
///
/// The pick is saved through <see cref="PowerUnlocks.Equip"/>, so Retry and Next Level from
/// the result card keep it without asking again — changing it means going back to the menu.
///
/// <see cref="ShowOrContinue"/> skips the screen entirely when there is nothing to choose:
/// fewer than two powers owned, powers off for the mode, or the tutorial still running.
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
    /// Shows the picker when the player has a choice to make for <paramref name="mode"/>,
    /// otherwise runs <paramref name="play"/> straight away. <paramref name="back"/> runs
    /// (after the screen closes) if the player backs out instead.
    /// </summary>
    public static void ShowOrContinue(GameMode mode, Action play, Action back = null)
    {
        var unlocked = new List<PowerId>();
        if (!ShouldShow(mode, unlocked))
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
        instance.Build(unlocked);
    }

    private static bool ShouldShow(GameMode mode, List<PowerId> unlocked)
    {
        PowerSettings settings = PowerSettings.Current;
        if (settings == null || !settings.AppliesTo(mode)) return false;
        if (settings.suppressDuringTutorial && FtueState.NeedsTutorial) return false;

        PowerUnlocks.GetUnlocked(unlocked);
        return unlocked.Count >= 2;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(List<PowerId> unlocked)
    {
        settings = PowerSettings.Current;
        selected = PowerUnlocks.Equipped;

        view = BuildFromPrefab();
        if (view == null)
        {
            var host = new GameObject("PowerLoadout");
            host.transform.SetParent(transform, false);
            view = PowerLoadoutView.BuildDefault(host);
        }

        var defs = new List<PowerDefinition>();
        foreach (PowerId id in unlocked) defs.Add(settings.Get(id));

        view.Populate(defs, OnCardClicked, OnPlayClicked, OnBackClicked);
        Select(selected);
        UIPopup.PopIn(view.PopupTarget);

        GameAnalytics.Track("power_loadout_shown", new Dictionary<string, object>
        {
            { "mode", mode.ToString() },
            { "owned", unlocked.Count }
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
        PowerDefinition def = settings.Get(id);
        // {0} is the Quetzal Feather's drop count; the other descriptions have no slot.
        view.SetSelected(id, LocalizationManager.Get(def.descriptionKey, settings.quetzalDrops));
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
