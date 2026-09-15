using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// B1 (2026-05-05) -- per-appointment custom-field value submitted from
/// the booking form. One row per <see cref="CustomField"/> the booker
/// answered. Mirrors OLD's <c>spm.CustomFieldsValues</c> insert from
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>
/// (custom-field write happens alongside the appointment insert).
///
/// The DTO carries no <c>AppointmentId</c> -- the AppService stamps that
/// from the parent appointment after the row is created. Empty-string
/// values are dropped (matches OLD's "no answer" semantics; OLD also
/// elides empty rows).
/// </summary>
public class CustomFieldValueInputDto
{
    public Guid CustomFieldId { get; set; }

    [StringLength(CustomFieldConsts.ValueMaxLength)]
    public string Value { get; set; } = string.Empty;
}
