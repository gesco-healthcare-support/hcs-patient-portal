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