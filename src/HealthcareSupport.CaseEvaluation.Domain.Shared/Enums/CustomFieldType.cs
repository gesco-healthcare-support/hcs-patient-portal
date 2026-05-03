namespace HealthcareSupport.CaseEvaluation.Enums;

/// <summary>
/// Data-type discriminator for an IT-Admin-defined custom intake field.
/// Mirrors OLD's <c>Models.Enums.CustomFieldTypeEnum</c> verbatim
/// (Phase 6, 2026-05-03). Numeric values are arbitrary; strict-parity
/// only requires that the rendered form input shape match OLD.
///
///   <list type="bullet">
///     <item>Date   -> Angular date picker / ISO 8601 string in storage.</item>
///     <item>Text   -> string up to <c>FieldLength</c> characters.</item>
///     <item>Number -> numeric input; stored as a string and parsed by callers.</item>
///   </list>
/// </summary>
public enum CustomFieldType
{
    Date = 1,
    Text = 2,
    Number = 3,
}
