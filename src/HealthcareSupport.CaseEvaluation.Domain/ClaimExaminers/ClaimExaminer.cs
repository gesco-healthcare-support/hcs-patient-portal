using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

/// <summary>
/// UM3/UM4 (2026-06-05): reusable Claim Examiner master -- a firm-less, record-based
/// mirror of <see cref="ApplicantAttorneys.ApplicantAttorney"/>. OBS-8: no firm fields.
/// IP6 record-based model: login-optional (nullable IdentityUserId); the identity is
/// linked later on self-register by email (ExternalUserType.ClaimExaminer = 2).
/// The per-appointment AppointmentClaimExaminer stays free-text; wiring its selector to
/// this master FK is deferred (CI1 coordination).
/// </summary>
public class ClaimExaminer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    [CanBeNull]
    public virtual string? FirstName { get; set; }

    [CanBeNull]
    public virtual string? LastName { get; set; }

    [CanBeNull]
    public virtual string? Email { get; set; }

    [CanBeNull]
    public virtual string? PhoneNumber { get; set; }

    [CanBeNull]
    public virtual string? FaxNumber { get; set; }

    [CanBeNull]
    public virtual string? Street { get; set; }

    [CanBeNull]
    public virtual string? City { get; set; }

    [CanBeNull]
    public virtual string? ZipCode { get; set; }

    public Guid? StateId { get; set; }

    public Guid? IdentityUserId { get; set; }

    protected ClaimExaminer()
    {
    }

    public ClaimExaminer(Guid id, Guid? stateId, Guid? identityUserId, string? phoneNumber = null, string? email = null)
    {
        Id = id;
        Check.Length(phoneNumber, nameof(phoneNumber), ClaimExaminerConsts.PhoneNumberMaxLength, 0);
        Check.Length(email, nameof(email), ClaimExaminerConsts.EmailMaxLength, 0);
        StateId = stateId;
        IdentityUserId = identityUserId;
        PhoneNumber = phoneNumber;
        Email = email;
    }
}
