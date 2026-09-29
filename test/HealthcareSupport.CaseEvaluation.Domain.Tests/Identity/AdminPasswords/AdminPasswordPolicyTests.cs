using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// B12 task 1 -- the policy that decides which passwords this product is known to seed, and the
/// generator that replaces them.
/// </summary>
public sealed class AdminPasswordPolicyTests
{
    /// <summary>
    /// The list must be the seeders' OWN constants, not copies of their current text. A copy would
    /// keep passing after a seeder changed its password, while silently describing one nothing
    /// seeds -- so this asserts identity with the declaration sites rather than the literals.
    /// </summary>
    [Fact]
    public void KnownDefaults_AreTheValuesTheSeedersActuallyUse()
    {
        AdminPasswordPolicy.KnownDefaults.ShouldBe(
            new[]
            {
                CaseEvaluationConsts.AdminPasswordDefaultValue,
                InternalUsersDataSeedContributor.DefaultPassword,
            },
            ignoreOrder: true);
    }

    /// <summary>
    /// The external seeder is a third declaration site that happens to hold the same text as the
    /// internal one. It is covered because they are equal, not because it is listed -- if they ever
    /// diverge this fails, which is the signal to add it.
    /// </summary>
    [Fact]
    public void TheExternalSeedersDefault_IsStillCoveredByTheList()
    {
        AdminPasswordPolicy.IsKnownDefault(ExternalUsersDataSeedContributor.DefaultPassword)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsKnownDefault_RecognisesEveryListedDefault()
    {
        foreach (var known in AdminPasswordPolicy.KnownDefaults)
        {
            AdminPasswordPolicy.IsKnownDefault(known).ShouldBeTrue(known);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a-real-password-nobody-seeds")]
    public void IsKnownDefault_IsFalseForAnythingElse(string? password)
    {
        AdminPasswordPolicy.IsKnownDefault(password).ShouldBeFalse();
    }

    /// <summary>
    /// Case-SENSITIVE on purpose. These are exact literals, so a case-insensitive compare would
    /// refuse a password that merely resembles a default, locking out an account that was never
    /// exposed.
    /// </summary>
    [Fact]
    public void IsKnownDefault_DoesNotMatchADifferentCasing()
    {
        var upper = AdminPasswordPolicy.KnownDefaults[0].ToUpperInvariant();
        upper.ShouldNotBe(AdminPasswordPolicy.KnownDefaults[0], "the fixture needs a real casing change");

        AdminPasswordPolicy.IsKnownDefault(upper).ShouldBeFalse();
    }

    [Fact]
    public void Generate_ProducesTheConfiguredLength()
    {
        AdminPasswordPolicy.Generate().Length.ShouldBe(AdminPasswordPolicy.GeneratedLength);
    }

    /// <summary>
    /// ABP's default password rules demand all four classes. Asserted over many samples rather than
    /// one, because a generator that merely usually satisfies them would seed an account that
    /// cannot be created, intermittently.
    /// </summary>
    [Fact]
    public void Generate_AlwaysCarriesEveryCharacterClass()
    {
        for (var i = 0; i < 1000; i++)
        {
            var password = AdminPasswordPolicy.Generate();

            password.Any(char.IsUpper).ShouldBeTrue(password);
            password.Any(char.IsLower).ShouldBeTrue(password);
            password.Any(char.IsDigit).ShouldBeTrue(password);
            password.Any(c => !char.IsLetterOrDigit(c)).ShouldBeTrue(password);
        }
    }

    /// <summary>
    /// Distinctness across 1,000 samples. This is the assertion that fails if the generator is ever
    /// reduced to a seeded PRNG, which is the way a generator like this usually breaks.
    /// </summary>
    [Fact]
    public void Generate_DoesNotRepeatItself()
    {
        var samples = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 1000; i++)
        {
            samples.Add(AdminPasswordPolicy.Generate());
        }

        samples.Count.ShouldBe(1000);
    }

    /// <summary>
    /// The guaranteed characters are placed in the first four positions before shuffling, so an
    /// unshuffled generator would put an uppercase first, a lowercase second and so on EVERY time.
    /// Over 200 samples that pattern cannot survive by chance.
    /// </summary>
    [Fact]
    public void Generate_DoesNotLeaveTheGuaranteedCharactersInAFixedOrder()
    {
        var firstIsAlwaysUpper = Enumerable.Range(0, 200)
            .Select(_ => AdminPasswordPolicy.Generate())
            .All(p => char.IsUpper(p[0]));

        firstIsAlwaysUpper.ShouldBeFalse();
    }

    /// <summary>
    /// A generated password is transcribed by an operator and may reach a shell, a compose file or
    /// a connection string. The four characters that change meaning there are excluded at source.
    /// </summary>
    [Fact]
    public void Generate_AvoidsTheCharactersThatChangeMeaningWhenPasted()
    {
        // Double quote, apostrophe, backslash, backtick and dollar, written as code points
        // so this line carries no escape sequence of its own to get wrong.
        var forbidden = new[] { (char)34, (char)39, (char)92, (char)96, (char)36 };

        for (var i = 0; i < 200; i++)
        {
            var password = AdminPasswordPolicy.Generate();
            password.IndexOfAny(forbidden).ShouldBe(-1, password);
        }
    }
}
