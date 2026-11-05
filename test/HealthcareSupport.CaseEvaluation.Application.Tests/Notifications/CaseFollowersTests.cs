using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Unit coverage for the case-follower helpers (<see cref="CaseFollowers"/>, 2026-11-04): appending the
/// followers to each of the two recipient list types, the de-duplication by address, the rule that a
/// follower is never tagged as the office, the patient-role rule (a Patient-role accessor is dropped when the
/// patient is not allowed, the booker never is), the consent-information variant that never includes the
/// patient, the role mapping and the packet gate. Pure: no database. Synthetic data only (HIPAA).
/// </summary>
public class CaseFollowersTests
{
    private const string AccessorEmail = "TEST-follower-accessor@test.local";
    private const string BookerEmail = "TEST-follower-booker@test.local";
    private const string PatientAccessorEmail = "TEST-follower-patient-accessor@test.local";
    private const string PatientEmail = "TEST-follower-patient@test.local";

    private static CaseFollower Follower(string email, bool booker, params string[] roles) =>
        new(email, Guid.NewGuid(), booker, CaseFollowers.IsPatientRole(roles), CaseFollowers.RoleOf(roles), roles);

    private static DocumentEmailContext Context(params CaseFollower[] followers) => new()
    {
        Followers = followers,
        PatientAddresses = new[] { PatientEmail },
    };

    private static List<SendAppointmentEmailArgs> Args(params string[] emails) =>
        emails.Select(e => new SendAppointmentEmailArgs { To = e, Role = RecipientRole.Patient }).ToList();

    private static List<NotificationRecipient> Recipients(params string[] emails) =>
        emails.Select(e => new NotificationRecipient(email: e, role: RecipientRole.Patient)).ToList();

    // ---- Append (SendAppointmentEmailArgs) ---------------------------------------------------------

    [Fact]
    public void Append_Args_AddsEachFollower_TaggedByRole_Registered_NeverAsTheOffice()
    {
        var ctx = Context(
            Follower(AccessorEmail, booker: false, "Applicant Attorney"),
            Follower(BookerEmail, booker: true, "Claim Examiner"));

        var result = CaseFollowers.Append(Args("TEST-party@test.local"), ctx, patientRoleAllowed: true);

        result.Select(r => r.To).ShouldBe(new[] { "TEST-party@test.local", AccessorEmail, BookerEmail });
        result.Single(r => r.To == AccessorEmail).Role.ShouldBe(RecipientRole.ApplicantAttorney);
        result.Single(r => r.To == BookerEmail).Role.ShouldBe(RecipientRole.ClaimExaminer);
        result.Skip(1).ShouldAllBe(r => r.IsRegistered && r.Role != RecipientRole.OfficeAdmin);
    }

    [Fact]
    public void Append_Args_SkipsAFollowerWhoseAddressIsAlreadyThere_IgnoringCase()
    {
        var ctx = Context(Follower(AccessorEmail, booker: false, "Applicant Attorney"));

        var result = CaseFollowers.Append(Args(AccessorEmail.ToUpperInvariant()), ctx, patientRoleAllowed: true);

        result.ShouldHaveSingleItem().To.ShouldBe(AccessorEmail.ToUpperInvariant());
    }

    [Fact]
    public void Append_Args_DropsAPatientRoleAccessor_ButNotTheBooker_WhenThePatientIsNotAllowed()
    {
        var ctx = Context(
            Follower(PatientAccessorEmail, booker: false, "Patient"),
            Follower(BookerEmail, booker: true, "Patient"),
            Follower(AccessorEmail, booker: false, "Applicant Attorney"));

        var withoutPatient = CaseFollowers.Append(Args(), ctx, patientRoleAllowed: false);
        var withPatient = CaseFollowers.Append(Args(), ctx, patientRoleAllowed: true);

        withoutPatient.Select(r => r.To).ShouldBe(new[] { BookerEmail, AccessorEmail });
        withPatient.Select(r => r.To).ShouldBe(new[] { PatientAccessorEmail, BookerEmail, AccessorEmail });
    }

    [Fact]
    public void Append_Args_WithNoFollowers_ReturnsTheRecipientsUnchanged()
    {
        CaseFollowers.Append(Args("TEST-a@test.local"), new DocumentEmailContext(), true)
            .Select(r => r.To).ShouldBe(new[] { "TEST-a@test.local" });
    }

    // ---- Append (NotificationRecipient) ------------------------------------------------------------

