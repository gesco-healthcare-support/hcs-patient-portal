using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace HealthcareSupport.CaseEvaluation.RateLimiting;

/// <summary>
/// BUG-035 fix (2026-05-22) -- peeks the JSON body's <c>email</c>
/// field on anonymous POSTs under
/// <see cref="CaseEvaluationHttpApiHostModule.PasswordResetPathPrefix"/>
/// and stashes the lowercased, trimmed value into
/// <c>HttpContext.Items[<see cref="ContextItemKey"/>]</c>. Runs in
/// the middleware pipeline immediately before
/// <c>UseRateLimiter()</c> so the rate-limiter partitioner can read
/// the value as its primary partition key (per-account control per
/// OWASP Forgot Password Cheat Sheet).
///
/// <para>The body is rewound after the peek so MVC's model binding
/// still receives a fresh stream. Errors (malformed JSON, body too
/// large, missing field, non-JSON content type) silently no-op --
/// the partitioner then falls through to JWT sub / client IP exactly
/// as it did before this middleware existed.</para>
///
/// <para>Body-size cap of 4 KB prevents an attacker from forcing the
/// server to buffer arbitrarily large bodies in memory just by
/// hitting the password-reset path. The legitimate body shape is
/// always &lt; 300 bytes (email + appName), so 4 KB is generous
/// headroom.</para>
/// </summary>
public sealed class PasswordResetEmailPeekMiddleware
{
    public const string ContextItemKey = "pwd-reset.email";
    private const int MaxBodyBytes = 4096;

    private readonly RequestDelegate _next;

    public PasswordResetEmailPeekMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldPeek(context))
        {
            await TryPeekEmailAsync(context);
        }

        await _next(context);
    }

    private static bool ShouldPeek(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return false;
        }
        if (!context.Request.Path.StartsWithSegments(CaseEvaluationHttpApiHostModule.PasswordResetPathPrefix))
        {
            return false;
        }
        // Body-size guard: skip empty / unknown / too-large bodies.
        // The legitimate password-reset bodies are < 300 bytes.
        if (context.Request.ContentLength is null or <= 0 or > MaxBodyBytes)
        {
            return false;
        }
        // Content-Type check: only JSON. Non-JSON anonymous bodies
        // (form-encoded, multipart) are not what the password-reset
        // endpoints accept; they'll be rejected downstream regardless.
        var contentType = context.Request.ContentType;
        if (contentType is null ||
            !contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    private static async Task TryPeekEmailAsync(HttpContext context)
    {
        try
        {
            context.Request.EnableBuffering();
            using var doc = await JsonDocument.ParseAsync(
                context.Request.Body,
                cancellationToken: context.RequestAborted);

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("email", out var emailElement) &&
                emailElement.ValueKind == JsonValueKind.String)
            {
                var email = emailElement.GetString();
                if (!string.IsNullOrWhiteSpace(email))
                {
                    context.Items[ContextItemKey] = email.Trim().ToLowerInvariant();
                }
            }
        }
        catch
        {
            // Silently swallow -- malformed JSON, parser failure, etc.
            // The partitioner falls back to JWT sub / IP exactly as it
            // did before this middleware existed.
        }
        finally
        {
            // Rewind so MVC's model binding still sees the body.
            // EnableBuffering wraps the stream in a FileBufferingReadStream
            // which is always seekable; the guard is defensive.
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }
        }
    }
}
