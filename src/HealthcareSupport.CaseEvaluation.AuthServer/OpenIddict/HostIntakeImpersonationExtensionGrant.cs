using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.HostOperators;
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
/// Phase D (2026-06-25) -- extends the stock <see cref="ImpersonationExtensionGrant"/>
/// so a HOST Intake operator can land as their LIMITED per-office shadow Intake
/// user. Replaces the stock "Impersonation" grant (registered in
/// <c>CaseEvaluationAuthServerModule</c>).
///
/// <para>Only <see cref="ImpersonateTenantAsync"/> is overridden -- the
/// host-context branch the SPA already drives (TenantId + TenantUserName, no
/// UserId). Routing of host/tenant/back-to is unchanged.</para>
///
/// <list type="bullet">
///   <item><b>Supervisor / IT Admin</b> (hold <c>Saas.Tenants.Impersonation</c>):
///         stock behavior -- switch into the office as its <c>admin</c>.</item>
///   <item><b>Host Intake operator</b> (hold <c>CaseEvaluation.IntakeImpersonation</c>,
///         NOT the broad SaaS permission): server-side, deny-by-default gate. The
///         operator may enter ONLY offices in their assignment set
///         (<see cref="IIntakeAssignmentChecker"/>), and the target is FORCED to
///         the operator's OWN shadow user (username == operator email) -- the
///         requested <c>TenantUserName</c> is ignored so they cannot request
///         <c>admin</c> or another user. The sign-in body mirrors stock
///         <c>ImpersonateTenantAsync</c> minus its broad-permission check, which
///         the intake operator deliberately does not satisfy.</item>
///   <item>Anything else: forbidden.</item>
/// </list>
///
/// The mechanism (host -> named limited user in a separate office DB, with the
/// office's tenant claim) was proven live before this was written: stock
/// <c>currentTenant.Change(tenantId)</c> + <c>FindByNameAsync</c> resolves the
/// user in the office's physical database.
/// </summary>
public class HostIntakeImpersonationExtensionGrant : ImpersonationExtensionGrant
{
    protected override async Task<IActionResult> ImpersonateTenantAsync(
        ExtensionGrantContext context,
        ClaimsPrincipal principal,
        Guid tenantId,
        string tenantUserName)
    {
        // Supervisor / IT Admin keep the stock switch-in-as-admin path (the stock
        // method enforces Saas.Tenants.Impersonation, which they hold).
        if (await permissionChecker.IsGrantedAsync(SaasHostPermissions.Tenants.Impersonation))
        {
            return await base.ImpersonateTenantAsync(context, principal, tenantId, tenantUserName);
        }

        // Host Intake operator: gated, lands ONLY as their own limited shadow user.
        if (await permissionChecker.IsGrantedAsync(CaseEvaluationPermissions.IntakeImpersonation.Default))
        {
            return await ImpersonateAssignedShadowUserAsync(context, principal, tenantId);
        }

        return Forbid(context, "You are not permitted to switch into offices.");
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

        // Deny-by-default office gate (server-side, the security boundary).
        var assignmentChecker = context.HttpContext.RequestServices
            .GetRequiredService<IIntakeAssignmentChecker>();
        if (!await assignmentChecker.IsAssignedAsync(operatorId.Value, officeId))
        {
            return Forbid(context, "You are not assigned to this office.");
        }

        // Land as the operator's OWN shadow user only -- the requested
        // TenantUserName is ignored. Sign-in body mirrors stock
        // ImpersonateTenantAsync (minus the broad-permission check above).
        using (currentTenant.Change(officeId))
        {
            var shadowUser = await userManager.FindByNameAsync(operatorEmail);
            if (shadowUser == null)
            {
                // Eager provisioning (on assignment) should have created it; a
                // missing shadow user means the assignment is stale.
                return Forbid(context, "No active intake access for this office.");
            }

            var claimsPrincipal = await userClaimsPrincipalFactory.CreateAsync(shadowUser);
            var extraClaims = new List<Claim>
            {
                new Claim(AbpClaimTypes.ImpersonatorUserId, operatorId.Value.ToString()),
                new Claim(AbpClaimTypes.ImpersonatorUserName, currentUser.UserName ?? operatorEmail),
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
