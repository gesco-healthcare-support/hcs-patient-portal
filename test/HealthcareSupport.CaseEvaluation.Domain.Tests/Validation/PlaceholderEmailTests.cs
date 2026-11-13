using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Validation;

/// <summary>
/// The placeholder-email rule (2026-11-13) and its drift guard against the Angular copy.
/// Synthetic addresses only.
/// </summary>
public class PlaceholderEmailTests
{
    [Theory]
    [InlineData("unknown@gmail.com")]
    [InlineData("Unknown@Gmail.com")]
    [InlineData("un.known@gmail.com")]
    [InlineData("un-known_1@gmail.com")]
    [InlineData("unknown123@gmail.com")]
    [InlineData("unknown+claim7@gmail.com")]
    [InlineData("na@na.com")]
    [InlineData("N/A@firm.com")]
    [InlineData("idk@firm.com")]
    [InlineData("none2@firm.com")]
    [InlineData("noemail@firm.com")]
    [InlineData("do.not.reply@firm.com")]
    [InlineData("test@firm.com")]
    [InlineData("x@firm.com")]
    [InlineData("xx@firm.com")]
    [InlineData("aaaa@firm.com")]
    [InlineData("anyone@example.com")]
    [InlineData("anyone@EXAMPLE.NET")]
    [InlineData("anyone@example.org")]
    [InlineData("jane@noemail.com")]
    [InlineData("jane@nomail.com")]
    [InlineData("jane@none.com")]
    [InlineData("jane@unknown.com")]
    [InlineData("jane@fake.com")]
    public void IsPlaceholder_RefusesPlaceholders(string email) =>
        PlaceholderEmail.IsPlaceholder(email).ShouldBeTrue(email);

    [Theory]
    [InlineData("jane.doe@firm.com")]
    [InlineData("jdoe@firm.com")]
    [InlineData("levonk+test@firm.com")]
    [InlineData("unknownsender@firm.com")]
    [InlineData("nathan@firm.com")]
    [InlineData("xavier@firm.com")]
    [InlineData("ab@firm.com")]
    [InlineData("123@firm.com")]
    [InlineData("1111xx@firm.com")]
    [InlineData("jane@example.co")]
    [InlineData("jane@myexample.com")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsPlaceholder_AllowsRealAddressesAndBlank(string? email) =>
        PlaceholderEmail.IsPlaceholder(email).ShouldBeFalse(email);

    [Fact]
    public void Lists_HaveNoDuplicatesAndAreLowercase()
    {
        PlaceholderEmail.Words.Distinct().Count().ShouldBe(PlaceholderEmail.Words.Count);
        PlaceholderEmail.Domains.Distinct().Count().ShouldBe(PlaceholderEmail.Domains.Count);
        PlaceholderEmail.Words.ShouldAllBe(w => w == w.ToLowerInvariant());
        PlaceholderEmail.Domains.ShouldAllBe(d => d == d.ToLowerInvariant());
    }

    /// <summary>
    /// The Angular form validator and this server rule must name the same words and domains, or the
    /// wizard would accept what the server refuses (or the reverse). Fails loudly if the file moves.
    /// </summary>
    [Fact]
    public void AngularLists_MatchTheServerLists()
    {
        var text = File.ReadAllText(FindAngularFileOrThrow());
        QuotedList(text, "PLACEHOLDER_EMAIL_WORDS").ShouldBe(PlaceholderEmail.Words, ignoreOrder: true);
        QuotedList(text, "PLACEHOLDER_EMAIL_DOMAINS").ShouldBe(PlaceholderEmail.Domains, ignoreOrder: true);
    }

    private static string[] QuotedList(string source, string name)
    {
        var match = Regex.Match(
            source,
            @"export const " + name + @"\s*:\s*readonly string\[\]\s*=\s*\[(?<body>.*?)\];",
            RegexOptions.Singleline);
        match.Success.ShouldBeTrue($"Could not find the {name} list in the Angular file.");
        var items = Regex.Matches(match.Groups["body"].Value, "'([^']+)'")
            .Select(m => m.Groups[1].Value)
            .ToArray();
        items.Length.ShouldBeGreaterThan(5, $"{name} parsed to almost nothing: the guard would pass vacuously.");
        return items;
    }

    private static string FindAngularFileOrThrow([CallerFilePath] string callerPath = "")
    {
        var directory = Directory.GetParent(callerPath);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "angular", "src", "app", "shared", "placeholder-email.ts");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new ShouldAssertException(
            "Could not find angular/src/app/shared/placeholder-email.ts walking up from the test. " +
            "This drift guard must fail loudly when it cannot read its input.");
    }
}
