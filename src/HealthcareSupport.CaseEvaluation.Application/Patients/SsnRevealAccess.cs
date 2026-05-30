using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// F1 / Design B (2026-05-29) -- pure authorization predicate for the SSN
/// reveal endpoint (<c>PatientsAppService.GetFullSsnAsync</c>). The full SSN
/// may be revealed only to an internal caller (admin / Clinic Staff / Staff
/// Supervisor / IT Admin / Doctor, via
/// <see cref="BookingFlowRoles.IsInternalUserCaller"/>) OR to the record owner
/// (the caller whose IdentityUser is the patient's own). External non-owners
/// (Applicant / Defense Attorney, Claim Examiner viewing someone else's
/// record) are denied even when they hold the <c>Patients.RevealSsn</c>
/// permission -- the permission gate is the first layer; this owner-scoping is
/// the second.
///
/// <para>Pure (no DI / no DB) so the branches are unit-tested directly; the
/// AppService passes in <c>CurrentUser.Roles</c> / <c>CurrentUser.Id</c> and
/// the target patient's <c>IdentityUserId</c>. Mirrors the
/// <see cref="SsnVisibility"/> /
/// <see cref="HealthcareSupport.CaseEvaluation.AppointmentDocuments.PacketVisibility"/>
/// testable-helper pattern.</para>
/// </summary>
internal static class SsnRevealAccess
{
    internal static bool CanReveal(
        IEnumerable<string?>? callerRoles,
        Guid? callerIdentityUserId,
        Guid patientIdentityUserId)
    {
        if (BookingFlowRoles.IsInternalUserCaller(callerRoles))
        {
            return true;
        }
        return callerIdentityUserId.HasValue
            && callerIdentityUserId.Value == patientIdentityUserId;
    }
}
