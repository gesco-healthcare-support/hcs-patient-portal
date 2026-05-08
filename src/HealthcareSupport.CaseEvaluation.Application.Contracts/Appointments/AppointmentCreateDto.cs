using HealthcareSupport.CaseEvaluation.CustomFields;
using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class AppointmentCreateDto
{
    [StringLength(AppointmentConsts.PanelNumberMaxLength)]
    public string? PanelNumber { get; set; }

    public DateTime AppointmentDate { get; set; }

    public string RequestConfirmationNumber { get; set; } = null!;
    public DateTime? DueDate { get; set; }

    public AppointmentStatusType AppointmentStatus { get; set; } = Enum.GetValues<AppointmentStatusType>()[0];
    public Guid PatientId { get; set; }

    public Guid IdentityUserId { get; set; }

    public Guid AppointmentTypeId { get; set; }

    public Guid LocationId { get; set; }

    public Guid DoctorAvailabilityId { get; set; }

    [StringLength(AppointmentConsts.PartyEmailMaxLength)]
    public string? PatientEmail { get; set; }

    [StringLength(AppointmentConsts.PartyEmailMaxLength)]
    public string? ApplicantAttorneyEmail { get; set; }

    [StringLength(AppointmentConsts.PartyEmailMaxLength)]
    public string? DefenseAttorneyEmail { get; set; }

    [StringLength(AppointmentConsts.PartyEmailMaxLength)]
    public string? ClaimExaminerEmail { get; set; }

    /// <summary>
    /// R2 (Phase 9, 2026-05-04) -- mirrors OLD's
    /// <c>P:\PatientPortalOld\PatientAppointment.Domain\Core\AppointmentDomain.cs:210, 217</c>
    /// where <c>Appointment.IsPatientAlreadyExist</c> is set on initial booking
    /// from the dedup outcome. The Angular booking form must populate this
    /// from the <see cref="HealthcareSupport.CaseEvaluation.Patients.PatientWithNavigationPropertiesDto.IsExisting"/>
    /// flag returned by <c>GetOrCreatePatientForAppointmentBookingAsync</c>.
    /// Defaults to <c>false</c> for backward compat with callers that haven't
    /// been updated yet.
    /// </summary>
    public bool IsPatientAlreadyExist { get; set; }

    /// <summary>
    /// B1 (2026-05-05) -- IT-Admin-defined per-AppointmentType custom fields
    /// answered by the booker. Each entry is (CustomFieldId, Value); the
    /// AppService persists one <c>CustomFieldValue</c> row per non-empty entry.
    /// Mirrors OLD's <c>spm.CustomFieldsValues</c> write that happens alongside
    /// the appointment insert in
    /// <c>PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>.
    /// </summary>
    public List<CustomFieldValueInputDto> CustomFieldValues { get; set; } = new();
}