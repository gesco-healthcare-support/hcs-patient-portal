using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- domain service for the opposing-side consent token on
/// a change request. Mirrors <c>Invitations/InvitationManager</c>: a 256-bit
/// random raw token is returned once and only its SHA256 hash is persisted; the
/// token is single-use (the aggregate's concurrency stamp resolves double-click
/// races) and expires after 7 days (expiry defaults to a No).
/// </summary>
public class ChangeRequestConsentManager : DomainService
{
    private readonly IAppointmentChangeRequestRepository _repository;
    private readonly IClock _clock;

    public ChangeRequestConsentManager(
        IAppointmentChangeRequestRepository repository,
        IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>
    /// Issues consent on a freshly-submitted request: stores the token hash +
    /// submitting side on the entity and returns the raw token (once) for the
    /// email link. The caller persists the request in its own unit of work.
    /// </summary>
    public virtual string IssueConsent(
        AppointmentChangeRequest request,
        ChangeRequestSide requestingSide,
        Guid submittedByUserId)
    {
        Check.NotNull(request, nameof(request));
        var rawToken = GenerateRawToken();
        var tokenHash = ComputeTokenHash(rawToken);
        var expiresAt = _clock.Now.ToUniversalTime()
            .AddDays(AppointmentChangeRequestConsts.ConsentDefaultTtlDays);
        request.IssueConsent(tokenHash, requestingSide, submittedByUserId, expiresAt);
        return rawToken;
    }

    /// <summary>
    /// Non-mutating: resolves the change request a raw token points at, for the
    /// public landing page. Throws <c>ConsentTokenInvalid</c> when no match. A
    /// length guard rejects obvious fuzzing before a DB roundtrip.
    /// </summary>
    public virtual async Task<AppointmentChangeRequest> ResolveByRawTokenAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)
            || rawToken.Length > AppointmentChangeRequestConsts.ConsentEncodedTokenMaxLength)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentTokenInvalid);
        }

        var tokenHash = ComputeTokenHash(rawToken);
        var request = await _repository.FindAsync(x => x.ConsentTokenHash == tokenHash);
        if (request == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentTokenInvalid);
        }
        return request;
    }

    /// <summary>
    /// Atomic: records the opposing side's decision. If the token has expired it
    /// is defaulted to a No (Expired) and <c>ConsentExpired</c> is thrown so the
    /// caller can surface the expiry message + notify staff. The aggregate's
    /// concurrency stamp makes a double-click race resolve to a single decision
    /// (the loser sees <c>AbpDbConcurrencyException</c>).
    /// </summary>
    public virtual async Task<AppointmentChangeRequest> RecordDecisionAsync(
        string rawToken,
        bool approved,
        string? respondedByEmail)
    {
        var request = await ResolveByRawTokenAsync(rawToken);
        var nowUtc = _clock.Now.ToUniversalTime();

        if (request.IsConsentExpired(nowUtc))
        {
            request.MarkConsentExpired(nowUtc);
            await _repository.UpdateAsync(request, autoSave: true);
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentExpired);
        }

        request.RecordConsentDecision(approved, respondedByEmail, nowUtc);
        await _repository.UpdateAsync(request, autoSave: true);
        return request;
    }

    /// <summary>
    /// 32 cryptographic random bytes encoded as URL-safe Base64 without padding
    /// (~43 chars). Mirrors <c>InvitationManager.GenerateRawToken</c>.
    /// </summary>
    internal static string GenerateRawToken()
    {
        Span<byte> buffer = stackalloc byte[AppointmentChangeRequestConsts.ConsentTokenByteLength];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>SHA256 of the UTF8 raw token as lowercase hex (64 chars).</summary>
    internal static string ComputeTokenHash(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hashed = SHA256.HashData(bytes);
        var sb = new StringBuilder(AppointmentChangeRequestConsts.ConsentTokenHashLength);
        foreach (var b in hashed)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
