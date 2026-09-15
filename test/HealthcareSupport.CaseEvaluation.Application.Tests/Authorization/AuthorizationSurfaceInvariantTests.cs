using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Authorization;

/// <summary>
/// #707 layer 2 -- the structural invariant.
///
/// Layer 1 (<see cref="AuthorizationSurfaceSnapshotTests"/>) compares the surface against
/// an approved file, and a snapshot can always be regenerated carelessly to make a build
/// go green. This layer refuses the SHAPE regardless of what the snapshot says, so a
/// thoughtless regeneration still fails:
///
///   1. Every public app-service method must DECLARE something -- a permission, a bare
///      [Authorize], or an explicit [AllowAnonymous]. A method carrying none of the three
///      is not "public by design", it is a method nobody decided about.
///   2. Every [AllowAnonymous] must appear on the allow-list below WITH A REASON. That is
///      the list that actually matters: these are the endpoints reachable with no
///      credential at all.
///   3. The allow-list may not carry stale entries, so it cannot quietly stop covering a
///      method that was renamed.
/// </summary>
public sealed class AuthorizationSurfaceInvariantTests
{
    /// <summary>
    /// Every deliberately anonymous app-service method, and why it is allowed to be.
    ///
    /// Each reason below was read off the implementation, not assumed from the name. The
    /// recurring shape is that the caller has no session yet but does hold a
    /// single-purpose secret (a consent token, a verification code, a one-time download
    /// token) that the method itself validates.
    ///
    /// Adding an entry here is a security decision. It belongs in a PR that says so.
    /// </summary>
    private static readonly Dictionary<string, string> AnonymousAllowList = new(StringComparer.Ordinal)
    {
        // Public consent links mailed to an external party, who has no portal account.
        // Both resolve the raw token through ChangeRequestConsentManager first; an
        // unresolvable token yields nothing.
        ["HealthcareSupport.CaseEvaluation.AppointmentChangeRequests.PublicChangeRequestConsentAppService.GetConsentInfoAsync(String)"]
            = "public consent link; ResolveByRawTokenAsync validates the token",
        ["HealthcareSupport.CaseEvaluation.AppointmentChangeRequests.PublicChangeRequestConsentAppService.SubmitDecisionAsync(String, SubmitChangeRequestConsentDto)"]
            = "public consent link; ResolveByRawTokenAsync validates the token",

        // Anonymous document upload, gated on a per-document verification code plus
        // appointment state (DocumentUploadGate.EnsureVerificationCodeMatches and
        // EnsureAppointmentApprovedAndNotPastDueDate).
        ["HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentsAppService.UploadByVerificationCodeAsync(Guid, Guid, String, String, Int64, Stream)"]
            = "upload-by-code; DocumentUploadGate validates code and appointment state",

        // Rendered on the sign-in page, before anyone can possibly be authenticated.
        ["HealthcareSupport.CaseEvaluation.Branding.BrandingAppService.DownloadLogoAsync()"]
            = "login-page branding, read before sign-in",
        ["HealthcareSupport.CaseEvaluation.Branding.BrandingAppService.GetBrandingAsync()"]
            = "login-page branding, read before sign-in",

        // Account recovery: by definition the caller cannot authenticate yet.
        ["HealthcareSupport.CaseEvaluation.ExternalAccount.ExternalAccountAppService.ResendEmailVerificationAsync(ResendEmailVerificationInput)"]
            = "pre-authentication account recovery",
        ["HealthcareSupport.CaseEvaluation.ExternalAccount.ExternalAccountAppService.ResetPasswordAsync(ResetPasswordInput)"]
            = "pre-authentication account recovery",
        ["HealthcareSupport.CaseEvaluation.ExternalAccount.ExternalAccountAppService.SendPasswordResetCodeAsync(SendPasswordResetCodeInput)"]
            = "pre-authentication account recovery",

        // Dev-only demo helpers. These two are the sharpest edges on this list: one HARD
        // deletes IdentityUser rows and their master records, the other marks an email
        // confirmed. Neither is protected by authorization at all -- the only guard is
        // EnsureDevelopmentOnly, i.e. IHostEnvironment.IsDevelopment(). That holds because
        // docker-compose.prod.yml sets ASPNETCORE_ENVIRONMENT=Production on both the api
        // and authserver services, so they throw on the server. It is a guard made of one
        // environment variable, and it is worth knowing that is all it is.
        ["HealthcareSupport.CaseEvaluation.ExternalSignups.ExternalSignupAppService.DeleteTestUsersAsync(IList<String>)"]
            = "dev-only demo helper; EnsureDevelopmentOnly is the only guard",
        ["HealthcareSupport.CaseEvaluation.ExternalSignups.ExternalSignupAppService.MarkEmailConfirmedAsync(String)"]
            = "dev-only demo helper; EnsureDevelopmentOnly is the only guard",

        // Tenant resolution on the sign-in / sign-up screens, before a session exists.
        ["HealthcareSupport.CaseEvaluation.ExternalSignups.ExternalSignupAppService.GetTenantOptionsAsync(String)"]
            = "office picker shown before sign-in",
        ["HealthcareSupport.CaseEvaluation.ExternalSignups.ExternalSignupAppService.ResolveTenantByNameAsync(String)"]
            = "office resolution before sign-in",
        ["HealthcareSupport.CaseEvaluation.InternalUsers.InternalUsersAppService.GetTenantOptionsAsync(String)"]
            = "office picker shown before sign-in",

        // Self-registration and invite acceptance, both pre-account by definition.
        ["HealthcareSupport.CaseEvaluation.ExternalSignups.ExternalSignupAppService.RegisterAsync(ExternalUserSignUpDto)"]
            = "external self-registration",
        ["HealthcareSupport.CaseEvaluation.ExternalSignups.ExternalSignupAppService.ValidateInviteAsync(String)"]
            = "invite-token validation before an account exists",

        // ABP's standard Excel-download shape: a browser download cannot carry the
        // Authorization header, so the endpoint is anonymous and validates a one-time
        // DownloadToken from the cache, throwing AbpAuthorizationException otherwise.
        ["HealthcareSupport.CaseEvaluation.WcabOffices.WcabOfficesAppService.GetListAsExcelFileAsync(WcabOfficeExcelDownloadDto)"]
            = "ABP download-token pattern; the method validates input.DownloadToken",
    };

