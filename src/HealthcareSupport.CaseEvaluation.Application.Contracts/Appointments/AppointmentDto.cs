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

    // IP6 (2026-06-05): nullable -- booking no longer mints a patient login.
    public Guid? IdentityUserId { get; set; }

    public Guid AppointmentTypeId { get; set; }

    public Guid LocationId { get; set; }

    public Guid DoctorAvailabilityId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;

    public string? PatientEmail { get; set; }
    public string? ApplicantAttorneyEmail { get; set; }
    public string? DefenseAttorneyEmail { get; set; }
    public string? ClaimExaminerEmail { get; set; }

    // #9 (2026-06-19): booking-time attorney snapshot. Read snapshot ?? master on
    // the client so a master self-edit never rewrites a past appointment. Null on
    // pre-migration appointments (those fall back to the master nav-property).
    public string? ApplicantAttorneyFirstName { get; set; }
    public string? ApplicantAttorneyLastName { get; set; }
    public string? ApplicantAttorneyFirmName { get; set; }
    public string? ApplicantAttorneyWebAddress { get; set; }
    public string? ApplicantAttorneyPhoneNumber { get; set; }
    public string? ApplicantAttorneyFaxNumber { get; set; }
    public string? ApplicantAttorneyStreet { get; set; }
    public string? ApplicantAttorneyCity { get; set; }
    public Guid? ApplicantAttorneyStateId { get; set; }
    public string? ApplicantAttorneyZipCode { get; set; }
    public string? DefenseAttorneyFirstName { get; set; }
    public string? DefenseAttorneyLastName { get; set; }
    public string? DefenseAttorneyFirmName { get; set; }
    public string? DefenseAttorneyWebAddress { get; set; }
    public string? DefenseAttorneyPhoneNumber { get; set; }
    public string? DefenseAttorneyFaxNumber { get; set; }
    public string? DefenseAttorneyStreet { get; set; }
    public string? DefenseAttorneyCity { get; set; }
    public Guid? DefenseAttorneyStateId { get; set; }
    public string? DefenseAttorneyZipCode { get; set; }

    /// <summary>2026-06-09: optional per-appointment "Referred By".</summary>
    public string? RefferedBy { get; set; }
}