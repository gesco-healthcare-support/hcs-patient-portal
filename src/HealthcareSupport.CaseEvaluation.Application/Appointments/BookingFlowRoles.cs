namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11h (2026-05-04) -- pure role-driven decisions for the
/// booking flow. Mirrors OLD
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>
/// lines 221-240 (UserType.InternalUser fast-path) and 358-380
/// (Adjuster auto-fill of ClaimExaminerEmail).
///
/// Extracted as <c>internal static</c> for unit-testability via the
/// existing <c>InternalsVisibleTo</c> wiring (matches the Phase 3 / 5
/// / 6 / 11a / 11b / 11e / 11f / 11i pattern).
/// </summary>
internal static class BookingFlowRoles
{
    /// <summary>
    /// Internal-user roles that flip the booking flow into the
    /// "auto-approved" fast-path. OLD's <c>UserType.InternalUser</c>
    /// covers admin / Clinic Staff / Staff Supervisor / IT Admin /
    /// Doctor. Mirror exactly so external callers (Patient, AA, DA,
    /// CE, Adjuster) always land at <c>Pending</c> and only the
    /// office side can self-approve.
    /// </summary>
    internal static readonly System.Collections.Generic.IReadOnlyList<string> InternalUserRoles = new[]
    {
        "admin",
        "Clinic Staff",
        "Staff Supervisor",
        "IT Admin",
        "Doctor",
    };

    /// <summary>
    /// Returns <c>true</c> when the calling user holds at least one
    /// internal role. The Manager.CreateAsync caller threads this
    /// through to choose Pending (external) vs Approved (internal)
    /// status at booking time.
    /// </summary>
    internal static bool IsInternalUserCaller(System.Collections.Generic.IEnumerable<string?>? callerRoles)
    {
        if (callerRoles == null)
        {
            return false;
        }
        foreach (var role in callerRoles)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }
            var trimmed = role.Trim();
            foreach (var internalRole in InternalUserRoles)
            {
                if (string.Equals(trimmed, internalRole, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Claim-examiner auto-fill: when the booker is the claim-examiner
    /// (OLD's <c>Adjuster</c> role, NEW's <c>Claim Examiner</c> role --
    /// same role under different names per OLD
    /// <c>P:\PatientPortalOld\PatientAppointment.Models\Enums\Roles.cs</c>:15
    /// (<c>Adjuster = 5</c>) renamed to "Claim Examiner" in NEW),
    /// the claim-examiner email field is forced to the booker's own
    /// email regardless of what the DTO carried. The UI rendered this
    /// field readonly for the role; the server-side override is the
    /// belt-and-suspenders so a hand-crafted API call cannot bypass.
    ///
    /// Returns the DTO value untouched when the caller is NOT in the
    /// Claim Examiner role, so non-CE callers keep authority over the
    /// field.
    ///
    /// Note: OLD's <c>AppointmentDomain.cs</c> does not have an active
    /// auto-fill block today (the relevant <c>AdjusterEmail</c> handler
    /// is commented out at lines 706-708). The audit-doc's claim that
    /// OLD ran this rule live was based on the readonly UI behaviour;
    /// NEW preserves the UI's intent at the API layer rather than
    /// faithfully porting OLD's commented-out C#. Documented as a
    /// defensive NEW-side override consistent with OLD's UI contract.
    /// </summary>
    internal static string? ResolveClaimExaminerEmail(
        System.Collections.Generic.IEnumerable<string?>? callerRoles,
        string? currentUserEmail,
        string? dtoClaimExaminerEmail)
    {
        if (callerRoles == null || string.IsNullOrWhiteSpace(currentUserEmail))
        {
            return dtoClaimExaminerEmail;
        }
        foreach (var role in callerRoles)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }
            if (string.Equals(role.Trim(), "Claim Examiner", System.StringComparison.OrdinalIgnoreCase))
            {
                return currentUserEmail;
            }
        }
        return dtoClaimExaminerEmail;
    }
}
