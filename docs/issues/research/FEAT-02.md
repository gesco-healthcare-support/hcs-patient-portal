[Home](../../INDEX.md) > [Issues](../) > Research > FEAT-02

# FEAT-02: Claim Examiner Role Has No UI or Workflow -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/ExternalUserType.cs`
- `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` lines 64-69, 239-248
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs`
- `angular/src/app/home/home.component.ts`

---

## Current state (verified 2026-04-17)

- `ExternalUserType.ClaimExaminer = 2` exists.
- `ToRoleName` maps ClaimExaminer -> `"Claim Examiner"`.
- Users CAN register as ClaimExaminer via the `[AllowAnonymous] RegisterAsync` endpoint.
- `GetExternalUserLookupAsync` hardcodes `allowedRoleNames = ["Patient", "Applicant Attorney", "Defense Attorney"]` -- **ClaimExaminer excluded** (see also [SEC-03](SEC-03.md)).
- No `CaseEvaluationPermissions.ClaimExaminer.*` permissions defined.
- No Angular component, menu, route, or dashboard for Claim Examiner.
- `HomeComponent` role branching has Patient/Attorney checks but not ClaimExaminer -- falls through to Patient-like default.
- E2E test B11.6.1 confirmed the role exists but does nothing.

Persona is real -- a workers' comp claims examiner is typically the carrier-side adjuster who authorises and sometimes schedules IMEs. The implementation is a stub.

---

## Official documentation

- [ABP Authorization](https://abp.io/docs/latest/framework/fundamentals/authorization) -- permission-based auth is canonical. Permissions assigned to roles OR directly to users. Built on ASP.NET Core policies.
- [ABP Permission Management](https://abp.io/docs/latest/modules/permission-management) -- module + UI for granting permissions to roles/users.
- [ABP Angular Permission Management](https://abp.io/docs/latest/framework/ui/angular/permission-management) -- client-side `abpPermission` directive + guard pattern.
- [ABP Navigation Menu / IMenuContributor](https://docs.abp.io/en/abp/latest/UI/AspNetCore/Navigation-Menu) + [ABP #5962](https://abp.io/support/questions/5962/How-to-set-permissions-for-dynamic-menu-items) -- `ConfigureMenuAsync` + `context.IsGrantedAsync("PermissionName")`; unauthorized children auto-hide parent.
- [ABP Multi-Tenancy](https://abp.io/docs/latest/framework/architecture/multi-tenancy) -- `IMultiTenant.TenantId` nullable = host-owned; tenant filtering automatic via EF global filters.
- [ABP Data Filtering](https://abp.io/docs/latest/framework/infrastructure/data-filtering) -- `IDataFilter` enable/disable inside `using` blocks; resets on dispose.
- [ABP Commercial Shared User Accounts (10.2+)](https://abp.io/docs/10.2/modules/account/shared-user-accounts) -- `UserSharingStrategy.Shared` lets one account belong to multiple tenants, login-time tenant picker + user-menu tenant switcher. **Requires Account.Pro / Identity.Pro**. Username/email uniqueness becomes **global**.
- [CA DWC QME process](https://www.dir.ca.gov/dwc/medicalunit/qme_page.html) + [CA DOI claims adjuster training](https://www.insurance.ca.gov/0200-industry/0100-education-provider/wc_training.cfm) -- workers' comp adjusters/examiners "determine the need for scheduling independent medical examinations and authorize treatments." Role scope: MEDIUM confidence (aggregated across job-description pages rather than a single DWC rule).

## Community findings

- [ABP Support #1350 -- Customizing tenant & user registration and adding roles](https://abp.io/support/questions/1350/customizing-tenant--user-registration-and-adding-roles) -- canonical pattern: override `RegisterAsync` to assign a default role; role must pre-exist and be granted permissions normally.
- [ABP Support #7882 -- Override Users, Roles & Permissions Methodology](https://abp.io/support/questions/7882/Override-the-existing-Users-Roles--Permissions-Methodology) -- ABP treats roles as permission bundles, not behavioural branches.
- [ABP Discussion #20299 -- Identity users and roles in host and tenant database](https://github.com/abpframework/abp/discussions/20299) -- roles live per side (host vs tenant); a cross-tenant "carrier" role must use Shared User Accounts or host-side users.
- [ABP Support #6907 -- Best practices for permission management](https://abp.io/support/questions/6907/ABP-best-practices-for-permission-management) -- "define permissions, gate UI/API on permissions, roles only as permission grouping."
- [OPM GS-0991 Workers' Comp Claims Examining](https://www.opm.gov/policy-data-oversight/classification-qualifications/classifying-general-schedule-positions/standards/0900/gs0991.pdf) -- federal job series: examiner authority to schedule/authorise IMEs.
- [Wisconsin DPM PD 320436 -- WC Claims Examiner](https://dpm.wi.gov/Documents/PD/PD_320436.pdf) -- state-level job description.

## Recommended approach

Sequence matters -- **do these in order**:

1. **Decide tenancy model** (product decision, see open questions). If carriers/examiners span multiple doctor-owned tenants (likely for CA workers' comp), use ABP Commercial's **Shared User Accounts** (`UserSharingStrategy.Shared`) so one examiner account joins each carrier-authorised tenant. Project already uses Account.Pro/Identity.Pro.
2. **Define `CaseEvaluationPermissions.ClaimExaminer.*` permissions** (Default/Create/Edit/Delete or narrower verbs like `ViewAssignedAppointments`, `RequestIme`, `ApproveIme`). Register in `CaseEvaluationPermissionDefinitionProvider`. Grant to the "Claim Examiner" role via `DataSeedContributor`.
3. **Switch role-branching to permission-branching.** `HomeComponent` and menu contributors should use `abpPermission` / `IsGrantedAsync` checks, not hardcoded `role === 'Claim Examiner'`. Permission checks are additive and handle multi-role users correctly.
4. **Ship actual UI**: claim-examiner dashboard showing assigned claimants' appointments, with permitted actions.
5. **Stop-gap**: until the feature ships, consider removing `ClaimExaminer` from `RegisterAsync`'s allowed `UserType` list to prevent orphan accounts. Low effort, high signal.

## Gotchas / blockers

- **Role-based branching is an ABP anti-pattern.** Hardcoded `role === 'Patient'` breaks the moment a power user has multiple roles. Permission checks always work.
- **Menu items vanish silently** if permissions aren't granted -- newcomers see an empty app. Seed at least one read permission per external role + a landing page.
- **`[AllowAnonymous] RegisterAsync` + arbitrary `UserType`** means today a bad actor can self-assign `ClaimExaminer`. Currently cosmetic (no permissions attached) but becomes privilege escalation the moment you grant cross-tenant read permissions to that role. Validate `UserType` server-side or require email-domain / invite code.
- **Shared User Accounts caveat**: enabling global username/email uniqueness is a **one-way migration** -- a Patient and a ClaimExaminer with the same email collide. Plan a migration script.
- **Tenant filter + host-side users**: if examiners are host-side (no `TenantId`) and need to see tenant-scoped appointments, every query path needs `IDataFilter.Disable<IMultiTenant>()` inside a `using` block, or business rules leak data across tenants.

## Open questions

- **Product**: does a ClaimExaminer work for one carrier (one tenant) or multiple? Drives Shared-Accounts vs host-user vs tenant-user. Inference: CA carriers handle claims across many employer/doctor relationships, so multi-tenant is the likely fit -- confirm with Gesco.
- **Product**: what actions does the examiner perform? View-only dashboard? Authorise IME requests? Schedule directly? Determines permission set + state-machine transitions.
- **Engineering**: keep `ExternalUserType` enum (simple but denormalised) vs drop in favour of ABP roles only (canonical but requires rewriting `RegisterAsync` to look up role by name)?
- **Engineering**: should `ClaimExaminer` be removed from the register allowlist now to prevent orphan accounts (low effort, high signal) until the feature ships?
- **Compliance**: cross-tenant visibility is a HIPAA/PHI surface -- who audits that a ClaimExaminer only sees *their* carrier's claimants, not all tenants?

## Related

- [SEC-03](SEC-03.md) -- this role is hardcoded out of the lookup method; fix together
- [FEAT-01](FEAT-01.md) -- claim examiners likely fire some status transitions (ApproveIme, RequestIme)
- Q4 in [TECHNICAL-OPEN-QUESTIONS.md](../TECHNICAL-OPEN-QUESTIONS.md#q4-what-is-the-claim-examiner-role-supposed-to-do)
- [docs/issues/INCOMPLETE-FEATURES.md#feat-02](../INCOMPLETE-FEATURES.md#feat-02-claim-examiner-has-no-ui-or-workflow)
