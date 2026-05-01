using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using Volo.Abp.Identity;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

[Audited]
public class AppointmentDefenseAttorney : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public Guid AppointmentId { get; set; }

    public Guid DefenseAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }

    protected AppointmentDefenseAttorney()
    {
    }

    public AppointmentDefenseAttorney(Guid id, Guid appointmentId, Guid defenseAttorneyId, Guid identityUserId)
    {
        Id = id;
        AppointmentId = appointmentId;
        DefenseAttorneyId = defenseAttorneyId;
        IdentityUserId = identityUserId;
    }
}
