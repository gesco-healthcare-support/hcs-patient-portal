namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

public static class AppointmentChangeRequestConsts
{
    /// <summary>
    /// Cap for free-text reason / notes fields. Aligns with
    /// Appointments.AppointmentConsts.ReasonMaxLength so a copy-down from
    /// the change request to the parent appointment cannot truncate.
    /// </summary>
    public const int ReasonMaxLength = 1000;

    // ---- Group D (2026-06-09): opposing-side consent token ----

    /// <summary>Consent token entropy: 32 random bytes = 256-bit (mirrors InvitationConsts).</summary>
    public const int ConsentTokenByteLength = 32;

    /// <summary>SHA256 hex length (64 chars) stored in <c>ConsentTokenHash</c>.</summary>
    public const int ConsentTokenHashLength = 64;

    /// <summary>Defensive cap on the URL-supplied raw token before a DB roundtrip.</summary>
    public const int ConsentEncodedTokenMaxLength = 64;

    /// <summary>Consent token lifetime; matches the invitation default (7 days).</summary>
    public const int ConsentDefaultTtlDays = 7;

    /// <summary>Max length for the recorded responder email.</summary>
    public const int ConsentRespondedByEmailMaxLength = 256;

    /// <summary>
    /// Group D (2026-06-09) feature flag (kill switch). When false, the submit flow
    /// skips consent issuance and the approval gate is a no-op (legacy staff-only
    /// flow). A compile-time const for Phase 1; promote to a per-tenant
    /// <c>ISettingProvider</c> setting when multi-tenant toggling is needed.
    /// </summary>
    public const bool ConsentGatingEnabled = true;
}
