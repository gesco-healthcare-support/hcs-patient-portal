using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the inbound attendance report (phase 5, 2026-08-07).
///
/// <para>The load-bearing property is the THREE-way result. The reconcile read collapses every
/// failure to null so an anonymous caller cannot probe which offices exist; copying that shape here
/// would swallow the state machine's invalid-transition exception and report a conflict as a 404.
/// Several tests below exist purely to pin that distinction.</para>
///
/// <para>All fixture data is synthetic.</para>
/// </summary>
public class CaseTrackerAttendanceServiceTests
{
    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");

    private sealed class Harness
    {
        public CaseTrackerAttendanceService Service { get; init; } = null!;
        public AppointmentManager Manager { get; init; } = null!;
        public IAppointmentRepository Repository { get; init; } = null!;
        public ICurrentTenant CurrentTenant { get; init; } = null!;
        public IDisposable TenantScope { get; init; } = null!;
    }

    private static Appointment AppointmentWith(AppointmentStatusType status)
    {
        var appointment = new Appointment(
            id: AppointmentId,
            patientId: new Guid("c0f1e2d3-a4b5-4c6d-8e9f-a0b1c2d3e4f5"),
            identityUserId: null,
            appointmentTypeId: new Guid("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5f6"),
            locationId: new Guid("e2f3a4b5-c6d7-4e8f-9a0b-b2c3d4e5f6a7"),
            doctorAvailabilityId: new Guid("f3a4b5c6-d7e8-4f90-ab1c-c3d4e5f6a7b8"),
            appointmentDate: new DateTime(2027, 3, 4, 9, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00065",
            appointmentStatus: status);
        return appointment;
    }

    private static Harness Build(
        AppointmentStatusType currentStatus = AppointmentStatusType.Approved,
        bool enabled = true,
        bool appointmentExists = true,
        Exception? repositoryThrows = null,
        Exception? managerThrows = null)
    {
        var repository = Substitute.For<IAppointmentRepository>();
        if (repositoryThrows != null)
        {
            repository.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Throws(repositoryThrows);
        }
        else
        {
            repository.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(appointmentExists ? AppointmentWith(currentStatus) : null));
        }

        // AppointmentManager is a concrete domain service whose constructor only assigns, so it
        // substitutes cleanly; MarkAttendanceOutcomeAsync is virtual.
        var manager = Substitute.For<AppointmentManager>(
            repository,
            Substitute.For<ILocalEventBus>(),
            Substitute.For<IAppointmentInjuryDetailRepository>(),
            Substitute.For<IRepository<AppointmentClaimExaminer, Guid>>(),
            Substitute.For<IRepository<AppointmentDocument, Guid>>());

        if (managerThrows != null)
        {
            manager.MarkAttendanceOutcomeAsync(
                    Arg.Any<Guid>(), Arg.Any<AppointmentStatusType>(), Arg.Any<string?>(), Arg.Any<Guid?>())
                .Throws(managerThrows);
        }
        else
        {
            manager.MarkAttendanceOutcomeAsync(
                    Arg.Any<Guid>(), Arg.Any<AppointmentStatusType>(), Arg.Any<string?>(), Arg.Any<Guid?>())
                .Returns(callInfo => Task.FromResult(
                    AppointmentWith(callInfo.ArgAt<AppointmentStatusType>(1))));
        }

        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled)
            .Returns(Task.FromResult<string?>(enabled ? "true" : "false"));

