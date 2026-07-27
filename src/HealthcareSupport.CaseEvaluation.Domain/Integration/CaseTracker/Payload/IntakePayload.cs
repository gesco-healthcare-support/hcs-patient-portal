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

    /// <summary>ISO-8601 UTC. Null only if pushed before approval, which the trigger prevents.</summary>
    public string? ApprovedAtUtc { get; set; }

    /// <summary>ISO-8601 UTC.</summary>
    public string SubmittedAtUtc { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC, monotonic per appointment. Drives the receiver's staleness guard.</summary>
    public string UpdatedAt { get; set; } = string.Empty;

    /// <summary><c>EVAL</c> or <c>RE_EVAL</c>.</summary>
    public string EvaluationKind { get; set; } = string.Empty;

    /// <summary>The original appointment on a re-evaluation; null on a first evaluation.</summary>
    public Guid? PreviousAppointmentId { get; set; }

    /// <summary>The original's confirmation number. Display only -- a re-eval gets its own.</summary>
    public string? PreviousConfirmationNumber { get; set; }

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
}

/// <summary>The office's single doctor (tenant == doctor). FirstName can legitimately be empty.</summary>
public class IntakeDoctorSection
{
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
