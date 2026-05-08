using System;
using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace HealthcareSupport.CaseEvaluation.Security;

[Dependency(ReplaceServices = true)]
public class FakeCurrentPrincipalAccessor : ThreadCurrentPrincipalAccessor
{
    protected override ClaimsPrincipal GetClaimsPrincipal()
    {
        return GetPrincipal();
    }

    private static ClaimsPrincipal GetPrincipal()
    {
        // Phase 13 (G6 follow-up, 2026-05-05): the role claim mirrors OLD's
        // "admin sees everything" semantics. AppointmentsAppService.EnsureCanRead*
        // and ChangeRequest's matching checks short-circuit when the caller holds
        // an internal role (admin / Clinic Staff / Staff Supervisor / IT Admin /
        // Doctor per BookingFlowRoles.InternalUserRoles). Without the claim, every
        // integration test that hits Get/GetWithNavigationProperties via the fake
        // principal trips the per-row access gate. Tests asserting on non-internal
        // callers override this via WithCurrentUser.Run(...).
        return new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
        {
            new Claim(AbpClaimTypes.UserId, "2e701e62-0953-4dd3-910b-dc6cc93ccb0d"),
            new Claim(AbpClaimTypes.UserName, "admin"),
            new Claim(AbpClaimTypes.Email, "admin@abp.io"),
            new Claim(AbpClaimTypes.Role, "admin")
        }));
    }
}
