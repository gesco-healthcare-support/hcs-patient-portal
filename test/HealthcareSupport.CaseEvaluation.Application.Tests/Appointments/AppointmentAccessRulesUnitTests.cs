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

    // -- Accessor-management gate (CanManageAccessors, 2026-06-10 Workstream B) ----
    // Rule = internal OR (isCreator AND authorized-external-accessor-manager).
    // The Edit-accessor pathway is intentionally NOT admitted here.

    [Fact]
    public void CanManageAccessors_InternalUser_True()
    {
        // Internal users always pass, regardless of creator / caller id.
        AppointmentAccessRules.CanManageAccessors(
            callerUserId: null,
            callerIsInternalUser: true,
            callerIsAuthorizedExternalAccessorManager: false,
            appointmentCreatorId: OtherUserId).ShouldBeTrue();
    }

    [Fact]
    public void CanManageAccessors_InternalUser_NullCreator_True()
    {
        AppointmentAccessRules.CanManageAccessors(
            callerUserId: CallerId,
            callerIsInternalUser: true,
            callerIsAuthorizedExternalAccessorManager: false,
            appointmentCreatorId: null).ShouldBeTrue();
    }

    [Fact]
    public void CanManageAccessors_ExternalManager_IsCreator_True()
    {
        // AA and DA both feed callerIsAuthorizedExternalAccessorManager: true
        // (the AA-vs-DA distinction is pinned in BookingFlowRolesUnitTests).
        // The creator who holds an authorized accessor-managing role may manage.
        AppointmentAccessRules.CanManageAccessors(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            callerIsAuthorizedExternalAccessorManager: true,
            appointmentCreatorId: CallerId).ShouldBeTrue();
    }

    [Fact]
    public void CanManageAccessors_ExternalManager_NotCreator_False()
    {
        // An authorized accessor-managing external role who did NOT create the
        // appointment (party only) cannot manage its accessors.
        AppointmentAccessRules.CanManageAccessors(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            callerIsAuthorizedExternalAccessorManager: true,
            appointmentCreatorId: OtherUserId).ShouldBeFalse();
    }

    [Fact]
    public void CanManageAccessors_ExternalNonManager_IsCreator_False()
    {
        // A Patient / Claim Examiner who created the appointment is NOT an
        // authorized accessor manager, so the creator-AND-role gate denies them.
        AppointmentAccessRules.CanManageAccessors(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            callerIsAuthorizedExternalAccessorManager: false,
            appointmentCreatorId: CallerId).ShouldBeFalse();
    }

    [Fact]
    public void CanManageAccessors_EditAccessorNonCreator_False()
    {
        // Pin the dropped pathway: an Edit-accessor is just an external
        // non-creator non-manager here (the rule does not even take accessor
        // rows), so it is denied -- Edit-accessors can no longer self-propagate.
        AppointmentAccessRules.CanManageAccessors(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            callerIsAuthorizedExternalAccessorManager: false,
            appointmentCreatorId: OtherUserId).ShouldBeFalse();
    }

    [Fact]
    public void CanManageAccessors_NullCaller_NotInternal_False()
    {
        AppointmentAccessRules.CanManageAccessors(
            callerUserId: null,
            callerIsInternalUser: false,
            callerIsAuthorizedExternalAccessorManager: true,
            appointmentCreatorId: OtherUserId).ShouldBeFalse();
    }

    [Fact]
    public void CanManageAccessors_ManagerButNullCreator_False()
    {
        // Defensive: a null creator cannot match the caller, so an external
        // manager on an appointment with no recorded creator is denied.
        AppointmentAccessRules.CanManageAccessors(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            callerIsAuthorizedExternalAccessorManager: true,
            appointmentCreatorId: null).ShouldBeFalse();
    }

    // -- #2 / Phase 5: email + role row-level visibility -----------------
    // Visible iff a party-email column == caller email AND the caller holds
    // that column's role. The leak guard: holding a role does NOT reveal a
    // different role's column even when the email matches it.

    private const string FirmEmail = "firm@example.com";

    [Fact]
    public void EmailRoleVisible_AaColumnMatches_AaRole_True()
    {
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            callerEmail: FirmEmail,
            callerRoles: new[] { "Applicant Attorney" },
            patientEmail: "patient@example.com",
            applicantAttorneyEmail: FirmEmail,
            defenseAttorneyEmail: "da@example.com",
            claimExaminerEmail: "ce@example.com").ShouldBeTrue();
    }

    [Fact]
    public void EmailRoleVisible_DaColumnMatches_ButOnlyAaRole_False()
    {
        // Cross-role leak guard: the caller's email is the DEFENSE attorney
        // column, but the caller holds only Applicant Attorney -> NOT visible.
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            callerEmail: FirmEmail,
            callerRoles: new[] { "Applicant Attorney" },
            patientEmail: null,
            applicantAttorneyEmail: null,
            defenseAttorneyEmail: FirmEmail,
            claimExaminerEmail: null).ShouldBeFalse();
    }

    [Fact]
    public void EmailRoleVisible_DaColumnMatches_AfterAccumulatingDaRole_True()
    {
        // After D9 role accumulation (AA firm gains Defense Attorney via an
        // accessor invite), the DA-column appointment becomes visible.
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            callerEmail: FirmEmail,
            callerRoles: new[] { "Applicant Attorney", "Defense Attorney" },
            patientEmail: null,
            applicantAttorneyEmail: null,
            defenseAttorneyEmail: FirmEmail,
            claimExaminerEmail: null).ShouldBeTrue();
    }

    [Fact]
    public void EmailRoleVisible_PatientColumn_PatientRole_True()
    {
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            FirmEmail, new[] { "Patient" }, FirmEmail, null, null, null).ShouldBeTrue();
    }

    [Fact]
    public void EmailRoleVisible_ClaimExaminerColumn_CeRole_True()
    {
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            FirmEmail, new[] { "Claim Examiner" }, null, null, null, FirmEmail).ShouldBeTrue();
    }

    [Fact]
    public void EmailRoleVisible_AaColumnMatches_ButPatientRoleOnly_False()
    {
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            FirmEmail, new[] { "Patient" }, null, FirmEmail, null, null).ShouldBeFalse();
    }

    [Fact]
    public void EmailRoleVisible_NoColumnMatchesEmail_False()
    {
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            FirmEmail,
            new[] { "Applicant Attorney", "Defense Attorney", "Patient", "Claim Examiner" },
            "patient@example.com", "aa@example.com", "da@example.com", "ce@example.com")
            .ShouldBeFalse();
    }

    [Fact]
    public void EmailRoleVisible_CaseInsensitiveEmailAndRole_True()
    {
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            callerEmail: "FIRM@Example.com",
            callerRoles: new[] { "applicant attorney" },
            patientEmail: null,
            applicantAttorneyEmail: "firm@example.com",
            defenseAttorneyEmail: null,
            claimExaminerEmail: null).ShouldBeTrue();
    }

    [Fact]
    public void EmailRoleVisible_NullCallerEmail_False()
    {
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            null, new[] { "Applicant Attorney" }, null, "aa@example.com", null, null).ShouldBeFalse();
    }

    [Fact]
    public void EmailRoleVisible_NullRoles_False()
    {
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            FirmEmail, null, FirmEmail, FirmEmail, FirmEmail, FirmEmail).ShouldBeFalse();
    }

    [Fact]
    public void EmailRoleVisible_EmptyRoles_False()
    {
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            FirmEmail, System.Array.Empty<string>(), FirmEmail, null, null, null).ShouldBeFalse();
    }

    [Fact]
    public void EmailRoleVisible_BlankColumns_False()
    {
        // Holds every role, but the appointment names no party emails -> hidden.
        AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            FirmEmail,
            new[] { "Applicant Attorney", "Defense Attorney", "Patient", "Claim Examiner" },
            "  ", null, "", "   ").ShouldBeFalse();
    }
}
