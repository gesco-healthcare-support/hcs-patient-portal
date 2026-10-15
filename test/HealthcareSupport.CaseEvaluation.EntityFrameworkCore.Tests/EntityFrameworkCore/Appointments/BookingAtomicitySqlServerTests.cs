using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.SqlServer;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Appointments;

/// <summary>
/// #732: a booking that fails part-way must leave NOTHING behind. <c>SubmitAsync</c> writes a
/// patient, an appointment and the child groups in one unit of work, because a half-written booking
/// already reached production once (A00010 and A00011: attorney columns with no join rows).
///
/// <para>The SQLite multi-office harness cannot test this: it switches transactions off and gives
/// every resolved connection string its own connection, so nothing there can roll back -- a
/// one-row control measured that on 2026-09-08. This class runs the same application services on
/// real SQL Server with transactions ON (<see cref="BookingAtomicityTestModule"/>), and proves the
/// harness can roll back before relying on it.</para>
///
/// <para>Every "it is gone" assertion here has a matching control that shows the same query DOES
/// find the row when it should exist. Without that, an absent row proves nothing: it is equally
/// what a write that never happened looks like.</para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public class BookingAtomicitySqlServerTests : IAsyncLifetime
{
    private readonly SqlServerFeedFixture _fixture;
    private BookingAtomicityHarness.Databases _databases = null!;
    private IAbpApplicationWithInternalServiceProvider _application = null!;

    public BookingAtomicitySqlServerTests(SqlServerFeedFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider Services => _application.ServiceProvider;

    private Guid OfficeId => _databases.Seeded.OfficeId;

    public async Task InitializeAsync()
    {
        _databases = await BookingAtomicityHarness.GetDatabasesAsync(_fixture);
        _application = await BookingAtomicityHarness.StartApplicationAsync(_databases.Host);
    }

    public async Task DisposeAsync()
    {
        await _application.ShutdownAsync();
        _application.Dispose();
    }

    [Fact]
    public async Task Harness_RollsBackATransactionalUnitOfWorkThatThrows()
    {
        // The precondition for every other test here, measured rather than assumed: the SQLite
        // harness fails exactly this.
        var name = $"ROLLBACK-STATE-{Guid.NewGuid():N}";

        await Should.ThrowAsync<InvalidOperationException>(() => WriteStateThenThrowAsync(name, isTransactional: true));

        (await CountStatesAsync(name)).ShouldBe(0,
            "a transactional unit of work that threw must leave no row behind; if it does, this harness "
            + "cannot roll back and no atomicity test in this class means anything");
    }

    [Fact]
    public async Task Harness_KeepsTheRowWhenTheUnitOfWorkIsNotTransactional()
    {
        // The negative control for the test above. With no transaction the flushed row survives the
        // throw -- which shows the read-back query can see a survivor, so a zero above is a rollback
        // and not a query that cannot find anything.
        var name = $"KEPT-STATE-{Guid.NewGuid():N}";

        await Should.ThrowAsync<InvalidOperationException>(() => WriteStateThenThrowAsync(name, isTransactional: false));

        (await CountStatesAsync(name)).ShouldBe(1);
    }

    [Fact]
    public async Task SubmitAsync_WhenAChildWriteFails_PersistsNothingAtAll()
    {
        var marker = Guid.NewGuid().ToString("N")[..12];
        var slotId = await InsertSlotAsync(dayOffset: 30, new TimeOnly(11, 0));
        var doomed = BuildBooking(slotId, dayOffset: 30, new TimeOnly(11, 0), marker);

        // THE TRIGGER. A WcabOfficeId that does not exist: AppointmentInjuryDetailCreateDto declares
        // it as a bare Guid? with no validation attribute, so it passes the DTO boundary, and fails at
        // the database on the foreign key to WcabOffices when the injury row is inserted. That is
        // AFTER the patient and appointment are flushed and AFTER the employer detail -- the first
        // child group written -- so real rows exist when it throws.
        //
        // Not an over-length string: [StringLength] makes ABP's validation interceptor reject the DTO
        // before SubmitAsync runs, so nothing is written and an absent row proves nothing. That is
        // the exact mistake #732 found in the two older "rollback" tests.
        doomed.InjuryDetails[0].Injury.WcabOfficeId = Guid.NewGuid();

        // SubmitAsync wraps an uncoded failure in AppointmentSubmitFailed, whose message tells the
        // booker "nothing was saved, so retrying is safe" -- the promise under test. Asserted by code
        // AND by the inner type: a base-type assertion accepts a validation failure as readily as a
        // write failure, which is how the older tests passed while testing nothing. A DbUpdateException
        // inside is what proves the failure happened at the database, after rows were written.
        var failure = await Should.ThrowAsync<BusinessException>(() => SubmitAsBookerAsync(doomed));
        failure.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentSubmitFailed);
        failure.InnerException.ShouldBeOfType<DbUpdateException>();

        // THE LOAD-BEARING ASSERTION. The employer detail certainly existed a moment before the
        // failure, and nothing else in this test could remove it, so only a rollback accounts for
        // its absence.
        (await CountEmployerDetailsAsync(marker)).ShouldBe(0,
            "the employer detail was written before the injury insert failed; if it survives, a "
            + "half-booking reached the database");
        (await CountPatientsAsync(marker)).ShouldBe(0, "a failed booking must not leave an orphan patient");
        (await CountAppointmentsAsync(slotId)).ShouldBe(0, "a failed booking must not leave an appointment");
    }

    [Fact]
    public async Task SubmitAsync_WithoutTheTrigger_WritesEveryRowTheFailureTestExpectsToBeGone()
    {
        // The positive control for the test above: the same booking with a valid injury commits all
        // three rows, so each zero there is a rollback rather than a row that is never written.
        var marker = Guid.NewGuid().ToString("N")[..12];
        var slotId = await InsertSlotAsync(dayOffset: 31, new TimeOnly(11, 0));

        await SubmitAsBookerAsync(BuildBooking(slotId, dayOffset: 31, new TimeOnly(11, 0), marker));

        (await CountEmployerDetailsAsync(marker)).ShouldBe(1);
        (await CountPatientsAsync(marker)).ShouldBe(1);
        (await CountAppointmentsAsync(slotId)).ShouldBe(1);
    }

    private async Task WriteStateThenThrowAsync(string name, bool isTransactional)
    {
        using (Services.GetRequiredService<ICurrentTenant>().Change(OfficeId))
        using (Services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: isTransactional))
        {
            await Services.GetRequiredService<IRepository<State, Guid>>()
                .InsertAsync(new State(Guid.NewGuid(), name), autoSave: true);
            throw new InvalidOperationException("TEST failure after the row was flushed");
        }
    }

    /// <summary>
    /// Calls the booking as the office's seeded booker, with NO surrounding unit of work, so the
    /// application service's own unit of work is the outermost one and decides whether there is a
    /// transaction. Wrapping the call in a test unit of work would let the service's unit of work
    /// join that one instead, and the test would be measuring the wrapper.
    /// </summary>
    private async Task SubmitAsBookerAsync(AppointmentSubmitDto booking)
    {
        using (Services.GetRequiredService<ICurrentTenant>().Change(OfficeId))
        using (WithCurrentUser.Run(Services.GetRequiredService<ICurrentPrincipalAccessor>(), _databases.Seeded.BookerUserId, "admin"))
        {
            await Services.GetRequiredService<IAppointmentsAppService>().SubmitAsync(booking);
        }
    }

    private async Task<Guid> InsertSlotAsync(int dayOffset, TimeOnly from)
    {
        var slot = new DoctorAvailability(
            id: Guid.NewGuid(),
            locationId: _databases.Seeded.LocationId,
            availableDate: DateTime.Today.AddDays(dayOffset),
            fromTime: from,
            toTime: from.AddHours(1),
            bookingStatusId: BookingStatus.Available,
            capacity: 3);
        slot.TenantId = OfficeId;
        slot.AppointmentTypes.Add(
            new DoctorAvailabilityAppointmentType(slot.Id, _databases.Seeded.AppointmentTypeId, OfficeId));

        await BookingAtomicityHarness.InOfficeAsync(Services, OfficeId, () =>
            Services.GetRequiredService<IRepository<DoctorAvailability, Guid>>().InsertAsync(slot, autoSave: true));
        return slot.Id;
    }

    /// <summary>
    /// The smallest booking that reaches the child-group writer: a new patient, an employer detail
    /// (the first group written) and one injury with one body part. The marker makes every row this
    /// booking writes findable, and keeps its patient distinct from every other test's for the
    /// deduplication scan. It is 12 hex characters, not a whole Guid, so the patient email stays
    /// inside the column limit.
    /// </summary>
    private AppointmentSubmitDto BuildBooking(Guid slotId, int dayOffset, TimeOnly from, string marker)
    {
        var date = DateTime.Today.AddDays(dayOffset);
        return new AppointmentSubmitDto
        {
            Patient = new CreatePatientForAppointmentBookingInput
            {
                FirstName = "Atomic",
                LastName = $"Booking{marker[..8]}",
                Email = PatientEmail(marker),
                DateOfBirth = new DateTime(1980, 1, 1).AddDays(dayOffset),
                PhoneNumber = $"555{dayOffset:D7}",
                SocialSecurityNumber = $"SSN-{marker[..8]}",
                ZipCode = $"8{dayOffset:D4}",
            },
            IdentityUserId = _databases.Seeded.BookerUserId,
            AppointmentTypeId = _databases.Seeded.AppointmentTypeId,
            LocationId = _databases.Seeded.LocationId,
            DoctorAvailabilityId = slotId,
            AppointmentDate = date.Add(from.ToTimeSpan()).AddMinutes(15),
            AppointmentStatus = AppointmentStatusType.Pending,
            PatientEmail = PatientEmail(marker),
            EmployerDetail = new AppointmentEmployerDetailCreateDto
            {
                EmployerName = EmployerName(marker),
                Occupation = "Machinist",
            },
            InjuryDetails = new List<AppointmentInjurySubmitDto>
            {
                new()
                {
                    Injury = new AppointmentInjuryDetailCreateDto
                    {
                        DateOfInjury = new DateTime(2025, 2, 3),
                        ClaimNumber = $"CLM-{marker[..8]}",
                        IsCumulativeInjury = false,
                        WcabAdj = "ADJ-1000001",
                        BodyPartsSummary = "Lower back",
                    },
                    BodyParts = new List<AppointmentBodyPartCreateDto>
                    {
                        new() { BodyPartDescription = "Lower back" },
                    },
                },
            },
        };
    }

    private static string PatientEmail(string marker) => $"atomic-{marker}@example.test";

    private static string EmployerName(string marker) => $"ROLLBACK-EMP-{marker}";

    private Task<int> CountStatesAsync(string name) =>
        CountInOfficeAsync<State>(state => state.Name == name);

    private Task<int> CountEmployerDetailsAsync(string marker) =>
        CountInOfficeAsync<AppointmentEmployerDetail>(detail => detail.EmployerName == EmployerName(marker));

    private Task<int> CountPatientsAsync(string marker) =>
        CountInOfficeAsync<Patient>(patient => patient.Email == PatientEmail(marker));

    private Task<int> CountAppointmentsAsync(Guid slotId) =>
        CountInOfficeAsync<Appointment>(appointment => appointment.DoctorAvailabilityId == slotId);

    private async Task<int> CountInOfficeAsync<TEntity>(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate)
        where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid>
    {
        var count = 0;
        await BookingAtomicityHarness.InOfficeAsync(Services, OfficeId, async () =>
        {
            count = await Services.GetRequiredService<IRepository<TEntity, Guid>>().CountAsync(predicate);
        });
        return count;
    }
}
