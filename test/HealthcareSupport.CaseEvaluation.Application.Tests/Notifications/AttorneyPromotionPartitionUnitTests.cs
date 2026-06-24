using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 4 / C3 / D3 / D6 (firm-based AA/DA registration) -- promotion + dedup
/// across booker types. Composes the pure promotion decision
/// (<see cref="AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail"/>) with the
/// To/CC partition (<see cref="BookerCcDispatcher.PartitionToBookerCc"/>) exactly as
/// the status-change + reminder handlers do: anchor on the promoted recipient (else
/// the booker), and append the creator to the stakeholder set when promoted.
///
/// Verifies:
///   paralegal-AA  -> To = the named attorney, CC includes the firm creator + patient
///   solo attorney -> To == creator == attorney collapses to a single recipient (D6)
///   Patient / CE  -> not promoted: To = the booker, behavior unchanged
///
/// Synthetic values only (per .claude/rules/test-data.md).
/// </summary>
public class AttorneyPromotionPartitionUnitTests
{
    private const string Aa = "Applicant Attorney";
    private const string Da = "Defense Attorney";

    /// <summary>
    /// Mirrors the handler composition: resolve the promoted email, fall back to
    /// the booker anchor, and append the creator to the CC stakeholder set when
    /// promoted (scoped to the promoted case). Returns the resulting To + CC.
    /// </summary>
    private static (NotificationRecipient? To, System.Collections.Generic.List<NotificationRecipient> Cc) Address(
        string[] creatorRoles,
        string? applicantAttorneyEmail,
        string? defenseAttorneyEmail,
        string? bookerEmail,
        string? creatorEmail,
        System.Collections.Generic.List<NotificationRecipient> stakeholders)
    {
        var primary = AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
            creatorRoles, applicantAttorneyEmail, defenseAttorneyEmail);
        var isPromoted = primary != null;

        var recipients = stakeholders;
        if (isPromoted && !string.IsNullOrWhiteSpace(creatorEmail))
        {
            recipients = new System.Collections.Generic.List<NotificationRecipient>(stakeholders)
            {
                new(email: creatorEmail!, role: RecipientRole.OfficeAdmin, isRegistered: true),
            };
        }

        return BookerCcDispatcher.PartitionToBookerCc(recipients, primary ?? bookerEmail);
    }

    private static System.Collections.Generic.List<NotificationRecipient> Parties(
        params (string Email, RecipientRole Role)[] parties)
    {
        var list = new System.Collections.Generic.List<NotificationRecipient>();
        foreach (var (email, role) in parties)
        {
            list.Add(new NotificationRecipient(email: email, role: role, isRegistered: true));
        }
        return list;
    }

    [Fact]
    public void ParalegalAaBooker_AddressesToAttorney_CcsCreatorAndPatient()
    {
        var (to, cc) = Address(
            creatorRoles: new[] { Aa },
            applicantAttorneyEmail: "attorney@example.com",
            defenseAttorneyEmail: null,
            bookerEmail: "patient@example.com",
            creatorEmail: "firm@example.com",
            stakeholders: Parties(
                ("patient@example.com", RecipientRole.Patient),
                ("attorney@example.com", RecipientRole.ApplicantAttorney)));

        to.ShouldNotBeNull();
        to!.Email.ShouldBe("attorney@example.com");
        cc.ShouldContain(r => r.Email == "firm@example.com");
        cc.ShouldContain(r => r.Email == "patient@example.com");
        cc.ShouldNotContain(r => r.Email == "attorney@example.com");
    }

    [Fact]
    public void ParalegalDaBooker_AddressesToDefenseAttorney_CcsCreator()
    {
        var (to, cc) = Address(
            creatorRoles: new[] { Da },
            applicantAttorneyEmail: null,
            defenseAttorneyEmail: "defense@example.com",
            bookerEmail: "patient@example.com",
            creatorEmail: "firm@example.com",
            stakeholders: Parties(
                ("patient@example.com", RecipientRole.Patient),
                ("defense@example.com", RecipientRole.DefenseAttorney)));

        to.ShouldNotBeNull();
        to!.Email.ShouldBe("defense@example.com");
        cc.ShouldContain(r => r.Email == "firm@example.com");
        cc.ShouldContain(r => r.Email == "patient@example.com");
    }

    [Fact]
    public void SoloAttorney_ToEqualsCreatorEqualsAttorney_CollapsesToSingleRecipient()
    {
        // The firm AA enters its own login email as the attorney: To == creator.
        var (to, cc) = Address(
            creatorRoles: new[] { Aa },
            applicantAttorneyEmail: "solo@example.com",
            defenseAttorneyEmail: null,
            bookerEmail: "patient@example.com",
            creatorEmail: "solo@example.com",
            stakeholders: Parties(
                ("patient@example.com", RecipientRole.Patient),
                ("solo@example.com", RecipientRole.ApplicantAttorney)));

        to.ShouldNotBeNull();
        to!.Email.ShouldBe("solo@example.com");
        // D6 dedup: the appended creator (same email as the To) must not appear in CC.
        cc.ShouldNotContain(r => r.Email == "solo@example.com");
        cc.ShouldContain(r => r.Email == "patient@example.com");
    }

    [Fact]
    public void PatientBooker_NotPromoted_AddressesToPatient_Unchanged()
    {
        var (to, cc) = Address(
            creatorRoles: new[] { "Patient" },
            applicantAttorneyEmail: "attorney@example.com",
            defenseAttorneyEmail: null,
            bookerEmail: "patient@example.com",
            creatorEmail: "patient@example.com",
            stakeholders: Parties(
                ("patient@example.com", RecipientRole.Patient),
                ("attorney@example.com", RecipientRole.ApplicantAttorney)));

        to.ShouldNotBeNull();
        to!.Email.ShouldBe("patient@example.com");
        cc.ShouldContain(r => r.Email == "attorney@example.com");
        // No creator promotion -> no extra firm CC injected.
        cc.Count.ShouldBe(1);
    }

    [Fact]
    public void ClaimExaminerBooker_NotPromoted_AddressesToBooker_Unchanged()
    {
        var (to, cc) = Address(
            creatorRoles: new[] { "Claim Examiner" },
            applicantAttorneyEmail: "attorney@example.com",
            defenseAttorneyEmail: null,
            bookerEmail: "examiner@example.com",
            creatorEmail: "examiner@example.com",
            stakeholders: Parties(
                ("examiner@example.com", RecipientRole.ClaimExaminer),
                ("attorney@example.com", RecipientRole.ApplicantAttorney)));

        to.ShouldNotBeNull();
        to!.Email.ShouldBe("examiner@example.com");
        cc.ShouldContain(r => r.Email == "attorney@example.com");
        cc.Count.ShouldBe(1);
    }
}
