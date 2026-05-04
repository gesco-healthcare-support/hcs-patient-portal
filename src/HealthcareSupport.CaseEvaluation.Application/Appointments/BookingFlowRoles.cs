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
    /// OLD's Adjuster auto-fill at <c>AppointmentDomain.cs:358-380</c>:
    /// when the booker is in the Adjuster role, the claim-examiner
    /// email field is forced to the booker's own email regardless of
    /// what the DTO carried. Strict parity preserves the override
    /// (the UI rendered the field readonly for adjusters; if a hand-
    /// crafted API call sneaks a different value through, we still
    /// snap it back).
    ///
    /// Returns the DTO value untouched when the caller is NOT in the
    /// Adjuster role, so non-adjuster callers keep authority over the
    /// field.
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
            if (string.Equals(role.Trim(), "Adjuster", System.StringComparison.OrdinalIgnoreCase))
            {
                return currentUserEmail;
            }
        }
        return dtoClaimExaminerEmail;
    }
}
