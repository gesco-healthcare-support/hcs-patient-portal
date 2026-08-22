using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.CustomFields;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// One booking, whole. Everything the wizard used to send across a patient call, an appointment
/// POST and seven further child POSTs arrives here in a single request so the server can commit it
/// in one transaction.
///
/// <para>Why this is not simply <see cref="AppointmentCreateDto"/> plus collections: three of that
/// type's fields are server-derived on this path and are therefore absent here.
/// <c>RequestConfirmationNumber</c> is allocated by the server, <c>PatientId</c> is the result of
/// resolving <see cref="Patient"/>, and <c>IsPatientAlreadyExist</c> is read off the dedup outcome.
/// A client that supplied them could disagree with what actually happened.</para>
/// </summary>
public class AppointmentSubmitDto
{
    /// <summary>
    /// An already-resolved patient. Supply this OR <see cref="Patient"/>. When both are present
    /// this wins and no dedup runs, which is the path an internal booker takes after picking an
    /// existing patient from the lookup.
    /// </summary>
    public Guid? PatientId { get; set; }

    /// <summary>
    /// The patient to resolve-or-create inside the submit transaction. Runs the same email
    /// fast-path and 3-of-6 deduplication as the standalone booking call, so a repeat booker is
    /// matched to their existing record rather than duplicated.
    /// </summary>
    public CreatePatientForAppointmentBookingInput? Patient { get; set; }

    /// <summary>
    /// Edits the booker made to an EXISTING patient's profile, applied inside the submit
    /// transaction. Optional: omit it when nothing on the patient changed.
    ///
    /// <para>This is separate from <see cref="Patient"/> because the two are different operations.
    /// <see cref="Patient"/> resolves-or-creates and runs deduplication; this updates a record that
    /// already exists and carries a <c>ConcurrencyStamp</c> so a stale edit is rejected rather than
    /// silently clobbering a concurrent one. Folding it in is what makes "atomic booking" true: the
    /// wizard used to PUT the profile before the appointment POST, so a booking that failed
    /// afterwards left the edit applied.</para>
    ///
    /// <para><c>IdentityUserId</c> and <c>TenantId</c> on this object are IGNORED -- both update
    /// paths read them off the stored patient, and honouring a caller's values would let a booking
    /// reassign a patient's login or office.</para>
    ///
    /// <para>Requires <see cref="PatientId"/> or <see cref="Patient"/>; on its own there is no
    /// record to update.</para>
    /// </summary>
    public PatientUpdateDto? PatientUpdate { get; set; }

    [StringLength(AppointmentConsts.PanelNumberMaxLength)]
    public string? PanelNumber { get; set; }

    public DateTime AppointmentDate { get; set; }

    public DateTime? DueDate { get; set; }

    public AppointmentStatusType AppointmentStatus { get; set; } = Enum.GetValues<AppointmentStatusType>()[0];

    /// <summary>
    /// Nullable: booking persists the appointment with no patient login (the record-only model).
    /// </summary>
    public Guid? IdentityUserId { get; set; }

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

    [StringLength(AppointmentConsts.RefferedByMaxLength)]
    public string? RefferedBy { get; set; }

    public List<CustomFieldValueInputDto> CustomFieldValues { get; set; } = new();

    // ---------------------------------------------------------------- child groups
    // Each child type below carries an AppointmentId of its own. On this path that value is
    // IGNORED and overwritten: the appointment does not exist when the request is built. The
    // existing types are reused rather than redeclared so their validation attributes and field
    // names stay in one place.

    public AppointmentEmployerDetailCreateDto? EmployerDetail { get; set; }

    public ApplicantAttorneyDetailsDto? ApplicantAttorney { get; set; }

    public DefenseAttorneyDetailsDto? DefenseAttorney { get; set; }

    public AppointmentPrimaryInsuranceCreateDto? PrimaryInsurance { get; set; }

    public AppointmentClaimExaminerCreateDto? ClaimExaminer { get; set; }

    public List<AppointmentInjurySubmitDto> InjuryDetails { get; set; } = new();

    public List<AppointmentAccessorCreateDto> Accessors { get; set; } = new();
}

/// <summary>
/// One injury and the body parts belonging to it.
///
/// <para>Body parts are nested rather than sitting flat on the submit request because
/// <see cref="AppointmentBodyPartCreateDto.AppointmentInjuryDetailId"/> points at the injury, not
/// at the appointment -- so the server must write the injury, take its id, and only then write its
/// body parts. A flat list could not express which injury a body part belonged to.</para>
/// </summary>
public class AppointmentInjurySubmitDto
{
    public AppointmentInjuryDetailCreateDto Injury { get; set; } = null!;

    /// <summary>
    /// Their <c>AppointmentInjuryDetailId</c> is ignored and overwritten with the id of the injury
    /// created from <see cref="Injury"/>.
    /// </summary>
    public List<AppointmentBodyPartCreateDto> BodyParts { get; set; } = new();
}
