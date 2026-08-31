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
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Appointments;

[Audited]
public class Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [CanBeNull]
    public virtual string? PanelNumber { get; set; }

    // CALENDAR DATE + wall-clock time of the appointment, not an instant -- see
    // CalendarDateNormalizationTests. Without this ABP kinds it Utc on read, it serializes with a
    // trailing Z, and every browser shifts it by its own offset.
    [DisableDateTimeNormalization]
    public virtual DateTime AppointmentDate { get; set; }

    public virtual bool IsPatientAlreadyExist { get; set; }

    [NotNull]
    public virtual string RequestConfirmationNumber { get; set; } = null!;

    // CALENDAR DATE (picked in the UI, arrives on the input DTO), not an instant -- see
    // CalendarDateNormalizationTests.
    [DisableDateTimeNormalization]
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

    // ---- Patient snapshot (item 5, 2026-08-14) --------------------------------
    //
    // WHY these are denormalised onto the appointment: an appointment is a legal
    // trail of what was served on a given date, so editing the shared Patient row
    // must not retroactively change what a PRIOR appointment reports. Attorneys,
    // employers, insurers and claim examiners already had this property; the
    // patient was the one party still read live, which meant a corrected address
    // silently rewrote every past appointment's Case Tracker payload and every
    // regenerated packet.
    //
    // NULL means "booked before this shipped" -- read the live patient instead.
    // Existing rows were deliberately NOT backfilled: we cannot know what a
    // patient's details were when a May appointment was booked, and stamping
    // today's values onto a legal record would assert a history we cannot
    // support. Resolve through AppointmentPatientSnapshotResolver so the fallback
    // lives in exactly one place.
    //
    // Records freeze; CONTACT does not. Notification and recipient resolution keep
    // reading the live patient, because a reminder must reach the address the
    // patient has today. PatientEmail above predates this block and stays a
    // contact-side value.
    //
    // Patient.Id is deliberately absent: SamePersonGroupKey is computed from it and
    // is what tells the Case Tracker two claims belong to the same person.

    [CanBeNull]
    public virtual string? PatientFirstName { get; set; }

    [CanBeNull]
    public virtual string? PatientMiddleName { get; set; }

    [CanBeNull]
    public virtual string? PatientLastName { get; set; }

    // CALENDAR DATE -- the snapshot of Patient.DateOfBirth, so it must be exempted for the same
    // reason and in the same way. See CalendarDateNormalizationTests.
    [DisableDateTimeNormalization]
    public virtual DateTime? PatientDateOfBirth { get; set; }

    /// <summary>
    /// Copied because <c>PacketTokenResolver</c> renders the SSN on generated
    /// documents; without it a frozen packet still moves when the patient record is
    /// corrected. This duplicates PHI into a table that already holds names, dates
    /// of birth and addresses for the same person, under the same tenant isolation.
    /// It must NOT become a second, unaudited read path -- the audited reveal
    /// endpoint (<c>PatientsAppService.GetFullSsnAsync</c>) remains the only way to
    /// read an unmasked SSN through the API.
    /// </summary>
    [CanBeNull]
    public virtual string? PatientSocialSecurityNumber { get; set; }

    [CanBeNull]
    public virtual string? PatientPhoneNumber { get; set; }

    [CanBeNull]
    public virtual string? PatientCellPhoneNumber { get; set; }

    public virtual PhoneNumberType? PatientPhoneNumberTypeId { get; set; }

    /// <summary>Street line 1. See <see cref="PatientApptNumber"/> for the unit.</summary>
    [CanBeNull]
    public virtual string? PatientStreet { get; set; }

    /// <summary>
    /// The unit / suite number. Named after <c>Patient.ApptNumber</c>, which is where
    /// every writer now puts it; the legacy <c>Patient.Address</c> column held units
    /// on older rows and is not snapshotted, because no new booking writes it.
    /// </summary>
    [CanBeNull]
    public virtual string? PatientApptNumber { get; set; }

    [CanBeNull]
    public virtual string? PatientCity { get; set; }

    public virtual Guid? PatientStateId { get; set; }

    [CanBeNull]
    public virtual string? PatientZipCode { get; set; }

    public virtual Gender? PatientGenderId { get; set; }

    [CanBeNull]
    public virtual string? PatientInterpreterVendorName { get; set; }

    // ---- end patient snapshot ------------------------------------------------

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
    /// RE-EVALUATION chain link: when this appointment is a re-evaluation of a prior one, points
    /// at the prior <see cref="Appointment"/>'s Id. Null for first-time bookings. Mirrors OLD's
    /// <c>OriginalAppointmentId</c> (Phase 1.6, 2026-05-01).
    ///
    /// <para>HISTORICAL AMBIGUITY, clarified in phase 4d (2026-08-05): OLD used this column for the
    /// RESCHEDULE chain and pre-2026-07-01 rows may still carry it for that reason, which is why
    /// <see cref="EvaluationKind"/> is persisted rather than derived from it. Current code writes
    /// it only for re-evaluations. The reschedule chain now has its OWN column,
    /// <see cref="RescheduledFromAppointmentId"/> -- do NOT overload this one again.</para>
    /// </summary>
    public virtual Guid? OriginalAppointmentId { get; set; }

    /// <summary>
    /// RESCHEDULE chain link (phase 4d, 2026-08-05): when this appointment was created by
    /// finalizing a reschedule, points at the appointment it replaced. Null for every other
    /// appointment, including re-evaluations.
    ///
    /// <para>Deliberately NOT <see cref="OriginalAppointmentId"/>: that column already carries the
    /// re-evaluation meaning in current code and the reschedule meaning in legacy rows, and a
    /// dual-purpose link is exactly what mislabels a Case Tracker case folder. A second column
    /// costs one migration per context and removes the ambiguity for good.</para>
    ///
    /// <para>The change request and its consent rounds stay on the OLD appointment; this link is
    /// how the new appointment explains where it came from.</para>
    /// </summary>
    public virtual Guid? RescheduledFromAppointmentId { get; set; }

    /// <summary>
    /// Whether this is a first evaluation or a re-evaluation (2026-07-27, Case Tracker
    /// integration). Stamped at booking from the lifecycle flow, NOT derived from
    /// <see cref="OriginalAppointmentId"/>: that column is documented as a reschedule-chain link
    /// and pre-2026-07-01 rows may carry it for that reason, so deriving would mislabel. The Case
    /// Tracker uses this to label a case folder, so a wrong value is an operational problem in
    /// another system. Defaults to <see cref="EvaluationKind.Evaluation"/>; the migration
    /// backfills existing rows to the same value (no re-evaluations exist yet).
    /// </summary>
    public virtual EvaluationKind EvaluationKind { get; set; } = EvaluationKind.Evaluation;

    /// <summary>
    /// When this AME appointment passed its Joint Declaration Form deadline with no JDF uploaded.
    /// Null means not overdue.
    ///
    /// <para>Added 2026-08-08 REPLACING an automatic cancellation. The daily job used to set such an
    /// appointment to <c>CancelledNoBill</c> outright, with no human involved -- Adrian: "auto-cancel
    /// without staff or anyone's involvement seems like a risky thing and we should not do that."
    /// The appointment now KEEPS its status and carries this marker instead, so a person decides.</para>
    ///
    /// <para>Stamped ONCE and not refreshed on later runs, so it records when the deadline actually
    /// passed rather than when the job last looked. CLEARED if a JDF is uploaded afterwards, because
    /// the flag describes the CURRENT state -- a latched marker would keep nagging about a document
    /// that has since arrived.</para>
    /// </summary>
    public virtual DateTime? JointDeclarationOverdueAt { get; set; }

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

    /// <summary>
    /// Carries this case's denormalised party snapshot onto a replacement appointment, for the
    /// reschedule split (phase 4d) which creates a NEW row for the same case rather than moving
    /// the old one.
    ///
    /// <para><b>Why this is not merely cosmetic.</b> Found live on 2026-08-26: the split created the
    /// new appointment with these columns NULL, and the external party of record was then 403'd off
    /// their own replacement appointment while holding its packet PDF in their inbox.
    /// <c>AppointmentReadAccessGuard</c> admits an external caller either by id (booker / patient
    /// identity / accessor row) or by the email+role rule, and that rule reads
    /// <see cref="PatientEmail"/>, <see cref="ApplicantAttorneyEmail"/>,
    /// <see cref="DefenseAttorneyEmail"/> and <see cref="ClaimExaminerEmail"/> off the appointment.
    /// With all four null, and <c>CreatorId</c> now the staff approver, every pathway failed.</para>
    ///
    /// <para>The child rows (attorney / examiner / employer / injuries) are copied separately by
    /// <c>IAppointmentChildCascadeCopier</c>, which is why notifications still reached the parties
    /// and hid the gap -- the packet path resolves through
    /// <c>AppointmentPatientSnapshotResolver</c>, which falls back to the Patient master. The access
    /// guard has no such fallback, so it was the only consumer that broke.</para>
    ///
    /// <para>Copied wholesale rather than field-by-field at the call site because these columns are
    /// one thing: what was true of this case AT BOOKING TIME. A replacement appointment continues
    /// the same case, so it inherits the same snapshot. Deliberately NOT copied:
    /// <see cref="PatientId"/> and the slot / date / confirmation-number fields, which the
    /// replacement defines for itself.</para>
    /// </summary>
    public virtual void CopyPartySnapshotFrom(Appointment source)
    {
        ArgumentNullException.ThrowIfNull(source);

        PatientEmail = source.PatientEmail;
        PatientFirstName = source.PatientFirstName;
        PatientMiddleName = source.PatientMiddleName;
        PatientLastName = source.PatientLastName;
        PatientDateOfBirth = source.PatientDateOfBirth;
        PatientSocialSecurityNumber = source.PatientSocialSecurityNumber;
        PatientPhoneNumber = source.PatientPhoneNumber;
        PatientPhoneNumberTypeId = source.PatientPhoneNumberTypeId;
        PatientCellPhoneNumber = source.PatientCellPhoneNumber;
        PatientStreet = source.PatientStreet;
        PatientApptNumber = source.PatientApptNumber;
        PatientCity = source.PatientCity;
        PatientStateId = source.PatientStateId;
        PatientZipCode = source.PatientZipCode;
        PatientGenderId = source.PatientGenderId;
        PatientInterpreterVendorName = source.PatientInterpreterVendorName;

        ApplicantAttorneyEmail = source.ApplicantAttorneyEmail;
        ApplicantAttorneyFirstName = source.ApplicantAttorneyFirstName;
        ApplicantAttorneyLastName = source.ApplicantAttorneyLastName;
        ApplicantAttorneyFirmName = source.ApplicantAttorneyFirmName;
        ApplicantAttorneyWebAddress = source.ApplicantAttorneyWebAddress;
        ApplicantAttorneyPhoneNumber = source.ApplicantAttorneyPhoneNumber;
        ApplicantAttorneyFaxNumber = source.ApplicantAttorneyFaxNumber;
        ApplicantAttorneyStreet = source.ApplicantAttorneyStreet;
        ApplicantAttorneyCity = source.ApplicantAttorneyCity;
        ApplicantAttorneyStateId = source.ApplicantAttorneyStateId;
        ApplicantAttorneyZipCode = source.ApplicantAttorneyZipCode;

        DefenseAttorneyEmail = source.DefenseAttorneyEmail;
        DefenseAttorneyFirstName = source.DefenseAttorneyFirstName;
        DefenseAttorneyLastName = source.DefenseAttorneyLastName;
        DefenseAttorneyFirmName = source.DefenseAttorneyFirmName;
        DefenseAttorneyWebAddress = source.DefenseAttorneyWebAddress;
        DefenseAttorneyPhoneNumber = source.DefenseAttorneyPhoneNumber;
        DefenseAttorneyFaxNumber = source.DefenseAttorneyFaxNumber;
        DefenseAttorneyStreet = source.DefenseAttorneyStreet;
        DefenseAttorneyCity = source.DefenseAttorneyCity;
        DefenseAttorneyStateId = source.DefenseAttorneyStateId;
        DefenseAttorneyZipCode = source.DefenseAttorneyZipCode;

        ClaimExaminerEmail = source.ClaimExaminerEmail;
    }
}