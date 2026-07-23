---
feature: Invite duplicate-email guard + clear invite-path register message
date: 2026-07-23
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Stop the "invite external user" flow from creating dead-end invitations for emails that already
have an account, and replace the misleading anonymised duplicate message on the invite
registration path with a clear one.

## Context & decisions

Live diagnosis (on-prem box, `development`): inviting `appatty1@gesco.com` failed at Register with
the enumeration-safe `Registration:DuplicateEmail` string because that email was already a
registered Applicant Attorney. `InviteExternalUserAsync` never checks for an existing account, so
it issues an invite that can never complete and shows a misleading "pending" row on
`/users/pending`. The register duplicate message is deliberately vague for anonymous self-signup
(anti-enumeration), but on the invite path the inviter already chose the email, so the vagueness
only confuses.

Resolved decisions:
- **Decision: add a NEW tailored error code + message for the invite guard** because reusing
  `InternalUserDuplicateEmail` would be semantically wrong (that's the internal-staff path) and a
  tailored message guides the inviter better.
- **Decision: guard blocks on ANY existing account (confirmed or not)** because it must mirror
  `RegisterAsync`'s own `FindByEmailAsync(...) != null` check (RegisterAsync would 400 on an
  unconfirmed duplicate too), keeping invite-time and register-time behaviour consistent.
- **Decision: the clear register message is thrown ONLY when `acceptedInvitation != null`, reusing
  the existing `RegistrationDuplicateEmail` code** because that code is already mapped to HTTP 400
  in both hosts (no mapping change), and the anonymous self-signup path must keep the anonymised
  message for enumeration safety.
- **Decision: throw `UserFriendlyException(message: L[key], code: code)`** (not
  `BusinessException(code)`) because BusinessException auto-localization via MapCodeNamespace does
  not resolve in this codebase (see ExternalSignupAppService.cs:789 comment).
- **Decision: TDD** because this is a security-adjacent business rule (access/registration path),
  and a DI-resolved test harness already exists.
- **Decision: no Angular changes** because both the invite UI and the register JS already surface
  `err.error.error.message`.

## All needed context

Anchors (all verified against current code, HIGH confidence):
- Guard insertion: `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:1380-1382`
  -- inside `using (CurrentTenant.Change(tenantId.Value, tenantName))`, immediately before
  `var (invitation, rawToken) = await _invitationManager.IssueAsync(...)` (line 1382). `_userManager`
  and `normalizedEmail` (line 1365) are already in scope.
- Duplicate-check pattern to mirror: `.../InternalUsers/InternalUsersAppService.cs:151-156`
  (`FindByEmailAsync` -> throw). Use the UserFriendlyException-with-message form instead of its
  BusinessException form.
- Register message branch: `ExternalSignupAppService.cs:789-791` (current throw); invite path
  detectable via `acceptedInvitation != null` (set at line 744).
- Error codes: `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs`
  -- `RegistrationDuplicateEmail` @662, `InternalUserDuplicateEmail` @192 (naming/format pattern:
  `public const string X = "CaseEvaluation:Area.Name";` with an XML-doc block).
- Localization: `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`
  -- `Registration:DuplicateEmail` @612 (add new keys near it).
- 400 mapping: `src/HealthcareSupport.CaseEvaluation.HttpApi/Exceptions/CaseEvaluationExceptionStatusCodeMappings.cs`
  -- `MapSharedRegistrationAndInternalUserCodes` (add the new code near line 41). Invoked by BOTH
  `CaseEvaluationAuthServerModule.cs:498` and `CaseEvaluationHttpApiHostModule.cs:182`.
- Tests: abstract base `test/HealthcareSupport.CaseEvaluation.Application.Tests/ExternalSignups/ExternalSignupAppServiceTests.cs`
  runs concretely via `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/ExternalSignups/EfCoreExternalSignupAppServiceTests.cs`
  (EF-backed). Fixture: DI `_appService`, `_currentTenant.Change(TenantsTestData.TenantARef)`, assert
  `UserFriendlyException.Code` + `.Message`. Mapping test:
  `test/HealthcareSupport.CaseEvaluation.Application.Tests/Exceptions/CaseEvaluationExceptionStatusCodeMappingsTests.cs`.

Exact new identifiers:
- Code: `CaseEvaluationDomainErrorCodes.InviteEmailAlreadyRegistered = "CaseEvaluation:Invite.EmailAlreadyRegistered";`
- Keys: `"Invite:EmailAlreadyRegistered": "This email already has an account in this office. Ask them to sign in or reset their password."`
  and `"Registration:DuplicateEmailInvited": "This email already has an account. Please sign in or reset your password."`

Gotchas:
- Guard MUST be inside the `CurrentTenant.Change` block (per-office DB scope) -- the anchor above is.
- `InviteExternalUserAsync` is `[Authorize(...InviteExternalUser)]`; the test harness resolves the
  AppService directly -- confirm the test module allows the call (existing tests hit `[AllowAnonymous]`
  RegisterAsync; if authorization blocks the invite test, resolve via the test base's current-user
  fake, consistent with other authorized-service tests). `CurrentUser.Id` null is already handled
  (invitedByUserId = Guid.Empty, line 1364).
