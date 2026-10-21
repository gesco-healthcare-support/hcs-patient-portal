using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// #9 (2026-06-19): copies an attorney master's displayed name/firm/contact fields
/// onto the Appointment as a booking-time snapshot. Pure (no DI) so the field set is
/// unit-tested directly. The appointment-attorney link managers call this whenever an
/// attorney is linked to or re-set on an appointment (booking, staff edit, and the
/// external-signup auto-link all funnel through those managers). A master self-edit
/// does NOT call this, so it never rewrites an existing appointment's snapshot.
/// Email is already snapshotted separately on the Appointment at booking.
/// </summary>
public static class AttorneySnapshot
{
    public static void CaptureApplicant(Appointment appointment, ApplicantAttorney master)
    {
        appointment.ApplicantAttorneyFirstName = master.FirstName;
        appointment.ApplicantAttorneyLastName = master.LastName;
        appointment.ApplicantAttorneyFirmName = master.FirmName;
        appointment.ApplicantAttorneyWebAddress = master.WebAddress;
        appointment.ApplicantAttorneyPhoneNumber = master.PhoneNumber;
        appointment.ApplicantAttorneyFaxNumber = master.FaxNumber;
        appointment.ApplicantAttorneyStreet = master.Street;
        appointment.ApplicantAttorneyCity = master.City;
        appointment.ApplicantAttorneyStateId = master.StateId;
        appointment.ApplicantAttorneyZipCode = master.ZipCode;
    }

    public static void CaptureDefense(Appointment appointment, DefenseAttorney master)
    {
        appointment.DefenseAttorneyFirstName = master.FirstName;
        appointment.DefenseAttorneyLastName = master.LastName;
        appointment.DefenseAttorneyFirmName = master.FirmName;
        appointment.DefenseAttorneyWebAddress = master.WebAddress;
        appointment.DefenseAttorneyPhoneNumber = master.PhoneNumber;
        appointment.DefenseAttorneyFaxNumber = master.FaxNumber;
        appointment.DefenseAttorneyStreet = master.Street;
        appointment.DefenseAttorneyCity = master.City;
        appointment.DefenseAttorneyStateId = master.StateId;
        appointment.DefenseAttorneyZipCode = master.ZipCode;
    }

    /// <summary>
    /// 2026-10-21: staff removed the applicant attorney from the appointment, so the snapshot no
    /// longer describes anyone on it. Same field set as <see cref="CaptureApplicant"/>.
    /// </summary>
    public static void ClearApplicant(Appointment appointment)
    {
        appointment.ApplicantAttorneyFirstName = null;
        appointment.ApplicantAttorneyLastName = null;
        appointment.ApplicantAttorneyFirmName = null;
        appointment.ApplicantAttorneyWebAddress = null;
        appointment.ApplicantAttorneyPhoneNumber = null;
        appointment.ApplicantAttorneyFaxNumber = null;
        appointment.ApplicantAttorneyStreet = null;
        appointment.ApplicantAttorneyCity = null;
        appointment.ApplicantAttorneyStateId = null;
        appointment.ApplicantAttorneyZipCode = null;
    }

    /// <summary>The defense-side twin of <see cref="ClearApplicant"/>.</summary>
    public static void ClearDefense(Appointment appointment)
    {
        appointment.DefenseAttorneyFirstName = null;
        appointment.DefenseAttorneyLastName = null;
        appointment.DefenseAttorneyFirmName = null;
        appointment.DefenseAttorneyWebAddress = null;
        appointment.DefenseAttorneyPhoneNumber = null;
        appointment.DefenseAttorneyFaxNumber = null;
        appointment.DefenseAttorneyStreet = null;
        appointment.DefenseAttorneyCity = null;
        appointment.DefenseAttorneyStateId = null;
        appointment.DefenseAttorneyZipCode = null;
    }
}
