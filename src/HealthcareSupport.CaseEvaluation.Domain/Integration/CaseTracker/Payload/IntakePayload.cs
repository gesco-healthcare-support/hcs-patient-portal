using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The intake payload published to the Case Tracker, field-for-field per
/// <c>docs/integration/case-tracker-api-contract.md</c> section A. Property names are PascalCase
/// here and serialized camelCase by the client's serializer options.
///
/// <para>Timestamps are PRE-FORMATTED strings, not <c>DateTime</c>, deliberately. EF Core reads
/// <c>datetime2</c> back with <c>DateTimeKind.Unspecified</c>, and System.Text.Json then emits it
/// WITHOUT the trailing <c>Z</c> the contract requires. Formatting once, explicitly, at the
/// boundary removes that whole class of bug. <see cref="UpdatedAt"/> keeps sub-second precision
/// because the receiver uses it as a monotonic skip-if-older guard -- truncating to seconds could
/// make two rapid edits compare equal and drop the newer one.</para>
///
/// <para>NOTE: no patient identifier is present, by design. The portal is database-per-office so it
/// has no cross-office patient identity, and CalMed mints a new patient id per claim -- anything we
/// sent would look authoritative and not be. The only linking facts are
/// <see cref="PreviousAppointmentId"/> (machine) and <see cref="PreviousConfirmationNumber"/>
/// (human aid).</para>
/// </summary>
public class IntakePayload
{
    /// <summary>Stable, globally unique. The receiver's upsert key.</summary>
    public Guid AppointmentId { get; set; }

    /// <summary>Human reference, e.g. <c>A00065</c>. Per-office sequential -- NOT a key.</summary>
    public string ConfirmationNumber { get; set; } = string.Empty;

    /// <summary>
    /// <c>AppointmentStatusType</c> name. At intake always <c>Approved</c>; later re-pushes may
    /// carry the cancellation / reschedule states (contract section A status table).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Billing intent as an explicit value (<c>NO_BILL</c> / <c>LATE</c> / <c>NONE</c>) so the
    /// receiver need not string-match <see cref="Status"/>. Always present; <c>NONE</c> when the
    /// appointment is not in a billing-bearing state. <see cref="Status"/> stays authoritative
    /// for lifecycle.
    /// </summary>
    public string BillingStatus { get; set; } = BillingStatusWire.None;

    /// <summary>
    /// Why the appointment was cancelled. Null unless it was. USER-AUTHORED FREE TEXT (or the
    /// auto-cancel constant) -- treat as untrusted display data and never log it.
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>ISO-8601 UTC. Null only if pushed before approval, which the trigger prevents.</summary>
    public string? ApprovedAtUtc { get; set; }

