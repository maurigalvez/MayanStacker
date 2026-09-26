using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Routes the Android back button. The Input System reports it as the keyboard's Escape key,
/// so Escape in the Editor exercises the same path.
///
/// Screens register a handler while they're up; the most recently registered one gets the
/// press first and returns true if it used it. UIManager handles pause/resume in a run,
/// MainMenuManager closes the open panel or asks to quit, and QuitConfirmView closes itself.
///
/// Full-screen flows that must be answered (the language picker, the power loadout) swallow
/// the press so it can't act on the screen hidden underneath them.
/// </summary>
public class BackButton : MonoBehaviour
{
    private static readonly List<Func<bool>> handlers = new List<Func<bool>>();
    private static BackButton instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        var go = new GameObject("BackButton");
        instance = go.AddComponent<BackButton>();
        DontDestroyOnLoad(go);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        handlers.Clear();
        instance = null;
    }

    /// <summary>Adds a handler on top. Registering one that's already there moves it to the top.</summary>
    public static void Register(Func<bool> handler)
    {
        if (handler == null) return;
        handlers.Remove(handler);
        handlers.Add(handler);
    }

    public static void Unregister(Func<bool> handler)
    {
        handlers.Remove(handler);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        if (LanguageSelectScreen.IsShowing || PowerLoadoutScreen.IsShowing) return;

        for (int i = handlers.Count - 1; i >= 0; i--)
        {
            // A handler can unregister itself (or others) while handling the press
            if (i >= handlers.Count) continue;
            if (handlers[i]()) return;
        }
    }
}
