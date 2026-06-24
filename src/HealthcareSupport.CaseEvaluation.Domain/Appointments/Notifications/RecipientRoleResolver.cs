using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications;

/// <summary>
/// Default <see cref="IRecipientRoleResolver"/> implementation. Looks the
/// email up via <see cref="IdentityUserManager.FindByEmailAsync"/> -- which
/// observes ABP's automatic <c>IMultiTenant</c> filter, so the lookup is
/// scoped to the current tenant -- then verifies role membership via
/// <see cref="UserManager{TUser}.IsInRoleAsync"/>. The role-name strings
/// match the seeded <c>AbpRoles.Name</c> values exactly: <c>Patient</c>,
/// <c>Applicant Attorney</c>, <c>Defense Attorney</c>, <c>Claim Examiner</c>.
///
/// <para>OfficeAdmin recipients are not IdentityUsers (they are mailbox
/// addresses sourced from the per-tenant <c>OfficeEmail</c> setting). For
/// that role the classifier short-circuits to "registered + matches" so
/// the handler keeps its OfficeAdmin branch unconditional.</para>
///
/// <para>Empty / null email returns the "not registered" sentinel without
/// hitting the database -- matches the rest of the resolver's empty-email
/// silent-skip pattern.</para>
/// </summary>
public class RecipientRoleResolver : IRecipientRoleResolver, ITransientDependency
{
    private readonly IdentityUserManager _userManager;
    private readonly ILogger<RecipientRoleResolver> _logger;

    public RecipientRoleResolver(
        IdentityUserManager userManager,
        ILogger<RecipientRoleResolver> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<RecipientRoleClassification> ClassifyAsync(string email, RecipientRole expectedRole)
    {
        var notRegistered = new RecipientRoleClassification(
            IsRegistered: false, MatchesRole: false, UserId: null);

        if (string.IsNullOrWhiteSpace(email))
        {
            return notRegistered;
        }

        // OfficeAdmin: tenant mailbox setting, not an IdentityUser. Bypass
        // the role check so the handler's OfficeAdmin branch fires
        // regardless of whether anyone happens to be registered under that
        // address.
        if (expectedRole == RecipientRole.OfficeAdmin)
        {
            return new RecipientRoleClassification(
                IsRegistered: true, MatchesRole: true, UserId: null);
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null)
        {
            return notRegistered;
        }

        var expectedRoleName = MapToRoleName(expectedRole);
        if (expectedRoleName == null)
        {
            // Unmapped role enum value (e.g., a future addition without a
            // matching IdentityRole). Surface as off-role so the handler
            // takes the Register CTA path; future enhancement: throw to
            // catch the missing mapping at deploy time instead.
            _logger.LogWarning(
                "RecipientRoleResolver: no role-name mapping for RecipientRole.{Role}; " +
                "treating {Email} as off-role until mapping is added.",
                expectedRole, email);
            return new RecipientRoleClassification(
                IsRegistered: false, MatchesRole: false, UserId: user.Id);
        }

        var matchesRole = await _userManager.IsInRoleAsync(user, expectedRoleName);
        if (!matchesRole)
        {
            // Off-role conflict. Per Option A we treat as not registered so
            // the handler renders the Register CTA. Logged at Information
            // because this is the fix-target Adrian flagged as
            // "Defense Attorney received the patient's email and ended up
            // on a Patient dashboard with no DA binding."
            _logger.LogInformation(
                "RecipientRoleResolver: {Email} resolves to user {UserId} but does not " +
                "hold role '{ExpectedRole}'; routing as not-registered for Option A.",
                email, user.Id, expectedRoleName);
        }

        return new RecipientRoleClassification(
            IsRegistered: matchesRole,
            MatchesRole: matchesRole,
            UserId: user.Id);
    }

    /// <summary>
    /// Maps the typed <see cref="RecipientRole"/> enum to the seeded
    /// <c>AbpRoles.Name</c> string verified in the live DB. Internal so
    /// unit tests can lock the mapping.
    /// </summary>
    internal static string? MapToRoleName(RecipientRole role) => role switch
    {
        RecipientRole.Patient => "Patient",
        RecipientRole.ApplicantAttorney => "Applicant Attorney",
        RecipientRole.DefenseAttorney => "Defense Attorney",
        RecipientRole.ClaimExaminer => "Claim Examiner",
        _ => null,
    };
}