    /// <summary>ISO-8601 UTC.</summary>
    public string SubmittedAtUtc { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC, monotonic per appointment. Drives the receiver's staleness guard.</summary>
    public string UpdatedAt { get; set; } = string.Empty;

    /// <summary><c>EVAL</c> or <c>RE_EVAL</c>.</summary>
    public string EvaluationKind { get; set; } = string.Empty;

    /// <summary>
    /// The original appointment on a RE-EVALUATION; null on a first evaluation.
    ///
    /// <para>This is the RE-EVAL chain and nothing else. The RESCHEDULE chain is
    /// <see cref="RescheduledFromAppointmentId"/> -- a separate pair, because the two relationships
    /// differ: a re-evaluated appointment HAPPENED and is followed up, a rescheduled one did NOT
    /// happen and is replaced. Conflating them is what <c>evaluationKind</c> was added to prevent.</para>
    /// </summary>
    public Guid? PreviousAppointmentId { get; set; }

    /// <summary>The original's confirmation number. Display only -- a re-eval gets its own.</summary>
    public string? PreviousConfirmationNumber { get; set; }

    // ---- Reschedule chain (phase 4e, 2026-08-06) ----
    // Finalizing a reschedule closes one appointment and opens another, so one claim becomes TWO
    // cases. These four fields are what let the receiver join them back up and say why.

    /// <summary>
    /// On a REPLACEMENT created by finalizing a reschedule: the appointment it replaced. Null
    /// otherwise. Distinct from <see cref="PreviousAppointmentId"/>; see that field.
    /// </summary>
    public Guid? RescheduledFromAppointmentId { get; set; }

    /// <summary>
    /// The replaced appointment's confirmation number. Display only -- a replacement gets its own,
    /// and the value is per-office sequential so it repeats across offices. Match on the id.
    /// </summary>
    public string? RescheduledFromConfirmationNumber { get; set; }

    /// <summary>
    /// On a CLOSED appointment that was replaced: the appointment that replaced it. Null otherwise.
    /// The forward half of the pair, so a closed case is not a dead end for their staff.
    /// </summary>
    public Guid? SupersededByAppointmentId { get; set; }

    /// <summary>
    /// WHY it was superseded (<c>RESCHEDULED</c>). Present exactly when
    /// <see cref="SupersededByAppointmentId"/> is. Sent explicitly because the id alone cannot say
    /// what kind of successor it is -- see <see cref="SupersededReasonWire"/>.
    /// </summary>
    public string? SupersededReason { get; set; }

    // ---- Change attribution (phase 6, 2026-08-08) ----
    // Who asked for the most recent change to this appointment, and when it was asked and settled.
    // All four are null when the appointment has never had a change request, which is the common
    // case -- absence here means "nothing was requested", not "we failed to look".

    /// <summary>
    /// Which side requested the change: <c>SIDE_A</c> (patient + applicant attorney) or
    /// <c>SIDE_B</c> (defense attorney + claim examiner). See <see cref="ChangeRequestSideWire"/>.
    ///
    /// <para>Null when staff initiated the change themselves, because then no PARTY requested it.
    /// Do not read null as "unknown".</para>
    /// </summary>
    public string? ChangeRequestedBySide { get; set; }

    /// <summary>
    /// What was asked for: <c>CANCEL</c> or <c>RESCHEDULE</c>, matching the portal's own two
    /// change-request kinds.
    /// </summary>
    public string? ChangeRequestType { get; set; }

    /// <summary>ISO-8601 UTC. When the change was REQUESTED.</summary>
    public string? ChangeRequestedAtUtc { get; set; }

    /// <summary>
    /// ISO-8601 UTC. When staff DECIDED the change -- accepted or rejected.
    ///
    /// <para>Null while the request is still pending, which is a real state and not missing data.
    /// Sourced from the appointment change request's own decision stamp, never from a
    /// last-modified column: that reflects the last write of ANY kind, so a later edit would
    /// silently relabel when the decision was made.</para>
    /// </summary>
    public string? ChangeFinalizedAtUtc { get; set; }

    public IntakeTenantSection Tenant { get; set; } = new();

    public IntakeLocationSection Location { get; set; } = new();

    public IntakeAppointmentTypeSection AppointmentType { get; set; } = new();

    public string? PanelNumber { get; set; }

    /// <summary>Clinic-local date, <c>yyyy-MM-dd</c>.</summary>
    public string AppointmentDateLocal { get; set; } = string.Empty;

    /// <summary>Clinic-local start time, <c>HH:mm</c> (24h).</summary>
    public string AppointmentTimeLocal { get; set; } = string.Empty;

    /// <summary>IANA zone the local date/time are expressed in.</summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>Derived from the slot's From/To times; the portal stores no duration.</summary>
    public int DurationMinutes { get; set; }

    public IntakePatientSection Patient { get; set; } = new();

    public IntakeDoctorSection Doctor { get; set; } = new();

    public IntakeStorageSection Storage { get; set; } = new();

    /// <summary>Only FETCHABLE files. Empty at intake because packets render asynchronously.</summary>
    public List<IntakeDocumentEntry> Documents { get; set; } = new();

    /// <summary>
    /// Every injury recorded on the appointment. Added 2026-07-28 so the receiver's staff can tell which
    /// of a patient's claims a case belongs to -- previously they saw only name and date of birth and
    /// were filing records against the wrong claim.
    ///
    /// <para>Expected non-empty in practice (booking blocks submit without at least one entry) but the
    /// guard is client-side only, so treat an empty array as possible rather than impossible.</para>
    /// </summary>
    public List<IntakeInjuryEntry> Injuries { get; set; } = new();

    /// <summary>The applicant (patient-side) attorney; null when none was recorded.</summary>
    public IntakeAttorneySection? ApplicantAttorney { get; set; }

    /// <summary>The defense attorney; null when none was recorded.</summary>
    public IntakeAttorneySection? DefenseAttorney { get; set; }

    /// <summary>
    /// Active insurance carriers. TOP-LEVEL, not nested per injury, because the portal stores no link
    /// between a carrier and a specific injury -- see <see cref="IntakeInsuranceSection"/>.
    /// </summary>
    public List<IntakeInsuranceSection> PrimaryInsurances { get; set; } = new();

    /// <summary>Active claim examiners. Same appointment-level caveat as the insurances.</summary>
    public List<IntakeClaimExaminerSection> ClaimExaminers { get; set; } = new();
}

/// <summary>Owning office. <see cref="FacilityId"/> is the clinic's external key.</summary>
public class IntakeTenantSection
{
    public Guid? TenantId { get; set; }

    /// <summary>From the appointment's clinic; may be empty on legacy location rows.</summary>
    public string FacilityId { get; set; } = string.Empty;

    public string OfficeName { get; set; } = string.Empty;
}

/// <summary>The specific clinic this appointment is booked at.</summary>
public class IntakeLocationSection
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }
}

