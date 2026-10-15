using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The appointment child-resource write gate.
///
/// <para>Seven child services accepted an update by row id, took the parent appointment id from the
/// REQUEST BODY, and never checked either one. Every external role holds the <c>.Edit</c> permission
/// they gate on, so an authenticated external user could rewrite another patient's claim data inside
/// the same office, and could move a child row onto a different appointment.</para>
///
/// <para>The move was not a theoretical gap: <c>AppointmentInjuryDetailManager.UpdateAsync</c> loads
/// the row by id and then assigns <c>entity.AppointmentId = appointmentId</c> from the caller's
/// input. The duplicate check and the injury-date validation above it both scope themselves to the
/// SUPPLIED id, so they validate against the destination appointment and cannot catch it.</para>
///
/// <para>These tests construct the service directly rather than resolving it, so they run in the
/// fast suite. The point is not that the guard works -- that is tested separately below -- but that
/// the SERVICE consults it, and does so BEFORE the manager writes anything.</para>
/// </summary>
public sealed class AppointmentChildOwnershipTests
{
    private static readonly Guid RowId = new("0ff1ce00-0000-4000-8000-00000000c0de");
    private static readonly Guid OwnAppointment = new("0ff1ce00-0000-4000-8000-00000000a001");
    private static readonly Guid SomeoneElsesAppointment = new("0ff1ce00-0000-4000-8000-00000000a002");

    private sealed class Harness
    {
        public AppointmentInjuryDetailsAppService Service { get; init; } = null!;
        public AppointmentChildOwnershipGuard Guard { get; init; } = null!;
        public AppointmentInjuryDetailManager Manager { get; init; } = null!;
    }

    private static Harness Build(Guid rowsActualParent)
    {
        var repository = Substitute.For<IAppointmentInjuryDetailRepository>();
        repository.GetAsync(RowId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new AppointmentInjuryDetail(
                id: RowId,
                appointmentId: rowsActualParent,
                dateOfInjury: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                claimNumber: "a1b2c3d4",
                isCumulativeInjury: false,
                bodyPartsSummary: "e5f6a7b8",
                wcabAdj: "c9d0e1f2"));

        var manager = Substitute.For<AppointmentInjuryDetailManager>(
            repository,
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid>>(),
            Substitute.For<Volo.Abp.Timing.IClock>());

        var readGuard = Substitute.For<AppointmentReadAccessGuard>(
            Substitute.For<IAppointmentRepository>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.AppointmentAccessors.AppointmentAccessor, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>(),
            Substitute.For<Volo.Abp.Linq.IAsyncQueryableExecuter>());

        var guard = Substitute.For<AppointmentChildOwnershipGuard>(readGuard, null!);

        return new Harness
        {
            Service = new AppointmentInjuryDetailsAppService(
                repository,
                manager,
                Substitute.For<IRepository<WcabOffice, Guid>>(),
                guard),
            Guard = guard,
            Manager = manager,
        };
    }

    /// <summary>
    /// The gate must be consulted with the row's OWN parent, not the one the caller sent. Passing
    /// the supplied id would let a caller nominate an appointment they are a party to and still
    /// write to somebody else's row, which is the whole defect wearing a check.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_AsksTheGuardAboutTheRowsOwnParent_NotTheSuppliedOne()
    {
        var h = Build(rowsActualParent: SomeoneElsesAppointment);

        // The substitute is made to refuse so the method returns at the gate. That is also what
        // keeps the test honest about ORDER: a directly-constructed app service cannot resolve
        // ABP's ObjectMapper, so if the gate were consulted after the write this would die on the
        // mapping instead of throwing here.
        h.Guard
            .EnsureCanWriteChildAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Throws(new AbpAuthorizationException("refused"));

        await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            await h.Service.UpdateAsync(RowId, new AppointmentInjuryDetailUpdateDto
            {
                AppointmentId = OwnAppointment,
            }));

