namespace HealthcareSupport.CaseEvaluation.States;

public static class StateConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "State." : string.Empty);
    }
}