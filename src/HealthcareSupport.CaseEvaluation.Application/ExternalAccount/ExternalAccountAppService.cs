using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Extensions;
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
    private const string ResendVerificationKeyPrefix = "resend-verify";
    private const int ResendVerificationMaxPerHour = 3;
    private static readonly TimeSpan ResendVerificationCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ResendVerificationHourlyWindow = TimeSpan.FromHours(1);

    // Item D (2026-08-22) -- the password-reset flow was COMPLETELY UNTHROTTLED for real users.
    //
    // The reset limiter that existed was ASP.NET middleware in HttpApi.Host, keyed on
    // ExternalAccountController's path prefix. But the AuthServer registers no rate limiter at all,
    // and its ForgotPassword page calls this service IN-PROCESS through DI -- no HTTP hop, so no
    // middleware. Nothing in the Angular app calls the API endpoint either (its /account/* routes
    // were deleted in PR #201), so the limiter guarded a path nothing used while the page users
    // actually reach had no cap. That left an anonymous form able to send unlimited real email
    // through our SMTP relay: mailbox flooding for any known address, plus sender-reputation damage
    // on a relay that has already had deliverability trouble.
    //
    // The throttle therefore belongs HERE, in the AppService both entry paths share. 10/hour is well
    // above any legitimate use; the 60-second cooldown is the part a real user will meet.
    private const string PasswordResetKeyPrefix = "password-reset";
    private const int PasswordResetMaxPerHour = 10;
    private static readonly TimeSpan PasswordResetCooldown = TimeSpan.FromSeconds(60);

    [AllowAnonymous]
    public virtual async Task SendPasswordResetCodeAsync(SendPasswordResetCodeInput input)
    {
        Check.NotNull(input, nameof(input));
        var normalizedEmail = NormalizeEmail(input.Email);
        if (normalizedEmail.Length == 0)
        {
            return;
        }

        // Item D (2026-08-22): throttle BEFORE the user lookup, and stamp unconditionally.
        //
        // Both choices are about enumeration, not just flooding. If only REGISTERED addresses were
        // throttled, an attacker could tell registered from unregistered by probing until one of them
        // started refusing -- the throttle itself would become the oracle that the rest of this flow
        // is carefully built to avoid (see PasswordResetGate's silent-return-on-null). Checking and
        // stamping ahead of the lookup makes every address behave identically.
        //
        // This is a deliberate deviation from the plan's "stamp after the dispatch attempt": stamping
        // early costs a legitimate user one attempt out of ten if they mistype their address, and buys
        // a uniformity guarantee that is hard to get any other way.
        if (await IsPasswordResetRateLimitedAsync(normalizedEmail))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.PasswordResetThrottled)
                .WithData("retryAfterSeconds", (int)PasswordResetCooldown.TotalSeconds);
        }

        await StampPasswordResetRateLimitAsync(normalizedEmail);

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
        // Item C (2026-08-22): a null tenant is a HOST OPERATOR, not a code bug.
        //
        // The old comment here said "External user without a tenant is a code bug", which was true
        // when it was written. Phase D made internal operators host logins and invalidated the
        // premise: this early return meant internal staff silently could not self-reset at all, and
        // the page shows generic success either way, so the whole flow no-opped. The AccountEmailer
        // was fixed the same day; this service was missed.
        //
        // The fence comes down, but not all the way. See PasswordResetGate.IsHostAccountEligible: the
        // risk is PRIVILEGE, not disclosure, so a null-tenant user is served only when they hold a
        // recognised internal role and do not carry the external marker.
        if (!user.TenantId.HasValue && !await IsHostAccountEligibleAsync(user))
        {
            // Error, not Warning: reaching here means a host row exists that the product cannot
            // create, so somebody hand-made it. That is worth noticing, and Warning is the level this
            // bug hid at for a month.
            _logger.LogError(
                "ExternalAccountAppService.SendPasswordResetCodeAsync: user {UserId} has no TenantId and is not host-eligible; refusing.",
                user.Id);
            return;
        }

        var resetUrl = await _accountUrlBuilder.BuildPasswordResetUrlForUserAsync(
            user.TenantId, user.Id, token);
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
        catch (BusinessException ex)
            when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
        {
            // Item C (2026-08-22): Error, not Warning. Anti-enumeration governs the RESPONSE, not the
            // logs -- the caller still sees generic success. A missing template is a deployment or
            // seeding fault that affects EVERY user of this flow, and swallowing it at Warning is
            // precisely why the host-scope bug survived a month unnoticed.
            // 2026-08-25: the template code is NAMED in the message rather than passed as an
            // argument. CodeQL reads `NotificationTemplateConsts.Codes.ResetPassword` and
            // `.PasswordChange` as sensitive by identifier name and raised
            // cs/cleartext-storage-of-sensitive-information (2 high) for logging them. They are
            // template CODES -- the literal strings "ResetPassword" and "PasswordChange" -- so the
            // alerts were false positives. The fix is deliberately NOT to silence the rule: it is the
            // one rule you most want armed on a file whose job includes handling real passwords, so
            // suppressing it here would trade a cosmetic win for a real blind spot. Dropping the
            // constant from the argument list removes the flow instead. All three catches are written
            // the same way so they cannot drift apart again.
            _logger.LogError(
                ex,
                "ExternalAccountAppService.SendPasswordResetCodeAsync: the ResetPassword template is MISSING for user {UserId}; no email was sent. Caller still saw generic success.",
                user.Id);
        }
        catch (Exception ex)
        {
            // Transport and other failures stay at Warning and stay swallowed: they are per-attempt,
            // not a misconfiguration, and the password has not changed.
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

        // Item D (2026-08-22) -- a completed reset RESTORES ACCESS.
        //
        // Nothing used to clear the lockout. A grep across src/ finds no call to
        // SetLockoutEndDateAsync or ResetAccessFailedCountAsync anywhere, and Identity's
        // ResetPasswordAsync contract is password-only. Only a successful sign-in resets the failure
        // count, and that cannot happen while PreSignInCheck short-circuits on the lockout -- so a
        // locked-out user who did exactly what the system told them to do stayed locked out anyway,
        // for the rest of the lockout window.
        //
        // Clearing it here is safe on a STRONGER signal than the one that caused the lockout:
        // possession of a valid single-use reset token proves control of the registered mailbox,
        // whereas the failed password attempts prove nothing about who made them. This is OWASP's
        // named mitigation for lockout-as-denial-of-service (Forgot Password Cheat Sheet), which also
        // advises that a forgotten-password flow restore access even when the account is locked.
        //
        // The ResetAccessFailedCountAsync override that item D adds to IdentityUserManager also
        // zeroes the escalation counter, so this user's next first lockout is one minute again rather
        // than resuming at the top of the ladder.
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);

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
        catch (BusinessException ex)
            when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
        {
            // Item C (2026-08-22): the plan named two catches; this is a THIRD with the same defect.
            // Leaving one of three at Warning would mean a missing PasswordChange template stayed
            // invisible while the other two shouted, which is the inconsistency that hid the original
            // bug. The password HAS already changed here, so the caller still gets success.
            _logger.LogError(
                ex,
                "ExternalAccountAppService.ResetPasswordAsync: the PasswordChange template is MISSING for user {UserId}; the password WAS changed but no confirmation was sent.",
                user.Id);
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
            // 2026-08-25: no identifier logged, not even a digest of one.
            //
            // This first said "email-key {EmailKey}" while passing the raw address. A SHA-256 digest
            // was tried next and CodeQL still flagged it -- correctly. An UNSALTED digest of an email
            // is reversible in practice, because the address space is enumerable: anyone holding the
            // logs and a candidate list can confirm a match. Salting it per process would break the
            // cross-request correlation the digest existed for, which left nothing worth keeping.
            //
            // What the line is actually for is knowing that resend throttling is firing, and how
            // often. That survives without naming anyone.
            _logger.LogInformation(
                "ExternalAccountAppService.ResendEmailVerificationAsync: rate-limited. Silent reject.");
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
        // Item C (2026-08-22): same fix as the reset path above, and it matters just as much -- an
        // unverified operator hits resend BEFORE they ever reach forgot-password, so this guard is the
        // first one they run into. UserRegistered is seeded host-scope exactly like ResetPassword, so
        // resend was broken identically.
        if (!user.TenantId.HasValue && !await IsHostAccountEligibleAsync(user))
        {
            _logger.LogError(
                "ExternalAccountAppService.ResendEmailVerificationAsync: user {UserId} has no TenantId and is not host-eligible; refusing.",
                user.Id);
            return;
        }

        var verifyUrl = await _accountUrlBuilder.BuildEmailConfirmationUrlForUserAsync(
            user.TenantId, user.Id, token);

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
        catch (BusinessException ex)
            when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
        {
            // See the reset path: a missing template is a deployment fault, loud in the logs, still
            // generic in the response.
            // Not flagged by CodeQL (UserRegistered does not trip the name heuristic), but written
            // like its two siblings above so the three catches stay uniform.
            _logger.LogError(
                ex,
                "ExternalAccountAppService.ResendEmailVerificationAsync: the UserRegistered template is MISSING for user {UserId}; no email was sent. Caller still saw generic success.",
                user.Id);
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
    /// Item C (2026-08-22) -- whether a null-tenant user is a genuine host operator.
    ///
    /// <para>Roles do not live on <see cref="IdentityUser"/>, so they are fetched here and passed into
    /// the pure gate, which keeps the eligibility RULE unit-testable without ABP DI while the fetching
    /// stays where the DI is.</para>
    ///
    /// <para>The flag is read via <c>ExtraPropertyConverters</c>, never <c>GetProperty&lt;bool&gt;</c>:
    /// the typed overload throws on a freshly reloaded entity because ABP cannot coerce the
    /// <c>JsonElement</c> that comes back out of the JSON column.</para>
    /// </summary>
    private async Task<bool> IsHostAccountEligibleAsync(IdentityUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var isExternal = ExtraPropertyConverters.GetBoolOrDefault(
            user,
            CaseEvaluationModuleExtensionConfigurator.IsExternalUserPropertyName);

        return PasswordResetGate.IsHostAccountEligible(roles, isExternal);
    }

    /// <summary>
    /// Phase 1.D rate-limit gate, GENERALISED by item D (2026-08-22) so the password-reset flow shares
    /// one implementation instead of a second copy that would drift. True when the email key is either
    /// inside its cooldown OR at/over its hourly cap.
    ///
    /// <para>Cache-backed (Redis in dev/prod, in-memory in tests via <c>MemoryDistributedCache</c>). A
    /// cache read failure returns FALSE -- fail OPEN. That mattered for resend-verification and matters
    /// more for password reset, because reset is now the documented way back in from a lockout: a Redis
    /// outage must not be able to strand a locked-out user.</para>
    /// </summary>
    private async Task<bool> IsRateLimitedAsync(
        string keyPrefix,
        string normalizedEmail,
        int maxPerHour,
        string purpose)
    {
        var cooldownKey = $"{keyPrefix}:cooldown:{normalizedEmail}";
        var hourlyKey = $"{keyPrefix}:hourly:{normalizedEmail}";

        try
        {
            if (await _cache.GetStringAsync(cooldownKey) != null)
            {
                return true;
            }
            var countStr = await _cache.GetStringAsync(hourlyKey);
            if (countStr != null && int.TryParse(countStr, out var count) && count >= maxPerHour)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ExternalAccountAppService: rate-limit cache read failed; failing open for {Purpose}.",
                purpose);
        }
        return false;
    }

    private Task<bool> IsResendVerificationRateLimitedAsync(string normalizedEmail) =>
        IsRateLimitedAsync(
            ResendVerificationKeyPrefix,
            normalizedEmail,
            ResendVerificationMaxPerHour,
            "resend-verification");

    private Task<bool> IsPasswordResetRateLimitedAsync(string normalizedEmail) =>
        IsRateLimitedAsync(
            PasswordResetKeyPrefix,
            normalizedEmail,
            PasswordResetMaxPerHour,
            "password-reset");

    /// <summary>
    /// Stamps the cooldown + increments the hourly counter for the given
    /// email key. Cooldown TTL is 60 seconds (rolling); hourly counter TTL
    /// is the remaining time in the current 1-hour window when the first
    /// request landed (not a rolling window -- counter resets to 0 once
    /// the TTL expires). Cache write failure is logged but not propagated.
    /// </summary>
    /// <param name="purpose">
    /// A caller-supplied LITERAL naming the limiter, for the failure log. It exists rather than
    /// logging <paramref name="keyPrefix"/> because that argument flows from the constant
    /// <c>PasswordResetKeyPrefix</c>, whose NAME contains "Password" -- enough for CodeQL to read it
    /// as a secret and raise cs/cleartext-storage-of-sensitive-information (it did: alert 286). The
    /// value is only "password-reset". A literal is not a tracked source, which is why the sibling
    /// <see cref="IsRateLimitedAsync"/> has always logged its own purpose parameter without
    /// tripping the same rule. The two methods now match.
    /// </param>
    private async Task StampRateLimitAsync(
        string keyPrefix,
        string normalizedEmail,
        TimeSpan cooldown,
        string purpose)
    {
        var cooldownKey = $"{keyPrefix}:cooldown:{normalizedEmail}";
        var hourlyKey = $"{keyPrefix}:hourly:{normalizedEmail}";

        try
        {
            await _cache.SetStringAsync(cooldownKey, "1", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cooldown,
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
            // Names the LIMITER, not the person: "resend-verification" or "password-reset", which is
            // the question an operator actually has here -- whose cache writes are failing -- and it
            // carries no identifier. See the note at the resend rate-limit log for why not even a
            // digest of the address is used, and the purpose parameter's docs for why this logs a
            // caller-supplied literal rather than the keyPrefix constant.
            _logger.LogWarning(
                ex,
                "ExternalAccountAppService: rate-limit cache write failed for {Purpose}; rate-limit may not enforce on this request.",
                purpose);
        }
    }

    private Task StampResendVerificationRateLimitAsync(string normalizedEmail) =>
        StampRateLimitAsync(
            ResendVerificationKeyPrefix,
            normalizedEmail,
            ResendVerificationCooldown,
            "resend-verification");

    private Task StampPasswordResetRateLimitAsync(string normalizedEmail) =>
        StampRateLimitAsync(
            PasswordResetKeyPrefix,
            normalizedEmail,
            PasswordResetCooldown,
            "password-reset");

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

    // ResolveAuthServerBaseUrlAsync removed. The hardcoded "Falkinstein"
    // workaround it carried is now actually fixed: IAccountUrlBuilder
    // resolves the tenant name from the explicit tenantId argument the
    // caller derives from user.TenantId.

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
