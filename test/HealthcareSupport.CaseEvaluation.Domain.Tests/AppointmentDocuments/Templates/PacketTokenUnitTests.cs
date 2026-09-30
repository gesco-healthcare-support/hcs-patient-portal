using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// Unit tests for the shared <see cref="PacketTokenMap"/> and the resolver's pure interpreter-token
/// decision. Pure logic -- no IO, no repositories. (Token <em>substitution</em> itself now happens
/// in the packet-renderer sidecar; the .NET side only builds the map via <see cref="PacketTokenMap.Build"/>
/// and ships it.) These guard that the map exposes the right keys/values and that the Yes/No
/// interpreter derivation is correct.
/// </summary>
public class PacketTokenUnitTests
{
    [Fact]
    public void TokenMap_MapsKnownTokens_FromContext()
    {
        var map = PacketTokenMap.Build(new PacketTokenContext
        {
            PatientFirstName = "JANE",
            PatientInterpreterRequired = "Yes",
            PatientInterpreterLanguage = "SPANISH",
        });

        map["##Patients.FirstName##"].ShouldBe("JANE");
        map["##Patients.InterpreterRequired##"].ShouldBe("Yes");
        map["##Patients.InterpreterLanguage##"].ShouldBe("SPANISH");
    }

    [Fact]
    public void TokenMap_OmitsSignaturePlaceholder()
    {
        // The signature token is handled by the DOCX image stamping, never substituted from the map.
        var map = PacketTokenMap.Build(new PacketTokenContext());

        map.ShouldNotContainKey(PacketTokenMap.SignaturePlaceholder);
    }

    [Fact]
    public void TokenRegex_Matches_GroupDotField_Only()
    {
        PacketTokenMap.TokenRegex.IsMatch("##Patients.FirstName##").ShouldBeTrue();
        PacketTokenMap.TokenRegex.IsMatch("## not a token ##").ShouldBeFalse();
    }

    [Theory]
    // Interpreter details (vendor) provided -> "Yes" + the (uppercased) language.
    [InlineData("Spanish", "ABC Interpreters", "Yes", "SPANISH")]
    // "Other" free-text language flows through as the (uppercased) language when a vendor is set.
    [InlineData("Farsi", "ACME Language Svc", "Yes", "FARSI")]
    // Vendor set but no language resolved -> "Yes" with a blank language.
    [InlineData(null, "ABC Interpreters", "Yes", "")]
    // No vendor -> "No" regardless of language: language alone never implies an interpreter,
    // mirroring the form's !!interpreterVendorName signal (the behavioral fix vs the first cut).
    [InlineData("Spanish", null, "No", "")]
    [InlineData(null, null, "No", "")]
    // Whitespace-only vendor is treated as "not provided".
    [InlineData("Spanish", "   ", "No", "")]
    public void DeriveInterpreter_DerivesRequiredAndLanguage(
        string? languageName, string? vendorName, string expectedRequired, string expectedLanguage)
    {
        var (required, language) = PacketTokenResolver.DeriveInterpreter(languageName, vendorName);

        required.ShouldBe(expectedRequired);
        language.ShouldBe(expectedLanguage);
    }
}
