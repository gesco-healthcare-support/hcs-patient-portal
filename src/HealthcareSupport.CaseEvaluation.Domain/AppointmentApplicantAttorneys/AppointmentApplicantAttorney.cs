using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using Volo.Abp.Identity;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class AppointmentApplicantAttorney : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public Guid AppointmentId { get; set; }

    public Guid ApplicantAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }

    protected AppointmentApplicantAttorney()
    {
    }

    public AppointmentApplicantAttorney(Guid id, Guid appointmentId, Guid applicantAttorneyId, Guid identityUserId)
    {
        Id = id;
        AppointmentId = appointmentId;
        ApplicantAttorneyId = applicantAttorneyId;
        IdentityUserId = identityUserId;
    }
}