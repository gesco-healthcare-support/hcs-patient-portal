using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Which side of the case a change request comes from, and who on the OTHER side must consent.
/// </summary>
/// <remarks>
/// Side A is the patient and applicant attorney; side B is the defense attorney and claim
/// examiner. The opposing representative is the attorney where there is one, and falls back to
/// the other party on that side. Getting either wrong sends the consent request to the wrong
/// person, or skips consent altogether. The recipient list is substituted, so each case states
/// exactly who is on the appointment. All emails are synthetic.
/// </remarks>
public class ChangeRequestSideResolverUnitTests
{
    private static readonly Guid AppointmentId = new("8e4a2b3c-0000-4000-9000-000000000001");

    private static SendAppointmentEmailArgs Party(RecipientRole role, string email) => new() { To = email, Role = role };

    private static readonly SendAppointmentEmailArgs Patient = Party(RecipientRole.Patient, "patient@example.test");
    private static readonly SendAppointmentEmailArgs Applicant = Party(RecipientRole.ApplicantAttorney, "applicant@example.test");
    private static readonly SendAppointmentEmailArgs Defense = Party(RecipientRole.DefenseAttorney, "defense@example.test");
    private static readonly SendAppointmentEmailArgs Examiner = Party(RecipientRole.ClaimExaminer, "examiner@example.test");

    private static ChangeRequestSideResolver With(params SendAppointmentEmailArgs[] parties)
    {
        var recipients = Substitute.For<IAppointmentRecipientResolver>();
        recipients.ResolveAsync(AppointmentId, NotificationKind.Submitted)
            .Returns(new List<SendAppointmentEmailArgs>(parties));
        return new ChangeRequestSideResolver(recipients);
    }

