using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-05-15 -- result of a multi-axis batched slot create. The AppService
/// re-runs the preview projection server-side, persists every non-conflicted
/// slot transactionally, and reports counts so the SPA can render
/// "N inserted, K skipped". Conflict rows are returned in full so the UI can
/// highlight which dates / times were skipped.
/// </summary>
public class DoctorAvailabilityCreateRangeResultDto
{
    public int InsertedCount { get; set; }
    public int SkippedConflictCount { get; set; }
    public List<DoctorAvailabilitySlotPreviewDto> ConflictedSlots { get; set; } = new();
}
