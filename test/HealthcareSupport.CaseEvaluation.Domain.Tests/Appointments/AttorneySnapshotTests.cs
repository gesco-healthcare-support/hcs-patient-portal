using System;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pins the #9 booking-time attorney snapshot: CaptureApplicant / CaptureDefense
/// copy every displayed name/firm/contact field from the master onto the
/// appointment, so a later master self-edit cannot rewrite this appointment.
/// Synthetic non-PHI values.
/// </summary>
public class AttorneySnapshotTests
{
    private static Appointment NewAppointment() =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateTime(2026, 6, 16), "A00001", AppointmentStatusType.Pending);

    [Fact]
    public void CaptureApplicant_copies_all_displayed_fields()
    {
        var appt = NewAppointment();
        var stateId = Guid.NewGuid();
        var master = new ApplicantAttorney(Guid.NewGuid(), stateId, null)
        {
            FirstName = "Avery",
            LastName = "Applicant",
            FirmName = "Avery Law",
            WebAddress = "avery.example",
            PhoneNumber = "555-0100",
            FaxNumber = "555-0101",
            Street = "1 A St",
            City = "Oakland",
            ZipCode = "94607",
            StateId = stateId,
        };

        AttorneySnapshot.CaptureApplicant(appt, master);

        appt.ApplicantAttorneyFirstName.ShouldBe("Avery");
        appt.ApplicantAttorneyLastName.ShouldBe("Applicant");
        appt.ApplicantAttorneyFirmName.ShouldBe("Avery Law");
        appt.ApplicantAttorneyWebAddress.ShouldBe("avery.example");
        appt.ApplicantAttorneyPhoneNumber.ShouldBe("555-0100");
        appt.ApplicantAttorneyFaxNumber.ShouldBe("555-0101");
        appt.ApplicantAttorneyStreet.ShouldBe("1 A St");
        appt.ApplicantAttorneyCity.ShouldBe("Oakland");
        appt.ApplicantAttorneyZipCode.ShouldBe("94607");
        appt.ApplicantAttorneyStateId.ShouldBe(stateId);
    }

    [Fact]
    public void CaptureDefense_copies_all_displayed_fields()
    {
        var appt = NewAppointment();
        var stateId = Guid.NewGuid();
        var master = new DefenseAttorney(Guid.NewGuid(), stateId, null)
        {
            FirstName = "Dana",
            LastName = "Defense",
            FirmName = "Dana Defense LLP",
            WebAddress = "dana.example",
            PhoneNumber = "555-0200",
            FaxNumber = "555-0201",
            Street = "2 D Ave",
            City = "Fresno",
            ZipCode = "93701",
            StateId = stateId,
        };

        AttorneySnapshot.CaptureDefense(appt, master);

        appt.DefenseAttorneyFirstName.ShouldBe("Dana");
        appt.DefenseAttorneyLastName.ShouldBe("Defense");
        appt.DefenseAttorneyFirmName.ShouldBe("Dana Defense LLP");
        appt.DefenseAttorneyWebAddress.ShouldBe("dana.example");
        appt.DefenseAttorneyPhoneNumber.ShouldBe("555-0200");
        appt.DefenseAttorneyFaxNumber.ShouldBe("555-0201");
        appt.DefenseAttorneyStreet.ShouldBe("2 D Ave");
        appt.DefenseAttorneyCity.ShouldBe("Fresno");
        appt.DefenseAttorneyZipCode.ShouldBe("93701");
        appt.DefenseAttorneyStateId.ShouldBe(stateId);
    }
}
