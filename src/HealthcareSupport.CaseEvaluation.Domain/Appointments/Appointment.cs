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

    public Guid? IdentityUserId { get; set; }

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

    // ---- Attorney snapshot (#9, 2026-06-19) ----
    // Booking-time copy of the applicant / defense attorney's name + firm + contact,
    // captured from the master when the attorney is linked to (or edited on) THIS
    // appointment. The detail reads snapshot ?? master, so an attorney's later
    // self-edit of their master record never rewrites past appointments. Null on
    // appointments booked before these columns existed -- those fall back to the
    // master join (forward-only immutability; no backfill).

    [CanBeNull]
    public virtual string? ApplicantAttorneyFirstName { get; set; }

    [CanBeNull]
    public virtual string? ApplicantAttorneyLastName { get; set; }

    [CanBeNull]
    public virtual string? ApplicantAttorneyFirmName { get; set; }

    [CanBeNull]
    public virtual string? ApplicantAttorneyWebAddress { get; set; }

    [CanBeNull]
    public virtual string? ApplicantAttorneyPhoneNumber { get; set; }

    [CanBeNull]
    public virtual string? ApplicantAttorneyFaxNumber { get; set; }

    [CanBeNull]
    public virtual string? ApplicantAttorneyStreet { get; set; }

    [CanBeNull]
    public virtual string? ApplicantAttorneyCity { get; set; }

    public virtual Guid? ApplicantAttorneyStateId { get; set; }

    [CanBeNull]
    public virtual string? ApplicantAttorneyZipCode { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyFirstName { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyLastName { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyFirmName { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyWebAddress { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyPhoneNumber { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyFaxNumber { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyStreet { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyCity { get; set; }

    public virtual Guid? DefenseAttorneyStateId { get; set; }

    [CanBeNull]
    public virtual string? DefenseAttorneyZipCode { get; set; }

    /// <summary>
    /// 2026-06-09: optional per-appointment "Referred By" (referring source).
    /// Per-appointment by design -- NOT carried over from the patient or prior
    /// appointments. Blank unless the booker explicitly fills it.
    /// </summary>
    [CanBeNull]
    public virtual string? RefferedBy { get; set; }

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

    /// <summary>
    /// R2-2 (2026-06-22): the user who booked this appointment -- the logged-in
    /// party, or staff / a paralegal acting on their behalf. Stamped explicitly at
    /// create time via <see cref="RecordBookedBy"/> so the booker's own list always
    /// shows the appointment, independent of the ABP audit <c>CreatorId</c> (which
    /// the audit interceptor skips on a tenant-claim mismatch, and which is null on
    /// record-only bookings for an unregistered patient). Carried forward onto a
    /// reschedule clone so the booker stays linked across the lifecycle.
    /// </summary>
    public virtual Guid? BookedByUserId { get; set; }

    protected Appointment()
    {
    }

    public Appointment(Guid id, Guid patientId, Guid? identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string requestConfirmationNumber, AppointmentStatusType appointmentStatus, string? panelNumber = null, DateTime? dueDate = null)
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

    /// <summary>
    /// Stamps the booking user at create time. Throws on an empty id so every
    /// appointment carries a real booker identity (D-R2-B). The reschedule clone
    /// copies the value directly via the property to preserve the original booker.
    /// </summary>
    public virtual void RecordBookedBy(Guid bookedByUserId)
    {
        if (bookedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Booked-by user id is required.", nameof(bookedByUserId));
        }
        BookedByUserId = bookedByUserId;
    }
}