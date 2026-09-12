namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// F1 / Design B (2026-05-29) -- payload of the dedicated SSN reveal endpoint
/// (<c>IPatientsAppService.GetFullSsnAsync</c>). Standard patient payloads now
/// carry only the masked last-4; this DTO is the ONLY response that carries
/// the full, unmasked value, and only to callers who pass both the
/// <c>Patients.RevealSsn</c> permission gate and the internal-or-owner check
/// (<c>SsnRevealAccess</c>). Each call is captured in ABP's HTTP audit log.
/// </summary>
public class SsnRevealDto
{
    public string? SocialSecurityNumber { get; set; }
}
