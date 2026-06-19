using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// #6 (2026-06-19) -- pure unit tests for <see cref="EmailBodySanitizer"/>.
/// Runnable without the ABP integration host (which is license-blocked; see
/// <c>docs/handoffs/2026-05-03-test-host-license-blocker.md</c>). Pins the
/// XSS allowlist against the constructs real templates use (links, lists,
/// formatting) and the ##Token## merge-placeholder URL preservation that
/// keeps templated links alive through sanitization.
/// </summary>
public class EmailBodySanitizerTests
{
    private readonly IEmailBodySanitizer _sanitizer = new EmailBodySanitizer();

    [Fact]
    public void Sanitize_RemovesScriptTagAndContent()
    {
        var result = _sanitizer.Sanitize("<p>Hi</p><script>alert('xss')</script>");

        result.ToLowerInvariant().ShouldNotContain("<script");
        result.ShouldNotContain("alert");
        result.ShouldContain("Hi");
    }

    [Fact]
    public void Sanitize_RemovesEventHandlerAttribute()
    {
        var result = _sanitizer.Sanitize("<p onclick=\"steal()\">Hi</p>");

        result.ToLowerInvariant().ShouldNotContain("onclick");
        result.ShouldNotContain("steal");
        result.ShouldContain("Hi");
    }

    [Fact]
    public void Sanitize_KeepsCommonEmailFormatting()
    {
        var result = _sanitizer.Sanitize(
            "<p><b>Bold</b> <i>Italic</i></p><ul><li>One</li><li>Two</li></ul>");

        result.ToLowerInvariant().ShouldContain("<b>");
        result.ToLowerInvariant().ShouldContain("<i>");
        result.ToLowerInvariant().ShouldContain("<li>");
    }

    [Fact]
    public void Sanitize_KeepsHttpsLink()
    {
        var result = _sanitizer.Sanitize("<a href=\"https://gesco.example/portal\">Open</a>");

        result.ShouldContain("https://gesco.example/portal");
    }

    [Fact]
    public void Sanitize_PreservesMergeTokenInHref()
    {
        // <a href="##ResetUrl##"> must survive: TemplateVariableSubstitutor
        // swaps the token for a real URL at render time. Without the FilterUrl
        // rule, Ganss would strip the non-scheme href.
        var result = _sanitizer.Sanitize("<a href=\"##ResetUrl##\">Reset password</a>");

        result.ShouldContain("##ResetUrl##");
        result.ShouldContain("Reset password");
    }

    [Fact]
    public void Sanitize_RemovesJavascriptScheme()
    {
        var result = _sanitizer.Sanitize("<a href=\"javascript:alert(1)\">Click</a>");

        result.ToLowerInvariant().ShouldNotContain("javascript:");
        result.ShouldNotContain("alert");
    }

    [Fact]
    public void Sanitize_LeavesMergeTokenTextUntouched()
    {
        var result = _sanitizer.Sanitize("<p>Hi ##PatientName##, your visit is set.</p>");

        result.ShouldContain("##PatientName##");
    }

    [Fact]
    public void Sanitize_EmptyString_ReturnsEmpty()
    {
        _sanitizer.Sanitize(string.Empty).ShouldBe(string.Empty);
    }
}
