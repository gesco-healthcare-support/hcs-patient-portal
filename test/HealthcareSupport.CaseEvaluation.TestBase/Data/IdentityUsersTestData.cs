using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs for IdentityUser entities seeded by
/// CaseEvaluationIntegrationTestSeedContributor. These IDs are referenced as
/// FK targets by downstream entity seeds (Doctor, Patient, Appointment,
/// ApplicantAttorney, AppointmentAccessor).
/// </summary>
public static class IdentityUsersTestData
{
    // Staff admin — the "booking clerk" persona. FK target for Appointment.IdentityUserId
    // when appointments are booked by back-office staff.
    public static readonly Guid StaffAdminId = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    // Attorney user — FK target for ApplicantAttorney.IdentityUserId and
    // AppointmentAccessor.IdentityUserId for attorney-scoped access grants.
    public static readonly Guid AttorneyUserId = Guid.Parse("a2222222-2222-2222-2222-222222222222");

    // Patient user — FK target for Patient.IdentityUserId when a patient has a
    // self-service login account. Used by Patient1 in the cross-tenant visibility tests.
    public static readonly Guid PatientUserId = Guid.Parse("a3333333-3333-3333-3333-333333333333");

    // Second patient user — FK target for Patient2 (scoped to TenantB in the seed).
    // Added in the Patients (T4) PR because Patient.IdentityUserId is non-nullable
    // and must be unique per patient in practice (1:1 relationship).
    public static readonly Guid Patient2UserId = Guid.Parse("a4444444-4444-4444-4444-444444444444");

    // TEST- prefix per .claude/rules/test-data.md identifier convention.
    public const string StaffAdminUserName = "TEST-staff-admin";
    public const string AttorneyUserName = "TEST-attorney";
    public const string PatientUserName = "TEST-patient";
    public const string Patient2UserName = "TEST-patient-2";

    // Synthetic emails consumed by downstream seeds (e.g., PatientsDataSeedContributor
    // looks up the patient user by email, Appointment booking flows assert against
    // AttorneyEmail, etc.).
    public const string StaffAdminEmail = "TEST-staff-admin@test.local";
    public const string AttorneyEmail = "TEST-attorney@test.local";
    public const string PatientEmail = "TEST-patient@test.local";
    public const string Patient2Email = "TEST-patient-2@test.local";

    // Role names mirror the production DoctorTenantAppService.EnsureRoleAsync pattern.
    // All three are seeded in host context in this PR to avoid depending on SaaS
    // Tenant rows existing; the PR-1C Patients work will rescope patient/attorney
    // users into TenantA once the cross-tenant visibility tests add their own
    // minimal Tenant-row seeding.
    public const string AdminRoleName = "Admin";
    public const string AttorneyRoleName = "Attorney";
    public const string PatientRoleName = "Patient";

    // Password satisfying ABP IdentityOptions.Password defaults
    // (RequireDigit, RequireLowercase, RequireNonAlphanumeric, RequireUppercase, RequiredLength=6).
    // Used only by the test seed contributor; never hits production.
    public const string SeedPassword = "Test-User1!";
}
