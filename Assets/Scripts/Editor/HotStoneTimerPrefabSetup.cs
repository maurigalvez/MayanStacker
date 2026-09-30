#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using B = DailyUIBuilder;

/// <summary>
/// Generates Assets/Resources/UI/HotStoneTimer.prefab — the countdown ring that rides above
/// the swinging stone under the Hot Stone modifier (<see cref="HotStoneTimerView"/>). Uses the
/// power medallion's ring and base art so it reads as part of the same family.
///
/// Non-interactive: its canvas has no raycaster, so a tap through it still drops the stone.
///
/// Menu: TamalStacker ▸ Daily Challenge ▸ Create Hot Stone Timer Prefab
/// </summary>
public static class HotStoneTimerPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/UI/HotStoneTimer.prefab";
    private const string RingSpritePath = "Assets/Resources/UI/Powers/PowerMedallion_Ring.png";
    private const string BaseSpritePath = "Assets/Resources/UI/Powers/PowerMedallion_Base.png";

    // Under the power meter (3010) and the HUD's taps, over the playfield.
    private const int SortingOrder = 2950;

    [MenuItem("TamalStacker/Daily Challenge/Create Hot Stone Timer Prefab")]
    public static void CreatePrefab()
    {
        if (!B.ConfirmReplace("Hot Stone Timer Prefab", PrefabPath)) return;

        GameObject root = B.CreateCanvasRoot("HotStoneTimer", SortingOrder, false, typeof(HotStoneTimerView));

        var timer = B.CreateChild("Timer", root.transform);
        B.Place(timer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));

        var disc = B.CreateChild("Disc", timer);
        B.Stretch(disc);
        var discImage = disc.gameObject.AddComponent<Image>();
        discImage.sprite = B.LoadSprite(BaseSpritePath);
        discImage.color = new Color(0.1f, 0.08f, 0.06f, 0.75f);
        discImage.preserveAspect = true;
        discImage.raycastTarget = false;

        var ringRt = B.CreateChild("Ring", timer);
        B.Stretch(ringRt);
        var ring = ringRt.gameObject.AddComponent<Image>();
        ring.sprite = B.LoadSprite(RingSpritePath);
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = false;
        ring.fillAmount = 0.7f;
        ring.preserveAspect = true;
        ring.raycastTarget = false;

        var seconds = B.Label("Seconds", timer, UIHouseStyle.Title, "2", 56f);
        B.Stretch(seconds.rectTransform);

        var so = new SerializedObject(root.GetComponent<HotStoneTimerView>());
        so.FindProperty("canvas").objectReferenceValue = root.GetComponent<Canvas>();
        so.FindProperty("timer").objectReferenceValue = timer;
        so.FindProperty("ring").objectReferenceValue = ring;
        so.FindProperty("secondsLabel").objectReferenceValue = seconds;
        so.ApplyModifiedPropertiesWithoutUndo();

        B.SavePrefab(root, PrefabPath);

        EditorUtility.DisplayDialog("Hot Stone Timer Prefab",
            "Created " + PrefabPath + ".\n\nThe ring follows the swinging stone and burns down " +
            "until it drops itself. Move/resize the Timer object to restyle it; its position is " +
            "set over the stone at runtime (World Offset Y on the view).", "OK");
    }
}
#endif