        await h.Guard.Received(1).EnsureCanWriteChildAsync(SomeoneElsesAppointment, OwnAppointment);
        await h.Guard.DidNotReceive().EnsureCanWriteChildAsync(OwnAppointment, OwnAppointment);
    }

    /// <summary>
    /// The refusal must happen BEFORE the manager runs. The manager assigns the caller-supplied
    /// parent onto the entity, so a gate that ran afterwards would refuse a write that had already
    /// happened.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_RefusesBeforeTheManagerWritesAnything()
    {
        var h = Build(rowsActualParent: SomeoneElsesAppointment);
        h.Guard
            .EnsureCanWriteChildAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Throws(new AbpAuthorizationException("refused"));

        await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            await h.Service.UpdateAsync(RowId, new AppointmentInjuryDetailUpdateDto
            {
                AppointmentId = OwnAppointment,
            }));

        await h.Manager.DidNotReceiveWithAnyArgs().UpdateAsync(
            default, default, default, default!, default, default!, default, default, default, default);
    }

    // ---- the guard's own rule ----

    /// <summary>
    /// PARTY IS CHECKED FIRST, and this test exists to keep it that way.
    ///
    /// <para>An earlier version pinned the opposite order and claimed the two refusals were
    /// indistinguishable because they shared an exception type. They did not: this class threw
    /// <c>AbpAuthorizationException</c> while the real gate throws
    /// <c>BusinessException(AppointmentAccessDenied)</c>. So a non-party received one error when the
    /// appointment id they supplied was wrong and the other when it happened to be right, which told
    /// them whether their guess matched the row's real parent. The test agreed with the comment only
    /// because it stubbed the gate to throw the wrong type.</para>
    ///
    /// <para>Order is what closes it: a non-party is now refused at the party check whatever they
    /// supply, and never reaches the parent comparison.</para>
    /// </summary>
    [Fact]
    public async Task TheGuard_ChecksPartyFirst_SoANonPartyCannotProbeTheParent()
    {
        var readGuard = Substitute.For<AppointmentReadAccessGuard>(
            Substitute.For<IAppointmentRepository>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.AppointmentAccessors.AppointmentAccessor, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>(),
            Substitute.For<Volo.Abp.Linq.IAsyncQueryableExecuter>());

        // The real gate's refusal, not this class's.
        readGuard
            .EnsureCanReadAsync(Arg.Any<Guid>())
            .Throws(new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied));

        var guard = new AppointmentChildOwnershipGuard(readGuard, null!);

        // A mismatched parent and a matching one must refuse identically for a non-party.
        var mismatched = await Should.ThrowAsync<BusinessException>(async () =>
            await guard.EnsureCanWriteChildAsync(OwnAppointment, SomeoneElsesAppointment));
        var matched = await Should.ThrowAsync<BusinessException>(async () =>
            await guard.EnsureCanWriteChildAsync(OwnAppointment, OwnAppointment));

        mismatched.Code.ShouldBe(matched.Code);
        await readGuard.Received(2).EnsureCanReadAsync(OwnAppointment);
    }

    /// <summary>
    /// A matching parent still has to pass the party check. Without this the guard would be a
    /// re-parenting check only, and editing someone else's row by supplying its correct parent id
    /// would sail through.
    /// </summary>
    [Fact]
    public async Task TheGuard_StillChecksPartyAccess_WhenTheParentMatches()
    {
        var readGuard = Substitute.For<AppointmentReadAccessGuard>(
            Substitute.For<IAppointmentRepository>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.AppointmentAccessors.AppointmentAccessor, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>(),
            Substitute.For<Volo.Abp.Linq.IAsyncQueryableExecuter>());

        var guard = new AppointmentChildOwnershipGuard(readGuard, null!);

        await guard.EnsureCanWriteChildAsync(OwnAppointment, OwnAppointment);

        await readGuard.Received(1).EnsureCanReadAsync(OwnAppointment);
    }
}
