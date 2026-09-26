#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the UNITY_PURCHASING_IAP scripting define in step with whether the In-App Purchasing
/// package (com.unity.purchasing) is installed, so <see cref="PurchaseManager"/> compiles
/// before the package is added and switches itself on the moment it is.
///
/// Same mechanism and the same two places as <see cref="PlayAppUpdateDefine"/> — Player
/// Settings plus every Android Build Profile, because Android_Release overrides Player
/// Settings wholesale and would otherwise compile purchases out of the build that ships.
/// </summary>
[InitializeOnLoad]
public static class UnityPurchasingDefine
{
    private const string Symbol = "UNITY_PURCHASING_IAP";
    private const string PackageTypeName = "UnityEngine.Purchasing.UnityIAPServices";

    static UnityPurchasingDefine()
    {
        EditorApplication.delayCall += Sync;
    }

    [MenuItem("TamalStacker/Monetization/Refresh In-App Purchasing Support")]
    public static void Sync()
    {
        bool packagePresent = PlayAppUpdateDefine.IsTypePresent(PackageTypeName);

        var changed = new List<string>();
        PlayAppUpdateDefine.SyncPlayerSettings(Symbol, packagePresent, changed);
        PlayAppUpdateDefine.SyncBuildProfiles(Symbol, packagePresent, changed);

        if (changed.Count == 0) return;

        AssetDatabase.SaveAssets();

        Debug.Log(packagePresent
            ? $"[UnityPurchasingDefine] In-App Purchasing package found — added {Symbol} to: {string.Join(", ", changed)}. Remove Ads is now live."
            : $"[UnityPurchasingDefine] In-App Purchasing package missing — removed {Symbol} from: {string.Join(", ", changed)}. Purchases are disabled.");
    }
}
#endif
