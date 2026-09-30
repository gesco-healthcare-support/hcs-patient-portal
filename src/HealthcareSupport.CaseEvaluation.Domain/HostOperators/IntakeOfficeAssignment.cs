using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Phase D (2026-06-25) -- maps a HOST Intake operator to an office they may
/// enter. Lives in the host/management database (never IMultiTenant): a
/// tenant-scoped row must never be able to grant cross-office reach. The
/// per-office assignment gate (<see cref="IIntakeAssignmentChecker"/>) reads
/// these rows in host context and is the deny-by-default boundary the custom
/// impersonation grant enforces.
///
/// <para>One row per (operator, office) -- a unique index backs idempotent
/// assign / unassign. Assigning provisions the operator's limited shadow Intake
/// user in that office's database; unassigning revokes it. The shadow user's
/// username equals the operator's email (the impersonation target), so this row
/// stores only the link, not a duplicate identity.</para>
/// </summary>
public class IntakeOfficeAssignment : FullAuditedAggregateRoot<Guid>
{
    /// <summary>The host Intake operator's IdentityUser id (TenantId == null).</summary>
    public Guid OperatorUserId { get; private set; }

    /// <summary>The office (Volo SaaS tenant) id the operator may enter.</summary>
    public Guid OfficeId { get; private set; }

    protected IntakeOfficeAssignment()
    {
    }

    public IntakeOfficeAssignment(Guid id, Guid operatorUserId, Guid officeId)
        : base(id)
    {
        OperatorUserId = operatorUserId;
        OfficeId = officeId;
    }
}
