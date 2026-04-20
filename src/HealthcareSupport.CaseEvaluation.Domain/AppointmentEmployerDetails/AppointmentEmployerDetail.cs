using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.States;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public class AppointmentEmployerDetail : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string EmployerName { get; set; } = null!;

    [NotNull]
    public virtual string Occupation { get; set; } = null!;

    [CanBeNull]
    public virtual string? PhoneNumber { get; set; }

    [CanBeNull]
    public virtual string? Street { get; set; }

    [CanBeNull]
    public virtual string? City { get; set; }

    [CanBeNull]
    public virtual string? ZipCode { get; set; }

    public Guid AppointmentId { get; set; }
    public Guid? StateId { get; set; }
    protected AppointmentEmployerDetail()
    {
    }

    public AppointmentEmployerDetail(Guid id, Guid appointmentId, Guid? stateId, string employerName, string occupation)
    {
        Id = id;
        Check.NotNull(employerName, nameof(employerName));
        Check.Length(employerName, nameof(employerName), AppointmentEmployerDetailConsts.EmployerNameMaxLength, 0);
        Check.NotNull(occupation, nameof(occupation));
        Check.Length(occupation, nameof(occupation), AppointmentEmployerDetailConsts.OccupationMaxLength, 0);
        EmployerName = employerName;
        Occupation = occupation;
        AppointmentId = appointmentId;
        StateId = stateId;
    }
}