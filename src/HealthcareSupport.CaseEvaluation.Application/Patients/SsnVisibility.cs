namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// F4-01 (2026-05-25) origin; F1 / Design B (2026-05-29) revision -- SSN
/// masking at the AppService mapping boundary.
///
/// <para>Under F4-01 this helper was role-aware: internal callers and the
/// record owner received the full SSN over the wire. Design B
/// (docs/plans/2026-05-29-ssn-redact-on-type.md) tightens that: EVERY
/// standard payload now carries only the last 4, regardless of caller role
/// or record ownership. The full value crosses the wire only via the
/// dedicated audited reveal endpoint (<c>PatientsAppService.GetFullSsnAsync</c>),
/// which performs its own internal/owner authorization and reads the raw
/// entity value directly -- it does not go through this helper.</para>
///
/// <para>Because the full value is never returned here, the helper no longer
/// takes role/ownership flags; it unconditionally masks.</para>
///
/// <para>Treats SSN as opaque -- the helper does not know that production
/// stores 9 digits with no separators. Tests pass synthetic hex strings (per
/// .claude/rules/test-data.md) and just assert the trailing 4 chars.</para>
///
/// <para>Pure (no DI / no DB): the AppService passes in the live DTO.</para>
/// </summary>
internal static class SsnVisibility
{
    internal const string MaskedPrefix = "***-**-";

    internal static string? MaskToLast4(string? ssn)
    {
        if (string.IsNullOrEmpty(ssn))
        {
            return ssn;
        }
        if (ssn.Length < 4)
        {
            return MaskedPrefix;
        }
        return MaskedPrefix + ssn.Substring(ssn.Length - 4);
    }

    internal static PatientDto? MaskToLast4(PatientDto? dto)
    {
        if (dto == null)
        {
            return null;
        }
        dto.SocialSecurityNumber = MaskToLast4(dto.SocialSecurityNumber);
        return dto;
    }

    internal static PatientWithNavigationPropertiesDto? MaskToLast4(PatientWithNavigationPropertiesDto? dto)
    {
        if (dto?.Patient != null)
        {
            MaskToLast4(dto.Patient);
        }
        return dto;
    }
}
