namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// F1 / Design B (2026-05-29) -- payload of the dedicated SSN reveal endpoint
/// (<c>IPatientsAppService.GetFullSsnAsync</c>). Standard patient payloads now
/// carry only the masked last-4; this DTO is the ONLY response that carries
/// the full, unmasked value, and only to callers who pass both the
/// <c>Patients.RevealSsn</c> permission gate and the internal-or-owner check
/// (<c>SsnRevealAccess</c>). NOT audited (corrected 2026-10-07): this is a GET and
/// the HTTP audit log does not record GETs (IsEnabledForGetRequests is false by
/// default), so reveals leave no audit trail today. See GetFullSsnAsync's note.
/// </summary>
public class SsnRevealDto
{
    public string? SocialSecurityNumber { get; set; }
}
