using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public class AppointmentLanguage : FullAuditedEntity<Guid>
{
    [NotNull]
    public virtual string Name { get; set; } = null!;

    protected AppointmentLanguage()
    {
    }

    public AppointmentLanguage(Guid id, string name)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentLanguageConsts.NameMaxLength, 0);
        Name = name;
    }
}