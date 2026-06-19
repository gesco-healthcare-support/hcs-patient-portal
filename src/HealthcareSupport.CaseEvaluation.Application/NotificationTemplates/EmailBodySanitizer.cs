using System.Text.RegularExpressions;
using Ganss.Xss;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Ganss.Xss-backed <see cref="IEmailBodySanitizer"/>. Uses the library's
/// default safe allowlist (drops <c>script</c>/<c>iframe</c>/<c>object</c>,
/// strips <c>on*</c> event handlers, and rejects non-allowed URL schemes)
/// plus two adjustments for email templates:
/// <list type="bullet">
///   <item><c>mailto:</c> is allowed (templates link support addresses).</item>
///   <item>a bare <c>##Token##</c> merge placeholder in a link/image URL is
///         preserved -- otherwise Ganss strips the non-scheme href and a
///         templated link like <c>&lt;a href="##ResetUrl##"&gt;</c> would lose
///         its target before <see cref="TemplateVariableSubstitutor"/> can
///         swap in the real URL at render time.</item>
/// </list>
/// Registered as a singleton: the sanitizer is configured once in the
/// constructor and never mutated afterward, so <c>Sanitize</c> is safe to
/// call concurrently.
/// </summary>
public class EmailBodySanitizer : IEmailBodySanitizer, ISingletonDependency
{
    // A bare merge placeholder occupying an entire URL attribute value, e.g.
    // href="##ResetUrl##". Mirrors TemplateVariableSubstitutor's ##Var## syntax.
    private static readonly Regex MergeToken =
        new("^##[A-Za-z0-9_]+##$", RegexOptions.Compiled);

    private readonly HtmlSanitizer _sanitizer;

    public EmailBodySanitizer()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.FilterUrl += (_, e) =>
        {
            if (MergeToken.IsMatch(e.OriginalUrl))
            {
                e.SanitizedUrl = e.OriginalUrl;
            }
        };
    }

    public string Sanitize(string html)
        => string.IsNullOrEmpty(html) ? html : _sanitizer.Sanitize(html);
}
