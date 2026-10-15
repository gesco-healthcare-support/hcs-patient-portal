using System.IO;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HttpApiHost.Tests;

/// <summary>
/// The middleware that reads the email out of an anonymous password-reset POST so the rate limiter
/// can partition by account. It must stash the normalized email ONLY for a small JSON POST to the
/// reset path, must never throw, and must always leave the body readable for model binding.
/// </summary>
/// <remarks>
/// Every "does not peek" case sends a request that would otherwise be peeked (a valid email in the
/// body), so an empty <c>Items</c> can only mean the guard refused it, not that there was nothing
/// to find.
/// </remarks>
public class PasswordResetEmailPeekMiddlewareTests
{
    private const string ResetPath = CaseEvaluationHttpApiHostModule.PasswordResetPathPrefix + "/send-password-reset-code";
    private const string ValidBody = "{\"email\":\"  TEST-Reset.User@Test.Local \",\"appName\":\"Angular\"}";

    private static DefaultHttpContext Request(
        string method = "POST",
        string path = ResetPath,
        string body = ValidBody,
        string? contentType = "application/json; charset=utf-8",
        long? contentLength = null)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = contentLength ?? bytes.Length;
        return context;
    }

    /// <summary>Runs the middleware; the next delegate records the body it can still read.</summary>
    private static async Task<string?> RunAsync(HttpContext context)
    {
        string? bodySeenDownstream = null;
        var middleware = new PasswordResetEmailPeekMiddleware(async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            bodySeenDownstream = await reader.ReadToEndAsync();
        });

        await middleware.InvokeAsync(context);
        return bodySeenDownstream;
    }

    [Fact]
    public async Task A_json_reset_post_stashes_the_trimmed_lowercased_email_and_leaves_the_body_readable()
    {
        var context = Request();

        var downstream = await RunAsync(context);

        context.Items[PasswordResetEmailPeekMiddleware.ContextItemKey].ShouldBe("test-reset.user@test.local");
        downstream.ShouldBe(ValidBody);
    }

    [Theory]
    [InlineData("GET", ResetPath, "application/json", null)]
    [InlineData("POST", "/api/app/appointments", "application/json", null)]
    [InlineData("POST", ResetPath, "application/x-www-form-urlencoded", null)]
    [InlineData("POST", ResetPath, null, null)]
    [InlineData("POST", ResetPath, "application/json", 4097L)]
    [InlineData("POST", ResetPath, "application/json", 0L)]
    public async Task Requests_outside_the_guard_are_passed_through_without_a_peek(
        string method, string path, string? contentType, long? contentLength)
    {
        // Each request carries a valid email body; only the guard can keep it out of Items.
        var context = Request(method, path, ValidBody, contentType, contentLength);

        await RunAsync(context);

        context.Items.ContainsKey(PasswordResetEmailPeekMiddleware.ContextItemKey).ShouldBeFalse();
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[\"TEST-reset@test.local\"]")]
    [InlineData("{\"email\":42}")]
    [InlineData("{\"email\":\"   \"}")]
    [InlineData("{\"username\":\"TEST-reset@test.local\"}")]
    public async Task A_body_without_a_usable_email_is_ignored_without_throwing_and_is_still_readable(string body)
    {
        var context = Request(body: body);

        var downstream = await RunAsync(context);

        context.Items.ContainsKey(PasswordResetEmailPeekMiddleware.ContextItemKey).ShouldBeFalse();
        downstream.ShouldBe(body);
    }
}
