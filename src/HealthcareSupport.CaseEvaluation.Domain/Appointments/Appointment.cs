using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [CanBeNull]
    public virtual string? PanelNumber { get; set; }

    public virtual DateTime AppointmentDate { get; set; }

    public virtual bool IsPatientAlreadyExist { get; set; }

    [NotNull]
    public virtual string RequestConfirmationNumber { get; set; }

    public virtual DateTime? DueDate { get; set; }

    [CanBeNull]
    public virtual string? InternalUserComments { get; set; }

    public virtual DateTime? AppointmentApproveDate { get; set; }

    public virtual AppointmentStatusType AppointmentStatus { get; set; }

    public Guid PatientId { get; set; }

    public Guid IdentityUserId { get; set; }

    public Guid AppointmentTypeId { get; set; }

    public Guid LocationId { get; set; }

    public Guid DoctorAvailabilityId { get; set; }

    protected Appointment()
    {
    }

    public Appointment(Guid id, Guid patientId, Guid identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string requestConfirmationNumber, AppointmentStatusType appointmentStatus, string? panelNumber = null, DateTime? dueDate = null)
    {
        Id = id;
        Check.NotNull(requestConfirmationNumber, nameof(requestConfirmationNumber));
        Check.Length(requestConfirmationNumber, nameof(requestConfirmationNumber), AppointmentConsts.RequestConfirmationNumberMaxLength, 0);
        Check.Length(panelNumber, nameof(panelNumber), AppointmentConsts.PanelNumberMaxLength, 0);
        AppointmentDate = appointmentDate;
        RequestConfirmationNumber = requestConfirmationNumber;
        AppointmentStatus = appointmentStatus;
        PanelNumber = panelNumber;
        DueDate = dueDate;
        PatientId = patientId;
        IdentityUserId = identityUserId;
        AppointmentTypeId = appointmentTypeId;
        LocationId = locationId;
        DoctorAvailabilityId = doctorAvailabilityId;
    }
}