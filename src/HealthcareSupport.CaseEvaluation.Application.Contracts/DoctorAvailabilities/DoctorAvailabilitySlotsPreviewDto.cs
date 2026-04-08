using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilitySlotsPreviewDto
{
    public string Dates { get; set; } = string.Empty;

    public string Days { get; set; } = string.Empty;

    public int MonthId { get; set; }

    public string? LocationName { get; set; }

    public string Time { get; set; } = string.Empty;

    public string? SameTimeValidation { get; set; }

    public List<DoctorAvailabilitySlotPreviewDto> DoctorAvailabilities { get; set; } = new();
}
