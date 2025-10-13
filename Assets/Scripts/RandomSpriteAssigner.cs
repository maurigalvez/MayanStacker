using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Assigns a random sprite from a Sprite Atlas to a specified SpriteRenderer.
/// </summary>
public class RandomSpriteAssigner : MonoBehaviour
{
    [Header("Sprite Settings")]
    [Tooltip("The SpriteRenderer to assign the random sprite to")]
    [SerializeField] private SpriteRenderer targetSpriteRenderer;

    [Tooltip("The Sprite Atlas to pick sprites from")]
    [SerializeField] private SpriteAtlas spriteAtlas;

    [Header("Options")]
    [Tooltip("Assign a random sprite when the game starts")]
    [SerializeField] private bool assignOnStart = true;

    [Tooltip("Assign a random sprite when this GameObject is enabled")]
    [SerializeField] private bool assignOnEnable = false;

    private Sprite[] cachedSprites;

    private void Start()
    {
        if (assignOnStart)
        {
            AssignRandomSprite();
        }
    }

    private void OnEnable()
    {
        if (assignOnEnable && !assignOnStart)
        {
            AssignRandomSprite();
        }
    }

    /// <summary>
    /// Assigns a random sprite from the sprite atlas to the target SpriteRenderer.
    /// </summary>
    public void AssignRandomSprite()
    {
        if (targetSpriteRenderer == null)
        {
            Debug.LogWarning("RandomSpriteAssigner: Target SpriteRenderer is not assigned!", this);
            return;
        }

        if (spriteAtlas == null)
        {
            Debug.LogWarning("RandomSpriteAssigner: Sprite Atlas is not assigned!", this);
            return;
        }

        // Cache sprites if not already cached
        if (cachedSprites == null || cachedSprites.Length == 0)
        {
            CacheSprites();
        }

        if (cachedSprites.Length == 0)
        {
            Debug.LogWarning("RandomSpriteAssigner: Sprite Atlas contains no sprites!", this);
            return;
        }

        // Select and assign a random sprite
        int randomIndex = Random.Range(0, cachedSprites.Length);
        targetSpriteRenderer.sprite = cachedSprites[randomIndex];
    }

    /// <summary>
    /// Gets and caches all sprites from the sprite atlas.
    /// </summary>
    private void CacheSprites()
    {
        if (spriteAtlas == null)
        {
            cachedSprites = new Sprite[0];
            return;
        }

        int spriteCount = spriteAtlas.spriteCount;
        cachedSprites = new Sprite[spriteCount];
        spriteAtlas.GetSprites(cachedSprites);
    }

    /// <summary>
    /// Forces a refresh of the cached sprites from the atlas.
    /// Call this if the sprite atlas has been modified at runtime.
    /// </summary>
    public void RefreshSpriteCache()
    {
        CacheSprites();
    }

    /// <summary>
    /// Sets the target SpriteRenderer programmatically.
    /// </summary>
    public void SetTargetSpriteRenderer(SpriteRenderer renderer)
    {
        targetSpriteRenderer = renderer;
    }

    /// <summary>
    /// Sets the sprite atlas programmatically.
    /// </summary>
    public void SetSpriteAtlas(SpriteAtlas atlas)
    {
        spriteAtlas = atlas;
        RefreshSpriteCache();
    }

    private void OnValidate()
    {
        // Auto-assign the SpriteRenderer on this GameObject if none is set
        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponent<SpriteRenderer>();
        }
    }
}
