using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Resolves WHO asked for the most recent change to an appointment, and when it was asked and
/// settled (phase 6, 2026-08-08).
///
/// <para>Selection rule: the MOST RECENTLY SUBMITTED change request for the appointment, decided or
/// not. An appointment can accumulate several over its life -- a rejected reschedule followed by a
/// cancel, say -- and the payload describes the appointment as it stands now, so the latest request
/// is the one that explains its current state. Older ones are history the receiver already has from
/// earlier pushes.</para>
///
/// <para>Every field is null when the appointment has never had a change request. That is the
/// common case and means "nothing was requested", not "the lookup failed".</para>
/// </summary>
public class ChangeAttributionResolver : ITransientDependency
{
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;

    public ChangeAttributionResolver(IRepository<AppointmentChangeRequest, Guid> changeRequestRepository)
    {
        _changeRequestRepository = changeRequestRepository;
    }

    public virtual async Task<ChangeAttributionSection> ResolveAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        // GetListAsync, not FirstOrDefaultAsync(predicate): the predicate overload is an EXTENSION
        // method, so a substitute cannot intercept it and an arrangement against it silently does
        // nothing. Ordering happens in memory over one appointment's requests, which is a handful.
        var requests = await _changeRequestRepository.GetListAsync(
            r => r.AppointmentId == appointmentId, cancellationToken: cancellationToken);

        var latest = requests
            .OrderByDescending(r => r.CreationTime)
            .FirstOrDefault();

        if (latest == null)
        {
            return new ChangeAttributionSection();
        }

        return new ChangeAttributionSection
        {
            RequestedBySide = ChangeRequestSideWire.ToWireOrNull(latest.RequestingSide),
            ChangeRequestType = ChangeRequestTypeWire.ToWire(latest.ChangeRequestType),
            RequestedAtUtc = IntegrationTimestamp.ToIsoUtc(latest.CreationTime),
            // The decision stamp, never LastModificationTime -- see IntakePayload.ChangeFinalizedAtUtc.
            FinalizedAtUtc = IntegrationTimestamp.ToIsoUtcOrNull(latest.DecidedAt),
        };
    }
}

/// <summary>The four change-attribution values, all null when no change was ever requested.</summary>
public class ChangeAttributionSection
{
    public string? RequestedBySide { get; set; }

    public string? ChangeRequestType { get; set; }

    public string? RequestedAtUtc { get; set; }

    public string? FinalizedAtUtc { get; set; }
}

/// <summary>
/// Maps <see cref="Enums.ChangeRequestType"/> to its wire value. Explicit rather than
/// <c>ToString()</c> for the reason <see cref="EvaluationKindWire"/> gives: a rename must not change
/// the wire format.
/// </summary>
public static class ChangeRequestTypeWire
{
    public const string Cancel = "CANCEL";
    public const string Reschedule = "RESCHEDULE";

    public static string ToWire(ChangeRequestType type) => type switch
    {
        ChangeRequestType.Cancel => Cancel,
        ChangeRequestType.Reschedule => Reschedule,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "No wire value for this change request type."),
    };
}
