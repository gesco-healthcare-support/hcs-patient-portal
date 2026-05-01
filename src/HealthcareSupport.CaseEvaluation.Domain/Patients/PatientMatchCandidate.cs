using System;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// Result row returned by <see cref="IPatientRepository.FindBestMatchAsync"/> -- the
/// minimum information needed by <see cref="PatientManager.FindOrCreateAsync"/> to
/// decide whether to return an existing Patient or create a new one. Carries no PHI;
/// caller rehydrates the full Patient via the standard repository read path.
/// </summary>
public record PatientMatchCandidate(Guid Id, int MatchCount, DateTime CreationTime);
