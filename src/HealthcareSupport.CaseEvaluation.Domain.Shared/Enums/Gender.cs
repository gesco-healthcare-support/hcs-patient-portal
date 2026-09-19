namespace HealthcareSupport.CaseEvaluation.Enums
{
    public enum Gender
    {
        // G-06-08 (2026-06-01): 0 = "not provided yet". Registration creates a
        // Patient stub before demographics are collected; Unspecified (the
        // default(Gender)) avoids fabricating a real gender (was Gender.Male) on
        // a real person. Booking and profile-save require a real value.
        Unspecified = 0,
        Male = 1,
        Female = 2,
        Other = 3
    }
}
