using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- pure unit tests for the
/// <see cref="ChangeRequestListFilter"/> queryable filter helper.
/// Tests run against an in-memory <see cref="IQueryable{T}"/> so the
/// same expression composes correctly when EF Core handles it
/// against SQL.
/// </summary>
public class ChangeRequestListFilterUnitTests
{
    [Fact]
    public void Apply_NoFilters_ReturnsAll()
    {
        var rows = BuildSampleRows();
        var result = ChangeRequestListFilter.Apply(
            source: rows.AsQueryable(),
            requestStatus: null,
            changeRequestType: null,
            createdFromUtc: null,
            createdToUtc: null).ToList();

        result.Count.ShouldBe(rows.Count);
    }

    [Fact]
    public void Apply_FilterByPending_ReturnsOnlyPending()
    {
        var rows = BuildSampleRows();
        var result = ChangeRequestListFilter.Apply(
            rows.AsQueryable(),
            requestStatus: RequestStatusType.Pending,
            changeRequestType: null,
            createdFromUtc: null,
            createdToUtc: null).ToList();

        result.ShouldAllBe(r => r.RequestStatus == RequestStatusType.Pending);
    }

    [Fact]
    public void Apply_FilterByCancelType_ReturnsOnlyCancelRequests()
    {
        var rows = BuildSampleRows();
        var result = ChangeRequestListFilter.Apply(
            rows.AsQueryable(),
            requestStatus: null,
            changeRequestType: ChangeRequestType.Cancel,
            createdFromUtc: null,
            createdToUtc: null).ToList();

        result.ShouldAllBe(r => r.ChangeRequestType == ChangeRequestType.Cancel);
    }

    [Fact]
    public void Apply_FilterByRescheduleType_ReturnsOnlyRescheduleRequests()
    {
        var rows = BuildSampleRows();
        var result = ChangeRequestListFilter.Apply(
            rows.AsQueryable(),
            requestStatus: null,
            changeRequestType: ChangeRequestType.Reschedule,
            createdFromUtc: null,
            createdToUtc: null).ToList();

        result.ShouldAllBe(r => r.ChangeRequestType == ChangeRequestType.Reschedule);
    }

    [Fact]
    public void Apply_FilterByCreatedFrom_ExcludesEarlierRows()
    {
        var rows = BuildSampleRows();
        var result = ChangeRequestListFilter.Apply(
            rows.AsQueryable(),
            requestStatus: null,
            changeRequestType: null,
            createdFromUtc: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            createdToUtc: null).ToList();

        result.ShouldAllBe(r => r.CreationTime >= new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Apply_FilterByCreatedTo_ExcludesLaterRows()
    {
        var rows = BuildSampleRows();
        var result = ChangeRequestListFilter.Apply(
            rows.AsQueryable(),
            requestStatus: null,
            changeRequestType: null,
            createdFromUtc: null,
            createdToUtc: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).ToList();

        result.ShouldAllBe(r => r.CreationTime <= new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Apply_CombinedFilters_AllApplied()
    {
        var rows = BuildSampleRows();
        var result = ChangeRequestListFilter.Apply(
            rows.AsQueryable(),
            requestStatus: RequestStatusType.Pending,
            changeRequestType: ChangeRequestType.Reschedule,
            createdFromUtc: null,
            createdToUtc: null).ToList();

        result.ShouldAllBe(r =>
            r.RequestStatus == RequestStatusType.Pending &&
            r.ChangeRequestType == ChangeRequestType.Reschedule);
    }

    [Fact]
    public void Apply_NullSource_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            ChangeRequestListFilter.Apply(
                source: null!,
                requestStatus: null,
                changeRequestType: null,
                createdFromUtc: null,
                createdToUtc: null));
    }

    private static List<AppointmentChangeRequest> BuildSampleRows()
    {
        var pendingCancel = NewRequest(ChangeRequestType.Cancel, RequestStatusType.Pending,
            new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc));
        var acceptedReschedule = NewRequest(ChangeRequestType.Reschedule, RequestStatusType.Accepted,
            new DateTime(2026, 4, 28, 10, 0, 0, DateTimeKind.Utc));
        var rejectedCancel = NewRequest(ChangeRequestType.Cancel, RequestStatusType.Rejected,
            new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc));
        var pendingReschedule = NewRequest(ChangeRequestType.Reschedule, RequestStatusType.Pending,
            new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc));

        return new List<AppointmentChangeRequest>
        {
            pendingCancel,
            acceptedReschedule,
            rejectedCancel,
            pendingReschedule,
        };
    }

    private static AppointmentChangeRequest NewRequest(
        ChangeRequestType type,
        RequestStatusType status,
        DateTime creationTime)
    {
        var request = new AppointmentChangeRequest(
            id: Guid.NewGuid(),
            tenantId: null,
            appointmentId: Guid.NewGuid(),
            changeRequestType: type,
            cancellationReason: type == ChangeRequestType.Cancel ? "patient unavailable" : null,
            reScheduleReason: type == ChangeRequestType.Reschedule ? "scheduling conflict" : null,
            newDoctorAvailabilityId: type == ChangeRequestType.Reschedule ? Guid.NewGuid() : null);
        request.RequestStatus = status;

        // FullAuditedAggregateRoot's CreationTime has a protected setter;
        // reflect to set for in-memory test data. The filter only reads
        // CreationTime so this is sufficient.
        var prop = typeof(AppointmentChangeRequest).GetProperty(
            nameof(AppointmentChangeRequest.CreationTime),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.GetSetMethod(nonPublic: true)?.Invoke(request, new object[] { creationTime });

        return request;
    }
}
