using HealthcareSupport.CaseEvaluation.Practices;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Practices;

/// <summary>
/// Pure unit tests for <see cref="PracticeNaming"/> (no DB / DI).
///
/// Pins the default practice display name: "Dr. {FirstName} {LastName}", the
/// fallback used when the New Practice form leaves the display name blank.
/// </summary>
public class PracticeNamingTests
{
    [Fact]
    public void DefaultDisplayName_prefixes_the_doctor_name_with_the_title()
        => PracticeNaming.DefaultDisplayName("John", "Smith").ShouldBe("Dr. John Smith");

    [Fact]
    public void DefaultDisplayName_trims_each_name()
        => PracticeNaming.DefaultDisplayName("  John ", " Smith ").ShouldBe("Dr. John Smith");

    [Fact]
    public void DefaultDisplayName_drops_a_blank_last_name()
        => PracticeNaming.DefaultDisplayName("John", "  ").ShouldBe("Dr. John");

    [Fact]
    public void DefaultDisplayName_drops_a_blank_first_name()
        => PracticeNaming.DefaultDisplayName("", "Smith").ShouldBe("Dr. Smith");

    [Fact]
    public void DefaultDisplayName_returns_just_the_title_when_both_blank()
        => PracticeNaming.DefaultDisplayName("  ", null).ShouldBe("Dr.");

    [Fact]
    public void DefaultDisplayName_handles_nulls()
        => PracticeNaming.DefaultDisplayName(null, null).ShouldBe("Dr.");
}
