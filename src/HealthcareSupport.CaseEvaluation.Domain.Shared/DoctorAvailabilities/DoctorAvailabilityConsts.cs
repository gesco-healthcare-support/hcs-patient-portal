namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public static class DoctorAvailabilityConsts
{
    private const string DefaultSorting = "{0}AvailableDate asc";

    /// <summary>
    /// 2026-10-30 -- the most patients one slot can be set to admit from the capacity editor. Generate
    /// has no upper bound of its own; this one keeps a typo (500 for 5) from opening a slot to a flood
    /// of requests. Capacity 0 means the slot is closed to new requests.
    /// </summary>
    public const int MaxCapacity = 50;

    /// <summary>
    /// 2026-10-30 -- the most slots one capacity change may touch. A day at one location is a few
    /// dozen at most; the cap bounds the transaction.
    /// </summary>
    public const int MaxSlotsPerCapacityChange = 500;

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "DoctorAvailability." : string.Empty);
    }
}