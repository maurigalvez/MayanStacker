using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gives every Button and Toggle in the game a <see cref="UIPressScale"/>, with no prefab or
/// scene edits — including controls built in code or instantiated later (level buttons,
/// boon cards, language rows).
///
/// Reads uGUI's own registry of enabled Selectables rather than scanning the scene, and
/// only touches each control once. Controls carrying <see cref="NoPressScale"/> are skipped.
/// </summary>
public class UIPressScaleAutoAttach : MonoBehaviour
{
    private Selectable[] buffer = new Selectable[64];
    private readonly HashSet<int> seen = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("UIPressScaleAutoAttach");
        DontDestroyOnLoad(go);
        go.AddComponent<UIPressScaleAutoAttach>();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // Instance IDs of destroyed controls are never reused, so this only bounds memory.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => seen.Clear();

    private void Update()
    {
        int count = Selectable.allSelectableCount;
        if (count > buffer.Length)
        {
            buffer = new Selectable[Mathf.NextPowerOfTwo(count)];
        }

        int filled = Selectable.AllSelectablesNoAlloc(buffer);
        for (int i = 0; i < filled; i++)
        {
            Selectable selectable = buffer[i];
            if (!(selectable is Button) && !(selectable is Toggle)) continue;
            if (!seen.Add(selectable.GetInstanceID())) continue;

            if (selectable.TryGetComponent(out NoPressScale _)) continue;
            if (selectable.TryGetComponent(out UIPressScale _)) continue;

            selectable.gameObject.AddComponent<UIPressScale>();
        }
    }
}
