using UnityEngine;

/// <summary>
/// One glow / ghost sprite for Swing Moods. The prefab (made by TamalStacker ▸ Swing Moods ▸
/// Create Fx Prefab) holds a SpriteRenderer; <see cref="SwingMoodDirector"/> owns two of them,
/// one for the telegraph glow around the hanging stone and one for the Echo ghost.
/// </summary>
public class SwingMoodFx : MonoBehaviour
{
    /// <summary>The Resources path the director loads the prefab from.</summary>
    public const string ResourcePath = "Fx/SwingMoodFx";

    [SerializeField] private SpriteRenderer spriteRenderer;

    public SpriteRenderer Renderer => spriteRenderer != null ? spriteRenderer : (spriteRenderer = GetComponentInChildren<SpriteRenderer>());
}
