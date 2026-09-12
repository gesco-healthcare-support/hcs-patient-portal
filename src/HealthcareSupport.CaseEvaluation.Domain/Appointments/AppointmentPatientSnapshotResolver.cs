using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The patient's demographics AS THEY WERE when the appointment was booked.
///
/// <para>Every field is nullable because the live <see cref="Patient"/> row allows
/// nulls on all but the names; a null here means "not captured", never "cleared".</para>
/// </summary>
public sealed record AppointmentPatientSnapshot(
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Email,
    DateTime? DateOfBirth,
    string? SocialSecurityNumber,
    string? PhoneNumber,
    string? CellPhoneNumber,
    PhoneNumberType? PhoneNumberTypeId,
    string? Street,
    string? Unit,
    string? City,
    Guid? StateId,
    string? ZipCode,
    Gender? GenderId,
    string? InterpreterVendorName);

/// <summary>
/// Item 5 (2026-08-14) -- resolves the patient values a RECORD-side reader should
/// report for an appointment: the booked-time snapshot when the appointment carries
/// one, the live patient row when it does not.
///
/// <para><b>Why this exists at all.</b> Adrian's principle: "Previous appointment
/// details should not be changed based on this new appointment, that old appointment
/// should stay as it is because that is a log/legal trail of what has happened till
/// now." Attorneys, employers, insurers and claim examiners already satisfied that --
/// attorneys through ~20 denormalised columns on the appointment, the rest through
/// per-appointment child rows. The patient was the sole exception, read live, so a
/// single address correction rewrote what EVERY prior appointment reported, both to
/// the Case Tracker and on any regenerated packet. That was a pre-existing defect;
/// the booking work only made it easier to trigger.</para>
///
/// <para><b>Who should call this.</b> Record-side readers only -- the Case Tracker
/// payload and generated documents, because those are the legal record of what was
/// served. CONTACT-side readers must keep reading the live patient: a reminder has to
/// reach the address the patient has TODAY, not the one they had in May. Treating the
/// two the same is how this gets built wrong.</para>
///
/// <para><b>The fallback is permanent, not transitional.</b> Appointments booked
/// before the snapshot shipped were deliberately not backfilled -- we cannot know what
/// a patient's details were at the time, and stamping today's values onto a record
/// whose purpose is being a legal trail would assert a history we cannot support. Old
/// rows therefore keep their current (live-read) behaviour for good.</para>
/// </summary>
public class AppointmentPatientSnapshotResolver : ITransientDependency
{
    private readonly IRepository<Patient, Guid> _patientRepository;

    public AppointmentPatientSnapshotResolver(IRepository<Patient, Guid> patientRepository)
    {
        _patientRepository = patientRepository;
    }

    /// <summary>
    /// Returns the values to report for <paramref name="appointment"/>, or null when the
    /// appointment predates the snapshot AND its patient row cannot be found.
    /// </summary>
    public async Task<AppointmentPatientSnapshot?> ResolveAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        if (appointment is null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }

        if (HasSnapshot(appointment))
        {
            return new AppointmentPatientSnapshot(
                FirstName: appointment.PatientFirstName,
                MiddleName: appointment.PatientMiddleName,
                LastName: appointment.PatientLastName,
                // PatientEmail predates this block and is written by the same create /
                // update path, so it is already a booked-time copy.
                Email: appointment.PatientEmail,
                DateOfBirth: appointment.PatientDateOfBirth,
                SocialSecurityNumber: appointment.PatientSocialSecurityNumber,
                PhoneNumber: appointment.PatientPhoneNumber,
                CellPhoneNumber: appointment.PatientCellPhoneNumber,
                PhoneNumberTypeId: appointment.PatientPhoneNumberTypeId,
                Street: appointment.PatientStreet,
                Unit: appointment.PatientApptNumber,
                City: appointment.PatientCity,
                StateId: appointment.PatientStateId,
                ZipCode: appointment.PatientZipCode,
                GenderId: appointment.PatientGenderId,
                InterpreterVendorName: appointment.PatientInterpreterVendorName);
        }

        var patient = await _patientRepository.FindAsync(
            appointment.PatientId, cancellationToken: cancellationToken);
        if (patient == null)
        {
            return null;
        }

        return new AppointmentPatientSnapshot(
            FirstName: patient.FirstName,
            MiddleName: patient.MiddleName,
            LastName: patient.LastName,
            Email: patient.Email,
            DateOfBirth: patient.DateOfBirth,
            SocialSecurityNumber: patient.SocialSecurityNumber,
            PhoneNumber: patient.PhoneNumber,
            CellPhoneNumber: patient.CellPhoneNumber,
            PhoneNumberTypeId: patient.PhoneNumberTypeId,
            Street: patient.Street,
            // The unit lived in TWO columns historically: every staff screen writes
            // ApptNumber, while the booking wizard and send-back used to write Address.
            // ApptNumber wins because it is where corrections land. This fallback is for
            // HISTORY only -- drop it once no row holds a unit in Address.
            Unit: patient.ApptNumber ?? patient.Address,
            City: patient.City,
            StateId: patient.StateId,
            ZipCode: patient.ZipCode,
            GenderId: patient.GenderId,
            InterpreterVendorName: patient.InterpreterVendorName);
    }

    /// <summary>
    /// Whether the appointment carries a booked-time copy.
    ///
    /// <para>Keyed on <see cref="Appointment.PatientLastName"/> because
    /// <see cref="Patient.LastName"/> is non-nullable, so any snapshot the write path
    /// produced populates it. A snapshot with a blank surname is not a state the write
    /// path can produce.</para>
    /// </summary>
    private static bool HasSnapshot(Appointment appointment)
    {
        return appointment.PatientLastName != null;
    }
}
