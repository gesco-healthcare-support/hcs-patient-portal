using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

/// <summary>
/// 2026-11-06 -- the throwaway password an Authorized User's new account is created with must satisfy
/// Identity's default policy every time, or the invite fails. It used to take its only digit from twelve
/// random hex characters, which are all a-f about once in 130,000 ("Passwords must have at least one
/// digit", seen once as a CI flake). The digit is now in the fixed prefix.
/// </summary>
public class AccessorTempPasswordTests
{
    [Fact]
    public void The_fixed_prefix_alone_has_a_digit_an_upper_a_lower_and_a_symbol()
    {
        // Checked on the constant, not on a sample, so the answer never depends on a random draw.
        const string prefix = AppointmentAccessorManager.TempPasswordPrefix;

        prefix.Any(char.IsDigit).ShouldBeTrue();
        prefix.Any(char.IsUpper).ShouldBeTrue();
        prefix.Any(char.IsLower).ShouldBeTrue();
        prefix.Any(c => !char.IsLetterOrDigit(c)).ShouldBeTrue();
    }

    [Fact]
    public void Every_generated_password_starts_with_the_prefix_and_is_long_enough()
    {
        var samples = Enumerable.Range(0, 1000)
            .Select(_ => AppointmentAccessorManager.GenerateTempPassword())
            .ToList();

        samples.ShouldAllBe(p => p.StartsWith(AppointmentAccessorManager.TempPasswordPrefix));
        samples.ShouldAllBe(p => p.Length >= 8);
    }

    [Fact]
    public void Two_passwords_differ()
    {
        AppointmentAccessorManager.GenerateTempPassword()
            .ShouldNotBe(AppointmentAccessorManager.GenerateTempPassword());
    }
}
