using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-10-14 -- the appointments a user is about to export from the internal appointment list as
/// a CSV. The list builds the file in the browser from rows it already holds, so the server records
/// the export here, BEFORE the file is produced, and the browser produces it only if this succeeds.
/// </summary>
public class RecordAppointmentListExportInput
{
    /// <summary>The largest export one call records; the list never selects more than a page.</summary>
    public const int MaxAppointments = 500;

    [Required]
    [MinLength(1)]
    [MaxLength(MaxAppointments)]
    public List<Guid> AppointmentIds { get; set; } = new();
}
