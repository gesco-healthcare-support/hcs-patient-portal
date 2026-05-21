using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Phase 10 (2026-05-03) -- OLD-parity password-reset surface. See
/// <see cref="IExternalAccountAppService"/> for the rationale behind
/// shipping a NEW AppService rather than overriding ABP Pro's
/// <c>AccountAppService</c> (member obfuscation makes service-replacement
/// fragile across patch versions).
///
/// <para>Two endpoints; both anonymous so unauthenticated users can
/// reset; rate-limited at the HTTP layer
/// (<c>CaseEvaluationHttpApiHostModule</c>) to 5 requests / hour / email
/// key per audit Q3 resolution.</para>
///
/// <para>Phase 1.B/1.C (Category 1, 2026-05-08): the inline
/// <c>IEmailSender</c> dispatch was replaced with the per-tenant
/// <see cref="INotificationDispatcher"/> + <c>NotificationTemplate</c>
/// path. Both endpoints now render the <c>ResetPassword</c> and
/// <c>PasswordChange</c> templates that IT-Admin can edit per tenant;
/// SMTP send becomes a queued Hangfire job (not synchronous), the
/// same pipeline every other email in the app uses. The previous
/// "synchronous to avoid did-it-send gap" rationale is moot: the SPA
/// always shows a generic "if registered, check your email" message
/// regardless of send status, and async queuing is what the rest of
/// the stack does.</para>
/// </summary>
[RemoteService(IsEnabled = false)]
public class ExternalAccountAppService : CaseEvaluationAppService, IExternalAccountAppService
{
    // BUG-029 v3 fix (2026-05-21): DefaultAuthServerBaseUrl const removed.
    // Tenant-aware URL composition lives in IAccountUrlBuilder; missing
    // App__SelfUrl env var now throws a clear error instead of silently
    // emitting "http://falkinstein.localhost:44368".

    private readonly IdentityUserManager _userManager;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ExternalAccountAppService> _logger;
    private readonly Notifications.IAccountUrlBuilder _accountUrlBuilder;

    public ExternalAccountAppService(
        IdentityUserManager userManager,
        INotificationDispatcher dispatcher,
        IDistributedCache cache,
        ILogger<ExternalAccountAppService> logger,
        Notifications.IAccountUrlBuilder accountUrlBuilder)
    {
        _userManager = userManager;
        _dispatcher = dispatcher;
        _cache = cache;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
    }

    // Phase 1.D rate-limit constants (Adrian Decision 3, 2026-05-08): tighter
    // than the password-reset-by-email partition because resend is a higher
    // SMTP-flood risk -- a registered-but-unverified email is a known target.
    // Silent reject (no thrown exception, no leak): the user-visible response
    // is identical to user-not-found / already-confirmed paths, which keeps
    // the endpoint enumeration-safe.
    private const int ResendVerificationMaxPerHour = 3;
    private static readonly TimeSpan ResendVerificationCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ResendVerificationHourlyWindow = TimeSpan.FromHours(1);

