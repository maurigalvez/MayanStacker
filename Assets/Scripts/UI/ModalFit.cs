using UnityEngine;

/// <summary>
/// Shrinks a fixed-size modal evenly when its parent is narrower than the modal plus a side margin,
/// so a card authored for a 9:16 phone (1080 wide) still fits a 9:21 one (about 823 wide) without
/// clipping. Never scales above 1. The modal's own layout is untouched, so every part shrinks together.
///
/// Added to the Modal of the Spins panels by TamalStacker ▸ UI ▸ Restyle Spins Panels.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ModalFit : MonoBehaviour
{
    [Tooltip("Space kept on each side between the modal and the screen edge, in canvas units.")]
    [SerializeField] private float sideMargin = 24f;

    private RectTransform rect;
    private RectTransform parent;
    private float fittedFor = -1f;

    private void OnEnable()
    {
        rect = (RectTransform)transform;
        parent = rect.parent as RectTransform;
        fittedFor = -1f;
        Fit();
    }

    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (parent == null) return;
        float available = parent.rect.width;
        if (Mathf.Approximately(available, fittedFor)) return;
        fittedFor = available;

        float width = rect.rect.width;
        float scale = width > 0f ? Mathf.Min(1f, (available - sideMargin * 2f) / width) : 1f;
        rect.localScale = new Vector3(scale, scale, 1f);
    }
}
