using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// 2026-11-20 -- pins what <see cref="EmailBodySanitizer"/> does to every SHIPPED email body, so a later
/// HtmlSanitizer or AngleSharp bump cannot quietly break them. The shipped default itself skips the
/// sanitizer (<see cref="NotificationTemplatesAppService.UpdateAsync"/>, 2026-11-02), but the first staff
/// edit of a template sends the whole body through it. So each body gets a hostile staff edit appended
/// (an event handler and a script) and the stored result must keep every <c>href="##Token##"</c> link,
/// in order, and lose both. Measured before the 9.0.892 to 11.0.26 bump: all 63 shipped bodies
/// sanitized byte-identically on both versions.
/// </summary>
public class ShippedEmailBodySanitizationTests
{
    private const string HostileEdit = "<p onclick=\"steal()\">edited</p><script>alert('x')</script>";

    private static readonly Regex TokenHref =
        new("href=\"(##[A-Za-z0-9_]+##)\"", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex EventHandlerAttribute =
        new(@"<[^>]*\son[a-z]+\s*=", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private readonly EmailBodySanitizer _sanitizer = new();

    public static IEnumerable<object[]> ShippedCodes() =>
        NotificationTemplateConsts.Codes.All
            .Where(NotificationTemplateSeedDefaults.HasResourceBackedBody)
            .OrderBy(code => code, StringComparer.Ordinal)
            .Select(code => new object[] { code });

    [Theory]
    [MemberData(nameof(ShippedCodes))]
    public void Sanitize_ShippedBodyWithHostileEdit_KeepsTokenLinksAndDropsScriptAndHandlers(string code)
    {
        var body = NotificationTemplateSeedDefaults.GetSeedDefaults(code).BodyEmail;

        var result = _sanitizer.Sanitize(body + HostileEdit);

        TokenLinks(result).ShouldBe(TokenLinks(body), $"{code}: the ##Token## links changed");
        result.ShouldContain("edited");
        result.ToLowerInvariant().ShouldNotContain("<script");
        result.ShouldNotContain("alert(");
        EventHandlerAttribute.IsMatch(result).ShouldBeFalse($"{code}: an on* attribute survived");
    }

    [Fact]
    public void ShippedBodies_AreLoaded_SoTheTheoryIsNotVacuous()
    {
        var codes = ShippedCodes().Select(row => (string)row[0]).ToList();

        codes.Count.ShouldBeGreaterThanOrEqualTo(60);
        codes.Sum(code => TokenLinks(NotificationTemplateSeedDefaults.GetSeedDefaults(code).BodyEmail).Count)
            .ShouldBeGreaterThanOrEqualTo(50);
    }

    private static List<string> TokenLinks(string html) =>
        TokenHref.Matches(html).Select(match => match.Groups[1].Value).ToList();
}
