using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Patients;
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

    public PartyResolver(
        IRepository<Patient, Guid> patientRepository,
        IRepository<Doctor, Guid> doctorRepository)
    {
        _patientRepository = patientRepository;
        _doctorRepository = doctorRepository;
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
            // Hashed and office-salted, never the raw Patient.Id -- see SamePersonGroupKey for why.
            SamePersonGroupKey = SamePersonGroupKey.Compute(appointment.TenantId, patient.Id),
        };
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
                FirstName = doctor.FirstName,
                LastName = doctor.LastName,
            };
    }
}
