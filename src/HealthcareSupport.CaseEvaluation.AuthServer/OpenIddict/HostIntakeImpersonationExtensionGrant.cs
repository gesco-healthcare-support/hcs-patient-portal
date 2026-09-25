using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Volo.Abp.Account.Web.ExtensionGrants;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict.ExtensionGrantTypes;
using Volo.Abp.Security.Claims;
using Volo.Saas.Host;

namespace HealthcareSupport.CaseEvaluation.OpenIddict;

/// <summary>
/// Extends the stock <see cref="ImpersonationExtensionGrant"/> so EVERY host operator who
/// switches into an office lands as their OWN per-office shadow user (username == operator
/// email) -- never the single shared office <c>admin</c> account. Replaces the stock
/// "Impersonation" grant (registered in <c>CaseEvaluationAuthServerModule</c>).
///
/// <para>Only <see cref="ImpersonateTenantAsync"/> is overridden. The requested
/// <c>TenantUserName</c> is ignored for every tier -- the grant is the sole authority on the
/// identity + role the operator lands as.</para>
///
/// <list type="bullet">
///   <item><b>Supervisor / IT Admin</b> (hold <c>Saas.Tenants.Impersonation</c>,
///         task_2e8e4dc2 2026-07-21): land as their own shadow holding the tenant role for
///         their host tier -- IT Admin -> the tenant <c>admin</c> role (full office powers),
///         Staff Supervisor -> the per-office Staff Supervisor role. The shadow is provisioned
///         LAZILY here (no assignment step -- <c>Saas.Tenants</c> authorizes every office), so
///         N operators get N distinct shadows (per-person audit, no shared-account collision).</item>
///   <item><b>Host Intake operator</b> (hold <c>CaseEvaluation.IntakeImpersonation</c>,
///         NOT the broad SaaS permission): server-side, deny-by-default assignment gate
///         (<see cref="IIntakeAssignmentChecker"/>); lands as their own Intake Staff shadow
///         (provisioned eagerly on assignment).</item>
///   <item>Anything else: forbidden.</item>
/// </list>
///
/// The mechanism (host -> named per-office user in a separate office DB, with the office's
/// tenant claim) is proven: <c>currentTenant.Change(officeId)</c> + <c>FindByNameAsync</c>
/// resolves the shadow in the office's physical database.
/// </summary>
public class HostIntakeImpersonationExtensionGrant : ImpersonationExtensionGrant
{
    // The Volo SaaS / ABP static per-tenant administrator role name. An IT-Admin's per-office
    // shadow holds this ROLE (full office powers) -- the point of task_2e8e4dc2 is that the
    // tenant admin is a role, not one shared account everyone impersonates into.
    private const string TenantAdminRoleName = "admin";

    protected override async Task<IActionResult> ImpersonateTenantAsync(
        ExtensionGrantContext context,
        ClaimsPrincipal principal,
        Guid tenantId,
        string tenantUserName)
    {
        // Supervisor / IT Admin: land as their OWN per-office shadow (task_2e8e4dc2), holding
        // the tenant role for their host tier -- NOT the shared office admin account.
        if (await permissionChecker.IsGrantedAsync(SaasHostPermissions.Tenants.Impersonation))
        {
            return await ImpersonateAsOwnShadowAsync(context, principal, tenantId);
        }

        // Host Intake operator: gated, lands ONLY as their own limited shadow user.
        if (await permissionChecker.IsGrantedAsync(CaseEvaluationPermissions.IntakeImpersonation.Default))
        {
            return await ImpersonateAssignedShadowUserAsync(context, principal, tenantId);
        }

        return Forbid(context, "You are not permitted to switch into offices.");
    }

    /// <summary>
    /// task_2e8e4dc2 -- Supervisor / IT-Admin switch-in. Each operator lands as their OWN
    /// per-office shadow user holding the tenant role for their host tier: IT Admin -> the
    /// tenant <c>admin</c> role (full office powers), Staff Supervisor -> the per-office Staff
    /// Supervisor role. The shadow is provisioned LAZILY here -- these tiers reach every office
    /// via <c>Saas.Tenants</c>, so there is no assignment step and new offices work automatically.
    /// No assignment gate (the broad SaaS permission is the authorization).
    /// </summary>
    private async Task<IActionResult> ImpersonateAsOwnShadowAsync(
        ExtensionGrantContext context,
        ClaimsPrincipal principal,
        Guid officeId)
    {
        var operatorId = currentUser.Id;
        var operatorEmail = currentUser.Email;
        if (operatorId == null || string.IsNullOrWhiteSpace(operatorEmail))
        {
            return Forbid(context, "Operator identity could not be resolved.");
        }

        // Map host tier -> per-office role (resolved at host scope, before entering the office).
        // IT-Admin (technical platform admin) lands with the tenant admin role; any other holder
        // of Saas.Tenants.Impersonation is a Staff Supervisor.
        var operatorUser = await userManager.GetByIdAsync(operatorId.Value);
        var tenantRole = await userManager.IsInRoleAsync(operatorUser, InternalUserRoleDataSeedContributor.ItAdminRoleName)
            ? TenantAdminRoleName
            : InternalUserRoleDataSeedContributor.StaffSupervisorRoleName;

        // Lazily provision-or-find the operator's own shadow in the office DB.
        var shadowProvisioner = context.HttpContext.RequestServices
            .GetRequiredService<IIntakeShadowUserProvisioner>();
        await shadowProvisioner.EnsureShadowUserAsync(officeId, operatorId.Value, tenantRole);

        return await SignInAsShadowUserAsync(
            context, principal, officeId, operatorId.Value, operatorEmail,
            currentUser.UserName ?? operatorEmail,
            missingShadowError: "Could not provision office access. Please try again.");
    }

