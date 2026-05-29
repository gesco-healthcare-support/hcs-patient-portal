using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// F5 (2026-05-29) -- per-role packet allow-list, applied at the
/// <c>AppointmentPacketsAppService</c> boundary (list + download). Pure,
/// internal, and unit-tested -- mirrors the <c>SsnVisibility</c> (F4-01)
/// pattern.
///
/// <para>Grid:</para>
/// <list type="bullet">
///   <item>Internal user (admin / Clinic Staff / Staff Supervisor / IT Admin
///         / Doctor) -- all three kinds.</item>
///   <item>Patient -- the Patient packet only.</item>
///   <item>Applicant Attorney / Defense Attorney / Claim Examiner -- the
///         Attorney-CE packet only.</item>
///   <item>Anyone else -- none.</item>
/// </list>
/// Internal is evaluated first, so a user who somehow holds both an internal
/// and an external role still gets the full set.
/// </summary>
internal static class PacketVisibility
{
    private static readonly IReadOnlyCollection<PacketKind> AllKinds =
        new[] { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer };

    private static readonly IReadOnlyCollection<PacketKind> PatientOnly =
        new[] { PacketKind.Patient };

    private static readonly IReadOnlyCollection<PacketKind> AttyCeOnly =
        new[] { PacketKind.AttorneyClaimExaminer };

    internal static IReadOnlyCollection<PacketKind> AllowedKinds(IEnumerable<string?>? roles)
    {
        if (BookingFlowRoles.IsInternalUserCaller(roles))
        {
            return AllKinds;
        }
        if (roles == null)
        {
            return Array.Empty<PacketKind>();
        }
        var trimmed = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.Trim())
            .ToList();
        if (trimmed.Any(r => string.Equals(r, "Patient", StringComparison.OrdinalIgnoreCase)))
        {
            return PatientOnly;
        }
        if (trimmed.Any(r =>
                string.Equals(r, "Applicant Attorney", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "Defense Attorney", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "Claim Examiner", StringComparison.OrdinalIgnoreCase)))
        {
            return AttyCeOnly;
        }
        return Array.Empty<PacketKind>();
    }

    internal static bool IsAllowed(IEnumerable<string?>? roles, PacketKind kind)
    {
        return AllowedKinds(roles).Contains(kind);
    }
}
