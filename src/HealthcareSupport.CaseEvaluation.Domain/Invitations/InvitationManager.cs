using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

// Permission-error-code constants are pulled in via the global
// `using static HealthcareSupport.CaseEvaluation.CaseEvaluationDomainErrorCodes;`
// alias kept simple; the explicit qualifier below avoids ambiguity when other
// `*ErrorCodes` classes ship for other features.

namespace HealthcareSupport.CaseEvaluation.Invitations;

/// <summary>
/// Domain service for issuing, validating, and accepting
/// <see cref="Invitation"/> rows. Encapsulates the token generation
/// (cryptographic randomness), hashing (SHA256), and TTL math so the
/// AppService and tests can interact with the invitation lifecycle
/// without re-implementing the crypto primitives.
///
/// <para>Three operations:</para>
/// <list type="bullet">
///   <item><see cref="IssueAsync"/> -- generate token, hash it, persist
///         the row, return the raw token (one-time only).</item>
///   <item><see cref="ValidateAsync"/> -- non-mutating: hash the raw
///         token, look up the row, return the entity (or throw a
///         <c>BusinessException</c> with the right error code).</item>
///   <item><see cref="AcceptAsync"/> -- atomic: validate + mark
///         accepted. The aggregate root's concurrency stamp wins races
///         between simultaneous accepts; the AppService catches the
///         <c>AbpDbConcurrencyException</c> and surfaces
///         <c>InviteAlreadyAccepted</c>.</item>
/// </list>
/// </summary>
public class InvitationManager : DomainService
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly IClock _clock;

    public InvitationManager(
        IInvitationRepository invitationRepository,
        IClock clock)
    {
        _invitationRepository = invitationRepository;
        _clock = clock;
    }

    /// <summary>
    /// Issues a new invitation for <paramref name="email"/> +
    /// <paramref name="userType"/>. Returns the raw token (Base64-URL
    /// encoded, ~43 chars) to the caller and persists the SHA256 hash
    /// in the database. The raw token must be returned to the user
    /// through the invite URL immediately; it cannot be retrieved later.
    /// </summary>
    /// <param name="tenantId">Tenant the invitation scopes to. Required.</param>
    /// <param name="email">Recipient email; stored verbatim (lowercased by caller).</param>
    /// <param name="userType">External role the invitation grants.</param>
    /// <param name="invitedByUserId">Internal staff user issuing the invite.</param>
    /// <returns>Tuple of (the persisted Invitation, the raw token).</returns>
    public virtual async Task<(Invitation Invitation, string RawToken)> IssueAsync(
        Guid tenantId,
        string email,
        ExternalUserType userType,
        Guid invitedByUserId)
    {
        Check.NotNullOrWhiteSpace(email, nameof(email));

        var rawToken = GenerateRawToken();
        var tokenHash = ComputeTokenHash(rawToken);
        var nowUtc = _clock.Now.ToUniversalTime();
        var expiresAt = nowUtc.AddDays(InvitationConsts.DefaultTtlDays);

        var invitation = new Invitation(
            id: GuidGenerator.Create(),
            tenantId: tenantId,
            email: email,
            userType: userType,
            tokenHash: tokenHash,
            expiresAt: expiresAt,
            invitedByUserId: invitedByUserId);

        await _invitationRepository.InsertAsync(invitation, autoSave: true);
        return (invitation, rawToken);
    }

    /// <summary>
    /// Non-mutating validation: hashes <paramref name="rawToken"/>,
    /// finds the invitation row, and confirms it is in the
    /// <c>Active</c> state. Throws a <c>BusinessException</c> with one
    /// of the invitation error codes when validation fails. Used by
    /// both the public validate-invite endpoint (for the JS overlay
    /// prefill) and the accept path (as the first step inside the
    /// transaction).
    /// </summary>
    public virtual async Task<Invitation> ValidateAsync(string rawToken)
    {
        var invitation = await ResolveAsync(rawToken);
        var nowUtc = _clock.Now.ToUniversalTime();

        if (invitation.AcceptedAt.HasValue)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.InviteAlreadyAccepted);
        }
        if (invitation.ExpiresAt <= nowUtc)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.InviteExpired);
        }

        return invitation;
    }

    /// <summary>
    /// Atomic: validates the token and marks the invitation accepted by
    /// <paramref name="acceptedByUserId"/>. The aggregate's
    /// <c>ConcurrencyStamp</c> ensures a race between two simultaneous
    /// accept attempts results in <c>AbpDbConcurrencyException</c> for
    /// the second writer; the AppService translates that to
    /// <c>InviteAlreadyAccepted</c> for the user-facing message.
    /// </summary>
    public virtual async Task<Invitation> AcceptAsync(
        string rawToken,
        Guid acceptedByUserId)
    {
        var invitation = await ValidateAsync(rawToken);
        invitation.MarkAccepted(acceptedByUserId, _clock.Now.ToUniversalTime());
        await _invitationRepository.UpdateAsync(invitation, autoSave: true);
        return invitation;
    }

    /// <summary>
    /// Hashes the input token and returns the matching Invitation row,
    /// or throws <c>InviteInvalid</c> when no row exists. Defensive
    /// length check rejects obviously malformed inputs (random URL
    /// fuzzing) without a DB roundtrip.
    /// </summary>
    private async Task<Invitation> ResolveAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)
            || rawToken.Length > InvitationConsts.EncodedTokenMaxLength)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.InviteInvalid);
        }

        var tokenHash = ComputeTokenHash(rawToken);
        var invitation = await _invitationRepository.FindByTokenHashAsync(tokenHash);
        if (invitation == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.InviteInvalid);
        }
        return invitation;
    }

    /// <summary>
    /// 32 cryptographic random bytes encoded as URL-safe Base64 without
    /// padding. Length ~= 43 chars. Internal so unit tests can assert
    /// token format without re-implementing the encoding.
    /// </summary>
    internal static string GenerateRawToken()
    {
        Span<byte> buffer = stackalloc byte[InvitationConsts.TokenByteLength];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// SHA256 of the UTF8-encoded raw token, returned as lowercase hex
    /// (64 chars). Internal so the same hash function is reachable
    /// from unit tests + the AppService when verifying token equality.
    /// </summary>
    internal static string ComputeTokenHash(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hashed = SHA256.HashData(bytes);
        var sb = new StringBuilder(InvitationConsts.TokenHashLength);
        foreach (var b in hashed)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
