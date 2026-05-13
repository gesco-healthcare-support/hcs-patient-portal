using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 13 (2026-05-04) -- pure tests for
/// <see cref="AppointmentAccessRules"/>.
///
/// 2026-05-12 expansion (2.5/2.6 fix): widened from 2-pathway to
/// 7-pathway. The legacy CanRead/CanEdit overloads kept for backward
/// compat are still exercised by the original tests below; new tests
/// cover the expanded pathways (Patient, AA, DA, CE).
/// </summary>
public class AppointmentAccessRulesUnitTests
{
    private static readonly Guid CallerId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private const string CallerEmail = "caller@gesco.com";

    // -- Legacy 2-pathway overload tests (CanRead) ----------------------

    [Fact]
    public void CanRead_InternalUser_BypassesEverything()
    {
        AppointmentAccessRules.CanRead(
            callerUserId: null,
            callerIsInternalUser: true,
            appointmentCreatorId: Guid.NewGuid(),
            accessorEntries: null).ShouldBeTrue();
    }

    [Fact]
    public void CanRead_ExternalUser_CreatorMatch_True()
    {
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: CallerId,
            accessorEntries: null).ShouldBeTrue();
    }

    [Fact]
    public void CanRead_ExternalUser_AccessorRowExists_True()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.View),
        };
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: entries).ShouldBeTrue();
    }

    [Fact]
    public void CanRead_ExternalUser_NoCreatorMatchNoAccessor_False()
    {
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: System.Array.Empty<AppointmentAccessRules.AccessorEntry>()).ShouldBeFalse();
    }

    [Fact]
    public void CanRead_AccessorEntryForDifferentUser_False()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(OtherUserId, AccessType.Edit),
        };
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: Guid.NewGuid(),
            accessorEntries: entries).ShouldBeFalse();
    }

    [Fact]
    public void CanRead_AnonymousCaller_False()
    {
        AppointmentAccessRules.CanRead(
            callerUserId: null,
            callerIsInternalUser: false,
            appointmentCreatorId: Guid.NewGuid(),
            accessorEntries: null).ShouldBeFalse();
    }

    // -- Legacy 2-pathway overload tests (CanEdit) ----------------------

    [Fact]
    public void CanEdit_InternalUser_BypassesEverything()
    {
        AppointmentAccessRules.CanEdit(
            callerUserId: null,
            callerIsInternalUser: true,
            appointmentCreatorId: Guid.NewGuid(),
            accessorEntries: null).ShouldBeTrue();
    }

    [Fact]
    public void CanEdit_ExternalUser_Creator_True()
    {
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: CallerId,
            accessorEntries: null).ShouldBeTrue();
    }

    [Fact]
    public void CanEdit_ExternalUser_AccessorWithEdit_True()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.Edit),
        };
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: entries).ShouldBeTrue();
    }

    [Fact]
    public void CanEdit_ExternalUser_AccessorWithViewOnly_False()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.View),
        };
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: entries).ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_ExternalUser_NoMatch_False()
    {
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: System.Array.Empty<AppointmentAccessRules.AccessorEntry>()).ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_NullCreatorId_FallsThroughToAccessorCheck()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.Edit),
        };
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: null,
            accessorEntries: entries).ShouldBeTrue();
    }

    [Fact]
    public void CanRead_NullCreatorId_FallsThroughToAccessorCheck()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.View),
        };
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: null,
            accessorEntries: entries).ShouldBeTrue();
    }

    // -- New 7-pathway tests (CanRead) -----------------------------------

    [Fact]
    public void CanRead_Expanded_Patient_True()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: CallerId,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.Patient);
    }

    [Fact]
    public void CanRead_Expanded_ApplicantAttorney_True()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: new[] { OtherUserId, CallerId },
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.ApplicantAttorney);
    }

    [Fact]
    public void CanRead_Expanded_DefenseAttorney_True()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: new[] { CallerId },
            claimExaminerEmails: null,
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.DefenseAttorney);
    }

    [Fact]
    public void CanRead_Expanded_ClaimExaminer_EmailCaseSensitive_True()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: new[] { "noise@gesco.com", CallerEmail },
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.ClaimExaminer);
    }

    [Fact]
    public void CanRead_Expanded_ClaimExaminer_EmailCaseInsensitive_True()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: "Caller@GESCO.com",
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: new[] { "caller@gesco.com" },
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.ClaimExaminer);
    }

    [Fact]
    public void CanRead_Expanded_UnrelatedExternalUser_False()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: OtherUserId,
            applicantAttorneyIdentityUserIds: new[] { OtherUserId },
            defenseAttorneyIdentityUserIds: new[] { OtherUserId },
            claimExaminerEmails: new[] { "someone.else@gesco.com" },
            accessorEntries: System.Array.Empty<AppointmentAccessRules.AccessorEntry>());
        allowed.ShouldBeFalse();
        pathway.ShouldBeNull();
    }

    [Fact]
    public void CanRead_Expanded_InternalUser_BypassesAllPathways()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: null,
            callerEmail: null,
            callerIsInternalUser: true,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.InternalUser);
    }

    [Fact]
    public void CanRead_Expanded_BookerWhoIsAlsoPatient_CreatorPathwayWinsFirst()
    {
        // Order check: Creator is checked before Patient.
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: CallerId,
            patientIdentityUserId: CallerId,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.Creator);
    }

    [Fact]
    public void CanRead_Expanded_AnonymousCaller_False()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: null,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: OtherUserId,
            applicantAttorneyIdentityUserIds: new[] { OtherUserId },
            defenseAttorneyIdentityUserIds: new[] { OtherUserId },
            claimExaminerEmails: new[] { CallerEmail },
            accessorEntries: null);
        allowed.ShouldBeFalse();
        pathway.ShouldBeNull();
    }

    [Fact]
    public void CanRead_Expanded_CallerEmailMissing_CeBranchSkipped()
    {
        // When caller has no email, the CE branch must not match -- even
        // if the CE-emails list contains a wildcard-shaped empty string.
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: null,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: new[] { "", CallerEmail },
            accessorEntries: System.Array.Empty<AppointmentAccessRules.AccessorEntry>());
        allowed.ShouldBeFalse();
        pathway.ShouldBeNull();
    }

    [Fact]
    public void CanRead_Expanded_MultipleAAs_CallerIsOne_True()
    {
        var aaIds = new[] { Guid.NewGuid(), Guid.NewGuid(), CallerId, Guid.NewGuid() };
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: aaIds,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.ApplicantAttorney);
    }

    [Fact]
    public void CanRead_Expanded_PathwayOrder_PatientBeforeAA()
    {
        // Patient is checked before AA. If the caller is both, Patient wins.
        var (allowed, pathway) = AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: CallerId,
            applicantAttorneyIdentityUserIds: new[] { CallerId },
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.Patient);
    }

    // -- New 7-pathway tests (CanEdit) -----------------------------------

    [Fact]
    public void CanEdit_Expanded_Patient_True()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: CallerId,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.Patient);
    }

    [Fact]
    public void CanEdit_Expanded_AA_True()
    {
        var (allowed, pathway) = AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: new[] { CallerId },
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: null);
        allowed.ShouldBeTrue();
        pathway.ShouldBe(AppointmentAccessRules.AccessPathway.ApplicantAttorney);
    }

    [Fact]
    public void CanEdit_Expanded_ViewOnlyAccessor_FallsThroughToFalse()
    {
        // Accessor with View only does not grant edit when none of the
        // other 6 pathways matches.
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.View),
        };
        var (allowed, pathway) = AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerEmail: CallerEmail,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: entries);
        allowed.ShouldBeFalse();
        pathway.ShouldBeNull();
    }
}
