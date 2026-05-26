using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class AppointmentDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string? PanelNumber { get; set; }

    public DateTime AppointmentDate { get; set; }

    public bool IsPatientAlreadyExist { get; set; }

    public string RequestConfirmationNumber { get; set; } = null!;
    public DateTime? DueDate { get; set; }

    public string? InternalUserComments { get; set; }

    public DateTime? AppointmentApproveDate { get; set; }

    public AppointmentStatusType AppointmentStatus { get; set; }

    /// <summary>
    /// F4-02 (2026-05-26) -- rejection reason persisted by the reject
    /// flow (<see cref="HealthcareSupport.CaseEvaluation.Appointments.AppointmentsAppService"/>
    /// reject path) and read back here so the patient and staff can
    /// see WHY the appointment was rejected. OLD parity: the
    /// PatientAppointmentRejected email template renders this value
    /// via the <c>##RejectionNotes##</c> token.
    /// </summary>
    public string? RejectionNotes { get; set; }

    /// <summary>F4-02 (2026-05-26) -- audit pair with RejectionNotes.</summary>
    public Guid? RejectedById { get; set; }

    public Guid PatientId { get; set; }

    public Guid IdentityUserId { get; set; }

    public Guid AppointmentTypeId { get; set; }

    public Guid LocationId { get; set; }

    public Guid DoctorAvailabilityId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;

    public string? PatientEmail { get; set; }
    public string? ApplicantAttorneyEmail { get; set; }
    public string? DefenseAttorneyEmail { get; set; }
    public string? ClaimExaminerEmail { get; set; }
}