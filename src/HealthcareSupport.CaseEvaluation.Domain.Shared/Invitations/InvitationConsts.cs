namespace HealthcareSupport.CaseEvaluation.Invitations;

/// <summary>
/// Compile-time constants for the external-user invitation feature.
/// Shared between Domain (entity construction, manager) and EF Core
/// (column lengths) so length budgets stay in sync.
/// </summary>
public static class InvitationConsts
{
    /// <summary>
    /// Number of random bytes used to generate an invite token. 32 bytes
    /// = 256 bits of entropy. Encoded URL-safe Base64 produces a 43-char
    /// token string. Forging by brute force is infeasible (2^256 search
    /// space) so a single-stage attacker cannot enumerate active invites.
    /// </summary>
    public const int TokenByteLength = 32;

    /// <summary>
    /// Encoded raw token length (URL-safe Base64 of 32 bytes, no padding).
    /// Used to defensively reject obviously malformed tokens before
    /// hashing + DB lookup -- saves a query in the common attack case
    /// (random URL fuzzing).
    /// </summary>
    public const int EncodedTokenMaxLength = 64;

    /// <summary>
    /// Token-hash storage column length. SHA256 hex = 64 chars; we store
    /// hex (not base64) so the value is case-insensitive and grep-able
    /// in raw DB inspection without ambiguity. The column is
    /// <c>UNIQUE</c> so two active invitations cannot collide on hash.
    /// </summary>
    public const int TokenHashLength = 64;

    /// <summary>
    /// Default invite lifetime. 7 days matches Adrian's lock 2026-05-15
    /// and the slow-loop test plan in
    /// <c>docs/plans/2026-05-15-invite-external-user.md</c>.
    /// </summary>
    public const int DefaultTtlDays = 7;

    /// <summary>
    /// Maximum email-address column length on the Invitation row. Mirrors
    /// the value enforced on <c>InviteExternalUserDto.Email</c>.
    /// </summary>
    public const int EmailMaxLength = 256;
}
