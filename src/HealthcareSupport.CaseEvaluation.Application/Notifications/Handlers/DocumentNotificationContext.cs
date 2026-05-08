using System;
using System.Collections.Generic;
using System.Globalization;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 14b (2026-05-04) -- shared variable-dictionary builder used by
/// the document-flow email handlers. Centralizes the
/// <c>##VarName##</c> map so every handler exposes the same set of
/// substitution keys regardless of which event triggered it.
///
/// <para>OLD parity: OLD's
/// <c>vEmailSenderViewModel</c> (per
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:441-451)
/// carries the same fields. NEW reflects-walk the equivalent
/// <c>NotificationTemplateVariables</c> flat dictionary at template-
/// substitute time via Phase 18's
/// <c>TemplateVariableSubstitutor</c>.</para>
///
/// <para><c>public static</c> for test reach + cross-handler reuse;
/// pure (no DI, no IO) so handlers can call it freely.</para>
/// </summary>
public static class DocumentNotificationContext
{
    /// <summary>
    /// Standard variable bag for document emails. Empty / null source
    /// values render as empty string downstream
    /// (<c>TemplateVariableSubstitutor.FormatValue</c> handles null).
    /// </summary>
    public static IReadOnlyDictionary<string, object?> BuildVariables(
        string? patientFirstName,
        string? patientLastName,
        string? patientEmail,
        string? requestConfirmationNumber,
        DateTime? appointmentDate,
        string? claimNumber,
        string? wcabAdj,
        string? documentName,
        string? rejectionNotes,
        string? clinicName,
        string? portalUrl)
    {
        var fullName = BuildFullName(patientFirstName, patientLastName);
        var subjectIdentity = EmailSubjectBuilder.BuildIdentitySuffix(
            patientFirstName, patientLastName, claimNumber, wcabAdj);

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["PatientFirstName"] = patientFirstName ?? string.Empty,
            ["PatientLastName"] = patientLastName ?? string.Empty,
            ["PatientFullName"] = fullName,
            ["PatientEmail"] = patientEmail ?? string.Empty,
            ["AppointmentRequestConfirmationNumber"] =
                requestConfirmationNumber ?? string.Empty,
            ["AppointmentDate"] = appointmentDate.HasValue
                ? appointmentDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
                : string.Empty,
            ["ClaimNumber"] = claimNumber ?? string.Empty,
            ["WcabAdj"] = wcabAdj ?? string.Empty,
            ["DocumentName"] = documentName ?? string.Empty,
            ["RejectionNotes"] = rejectionNotes ?? string.Empty,
            ["ClinicName"] = clinicName ?? string.Empty,
            ["PortalUrl"] = portalUrl ?? string.Empty,
            ["EmailSubjectIdentity"] = subjectIdentity,
        };
    }

    private static string BuildFullName(string? first, string? last)
    {
        var hasFirst = !string.IsNullOrWhiteSpace(first);
        var hasLast = !string.IsNullOrWhiteSpace(last);
        if (hasFirst && hasLast)
        {
            return first!.Trim() + " " + last!.Trim();
        }
        if (hasFirst)
        {
            return first!.Trim();
        }
        if (hasLast)
        {
            return last!.Trim();
        }
        return string.Empty;
    }
}
