using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Phase E (2026-06-25) -- pins the OfficeBranding aggregate invariants. Pure, no DB.
/// </summary>
public class OfficeBrandingTests
{
    [Fact]
    public void SetDisplayName_TrimsAndStores()
    {
        var branding = new OfficeBranding(Guid.NewGuid(), Guid.NewGuid());
        branding.SetDisplayName("  Falkinstein Medical  ");
        branding.DisplayName.ShouldBe("Falkinstein Medical");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetDisplayName_BlankOrNull_Clears(string? value)
    {
        var branding = new OfficeBranding(Guid.NewGuid(), Guid.NewGuid());
        branding.SetDisplayName("Something");
        branding.SetDisplayName(value);
        branding.DisplayName.ShouldBeNull();
    }

    [Fact]
    public void SetDisplayName_TooLong_Throws()
    {
        var branding = new OfficeBranding(Guid.NewGuid(), Guid.NewGuid());
        var tooLong = new string('x', OfficeBranding.DisplayNameMaxLength + 1);
        Should.Throw<ArgumentException>(() => branding.SetDisplayName(tooLong));
    }

    [Fact]
    public void SetLogo_StoresBlobNameAndContentType()
    {
        var branding = new OfficeBranding(Guid.NewGuid(), Guid.NewGuid());
        branding.SetLogo("abc.png", "image/png");
        branding.LogoBlobName.ShouldBe("abc.png");
        branding.LogoContentType.ShouldBe("image/png");
    }

    [Fact]
    public void ClearLogo_NullsBoth()
    {
        var branding = new OfficeBranding(Guid.NewGuid(), Guid.NewGuid());
        branding.SetLogo("abc.png", "image/png");
        branding.ClearLogo();
        branding.LogoBlobName.ShouldBeNull();
        branding.LogoContentType.ShouldBeNull();
    }
}
