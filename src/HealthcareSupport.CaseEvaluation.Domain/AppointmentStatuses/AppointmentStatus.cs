using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public class AppointmentStatus : FullAuditedEntity<Guid>
{
    [NotNull]
    public virtual string Name { get; set; }

    protected AppointmentStatus()
    {
    }

    public AppointmentStatus(Guid id, string name)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentStatusConsts.NameMaxLength, 0);
        Name = name;
    }
}