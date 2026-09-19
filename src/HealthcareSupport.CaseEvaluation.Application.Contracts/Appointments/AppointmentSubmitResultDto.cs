using System;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// What a submit actually wrote, counted per child group.
///
/// <para>Per-group counts rather than one success flag, and this is not stylistic. Bug F18 was a
/// cascade that silently dropped 2 of 8 child groups while reporting success; a single boolean
/// cannot fail that test. A caller -- and more importantly a test -- can assert each group
/// individually against what it sent.</para>
/// </summary>
public class AppointmentSubmitResultDto
{
    public Guid AppointmentId { get; set; }

    public string RequestConfirmationNumber { get; set; } = null!;

    /// <summary>
    /// The patient the appointment was attached to, whether newly created or matched to an
    /// existing record by the deduplication rules.
    /// </summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// True when the patient was matched to an existing record rather than created. Mirrors what
    /// the standalone booking call reports as <c>IsExisting</c>.
    /// </summary>
    public bool PatientAlreadyExisted { get; set; }

    public int EmployerDetails { get; set; }
    public int ApplicantAttorneys { get; set; }
    public int DefenseAttorneys { get; set; }
    public int PrimaryInsurances { get; set; }
    public int ClaimExaminers { get; set; }
    public int InjuryDetails { get; set; }
    public int BodyParts { get; set; }
    public int Accessors { get; set; }
    public int CustomFieldValues { get; set; }

    /// <summary>
    /// For logging only. NEVER assert on this: a total can be right while two groups are wrong in
    /// opposite directions, which is exactly how F18 stayed hidden.
    /// </summary>
    public int Total =>
        EmployerDetails + ApplicantAttorneys + DefenseAttorneys + PrimaryInsurances
        + ClaimExaminers + InjuryDetails + BodyParts + Accessors + CustomFieldValues;
}