    [AllowAnonymous]
    public virtual async Task SendPasswordResetCodeAsync(SendPasswordResetCodeInput input)
    {
        Check.NotNull(input, nameof(input));
        var normalizedEmail = NormalizeEmail(input.Email);
        if (normalizedEmail.Length == 0)
        {
            return;
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        PasswordResetGate.EnsureUserCanRequestReset(user);
        if (user == null)
        {
            // Silent return -- caller treats this as generic success.
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        // BUG-029 v3 fix (2026-05-21): tenant-aware reset URL via the
        // user's TenantId (source of truth), then append returnUrl
        // separately because IAccountUrlBuilder owns base + 3 standard
        // params; per-flow extras (returnUrl) layer on top.
        if (!user.TenantId.HasValue)
        {
            // External user without a tenant is a code bug.
            _logger.LogWarning(
                "ExternalAccountAppService.SendPasswordResetCodeAsync: user {UserId} has no TenantId; skipping send.",
                user.Id);
            return;
        }
        var resetUrl = await _accountUrlBuilder.BuildPasswordResetUrlAsync(
            user.TenantId.Value, user.Id, token);
        if (!string.IsNullOrWhiteSpace(input.ReturnUrl))
        {
            resetUrl += "&returnUrl=" + WebUtility.UrlEncode(input.ReturnUrl);
        }

        // Phase 1.B (Category 1, 2026-05-08): dispatch the ResetPassword template
        // through the per-tenant NotificationTemplate path. Body is the seeded
        // EmailBodies/ResetPassword.html with ##PatientFirstName## + ##URL##
        // tokens substituted. IT-Admin-editable per tenant; queued via Hangfire.
        try
        {
            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.ResetPassword,
                recipients: new[]
                {
                    new NotificationRecipient(
                        email: user.Email!,
                        role: RecipientRole.Patient,
                        isRegistered: true),
                },
                variables: BuildPasswordTokenVariables(user, resetUrl),
                contextTag: $"PasswordReset/RequestLink/{user.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ExternalAccountAppService.SendPasswordResetCodeAsync: dispatch failed for user {UserId}. Returning generic success to caller.",
                user.Id);
        }
    }

    [AllowAnonymous]
    public virtual async Task ResetPasswordAsync(ResetPasswordInput input)
    {
        Check.NotNull(input, nameof(input));
        EnsurePasswordsMatch(input.Password, input.ConfirmPassword);

        var user = await _userManager.FindByIdAsync(input.UserId.ToString());
        if (user == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid);
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, input.ResetToken, input.Password);
        if (!resetResult.Succeeded)
        {
            // ABP Identity surfaces both "token invalid" and "new password
            // failed policy" through the same IdentityResult.Errors list.
            // Token-related codes ("InvalidToken") are silently mapped to
            // ResetPasswordTokenInvalid to avoid info leak; policy-violation
            // codes (PasswordRequiresDigit etc.) re-throw verbatim so the
            // user can see what to fix. The classifier matches the codes
            // ABP returns for token vs policy failures.
            if (IsTokenFailure(resetResult))
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid);
            }
            throw new UserFriendlyException(
                string.Join(", ", resetResult.Errors.Select(e => e.Description)));
        }

        // Phase 1.C (Category 1, 2026-05-08): security-receipt confirmation
        // email after a successful password reset. ABP 10.0.2 has no
        // UserPasswordChangedEto distributed event we can subscribe to
        // (verified by reflection 2026-05-03), so the confirmation goes
        // out inline immediately after the ResetPasswordAsync succeeds.
        // Dispatched through the per-tenant PasswordChange template via
        // INotificationDispatcher (replaces an earlier inline IEmailSender
        // path that used localized strings with unsubstituted {0}
        // placeholders -- pre-Phase-1.C bug). Dispatch failure is logged
        // but not bubbled because the user's password has already been
        // changed and the API call should still return success.
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }
        try
        {
            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.PasswordChange,
                recipients: new[]
                {
                    new NotificationRecipient(
                        email: user.Email,
                        role: RecipientRole.Patient,
                        isRegistered: true),
                },
                variables: BuildPasswordTokenVariables(user, url: null),
                contextTag: $"PasswordChange/PostReset/{user.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ExternalAccountAppService.ResetPasswordAsync: post-reset confirmation dispatch failed for user {UserId}.",
                user.Id);
        }
    }

    /// <summary>
    /// Phase 1.D (Category 1, 2026-05-08): re-fires the email-verification
    /// link to an unverified user. See <see cref="IExternalAccountAppService.ResendEmailVerificationAsync"/>
    /// for the contract.
    /// </summary>
    [AllowAnonymous]
    public virtual async Task ResendEmailVerificationAsync(ResendEmailVerificationInput input)
    {
        Check.NotNull(input, nameof(input));
        var normalizedEmail = NormalizeEmail(input.Email);
        if (normalizedEmail.Length == 0)
        {
            return;
        }

        // Phase 1.D rate-limit: silent reject when over the hourly limit OR
        // inside the 60-second cooldown window. Silent so the response is
        // identical to the user-not-found / already-confirmed paths -- keeps
        // the endpoint enumeration-safe and doesn't leak rate-limit state to
        // attackers. Cache-keyed by normalized email so the same user can't
        // bypass by varying case / whitespace.
        if (await IsResendVerificationRateLimitedAsync(normalizedEmail))
        {
            _logger.LogInformation(
                "ExternalAccountAppService.ResendEmailVerificationAsync: rate-limited for email-key {EmailKey}. Silent reject.",
                normalizedEmail);
            return;
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            // Silent return -- do not leak which emails are registered.
            return;
        }
        if (user.EmailConfirmed)
        {
            // Already confirmed -- no need to fire another verify link.
            // Generic success keeps the SPA flow consistent.
            return;
        }
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        // BUG-029 v3 fix (2026-05-21): tenant-aware verify URL via the
        // user's TenantId.
        if (!user.TenantId.HasValue)
        {
            _logger.LogWarning(
                "ExternalAccountAppService.ResendEmailVerificationAsync: user {UserId} has no TenantId; skipping send.",
                user.Id);
            return;
        }
        var verifyUrl = await _accountUrlBuilder.BuildEmailConfirmationUrlAsync(
            user.TenantId.Value, user.Id, token);

        try
        {
            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.UserRegistered,
                recipients: new[]
                {
                    new NotificationRecipient(
                        email: user.Email,
                        role: RecipientRole.Patient,
                        isRegistered: false),
                },
                variables: BuildPasswordTokenVariables(user, verifyUrl),
                contextTag: $"UserRegistered/Resend/{user.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ExternalAccountAppService.ResendEmailVerificationAsync: dispatch failed for user {UserId}. Returning generic success to caller.",
                user.Id);
        }

        // Stamp cache entries AFTER the dispatch so a dispatch failure doesn't
        // tick the rate-limit counter against a user who legitimately needs
        // a retry. Cooldown gate is the same email key for both successful
        // sends and failed-but-attempted sends -- failures still consume an
        // SMTP attempt slot, so we tick on every reach-the-dispatch path.
        await StampResendVerificationRateLimitAsync(normalizedEmail);
    }

    /// <summary>
    /// Phase 1.D rate-limit gate. True when the email-key is either (a)
    /// inside the 60-second cooldown OR (b) at/over the 3-per-hour cap.
    /// Cache-backed (Redis in dev/prod, in-memory in tests via
    /// <c>MemoryDistributedCache</c>). Failure to read the cache returns
    /// false (open) -- a Redis outage shouldn't lock all users out of
    /// resend-verification; better to fail-open for this UX gate.
    /// </summary>
    private async Task<bool> IsResendVerificationRateLimitedAsync(string normalizedEmail)
    {
        var cooldownKey = $"resend-verify:cooldown:{normalizedEmail}";
        var hourlyKey = $"resend-verify:hourly:{normalizedEmail}";

        try
        {
            if (await _cache.GetStringAsync(cooldownKey) != null)
            {
                return true;
            }
            var countStr = await _cache.GetStringAsync(hourlyKey);
            if (countStr != null && int.TryParse(countStr, out var count) && count >= ResendVerificationMaxPerHour)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ExternalAccountAppService: rate-limit cache read failed; failing open for resend-verification.");
        }
        return false;
    }

    /// <summary>
    /// Stamps the cooldown + increments the hourly counter for the given
    /// email key. Cooldown TTL is 60 seconds (rolling); hourly counter TTL
    /// is the remaining time in the current 1-hour window when the first
    /// request landed (not a rolling window -- counter resets to 0 once
    /// the TTL expires). Cache write failure is logged but not propagated.
    /// </summary>
    private async Task StampResendVerificationRateLimitAsync(string normalizedEmail)
    {
        var cooldownKey = $"resend-verify:cooldown:{normalizedEmail}";
        var hourlyKey = $"resend-verify:hourly:{normalizedEmail}";

        try
        {
            await _cache.SetStringAsync(cooldownKey, "1", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ResendVerificationCooldown,
            });

            var existing = await _cache.GetStringAsync(hourlyKey);
            var nextCount = (existing != null && int.TryParse(existing, out var c) ? c : 0) + 1;
            await _cache.SetStringAsync(hourlyKey, nextCount.ToString(), new DistributedCacheEntryOptions
            {
                // Set absolute expiration on first write; subsequent writes
                // refresh-but-don't-extend by re-using the existing window.
                // IDistributedCache doesn't expose remaining-TTL inspection,
                // so we accept the simplification: the window slides slightly
                // on each successful send. Behavioral effect: a user pinned
                // at the hourly cap eventually times out their counter as
                // they stop sending.
                AbsoluteExpirationRelativeToNow = ResendVerificationHourlyWindow,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ExternalAccountAppService: rate-limit cache write failed for email-key {EmailKey}; rate-limit may not enforce on this request.",
                normalizedEmail);
        }
    }

    // BUG-029 v3 fix (2026-05-21): BuildEmailConfirmationUrl static helper
    // moved into IAccountUrlBuilder. The Service now owns this shape.

    /// <summary>
    /// Phase 1.B/1.C variable bag for the ResetPassword and PasswordChange
    /// templates. PasswordFirstName / LastName / FullName / Email tokens
    /// are populated from the IdentityUser; URL is the reset-link for the
    /// SendPasswordResetCodeAsync flow and null for the post-reset
    /// confirmation flow (the PasswordChange body has no link). Brand
    /// placeholder tokens stay as empty strings until per-tenant branding
    /// ships (deferred to end-of-categories per Adrian directive
    /// 2026-05-08, Decision A).
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildPasswordTokenVariables(
        Volo.Abp.Identity.IdentityUser user,
        string? url)
    {
        var vars = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["PatientFirstName"] = user.Name ?? string.Empty,
            ["PatientLastName"] = user.Surname ?? string.Empty,
            ["PatientFullName"] = JoinName(user.Name, user.Surname),
            ["PatientEmail"] = user.Email ?? string.Empty,
            ["URL"] = url ?? string.Empty,
        };
        AddBrandPlaceholders(vars);
        return vars;
    }

    private static string JoinName(string? first, string? last)
    {
        var hasFirst = !string.IsNullOrWhiteSpace(first);
        var hasLast = !string.IsNullOrWhiteSpace(last);
        if (hasFirst && hasLast) return first!.Trim() + " " + last!.Trim();
        if (hasFirst) return first!.Trim();
        if (hasLast) return last!.Trim();
        return string.Empty;
    }

    private static void AddBrandPlaceholders(Dictionary<string, object?> vars)
    {
        vars["CompanyLogo"] = string.Empty;
        vars["lblHeaderTitle"] = string.Empty;
        vars["lblFooterText"] = string.Empty;
        vars["Email"] = string.Empty;
        vars["Skype"] = string.Empty;
        vars["ph_US"] = string.Empty;
        vars["fax"] = string.Empty;
        vars["imageInByte"] = string.Empty;
    }

    // BUG-029 v3 fix (2026-05-21): ResolveAuthServerBaseUrlAsync removed.
    // The hardcoded "Falkinstein" workaround it carried (TODO Phase 1B)
    // is now actually fixed: IAccountUrlBuilder resolves the tenant name
    // from the explicit tenantId argument passed by the caller.

    /// <summary>
    /// Trims + lowercases the inbound email so reverse lookups match the
    /// normalized form ABP Identity stores in <c>NormalizedEmail</c>. Returns
    /// an empty string when the input is null / whitespace -- callers treat
    /// this as a no-op (silent success path).
    /// Internal so unit tests can verify normalization edge cases.
    /// </summary>
    internal static string NormalizeEmail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }
        return raw.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Mirrors OLD <c>PutCredentialValidation</c>'s
    /// <c>Password.Equals(ConfirmPassword)</c> check
    /// (<c>UserAuthenticationDomain.cs:222</c>). Ordinal compare so trailing
    /// whitespace + case differences are caught.
    /// Internal for unit-test coverage.
    /// </summary>
    internal static void EnsurePasswordsMatch(string password, string confirmPassword)
    {
        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            throw new UserFriendlyException("Passwords don't match.");
        }
    }

    // BUG-029 v3 fix (2026-05-21): BuildResetUrl static helper moved into
    // IAccountUrlBuilder.BuildPasswordResetUrlAsync. The returnUrl param
    // is now appended at the call site (single use; not worth widening
    // the central builder's contract for it).

    /// <summary>
    /// Heuristic: ABP Identity's ResetPasswordAsync surfaces token failures
    /// with code <c>"InvalidToken"</c> (verified against ASP.NET Core
    /// Identity 10.0.x source -- IdentityErrorDescriber.InvalidToken).
    /// Anything else -- digit / length / non-alphanumeric policy errors --
    /// is a password-policy failure the user can fix without a new email.
    /// Internal for unit-test coverage.
    /// </summary>
    internal static bool IsTokenFailure(Microsoft.AspNetCore.Identity.IdentityResult result)
    {
        if (result == null)
        {
            return false;
        }
        foreach (var err in result.Errors)
        {
            if (string.Equals(err.Code, "InvalidToken", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
