using UnityEngine;

/// <summary>
/// Run state for Sun Surge, owned by <see cref="GameFeelManager"/>. It counts the Perfect chain,
/// ignites the surge when the chain is long enough, counts the surge down one stone at a time and
/// hangs the gold trail on each stone that swings during it. The feel on ignition lives in
/// GameFeelManager (it owns shake, flash and hit-stop); this class only holds the state.
/// </summary>
public class SunSurgeDirector
{
    private int chain;            // Perfects in a row that count toward the next surge
    private int cooldownLeft;     // stones that must land before the chain counts again
    private int stonesLeft;       // surge stones still to land; > 0 means burning
    private GameObject trail;     // the gold trail on the hanging stone
    private bool trailWarned;

    /// <summary>True while the surge is burning.</summary>
    public bool Burning => stonesLeft > 0;

    /// <summary>Surges ignited this run (diagnostics and the regression bot).</summary>
    public int IgnitedCount { get; private set; }

    /// <summary>Raised the moment the surge ignites.</summary>
    public System.Action OnIgnited;

    /// <summary>Raised when the last surge stone has landed.</summary>
    public System.Action OnExtinguished;

    /// <summary>Clears all per-run state. Called when a run starts or restarts.</summary>
    public void BeginRun()
    {
        chain = 0;
        cooldownLeft = 0;
        stonesLeft = 0;
        IgnitedCount = 0;
        DropTrail(detach: false);
        SunSurge.Set(false, 1f);
    }

    /// <summary>
    /// A stone has landed. Returns true when this landing ignited the surge. A landing during the
    /// surge counts it down; a Perfect outside it grows the chain.
    /// </summary>
    public bool OnLanded(bool perfect, SunSurgeSettings s)
    {
        if (s == null) return false;

        if (Burning)
        {
            stonesLeft--;
            if (stonesLeft <= 0) Extinguish(s);
            return false;
        }

        if (cooldownLeft > 0)
        {
            cooldownLeft--;
            chain = 0;
            return false;
        }

        if (!perfect)
        {
            chain = 0;
            return false;
        }

        chain++;
        if (chain < s.perfectsToTrigger) return false;

        chain = 0;
        stonesLeft = Mathf.Max(1, s.surgeStones);
        IgnitedCount++;
        SunSurge.Set(true, s.scoreMultiplier);
        OnIgnited?.Invoke();
        return true;
    }

    /// <summary>A new stone is hanging: give it the trail while the surge burns.</summary>
    public void OnStoneSpawned(GameObject stone)
    {
        DropTrail(detach: false);
        if (!Burning || stone == null) return;

        GameObject prefab = Resources.Load<GameObject>(SunSurge.TrailResourcePath);
        if (prefab == null)
        {
            if (!trailWarned)
            {
                trailWarned = true;
                Debug.LogWarning($"[SunSurge] Missing {SunSurge.TrailResourcePath}. Run TamalStacker > Game Feel > Create Sun Surge Assets.");
            }
            return;
        }
        trail = Object.Instantiate(prefab, stone.transform);
        trail.transform.localPosition = Vector3.zero;
    }

    /// <summary>The stone was dropped: let the trail fade where it is.</summary>
    public void OnStoneDropped() => DropTrail(detach: true);

    private void Extinguish(SunSurgeSettings s)
    {
        stonesLeft = 0;
        cooldownLeft = s.cooldownStones;
        SunSurge.Set(false, 1f);
        OnExtinguished?.Invoke();
    }

    private void DropTrail(bool detach)
    {
        if (trail == null) return;
        if (detach)
        {
            var tr = trail.GetComponent<TrailRenderer>();
            trail.transform.SetParent(null, true);
            Object.Destroy(trail, tr != null ? tr.time + 0.2f : 1f);
        }
        else
        {
            Object.Destroy(trail);
        }
        trail = null;
    }
}

/// <summary>Read-only view of the surge for scoring. 1x and inactive unless a surge is burning.</summary>
public static class SunSurge
{
    public const string TrailResourcePath = "Fx/SunSurgeTrail";

    public static bool Active { get; private set; }
    public static float ScoreMultiplier { get; private set; } = 1f;

    public static void Set(bool active, float multiplier)
    {
        Active = active;
        ScoreMultiplier = active ? Mathf.Max(1f, multiplier) : 1f;
    }
}
