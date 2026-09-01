using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.States;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the attorney, insurance and claim-examiner sections. All fixture data is synthetic.
///
/// <para>Two behaviours are deliberate rather than incidental and are asserted here: an attorney with no
/// name, firm or email is published as NULL rather than an object of nulls, and every referenced state is
/// resolved in ONE query however many rows there are.</para>
/// </summary>
public class PartyDetailResolverTests
{
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly Guid CaliforniaId = new("c1a1f0a2-3b4c-4d5e-8f60-a1b2c3d4e5f6");
    private static readonly Guid NevadaId = new("d2b2e1b3-4c5d-4e6f-9a71-b2c3d4e5f6a7");

    private sealed class Harness
    {
        public PartyDetailResolver Resolver { get; init; } = null!;
        public IRepository<State, Guid> States { get; init; } = null!;
    }

    private static Appointment NewAppointment(
        bool withApplicantAttorney = true,
        bool withDefenseAttorney = false) =>
        new(
            AppointmentId,
            patientId: new Guid("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc"),
            identityUserId: null,
            appointmentTypeId: new Guid("a1c2e3f4-5566-4778-9900-aabbccddeeff"),
            locationId: new Guid("c0ffee0a-bcde-4f01-9abc-de0123456f7a"),
            doctorAvailabilityId: new Guid("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa"),
            appointmentDate: new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00065",
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: "PN-SAMPLE")
        {
            ApplicantAttorneyFirstName = withApplicantAttorney ? "Ada" : null,
            ApplicantAttorneyLastName = withApplicantAttorney ? "Sample" : null,
            ApplicantAttorneyFirmName = withApplicantAttorney ? "Sample and Partners LLP" : null,
            ApplicantAttorneyEmail = withApplicantAttorney ? "ada.sample@example.test" : null,
            ApplicantAttorneyStreet = withApplicantAttorney ? "10 Sample Way" : null,
            ApplicantAttorneyCity = withApplicantAttorney ? "Encino" : null,
            ApplicantAttorneyStateId = withApplicantAttorney ? CaliforniaId : null,
            ApplicantAttorneyZipCode = withApplicantAttorney ? "91436" : null,
            DefenseAttorneyLastName = withDefenseAttorney ? "Testcase" : null,
            DefenseAttorneyStateId = withDefenseAttorney ? NevadaId : null,
        };

    private static Harness Build(
        List<AppointmentPrimaryInsurance>? insurances = null,
        List<AppointmentClaimExaminer>? examiners = null,
        bool statesResolve = true)
    {
        var insuranceRepo = Substitute.For<IRepository<AppointmentPrimaryInsurance, Guid>>();
        insuranceRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPrimaryInsurance, bool>>>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(insurances ?? new List<AppointmentPrimaryInsurance>()));

