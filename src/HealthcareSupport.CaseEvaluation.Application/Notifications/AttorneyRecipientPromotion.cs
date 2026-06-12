namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 4 / C3 / D3 (firm-based AA/DA registration) -- pure recipient-promotion
/// decision for the appointment emails. When the appointment CREATOR (the
/// firm/paralegal booker) holds an attorney role AND the appointment names that
/// side's attorney email, the appointment emails are addressed TO that attorney
/// (the firm/paralegal booker + the other parties are CC'd).
///
/// <para>Returns null when no promotion applies, so every caller can fall back to
/// its existing booker anchor unchanged (<c>PrimaryRecipientEmail ?? bookerEmail</c>).
/// Crucially this keeps Patient / Claim Examiner / internal-staff bookings
/// byte-identical -- an earlier draft returned the creator email as a blanket
/// fallback, which would have flipped the To from the patient to an internal staff
/// booker (CreatorId = staff, IdentityUserId = patient) on staff-booked
/// appointments.</para>
/// </summary>
public static class AttorneyRecipientPromotion
{
    /// <summary>Canonical role names (mirror the ExternalUserType -> role map in
    /// ExternalSignups). Compared case-insensitively.</summary>
    public const string ApplicantAttorneyRole = "Applicant Attorney";

    public const string DefenseAttorneyRole = "Defense Attorney";

    /// <summary>
    /// Resolves the promoted "To" email, or null when no promotion applies.
    /// Applicant-Attorney takes precedence over Defense-Attorney when the creator
    /// holds both (D9 role accumulation). A role whose matching attorney email is
    /// blank does NOT promote -- there is no attorney address to send to.
    /// </summary>
    public static string? ResolvePrimaryRecipientEmail(
        IEnumerable<string>? creatorRoles,
        string? applicantAttorneyEmail,
        string? defenseAttorneyEmail)
    {
        if (creatorRoles == null)
        {
            return null;
        }

        var roles = new HashSet<string>(creatorRoles, StringComparer.OrdinalIgnoreCase);

        if (roles.Contains(ApplicantAttorneyRole) && !string.IsNullOrWhiteSpace(applicantAttorneyEmail))
        {
            return applicantAttorneyEmail.Trim();
        }

        if (roles.Contains(DefenseAttorneyRole) && !string.IsNullOrWhiteSpace(defenseAttorneyEmail))
        {
            return defenseAttorneyEmail.Trim();
        }

        return null;
    }
}
