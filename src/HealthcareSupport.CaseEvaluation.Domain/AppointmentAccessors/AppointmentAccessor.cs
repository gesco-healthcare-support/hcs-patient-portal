using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.Appointments;
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

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

[Audited]
public class AppointmentAccessor : FullAuditedEntity<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual AccessType AccessTypeId { get; set; }

    public Guid IdentityUserId { get; set; }

    public Guid AppointmentId { get; set; }

    protected AppointmentAccessor()
    {
    }

    public AppointmentAccessor(Guid id, Guid identityUserId, Guid appointmentId, AccessType accessTypeId)
    {
        Id = id;
        AccessTypeId = accessTypeId;
        IdentityUserId = identityUserId;
        AppointmentId = appointmentId;
    }
}