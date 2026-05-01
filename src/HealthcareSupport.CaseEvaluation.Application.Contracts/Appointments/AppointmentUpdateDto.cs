using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class AppointmentUpdateDto : IHasConcurrencyStamp
{
    [StringLength(AppointmentConsts.PanelNumberMaxLength)]
    public string? PanelNumber { get; set; }

    public DateTime AppointmentDate { get; set; }
    public string RequestConfirmationNumber { get; set; } = null!;
    public DateTime? DueDate { get; set; }

    public Guid PatientId { get; set; }

    public Guid IdentityUserId { get; set; }

    public Guid AppointmentTypeId { get; set; }

    public Guid LocationId { get; set; }

    public Guid DoctorAvailabilityId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;

    [StringLength(AppointmentConsts.PartyEmailMaxLength)]
    public string? PatientEmail { get; set; }

    [StringLength(AppointmentConsts.PartyEmailMaxLength)]
    public string? ApplicantAttorneyEmail { get; set; }

    [StringLength(AppointmentConsts.PartyEmailMaxLength)]
    public string? DefenseAttorneyEmail { get; set; }

    [StringLength(AppointmentConsts.PartyEmailMaxLength)]
    public string? ClaimExaminerEmail { get; set; }
}