/// <summary>Branch on <see cref="Id"/>; <see cref="Name"/> is admin-editable free text.</summary>
public class IntakeAppointmentTypeSection
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Patient demographics. <see cref="DateOfBirth"/> is included at the Case Tracker's request purely
/// so staff can eyeball a mismatch before a mistyped Cal-Med id creates an orphan folder -- it is
/// explicitly not a key and not for automated matching.
/// </summary>
public class IntakePatientSection
{
    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary><c>yyyy-MM-dd</c>.</summary>
    public string DateOfBirth { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    /// <summary><c>Home</c> or <c>Work</c>.</summary>
    public string PhoneNumberType { get; set; } = string.Empty;

    public string? CellPhoneNumber { get; set; }

    /// <summary>
    /// Street address, line 1.
    ///
    /// <para>Backed by <c>Patient.Street</c>, which the booking form labels "Street" and fills via
    /// the address autocomplete. Do NOT confuse it with the column <c>Patient.Address</c>, whose
    /// name is misleading -- see <see cref="Unit"/>.</para>
    /// </summary>
    public string? Street { get; set; }

    /// <summary>
    /// Apartment / suite number, when the patient supplied one.
    ///
    /// <para>Called <c>unit</c> on the wire even though it is backed by the column
    /// <c>Patient.Address</c>, because "Unit #" is what the booking form asks for and what the value
    /// actually holds. Publishing it as <c>address</c> would invite the receiver to render a bare
    /// "4B" as a street address. The column name is a historical accident; this name is the truth.
    /// </para>
    /// </summary>
    public string? Unit { get; set; }

    public string? City { get; set; }

    /// <summary>
    /// State NAME (e.g. <c>California</c>), not the portal's lookup id -- matching how the attorney
    /// and claim-examiner sections already publish state.
    /// </summary>
    public string? State { get; set; }

    public string? ZipCode { get; set; }

    /// <summary>
    /// Opaque, office-scoped token that is EQUAL for two appointments belonging to the same patient, so
    /// the receiver's staff can be shown "these two claims are the same person".
    ///
    /// <para>A hash, not our <c>Patient.Id</c>. Equality is the receiver's only use, and our patient row
    /// key means nothing in CalMed's world where patient identity is actually minted -- publishing it raw
    /// would invite something downstream to store it as a patient identifier. Deliberately NOT named
    /// <c>portalPatientId</c> for the same reason. Salted with the office so a cross-office false match
    /// is impossible by construction. See <see cref="SamePersonGroupKey"/>.</para>
    /// </summary>
    public string SamePersonGroupKey { get; set; } = string.Empty;
}

/// <summary>The office's single doctor (tenant == doctor). FirstName can legitimately be empty.</summary>
public class IntakeDoctorSection
{
    /// <summary>
    /// The doctor's stable portal identifier, for matching instead of the name.
    ///
    /// <para>Added 2026-07-31 at the Case Tracker team's request. Their matcher previously keyed on
    /// first + last name, which failed on the first live push and left staff picking the doctor by
    /// hand on every intake -- two systems cannot be relied on to spell a name identically forever.
    /// Map once against this and ignore the name for matching.</para>
    ///
    /// <para>This is the portal's own row key, stable for the life of the doctor record. It is NOT a
    /// licence number or any externally-minted identifier, and it is deliberately NOT the CalMed
    /// Facility ID equivalent -- conflating our surrogate keys with externally-minted ones is a
    /// mistake this integration has had to correct before.</para>
    ///
    /// <para>Nullable because an office with no doctor record resolves to an empty section; null says
    /// "no doctor on file" honestly, where <c>Guid.Empty</c> would look like a real identifier the
    /// receiver could try to map.</para>
    /// </summary>
    public Guid? Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;
}

/// <summary>
/// Which bucket the document object keys live in. Endpoint, region and credentials are deliberately
/// NOT sent -- the receiver takes those from its own config so a forged push cannot redirect its S3
/// client at a hostile host.
/// </summary>
public class IntakeStorageSection
{
    public string Bucket { get; set; } = string.Empty;
}

/// <summary>
/// One fetchable file. Union over uploaded documents and generated packets; see contract section B.
/// </summary>
public class IntakeDocumentEntry
{
    /// <summary>Stable per-file key for the receiver's upsert.</summary>
    public Guid Id { get; set; }

    /// <summary><c>document</c> (uploaded) or <c>packet</c> (generated).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Packets only: <c>Patient</c> / <c>Doctor</c> / <c>AttorneyClaimExaminer</c>.</summary>
    public string? Kind { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    /// <summary>Client-supplied for uploads (so nullable); always <c>application/pdf</c> for packets.</summary>
    public string? ContentType { get; set; }

    /// <summary>Null for packets -- the portal does not store a packet size.</summary>
    public long? FileSize { get; set; }

    /// <summary>Document: Uploaded/Accepted/Rejected. Packet: Generated.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Fully-qualified within <see cref="IntakeStorageSection.Bucket"/>. Opaque -- use verbatim.</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC.</summary>
    public string CreatedAtUtc { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC; per-document staleness guard.</summary>
    public string UpdatedAt { get; set; } = string.Empty;

    /// <summary>Uploaded documents only: the chosen category, or the free-text "Other" label.</summary>
    public string? DocumentType { get; set; }
}