        var scope = Substitute.For<IDisposable>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        return new Harness
        {
            Service = new CaseTrackerAttendanceService(
                repository,
                manager,
                settingProvider,
                currentTenant,
                NullLogger<CaseTrackerAttendanceService>.Instance),
            Manager = manager,
            Repository = repository,
            CurrentTenant = currentTenant,
            TenantScope = scope,
        };
    }

    private static BusinessException InvalidTransition() =>
        new(CaseEvaluationDomainErrorCodes.AppointmentInvalidTransition);

    // ---- the conflict arm: the reason this service does not return a nullable ----

    [Fact]
    public async Task WhenTheStateMachineRefusesTheTransition_TheAnswerIsConflictNotNotFound()
    {
        // THE test this design exists for. Widen the service's catch to the reconcile read's
        // catch-all and this becomes NotFound -- silently telling the Case Tracker the appointment
        // does not exist when it does.
        var h = Build(currentStatus: AppointmentStatusType.Pending, managerThrows: InvalidTransition());

        var result = await h.Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NoShow);

        result.ShouldBe(CaseTrackerAttendanceResult.Conflict);
    }

    [Fact]
    public async Task WhenTheAppointmentAlreadyHoldsTheOtherOutcome_TheAnswerIsConflict()
    {
        var h = Build(currentStatus: AppointmentStatusType.NoShow, managerThrows: InvalidTransition());

        var result = await h.Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NotSeen);

        result.ShouldBe(CaseTrackerAttendanceResult.Conflict);
    }

    [Fact]
    public async Task AConflictIsDistinguishableFromANotFound()
    {
        var conflict = await Build(managerThrows: InvalidTransition())
            .Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NoShow);
        var missing = await Build(appointmentExists: false)
            .Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NoShow);

        conflict.ShouldBe(CaseTrackerAttendanceResult.Conflict);
        missing.ShouldBe(CaseTrackerAttendanceResult.NotFound);
        conflict.ShouldNotBe(missing);
    }

    // ---- idempotency ----

    [Theory]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.NotSeen)]
    public async Task ARetryCarryingTheSameOutcome_SucceedsWithoutASecondStatusChange(
        AppointmentStatusType outcome)
    {
        // A second status change would publish a second event and so send a duplicate staff email.
        var h = Build(currentStatus: outcome);

        var result = await h.Service.ApplyAsync(TenantId, AppointmentId, outcome);

        result.ShouldBe(CaseTrackerAttendanceResult.Applied);
        await h.Manager.DidNotReceiveWithAnyArgs()
            .MarkAttendanceOutcomeAsync(default, default, default, default);
    }

    // ---- the happy path ----

    [Theory]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.NotSeen)]
    public async Task FromApproved_TheOutcomeIsApplied(AppointmentStatusType outcome)
    {
        var h = Build();

        var result = await h.Service.ApplyAsync(TenantId, AppointmentId, outcome);

        result.ShouldBe(CaseTrackerAttendanceResult.Applied);
        await h.Manager.Received(1).MarkAttendanceOutcomeAsync(AppointmentId, outcome, null, null);
    }

    // ---- the office gate, mirroring the reconcile read ----

    [Fact]
    public async Task TheOfficeScopeIsEntered_BeforeAnyLookup()
    {
        var h = Build();

        await h.Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NoShow);

        h.CurrentTenant.Received(1).Change(TenantId, Arg.Any<string?>());
    }

    [Fact]
    public async Task TheOfficeScopeIsAlwaysDisposed_EvenWhenTheLookupFails()
    {
        // Leaking the scope would let this office's identity bleed into the rest of the request.
        var h = Build(repositoryThrows: new InvalidOperationException("office database unreachable"));

        await h.Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NoShow);

        h.TenantScope.Received(1).Dispose();
    }

    [Fact]
    public async Task WhenTheOfficeHasTheIntegrationDisabled_NothingIsAppliedAndNothingIsLookedUp()
    {
        var h = Build(enabled: false);

        var result = await h.Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NoShow);

        result.ShouldBe(CaseTrackerAttendanceResult.NotFound);
        // The id overload, NOT the predicate one: the predicate FindAsync is an EXTENSION method,
        // so NSubstitute cannot intercept it and the arrangement would silently do nothing.
        await h.Repository.DidNotReceive().FindAsync(
            AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await h.Manager.DidNotReceiveWithAnyArgs()
            .MarkAttendanceOutcomeAsync(default, default, default, default);
    }

    [Fact]
    public async Task WhenTheOfficeIsUnknownOrBroken_NotFoundIsReturnedRatherThanThrowing()
    {
        // An unrecognised tenant id fails when its connection string is resolved. That must not
        // become a 500, because a 500 tells the caller it guessed a real office.
        var h = Build(repositoryThrows: new InvalidOperationException("no connection string for tenant"));

        var result = CaseTrackerAttendanceResult.Applied;
        await Should.NotThrowAsync(async () =>
            result = await h.Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NoShow));

        result.ShouldBe(CaseTrackerAttendanceResult.NotFound);
    }

    [Fact]
    public async Task DisabledOfficeAndUnknownAppointment_AreIndistinguishable()
    {
        var disabled = await Build(enabled: false)
            .Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NoShow);
        var unknown = await Build(appointmentExists: false)
            .Service.ApplyAsync(TenantId, AppointmentId, AppointmentStatusType.NoShow);

        disabled.ShouldBe(CaseTrackerAttendanceResult.NotFound);
        unknown.ShouldBe(CaseTrackerAttendanceResult.NotFound);
        disabled.ShouldBe(unknown);
    }
}
