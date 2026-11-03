using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// The patient hears nothing before approval unless the patient submitted the request (2026-11-03).
/// The patient is found by the appointment's patient addresses, never by the Patient recipient role,
/// which the resolver also hands to a submitter who is not a named party. Synthetic data only.
/// </summary>
public class PatientBeforeApprovalRuleTests
{
    private static DocumentEmailContext Context(bool patientIsSubmitter) => new()
    {
        PatientAddresses = new[] { "TEST-patient@test.local", "TEST-patient-column@test.local" },
        PatientIsSubmitter = patientIsSubmitter,
    };

    private static List<NotificationRecipient> Recipients() => new()
    {
        new(email: "TEST-PATIENT@test.local", role: RecipientRole.Patient),
        new(email: "TEST-patient-column@test.local", role: RecipientRole.Patient),
        new(email: "TEST-submitter@test.local", role: RecipientRole.Patient), // a non-party submitter carries the Patient tag
        new(email: "TEST-aa@test.local", role: RecipientRole.ApplicantAttorney),
        new(email: "TEST-inbox@test.local", role: RecipientRole.OfficeAdmin),
    };

    [Fact]
    public void WhenSomeoneElseSubmitted_TheTwoPatientAddressesAreRemoved_AndNobodyElse()
    {
        var kept = PatientBeforeApprovalRule.Apply(Recipients(), Context(patientIsSubmitter: false));

        kept.Select(r => r.Email).ShouldBe(
            new[] { "TEST-submitter@test.local", "TEST-aa@test.local", "TEST-inbox@test.local" });
    }

    [Fact]
    public void ARecipientTaggedPatient_IsKept_WhenItIsNotThePatientsAddress()
    {
        PatientBeforeApprovalRule.Apply(Recipients(), Context(false))
            .ShouldContain(r => r.Email == "TEST-submitter@test.local");
    }

    [Fact]
    public void WhenThePatientSubmitted_NobodyIsRemoved()
    {
        PatientBeforeApprovalRule.Apply(Recipients(), Context(patientIsSubmitter: true)).Count.ShouldBe(5);
    }

    [Fact]
    public void TheResolverArgsOverload_AppliesTheSameRule()
    {
        var args = new List<SendAppointmentEmailArgs>
        {
            new() { To = "test-patient@test.local", Role = RecipientRole.Patient },
            new() { To = "TEST-aa@test.local", Role = RecipientRole.ApplicantAttorney },
        };

        PatientBeforeApprovalRule.Apply(args, Context(false)).Select(r => r.To).ShouldBe(new[] { "TEST-aa@test.local" });
        PatientBeforeApprovalRule.Apply(args, Context(true)).Count.ShouldBe(2);
    }
}
