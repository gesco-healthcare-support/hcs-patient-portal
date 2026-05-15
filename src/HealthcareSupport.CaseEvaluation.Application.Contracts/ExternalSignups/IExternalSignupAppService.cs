using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

public interface IExternalSignupAppService : IApplicationService
{
    Task<ListResultDto<LookupDto<Guid>>> GetTenantOptionsAsync(string? filter = null);
    Task<ListResultDto<ExternalUserLookupDto>> GetExternalUserLookupAsync(string? filter = null);
    Task<ExternalUserProfileDto> GetMyProfileAsync();

    Task RegisterAsync(ExternalUserSignUpDto input);

    /// <summary>
    /// 1.6 (2026-04-30): host-context tenant lookup by name; returns null on
    /// miss. Used by the AuthServer JS overlay to resolve `?__tenant=&lt;Name&gt;`
    /// invite-link query strings to the GUID required by the registration
    /// payload.
    /// </summary>
    Task<LookupDto<Guid>?> ResolveTenantByNameAsync(string name);

    /// <summary>
    /// 2026-05-15 (revised) -- admin-side external-user invite. Generates
    /// a 32-byte cryptographic random token, stores its SHA256 hash in
    /// the <c>Invitation</c> table with a 7-day TTL, dispatches the
    /// <c>InviteExternalUser</c> notification template, and returns the
    /// constructed URL (including the raw token) so the admin can also
    /// copy + paste it manually.
    /// AppService gated by
    /// <c>CaseEvaluation.UserManagement.InviteExternalUser</c> permission;
    /// granted to IT Admin + Staff Supervisor + Clinic Staff.
    /// </summary>
    Task<InviteExternalUserResultDto> InviteExternalUserAsync(InviteExternalUserDto input);

    /// <summary>
    /// 2026-05-15 -- anonymous endpoint that validates a raw invite
    /// token against the persisted <c>Invitation</c> row and returns the
    /// resolved email + role for the JS overlay on
    /// <c>/Account/Register</c> to prefill (and lock) the form fields.
    /// Throws <c>BusinessException(InviteInvalid | InviteExpired |
    /// InviteAlreadyAccepted)</c> on validation failure so the overlay
    /// can render the appropriate friendly banner.
    /// </summary>
    Task<InvitationValidationDto> ValidateInviteAsync(string token);

    /// <summary>
    /// Dev-only test helper: flip <c>EmailConfirmed=true</c> on the user
    /// matching <paramref name="email"/> across all tenants. Lets demo
    /// testing skip the inbox round-trip when verifying flows that depend
    /// on the email-confirm gate. Throws when not running in Development.
    /// </summary>
    Task MarkEmailConfirmedAsync(string email);

    /// <summary>
    /// Dev-only test helper: delete IdentityUser rows (and dependent
    /// Patient/ApplicantAttorney/DefenseAttorney profiles) for the given
    /// emails, across all tenants. Allows the demo register flow to be
    /// re-run repeatedly with the same email addresses. Throws when not
    /// running in Development.
    /// </summary>
    Task<DeleteTestUsersResultDto> DeleteTestUsersAsync(IList<string> emails);
}