    private async Task<IActionResult> ImpersonateAssignedShadowUserAsync(
        ExtensionGrantContext context,
        ClaimsPrincipal principal,
        Guid officeId)
    {
        var operatorId = currentUser.Id;
        var operatorEmail = currentUser.Email;
        if (operatorId == null || string.IsNullOrWhiteSpace(operatorEmail))
        {
            return Forbid(context, "Operator identity could not be resolved.");
        }

        // Deny-by-default office gate (server-side, the security boundary for Intake).
        var assignmentChecker = context.HttpContext.RequestServices
            .GetRequiredService<IIntakeAssignmentChecker>();
        if (!await assignmentChecker.IsAssignedAsync(operatorId.Value, officeId))
        {
            return Forbid(context, "You are not assigned to this office.");
        }

        return await SignInAsShadowUserAsync(
            context, principal, officeId, operatorId.Value, operatorEmail,
            currentUser.UserName ?? operatorEmail,
            missingShadowError: "No active intake access for this office.");
    }

    /// <summary>
    /// Signs the operator in AS their per-office shadow user (username == operator email),
    /// carrying the ImpersonatorUserId/UserName claims so the shell shows who they really are.
    /// The shadow must already exist in the office DB (Intake provisions on assignment;
    /// Supervisor/IT-Admin provision just before calling this). The requested TenantUserName is
    /// ignored. Body mirrors stock ImpersonateTenantAsync minus the broad-permission check.
    /// </summary>
    private async Task<IActionResult> SignInAsShadowUserAsync(
        ExtensionGrantContext context,
        ClaimsPrincipal principal,
        Guid officeId,
        Guid operatorId,
        string operatorEmail,
        string operatorUserName,
        string missingShadowError)
    {
        using (currentTenant.Change(officeId))
        {
            var shadowUser = await userManager.FindByNameAsync(operatorEmail);
            if (shadowUser == null)
            {
                return Forbid(context, missingShadowError);
            }

            var claimsPrincipal = await userClaimsPrincipalFactory.CreateAsync(shadowUser);
            var extraClaims = new List<Claim>
            {
                new Claim(AbpClaimTypes.ImpersonatorUserId, operatorId.ToString()),
                new Claim(AbpClaimTypes.ImpersonatorUserName, operatorUserName),
            };
            var rememberMe = principal.Claims.FirstOrDefault(x => x.Type == AbpClaimTypes.RememberMe);
            if (rememberMe != null)
            {
                extraClaims.Add(rememberMe);
            }
            claimsPrincipal.Identities.First().AddClaims(extraClaims);

            using (currentPrincipalAccessor.Change(claimsPrincipal))
            {
                await identitySecurityLogManager.SaveAsync(new IdentitySecurityLogContext
                {
                    Identity = IdentitySecurityLogIdentityConsts.Identity,
                    Action = "ImpersonateUser",
                });
            }

            await CreateSessionAsync(context, claimsPrincipal);
            claimsPrincipal.SetScopes(principal.GetScopes());
            claimsPrincipal.SetResources(await GetResourcesAsync(context, principal.GetScopes()));
            await SetClaimsDestinationsAsync(context, claimsPrincipal);
            await RevokeSessionAsync(context, principal);

            return new SignInResult(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, claimsPrincipal);
        }
    }

    private static ForbidResult Forbid(ExtensionGrantContext context, string description)
    {
        return new ForbidResult(
            new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme },
            new AuthenticationProperties(
                // Keys match the stock ImpersonationExtensionGrant forbid shape
                // (OpenIddict reads ".error"/".error_description" from the
                // AuthenticationProperties on a ForbidResult at the token endpoint).
                new Dictionary<string, string?>
                {
                    [".error"] = OpenIddictConstants.Errors.InvalidRequest,
                    [".error_description"] = description,
                },
                new Dictionary<string, object?>
                {
                    ["grant_type"] = context.Request.GrantType,
                }));
    }
}
