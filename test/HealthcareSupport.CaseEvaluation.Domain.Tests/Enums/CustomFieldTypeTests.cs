using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Tests.Enums;

/// <summary>
/// Pin <see cref="CustomFieldType"/> to OLD's verbatim int values
/// (file: P:\PatientPortalOld\PatientAppointment.DbEntities\Enums\CustomFieldType.cs).
/// G3 (Phase 6b, 2026-05-04). Without these tests an accidental
/// re-numbering would silently break parity for any IT-Admin-defined
/// custom field already persisted with a specific FieldType int.
/// </summary>
public class CustomFieldTypeTests
{
    [Fact]
    public void Alphanumeric_is_12()
        => ((int)CustomFieldType.Alphanumeric).ShouldBe(12);

    [Fact]
    public void Numeric_is_13()
        => ((int)CustomFieldType.Numeric).ShouldBe(13);

    [Fact]
    public void Picklist_is_14()
        => ((int)CustomFieldType.Picklist).ShouldBe(14);

    [Fact]
    public void Tickbox_is_15()
        => ((int)CustomFieldType.Tickbox).ShouldBe(15);

    [Fact]
    public void Date_is_16()
        => ((int)CustomFieldType.Date).ShouldBe(16);

    [Fact]
    public void Radio_is_17()
        => ((int)CustomFieldType.Radio).ShouldBe(17);

    [Fact]
    public void Time_is_18()
        => ((int)CustomFieldType.Time).ShouldBe(18);

    [Fact]
    public void Has_exactly_seven_values()
        => System.Enum.GetValues<CustomFieldType>().Length.ShouldBe(7);
}