    [Fact]
    public void Append_Recipients_AddsFollowers_Deduped_NeverAsTheOffice()
    {
        var ctx = Context(
            Follower(AccessorEmail, booker: false, "Defense Attorney"),
            Follower(BookerEmail, booker: true, "Applicant Attorney"));

        var result = CaseFollowers.Append(Recipients(BookerEmail.ToUpperInvariant()), ctx, patientRoleAllowed: true);

        result.Select(r => r.Email).ShouldBe(new[] { BookerEmail.ToUpperInvariant(), AccessorEmail });
        var added = result.Single(r => r.Email == AccessorEmail);
        added.Role.ShouldBe(RecipientRole.DefenseAttorney);
        added.IsRegistered.ShouldBeTrue();
        result.ShouldNotContain(r => r.Role == RecipientRole.OfficeAdmin);
    }

    [Fact]
    public void Append_Recipients_DropsAPatientRoleAccessor_ButNotTheBooker_WhenThePatientIsNotAllowed()
    {
        var ctx = Context(
            Follower(PatientAccessorEmail, booker: false, "Patient"),
            Follower(BookerEmail, booker: true, "Patient"));

        CaseFollowers.Append(Recipients(), ctx, patientRoleAllowed: false).Select(r => r.Email)
            .ShouldBe(new[] { BookerEmail });
        CaseFollowers.Append(Recipients(), ctx, patientRoleAllowed: true).Select(r => r.Email)
            .ShouldBe(new[] { PatientAccessorEmail, BookerEmail });
    }

    // ---- The consent information notice ------------------------------------------------------------

    [Fact]
    public void AppendWithoutPatient_SkipsAPatientRoleFollower_EvenTheBooker_AndAFollowerWithAPatientAddress()
    {
        var ctx = Context(
            Follower(PatientAccessorEmail, booker: false, "Patient"),
            Follower(BookerEmail, booker: true, "Patient"),
            Follower(PatientEmail, booker: false, "Applicant Attorney"),
            Follower(AccessorEmail, booker: false, "Applicant Attorney"));

        CaseFollowers.AppendWithoutPatient(Recipients("TEST-ce@test.local"), ctx).Select(r => r.Email)
            .ShouldBe(new[] { "TEST-ce@test.local", AccessorEmail });
    }

    [Fact]
    public void AppendWithoutPatient_KeepsAnAttorneyBooker()
    {
        var ctx = Context(Follower(BookerEmail, booker: true, "Applicant Attorney"));

        CaseFollowers.AppendWithoutPatient(Recipients(), ctx).Select(r => r.Email).ShouldBe(new[] { BookerEmail });
    }

    // ---- Roles -------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Applicant Attorney", RecipientRole.ApplicantAttorney)]
    [InlineData("Applicant Paralegal", RecipientRole.ApplicantAttorney)]
    [InlineData("Defense Attorney", RecipientRole.DefenseAttorney)]
    [InlineData("Defense Paralegal", RecipientRole.DefenseAttorney)]
    [InlineData("Claim Examiner", RecipientRole.ClaimExaminer)]
    [InlineData("claim examiner", RecipientRole.ClaimExaminer)]
    [InlineData("Patient", RecipientRole.Patient)]
    public void RoleOf_MapsEachExternalRole(string role, RecipientRole expected)
    {
        CaseFollowers.RoleOf(new[] { role }).ShouldBe(expected);
    }

    [Fact]
    public void RoleOf_IsNullForNoPartyRole()
    {
        CaseFollowers.RoleOf(new[] { "Intake Staff" }).ShouldBeNull();
        CaseFollowers.RoleOf(Array.Empty<string>()).ShouldBeNull();
    }

    [Fact]
    public void IsPatientRole_IsTrueOnlyForThePatientRole()
    {
        CaseFollowers.IsPatientRole(new[] { "Patient" }).ShouldBeTrue();
        CaseFollowers.IsPatientRole(new[] { " patient " }).ShouldBeTrue();
        CaseFollowers.IsPatientRole(new[] { "Applicant Attorney" }).ShouldBeFalse();
    }

    // ---- Packets -----------------------------------------------------------------------------------

    [Fact]
    public void AccessorPacketRecipients_AreNoneByDefault_AndTheSwitchIsTheOnlyGate()
    {
        var ctx = Context(
            Follower(AccessorEmail, booker: false, "Applicant Attorney"),
            Follower(PatientAccessorEmail, booker: false, "Patient"),
            Follower(BookerEmail, booker: true, "Applicant Attorney"),
            Follower("TEST-follower-nobody@test.local", booker: false, "Some Other Role"));

        CaseFollowers.AccessorPacketRecipients(ctx).ShouldBeEmpty();
        CaseFollowers.AccessorPacketRecipients(ctx, enabled: false).ShouldBeEmpty();

        var on = CaseFollowers.AccessorPacketRecipients(ctx, enabled: true);
        on.Select(r => r.Email).ShouldBe(new[] { AccessorEmail });
        on.Single().Role.ShouldBe(RecipientRole.ApplicantAttorney);
        on.Single().IsRegistered.ShouldBeTrue();
    }
}
