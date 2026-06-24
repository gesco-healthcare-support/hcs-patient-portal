using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// #2 (2026-06-19) -- the booked/reserved patient names on one availability slot,
/// for the internal week-view chips. Read-only projection; returned only for slots
/// that have at least one non-terminal appointment. Patient names are shown to
/// internal staff only (the week view is gated on DoctorAvailabilities.Default).
/// </summary>
public class SlotPatientNamesDto
{
    public Guid SlotId { get; set; }

    public List<string> Names { get; set; } = new();
}
