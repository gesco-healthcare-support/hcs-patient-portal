[Home](../../INDEX.md) > [Issues](../) > Research > SEC-03

# SEC-03: External User Lookup Unauthenticated -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` lines 62-131
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs`

---

## Current state (verified 2026-04-17)

```csharp
public virtual async Task<ListResultDto<ExternalUserLookupDto>> GetExternalUserLookupAsync(string? filter = null)
{
    var allowedRoleNames = new[] { "Patient", "Applicant Attorney", "Defense Attorney" };
    ...
}
```

Facts:

- No `[Authorize]` at method level.
- No class-level `[Authorize]` on `ExternalSignupAppService`.
- Returns `FirstName`, `LastName`, `Email`, `UserRole` for every user in the three roles above (ClaimExaminer is hardcoded out, see FEAT-02).
- Excludes the current user but not ClaimExaminer.
- No `MaxResultCount` / `PagedAndSortedResultRequestDto` cap -- a single call returns every matching user.
- Signature has **drifted** from what `docs/issues/SECURITY.md` describes: the method no longer takes `ExternalUserLookupRequestDto input`; it takes `string? filter = null` directly. Update the source doc when fixing.

Deprecated CLAUDE.md note in `src/.../Application/ExternalSignups/CLAUDE.md` already flags this as Gotcha #6.

---

## Official documentation

- [ABP Authorization](https://abp.io/docs/latest/framework/fundamentals/authorization) -- application service authorization follows ASP.NET Core MVC semantics; no implicit default "require authenticated user" at the AppService layer.
- [ABP Auto API Controllers](https://docs.abp.io/en/abp/6.0/API/Auto-API-Controllers) -- dynamic controllers inherit AppService attributes; an AppService without `[Authorize]` yields an unauthenticated endpoint.
- [abpframework/abp #3682](https://github.com/abpframework/abp/issues/3682) -- confirms no framework-level "all AppServices require auth" default.
- [ABP Data Transfer Objects](https://abp.io/docs/latest/Data-Transfer-Objects) -- `LimitedResultRequestDto.MaxResultCount` default 10; validator caps at 1000; override via `LimitedResultRequestDto.DefaultMaxResultCount`.
- [PagedAndSortedResultRequestDto API](https://abp.io/docs/api/9.0/Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto.html) -- canonical paged-list shape.
- [OWASP API Security Top 10 (2023)](https://owasp.org/API-Security/editions/2023/en/0x11-t10/) -- this endpoint maps to **API3:2023 Broken Object Property Level Authorization** (excessive property exposure -- Email) and **API5:2023 Broken Function Level Authorization** (function gated only by "authenticated").

## Community findings

- [ABP Support #4484 -- Allow guest user to use certain app services](https://abp.io/support/questions/4484/Allow-Guest-user-to-use-certain-application-services-served-by-a-guest-page) -- idiomatic pattern: class-level `[Authorize]`, `[AllowAnonymous]` only on specific signup methods.
- [abpframework/abp #2262 -- Authorize attribute not working in custom AppService](https://github.com/abpframework/abp/issues/2262) -- `[Authorize]` on interface vs class behaviour; method-level wins, class-level applies to all methods.
- [aspnetboilerplate/aspnetboilerplate #6745 -- AbpAllowAnonymous not working on controller](https://github.com/aspnetboilerplate/aspnetboilerplate/issues/6745) -- explicit attributes required; default is ambiguous.

## Recommended approach

1. Add class-level `[Authorize]` to `ExternalSignupAppService`; keep `[AllowAnonymous]` only on `RegisterAsync` and `GetTenantOptionsAsync` (the legit public signup surface).
2. Introduce a new permission: `CaseEvaluationPermissions.Appointments.LookupUsers` (or `ExternalSignups.Lookup`). Register in `CaseEvaluationPermissionDefinitionProvider`. Apply via `[Authorize(CaseEvaluationPermissions.Appointments.LookupUsers)]`. Grant only to intake-staff / tenant-admin roles.
3. Accept a `PagedAndSortedResultRequestDto`-derived input. Cap `MaxResultCount` at 20. Require minimum filter length (e.g. 3 chars) server-side.
4. Reshape the response: return `IdentityUserId` + display name only. Remove `Email` from the lookup DTO. Hydrate full details via a second permissioned call.

## Gotchas / blockers

- Both the auto dynamic controller (`/api/app/...`) and the manual `ExternalSignupController` expose the method. `[Authorize]` on the AppService covers both; `[AllowAnonymous]` on the controller alone is NOT enough if the AppService has no attribute -- the auto route still serves the endpoint.
- Setting a global fallback auth policy in `AddAuthorization` (`FallbackPolicy = ...`) affects the entire host including health endpoints and OpenIddict endpoints -- don't blanket-apply.
- Angular proxy regeneration: after changing DTO shape and adding a permission, regenerate proxies with `abp generate-proxy` so the UI sees the new contract and permission name for conditional rendering.

## Open questions

- Why is `ClaimExaminer` excluded from `allowedRoleNames`? Intentional to prevent examiner-identity enumeration, or oversight? See [FEAT-02](FEAT-02.md).
- Does any self-signup UI flow genuinely require unauthenticated lookup access? If yes, pair permission-gating with a throttled lookup returning far fewer fields.
- Should the email ever be returned? If never, remove from the DTO entirely. If sometimes (e.g. tenant admin), gate that with a second permission.

## Related

- [FEAT-02](FEAT-02.md) -- ClaimExaminer exclusion from this same method
- [docs/issues/SECURITY.md#sec-03](../SECURITY.md#sec-03-external-user-lookup-endpoint-unauthenticated-and-unprotected)
