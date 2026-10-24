#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// The five badge icons for the main-menu rail medallions, one per feature. Each is a complete,
/// self-contained badge in the style of the Remove Ads icon (T_MayanStacker_NoAds_Icon) — square,
/// transparent background — so the rail reads as one set.
///
/// Editor-only: <see cref="MainMenuRailSetup"/> reads it when it builds the rail and bakes the
/// sprites into the Main Menu Canvas prefab. It refuses to build while any slot is empty, so a
/// medallion never ships with a stand-in icon.
/// </summary>
[CreateAssetMenu(menuName = "TamalStacker/Main Menu Rail Art", fileName = "MenuRailArt")]
public class MenuRailArt : ScriptableObject
{
    [Tooltip("Login chest — Kukulkan's Chest.")]
    public Sprite chest;

    [Tooltip("Daily Missions.")]
    public Sprite missions;

    [Tooltip("Weekly Expedition.")]
    public Sprite expedition;

    [Tooltip("Path of Kukulkan.")]
    public Sprite path;

    [Tooltip("Jade Market.")]
    public Sprite market;

    [Tooltip("The jade emblem — the gem on its gold pedestal — at the left of the balance bar at the top of the menu. " +
             "While it or the counter frame is empty, Build Rail builds the rail but skips the balance bar.")]
    public Sprite jade;

    [Tooltip("The gold counter frame (parchment window, jade studs at the ends) the balance is written in. Sliced at its ends.")]
    public Sprite jadeCounterFrame;

    [Tooltip("Loose jade pieces that burst out of an opened chest and fly into the emblem. Any number; one is picked per piece.")]
    public Sprite[] jadeShards;

    [Tooltip("Short jade clink played as each piece lands in the emblem (pitch climbs per piece). Optional.")]
    public AudioClip jadeClink;

    [Tooltip("Richer chime for the last piece, the one that lands the exact balance. Optional.")]
    public AudioClip jadeChime;
}
#endif
