using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-06-11 -- pure tests for the patient-lookup PII guard
/// (<see cref="AppointmentsAppService.IsLookupFilterTooShort"/>). The lookup
/// must return no rows until the caller types at least
/// <see cref="AppointmentsAppService.PatientLookupMinFilterLength"/>
/// non-whitespace characters, so AA / DA / CE (and internal staff) cannot pull
/// a default list of every patient's email in the tenant. The CE scoping and
/// the actual query are integration-level and verified by click-test.
/// </summary>
public class PatientLookupFilterUnitTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData(" a ")] // trims to 1 char
    public void IsLookupFilterTooShort_BelowTwoChars_ReturnsTrue(string? filter)
    {
        AppointmentsAppService.IsLookupFilterTooShort(filter).ShouldBeTrue();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData(" ab ")] // trims to 2 chars
    [InlineData("jo@")]
    [InlineData("john@example.com")]
    public void IsLookupFilterTooShort_TwoOrMoreChars_ReturnsFalse(string filter)
    {
        AppointmentsAppService.IsLookupFilterTooShort(filter).ShouldBeFalse();
    }
}
