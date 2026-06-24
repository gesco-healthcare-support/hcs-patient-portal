using System;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 14b (2026-05-04) -- unit tests for the variable-bag
/// builder used across the four document email handlers. Pins the
/// expected key set + null/empty handling so a future template
/// reference to a variable name fails compile-time (via the dictionary
/// key checks).
/// </summary>
public class DocumentNotificationContextUnitTests
{
    [Fact]
    public void BuildVariables_AllFieldsSupplied_PopulatesEveryKey()
    {
        var vars = DocumentNotificationContext.BuildVariables(
            patientFirstName: "Jane",
            patientLastName: "Doe",
            patientEmail: "jane@example.com",
            requestConfirmationNumber: "A00042",
            appointmentDate: new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
            claimNumber: "WC-12345",
            wcabAdj: "ADJ-9999",
            documentName: "Medical history form",
            rejectionNotes: "Photo too blurry",
            clinicName: "Demo Clinic",
            portalUrl: "https://app.example.com");

        vars["PatientFirstName"].ShouldBe("Jane");
        vars["PatientLastName"].ShouldBe("Doe");
        vars["PatientFullName"].ShouldBe("Jane Doe");
        vars["PatientEmail"].ShouldBe("jane@example.com");
        vars["AppointmentRequestConfirmationNumber"].ShouldBe("A00042");
        vars["AppointmentDate"].ShouldBe("07/15/2026");
        vars["ClaimNumber"].ShouldBe("WC-12345");
        vars["WcabAdj"].ShouldBe("ADJ-9999");
        vars["DocumentName"].ShouldBe("Medical history form");
        vars["RejectionNotes"].ShouldBe("Photo too blurry");
        vars["ClinicName"].ShouldBe("Demo Clinic");
        vars["PortalUrl"].ShouldBe("https://app.example.com");
        vars["EmailSubjectIdentity"].ShouldBe("(Patient: Jane Doe - Claim: WC-12345 - ADJ: ADJ-9999)");
    }

    [Fact]
    public void BuildVariables_AllNull_RendersEmptyStrings()
    {
        var vars = DocumentNotificationContext.BuildVariables(
            patientFirstName: null,
            patientLastName: null,
            patientEmail: null,
            requestConfirmationNumber: null,
            appointmentDate: null,
            claimNumber: null,
            wcabAdj: null,
            documentName: null,
            rejectionNotes: null,
            clinicName: null,
            portalUrl: null);

        vars["PatientFullName"].ShouldBe(string.Empty);
        vars["AppointmentDate"].ShouldBe(string.Empty);
        vars["RejectionNotes"].ShouldBe(string.Empty);
        vars["EmailSubjectIdentity"].ShouldBe(string.Empty);
    }

    [Fact]
    public void BuildVariables_FullNameJoinsFirstAndLast()
    {
        var vars = DocumentNotificationContext.BuildVariables(
            patientFirstName: "  Jane  ",
            patientLastName: "Doe",
            patientEmail: null,
            requestConfirmationNumber: null,
            appointmentDate: null,
            claimNumber: null,
            wcabAdj: null,
            documentName: null,
            rejectionNotes: null,
            clinicName: null,
            portalUrl: null);

        vars["PatientFullName"].ShouldBe("Jane Doe");
    }

    [Fact]
    public void BuildVariables_DateFormatIsInvariantMmddYyyy()
    {
        // OLD-parity: MM/dd/yyyy.
        var vars = DocumentNotificationContext.BuildVariables(
            patientFirstName: null,
            patientLastName: null,
            patientEmail: null,
            requestConfirmationNumber: null,
            appointmentDate: new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            claimNumber: null,
            wcabAdj: null,
            documentName: null,
            rejectionNotes: null,
            clinicName: null,
            portalUrl: null);

        vars["AppointmentDate"].ShouldBe("01/05/2026");
    }
}