    /// <summary>
    /// A method with no [Authorize], no bare [Authorize] and no [AllowAnonymous] at either
    /// level. Currently empty, and it should stay that way: a genuine exception belongs
    /// here with a reason, never by loosening the assertion below.
    /// </summary>
    private static readonly HashSet<string> UnauthorizedAllowList = new(StringComparer.Ordinal);

    [Fact]
    public void Every_public_app_service_method_declares_some_authorization()
    {
        var offenders = Surface()
            .Where(m => AuthorizationSurface.HasNoDeclaredAuthorization(m.Method))
            .Select(m => m.Key)
            .Where(key => !UnauthorizedAllowList.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        offenders.ShouldBeEmpty(
            "These public application-service methods declare no authorization at all -- " +
            "not a permission, not a bare [Authorize], not even an explicit " +
            "[AllowAnonymous]. Decide which they are and say so in the attribute. If one " +
            "genuinely must be unauthorized, add it to UnauthorizedAllowList with a " +
            "reason rather than relaxing this test:\n  " +
            string.Join("\n  ", offenders));
    }

    [Fact]
    public void Every_anonymous_method_is_on_the_justified_allow_list()
    {
        var unjustified = Surface()
            .Where(m => AuthorizationSurface.MethodAuthorization(m.Method) == AuthorizationSurface.Anonymous
                     || AuthorizationSurface.ClassAuthorization(m.Method.DeclaringType!) == AuthorizationSurface.Anonymous)
            .Select(m => m.Key)
            .Where(key => !AnonymousAllowList.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        unjustified.ShouldBeEmpty(
            "These methods are reachable with no credential at all and are not on the " +
            "reviewed allow-list. Adding [AllowAnonymous] is a security decision; record " +
            "why in AnonymousAllowList in the same pull request:\n  " +
            string.Join("\n  ", unjustified));
    }

    /// <summary>
    /// Without this, renaming an anonymous method leaves its allow-list entry behind
    /// covering nothing, and the rename itself shows up as a NEW unjustified anonymous
    /// method -- two confusing failures instead of one clear one. It also stops the list
    /// growing into a graveyard nobody trusts.
    /// </summary>
    [Fact]
    public void Allow_list_carries_no_stale_entries()
    {
        var anonymous = Surface()
            .Where(m => AuthorizationSurface.MethodAuthorization(m.Method) == AuthorizationSurface.Anonymous
                     || AuthorizationSurface.ClassAuthorization(m.Method.DeclaringType!) == AuthorizationSurface.Anonymous)
            .Select(m => m.Key)
            .ToHashSet(StringComparer.Ordinal);

        var stale = AnonymousAllowList.Keys
            .Where(key => !anonymous.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        stale.ShouldBeEmpty(
            "These allow-list entries no longer match any anonymous method. If the method " +
            "was renamed, update the key; if [AllowAnonymous] was removed (good), delete " +
            "the entry:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>
    /// Guards the guards. If the reflection filter ever stopped matching app services,
    /// every test above would pass over an empty sequence and report nothing wrong --
    /// the same shape as a lint job reporting "0 file(s), 0 error(s)" while looking green.
    /// </summary>
    [Fact]
    public void Invariants_are_evaluated_against_a_non_trivial_surface()
    {
        Surface().Count.ShouldBeGreaterThan(200,
            "the app-service surface is known to be in the hundreds of methods; a much " +
            "smaller number means these invariants are checking almost nothing.");
    }

    private static IReadOnlyList<(string Key, System.Reflection.MethodInfo Method)> Surface()
    {
        var assembly = typeof(CaseEvaluationApplicationModule).Assembly;

        return AuthorizationSurface.Services(assembly)
            .SelectMany(service => AuthorizationSurface.Methods(service)
                .Select(method => (
                    Key: service.FullName + "." + AuthorizationSurface.Signature(method),
                    Method: method)))
            .ToList();
    }
}
