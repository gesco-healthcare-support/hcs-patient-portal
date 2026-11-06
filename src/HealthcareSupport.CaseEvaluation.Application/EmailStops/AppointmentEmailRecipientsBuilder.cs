using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 -- builds the staff "Who gets emails for this case" card from the SAME sources the emails use:
/// the appointment recipient resolver (patient, attorneys, claim examiner, the booker) and the case followers
/// (the booker and the Authorized Users). Each external address is listed once, with who they are and the
/// routing note that explains why they do not get everything today (a lawyer sending to their paralegal
/// instead, a booker who opted out, a patient whose emails start after approval). The office inbox and staff
/// are never listed.
/// </summary>
public class AppointmentEmailRecipientsBuilder : ITransientDependency
{
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IIdentityUserRepository _userRepository;
    private readonly EmailStopAddressGuard _addressGuard;

    public AppointmentEmailRecipientsBuilder(
        IAppointmentRecipientResolver recipientResolver,
        DocumentEmailContextResolver contextResolver,
        IIdentityUserRepository userRepository,
        EmailStopAddressGuard addressGuard)
    {
        _recipientResolver = recipientResolver;
        _contextResolver = contextResolver;
        _userRepository = userRepository;
        _addressGuard = addressGuard;
    }

    /// <summary>The external people this case emails, before any stop is applied.</summary>
    public virtual async Task<List<AppointmentEmailRecipientDto>> BuildAsync(Guid appointmentId)
    {
        var ctx = await _contextResolver.ResolveAsync(appointmentId, appointmentDocumentId: null);
        if (ctx == null)
        {
            return new List<AppointmentEmailRecipientDto>();
        }

        var rows = new Dictionary<string, AppointmentEmailRecipientDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var party in await _recipientResolver.ResolveAsync(appointmentId, NotificationKind.Submitted))
        {
            if (party.Role == RecipientRole.OfficeAdmin)
            {
                continue;
            }
            var kind = AppointmentRecipientResolver.IsBookerEntry(party)
                ? EmailRecipientKind.Booker
                : KindOfParty(party.Role);
            AddKind(rows, party.To, kind);
        }
        foreach (var follower in ctx.Followers)
        {
            AddKind(rows, follower.Email, follower.IsBooker ? EmailRecipientKind.Booker : EmailRecipientKind.AuthorizedUser);
            foreach (var kind in KindsOfRoles(follower.RoleNames))
            {
                AddKind(rows, follower.Email, kind);
            }
        }

        var result = new List<AppointmentEmailRecipientDto>();
        foreach (var row in rows.Values)
        {
            if (await _addressGuard.IsInternalAsync(row.Email))
            {
                continue;
            }
            row.RoutingNote = RoutingNoteOf(ctx, row);
            row.Name = await NameOfAsync(ctx, row);
            row.Kinds = row.Kinds.Distinct().OrderBy(k => k).ToList();
            result.Add(row);
        }
        return result
            .OrderBy(r => r.Kinds.Count == 0 ? int.MaxValue : (int)r.Kinds.Min())
            .ThenBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Why this address does not get every case email today (before any stop).</summary>
    public static EmailRoutingNote RoutingNoteOf(DocumentEmailContext ctx, AppointmentEmailRecipientDto row)
    {
        if (ctx.RedirectedAddresses.Contains(row.Email, StringComparer.OrdinalIgnoreCase))
        {
            return EmailRoutingNote.SendsToParalegalInstead;
        }
        if (!string.IsNullOrWhiteSpace(ctx.OptedOutBookerAddress)
            && string.Equals(ctx.OptedOutBookerAddress.Trim(), row.Email, StringComparison.OrdinalIgnoreCase))
        {
            return EmailRoutingNote.RequestReceiptOnly;
        }
        var isPatient = ctx.PatientAddresses.Contains(row.Email, StringComparer.OrdinalIgnoreCase);
        var beforeApproval = ctx.AppointmentStatus is AppointmentStatusType.Pending or AppointmentStatusType.InfoRequested;
        if (isPatient && beforeApproval && !ctx.PatientIsSubmitter)
        {
            return EmailRoutingNote.StartsAfterApproval;
        }
        return EmailRoutingNote.None;
    }

    private async Task<string?> NameOfAsync(DocumentEmailContext ctx, AppointmentEmailRecipientDto row)
    {
        if (row.Kinds.Contains(EmailRecipientKind.Patient)
            && ctx.PatientAddresses.Contains(row.Email, StringComparer.OrdinalIgnoreCase))
        {
            var patientName = $"{ctx.PatientFirstName} {ctx.PatientLastName}".Trim();
            if (patientName.Length > 0)
            {
                return patientName;
            }
        }
        var normalized = EmailStopAddress.Normalize(row.Email);
        var user = normalized == null
            ? null
            : await _userRepository.FindByNormalizedEmailAsync(normalized, includeDetails: false);
        var name = user == null ? string.Empty : $"{user.Name} {user.Surname}".Trim();
        return name.Length > 0 ? name : null;
    }

    private static void AddKind(Dictionary<string, AppointmentEmailRecipientDto> rows, string? email, EmailRecipientKind? kind)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }
        var address = email.Trim();
        if (!rows.TryGetValue(address, out var row))
        {
            row = new AppointmentEmailRecipientDto { Email = address };
            rows[address] = row;
        }
        if (kind.HasValue)
        {
            row.Kinds.Add(kind.Value);
        }
    }

    private static EmailRecipientKind? KindOfParty(RecipientRole? role) => role switch
    {
        RecipientRole.Patient => EmailRecipientKind.Patient,
        RecipientRole.ApplicantAttorney => EmailRecipientKind.ApplicantAttorney,
        RecipientRole.DefenseAttorney => EmailRecipientKind.DefenseAttorney,
        RecipientRole.ClaimExaminer => EmailRecipientKind.ClaimExaminer,
        _ => null,
    };

    /// <summary>The kinds an Authorized User or booker account's roles make them.</summary>
    public static IEnumerable<EmailRecipientKind> KindsOfRoles(IEnumerable<string?>? roleNames)
    {
        foreach (var role in roleNames ?? Array.Empty<string?>())
        {
            var kind = role?.Trim() switch
            {
                ExternalRoleConsts.Patient => EmailRecipientKind.Patient,
                ExternalRoleConsts.ApplicantAttorney => EmailRecipientKind.ApplicantAttorney,
                ExternalRoleConsts.ApplicantParalegal => EmailRecipientKind.ApplicantParalegal,
                ExternalRoleConsts.DefenseAttorney => EmailRecipientKind.DefenseAttorney,
                ExternalRoleConsts.DefenseParalegal => EmailRecipientKind.DefenseParalegal,
                ExternalRoleConsts.ClaimExaminer => EmailRecipientKind.ClaimExaminer,
                _ => (EmailRecipientKind?)null,
            };
            if (kind.HasValue)
            {
                yield return kind.Value;
            }
        }
    }
}
