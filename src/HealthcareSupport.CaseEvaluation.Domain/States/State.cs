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

public class State : FullAuditedAggregateRoot<Guid>
{
    [NotNull]
    public virtual string Name { get; set; }

    protected State()
    {
    }

    public State(Guid id, string name)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Name = name;
    }
}