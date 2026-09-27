using System.Collections.Generic;

/// <summary>
/// A run system whose per-landing state the Tzolk'in Rewind can turn back: score and combo,
/// tremor, objective progress, the Serpent's Edge receipt, the Infinite peak.
/// </summary>
public interface IRunRewindable
{
    /// <summary>A copy of the state as it stands now. Must not share mutable data with the live state.</summary>
    object CaptureRewindState();

    /// <summary>
    /// Puts back a state from <see cref="CaptureRewindState"/>. Called after the rewound stones
    /// have already left the stack, so the stack count is the restored one.
    /// </summary>
    void RestoreRewindState(object state);
}

/// <summary>
/// Snapshots of the run taken each time a stone leaves the spawner, keyed by the stack height
/// at that moment. At a drop everything the previous landing set in motion has settled
/// (score, combo, tremor, a Kukulkan shift), so "the state at height N" is exactly the run
/// before stone N+1 was placed. Rewinding k stones from height H restores snapshot H - k.
///
/// Static and scene-agnostic: participants register themselves, and the store is cleared at
/// every run start by <see cref="PowerSystem"/>.
/// </summary>
public static class RunRewind
{
    private static readonly List<IRunRewindable> participants = new List<IRunRewindable>();
    private static readonly Dictionary<int, Dictionary<IRunRewindable, object>> snapshots =
        new Dictionary<int, Dictionary<IRunRewindable, object>>();
    private static readonly List<int> staleKeys = new List<int>();

    public static void Register(IRunRewindable participant)
    {
        if (participant != null && !participants.Contains(participant)) participants.Add(participant);
    }

    public static void Unregister(IRunRewindable participant)
    {
        participants.Remove(participant);
        foreach (var snapshot in snapshots.Values) snapshot.Remove(participant);
    }

    /// <summary>
    /// Records the run at <paramref name="stackHeight"/>. Anything recorded above it belongs to
    /// a timeline that was rewound away, and anything more than <paramref name="depth"/> below
    /// can never be reached, so both are dropped.
    /// </summary>
    public static void Capture(int stackHeight, int depth)
    {
        staleKeys.Clear();
        foreach (int key in snapshots.Keys)
        {
            if (key > stackHeight || key < stackHeight - depth) staleKeys.Add(key);
        }
        foreach (int key in staleKeys) snapshots.Remove(key);

        var snapshot = new Dictionary<IRunRewindable, object>(participants.Count);
        foreach (IRunRewindable participant in participants)
        {
            snapshot[participant] = participant.CaptureRewindState();
        }
        snapshots[stackHeight] = snapshot;
    }

    public static bool Has(int stackHeight) => snapshots.ContainsKey(stackHeight);

    /// <summary>Restores every participant to the snapshot at <paramref name="stackHeight"/>.</summary>
    public static bool Restore(int stackHeight)
    {
        if (!snapshots.TryGetValue(stackHeight, out var snapshot)) return false;

        foreach (var pair in snapshot)
        {
            if (pair.Key != null) pair.Key.RestoreRewindState(pair.Value);
        }
        return true;
    }

    public static void Clear() => snapshots.Clear();
}
