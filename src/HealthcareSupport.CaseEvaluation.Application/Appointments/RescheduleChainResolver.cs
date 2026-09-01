using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 4d (2026-08-05) -- builds the "rescheduled from" chain for appointments created by
/// finalizing a reschedule.
///
/// <para>Its own service rather than another private method on <c>AppointmentsAppService</c>: the
/// chain needs the change-request and consent-round repositories, which that class has no other
/// reason to know about, and this way the resolution is unit-testable without standing up the app
/// service.</para>
///
/// <para>SET-BASED by construction -- <see cref="ResolveAsync"/> takes a collection and issues
/// three queries no matter how many appointments it is given, mirroring phase 4b's
/// <c>PopulateAppointmentContextAsync</c>. Today only the two detail reads call it with a single
/// appointment; a caller that wants the chain on a whole page needs no change here.</para>
/// </summary>
public class RescheduleChainResolver : ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<ChangeRequestConsentRound, Guid> _consentRoundRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public RescheduleChainResolver(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        IRepository<ChangeRequestConsentRound, Guid> consentRoundRepository,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _appointmentRepository = appointmentRepository;
        _changeRequestRepository = changeRequestRepository;
        _consentRoundRepository = consentRoundRepository;
        _asyncExecuter = asyncExecuter;
    }

    /// <summary>
    /// Chains keyed by the id of the appointment they belong to. Appointments with no
    /// <see cref="Appointment.RescheduledFromAppointmentId"/> are simply absent from the result, so
    /// a caller writes <c>TryGetValue</c> and gets null for a normally booked appointment.
    /// </summary>
    public virtual async Task<Dictionary<Guid, RescheduleChainDto>> ResolveAsync(
        IReadOnlyCollection<Appointment> appointments,
        CancellationToken cancellationToken = default)
    {
        var chains = new Dictionary<Guid, RescheduleChainDto>();
        if (appointments == null || appointments.Count == 0)
        {
            return chains;
        }

        var replacements = appointments
            .Where(a => a.RescheduledFromAppointmentId.HasValue)
            .ToList();
        if (replacements.Count == 0)
        {
            // The common case. No chain means no queries at all -- a normally booked appointment
            // must not pay for this feature.
            return chains;
        }

        var sourceIds = replacements
            .Select(a => a.RescheduledFromAppointmentId!.Value)
            .Distinct()
            .ToList();

        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var sourceRows = await _asyncExecuter.ToListAsync(
            appointmentQuery
                .Where(a => sourceIds.Contains(a.Id))
                .Select(a => new { a.Id, a.RequestConfirmationNumber }),
            cancellationToken);
        var confirmationBySourceId = sourceRows.ToDictionary(r => r.Id, r => r.RequestConfirmationNumber);

        // The request stays on the OLD appointment (decision 5), so it is found by the SOURCE id.
        // Accepted only: a rejected or still-pending request against the same appointment did not
        // produce this replacement and its timestamps would describe a different decision.
        var changeRequestQuery = await _changeRequestRepository.GetQueryableAsync();
        var changeRequestRows = await _asyncExecuter.ToListAsync(
            changeRequestQuery
                .Where(c => sourceIds.Contains(c.AppointmentId)
                    && c.ChangeRequestType == ChangeRequestType.Reschedule
                    && c.RequestStatus == RequestStatusType.Accepted)
                .Select(c => new { c.Id, c.AppointmentId, c.DecidedAt }),
            cancellationToken);
        var requestBySourceId = changeRequestRows
            .GroupBy(r => r.AppointmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.DecidedAt).First());

        // Consent lives on the CURRENT round, not the request: 4c re-asks on every new date, and a
        // superseded round holds agreement to a date nobody was booked onto.
        var requestIds = requestBySourceId.Values.Select(r => r.Id).ToList();
        var roundQuery = await _consentRoundRepository.GetQueryableAsync();
        var roundRows = await _asyncExecuter.ToListAsync(
            roundQuery
                .Where(r => requestIds.Contains(r.AppointmentChangeRequestId) && r.SupersededAt == null)
                .Select(r => new
                {
                    r.AppointmentChangeRequestId,
                    r.RoundNumber,
                    r.SideAConsentRespondedAt,
                    r.SideBConsentRespondedAt,
                }),
            cancellationToken);
        var roundByRequestId = roundRows
            .GroupBy(r => r.AppointmentChangeRequestId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RoundNumber).First());

        foreach (var replacement in replacements)
        {
            var sourceId = replacement.RescheduledFromAppointmentId!.Value;
            var chain = new RescheduleChainDto { SourceAppointmentId = sourceId };

            if (confirmationBySourceId.TryGetValue(sourceId, out var confirmationNumber))
            {
                chain.SourceRequestConfirmationNumber = confirmationNumber;
            }

            if (requestBySourceId.TryGetValue(sourceId, out var request))
            {
                chain.DecidedAt = request.DecidedAt;

                if (roundByRequestId.TryGetValue(request.Id, out var round))
                {
                    chain.SideAAgreedAt = round.SideAConsentRespondedAt;
                    chain.SideBAgreedAt = round.SideBConsentRespondedAt;
                }
            }

            chains[replacement.Id] = chain;
        }

        return chains;
    }

    /// <summary>
    /// Single-appointment convenience for the detail reads. Returns null when the appointment was
    /// booked normally.
    /// </summary>
    public virtual async Task<RescheduleChainDto?> ResolveOneAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        var chains = await ResolveAsync(new[] { appointment }, cancellationToken);
        return chains.TryGetValue(appointment.Id, out var chain) ? chain : null;
    }
}
