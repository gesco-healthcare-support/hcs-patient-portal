using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + user-identity constants for IdentityUser entities
/// seeded by <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// Models the eight test users that exercise the seven intended application roles:
///
///   Host scope (TenantId = null):
///     - HostAdmin      (role: "admin")  -- dev/debug superuser with cross-tenant visibility
///
///   TenantA scope:
///     - TenantAdmin1   (role: "TenantAdmin")      -- per-tenant administrator; a business role distinct from "Doctor"
///     - Doctor1        (role: "Doctor")           -- practitioner; FK target for the Doctor1 entity
///     - ApplicantAttorney1 (role: "Applicant Attorney")
///     - DefenseAttorney1   (role: "Defense Attorney")
///     - ClaimExaminer1     (role: "Claim Examiner")
///     - Patient1       (role: "Patient")          -- FK target for the Patient1 entity
///
///   TenantB scope:
///     - Doctor2        (role: "Doctor")           -- FK target for the Doctor2 entity
///     - Patient2       (role: "Patient")          -- FK target for the Patient2 entity
///
/// The production codebase does not yet formally define "HostAdmin" and "TenantAdmin"
/// as distinct roles (see INCOMPLETE-FEATURES.md FEAT-11 and FEAT-12). Test infra
/// pre-models them so subsequent PRs that introduce these roles in production code
/// do not have to rename test constants.
/// </summary>
public static class IdentityUsersTestData
{
    // Guids: prefixed by role hint for readability (not semantically meaningful to ABP).
    public static readonly Guid HostAdminId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantAdmin1UserId = Guid.Parse("a5555555-5555-5555-5555-555555555555");
    public static readonly Guid Doctor1UserId = Guid.Parse("a6666666-6666-6666-6666-666666666666");
    public static readonly Guid Doctor2UserId = Guid.Parse("a7777777-7777-7777-7777-777777777777");
    public static readonly Guid ApplicantAttorney1UserId = Guid.Parse("a2222222-2222-2222-2222-222222222222");
    public static readonly Guid DefenseAttorney1UserId = Guid.Parse("a8888888-8888-8888-8888-888888888888");
    public static readonly Guid ClaimExaminer1UserId = Guid.Parse("a9999999-9999-9999-9999-999999999999");
    public static readonly Guid Patient1UserId = Guid.Parse("a3333333-3333-3333-3333-333333333333");
    public static readonly Guid Patient2UserId = Guid.Parse("a4444444-4444-4444-4444-444444444444");

    // Usernames: "TEST-" prefix per .claude/rules/test-data.md identifier convention.
    public const string HostAdminUserName = "TEST-host-admin";
    public const string TenantAdmin1UserName = "TEST-tenant-admin-1";
    public const string Doctor1UserName = "TEST-doctor-1";
    public const string Doctor2UserName = "TEST-doctor-2";
    public const string ApplicantAttorney1UserName = "TEST-applicant-attorney-1";
    public const string DefenseAttorney1UserName = "TEST-defense-attorney-1";
    public const string ClaimExaminer1UserName = "TEST-claim-examiner-1";
    public const string Patient1UserName = "TEST-patient-1";
    public const string Patient2UserName = "TEST-patient-2";

    // Synthetic emails; @test.local is RFC-reserved so cannot be routed to a real inbox.
    public const string HostAdminEmail = "TEST-host-admin@test.local";
    public const string TenantAdmin1Email = "TEST-tenant-admin-1@test.local";
    public const string Doctor1Email = "TEST-doctor-1@test.local";
    public const string Doctor2Email = "TEST-doctor-2@test.local";
    public const string ApplicantAttorney1Email = "TEST-applicant-attorney-1@test.local";
    public const string DefenseAttorney1Email = "TEST-defense-attorney-1@test.local";
    public const string ClaimExaminer1Email = "TEST-claim-examiner-1@test.local";
    public const string Patient1Email = "TEST-patient-1@test.local";
    public const string Patient2Email = "TEST-patient-2@test.local";

    // Role names -- each matches what production seed code creates where the role
    // already exists, or introduces the intended name where the role does not yet
    // exist in the codebase (HostAdmin, TenantAdmin).
    public const string HostAdminRoleName = "admin";                     // matches ABP default host admin
    public const string TenantAdminRoleName = "TenantAdmin";             // new; not yet in production code (FEAT-12)
    public const string DoctorRoleName = "Doctor";                       // matches DoctorTenantAppService.EnsureRoleAsync
    public const string ApplicantAttorneyRoleName = "Applicant Attorney"; // matches ExternalUserRoleDataSeedContributor
    public const string DefenseAttorneyRoleName = "Defense Attorney";     // matches ExternalUserRoleDataSeedContributor
    public const string ClaimExaminerRoleName = "Claim Examiner";         // matches ExternalUserRoleDataSeedContributor
    public const string PatientRoleName = "Patient";                      // matches ExternalUserRoleDataSeedContributor

    // Password satisfying ABP IdentityOptions.Password defaults
    // (RequireDigit, RequireLowercase, RequireNonAlphanumeric, RequireUppercase, RequiredLength=6).
    // Used only by the test seed contributor; never hits production.
    public const string SeedPassword = "Test-User1!";
}