- To reach the register invite-path branch a test needs BOTH a pending invitation (via
  `InvitationManager.IssueAsync` / `InviteExternalUserAsync` BEFORE a user exists) AND an existing
  user for that email -- mirror appatty1's real state.
- en.json is embedded -> the fix needs an api + authserver rebuild/redeploy; it is NOT a seeded row.

## Tasks (implementation blueprint)

### Task 1 -- declarations (code)
- **what:** MODIFY `CaseEvaluationDomainErrorCodes.cs` -- add `public const string InviteEmailAlreadyRegistered = "CaseEvaluation:Invite.EmailAlreadyRegistered";` with an XML-doc block. MODIFY `en.json` -- add `Invite:EmailAlreadyRegistered` and `Registration:DuplicateEmailInvited` (strings above). MODIFY `CaseEvaluationExceptionStatusCodeMappings.cs` -- add `options.Map(CaseEvaluationDomainErrorCodes.InviteEmailAlreadyRegistered, HttpStatusCode.BadRequest);`.
- **pattern:** the `RegistrationDuplicateEmail` const (DomainErrorCodes:662) + its en.json entry (@612) + its mapping line (mappings:34).
- **approach:** code
- **acceptance (EARS):** The system shall expose `CaseEvaluationDomainErrorCodes.InviteEmailAlreadyRegistered`, resolve both new localization keys to their English strings, and map the new code to HTTP 400 in both hosts.

### Task 2 -- invite guard (tdd)
- **what:** MODIFY `ExternalSignupAppService.InviteExternalUserAsync` at :1381 (before `IssueAsync`): `var existingUser = await _userManager.FindByEmailAsync(normalizedEmail); if (existingUser != null) throw new UserFriendlyException(message: L["Invite:EmailAlreadyRegistered"], code: CaseEvaluationDomainErrorCodes.InviteEmailAlreadyRegistered);`
- **pattern:** InternalUsersAppService.cs:151-156 (check) + ExternalSignupAppService.cs:789-791 (throw form).
- **approach:** tdd -- new test in `ExternalSignupAppServiceTests`: register a user for email X in TenantARef, then call `InviteExternalUserAsync` for X in TenantARef; assert `UserFriendlyException` with `.Code == InviteEmailAlreadyRegistered`.
- **acceptance (EARS):** WHEN an invite is issued for an email that already has any account in the target office, THE SYSTEM SHALL throw `InviteEmailAlreadyRegistered` and SHALL NOT create an `AppInvitations` row.

### Task 3 -- clear register message on invite path (tdd)
- **what:** MODIFY `ExternalSignupAppService.cs:789-791`: `throw new UserFriendlyException(message: acceptedInvitation != null ? L["Registration:DuplicateEmailInvited"] : L["Registration:DuplicateEmail"], code: CaseEvaluationDomainErrorCodes.RegistrationDuplicateEmail);`
- **pattern:** the existing throw at :789.
- **approach:** tdd -- test: issue an invite for email X (no user yet), register user X, then call `RegisterAsync` with that `InviteToken`; assert `.Code == RegistrationDuplicateEmail` and `.Message == "This email already has an account. Please sign in or reset your password."` Add a second test: anonymous duplicate (no InviteToken, existing user) still returns the anonymised `Registration:DuplicateEmail` message.
- **acceptance (EARS):** WHEN a register request carries a valid InviteToken for an email that already has an account, THE SYSTEM SHALL return the `Registration:DuplicateEmailInvited` message with code `RegistrationDuplicateEmail`; WHEN it carries no InviteToken, THE SYSTEM SHALL return the anonymised `Registration:DuplicateEmail` message.

### Task 4 -- mapping test (test-after)
- **what:** MODIFY `CaseEvaluationExceptionStatusCodeMappingsTests.cs` -- assert `InviteEmailAlreadyRegistered` maps to `HttpStatusCode.BadRequest`.
- **pattern:** the existing assertion for `RegistrationDuplicateEmail` in that file.
- **approach:** test-after
- **acceptance (EARS):** The system shall verify, in a unit test, that the invite-guard code maps to HTTP 400.

## Validation loop

Run from the P:-mapped solution root (`subst P: "C:\Users\RajeevG\Documents\Projects"` if unmapped):
1. `dotnet build` -- compiles clean (new const + call sites).
2. `dotnet test test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests` -- ExternalSignup guard + register-message tests green.
3. `dotnet test test/HealthcareSupport.CaseEvaluation.Application.Tests` -- mapping test green.
4. `dotnet format --verify-no-changes` (or the repo's format check) -- CI Backend: Format Check parity.

Done-bar: all four green (not merely "it compiles"). CI (Backend: Build/Test, Frontend unaffected)
must pass; SonarCloud new-code coverage >= 80% is satisfied by the TDD tests on the two new logic branches.

## Risk / rollback

- Blast radius: one AppService method (invite issue), one throw line (register), one new error code +
  two localization strings + one 400-mapping line. No schema, no migration, no Angular, no auth-policy
  change. Existing dead invitations (e.g. appatty1) are NOT retroactively cleaned -- the guard is
  preventive only.
- Rollback: revert the branch; nothing persisted (no migration/seed). Deployed copies revert on the
  next image rebuild from the reverted commit.
