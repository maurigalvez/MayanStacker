using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Teaches each special block the first time the player ever meets it.
///
/// Block variety was shipping silent: the spawner would hand the player a narrow jade
/// block worth triple, or a cracked one that ends the run on a sloppy landing, and nothing
/// anywhere said so. The FTUE can't cover them either — the variety table deliberately
/// suppresses specials for the whole tutorial, so the first one always arrives long after
/// the tutorial has closed.
///
/// So the lesson goes where the block is. When a block the player has never seen swings
/// into view, a banner names it and says in one line what it does, while the block is
/// still hanging there to be looked at. It is shown once per block, ever
/// (see <see cref="BlockCodex"/>), and it never blocks input — the player can ignore it
/// and drop immediately.
///
/// Self-bootstraps into any gameplay scene, so it needs no scene wiring.
/// </summary>
public class BlockIntroPresenter : MonoBehaviour
{
    /// <summary>
    /// Below the middle of the screen: the block being introduced is swinging across the
    /// top, and a banner over it would hide the very thing it is describing.
    /// </summary>
    private const float BannerYOffset = -180f;

    private const float BannerHoldSeconds = 2.6f;

    private static BlockIntroPresenter instance;

    private ObjectSpawner spawner;

    // Blocks introduced during this session. The codex is the persistent record; this only
    // guards against a second banner inside one run if PlayerPrefs somehow fails to save.
    private readonly HashSet<BlockVariantId> introducedThisSession = new HashSet<BlockVariantId>();

    #region Bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode) => EnsureInstance();

    private static void EnsureInstance()
    {
        if (instance != null) return;

        // Gameplay scenes only — there are no blocks to introduce in the menu.
        if (Object.FindFirstObjectByType<ObjectSpawner>() == null) return;

        var go = new GameObject("BlockIntroPresenter");
        instance = go.AddComponent<BlockIntroPresenter>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    #endregion

    private void Start()
    {
        spawner = DependencyRegistry.Find<ObjectSpawner>();
        if (spawner == null)
        {
            Destroy(gameObject);
            return;
        }

        spawner.OnObjectSpawned += OnObjectSpawned;
    }

    private void OnObjectSpawned(GameObject spawnedObject)
    {
        if (spawnedObject == null) return;

        var stackable = spawnedObject.GetComponent<StackableObject>();
        if (stackable == null) return;

        Introduce(stackable.Variant);
    }

    /// <summary>
    /// Shows the introduction for <paramref name="variant"/> if it is owed one. Public so a
    /// designer-facing tool or a future codex screen can replay a lesson.
    /// </summary>
    public void Introduce(BlockVariant variant)
    {
        if (variant == null || !variant.IsSpecial) return;
        if (introducedThisSession.Contains(variant.id)) return;
        if (!BlockCodex.MarkSeen(variant.id)) return;

        introducedThisSession.Add(variant.id);

        string name = LocalizationManager.Get(variant.nameKey);
        if (string.IsNullOrEmpty(name)) return;

        string description = string.IsNullOrEmpty(variant.descriptionKey)
            ? string.Empty
            : LocalizationManager.Get(variant.descriptionKey);

        // Banner in the block's own colour, so the words and the thing on screen are
        // obviously about each other.
        Color accent = variant.overrideTint ? variant.tint : RunOverlayUI.Gold;

        RunBanner.Show(name, description, accent, BannerHoldSeconds, BannerYOffset);

        GameAnalytics.Track("block_variant_introduced", new Dictionary<string, object>
        {
            { "variant", variant.id.ToString() },
            { "lifetime_runs", FtueState.LifetimeRuns }
        });
    }

    private void OnDestroy()
    {
        if (spawner != null) spawner.OnObjectSpawned -= OnObjectSpawned;
        if (instance == this) instance = null;
    }
}
