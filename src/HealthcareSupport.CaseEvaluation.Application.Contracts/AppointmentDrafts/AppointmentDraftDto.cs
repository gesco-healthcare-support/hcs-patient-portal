using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts;

/// <summary>
/// #15 (2026-06-22): the signed-in user's own in-progress booking draft, returned
/// by the self-scoped <see cref="IAppointmentDraftAppService"/>. Null (not this
/// dto) is returned when the caller has no draft. <see cref="PayloadJson"/> is the
/// opaque PHI form snapshot -- never logged.
/// </summary>
public class AppointmentDraftDto
{
    public string PayloadJson { get; set; } = null!;

    public int CurrentStep { get; set; }

    public string? Label { get; set; }

    public DateTime LastSavedTime { get; set; }
}