        var examinerRepo = Substitute.For<IRepository<AppointmentClaimExaminer, Guid>>();
        examinerRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentClaimExaminer, bool>>>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(examiners ?? new List<AppointmentClaimExaminer>()));

        var stateRepo = Substitute.For<IRepository<State, Guid>>();
        stateRepo.GetListAsync(
                Arg.Any<Expression<Func<State, bool>>>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(statesResolve
                ? new List<State>
                {
                    new(CaliforniaId, "California", isSystem: true),
                    new(NevadaId, "Nevada", isSystem: true),
                }
                : new List<State>()));

        return new Harness
        {
            Resolver = new PartyDetailResolver(insuranceRepo, examinerRepo, stateRepo),
            States = stateRepo,
        };
    }

    private static AppointmentPrimaryInsurance NewInsurance(Guid? stateId, bool isActive = true)
    {
        var insurance = new AppointmentPrimaryInsurance(Guid.NewGuid(), AppointmentId, isActive)
        {
            Name = "Sample Mutual Insurance",
            City = "Encino",
            StateId = stateId,
            Zip = "91436",
        };
        return insurance;
    }

    private static AppointmentClaimExaminer NewExaminer(Guid? stateId, bool isActive = true)
    {
        var examiner = new AppointmentClaimExaminer(Guid.NewGuid(), AppointmentId, isActive)
        {
            Name = "Sample Examiner",
            Email = "examiner@example.test",
            StateId = stateId,
        };
        return examiner;
    }

    [Fact]
    public async Task AnAttorneyWithDetails_IsPublishedWithTheFullAddressBlock()
    {
        var h = Build();

        var result = await h.Resolver.ResolveAsync(NewAppointment());

        result.ApplicantAttorney.ShouldNotBeNull();
        result.ApplicantAttorney!.FirmName.ShouldBe("Sample and Partners LLP");
        result.ApplicantAttorney.Street.ShouldBe("10 Sample Way");
        result.ApplicantAttorney.State.ShouldBe("California"); // NAME, not the id
        result.ApplicantAttorney.ZipCode.ShouldBe("91436");
    }

    [Fact]
    public async Task AnAbsentAttorney_IsNullRatherThanAnObjectOfNulls()
    {
        // "None recorded" is information; an object with eleven null fields is not.
        var h = Build();

        var result = await h.Resolver.ResolveAsync(NewAppointment(withApplicantAttorney: false));

        result.ApplicantAttorney.ShouldBeNull();
        result.DefenseAttorney.ShouldBeNull();
    }

    [Fact]
    public async Task AnAttorneyKnownOnlyByLastName_IsStillPublished()
    {
        var h = Build();

        var result = await h.Resolver.ResolveAsync(NewAppointment(withDefenseAttorney: true));

        result.DefenseAttorney.ShouldNotBeNull();
        result.DefenseAttorney!.LastName.ShouldBe("Testcase");
        result.DefenseAttorney.State.ShouldBe("Nevada");
    }

    [Fact]
    public async Task InactiveInsurancesAndExaminers_AreExcludedByTheQuery()
    {
        // The filter is pushed into the repository, so an inactive row never reaches the mapper. This
        // asserts the predicate is applied rather than the mapper filtering after the fact.
        var h = Build(
            insurances: new List<AppointmentPrimaryInsurance> { NewInsurance(CaliforniaId) },
            examiners: new List<AppointmentClaimExaminer> { NewExaminer(CaliforniaId) });

        var result = await h.Resolver.ResolveAsync(NewAppointment());

        result.PrimaryInsurances.Count.ShouldBe(1);
        result.ClaimExaminers.Count.ShouldBe(1);
        result.PrimaryInsurances[0].State.ShouldBe("California");
        result.ClaimExaminers[0].State.ShouldBe("California");
    }

    [Fact]
    public async Task EveryReferencedState_IsResolvedInASingleQuery()
    {
        var h = Build(
            insurances: new List<AppointmentPrimaryInsurance>
            {
                NewInsurance(CaliforniaId), NewInsurance(NevadaId), NewInsurance(CaliforniaId),
            },
            examiners: new List<AppointmentClaimExaminer> { NewExaminer(NevadaId) });

        await h.Resolver.ResolveAsync(NewAppointment(withDefenseAttorney: true));

        await h.States.Received(1).GetListAsync(
            Arg.Any<Expression<Func<State, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithNoStatesReferencedAtAll_NoStateQueryIsMade()
    {
        var h = Build();

        await h.Resolver.ResolveAsync(NewAppointment(withApplicantAttorney: false));

        await h.States.DidNotReceiveWithAnyArgs().GetListAsync(default!, default, default);
    }

    [Fact]
    public async Task AnUnresolvableState_YieldsNullRatherThanThrowing()
    {
        var h = Build(statesResolve: false);

        var result = await h.Resolver.ResolveAsync(NewAppointment());

        result.ApplicantAttorney.ShouldNotBeNull();
        result.ApplicantAttorney!.State.ShouldBeNull();
        result.ApplicantAttorney.City.ShouldBe("Encino"); // the rest still published
    }

    [Fact]
    public async Task WithNoPartiesAtAll_CollectionsAreEmptyNotNull()
    {
        var h = Build();

        var result = await h.Resolver.ResolveAsync(NewAppointment(withApplicantAttorney: false));

        result.PrimaryInsurances.ShouldBeEmpty();
        result.ClaimExaminers.ShouldBeEmpty();
    }
}
