using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Domain service for the change-request consent tokens (reschedule/cancel consent
/// redesign, 2026-07-01). Each solicited side gets its own 256-bit random raw token
/// (returned once; only the SHA256 hash is persisted). A token is single-use (the
/// aggregate's concurrency stamp resolves double-click races) and expires after 7 days
/// (expiry defaults that side to a No). Mirrors <c>Invitations/InvitationManager</c>.
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
    /// Issues consent for one side: generates a token, stores its hash + expiry on that
    /// side of the entity, and returns the raw token (once) for the email link. The caller
    /// sets submitter metadata via <see cref="AppointmentChangeRequest.InitiateConsent"/> and
    /// persists the request in its own unit of work.
    /// </summary>
    public virtual string IssueSideConsent(AppointmentChangeRequest request, ChangeRequestSide side)
    {
        Check.NotNull(request, nameof(request));
        var rawToken = GenerateRawToken();
        var tokenHash = ComputeTokenHash(rawToken);
        var expiresAt = _clock.Now.ToUniversalTime()
            .AddDays(AppointmentChangeRequestConsts.ConsentDefaultTtlDays);
        request.IssueSideConsent(side, tokenHash, expiresAt);
        return rawToken;
    }

    /// <summary>
    /// Non-mutating: resolves the change request + which side a raw token points at, for the
    /// public landing page. Throws <c>ConsentTokenInvalid</c> when no match. A length guard
    /// rejects obvious fuzzing before a DB roundtrip.
    /// </summary>
    public virtual async Task<ChangeRequestConsentMatch> ResolveByRawTokenAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)
            || rawToken.Length > AppointmentChangeRequestConsts.ConsentEncodedTokenMaxLength)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentTokenInvalid);
        }

        var tokenHash = ComputeTokenHash(rawToken);
        var request = await _repository.FindAsync(x =>
            x.SideAConsentTokenHash == tokenHash || x.SideBConsentTokenHash == tokenHash);
        if (request == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentTokenInvalid);
        }

        var side = request.SideConsentTokenHash(ChangeRequestSide.SideA) == tokenHash
            ? ChangeRequestSide.SideA
            : ChangeRequestSide.SideB;
        return new ChangeRequestConsentMatch(request, side);
    }

    /// <summary>
    /// Atomic: records the matched side's decision. If that side's token has expired it is
    /// defaulted to a No (Expired) and <c>ConsentExpired</c> is thrown so the caller can
    /// surface the expiry message + notify staff. The aggregate's concurrency stamp makes a
    /// double-click race resolve to a single decision (the loser sees
    /// <c>AbpDbConcurrencyException</c>).
    /// </summary>
    public virtual async Task<ChangeRequestConsentMatch> RecordDecisionAsync(
        string rawToken,
        bool approved,
        string? respondedByEmail)
    {
        var match = await ResolveByRawTokenAsync(rawToken);
        var nowUtc = _clock.Now.ToUniversalTime();

        if (match.Request.IsSideExpired(match.Side, nowUtc))
        {
            match.Request.MarkSideExpired(match.Side, nowUtc);
            await _repository.UpdateAsync(match.Request, autoSave: true);
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentExpired);
        }

        match.Request.RecordSideDecision(match.Side, approved, respondedByEmail, nowUtc);
        await _repository.UpdateAsync(match.Request, autoSave: true);
        return match;
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

/// <summary>A raw consent token resolved to its change request + the side that owns it.</summary>
public sealed record ChangeRequestConsentMatch(AppointmentChangeRequest Request, ChangeRequestSide Side);