    [Fact]
    public async Task A_patient_side_submitter_needs_the_defense_attorney()
    {
        var result = await With(Patient, Applicant, Defense, Examiner).ResolveAsync(AppointmentId, "PATIENT@example.test");

        result.ShouldNotBeNull();
        result.RequestingSide.ShouldBe(ChangeRequestSide.SideA);
        result.OpposingRepEmail.ShouldBe(Defense.To);
        result.OpposingRepRole.ShouldBe(RecipientRole.DefenseAttorney);
        result.AllParties.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Side_a_falls_back_to_the_claim_examiner_when_there_is_no_defense_attorney()
    {
        var result = await With(Applicant, Examiner).ResolveAsync(AppointmentId, Applicant.To);
        result!.OpposingRepEmail.ShouldBe(Examiner.To);
    }

    [Fact]
    public async Task A_defense_side_submitter_needs_the_applicant_attorney_then_the_patient()
    {
        var withAttorney = await With(Patient, Applicant, Examiner).ResolveAsync(AppointmentId, Examiner.To);
        var withoutAttorney = await With(Patient, Defense).ResolveAsync(AppointmentId, Defense.To);

        withAttorney!.RequestingSide.ShouldBe(ChangeRequestSide.SideB);
        withAttorney.OpposingRepEmail.ShouldBe(Applicant.To);
        withoutAttorney!.OpposingRepEmail.ShouldBe(Patient.To);
    }

    [Fact]
    public async Task A_booker_who_is_not_a_named_party_is_placed_by_their_role()
    {
        var sideA = await With(Patient, Defense).ResolveAsync(AppointmentId, "paralegal@example.test", new[] { " ", "Applicant Attorney" });
        var sideB = await With(Patient, Defense).ResolveAsync(AppointmentId, "paralegal@example.test", new[] { "claim examiner" });

        sideA!.RequestingSide.ShouldBe(ChangeRequestSide.SideA);
        sideB!.RequestingSide.ShouldBe(ChangeRequestSide.SideB);
    }

    // 2026-11-04: the recipient resolver also lists the SUBMITTER, tagged Patient, with a context
    // ending in "/booker". It is not a named party, so it must not decide the side or stand in as a
    // side's representative. The literal "booker" is deliberate: an independent copy of the suffix.
    private static SendAppointmentEmailArgs Booker(string email) => new()
    {
        To = email,
        Role = RecipientRole.Patient,
        Context = $"Resolver/Submitted/Patient/{AppointmentId}/booker",
    };

    [Theory]
    [InlineData("Defense Paralegal")]
    [InlineData("Defense Attorney")]
    public async Task A_defense_side_booker_listed_by_the_resolver_is_placed_on_side_b(string bookerRole)
    {
        // Before 2026-11-04 the booker's own entry matched first and put them on Side A, so the
        // defense side was asked to consent to its own request.
        var booker = Booker("TEST-defense-paralegal@test.local");

        var result = await With(Patient, Applicant, Defense, booker)
            .ResolveAsync(AppointmentId, "test-defense-paralegal@TEST.local", new[] { bookerRole });

        result.ShouldNotBeNull();
        result.RequestingSide.ShouldBe(ChangeRequestSide.SideB);
        result.OpposingRepEmail.ShouldBe(Applicant.To);
        result.AllParties.ShouldContain(booker); // the confirmation still copies the booker
    }

    [Fact]
    public async Task An_applicant_side_paralegal_booker_is_placed_on_side_a()
    {
        var result = await With(Patient, Applicant, Defense, Booker("TEST-applicant-paralegal@test.local"))
            .ResolveAsync(AppointmentId, "TEST-applicant-paralegal@test.local", new[] { "Applicant Paralegal" });

        result!.RequestingSide.ShouldBe(ChangeRequestSide.SideA);
        result.OpposingRepEmail.ShouldBe(Defense.To);
    }

    [Fact]
    public async Task A_patient_booker_is_still_side_a_whether_or_not_their_login_is_the_patient_address()
    {
        // Same address: the resolver keeps the patient entry only. Different address: only the
        // booker entry carries it, and the Patient role places them.
        var sameAddress = await With(Patient, Defense).ResolveAsync(AppointmentId, Patient.To, new[] { "Patient" });
        var ownLogin = await With(Patient, Defense, Booker("TEST-patient-login@test.local"))
            .ResolveAsync(AppointmentId, "TEST-patient-login@test.local", new[] { "Patient" });

        sameAddress!.RequestingSide.ShouldBe(ChangeRequestSide.SideA);
        sameAddress.OpposingRepEmail.ShouldBe(Defense.To);
        ownLogin!.RequestingSide.ShouldBe(ChangeRequestSide.SideA);
        ownLogin.OpposingRepEmail.ShouldBe(Defense.To);
    }

    [Fact]
    public async Task The_booker_is_never_a_sides_consent_representative()
    {
        // No applicant attorney and no patient address: Side A has nobody to ask. The booker used
        // to be picked as the "Patient" fallback.
        var booker = Booker("TEST-firm-booker@test.local");

        var bothSides = await With(Defense, booker).ResolveBothSidesAsync(AppointmentId);
        var defenseFiled = await With(Defense, booker).ResolveAsync(AppointmentId, Defense.To);

        bothSides.ShouldBe(new ChangeRequestBothSidesResolution(null, null, Defense.To, RecipientRole.DefenseAttorney));
        defenseFiled.ShouldBeNull();
    }

    [Fact]
    public async Task No_side_is_resolved_for_an_unplaceable_submitter()
    {
        var resolver = With(Patient, Defense);

        (await resolver.ResolveAsync(AppointmentId, null)).ShouldBeNull();
        (await resolver.ResolveAsync(AppointmentId, "stranger@example.test", new[] { "Intake Staff" })).ShouldBeNull();
    }

    [Fact]
    public async Task No_side_is_resolved_when_nobody_on_the_other_side_can_consent()
    {
        (await With(Patient, Applicant).ResolveAsync(AppointmentId, Patient.To)).ShouldBeNull();
    }

    [Fact]
    public async Task Recipients_without_an_address_are_ignored()
    {
        var result = await With(Patient, Party(RecipientRole.DefenseAttorney, " "), Examiner)
            .ResolveAsync(AppointmentId, Patient.To);
        result!.OpposingRepEmail.ShouldBe(Examiner.To);
    }

    [Fact]
    public async Task Both_sides_are_named_with_their_attorney_first()
    {
        var full = await With(Patient, Applicant, Defense, Examiner).ResolveBothSidesAsync(AppointmentId);
        var partyOnly = await With(Patient, Examiner).ResolveBothSidesAsync(AppointmentId);
        var empty = await With().ResolveBothSidesAsync(AppointmentId);

        full.ShouldBe(new ChangeRequestBothSidesResolution(
            Applicant.To, RecipientRole.ApplicantAttorney, Defense.To, RecipientRole.DefenseAttorney));
        partyOnly.ShouldBe(new ChangeRequestBothSidesResolution(
            Patient.To, RecipientRole.Patient, Examiner.To, RecipientRole.ClaimExaminer));
        empty.ShouldBe(new ChangeRequestBothSidesResolution(null, null, null, null));
    }
}
