using System.ComponentModel.DataAnnotations;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Validation;

/// <summary>
/// Tests for <see cref="PhoneNumberAttribute"/>. All numbers use the 555 fictional exchange.
///
/// <para>The lenient-on-punctuation behaviour is deliberate and tested rather than incidental: a
/// browser holding a cached copy of the previous SPA bundle still posts formatted values, and a
/// strict digits-only rule would reject real submissions during a deploy instead of at the point
/// the data is actually wrong.</para>
/// </summary>
public class PhoneNumberAttributeTests
{
    private static ValidationResult? Validate(string? value)
    {
        var attribute = new PhoneNumberAttribute();
        var context = new ValidationContext(new object()) { MemberName = "PhoneNumber" };
        return attribute.GetValidationResult(value, context);
    }

    [Theory]
    [InlineData("2135550134")]
    [InlineData("(213)-555-0134")]
    [InlineData("(213) 555-0134")]
    [InlineData("213.555.0134")]
    [InlineData("213 555 0134")]
    public void Accepts_TenDigitsInAnyShape(string value)
    {
        Validate(value).ShouldBe(ValidationResult.Success);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Accepts_AbsentValue(string? value)
    {
        // These fields are optional across the app; requiring one is a product decision, so it is
        // [Required]'s job rather than this attribute's.
        Validate(value).ShouldBe(ValidationResult.Success);
    }

    [Theory]
    [InlineData("5550134")]
    [InlineData("213555013")]
    [InlineData("12135550134")]
    [InlineData("2135550134 ext 22")]
    [InlineData("call the office")]
    [InlineData("()-")]
    public void Rejects_AnythingThatIsNotTenDigits(string value)
    {
        var result = Validate(value);

        result.ShouldNotBe(ValidationResult.Success);
        result!.ErrorMessage.ShouldBe("Enter a 10-digit phone number.");
        result.MemberNames.ShouldContain("PhoneNumber");
    }

    [Fact]
    public void Digits_StripsFormattingWithoutTruncating()
    {
        PhoneNumberAttribute.Digits("(213)-555-0134").ShouldBe("2135550134");
        PhoneNumberAttribute.Digits(null).ShouldBe(string.Empty);

        // Deliberately does NOT cap at ten, unlike the Angular field. The field is masking input,
        // so dropping an eleventh keystroke is the right behaviour there; the server is judging a
        // submitted value, and silently truncating one would store a number nobody typed.
        PhoneNumberAttribute.Digits("12135550134").ShouldBe("12135550134");
    }

    [Fact]
    public void DigitCount_IsTen()
    {
        // Pinned because the Angular field, this attribute and the display format all assume it,
        // and they live in three different places.
        PhoneNumberAttribute.DigitCount.ShouldBe(10);
    }
}
