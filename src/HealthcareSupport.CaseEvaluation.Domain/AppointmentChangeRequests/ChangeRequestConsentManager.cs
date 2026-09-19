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
    private readonly IChangeRequestConsentRoundRepository _roundRepository;
    private readonly IClock _clock;

    public ChangeRequestConsentManager(
        IAppointmentChangeRequestRepository repository,
        IChangeRequestConsentRoundRepository roundRepository,
        IClock clock)
    {
        _repository = repository;
        _roundRepository = roundRepository;
        _clock = clock;
    }

    /// <summary>
    /// Issues consent for one side of a CANCELLATION request: generates a token, stores its
    /// hash + expiry on that side of the request, and returns the raw token (once) for the
    /// email link. The caller sets submitter metadata via
    /// <see cref="AppointmentChangeRequest.InitiateConsent"/> and persists the request in its
    /// own unit of work.
    ///
    /// <para>Reschedule consent does NOT come through here since phase 4c -- it is issued per
    /// <see cref="ChangeRequestConsentRound"/> by the overload below, because a reschedule can
    /// be re-proposed and this row can only ever be tokened once per side.</para>
    /// </summary>
    public virtual string IssueSideConsent(AppointmentChangeRequest request, ChangeRequestSide side)
    {
        Check.NotNull(request, nameof(request));
        var rawToken = GenerateRawToken();
        request.IssueSideConsent(side, ComputeTokenHash(rawToken), BuildExpiry());
        return rawToken;
    }

    /// <summary>
    /// Issues consent for one side of a RESCHEDULE consent round (phase 4c, 2026-08-05). The
    /// caller persists the round in its own unit of work.
    /// </summary>
    public virtual string IssueSideConsent(ChangeRequestConsentRound round, ChangeRequestSide side)
    {
        Check.NotNull(round, nameof(round));
        var rawToken = GenerateRawToken();
        round.IssueSideConsent(side, ComputeTokenHash(rawToken), BuildExpiry());
        return rawToken;
    }

    /// <summary>
    /// Re-issues consent for one still-Pending side of a round on a RESEND (phase 4c): mints a
    /// fresh token, replaces that side's stored hash, and restarts its 7-day window. The old
    /// token stops working -- unavoidable, because only its hash was ever persisted. The caller
    /// persists the round in its own unit of work.
    /// </summary>
    public virtual string ReissueSideConsent(ChangeRequestConsentRound round, ChangeRequestSide side)
    {
        Check.NotNull(round, nameof(round));
        var rawToken = GenerateRawToken();
        round.ReissueSideConsent(side, ComputeTokenHash(rawToken), BuildExpiry());
        return rawToken;
    }

    /// <summary>
    /// Non-mutating: resolves the change request, the consent ROUND that owns the token (null
    /// for cancellations), and which side a raw token points at, for the public landing page.
    /// Throws <c>ConsentTokenInvalid</c> when no match. A length guard rejects obvious fuzzing
    /// before a DB roundtrip.
    ///
    /// <para>Two stores are searched because consent lives in two places by design: reschedule
    /// consent on <see cref="ChangeRequestConsentRound"/> rows (phase 4c), cancellation consent
    /// on the request's own flat columns. The parent lookup ALSO keeps pre-4c reschedule tokens
    /// -- already emailed and possibly still in someone's inbox -- resolvable.</para>
    /// </summary>
    public virtual async Task<ChangeRequestConsentMatch> ResolveByRawTokenAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)
            || rawToken.Length > AppointmentChangeRequestConsts.ConsentEncodedTokenMaxLength)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentTokenInvalid);
        }

        var tokenHash = ComputeTokenHash(rawToken);

        var round = await _roundRepository.FindByTokenHashAsync(tokenHash);
        if (round != null)
        {
            var request = await _repository.FindAsync(round.AppointmentChangeRequestId);
            if (request == null)
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentTokenInvalid);
            }
            return new ChangeRequestConsentMatch(request, round, SideOwning(round, tokenHash));
        }

        var parentRequest = await _repository.FindAsync(x =>
            x.SideAConsentTokenHash == tokenHash || x.SideBConsentTokenHash == tokenHash);
        if (parentRequest == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentTokenInvalid);
        }

        var parentSide = parentRequest.SideConsentTokenHash(ChangeRequestSide.SideA) == tokenHash
            ? ChangeRequestSide.SideA
            : ChangeRequestSide.SideB;
        return new ChangeRequestConsentMatch(parentRequest, Round: null, parentSide);
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

        // Reschedule consent lives on the round; cancellation consent on the request itself.
        // Whichever store the token resolved from is the one the decision is written back to.
        if (match.Round != null)
        {
            if (match.Round.IsSideExpired(match.Side, nowUtc))
            {
                match.Round.MarkSideExpired(match.Side, nowUtc);
                await _roundRepository.UpdateAsync(match.Round, autoSave: true);
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentExpired);
            }

            match.Round.RecordSideDecision(match.Side, approved, respondedByEmail, nowUtc);
            await _roundRepository.UpdateAsync(match.Round, autoSave: true);
            return match;
        }

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

    private DateTime BuildExpiry() =>
        _clock.Now.ToUniversalTime().AddDays(AppointmentChangeRequestConsts.ConsentDefaultTtlDays);

    private static ChangeRequestSide SideOwning(ChangeRequestConsentRound round, string tokenHash) =>
        round.SideConsentTokenHash(ChangeRequestSide.SideA) == tokenHash
            ? ChangeRequestSide.SideA
            : ChangeRequestSide.SideB;

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

/// <summary>
/// A raw consent token resolved to its change request, the consent ROUND that owns it, and the
/// side that owns it. <paramref name="Round"/> is null for a CANCELLATION (whose consent lives
/// on the request's own columns) and for any pre-4c reschedule token.
/// </summary>
public sealed record ChangeRequestConsentMatch(
    AppointmentChangeRequest Request,
    ChangeRequestConsentRound? Round,
    ChangeRequestSide Side);
