using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using System;
using System.Collections.Generic;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

[Audited]
public class AppointmentBodyPart : FullAuditedEntity<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public Guid AppointmentInjuryDetailId { get; set; }

    [NotNull]
    public virtual string BodyPartDescription { get; set; } = null!;

    protected AppointmentBodyPart()
    {
    }

    public AppointmentBodyPart(Guid id, Guid appointmentInjuryDetailId, string bodyPartDescription)
    {
        Id = id;
        Check.NotNullOrWhiteSpace(bodyPartDescription, nameof(bodyPartDescription));
        Check.Length(bodyPartDescription, nameof(bodyPartDescription), AppointmentBodyPartConsts.BodyPartDescriptionMaxLength);
        AppointmentInjuryDetailId = appointmentInjuryDetailId;
        BodyPartDescription = bodyPartDescription;
    }
}
