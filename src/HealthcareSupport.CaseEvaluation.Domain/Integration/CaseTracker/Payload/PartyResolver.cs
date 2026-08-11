using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Resolves the two human parties on the payload: the patient, and the office's doctor.
///
/// <para>The doctor is NOT an appointment field. Each office has exactly one doctor (tenant ==
/// doctor, enforced by <c>IX_AppEntity_Doctors_TenantId_Unique</c>), so it is read as "the single
/// Doctor row in this office's database". <c>FirstName</c> can legitimately be empty on the
/// placeholder seed path, so an empty value is passed through rather than treated as missing.</para>
/// </summary>
public class PartyResolver : ITransientDependency
{
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<Doctor, Guid> _doctorRepository;
    private readonly IRepository<State, Guid> _stateRepository;

    public PartyResolver(
        IRepository<Patient, Guid> patientRepository,
        IRepository<Doctor, Guid> doctorRepository,
        IRepository<State, Guid> stateRepository)
    {
        _patientRepository = patientRepository;
        _doctorRepository = doctorRepository;
        _stateRepository = stateRepository;
    }

    public virtual async Task<IntakePatientSection> ResolvePatientAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        if (appointment is null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }

        var patient = await _patientRepository.FindAsync(appointment.PatientId, cancellationToken: cancellationToken);
        if (patient == null)
        {
            return new IntakePatientSection();
        }

        return new IntakePatientSection
        {
            FirstName = patient.FirstName,
            MiddleName = patient.MiddleName,
            LastName = patient.LastName,
            Email = patient.Email,
            DateOfBirth = IntegrationTimestamp.ToDateOnly(patient.DateOfBirth),
            PhoneNumber = patient.PhoneNumber,
            PhoneNumberType = patient.PhoneNumberTypeId.ToString(),
            CellPhoneNumber = patient.CellPhoneNumber,
            // Phase 6 (2026-08-08). The column names are BACKWARDS relative to what they hold:
            // Patient.Street is street line 1 and Patient.Address is the "Unit #" the booking form
            // asks for. Mapped by MEANING, not by matching names, so the receiver never renders a
            // bare unit number as a street.
            Street = patient.Street,
            Unit = patient.Address,
            City = patient.City,
            State = await ResolveStateNameAsync(patient.StateId, cancellationToken),
            ZipCode = patient.ZipCode,
            // Hashed and office-salted, never the raw Patient.Id -- see SamePersonGroupKey for why.
            SamePersonGroupKey = SamePersonGroupKey.Compute(appointment.TenantId, patient.Id),
        };
    }

    /// <summary>
    /// The state's NAME, or null when the patient has none or the id does not resolve.
    ///
    /// <para>A single lookup rather than the batched dictionary <see cref="PartyDetailResolver"/>
    /// builds: that one exists because it resolves state for many parties at once, whereas a
    /// patient has exactly one. Null rather than empty string, matching how the attorney sections
    /// publish an absent state.</para>
    /// </summary>
    private async Task<string?> ResolveStateNameAsync(Guid? stateId, CancellationToken cancellationToken)
    {
        if (stateId is not { } id)
        {
            return null;
        }

        var state = await _stateRepository.FindAsync(id, cancellationToken: cancellationToken);
        return state?.Name;
    }

    public virtual async Task<IntakeDoctorSection> ResolveDoctorAsync(CancellationToken cancellationToken = default)
    {
        // One doctor per office; the tenant filter scopes this to the current office's database.
        var queryable = await _doctorRepository.GetQueryableAsync();
        var doctor = queryable.OrderBy(d => d.CreationTime).FirstOrDefault();

        return doctor == null
            ? new IntakeDoctorSection()
            : new IntakeDoctorSection
            {
                Id = doctor.Id,
                FirstName = doctor.FirstName,
                LastName = doctor.LastName,
            };
    }
}
