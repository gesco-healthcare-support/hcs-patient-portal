---
feature: accessor-email-sharing
date: 2026-06-05
status: in-progress
base-branch: feat/replicate-old-app
related-issues: []
---

## Goal

Let staff add an appointment accessor by typing name + email + role + View/Edit rights
(auto-provisioning/inviting the person if that email isn't a user yet), and properly
secure accessor mutations -- restoring the OLD free-text create-or-link flow (parity
Group J: G-01-06, G-07-09, G-10-08).

## Context

Phase 3, first slice. The machinery mostly EXISTS on NEW; the gap is wiring + UI + two gates:
- `AppointmentAccessorManager.CreateOrLinkAsync` is fully built but DEAD (zero callers):
  resolves user by email, auto-creates (IsAccessor, random pw) + grants role + publishes
  `AppointmentAccessorInvitedEto`, or links existing; rejects a role mismatch
  (`AppointmentAccessorRoleMismatch`, verbatim OLD message). Uses the unit-tested
  `AppointmentAccessorRules.ResolveOutcome`. Needs the manager's FULL ctor (DI resolves it).
- The live path `AppointmentAccessorsAppService.CreateAsync` calls the SLIM
  `CreateAsync(identityUserId,...)`; `AppointmentAccessorCreateDto` has no email/name/role.
- `AccessorInvitedEmailHandler` already subscribes to the ETO (dormant until the manager fires it).
- `AppointmentReadAccessGuard` honors accessors / AA / DA / CE for READ; `AppointmentAccessRules.CanEdit`
  already requires `AccessType.Edit` for accessors -- but the guard exposes only `EnsureCanReadAsync`,
  and the core `AppointmentsAppService.UpdateAsync` never checks edit-access (only the
  change-request path does, via a private `EnsureCanEditAsync` duplicate).
- Accessor mutation endpoints are bare `[Authorize]` today -> any tenant user could add an
  accessor or self-grant Edit (self-escalation).
- Angular: `appointments/sections/appointment-add-authorized-users.component` is a modal with a
  DROPDOWN of existing users (identityUserId-based); it persists + loads correctly.

Record corrections from research: G-07-09 (AccessType) is already present + enforced on the
change-request path -- only the core-Update gate is missing. G-10-08 is satisfied by wiring
CreateOrLinkAsync (auto-provision by email; row stays FK-only) -- NO new columns / migration.

## Approach

- **Promote `EnsureCanEditAsync` to `AppointmentReadAccessGuard`** (mirror `EnsureCanReadAsync`
  but use `AppointmentAccessRules.CanEdit`); have `AppointmentChangeRequestsAppService` delegate
  to it (removes the duplicate). One authoritative edit gate, reused by the accessor AppService
  and the core Update.
- **Wire `CreateOrLinkAsync` as the live accessor-create path** (email + name + role); the row
  stays FK-only and the user is auto-provisioned/invited. No schema change (Gate 1 + decision 1).
- **Gate accessor create/update/delete + core `AppointmentsAppService.UpdateAsync` behind
  `EnsureCanEditAsync`** (deny-by-default; non-parties and View-only accessors blocked).
- **Invite email active now** (decision 2): wiring the manager fires the existing handler.
- Frontend: dropdown -> free-text first/last/email/role/rights; submit email-based.
- Rejected: re-adding name/email columns to the row (record's Option A) -- heavier, a migration,
  and redundant once the user is provisioned. Rejected: leaving the change-request edit-gate
  duplicated -- consolidate into the guard now (genuine duplicated knowledge).

## Tasks

- T1: Shared edit-access logic (SLIM rule: internal + creator + Edit-accessor).
  - approach: test-after
  - files-touched:
    - `src/.../Application/Appointments/AppointmentReadAccessGuard.cs` (add `CanEditAsync(Guid): Task<bool>` -- hydrate accessor entries + call the SLIM `AppointmentAccessRules.CanEdit(callerUserId, isInternal, creatorId, accessorEntries)`; plus `EnsureCanEditAsync(Guid)` that throws `AppointmentAccessDenied` when false)
    - `src/.../Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.cs` (replace its private hydrate+CanEdit duplicate with `_readAccessGuard.CanEditAsync(...)`; KEEP its own `ChangeRequestEditAccessRequired` throw -- NO behavior change, same rule + same error code)
    - EF Core test for the guard: internal / creator / Edit-accessor allowed; non-party / View-only accessor denied
  - acceptance: guard test green; change-request tests still pass unchanged (same slim rule, same error code). Decision (Adrian): slim rule, consolidate with no behavior change.

- T2: Accessor create-by-email (wire CreateOrLinkAsync) + secure mutations.
  - approach: test-after
  - files-touched:
    - `src/.../Application.Contracts/AppointmentAccessors/AppointmentAccessorCreateDto.cs` (-> {AppointmentId, Email, FirstName, LastName, Role, AccessTypeId}; drop the required IdentityUserId)
    - `src/.../Application/AppointmentAccessors/AppointmentAccessorsAppService.cs` (inject `AppointmentReadAccessGuard`; `CreateAsync` -> `EnsureCanEditAsync(input.AppointmentId)` then `CreateOrLinkAsync(...)`; `UpdateAsync`/`DeleteAsync` -> `EnsureCanEditAsync(appointmentId)`; resolve the accessor's appointmentId for update/delete)
    - localization key(s) if the role-mismatch / validation messages aren't already in en.json
    - EF Core test: a non-party is denied CreateAsync (auth gate); a party creates an accessor by a new email (CreateOrLinkAsync path). (Role-conflict + create-or-link internals are already unit-tested -- do not duplicate.)
  - acceptance: backend builds; non-party create is rejected; party create by new email provisions + links; role mismatch rejected.

- (Former T3 -- DEFERRED, not in this PR.) Discovery: the core `AppointmentsAppService.UpdateAsync`
  is bare `[Authorize]` -- ANY authenticated tenant user can edit ANY appointment (no permission, no
  access gate). This is broader than accessor-sharing and entangled with the "Edit is supervisor-only"
  model decision. Adrian: fix as a DEDICATED security slice BEFORE Phase 4. Recorded in memory
  `parity-phase-progress`. Remaining tasks below run T1 -> T2 -> T4 -> T5 -> T6.

- T4: Controller + proxy regen.
  - approach: code
  - files-touched:
    - `src/.../HttpApi/Controllers/AppointmentAccessors/AppointmentAccessorController.cs` (signature follows the DTO; likely unchanged route)
    - `angular/src/app/proxy/appointment-accessors/**` (regenerate; stage only real changes via `git diff --ignore-cr-at-eol`, restore EOL churn)
  - acceptance: build green; proxy carries the email-based create DTO.

- T5: Angular -- free-text accessor entry.
  - approach: code (UI; live-verified)
  - files-touched:
    - `angular/src/app/appointments/sections/appointment-add-authorized-users.component.ts/.html` (replace the user dropdown with free-text First/Last name + email + role select + View/Edit rights; client-side email/required validation)
    - `angular/src/app/appointments/appointment-add.component.ts` (submit email-based create; drop the externalAuthorizedUserOptions dependency for adding)
    - `angular/src/app/appointments/.../appointment-view...` (load still works; display name/email from the accessor/identityUser)
  - acceptance: the section lets a user type any name+email+role+rights and add it; prettier-clean; Angular build green.

- T6: Docs in the same change.
  - approach: code
  - files-touched:
    - `src/.../Domain/AppointmentAccessors/CLAUDE.md` (note: live create path is now email-based CreateOrLinkAsync; mutations gated by EnsureCanEditAsync; invite email active)
  - acceptance: docs reflect the shipped behavior.

## Risk / Rollback

- Blast radius: accessor create/update/delete + the core appointment Update gate + one Angular
  section. No schema/migration. The edit-gate consolidation touches the change-request AppService
  (covered by its tests).
- Security-positive: closes the self-escalation hole (non-parties/View-only blocked).
- Rollback: revert the PR.
- Watch items: (1) the manager's FULL ctor must resolve so CreateOrLinkAsync doesn't throw
  InvalidOperationException -- verify at build; (2) auto-provisioning users by free-typed email is
  a real side effect -- it is staff-initiated, edit-gated, server-side-validated, minimal role;
  (3) the invite email now fires on add.

## Verification

1. `dotnet test` -- new guard edit test (T1), accessor auth-gate + create-by-email test (T2),
   View-only-cannot-edit test (T3); existing AppointmentAccessor/AccessRules/change-request tests stay green.
2. Backend build + Angular build (in-container) green.
3. Live on the Docker stack (seed/confirm an appointment a staff user can edit):
   - As a party (staff/creator), add an accessor by a BRAND-NEW email -> a user is provisioned and
     the **invite email lands** (check the inbox/SMTP); the accessor row links to the new user.
   - Add an accessor whose email already exists with a DIFFERENT role -> role-mismatch rejected.
   - As a NON-party, the accessor create call is rejected (deny-by-default).
   - A View-only accessor cannot edit the appointment (main PUT denied); an Edit-accessor can.
4. Self-review (code-simplifier + code-reviewer); PR into `feat/replicate-old-app`; STOP.
   Expect the SonarCloud Quality Gate to be the only failing check (accepted exception).
