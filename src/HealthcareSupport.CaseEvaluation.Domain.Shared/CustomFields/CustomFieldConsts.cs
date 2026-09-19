namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// Field-length constraints for the custom-fields catalog. Values mirror
/// the column widths in OLD's <c>spm.CustomFields</c> /
/// <c>spm.CustomFieldsValues</c> tables verbatim.
/// </summary>
public static class CustomFieldConsts
{
    /// <summary>OLD <c>CustomField.FieldLabel</c> nvarchar(200).</summary>
    public const int FieldLabelMaxLength = 200;

    /// <summary>OLD <c>CustomField.MultipleValues</c> nvarchar(200) -- comma-separated dropdown options.</summary>
    public const int MultipleValuesMaxLength = 200;

    /// <summary>OLD <c>CustomField.DefaultValue</c> nvarchar(200).</summary>
    public const int DefaultValueMaxLength = 200;

    /// <summary>OLD <c>CustomFieldsValues.CustomFieldValue</c> varchar(MAX); cap chosen for input safety.</summary>
    public const int ValueMaxLength = 4000;

    /// <summary>
    /// Maximum active rows per <c>AppointmentTypeId</c>. Mirrors spec line
    /// 543 ("Up to 10 fields per appointment type"). NEW enforces
    /// <c>&gt;= MaxActiveCountPerAppointmentType</c>; OLD's
    /// <c>CustomFieldDomain.cs:40</c> uses <c>== 10</c> (an OLD bug --
    /// boundary failure if the count ever exceeds 10 by other paths).
    /// </summary>
    public const int MaxActiveCountPerAppointmentType = 10;
}
