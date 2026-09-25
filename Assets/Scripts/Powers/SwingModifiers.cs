/// <summary>
/// Run-scoped nudges to the swing that <see cref="SpawnerHolder"/> reads every frame.
///
/// Kept out of SpawnerHolder's own tuning so a power or temple rule can bend the swing for a
/// few drops and hand it back untouched: Quetzal Feather slows it, Jungle Wind pushes its
/// centre to one side. Both reset to neutral at the start and end of every run.
/// </summary>
public static class SwingModifiers
{
    /// <summary>Multiplies how fast the swing advances. 1 = unchanged.</summary>
    public static float SpeedScale { get; set; } = 1f;

    /// <summary>World units the swing's centre is pushed sideways. 0 = unchanged.</summary>
    public static float LateralOffset { get; set; }

    public static void ResetAll()
    {
        SpeedScale = 1f;
        LateralOffset = 0f;
    }
}
