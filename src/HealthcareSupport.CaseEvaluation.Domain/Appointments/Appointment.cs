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
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Appointments;

[Audited]
public class Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [CanBeNull]
    public virtual string? PanelNumber { get; set; }

    public virtual DateTime AppointmentDate { get; set; }

    public virtual bool IsPatientAlreadyExist { get; set; }

    [NotNull]
    public virtual string RequestConfirmationNumber { get; set; } = null!;

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

    [CanBeNull]
    public virtual string? PatientEmail { get; set; }

    [CanBeNull]
    public virtual string? ApplicantAttorneyEmail { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyEmail { get; set; }

    [CanBeNull]
    public virtual string? ClaimExaminerEmail { get; set; }

    /// <summary>
    /// Reschedule-chain link: when this appointment is a reschedule of a
    /// prior one, points at the prior <see cref="Appointment"/>'s Id. Null
    /// for first-time bookings. Mirrors OLD's <c>OriginalAppointmentId</c>
    /// (Phase 1.6, 2026-05-01).
    /// </summary>
    public virtual Guid? OriginalAppointmentId { get; set; }

    [CanBeNull]
    public virtual string? ReScheduleReason { get; set; }

    public virtual Guid? ReScheduledById { get; set; }

    [CanBeNull]
    public virtual string? CancellationReason { get; set; }

    public virtual Guid? CancelledById { get; set; }

    [CanBeNull]
    public virtual string? RejectionNotes { get; set; }

    public virtual Guid? RejectedById { get; set; }

    /// <summary>Internal staff user assigned as the primary responsible user on approval.</summary>
    public virtual Guid? PrimaryResponsibleUserId { get; set; }

    /// <summary>
    /// Admin override flag: when true, the appointment was scheduled past
    /// the per-type max-time window. Set by IT Admin during reschedule
    /// approval; lifts the lead-time / max-time gate.
    /// </summary>
    public virtual bool IsBeyondLimit { get; set; }

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