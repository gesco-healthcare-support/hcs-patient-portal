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
    // self-service login account.
    public static readonly Guid PatientUserId = Guid.Parse("a3333333-3333-3333-3333-333333333333");

    // TEST- prefix per .claude/rules/test-data.md identifier convention.
    public const string StaffAdminUserName = "TEST-staff-admin";
    public const string AttorneyUserName = "TEST-attorney";
    public const string PatientUserName = "TEST-patient";
}
