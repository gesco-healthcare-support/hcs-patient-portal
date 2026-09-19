using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// PR2 (2026-07-10) -- read-only projection of a single custom field for the
/// appointment detail views. One row per ACTIVE <see cref="CustomField"/>
/// defined on the appointment's type; <see cref="Value"/> is the booker's
/// saved answer, or null when the field was left unanswered, so the detail
/// views can show every field "filled or empty" to match the booking wizard's
/// review step. Returned by
/// <c>IAppointmentsAppService.GetAppointmentCustomFieldValuesAsync</c>; there is
/// no write path (values are written via the appointment create/update inputs).
/// </summary>
public class CustomFieldValueDisplayDto
{
    public Guid CustomFieldId { get; set; }

    public string FieldLabel { get; set; } = null!;

    public CustomFieldType FieldType { get; set; }

    /// <summary>The booker's saved answer, or null when unanswered.</summary>
    public string? Value { get; set; }

    public int DisplayOrder { get; set; }
}
