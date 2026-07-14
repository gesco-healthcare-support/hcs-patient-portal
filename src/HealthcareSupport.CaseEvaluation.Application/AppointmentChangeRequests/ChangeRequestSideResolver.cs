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

    public virtual async Task<ChangeRequestSideResolution?> ResolveAsync(
        Guid appointmentId, string? submitterEmail, IEnumerable<string?>? submitterRoles = null)
    {
        var recipients = (await _recipientResolver.ResolveAsync(appointmentId, NotificationKind.Submitted))
            .Where(r => !string.IsNullOrWhiteSpace(r.To))
            .ToList();

        var submitterParty = string.IsNullOrWhiteSpace(submitterEmail)
            ? null
            : recipients.FirstOrDefault(r => string.Equals(r.To, submitterEmail, StringComparison.OrdinalIgnoreCase));

        // F-014 fix (2026-06-23): the submitter is often the BOOKER (paralegal) who is not a
        // named party on the appointment, so an email match yields no side and consent used to
        // be skipped. Fall back to the submitter's registered role to place them on a side:
        // Patient / Applicant Attorney = Side A; Defense Attorney / Claim Examiner = Side B.
        var side = ClassifySide(submitterParty?.Role) ?? ClassifySideFromRoles(submitterRoles);
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

    /// <summary>
    /// Resolves BOTH sides' single actionable representatives for a staff-initiated change
    /// (2026-07-01): Side A = Applicant Attorney (else Patient); Side B = Defense Attorney
    /// (else Claim Examiner). Either email may be null when that side has no representative
    /// on the appointment -- the caller then leaves that side NotRequired (auto-satisfied).
    /// </summary>
    public virtual async Task<ChangeRequestBothSidesResolution> ResolveBothSidesAsync(Guid appointmentId)
    {
        var recipients = (await _recipientResolver.ResolveAsync(appointmentId, NotificationKind.Submitted))
            .Where(r => !string.IsNullOrWhiteSpace(r.To))
            .ToList();

        var sideA = recipients.FirstOrDefault(r => r.Role == RecipientRole.ApplicantAttorney)
                    ?? recipients.FirstOrDefault(r => r.Role == RecipientRole.Patient);
        var sideB = recipients.FirstOrDefault(r => r.Role == RecipientRole.DefenseAttorney)
                    ?? recipients.FirstOrDefault(r => r.Role == RecipientRole.ClaimExaminer);

        return new ChangeRequestBothSidesResolution(
            sideA?.To, sideA?.Role,
            sideB?.To, sideB?.Role);
    }

    private static ChangeRequestSide? ClassifySide(RecipientRole? role) => role switch
    {
        RecipientRole.Patient => ChangeRequestSide.SideA,
        RecipientRole.ApplicantAttorney => ChangeRequestSide.SideA,
        RecipientRole.DefenseAttorney => ChangeRequestSide.SideB,
        RecipientRole.ClaimExaminer => ChangeRequestSide.SideB,
        _ => null,
    };

    /// <summary>
    /// F-014 fallback: classify the submitter's side from their registered role name when they
    /// are not a named party on the appointment (the booker/paralegal acting on behalf). Side A
    /// = Applicant Attorney / Patient; Side B = Defense Attorney / Claim Examiner. Returns null
    /// for internal/neutral roles so consent stays unresolved (Staff Supervisor finalizes).
    /// </summary>
    private static ChangeRequestSide? ClassifySideFromRoles(IEnumerable<string?>? roles)
    {
        if (roles == null)
        {
            return null;
        }
        foreach (var role in roles)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }
            var trimmed = role.Trim();
            if (string.Equals(trimmed, "Applicant Attorney", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                return ChangeRequestSide.SideA;
            }
            if (string.Equals(trimmed, "Defense Attorney", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "Claim Examiner", StringComparison.OrdinalIgnoreCase))
            {
                return ChangeRequestSide.SideB;
            }
        }
        return null;
    }
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

/// <summary>
/// Both sides' representatives for a staff-initiated change (2026-07-01). Either email/role
/// may be null when that side has no representative on the appointment.
/// </summary>
public sealed record ChangeRequestBothSidesResolution(
    string? SideARepEmail,
    RecipientRole? SideARepRole,
    string? SideBRepEmail,
    RecipientRole? SideBRepRole);
