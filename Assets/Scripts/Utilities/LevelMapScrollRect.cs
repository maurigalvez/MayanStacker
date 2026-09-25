using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ScrollRect for the level map that plays nicely with pinch-to-zoom.
/// The stock ScrollRect lets every finger start its own drag, so two fingers fight over the
/// content position and undo whatever the zoom did. This version only pans with one finger,
/// stands down while ScrollView_PinchScale is pinching, and hands the mouse wheel to zoom.
/// </summary>
public class LevelMapScrollRect : ScrollRect
{
    private const int NoPointer = int.MinValue;

    private int dragPointerId = NoPointer;
    private PointerEventData lastDragEvent;
    private bool needsRebase;
    private bool dragSuppressed;

    /// <summary>
    /// While true, drags are ignored. Turning it on ends any drag in progress so the
    /// ScrollRect goes back to clamping the content inside the viewport.
    /// </summary>
    public bool DragSuppressed
    {
        get => dragSuppressed;
        set
        {
            if (value && !dragSuppressed)
                CancelDrag();
            dragSuppressed = value;
        }
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        // A second finger must not restart the drag of the first one
        if (dragSuppressed || dragPointerId != NoPointer) return;

        dragPointerId = eventData.pointerId;
        lastDragEvent = eventData;
        needsRebase = false;
        base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (dragSuppressed) return;

        // The finger left over after a pinch picks up panning from where the map is now
        if (dragPointerId == NoPointer)
        {
            dragPointerId = eventData.pointerId;
            needsRebase = true;
        }
        if (eventData.pointerId != dragPointerId) return;

        lastDragEvent = eventData;
        if (needsRebase)
        {
            needsRebase = false;
            base.OnBeginDrag(eventData);
        }
        base.OnDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != dragPointerId) return;

        dragPointerId = NoPointer;
        lastDragEvent = null;
        base.OnEndDrag(eventData);
    }

    // The wheel zooms the map (ScrollView_PinchScale.OnScroll) instead of scrolling it
    public override void OnScroll(PointerEventData data) { }

    private void CancelDrag()
    {
        if (lastDragEvent != null)
            base.OnEndDrag(lastDragEvent);

        dragPointerId = NoPointer;
        lastDragEvent = null;
        needsRebase = false;
    }

    protected override void OnDisable()
    {
        CancelDrag();
        dragSuppressed = false;
        base.OnDisable();
    }
}
