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
    private readonly AppointmentPatientSnapshotResolver _patientSnapshotResolver;
    private readonly IRepository<Doctor, Guid> _doctorRepository;
    private readonly IRepository<State, Guid> _stateRepository;

    public PartyResolver(
        AppointmentPatientSnapshotResolver patientSnapshotResolver,
        IRepository<Doctor, Guid> doctorRepository,
        IRepository<State, Guid> stateRepository)
    {
        _patientSnapshotResolver = patientSnapshotResolver;
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

        // Item 5 (2026-08-14): the payload is the RECORD of what was served, so it reads the
        // booked-time snapshot rather than the live patient. Before this, editing a patient
        // silently rewrote what every one of their PRIOR appointments reported to the Case
        // Tracker. Appointments booked before the snapshot shipped fall back to the live row
        // inside the resolver -- they were deliberately not backfilled.
        var patient = await _patientSnapshotResolver.ResolveAsync(appointment, cancellationToken);
        if (patient == null)
        {
            return new IntakePatientSection();
        }

        return new IntakePatientSection
        {
            // The snapshot type is nullable throughout because the columns are; the WIRE
            // contract keeps these five non-null, so they coalesce to the same empty-string
            // default the section already declared. A snapshot written by ApplyPatientSnapshot
            // always carries them -- Patient.FirstName / LastName / Email / DateOfBirth /
            // PhoneNumberTypeId are all non-nullable on the source row.
            FirstName = patient.FirstName ?? string.Empty,
            MiddleName = patient.MiddleName,
            LastName = patient.LastName ?? string.Empty,
            Email = patient.Email ?? string.Empty,
            DateOfBirth = patient.DateOfBirth.HasValue
                ? IntegrationTimestamp.ToDateOnly(patient.DateOfBirth.Value)
                : string.Empty,
            PhoneNumber = patient.PhoneNumber,
            PhoneNumberType = patient.PhoneNumberTypeId?.ToString() ?? string.Empty,
            CellPhoneNumber = patient.CellPhoneNumber,
            // Phase 6 (2026-08-08). The column names are BACKWARDS relative to what they hold:
            // Patient.Street is street line 1, and the "Unit #" the forms ask for is NOT Street.
            // Mapped by MEANING, not by matching names, so the receiver never renders a bare unit
            // number as a street.
            Street = patient.Street,
            // 2026-08-13: the unit lived in TWO columns. Every staff screen writes ApptNumber; the
            // booking wizard and send-back wrote Address. Sending Address alone meant a staff
            // CORRECTION never reached the Case Tracker while the stale booking-time value kept
            // going out -- found by the phase 6 live gate. Both writers now target ApptNumber.
            // The ApptNumber-then-Address coalesce now lives in the snapshot resolver's live-read
            // branch, so it still covers rows booked before either fix.
            Unit = patient.Unit,
            City = patient.City,
            State = await ResolveStateNameAsync(patient.StateId, cancellationToken),
            ZipCode = patient.ZipCode,
            // Hashed and office-salted, never the raw Patient.Id -- see SamePersonGroupKey for why.
            // Taken from the APPOINTMENT, not the snapshot: the group key must keep identifying the
            // same person across claims, so it is deliberately not frozen with the demographics.
            SamePersonGroupKey = SamePersonGroupKey.Compute(appointment.TenantId, appointment.PatientId),
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
