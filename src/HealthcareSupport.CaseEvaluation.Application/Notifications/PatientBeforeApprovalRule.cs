using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// The office's rule for the patient on a request someone else submitted (2026-11-03): until the
/// appointment is approved, the patient hears nothing about it -- not that it was requested, not that
/// it was rejected, not that more information was asked for. The rule covers the new-request,
/// rejected and sent-back emails; after approval the patient is a normal party again.
///
/// <para>The patient is identified by the appointment's patient addresses (the patient row and the
/// appointment's patient email column), never by <see cref="RecipientRole.Patient"/>: the recipient
/// resolver tags a submitter who is not a named party with that role, so the tag would drop the very
/// person who should be told. When the patient IS the submitter, nothing is removed.</para>
/// </summary>
public static class PatientBeforeApprovalRule
{
    /// <summary>The recipients without the patient, unless the patient submitted the request.</summary>
    public static List<SendAppointmentEmailArgs> Apply(
        IEnumerable<SendAppointmentEmailArgs> recipients, DocumentEmailContext ctx)
    {
        return ctx.PatientIsSubmitter
            ? recipients.ToList()
            : recipients.Where(r => !IsPatientAddress(r.To, ctx)).ToList();
    }

    /// <summary>The recipients without the patient, unless the patient submitted the request.</summary>
    public static List<NotificationRecipient> Apply(
        IEnumerable<NotificationRecipient> recipients, DocumentEmailContext ctx)
    {
        return ctx.PatientIsSubmitter
            ? recipients.ToList()
            : recipients.Where(r => !IsPatientAddress(r.Email, ctx)).ToList();
    }

    private static bool IsPatientAddress(string? email, DocumentEmailContext ctx) =>
        !string.IsNullOrWhiteSpace(email)
        && ctx.PatientAddresses.Any(a => string.Equals(a, email.Trim(), StringComparison.OrdinalIgnoreCase));
}
