using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;

/// <summary>
/// B12 -- what counts as a known default admin password, and how a replacement is generated.
///
/// <para>Two passwords are seeded into this product by default: ABP's own
/// <c>1q2w3E*</c> (aliased as <see cref="CaseEvaluationConsts.AdminPasswordDefaultValue"/>) and this
/// repository's <see cref="InternalUsersDataSeedContributor.DefaultPassword"/>. Both are published --
/// ABP's in the framework source, this repository's in a public repository -- so outside Development
/// an account still holding either is not protected by it.</para>
///
/// <para>The literals are referenced from where they are already declared rather than copied, so a
/// change to a seeder cannot leave this list silently describing a password nothing uses.</para>
/// </summary>
public static class AdminPasswordPolicy
{
    /// <summary>
    /// Length of a generated password. Long enough that the character-class rules below are a
    /// rounding error against its entropy, and short enough to be transcribed by an operator once.
    /// </summary>
    public const int GeneratedLength = 32;

    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";

    /// <summary>
    /// Deliberately excludes the quote, backslash, backtick and dollar characters. A generated
    /// password is read back by an operator and may be pasted into a shell, a compose file or a
    /// connection string, and those four are the ones that change meaning when it is.
    /// </summary>
    private const string Symbols = "!@#%^&*()-_=+";

    private static readonly string[] CharacterClasses = { Uppercase, Lowercase, Digits, Symbols };

    /// <summary>
    /// Every password this product is known to seed. Outside Development an admin holding one of
    /// these is rotated by the migrator and refused at sign-in.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownDefaults = new[]
    {
        CaseEvaluationConsts.AdminPasswordDefaultValue,
        InternalUsersDataSeedContributor.DefaultPassword,
    };

    /// <summary>
    /// True when <paramref name="password"/> is one this product seeds.
    ///
    /// <para>Ordinal and case-SENSITIVE: these are exact literals, not names. A case-insensitive
    /// compare would refuse passwords that merely resemble a default and are not one.</para>
    /// </summary>
    public static bool IsKnownDefault(string? password)
    {
        return password != null && KnownDefaults.Contains(password, StringComparer.Ordinal);
    }

    /// <summary>
    /// A new password of <see cref="GeneratedLength"/> characters carrying at least one character
    /// from each class, which is what ABP's default password rules require.
    ///
    /// <para>Built by taking one character from each class first and filling the remainder from the
    /// union, then shuffling. Without the shuffle the first four positions would always be
    /// upper, lower, digit, symbol in that order, which is a published prefix pattern.</para>
    ///
    /// <para><see cref="RandomNumberGenerator.GetInt32(int)"/> throughout: it is uniform over the
    /// range and rejects modulo bias, which a <c>% length</c> over random bytes does not.</para>
    /// </summary>
    public static string Generate()
    {
        var all = string.Concat(CharacterClasses);
        var chars = new char[GeneratedLength];

        for (var i = 0; i < CharacterClasses.Length; i++)
        {
            var set = CharacterClasses[i];
            chars[i] = set[RandomNumberGenerator.GetInt32(set.Length)];
        }

        for (var i = CharacterClasses.Length; i < GeneratedLength; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Fisher-Yates, drawing each index from the crypto source for the same reason as above.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
