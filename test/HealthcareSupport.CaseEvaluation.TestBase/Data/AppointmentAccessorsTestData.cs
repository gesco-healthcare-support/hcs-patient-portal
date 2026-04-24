using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Deterministic GUIDs + field constants for AppointmentAccessor entities.
///
/// AppointmentAccessor is `FullAuditedEntity` (NOT AggregateRoot) and
/// `IMultiTenant`. Two accessors seeded across the two tenants so Tier-2
/// tests can exercise multi-tenant isolation AND the View(23)/Edit(24)
/// AccessType enum split:
///   Accessor1 -- TenantA, Appointment1, ApplicantAttorney1UserId, View(23)
///   Accessor2 -- TenantB, Appointment2, DefenseAttorney1UserId, Edit(24)
/// </summary>
public static class AppointmentAccessorsTestData
{
    public static readonly Guid Accessor1Id = Guid.Parse("aa111111-1111-1111-1111-111111111111");
    public static readonly Guid Accessor2Id = Guid.Parse("aa222222-2222-2222-2222-222222222222");

    public const AccessType Accessor1AccessType = AccessType.View;
    public const AccessType Accessor2AccessType = AccessType.Edit;
}
