using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Read paths of <see cref="EfCoreAppointmentRepository"/> that the existing repository tests do
/// not reach: the empty-input guards, the slot-holder name projection, the plain (no navigation)
/// list filters, the caller-computed visibility list, and the detail view's booker, defense
/// attorney and WCAB office loads.
///
/// <para>Every fact inserts its own rows in TenantA with fresh ids and a unique confirmation
/// number, and every filter test inserts a decoy the filter must leave out, so a filter that
/// stopped filtering fails instead of passing on a lucky data set.</para>
/// </summary>
public class EfCoreAppointmentRepositoryReadPathTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IAppointmentRepository _appointments;
    private readonly IRepository<Appointment, Guid> _appointmentStore;
    private readonly IDoctorAvailabilityRepository _slots;
    private readonly IRepository<DefenseAttorney, Guid> _defenseAttorneys;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _defenseLinks;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuries;
    private readonly IRepository<WcabOffice, Guid> _wcabOffices;
    private readonly ICurrentTenant _currentTenant;

    public EfCoreAppointmentRepositoryReadPathTests()
    {
        _appointments = GetRequiredService<IAppointmentRepository>();
        _appointmentStore = GetRequiredService<IRepository<Appointment, Guid>>();
        _slots = GetRequiredService<IDoctorAvailabilityRepository>();
        _defenseAttorneys = GetRequiredService<IRepository<DefenseAttorney, Guid>>();
        _defenseLinks = GetRequiredService<IRepository<AppointmentDefenseAttorney, Guid>>();
        _injuries = GetRequiredService<IRepository<AppointmentInjuryDetail, Guid>>();
        _wcabOffices = GetRequiredService<IRepository<WcabOffice, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindByConfirmationNumber_BlankNumber_ReturnsNullEvenThoughAppointmentsExist(string? number)
    {
        // Decoy: the seeded appointments exist in TenantA, so a blank number that fell through
        // to the query could still match something.
        var found = await InTenantAAsync(() => _appointments.FindByConfirmationNumberAsync(number!));

        found.ShouldBeNull();
    }

    [Fact]
    public async Task SlotBulkQueries_EmptyOrNullSlotList_ReturnEmptyResults()
    {
        await InTenantAAsync(async () =>
        {
            (await _appointments.GetActivePatientNamesForSlotsAsync(new List<Guid>())).ShouldBeEmpty();
            (await _appointments.GetActivePatientNamesForSlotsAsync(null!)).ShouldBeEmpty();
            (await _appointments.GetActiveAppointmentsForSlotsAsync(new List<Guid>())).ShouldBeEmpty();
            (await _appointments.GetActiveAppointmentsForSlotsAsync(null!)).ShouldBeEmpty();
            return true;
        });
    }

    [Fact]
    public async Task GetActivePatientNamesForSlots_ListsOnlyActiveHolders_OfTheRequestedSlots()
    {
        var requestedSlot = await InsertSlotAsync(new DateTime(2033, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        var otherSlot = await InsertSlotAsync(new DateTime(2033, 2, 2, 0, 0, 0, DateTimeKind.Utc));
        await InsertAppointmentAsync(requestedSlot, AppointmentStatusType.Approved);
        // Decoys: a freed status in the requested slot, and an active holder of a slot not asked for.
        await InsertAppointmentAsync(requestedSlot, AppointmentStatusType.Rejected);
        await InsertAppointmentAsync(otherSlot, AppointmentStatusType.Approved);

        var names = await InTenantAAsync(() =>
            _appointments.GetActivePatientNamesForSlotsAsync(new List<Guid> { requestedSlot }));

        names.Keys.ShouldBe(new[] { requestedSlot });
        names[requestedSlot].ShouldBe(new[]
        {
            $"{PatientsTestData.Patient1FirstName} {PatientsTestData.Patient1LastName}".Trim(),
        });
    }

    [Fact]
    public async Task GetList_WithoutNavigation_FiltersByPanelNumberTextAndDateRange()
    {
        var slot = await InsertSlotAsync(new DateTime(2033, 3, 3, 0, 0, 0, DateTimeKind.Utc));
        var panel = "TEST-PNL-" + NewToken();
        var target = await InsertAppointmentAsync(
            slot, AppointmentStatusType.Pending, panelNumber: panel, date: new DateTime(2033, 3, 3, 9, 0, 0, DateTimeKind.Utc));
        // Decoy: same slot, a different panel and a later date, so every filter must exclude it.
        await InsertAppointmentAsync(
            slot, AppointmentStatusType.Pending, panelNumber: "TEST-PNL-" + NewToken(), date: new DateTime(2033, 3, 10, 9, 0, 0, DateTimeKind.Utc));

        await InTenantAAsync(async () =>
        {
            (await _appointments.GetListAsync(panelNumber: panel)).Select(a => a.Id).ShouldBe(new[] { target });
            (await _appointments.GetListAsync(filterText: panel)).Select(a => a.Id).ShouldBe(new[] { target });
            var inRange = await _appointments.GetListAsync(
                filterText: "TEST-PNL-",
                appointmentDateMin: new DateTime(2033, 3, 3, 0, 0, 0, DateTimeKind.Utc),
                appointmentDateMax: new DateTime(2033, 3, 4, 0, 0, 0, DateTimeKind.Utc));
            inRange.Select(a => a.Id).ShouldBe(new[] { target });
            return true;
        });
    }

    [Fact]
    public async Task GetCount_WithVisibilityList_CountsOnlyTheListedAppointments()
    {
        var slot = await InsertSlotAsync(new DateTime(2033, 4, 4, 0, 0, 0, DateTimeKind.Utc));
        var visible = await InsertAppointmentAsync(slot, AppointmentStatusType.Pending);
        await InsertAppointmentAsync(slot, AppointmentStatusType.Pending); // decoy: not in the list

        await InTenantAAsync(async () =>
        {
            (await _appointments.GetCountAsync(visibleAppointmentIds: new[] { visible })).ShouldBe(1);
            // An empty list means "this user may see no appointment", not "no filter".
            (await _appointments.GetCountAsync(visibleAppointmentIds: Array.Empty<Guid>())).ShouldBe(0);
            return true;
        });
    }

    [Fact]
    public async Task GetWithNavigation_LoadsTheBookerTheDefenseAttorneyAndTheInjuryWcabOffice()
    {
        var slot = await InsertSlotAsync(new DateTime(2033, 5, 5, 0, 0, 0, DateTimeKind.Utc));
        var appointmentId = await InsertAppointmentAsync(
            slot, AppointmentStatusType.Pending, bookedBy: IdentityUsersTestData.ApplicantAttorney1UserId);
        var defenseAttorneyId = Guid.NewGuid();
        var wcabOfficeId = Guid.NewGuid();
        await InTenantAAsync(async () =>
        {
            await _defenseAttorneys.InsertAsync(
                new DefenseAttorney(defenseAttorneyId, null, IdentityUsersTestData.DefenseAttorney1UserId, firmName: "TEST-Defense Firm"),
                autoSave: true);
            await _defenseLinks.InsertAsync(
                new AppointmentDefenseAttorney(Guid.NewGuid(), appointmentId, defenseAttorneyId, IdentityUsersTestData.DefenseAttorney1UserId),
                autoSave: true);
            await _wcabOffices.InsertAsync(
                new WcabOffice(wcabOfficeId, null, "TEST-Wcab " + NewToken(), "TEST-W" + NewToken(), isActive: true),
                autoSave: true);
            await _injuries.InsertAsync(
                new AppointmentInjuryDetail(
                    Guid.NewGuid(), appointmentId, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    "TEST-CLM-" + NewToken(), isCumulativeInjury: false, bodyPartsSummary: "TEST-knee",
                    wcabAdj: "TEST-ADJ-" + NewToken(), wcabOfficeId: wcabOfficeId),
                autoSave: true);
            return true;
        });

        var result = (await InTenantAAsync(() => _appointments.GetWithNavigationPropertiesAsync(appointmentId))).ShouldNotBeNull();

        result.BookedByUser.ShouldNotBeNull().Id.ShouldBe(IdentityUsersTestData.ApplicantAttorney1UserId);
        var defense = result.AppointmentDefenseAttorney.ShouldNotBeNull();
        defense.DefenseAttorney.ShouldNotBeNull().Id.ShouldBe(defenseAttorneyId);
        defense.IdentityUser.ShouldNotBeNull().Id.ShouldBe(IdentityUsersTestData.DefenseAttorney1UserId);
        result.AppointmentInjuryDetails.Count.ShouldBe(1);
        result.AppointmentInjuryDetails[0].WcabOffice.ShouldNotBeNull().Id.ShouldBe(wcabOfficeId);
    }

    [Fact]
    public async Task GetWithNavigation_DefenseLinkWithoutAUser_LeavesTheDefenseSlotEmpty()
    {
        var slot = await InsertSlotAsync(new DateTime(2033, 6, 6, 0, 0, 0, DateTimeKind.Utc));
        var appointmentId = await InsertAppointmentAsync(slot, AppointmentStatusType.Pending);
        var defenseAttorneyId = Guid.NewGuid();
        await InTenantAAsync(async () =>
        {
            await _defenseAttorneys.InsertAsync(
                new DefenseAttorney(defenseAttorneyId, null, null, firmName: "TEST-Unlinked Firm"), autoSave: true);
            await _defenseLinks.InsertAsync(
                new AppointmentDefenseAttorney(Guid.NewGuid(), appointmentId, defenseAttorneyId, identityUserId: null),
                autoSave: true);
            return true;
        });

        var result = (await InTenantAAsync(() => _appointments.GetWithNavigationPropertiesAsync(appointmentId))).ShouldNotBeNull();

        result.Appointment.ShouldNotBeNull().Id.ShouldBe(appointmentId);
        result.AppointmentDefenseAttorney.ShouldBeNull();
    }

    [Fact]
    public async Task GetListWithNavigation_AttachesTheInjuryWcabOffice_ToEachRow()
    {
        var slot = await InsertSlotAsync(new DateTime(2033, 7, 7, 0, 0, 0, DateTimeKind.Utc));
        var panel = "TEST-PNL-" + NewToken();
        var appointmentId = await InsertAppointmentAsync(slot, AppointmentStatusType.Pending, panelNumber: panel);
        var wcabOfficeId = Guid.NewGuid();
        await InTenantAAsync(async () =>
        {
            await _wcabOffices.InsertAsync(
                new WcabOffice(wcabOfficeId, null, "TEST-Wcab " + NewToken(), "TEST-W" + NewToken(), isActive: true),
                autoSave: true);
            await _injuries.InsertAsync(
                new AppointmentInjuryDetail(
                    Guid.NewGuid(), appointmentId, new DateTime(2030, 2, 2, 0, 0, 0, DateTimeKind.Utc),
                    "TEST-CLM-" + NewToken(), isCumulativeInjury: false, bodyPartsSummary: "TEST-back",
                    wcabAdj: "TEST-ADJ-" + NewToken(), wcabOfficeId: wcabOfficeId),
                autoSave: true);
            return true;
        });

        var rows = await InTenantAAsync(() => _appointments.GetListWithNavigationPropertiesAsync(panelNumber: panel));

        rows.Count.ShouldBe(1);
        rows[0].AppointmentInjuryDetails.Count.ShouldBe(1);
        rows[0].AppointmentInjuryDetails[0].WcabOffice.ShouldNotBeNull().Id.ShouldBe(wcabOfficeId);
    }

    [Fact]
    public async Task FindByConfirmationNumber_DoesNotSeeAnotherOfficesAppointment()
    {
        // Office decoy: the confirmation number exists in TenantB only. TenantA's lookup must not
        // find it (office isolation), while TenantB's own lookup does (positive control).
        var number = "T" + NewToken();
        var slotB = await InTenantAsync(TenantsTestData.TenantBRef, async () =>
        {
            var id = Guid.NewGuid();
            await _slots.InsertAsync(new DoctorAvailability(id, LocationsTestData.Location1Id,
                new DateTime(2033, 8, 8, 0, 0, 0, DateTimeKind.Utc), new TimeOnly(9, 0), new TimeOnly(10, 0), BookingStatus.Available), autoSave: true);
            return id;
        });
        var appointmentB = await InTenantAsync(TenantsTestData.TenantBRef, async () =>
        {
            var id = Guid.NewGuid();
            await _appointmentStore.InsertAsync(new Appointment(id, PatientsTestData.Patient2Id, IdentityUsersTestData.Patient2UserId,
                LocationsTestData.AppointmentType1Id, LocationsTestData.Location1Id, slotB,
                new DateTime(2033, 8, 8, 9, 0, 0, DateTimeKind.Utc), number, AppointmentStatusType.Pending), autoSave: true);
            return id;
        });

        var fromOfficeA = await InTenantAAsync(() => _appointments.FindByConfirmationNumberAsync(number));
        var fromOfficeB = await InTenantAsync(TenantsTestData.TenantBRef, () => _appointments.FindByConfirmationNumberAsync(number));

        fromOfficeA.ShouldBeNull();
        fromOfficeB.ShouldNotBeNull().Id.ShouldBe(appointmentB);
    }

    private Task<T> InTenantAAsync<T>(Func<Task<T>> action) => InTenantAsync(TenantsTestData.TenantARef, action);

    private Task<T> InTenantAsync<T>(Guid tenantId, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                return await action();
            }
        });

    private Task<Guid> InsertSlotAsync(DateTime day) =>
        InTenantAAsync(async () =>
        {
            var id = Guid.NewGuid();
            await _slots.InsertAsync(
                new DoctorAvailability(id, LocationsTestData.Location1Id, day, new TimeOnly(9, 0), new TimeOnly(10, 0), BookingStatus.Available),
                autoSave: true);
            return id;
        });

    private Task<Guid> InsertAppointmentAsync(
        Guid slotId,
        AppointmentStatusType status,
        string? panelNumber = null,
        DateTime? date = null,
        Guid? bookedBy = null) =>
        InTenantAAsync(async () =>
        {
            var id = Guid.NewGuid();
            var appointment = new Appointment(
                id,
                PatientsTestData.Patient1Id,
                IdentityUsersTestData.Patient1UserId,
                LocationsTestData.AppointmentType1Id,
                LocationsTestData.Location1Id,
                slotId,
                date ?? new DateTime(2033, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                "T" + NewToken(),
                status,
                panelNumber: panelNumber);
            appointment.BookedByUserId = bookedBy;
            await _appointmentStore.InsertAsync(appointment, autoSave: true);
            return id;
        });

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
