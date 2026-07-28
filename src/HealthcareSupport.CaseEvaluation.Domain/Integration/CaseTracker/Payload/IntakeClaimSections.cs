using System;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// One injury on the appointment. Sent as an ARRAY because the portal genuinely supports several -- a
/// booker can record a specific injury and a cumulative-trauma injury on one evaluation -- and there is
/// no primary flag, so flattening would force the PORTAL to choose a primary claim with less information
/// than the receiver's staff have.
/// </summary>
public class IntakeInjuryEntry
{
    /// <summary>The injury row's own stable id. Useful for lining entries up across pushes; NOT a claim key.</summary>
    public Guid Id { get; set; }

    /// <summary>Clinic-local date, <c>yyyy-MM-dd</c>. Mandatory at booking.</summary>
    public string DateOfInjury { get; set; } = string.Empty;

    /// <summary>
    /// End of the exposure period on a cumulative injury, <c>yyyy-MM-dd</c>; null on a specific injury.
    /// </summary>
    public string? ToDateOfInjury { get; set; }

    /// <summary>
    /// True means <see cref="DateOfInjury"/>..<see cref="ToDateOfInjury"/> is an exposure PERIOD rather
    /// than an incident date.
    /// </summary>
    public bool IsCumulativeInjury { get; set; }

    /// <summary>Exactly as typed by the booker. Mandatory, but free text -- see <see cref="ClaimNumberNormalized"/>.</summary>
    public string ClaimNumber { get; set; } = string.Empty;

    /// <summary>
    /// Comparable form of <see cref="ClaimNumber"/> for GROUPING only, never a key. Exists because the
    /// portal validates claim numbers for length alone, so one claim can arrive as <c>WC-4417</c> at one
    /// booking and <c>WC4417</c> at the next; without this the receiver cannot match them. Null when the
    /// raw value contains nothing alphanumeric.
    /// </summary>
    public string? ClaimNumberNormalized { get; set; }

    /// <summary>WCAB ADJ number as typed. Mandatory at booking, and free text like the claim number.</summary>
    public string WcabAdj { get; set; } = string.Empty;

    /// <summary>Comparable form of <see cref="WcabAdj"/>. Same rules as <see cref="ClaimNumberNormalized"/>.</summary>
    public string? WcabAdjNormalized { get; set; }

    /// <summary>Free text as typed by the booker. Mandatory at booking.</summary>
    public string BodyPartsSummary { get; set; } = string.Empty;

    /// <summary>The WCAB venue for this injury; null when none was recorded. Genuinely per-injury.</summary>
    public IntakeWcabOfficeSection? WcabOffice { get; set; }
}

/// <summary>WCAB venue. Name and abbreviation rather than the id, which means nothing to the receiver.</summary>
public class IntakeWcabOfficeSection
{
    public string Name { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;
}

/// <summary>
/// An attorney on the appointment. The full address block is included because the receiver may need to
/// serve documents on parties, and their DTOs ignore fields they do not consume -- so sending it now
/// costs nothing and avoids a second contract revision and deploy later.
///
/// <para>Read from the appointment's own denormalised columns, not from the master attorney list, so it
/// reflects what was recorded for THIS appointment.</para>
/// </summary>
public class IntakeAttorneySection
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? FirmName { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? FaxNumber { get; set; }

    public string? WebAddress { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    /// <summary>Resolved state NAME, not its id. Null when unrecorded or unresolvable.</summary>
    public string? State { get; set; }

    public string? ZipCode { get; set; }
}

/// <summary>
/// An insurance carrier on the appointment.
///
/// <para>IMPORTANT: attached to the APPOINTMENT, not to a specific injury. The booking UI collects these
/// through the injury modal, so a booker experiences them as belonging to an injury, but no injury
/// foreign key is stored. On a two-injury appointment the portal therefore does NOT record which carrier
/// covers which claim, and the receiver must not infer one.</para>
/// </summary>
public class IntakeInsuranceSection
{
    public string? Name { get; set; }

    public string? Suite { get; set; }

    public string? PhoneNumber { get; set; }

    public string? FaxNumber { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? ZipCode { get; set; }
}

/// <summary>
/// A claim examiner on the appointment. Same appointment-level caveat as
/// <see cref="IntakeInsuranceSection"/>: no link to a specific injury is stored.
/// </summary>
public class IntakeClaimExaminerSection
{
    public string? Name { get; set; }

    public string? Suite { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? FaxNumber { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? ZipCode { get; set; }
}
