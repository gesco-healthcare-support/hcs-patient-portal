using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Deterministic GUIDs for AppointmentApplicantAttorney join rows seeded by
/// <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// AppointmentApplicantAttorney is `FullAuditedAggregateRoot` and `IMultiTenant`.
/// The entity only has 3 FK fields (AppointmentId, ApplicantAttorneyId,
/// IdentityUserId) plus the framework audit columns, so no scalar test values
/// are needed. Two rows seeded across the two tenants:
///   Join1 -- TenantA, Appointment1, Attorney1, ApplicantAttorney1UserId
///   Join2 -- TenantB, Appointment2, Attorney2, DefenseAttorney1UserId
/// Each join lives in the same tenant as its parent Appointment so the
/// IMultiTenant filter behaves predictably in tenant-scoped test contexts.
/// </summary>
public static class AppointmentApplicantAttorneysTestData
{
    public static readonly Guid Join1Id = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    public static readonly Guid Join2Id = Guid.Parse("c2222222-2222-2222-2222-222222222222");
}
