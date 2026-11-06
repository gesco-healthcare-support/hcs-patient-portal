using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 (office decision 1) -- the warning before a stop is saved: is this address the ONLY one left
/// who gets the Agree/Decline link for a side? "Who gets the link" is computed exactly as the consent email
/// computes it: the side representative (unless their mail goes to their paralegal) plus every case follower
/// who may answer for that side (<see cref="CaseFollowers.ConsentLinkFollowers"/>); an address that is
/// already stopped does not count. A warning only, never a block: the person can still answer in the portal.
/// </summary>
public class EmailStopConsentImpactCalculator : ITransientDependency
{
    /// <summary>How many open-request appointments the all-cases check reads at most.</summary>
    public const int MaxOpenAppointmentsScanned = 50;

    private static readonly ChangeRequestSide[] BothSides = { ChangeRequestSide.SideA, ChangeRequestSide.SideB };

    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly ChangeRequestSideResolver _sideResolver;
    private readonly IEmailStopPolicy _emailStopPolicy;
    private readonly IRepository<ChangeRequestConsentRound, Guid> _roundRepository;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;

    public EmailStopConsentImpactCalculator(
        DocumentEmailContextResolver contextResolver,
        ChangeRequestSideResolver sideResolver,
        IEmailStopPolicy emailStopPolicy,
        IRepository<ChangeRequestConsentRound, Guid> roundRepository,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        IRepository<Appointment, Guid> appointmentRepository)
    {
        _contextResolver = contextResolver;
        _sideResolver = sideResolver;
        _emailStopPolicy = emailStopPolicy;
        _roundRepository = roundRepository;
        _changeRequestRepository = changeRequestRepository;
        _appointmentRepository = appointmentRepository;
    }

    /// <summary>The warning for a stop made from one case (and, for all cases, the open requests elsewhere).</summary>
    public virtual async Task<EmailStopConsentImpactDto> CalculateAsync(
        Guid? appointmentId, string email, EmailStopScope scope)
    {
        var result = new EmailStopConsentImpactDto();
        if (appointmentId.HasValue)
        {
            result.ThisCaseSides = await OnlyResponderSidesAsync(appointmentId.Value, email, BothSides);
        }
        if (scope == EmailStopScope.AllCases)
        {
            result.OtherCases = await OpenRequestCasesAsync(email, appointmentId);
        }
        result.HasImpact = result.ThisCaseSides.Count > 0 || result.OtherCases.Count > 0;
        return result;
    }

    /// <summary>The sides of one case where <paramref name="email"/> is the only one left who gets the link.</summary>
    public virtual async Task<List<ChangeRequestSide>> OnlyResponderSidesAsync(
        Guid appointmentId, string email, IEnumerable<ChangeRequestSide> sides)
    {
        var target = EmailStopAddress.Normalize(email);
        var ctx = await _contextResolver.ResolveAsync(appointmentId, appointmentDocumentId: null);
        if (target == null || ctx == null)
        {
            return new List<ChangeRequestSide>();
        }
        var reps = await _sideResolver.ResolveBothSidesAsync(appointmentId);

        var respondersBySide = sides.Distinct().ToDictionary(
            side => side,
            side => LinkHolders(ctx, side == ChangeRequestSide.SideA ? reps.SideARepEmail : reps.SideBRepEmail, side));
        var stops = await _emailStopPolicy.LookupAsync(respondersBySide.Values.SelectMany(v => v), appointmentId)
            ?? EmailStopLookup.Empty;

        return respondersBySide
            .Where(pair => IsOnlyResponder(pair.Value, target, stops, appointmentId))
            .Select(pair => pair.Key)
            .ToList();
    }

    /// <summary>Who gets the Agree/Decline link for a side, as the consent email decides it.</summary>
    public static List<string> LinkHolders(DocumentEmailContext ctx, string? representativeEmail, ChangeRequestSide side)
    {
        var holders = new List<string>();
        var redirected = !string.IsNullOrWhiteSpace(representativeEmail)
            && CaseFollowers.IsExcluded(ctx, representativeEmail);
        if (!string.IsNullOrWhiteSpace(representativeEmail) && !redirected)
        {
            holders.Add(representativeEmail.Trim());
        }
        holders.AddRange(CaseFollowers.ConsentLinkFollowers(ctx, side).Select(f => f.Email));
        if (holders.Count == 0 && redirected)
        {
            holders.Add(representativeEmail!.Trim());
        }
        return holders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsOnlyResponder(
        IReadOnlyCollection<string> holders, string target, EmailStopLookup stops, Guid appointmentId)
    {
        var stillReached = holders
            .Where(h => EmailStopAddress.Normalize(h) == target || !stops.IsStopped(h, appointmentId))
            .Select(EmailStopAddress.Normalize)
            .Distinct()
            .ToList();
        return stillReached.Count == 1 && stillReached[0] == target;
    }

    // Other cases of THIS practice with an open Agree/Decline round this address alone can answer. Only
    // open rounds are read (cheap); other practices are not scanned.
    private async Task<List<EmailStopConsentImpactCaseDto>> OpenRequestCasesAsync(string email, Guid? exceptAppointmentId)
    {
        var rounds = await _roundRepository.GetQueryableAsync();
        var requests = await _changeRequestRepository.GetQueryableAsync();
        var open = rounds
            .Where(r => r.SupersededAt == null
                && (r.SideAConsentStatus == ChangeRequestConsentStatus.Pending
                    || r.SideBConsentStatus == ChangeRequestConsentStatus.Pending))
            .Join(requests, r => r.AppointmentChangeRequestId, cr => cr.Id, (r, cr) => new
            {
                cr.AppointmentId,
                SideAPending = r.SideAConsentStatus == ChangeRequestConsentStatus.Pending,
                SideBPending = r.SideBConsentStatus == ChangeRequestConsentStatus.Pending,
            })
            .Where(x => exceptAppointmentId == null || x.AppointmentId != exceptAppointmentId.Value)
            .OrderBy(x => x.AppointmentId)
            .Take(MaxOpenAppointmentsScanned)
            .ToList();

        var result = new List<EmailStopConsentImpactCaseDto>();
        foreach (var item in open)
        {
            var pendingSides = new List<ChangeRequestSide>();
            if (item.SideAPending)
            {
                pendingSides.Add(ChangeRequestSide.SideA);
            }
            if (item.SideBPending)
            {
                pendingSides.Add(ChangeRequestSide.SideB);
            }
            foreach (var side in await OnlyResponderSidesAsync(item.AppointmentId, email, pendingSides))
            {
                var appointment = await _appointmentRepository.FindAsync(item.AppointmentId);
                result.Add(new EmailStopConsentImpactCaseDto
                {
                    AppointmentId = item.AppointmentId,
                    RequestConfirmationNumber = appointment?.RequestConfirmationNumber ?? string.Empty,
                    Side = side,
                });
            }
            if (result.Count >= EmailStopConsts.ConsentImpactMaxOtherCases)
            {
                break;
            }
        }
        return result.Take(EmailStopConsts.ConsentImpactMaxOtherCases).ToList();
    }
}
