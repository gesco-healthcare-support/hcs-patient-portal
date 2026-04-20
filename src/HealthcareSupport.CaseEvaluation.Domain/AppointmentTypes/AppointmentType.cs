using HealthcareSupport.CaseEvaluation.Doctors;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public class AppointmentType : FullAuditedEntity<Guid>
{
    [NotNull]
    public virtual string Name { get; set; } = null!;

    [CanBeNull]
    public virtual string? Description { get; set; }
    public virtual ICollection<DoctorAppointmentType> DoctorAppointmentTypes { get; set; } = new Collection<DoctorAppointmentType>();

    protected AppointmentType()
    {
    }

    public AppointmentType(Guid id, string name, string? description = null)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentTypeConsts.NameMaxLength, 0);
        Check.Length(description, nameof(description), AppointmentTypeConsts.DescriptionMaxLength, 0);
        Name = name;
        Description = description;
    }
}