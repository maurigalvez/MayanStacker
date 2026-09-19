using UnityEngine;

/// <summary>
/// Persistent, invisible host for <see cref="UIPopup"/> coroutines.
///
/// Popup tweens don't run on the popup itself: a close animation ends by deactivating its
/// target, and a popup whose owner deactivates it mid-tween would otherwise kill the
/// coroutine and silently drop the action waiting on the close (e.g. resuming the game).
/// Survives scene loads so a close that triggers a scene change still finishes cleanly.
/// </summary>
public class UIMotionRunner : MonoBehaviour
{
    private static UIMotionRunner instance;
    private static bool quitting;

    /// <summary>Null outside Play mode and during application quit.</summary>
    public static UIMotionRunner Instance
    {
        get
        {
            if (instance == null && !quitting && Application.isPlaying)
            {
                var go = new GameObject("UIMotionRunner");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<UIMotionRunner>();
            }
            return instance;
        }
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
