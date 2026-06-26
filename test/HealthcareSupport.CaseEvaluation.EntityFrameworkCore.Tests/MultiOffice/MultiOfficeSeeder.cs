using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Identifiers of the rows the multi-office seeder created for one office, so a test
/// can reference them when asserting that a DIFFERENT office cannot reach them.
/// </summary>
public record SeededOffice(
    Guid OfficeId,
    Guid AppointmentTypeId,
    Guid StateId,
    Guid LocationId,
    Guid DoctorAvailabilityId,
    Guid PatientId,
    Guid AppointmentId,
    string PatientEmail,
    string PatientSsnSentinel);

/// <summary>
/// Seeds a minimal but coherent dataset -- one catalog set plus a single operational
/// chain (appointment type, state, location, availability slot, patient, appointment)
/// -- into ONE office's database. Catalogs are seeded inside the office's tenant
/// context (they are IMultiTenant per office since Phase A), which is exactly what the
/// single-database integration seeder cannot do and why the catalog tests were skipped.
///
/// Caller must run this inside a unit of work; the seeder switches CurrentTenant to the
/// office so ABP routes every insert to that office's database and stamps TenantId.
/// All synthetic data (HIPAA): the "SSN" field holds a non-numeric sentinel token, not a
/// real-format SSN, so leak tests can assert it is never returned to a non-owner.
/// </summary>
public class MultiOfficeSeeder : ITransientDependency
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly IRepository<State, Guid> _stateRepository;
    private readonly IRepository<Location, Guid> _locationRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;

    public MultiOfficeSeeder(
        ICurrentTenant currentTenant,
        IRepository<AppointmentType, Guid> appointmentTypeRepository,
        IRepository<State, Guid> stateRepository,
        IRepository<Location, Guid> locationRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<Appointment, Guid> appointmentRepository)
    {
        _currentTenant = currentTenant;
        _appointmentTypeRepository = appointmentTypeRepository;
        _stateRepository = stateRepository;
        _locationRepository = locationRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _patientRepository = patientRepository;
        _appointmentRepository = appointmentRepository;
    }

    /// <param name="label">Short, distinct token woven into names/email so the two
    /// offices' rows are easy to tell apart in failures.</param>
    public async Task<SeededOffice> SeedAsync(Guid officeId, string label)
    {
        var appointmentTypeId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var patientEmail = $"patient.{label}@example.test";
        var ssnSentinel = $"SSN-SENTINEL-{label}";

        using (_currentTenant.Change(officeId))
        {
            // Catalogs first (FK targets for the operational chain).
            await _appointmentTypeRepository.InsertAsync(
                new AppointmentType(appointmentTypeId, $"TEST-IME-{label}"), autoSave: true);
            await _stateRepository.InsertAsync(
                new State(stateId, $"TEST-State-{label}"), autoSave: true);

            var location = new Location(
                id: locationId,
                stateId: stateId,
                name: $"TEST-Clinic-{label}",
                parkingFee: 0m,
                isActive: true);
            location.AddAppointmentType(appointmentTypeId);
            await _locationRepository.InsertAsync(location, autoSave: true);

            var slot = new DoctorAvailability(
                id: slotId,
                locationId: locationId,
                availableDate: new DateTime(2026, 3, 1),
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Booked);
            slot.TenantId = officeId;
            slot.AddAppointmentType(appointmentTypeId);
            await _doctorAvailabilityRepository.InsertAsync(slot, autoSave: true);

            // Patient is the PHI-bearing entity and is NOT IMultiTenant, so isolation
            // here rests on the office living in a separate database (and the explicit
            // repo filter), not on ABP's tenant query filter.
            await _patientRepository.InsertAsync(new Patient(
                id: patientId,
                stateId: null,
                appointmentLanguageId: null,
                identityUserId: null,
                tenantId: officeId,
                firstName: $"Pat-{label}",
                lastName: "Synthetic",
                email: patientEmail,
                genderId: Gender.Unspecified,
                dateOfBirth: new DateTime(1990, 1, 1),
                phoneNumberTypeId: PhoneNumberType.Work,
                socialSecurityNumber: ssnSentinel), autoSave: true);

            await _appointmentRepository.InsertAsync(new Appointment(
                id: appointmentId,
                patientId: patientId,
                identityUserId: null,
                appointmentTypeId: appointmentTypeId,
                locationId: locationId,
                doctorAvailabilityId: slotId,
                appointmentDate: new DateTime(2026, 3, 1, 9, 0, 0),
                requestConfirmationNumber: $"RCN-{label}",
                appointmentStatus: AppointmentStatusType.Pending), autoSave: true);
        }

        return new SeededOffice(
            officeId, appointmentTypeId, stateId, locationId, slotId, patientId,
            appointmentId, patientEmail, ssnSentinel);
    }
}
