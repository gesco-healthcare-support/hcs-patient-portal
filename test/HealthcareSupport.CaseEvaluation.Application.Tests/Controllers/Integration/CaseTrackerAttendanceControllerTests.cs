using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// Unit tests for the inbound attendance endpoint's HTTP contract, exercised through the REAL token
/// validator and attendance service over substituted repositories. Wiring them for real is the point:
/// the valuable assertions are that a bad token is rejected BEFORE any office is opened, and that a
/// conflict is not flattened into a 404.
///
/// <para>No web host is needed -- the controller is driven directly with a fake
/// <see cref="HttpContext"/>. That also means the MVC filter pipeline does NOT run here, so these
/// tests cannot settle whether the POST needs <c>[IgnoreAntiforgeryToken]</c>; the live gate does.
/// </para>
///
/// <para>All fixture data is synthetic and the token is an arbitrary string.</para>
/// </summary>
public class CaseTrackerAttendanceControllerTests
{
    private const string Token = "sample-integration-token-value";

    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");

    private sealed class Harness
    {
        public CaseTrackerAttendanceController Controller { get; init; } = null!;
        public IAppointmentRepository Repository { get; init; } = null!;
        public AppointmentManager Manager { get; init; } = null!;
    }

    private static Appointment ApprovedAppointment(AppointmentStatusType status) =>
        new(
            id: AppointmentId,
            patientId: new Guid("c0f1e2d3-a4b5-4c6d-8e9f-a0b1c2d3e4f5"),
            identityUserId: null,
            appointmentTypeId: new Guid("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5f6"),
            locationId: new Guid("e2f3a4b5-c6d7-4e8f-9a0b-b2c3d4e5f6a7"),
            doctorAvailabilityId: new Guid("f3a4b5c6-d7e8-4f90-ab1c-c3d4e5f6a7b8"),
            appointmentDate: new DateTime(2027, 3, 4, 9, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00065",
            appointmentStatus: status);

    private static Harness Build(
        string? presentedToken,
        bool officeEnabled = true,
        bool appointmentExists = true,
        AppointmentStatusType currentStatus = AppointmentStatusType.Approved,
        bool transitionRefused = false)
    {
        var repository = Substitute.For<IAppointmentRepository>();
        repository.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(appointmentExists ? ApprovedAppointment(currentStatus) : null));

        var manager = Substitute.For<AppointmentManager>(
            repository,
            Substitute.For<ILocalEventBus>(),
            Substitute.For<IAppointmentInjuryDetailRepository>(),
            Substitute.For<IRepository<AppointmentClaimExaminer, Guid>>(),
            Substitute.For<IRepository<AppointmentDocument, Guid>>());

        if (transitionRefused)
        {
            manager.MarkAttendanceOutcomeAsync(
                    Arg.Any<Guid>(), Arg.Any<AppointmentStatusType>(), Arg.Any<string?>(), Arg.Any<Guid?>())
                .Throws(new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentInvalidTransition));
        }
        else
        {
            manager.MarkAttendanceOutcomeAsync(
                    Arg.Any<Guid>(), Arg.Any<AppointmentStatusType>(), Arg.Any<string?>(), Arg.Any<Guid?>())
                .Returns(Task.FromResult(ApprovedAppointment(AppointmentStatusType.NoShow)));
        }

        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled)
            .Returns(Task.FromResult<string?>(officeEnabled ? "true" : "false"));

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CaseTrackerIntegrationConsts.TokenConfigurationKey] = Token,
            })
            .Build();

        var controller = new CaseTrackerAttendanceController(
            new IntegrationTokenValidator(configuration),
            new CaseTrackerAttendanceService(
                repository,
                manager,
                settingProvider,
                currentTenant,
                NullLogger<CaseTrackerAttendanceService>.Instance));

        var httpContext = new DefaultHttpContext();
        if (presentedToken != null)
        {
            httpContext.Request.Headers[CaseTrackerIntegrationConsts.IntegrationTokenHeaderName] = presentedToken;
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return new Harness { Controller = controller, Repository = repository, Manager = manager };
    }

    private static Task<IActionResult> ActAsync(Harness h, string? outcome = AttendanceOutcomeWire.NoShow) =>
        h.Controller.RecordAttendanceAsync(
            TenantId,
            AppointmentId,
            new CaseTrackerAttendanceRequest { Outcome = outcome },
            CancellationToken.None);

    [Theory]
    [InlineData(AttendanceOutcomeWire.NoShow)]
    [InlineData(AttendanceOutcomeWire.NotSeen)]
    public async Task WithTheCorrectToken_TheOutcomeIsApplied(string outcome)
    {
        var h = Build(Token);

        var result = await ActAsync(h, outcome);

        result.ShouldBeOfType<OkResult>();
    }

    [Theory]
    [InlineData(null)]          // header absent
    [InlineData("")]            // header present but empty
    [InlineData("wrong-token")]
    [InlineData("Sample-integration-token-value")] // differs only by case
    public async Task WithoutTheCorrectToken_TheRequestIsUnauthorized(string? presented)
    {
        var h = Build(presented);

        var result = await ActAsync(h);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AnUnauthorizedRequest_NeverTouchesAnOfficeDatabase()
    {
        // The token check must gate the DB work, not merely the response.
        var h = Build("wrong-token");

        await ActAsync(h);

        await h.Repository.DidNotReceive().FindAsync(
            AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("APPROVED")]
    [InlineData("NoShow")]
    [InlineData("no_show")]
    public async Task WithAnUnrecognisedOutcome_TheAnswerIsBadRequest(string? outcome)
    {
        var h = Build(Token);

        var result = await ActAsync(h, outcome);

        result.ShouldBeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task AnUnrecognisedOutcome_NeverTouchesAnOfficeDatabase()
    {
        // A malformed body must not cost a tenant switch or a lookup.
        var h = Build(Token);

        await ActAsync(h, "APPROVED");

        await h.Repository.DidNotReceive().FindAsync(
            AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenTheStateMachineRefuses_TheAnswerIsConflictRatherThanNotFound()
    {
        // The distinction the three-way service result exists to preserve. If this ever reports 404,
        // the Case Tracker is told the appointment does not exist when it plainly does.
        var h = Build(Token, currentStatus: AppointmentStatusType.Pending, transitionRefused: true);

        var result = await ActAsync(h);

        // ConflictObjectResult, not ConflictResult: since 2026-08-18 the 409 carries the status and
        // the retry verdict, agreed with the Case Tracker so they stop retrying conflicts that can
        // never succeed. Asserting the BODY rather than just the status code is the point -- this is
        // the only test that drives the flag through the controller, and Pending is a case where
        // retrying genuinely does succeed once our staff approve.
        var conflict = result.ShouldBeOfType<ConflictObjectResult>();
        var body = conflict.Value.ShouldBeOfType<CaseTrackerAttendanceConflictResponse>();
        body.Status.ShouldBe(nameof(AppointmentStatusType.Pending));
        body.Retryable.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenTheConflictIsPermanent_TheFlagSaysSo()
    {
        // The other half of the flag, and the half that saves them the wasted retries: a checked-out
        // appointment was attended, so no amount of calling again will let it take a no-show.
        var h = Build(Token, currentStatus: AppointmentStatusType.CheckedOut, transitionRefused: true);

        var result = await ActAsync(h);

        var conflict = result.ShouldBeOfType<ConflictObjectResult>();
        var body = conflict.Value.ShouldBeOfType<CaseTrackerAttendanceConflictResponse>();
        body.Status.ShouldBe(nameof(AppointmentStatusType.CheckedOut));
        body.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenTheAppointmentAlreadyCarriesTheOutcome_TheRetryIs200AndChangesNothing()
    {
        var h = Build(Token, currentStatus: AppointmentStatusType.NoShow);

        var result = await ActAsync(h, AttendanceOutcomeWire.NoShow);

        result.ShouldBeOfType<OkResult>();
        await h.Manager.DidNotReceiveWithAnyArgs()
            .MarkAttendanceOutcomeAsync(default, default, default, default);
    }

    [Fact]
    public async Task WhenTheOfficeHasTheIntegrationDisabled_TheAnswerIsNotFound()
    {
        var h = Build(Token, officeEnabled: false);

        var result = await ActAsync(h);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task WhenTheAppointmentIsUnknown_TheAnswerIsNotFound()
    {
        var h = Build(Token, appointmentExists: false);

        var result = await ActAsync(h);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DisabledOfficeAndUnknownAppointment_AreIndistinguishable()
    {
        // A holder of the token must not be able to tell whether an appointment or office exists.
        var disabled = await ActAsync(Build(Token, officeEnabled: false));
        var unknown = await ActAsync(Build(Token, appointmentExists: false));

        disabled.ShouldBeOfType<NotFoundResult>();
        unknown.ShouldBeOfType<NotFoundResult>();
        disabled.ShouldBeOfType(unknown.GetType());
    }
}
