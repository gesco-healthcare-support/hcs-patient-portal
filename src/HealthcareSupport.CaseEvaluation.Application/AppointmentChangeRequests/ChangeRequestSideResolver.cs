using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- determines which "side" submitted a change request and
/// the opposing side's single actionable representative. Side A = Patient +
/// Applicant Attorney; Side B = Defense Attorney + Claim Examiner. The opposing
/// representative is the attorney if present, else the fallback (patient for Side A,
/// claim examiner for Side B). Reuses <see cref="IAppointmentRecipientResolver"/> so
/// party resolution stays in one place.
///
/// <para>Returns <c>null</c> when the submitter cannot be matched to a side, or the
/// opposing side has no representative -- the caller routes those to the Staff
/// Supervisor (the documented defensive path; not expected in practice since the
/// patient and a claim examiner always exist).</para>
/// </summary>
public class ChangeRequestSideResolver : ITransientDependency
{
    private readonly IAppointmentRecipientResolver _recipientResolver;

    public ChangeRequestSideResolver(IAppointmentRecipientResolver recipientResolver)
    {
        _recipientResolver = recipientResolver;
    }

    public virtual async Task<ChangeRequestSideResolution?> ResolveAsync(Guid appointmentId, string? submitterEmail)
    {
        var recipients = (await _recipientResolver.ResolveAsync(appointmentId, NotificationKind.Submitted))
            .Where(r => !string.IsNullOrWhiteSpace(r.To))
            .ToList();

        var submitterParty = string.IsNullOrWhiteSpace(submitterEmail)
            ? null
            : recipients.FirstOrDefault(r => string.Equals(r.To, submitterEmail, StringComparison.OrdinalIgnoreCase));

        var side = ClassifySide(submitterParty?.Role);
        if (side == null)
        {
            return null;
        }

        var opposingRep = side == ChangeRequestSide.SideA
            ? recipients.FirstOrDefault(r => r.Role == RecipientRole.DefenseAttorney)
              ?? recipients.FirstOrDefault(r => r.Role == RecipientRole.ClaimExaminer)
            : recipients.FirstOrDefault(r => r.Role == RecipientRole.ApplicantAttorney)
              ?? recipients.FirstOrDefault(r => r.Role == RecipientRole.Patient);

        if (opposingRep == null)
        {
            return null;
        }

        return new ChangeRequestSideResolution(
            side.Value,
            opposingRep.To,
            opposingRep.Role ?? RecipientRole.Patient,
            recipients);
    }

    private static ChangeRequestSide? ClassifySide(RecipientRole? role) => role switch
    {
        RecipientRole.Patient => ChangeRequestSide.SideA,
        RecipientRole.ApplicantAttorney => ChangeRequestSide.SideA,
        RecipientRole.DefenseAttorney => ChangeRequestSide.SideB,
        RecipientRole.ClaimExaminer => ChangeRequestSide.SideB,
        _ => null,
    };
}

/// <summary>
/// Result of <see cref="ChangeRequestSideResolver.ResolveAsync"/>: the submitting
/// side, the opposing representative's email + role, and the full party list (so the
/// caller can CC everyone on the confirmation email).
/// </summary>
public sealed record ChangeRequestSideResolution(
    ChangeRequestSide RequestingSide,
    string OpposingRepEmail,
    RecipientRole OpposingRepRole,
    IReadOnlyList<SendAppointmentEmailArgs> AllParties);
