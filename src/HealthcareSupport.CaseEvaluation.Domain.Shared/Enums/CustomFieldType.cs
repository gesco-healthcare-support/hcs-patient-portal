namespace HealthcareSupport.CaseEvaluation.Enums;

/// <summary>
/// Data-type discriminator for an IT-Admin-defined custom intake field.
/// Mirrors OLD's <c>Models.Enums.CustomFieldTypeEnum</c> verbatim
/// (file: <c>P:\PatientPortalOld\PatientAppointment.DbEntities\Enums\CustomFieldType.cs</c>),
/// preserving OLD's int values 12-18 exactly.
///
/// <para>PARITY-FLAG (PF-002): OLD declares all 7 values but its Angular
/// templates only render 3 (Alphanumeric, Numeric, Date). Picklist,
/// Tickbox, Radio, and Time are latent in OLD's schema + enum but never
/// wired in the booking-form UI. NEW carries all 7 enum values to keep
/// schema parity; B1 (booking form) renders all 7 input types =
/// parity-plus. See docs/parity/_parity-flags.md.</para>
/// </summary>
public enum CustomFieldType
{
    Alphanumeric = 12,
    Numeric = 13,
    Picklist = 14,
    Tickbox = 15,
    Date = 16,
    Radio = 17,
    Time = 18,
}
