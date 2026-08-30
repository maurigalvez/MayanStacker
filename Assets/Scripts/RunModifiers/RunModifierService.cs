using UnityEngine;

/// <summary>
/// The single place every system asks "what are this run's rules?".
///
/// Before this existed, each modifier was checked inline by its owning system with a
/// hard-coded "are we in Daily Challenge mode AND is modifier X active?" test. That worked
/// but locked modifiers to one game mode. Systems now read the rules from here instead, and
/// whoever started the run decides which modifier is in play — the Daily, an Infinite trial,
/// or nothing at all.
///
/// Baseline safety: with no modifier applied this returns exactly the game's original
/// numbers, so an unmodified run behaves identically to before.
///
/// State is static because a run's rules are global and short-lived, matching how
/// <see cref="FtueState"/> and <see cref="DailyStreak"/> already work. It is reset on
/// subsystem registration so a disabled domain reload can't leak a modifier between plays.
/// </summary>
public static class RunModifierService
{
    private static RunModifier active = RunModifier.None;
    private static RunModifierDefinition definition = RunModifierDefinition.For(RunModifier.None);

    // Swing speed is the one rule we can't express as a passive query — it has to be
    // pushed into SpawnerHolder and pulled back out again when the run ends.
    private static float swingSpeedBeforeApply = -1f;

    /// <summary>The modifier in play, or <see cref="RunModifier.None"/>.</summary>
    public static RunModifier Active => active;

    /// <summary>The full rule set for the current run. Always valid, never null.</summary>
    public static RunModifierDefinition Definition => definition;

    /// <summary>True when any modifier is applied.</summary>
    public static bool HasModifier => active != RunModifier.None;

    /// <summary>Convenience test for a specific modifier.</summary>
    public static bool IsActive(RunModifier modifier) =>
        modifier != RunModifier.None && active == modifier;

    // ── Rule queries ─────────────────────────────────────────────────────
    // Each returns the baseline when no modifier is applied, so callers can use them
    // unconditionally in place of the constants they used to hard-code.

    /// <summary>Accuracy (0-1) needed for a Perfect landing this run.</summary>
    public static float PerfectThreshold => definition.perfectThreshold;

    /// <summary>Accuracy (0-1) needed for a Good landing this run.</summary>
    public static float GoodThreshold => definition.goodThreshold;

    /// <summary>Flat score multiplier for this run.</summary>
    public static float ScoreMultiplier => definition.scoreMultiplier;

    /// <summary>True when a sub-Good landing should end the run outright.</summary>
    public static bool EndsRunOnPoorLanding => definition.endRunOnPoorLanding;

    /// <summary>True when a Good landing preserves the combo (the baseline rule).</summary>
    public static bool GoodHoldsCombo => definition.goodHoldsCombo;

    /// <summary>
    /// Combo multiplier for <paramref name="combo"/> consecutive Perfects, or -1 when this
    /// run uses the baseline (additive) progression and the caller should compute its own.
    /// </summary>
    public static float GetComboMultiplierOverride(int combo)
    {
        if (!definition.geometricCombo) return -1f;

        float multiplier = Mathf.Pow(definition.geometricComboBase, Mathf.Max(0, combo - 1));
        float cap = definition.comboMultiplierCap > 0f ? definition.comboMultiplierCap : float.MaxValue;
        return Mathf.Min(multiplier, cap);
    }

    /// <summary>Localized display name, or empty when no modifier is applied.</summary>
    public static string DisplayName =>
        definition.IsSomething ? LocalizationManager.Get(definition.nameKey) : string.Empty;

    /// <summary>Localized description, or empty when no modifier is applied.</summary>
    public static string Description =>
        definition.IsSomething ? LocalizationManager.Get(definition.descriptionKey) : string.Empty;

    // ── Lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Applies <paramref name="modifier"/> for the coming run.
    ///
    /// <paramref name="swingSpeedMultiplierOverride"/> lets a caller keep its own tuned
    /// value (the Daily has one serialized on its prefab) instead of the table's default.
    /// Pass a value &lt;= 0 to use the table.
    /// </summary>
    public static void Apply(RunModifier modifier, float swingSpeedMultiplierOverride = -1f)
    {
        // Restore anything the previous modifier pushed out before overwriting it.
        Clear();

        active = modifier;
        definition = RunModifierDefinition.For(modifier);

        float swingMultiplier = swingSpeedMultiplierOverride > 0f
            ? swingSpeedMultiplierOverride
            : definition.swingSpeedMultiplier;

        if (!Mathf.Approximately(swingMultiplier, 1f))
        {
            ApplySwingSpeed(swingMultiplier);
        }

        if (definition.IsSomething)
        {
            Debug.Log($"[RunModifier] Applied '{modifier}'.");
        }
    }

    /// <summary>
    /// Clears the modifier and undoes its side-effects. Safe to call when nothing is applied.
    /// </summary>
    public static void Clear()
    {
        RestoreSwingSpeed();

        active = RunModifier.None;
        definition = RunModifierDefinition.For(RunModifier.None);
    }

    private static void ApplySwingSpeed(float multiplier)
    {
        var spawnerHolder = DependencyRegistry.Find<SpawnerHolder>();
        if (spawnerHolder == null)
        {
            Debug.LogWarning("[RunModifier] SpawnerHolder not found - swing speed left unchanged.");
            return;
        }

        swingSpeedBeforeApply = spawnerHolder.SwingSpeed;
        spawnerHolder.SetSwingSpeed(swingSpeedBeforeApply * multiplier);
        Debug.Log($"[RunModifier] Swing speed {swingSpeedBeforeApply} -> {spawnerHolder.SwingSpeed}");
    }

    private static void RestoreSwingSpeed()
    {
        if (swingSpeedBeforeApply <= 0f) return;

        var spawnerHolder = DependencyRegistry.Find<SpawnerHolder>();
        if (spawnerHolder != null)
        {
            spawnerHolder.SetSwingSpeed(swingSpeedBeforeApply);
        }

        swingSpeedBeforeApply = -1f;
    }

    /// <summary>
    /// Wipes state before the first scene loads. Required because "Enter Play Mode without
    /// domain reload" keeps statics alive between play sessions in the editor.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active = RunModifier.None;
        definition = RunModifierDefinition.For(RunModifier.None);
        swingSpeedBeforeApply = -1f;
    }
}
