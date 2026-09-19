using UnityEngine;

/// <summary>
/// Opt-out marker: a Button or Toggle with this component is skipped by
/// <see cref="UIPressScaleAutoAttach"/> and won't squash when pressed.
/// </summary>
[DisallowMultipleComponent]
public class NoPressScale : MonoBehaviour
{
}
