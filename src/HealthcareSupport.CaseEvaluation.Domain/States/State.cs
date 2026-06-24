using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.States;

public class State : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    // Reference list copied into each office DB (db-per-office): a separate
    // database cannot foreign-key into another, so addresses resolve States
    // locally. Seeded identically per office; not office-editable.
    public virtual Guid? TenantId { get; protected set; }

    [NotNull]
    public virtual string Name { get; set; } = null!;

    /// <summary>Reserved system state: not editable or deletable by admins.
    /// Mirrors <c>AppointmentDocumentType.IsSystem</c>.</summary>
    public virtual bool IsSystem { get; set; }

    protected State()
    {
    }

    public State(Guid id, string name, bool isSystem = false)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Name = name;
        IsSystem = isSystem;
    }
}