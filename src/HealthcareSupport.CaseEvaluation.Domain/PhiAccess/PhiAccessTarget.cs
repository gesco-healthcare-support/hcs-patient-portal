using System;

namespace HealthcareSupport.CaseEvaluation.PhiAccess;

/// <summary>The records one PHI read touched. Any of the three may be absent.</summary>
public sealed record PhiAccessTarget(Guid? PatientId, Guid? AppointmentId, Guid? DocumentId = null);
