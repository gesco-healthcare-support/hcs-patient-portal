using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// <see cref="AppointmentManager"/> paths the existing tests do not reach: the three "load the
/// source by confirmation number" gates used by Re-Submit, Re-Evaluation and Re-Book, the
/// Approved-at-creation date stamp, and the refusal of a booking dated in the past.
///
/// <para>Refusals are asserted with the decoy the refusal must not touch: the seeded
/// appointments are present when an unknown number is looked up, and a refused create is
/// checked to have saved nothing under its confirmation number.</para>
/// </summary>
public class EfCoreAppointmentManagerSourceTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly AppointmentManager _manager;
    private readonly IRepository<Appointment, Guid> _appointments;
    private readonly IDoctorAvailabilityRepository _slots;
    private readonly ICurrentTenant _currentTenant;

    public EfCoreAppointmentManagerSourceTests()
    {
        _manager = GetRequiredService<AppointmentManager>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _slots = GetRequiredService<IDoctorAvailabilityRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    public static TheoryData<string> Loaders => new() { "resubmit", "reval", "rebook" };

    [Theory]
    [MemberData(nameof(Loaders))]
    public async Task Loader_BlankConfirmationNumber_IsRejectedBeforeAnyLookup(string loader)
    {
        var ex = await Should.ThrowAsync<ArgumentException>(() => InTenantAAsync(() => LoadAsync(loader, "  ")));

        ex.ParamName.ShouldBe("sourceConfirmationNumber");
    }

    [Theory]
    [MemberData(nameof(Loaders))]
    public async Task Loader_UnknownConfirmationNumber_IsNotFound_WhileOtherAppointmentsExist(string loader)
    {
        // Decoy: the seeded appointment is present and findable, so "not found" is about the number.
        (await InTenantAAsync(() => _appointments.CountAsync(
            a => a.RequestConfirmationNumber == AppointmentsTestData.Appointment1RequestConfirmationNumber))).ShouldBe(1);

        await Should.ThrowAsync<EntityNotFoundException>(() =>
            InTenantAAsync(() => LoadAsync(loader, "TNOSUCH" + NewToken()[..4])));
    }

    [Fact]
    public async Task LoadResubmitSource_ReturnsARejectedSource()
    {
        var number = await InsertAppointmentAsync(AppointmentStatusType.Rejected);

        var source = await InTenantAAsync(() => _manager.LoadResubmitSourceAsync(number));

        source.RequestConfirmationNumber.ShouldBe(number);
        source.AppointmentStatus.ShouldBe(AppointmentStatusType.Rejected);
    }

    [Fact]
    public async Task LoadRevalSource_ReturnsAnApprovedSource()
    {
        var number = await InsertAppointmentAsync(AppointmentStatusType.Approved);

        var source = await InTenantAAsync(() => _manager.LoadRevalSourceAsync(number, callerIsItAdmin: false));

        source.RequestConfirmationNumber.ShouldBe(number);
    }

    [Fact]
    public async Task LoadReBookSource_ReturnsACancelledSourceThatWasNeverReBooked()
    {
        var number = await InsertAppointmentAsync(AppointmentStatusType.CancelledNoBill);

        var source = await InTenantAAsync(() => _manager.LoadReBookSourceAsync(number, callerIsInternal: true));

        source.RequestConfirmationNumber.ShouldBe(number);
    }

    [Fact]
    public async Task Create_InApprovedState_StampsTheApproveDate()
    {
        var slot = await InsertSlotAsync();
        var number = "T" + NewToken();

        var created = await InTenantAAsync(() => _manager.CreateAsync(
            PatientsTestData.Patient1Id, IdentityUsersTestData.Patient1UserId, LocationsTestData.AppointmentType1Id,
            LocationsTestData.Location1Id, slot, new DateTime(2034, 1, 1, 9, 0, 0, DateTimeKind.Utc), number,
            AppointmentStatusType.Approved));

        created.AppointmentStatus.ShouldBe(AppointmentStatusType.Approved);
        created.AppointmentApproveDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_DatedInThePast_IsRefused_AndSavesNothing()
    {
        var slot = await InsertSlotAsync();
        var number = "T" + NewToken();

        var ex = await Should.ThrowAsync<BusinessException>(() => InTenantAAsync(() => _manager.CreateAsync(
            PatientsTestData.Patient1Id, IdentityUsersTestData.Patient1UserId, LocationsTestData.AppointmentType1Id,
            LocationsTestData.Location1Id, slot, new DateTime(2000, 1, 1, 9, 0, 0, DateTimeKind.Utc), number,
            AppointmentStatusType.Pending)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime);
        (await InTenantAAsync(() => _appointments.CountAsync(a => a.RequestConfirmationNumber == number))).ShouldBe(0);
        // The decoy is untouched: the seeded appointment is still there.
        (await InTenantAAsync(() => _appointments.CountAsync(a => a.Id == AppointmentsTestData.Appointment1Id))).ShouldBe(1);
    }

    [Fact]
    public async Task LoadResubmitSource_CannotLoadAnotherOfficesRejectedAppointment()
    {
        // Office decoy: an eligible (Rejected) source with this number exists in TenantB only.
        // TenantA must get "not found", not TenantB's appointment; TenantB loads its own.
        var number = "T" + NewToken();
        var slotB = await InTenantAsync(TenantsTestData.TenantBRef, async () =>
        {
            var id = Guid.NewGuid();
            await _slots.InsertAsync(new DoctorAvailability(id, LocationsTestData.Location1Id,
                new DateTime(2034, 2, 2, 0, 0, 0, DateTimeKind.Utc), new TimeOnly(9, 0), new TimeOnly(10, 0), BookingStatus.Available), autoSave: true);
            return id;
        });
        await InTenantAsync(TenantsTestData.TenantBRef, async () =>
        {
            await _appointments.InsertAsync(new Appointment(Guid.NewGuid(), PatientsTestData.Patient2Id, IdentityUsersTestData.Patient2UserId,
                LocationsTestData.AppointmentType1Id, LocationsTestData.Location1Id, slotB,
                new DateTime(2034, 2, 2, 9, 0, 0, DateTimeKind.Utc), number, AppointmentStatusType.Rejected), autoSave: true);
            return true;
        });

        await Should.ThrowAsync<EntityNotFoundException>(() => InTenantAAsync(() => _manager.LoadResubmitSourceAsync(number)));
        (await InTenantAsync(TenantsTestData.TenantBRef, () => _manager.LoadResubmitSourceAsync(number)))
            .RequestConfirmationNumber.ShouldBe(number);
    }

    private Task<Appointment> LoadAsync(string loader, string number) => loader switch
    {
        "resubmit" => _manager.LoadResubmitSourceAsync(number),
        "reval" => _manager.LoadRevalSourceAsync(number, callerIsItAdmin: false),
        _ => _manager.LoadReBookSourceAsync(number, callerIsInternal: false),
    };

    private Task<T> InTenantAAsync<T>(Func<Task<T>> action) => InTenantAsync(TenantsTestData.TenantARef, action);

    private Task<T> InTenantAsync<T>(Guid tenantId, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                return await action();
            }
        });

    private Task<Guid> InsertSlotAsync() =>
        InTenantAAsync(async () =>
        {
            var id = Guid.NewGuid();
            await _slots.InsertAsync(
                new DoctorAvailability(id, LocationsTestData.Location1Id, new DateTime(2034, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    new TimeOnly(9, 0), new TimeOnly(10, 0), BookingStatus.Available),
                autoSave: true);
            return id;
        });

    private async Task<string> InsertAppointmentAsync(AppointmentStatusType status)
    {
        var slot = await InsertSlotAsync();
        var number = "T" + NewToken();
        await InTenantAAsync(async () =>
        {
            await _appointments.InsertAsync(
                new Appointment(Guid.NewGuid(), PatientsTestData.Patient1Id, IdentityUsersTestData.Patient1UserId,
                    LocationsTestData.AppointmentType1Id, LocationsTestData.Location1Id, slot,
                    new DateTime(2034, 1, 1, 9, 0, 0, DateTimeKind.Utc), number, status),
                autoSave: true);
            return true;
        });
        return number;
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
