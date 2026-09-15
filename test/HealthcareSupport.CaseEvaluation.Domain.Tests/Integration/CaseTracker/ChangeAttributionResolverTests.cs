using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Phase 6 T7 (2026-08-08) -- change attribution on the outbound payload.
///
/// <para>Two properties carry the design. The LATEST request wins, because the payload describes the
/// appointment as it stands and an appointment can accumulate several requests over its life. And
/// the finalized timestamp comes from the DECISION stamp, never a last-modified column -- the whole
/// reason that column exists.</para>
/// </summary>
public class ChangeAttributionResolverTests
{
    private static readonly Guid AppointmentId = new("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");

    private static AppointmentChangeRequest Request(
        ChangeRequestType type,
        DateTime creationTime,
        ChangeRequestSide? side = null,
        DateTime? decidedAt = null)
    {
        var request = new AppointmentChangeRequest(
            id: Guid.NewGuid(),
            tenantId: null,
            appointmentId: AppointmentId,
            changeRequestType: type,
            cancellationReason: type == ChangeRequestType.Cancel ? "patient unavailable" : null,
            reScheduleReason: type == ChangeRequestType.Reschedule ? "scheduling conflict" : null,
            newDoctorAvailabilityId: null);

        if (side is { } value)
        {
            request.InitiateConsent(value, Guid.NewGuid());
        }

        if (decidedAt is { } decided)
        {
            request.MarkDecided(RequestStatusType.Accepted, Guid.NewGuid(), decided);
        }

        // CreationTime has a protected setter on the audited base; reflect for in-memory fixtures.
        typeof(AppointmentChangeRequest)
            .GetProperty(nameof(AppointmentChangeRequest.CreationTime))!
            .SetValue(request, creationTime);

        return request;
    }

    private static ChangeAttributionResolver Build(params AppointmentChangeRequest[] requests)
    {
        var repo = Substitute.For<IRepository<AppointmentChangeRequest, Guid>>();
        repo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentChangeRequest, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<AppointmentChangeRequest>(requests)));
        return new ChangeAttributionResolver(repo);
    }

    [Fact]
    public async Task WithNoChangeRequest_EveryFieldIsNull()
    {
        // Absence means "nothing was requested", not "the lookup failed".
        var section = await Build().ResolveAsync(AppointmentId);

        section.RequestedBySide.ShouldBeNull();
        section.ChangeRequestType.ShouldBeNull();
        section.RequestedAtUtc.ShouldBeNull();
        section.FinalizedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task TheMostRecentlySubmittedRequestWins()
    {
        var older = Request(
            ChangeRequestType.Reschedule,
            new DateTime(2027, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            ChangeRequestSide.SideA);
        var newer = Request(
            ChangeRequestType.Cancel,
            new DateTime(2027, 2, 2, 8, 0, 0, DateTimeKind.Utc),
            ChangeRequestSide.SideB);

        // Deliberately out of order, so the resolver cannot pass by taking the first element.
        var section = await Build(older, newer).ResolveAsync(AppointmentId);

        section.ChangeRequestType.ShouldBe(ChangeRequestTypeWire.Cancel);
        section.RequestedBySide.ShouldBe(ChangeRequestSideWire.SideB);
    }

    [Fact]
    public async Task APendingRequestHasNoFinalizedTimestamp()
    {
        // Pending is a real state, not missing data.
        var pending = Request(
            ChangeRequestType.Cancel,
            new DateTime(2027, 3, 3, 8, 0, 0, DateTimeKind.Utc),
            ChangeRequestSide.SideA);

        var section = await Build(pending).ResolveAsync(AppointmentId);

        section.RequestedAtUtc.ShouldNotBeNull();
        section.FinalizedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task ADecidedRequestReportsItsDecisionTime_NotItsCreationTime()
    {
        var created = new DateTime(2027, 4, 4, 8, 0, 0, DateTimeKind.Utc);
        var decided = new DateTime(2027, 4, 6, 15, 30, 0, DateTimeKind.Utc);
        var request = Request(ChangeRequestType.Reschedule, created, ChangeRequestSide.SideA, decided);

        var section = await Build(request).ResolveAsync(AppointmentId);

        section.FinalizedAtUtc.ShouldBe(IntegrationTimestamp.ToIsoUtc(decided));
        section.FinalizedAtUtc.ShouldNotBe(section.RequestedAtUtc);
    }

    [Fact]
    public async Task AStaffInitiatedRequestReportsNoSide()
    {
        // No PARTY asked, so attributing it to one would be wrong.
        var staffInitiated = Request(
            ChangeRequestType.Reschedule,
            new DateTime(2027, 5, 5, 8, 0, 0, DateTimeKind.Utc));

        var section = await Build(staffInitiated).ResolveAsync(AppointmentId);

        section.RequestedBySide.ShouldBeNull();
        section.ChangeRequestType.ShouldBe(ChangeRequestTypeWire.Reschedule);
    }
}
