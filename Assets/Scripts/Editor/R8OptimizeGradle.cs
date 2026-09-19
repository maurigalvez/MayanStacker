#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Turns on R8's optimization pass for Android release builds.
///
/// Ticking Minify Release is not enough on its own. Unity's launcher Gradle template points
/// both build types at getDefaultProguardFile('proguard-android.txt'), and that file carries
/// -dontoptimize. R8 therefore only shrinks and obfuscates: no inlining, no class merging, no
/// dead-branch removal. That is what Play Console's "Improve your app's memory and performance
/// with R8 optimization" warning is about, and it stays up even with minify on.
///
/// This swaps the launcher's rules for 'proguard-android-optimize.txt', the same rules without
/// -dontoptimize. It runs on the generated Gradle project, so it needs neither a custom
/// launcher template nor a change to the Android_Release profile. Everything reached only
/// through JNI is still protected by the keep rules in Assets/Plugins/Android/proguard-user.txt
/// and the plugins' own consumer rules.
///
/// If Unity's template stops using proguard-android.txt this does nothing and logs a warning,
/// so the build never fails because of it.
/// </summary>
public class R8OptimizeGradle : IPostGenerateGradleAndroidProject
{
    const string Unoptimized = "getDefaultProguardFile('proguard-android.txt')";
    const string Optimized = "getDefaultProguardFile('proguard-android-optimize.txt')";

    // Run after anything else that edits the Gradle project.
    public int callbackOrder => 1000;

    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        string buildGradle = Path.Combine(unityLibraryPath, "..", "launcher", "build.gradle");
        if (!File.Exists(buildGradle))
        {
            Debug.LogWarning($"[R8OptimizeGradle] {buildGradle} not found; R8 optimization left off.");
            return;
        }

        string text = File.ReadAllText(buildGradle);
        if (text.Contains(Optimized))
            return;
        if (!text.Contains(Unoptimized))
        {
            Debug.LogWarning("[R8OptimizeGradle] Launcher build.gradle doesn't use proguard-android.txt; R8 optimization left unchanged.");
            return;
        }

        File.WriteAllText(buildGradle, text.Replace(Unoptimized, Optimized));
        Debug.Log("[R8OptimizeGradle] Launcher now uses proguard-android-optimize.txt (R8 optimization on).");
    }
}
#endif
