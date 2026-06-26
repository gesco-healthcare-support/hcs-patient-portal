using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Restores the 20 AppointmentsAppService booking-flow tests that were skipped because
/// booking needs per-tenant catalogs (appointment type, location, availability slot) and
/// the single-shared-SQLite rig could not seed them per office. On the multi-office
/// harness office A owns its database, so the full booking chain seeds and exercises
/// there. Each test seeds its own scratch slot (unique GUID) so the shared seeded office
/// data is never mutated and tests stay independent.
///
/// Ported from AppointmentsAppServiceTests (the [Fact(Skip "Phase F harness")] block).
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeAppointmentsAppServiceTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly IAppointmentsAppService _appointmentsAppService;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public MultiOfficeAppointmentsAppServiceTests()
    {
        _appointmentsAppService = GetRequiredService<IAppointmentsAppService>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _slotRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_ResolvesPatientLocationTypeAndSlot()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            // The read-access guard requires the caller to be a party to the
            // appointment; run as the booker who created the seeded appointment.
            using (WithCurrentUser.Run(_principalAccessor, officeA.BookerUserId))
            {
                var result = await _appointmentsAppService.GetWithNavigationPropertiesAsync(officeA.AppointmentId);

                result.ShouldNotBeNull();
                result.Appointment.Id.ShouldBe(officeA.AppointmentId);
                result.Patient.ShouldNotBeNull();
                result.Patient!.Id.ShouldBe(officeA.PatientId);
                result.AppointmentType.ShouldNotBeNull();
                result.AppointmentType!.Id.ShouldBe(officeA.AppointmentTypeId);
                result.Location.ShouldNotBeNull();
                result.Location!.Id.ShouldBe(officeA.LocationId);
                result.DoctorAvailability.ShouldNotBeNull();
                result.DoctorAvailability!.Id.ShouldBe(officeA.DoctorAvailabilityId);
            }
        });
    }

    [Fact]
    public async Task CreateAsync_WhenInputValid_PersistsAppointmentAndReturnsDto()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(7);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(10, 0), new TimeOnly(11, 0));
            var input = BuildCreateDto(officeA, slotId, date.AddHours(10).AddMinutes(15));

            var created = await _appointmentsAppService.CreateAsync(input);

            created.ShouldNotBeNull();
            created.PatientId.ShouldBe(input.PatientId);
            created.DoctorAvailabilityId.ShouldBe(slotId);
            created.Id.ShouldNotBe(Guid.Empty);
            (await _appointmentRepository.FindAsync(created.Id)).ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task CreateAsync_WhenInputHasIsPatientAlreadyExistTrue_PersistsTrue()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(8);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(10, 0), new TimeOnly(11, 0));
            var input = BuildCreateDto(officeA, slotId, date.AddHours(10).AddMinutes(15));
            input.IsPatientAlreadyExist = true;

            var created = await _appointmentsAppService.CreateAsync(input);

            var persisted = await _appointmentRepository.FindAsync(created.Id);
            persisted!.IsPatientAlreadyExist.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task CreateRevalAsync_LinksRevalChildToSourceViaOriginalAppointmentId()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var sourceConfirmation = "A9REVALSRC";
        var sourceId = Guid.NewGuid();

        await InOfficeAsync(officeA, async () =>
        {
            // Approved source on the seeded slot (the approval gates are out of scope here).
            await _appointmentRepository.InsertAsync(new Appointment(
                id: sourceId,
                patientId: officeA.PatientId,
                identityUserId: officeA.BookerUserId,
                appointmentTypeId: officeA.AppointmentTypeId,
                locationId: officeA.LocationId,
                doctorAvailabilityId: officeA.DoctorAvailabilityId,
                appointmentDate: DateTime.Today.AddDays(-1),
                requestConfirmationNumber: sourceConfirmation,
                appointmentStatus: AppointmentStatusType.Approved), autoSave: true);

            var date = DateTime.Today.AddDays(9);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(13, 0), new TimeOnly(14, 0));
            var input = BuildCreateDto(officeA, slotId, date.AddHours(13).AddMinutes(15));

            var created = await _appointmentsAppService.CreateRevalAsync(sourceConfirmation, input);

            created.Id.ShouldNotBe(sourceId);
            var persisted = await _appointmentRepository.FindAsync(created.Id);
            persisted!.OriginalAppointmentId.ShouldBe(sourceId);
        });
    }

    [Fact]
    public async Task CreateAsync_WhenInputOmitsIsPatientAlreadyExist_DefaultsToFalseOnEntity()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(9);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(14, 0), new TimeOnly(15, 0));
            var input = BuildCreateDto(officeA, slotId, date.AddHours(14).AddMinutes(15));

            var created = await _appointmentsAppService.CreateAsync(input);

            var persisted = await _appointmentRepository.FindAsync(created.Id);
            persisted!.IsPatientAlreadyExist.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task CreateAsync_GeneratesConfirmationNumberInAFormat()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(10);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(10, 0), new TimeOnly(11, 0));
            var input = BuildCreateDto(officeA, slotId, date.AddHours(10).AddMinutes(30));

            var created = await _appointmentsAppService.CreateAsync(input);

            Regex.IsMatch(created.RequestConfirmationNumber, @"^A\d{5}$").ShouldBeTrue();
        });
    }

    [Fact]
    public async Task CreateAsync_TwoSequentialCreates_ProduceIncreasingNumbers()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var dateA = DateTime.Today.AddDays(11);
            var slotA = await InsertSlotAsync(officeA, dateA, new TimeOnly(10, 0), new TimeOnly(11, 0));
            var first = await _appointmentsAppService.CreateAsync(
                BuildCreateDto(officeA, slotA, dateA.AddHours(10).AddMinutes(15)));

            var dateB = DateTime.Today.AddDays(12);
            var slotB = await InsertSlotAsync(officeA, dateB, new TimeOnly(10, 0), new TimeOnly(11, 0));
            var second = await _appointmentsAppService.CreateAsync(
                BuildCreateDto(officeA, slotB, dateB.AddHours(10).AddMinutes(15)));

            var firstNum = int.Parse(first.RequestConfirmationNumber.Substring(1));
            var secondNum = int.Parse(second.RequestConfirmationNumber.Substring(1));
            secondNum.ShouldBeGreaterThan(firstNum);
        });
    }

    [Fact]
    public async Task CreateAsync_LeavesSlotInAvailable_UnderCapacityModel()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(13);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(10, 0), new TimeOnly(11, 0));

            await _appointmentsAppService.CreateAsync(
                BuildCreateDto(officeA, slotId, date.AddHours(10).AddMinutes(15)));

            var slotAfter = await _slotRepository.GetAsync(slotId);
            slotAfter.BookingStatusId.ShouldBe(BookingStatus.Available);
        });
    }

    [Fact]
    public async Task CreateAsync_WhenSlotIsReserved_Throws()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(14);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            await CloseSlotAsync(slotId);

            var input = BuildCreateDto(officeA, slotId, date.AddHours(9).AddMinutes(15));

            var ex = await Should.ThrowAsync<BusinessException>(
                () => _appointmentsAppService.CreateAsync(input));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingSlotClosed);
        });
    }

    [Fact]
    public async Task CreateAsync_WhenSlotLocationMismatch_Throws()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            // Seed a second location, put the slot there, then ask for the original.
            var otherLocationId = await InsertSecondLocationAsync(officeA);
            var date = DateTime.Today.AddDays(15);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(10, 0), new TimeOnly(11, 0), locationId: otherLocationId);

            var input = BuildCreateDto(officeA, slotId, date.AddHours(10).AddMinutes(15));
            input.LocationId = officeA.LocationId; // mismatches the slot's location

            var ex = await Should.ThrowAsync<UserFriendlyException>(
                () => _appointmentsAppService.CreateAsync(input));
            ex.Message.ShouldContain("location");
        });
    }

    [Fact]
    public async Task CreateAsync_WhenSlotDateMismatch_Throws()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(16);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(10, 0), new TimeOnly(11, 0));

            // Request a different calendar day than the slot's date.
            var input = BuildCreateDto(officeA, slotId, date.AddDays(7).AddHours(10).AddMinutes(15));

            var ex = await Should.ThrowAsync<UserFriendlyException>(
                () => _appointmentsAppService.CreateAsync(input));
            ex.Message.ShouldContain("date");
        });
    }

    [Fact]
    public async Task CreateAsync_WhenSlotIsReserved_ThrowsSlotClosed()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(17);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            await CloseSlotAsync(slotId);

            var input = BuildCreateDto(officeA, slotId, date.AddHours(9).AddMinutes(15));

            var ex = await Should.ThrowAsync<BusinessException>(
                () => _appointmentsAppService.CreateAsync(input));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingSlotClosed);
        });
    }

    [Fact]
    public async Task CreateAsync_WhenSlotCapacityIsExhausted_ThrowsSlotFull()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(18);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(9, 0), new TimeOnly(10, 0), capacity: 2);
            await InsertPendingAppointmentAsync(officeA, slotId, date.AddHours(9).AddMinutes(10), "A-CAP-1");
            await InsertPendingAppointmentAsync(officeA, slotId, date.AddHours(9).AddMinutes(20), "A-CAP-2");

            var input = BuildCreateDto(officeA, slotId, date.AddHours(9).AddMinutes(30));

            var ex = await Should.ThrowAsync<BusinessException>(
                () => _appointmentsAppService.CreateAsync(input));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingSlotFull);
            ex.Data["capacity"].ShouldBe(2);
            ex.Data["activeCount"].ShouldBe(2L);
        });
    }

    [Fact]
    public async Task CreateAsync_WhenSlotHasFreedAppointments_DoesNotCountThem()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(19);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(9, 0), new TimeOnly(10, 0), capacity: 1);

            // A Rejected appointment must NOT count toward active capacity.
            await _appointmentRepository.InsertAsync(new Appointment(
                id: Guid.NewGuid(),
                patientId: officeA.PatientId,
                identityUserId: officeA.BookerUserId,
                appointmentTypeId: officeA.AppointmentTypeId,
                locationId: officeA.LocationId,
                doctorAvailabilityId: slotId,
                appointmentDate: date.AddHours(9).AddMinutes(10),
                requestConfirmationNumber: "A-FREED-1",
                appointmentStatus: AppointmentStatusType.Rejected), autoSave: true);

            var result = await _appointmentsAppService.CreateAsync(
                BuildCreateDto(officeA, slotId, date.AddHours(9).AddMinutes(30)));
            result.Id.ShouldNotBe(Guid.Empty);
        });
    }

    [Fact]
    public async Task CreateAsync_WhenSlotTypesEmpty_AnyTypeWorks()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(20);
            // Loose slot: no appointment types -> accepts any.
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(9, 0), new TimeOnly(10, 0),
                types: Array.Empty<Guid>());

            var result = await _appointmentsAppService.CreateAsync(
                BuildCreateDto(officeA, slotId, date.AddHours(9).AddMinutes(15)));
            result.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task CreateAsync_WhenRequestedTypeNotInSlotTypes_ThrowsTypeMismatch()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(21);
            // Slot accepts only the primary type; request the second type.
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildCreateDto(officeA, slotId, date.AddHours(9).AddMinutes(15));
            input.AppointmentTypeId = officeA.SecondAppointmentTypeId;

            var ex = await Should.ThrowAsync<BusinessException>(
                () => _appointmentsAppService.CreateAsync(input));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingSlotTypeMismatch);
        });
    }

    [Fact]
    public async Task CreateAsync_WhenRequestedTypeInSlotTypes_Succeeds()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var date = DateTime.Today.AddDays(22);
            var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(9, 0), new TimeOnly(10, 0),
                types: new[] { officeA.AppointmentTypeId, officeA.SecondAppointmentTypeId });
            var input = BuildCreateDto(officeA, slotId, date.AddHours(9).AddMinutes(15));
            input.AppointmentTypeId = officeA.SecondAppointmentTypeId;

            var result = await _appointmentsAppService.CreateAsync(input);
            result.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task CreateAsync_WhenLeadTimeBlocks_RaisesLeadTimeNotCapacity()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            // Past date: slot is Available + non-full + correct type, so the capacity
            // gate passes; the booking-policy past-date check then throws.
            var pastDate = DateTime.Today.AddDays(-5);
            var slotId = await InsertSlotAsync(officeA, pastDate, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildCreateDto(officeA, slotId, pastDate.AddHours(9).AddMinutes(15));

            // The booking-policy validator rejects a past date (lead time), not capacity.
            await Should.ThrowAsync<BusinessException>(
                () => _appointmentsAppService.CreateAsync(input));
        });
    }

    [Fact]
    public async Task GetAppointmentApplicantAttorneyAsync_ReturnsStoredName_WhenAttorneyHasNoIdentityUser()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            using (WithCurrentUser.Run(_principalAccessor, officeA.BookerUserId))
            {
                var date = DateTime.Today.AddDays(23);
                var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(10, 0), new TimeOnly(11, 0));
                var appointment = await _appointmentsAppService.CreateAsync(
                    BuildCreateDto(officeA, slotId, date.AddHours(10).AddMinutes(15)));

                await _appointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync(appointment.Id,
                    new ApplicantAttorneyDetailsDto
                    {
                        ApplicantAttorneyId = null,
                        IdentityUserId = Guid.Empty,
                        FirstName = "Aria",
                        LastName = "Stone",
                        Email = "aria.synthetic@test.local",
                        FirmName = "Stone & Associates",
                    });

                var result = await _appointmentsAppService.GetAppointmentApplicantAttorneyAsync(appointment.Id);

                result.ShouldNotBeNull();
                result!.FirstName.ShouldBe("Aria");
                result.LastName.ShouldBe("Stone");
                result.FirmName.ShouldBe("Stone & Associates");
                result.IdentityUserId.ShouldBe(Guid.Empty);
            }
        });
    }

    [Fact]
    public async Task GetAppointmentDefenseAttorneyAsync_ReturnsStoredName_WhenAttorneyHasNoIdentityUser()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            using (WithCurrentUser.Run(_principalAccessor, officeA.BookerUserId))
            {
                var date = DateTime.Today.AddDays(24);
                var slotId = await InsertSlotAsync(officeA, date, new TimeOnly(10, 0), new TimeOnly(11, 0));
                var appointment = await _appointmentsAppService.CreateAsync(
                    BuildCreateDto(officeA, slotId, date.AddHours(10).AddMinutes(15)));

                await _appointmentsAppService.UpsertDefenseAttorneyForAppointmentAsync(appointment.Id,
                    new DefenseAttorneyDetailsDto
                    {
                        DefenseAttorneyId = null,
                        IdentityUserId = Guid.Empty,
                        FirstName = "Dana",
                        LastName = "Defense",
                        Email = "dana.synthetic@test.local",
                        FirmName = "Shield Defense Group",
                    });

                var result = await _appointmentsAppService.GetAppointmentDefenseAttorneyAsync(appointment.Id);

                result.ShouldNotBeNull();
                result!.FirstName.ShouldBe("Dana");
                result.LastName.ShouldBe("Defense");
                result.FirmName.ShouldBe("Shield Defense Group");
                result.IdentityUserId.ShouldBe(Guid.Empty);
            }
        });
    }

    // ----- helpers (caller has already entered the office's tenant + a UoW) -----

    private Task InOfficeAsync(SeededOffice office, Func<Task> body) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                await body();
            }
        }, requiresNew: true);

    private async Task<Guid> InsertSlotAsync(
        SeededOffice office, DateTime date, TimeOnly from, TimeOnly to,
        Guid? locationId = null, int capacity = 3, Guid[]? types = null)
    {
        var slot = new DoctorAvailability(
            id: Guid.NewGuid(),
            locationId: locationId ?? office.LocationId,
            availableDate: date,
            fromTime: from,
            toTime: to,
            bookingStatusId: BookingStatus.Available,
            capacity: capacity);
        slot.TenantId = office.OfficeId;
        foreach (var typeId in types ?? new[] { office.AppointmentTypeId })
        {
            slot.AddAppointmentType(typeId);
        }
        await _slotRepository.InsertAsync(slot, autoSave: true);
        return slot.Id;
    }

    private async Task CloseSlotAsync(Guid slotId)
    {
        var slot = await _slotRepository.GetAsync(slotId);
        slot.BookingStatusId = BookingStatus.Reserved;
        await _slotRepository.UpdateAsync(slot, autoSave: true);
    }

    private async Task<Guid> InsertSecondLocationAsync(SeededOffice office)
    {
        var locationRepository = GetRequiredService<IRepository<Locations.Location, Guid>>();
        var location = new Locations.Location(
            id: Guid.NewGuid(),
            stateId: null,
            name: "TEST-Clinic-2",
            parkingFee: 0m,
            isActive: true);
        await locationRepository.InsertAsync(location, autoSave: true);
        return location.Id;
    }

    private async Task InsertPendingAppointmentAsync(
        SeededOffice office, Guid slotId, DateTime date, string confirmation)
    {
        await _appointmentRepository.InsertAsync(new Appointment(
            id: Guid.NewGuid(),
            patientId: office.PatientId,
            identityUserId: office.BookerUserId,
            appointmentTypeId: office.AppointmentTypeId,
            locationId: office.LocationId,
            doctorAvailabilityId: slotId,
            appointmentDate: date,
            requestConfirmationNumber: confirmation,
            appointmentStatus: AppointmentStatusType.Pending), autoSave: true);
    }

    private static AppointmentCreateDto BuildCreateDto(SeededOffice office, Guid slotId, DateTime date) =>
        new()
        {
            PatientId = office.PatientId,
            IdentityUserId = office.BookerUserId,
            AppointmentTypeId = office.AppointmentTypeId,
            LocationId = office.LocationId,
            DoctorAvailabilityId = slotId,
            AppointmentDate = date,
            RequestConfirmationNumber = "ignored-by-server",
            AppointmentStatus = AppointmentStatusType.Pending,
            PanelNumber = null,
            DueDate = null,
        };
}
