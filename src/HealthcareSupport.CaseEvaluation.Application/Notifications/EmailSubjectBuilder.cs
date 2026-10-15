using System;
using System.Text;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 14b (2026-05-04) -- pure helper that builds the OLD-parity
/// email-subject suffix used across the document flows + the
/// stakeholder approval emails. Mirrors OLD's
/// <c>AppointmentNewDocumentDomain.SendDocumentEmail</c> pattern at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:451-456:
/// "Patient: {FirstName} {LastName} - Claim: {ClaimNumber} - ADJ: {WcabAdj}".
///
/// <para>OLD wraps the suffix in parentheses and concatenates onto the
/// template's base subject:
/// <code>
/// var subject = "Patient Appointment Portal - " + patientDetailsEmailSubject + " - Appointment document is " + status;
/// </code>
/// where <c>patientDetailsEmailSubject = "(" + patientName + injuryDetails + ")"</c>.</para>
///
/// <para>NEW exposes the same suffix as a template variable
/// <c>##EmailSubjectIdentity##</c> so the per-template Subject can
/// place it wherever. Templates that need ONLY the patient identity
/// reference <c>##PatientFullName##</c>; templates that need the full
/// "(Patient: ... - Claim: ... - ADJ: ...)" suffix reference
/// <c>##EmailSubjectIdentity##</c>.</para>
///
/// <para><c>public static</c> -- pure functions; lives in Session B's
/// notification namespace. Tests pin the empty / null / partial-data
/// cases.</para>
/// </summary>
public static class EmailSubjectBuilder
{
    /// <summary>
    /// Builds the OLD-verbatim "(Patient: {first} {last} - Claim: {claim}
    /// - ADJ: {adj})" suffix. Empty fields are skipped (mirrors OLD's
    /// <c>String.IsNullOrEmpty</c> guards in the source); when nothing
    /// is supplied, returns empty string. Trims surrounding whitespace
    /// on every input.
    /// </summary>
    public static string BuildIdentitySuffix(
        string? patientFirstName,
        string? patientLastName,
        string? claimNumber,
        string? wcabAdj)
    {
        var fullName = JoinNonEmpty(patientFirstName?.Trim(), patientLastName?.Trim(), separator: " ");
        var hasClaim = !string.IsNullOrWhiteSpace(claimNumber);
        var hasAdj = !string.IsNullOrWhiteSpace(wcabAdj);
        var hasName = !string.IsNullOrEmpty(fullName);

        if (!hasName && !hasClaim && !hasAdj)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append('(');
        if (hasName)
        {
            sb.Append("Patient: ").Append(fullName);
        }
        if (hasClaim)
        {
            if (hasName)
            {
                sb.Append(" - ");
            }
            sb.Append("Claim: ").Append(claimNumber!.Trim());
        }
        if (hasAdj)
        {
            if (hasName || hasClaim)
            {
                sb.Append(" - ");
            }
            sb.Append("ADJ: ").Append(wcabAdj!.Trim());
        }
        sb.Append(')');
        return sb.ToString();
    }

    /// <summary>
    /// Convenience overload that joins patient first + last and accepts
    /// the result alongside claim + adj. Used when the caller has a
    /// pre-built display name.
    /// </summary>
    public static string BuildIdentitySuffixFromFullName(
        string? patientFullName,
        string? claimNumber,
        string? wcabAdj)
    {
        return BuildIdentitySuffix(patientFullName, patientLastName: null, claimNumber, wcabAdj);
    }

    private static string JoinNonEmpty(string? first, string? second, string separator)
    {
        var hasFirst = !string.IsNullOrEmpty(first);
        var hasSecond = !string.IsNullOrEmpty(second);
        if (hasFirst && hasSecond)
        {
            return first + separator + second;
        }
        if (hasFirst)
        {
            return first!;
        }
        if (hasSecond)
        {
            return second!;
        }
        return string.Empty;
    }
}
