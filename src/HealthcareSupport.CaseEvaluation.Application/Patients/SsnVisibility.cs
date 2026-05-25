namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// F4-01 (2026-05-25) -- role-aware SSN redaction at the AppService
/// mapping boundary.
///
/// <para>OLD <c>P:\PatientPortalOld</c> renders SSN in plain text to
/// every viewer (no redaction at all). The NEW stack previously masked
/// SSN client-side via <c>-webkit-text-security: disc</c> on every
/// input, which (a) didn't change what crossed the wire and (b) hid
/// the SSN even from people authorized to see it. This helper moves
/// the control to the API layer so the wire never carries the value
/// for callers who shouldn't see it -- defense in depth -- and the
/// client-side blanket-redaction can be removed.</para>
///
/// <para>Visibility grid:</para>
/// <list type="bullet">
///   <item>Internal caller (Clinic Staff / Staff Supervisor / IT Admin)
///         -- full value.</item>
///   <item>Record owner (caller's IdentityUserId matches the patient's)
///         -- full value, even if the caller is an external role.</item>
///   <item>Everyone else (external attorney / claim examiner viewing
///         someone else's record) -- last 4 chars prefixed with
///         <see cref="MaskedPrefix"/>.</item>
/// </list>
///
/// <para>Treats SSN as opaque -- the helper does not know that
/// production stores 9 digits with no separators. Tests pass synthetic
/// hex strings (per .claude/rules/test-data.md).</para>
///
/// <para>Pure (no DI / no DB): the AppService passes in the live DTO
/// and the two role / ownership flags it has already resolved.
/// Caller-side role resolution uses
/// <see cref="HealthcareSupport.CaseEvaluation.Appointments.BookingFlowRoles.IsInternalUserCaller"/>.</para>
/// </summary>
internal static class SsnVisibility
{
    internal const string MaskedPrefix = "***-**-";

    internal static string? RedactForCaller(string? ssn, bool isInternalCaller, bool isRecordOwner)
    {
        if (string.IsNullOrEmpty(ssn))
        {
            return ssn;
        }
        if (isInternalCaller || isRecordOwner)
        {
            return ssn;
        }
        if (ssn.Length < 4)
        {
            return MaskedPrefix;
        }
        return MaskedPrefix + ssn.Substring(ssn.Length - 4);
    }

    internal static PatientDto? RedactForCaller(PatientDto? dto, bool isInternalCaller, bool isRecordOwner)
    {
        if (dto == null)
        {
            return null;
        }
        dto.SocialSecurityNumber = RedactForCaller(dto.SocialSecurityNumber, isInternalCaller, isRecordOwner);
        return dto;
    }

    internal static PatientWithNavigationPropertiesDto? RedactForCaller(
        PatientWithNavigationPropertiesDto? dto,
        bool isInternalCaller,
        bool isRecordOwner)
    {
        if (dto?.Patient != null)
        {
            RedactForCaller(dto.Patient, isInternalCaller, isRecordOwner);
        }
        return dto;
    }
}
