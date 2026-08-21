# F-H01 (HIGH) -- Attorney "register-after-booking" fails with HTTP 500

Status: OPEN. Confirmed on `main` @ 74e91563 (post frontend-rework merge #322), 2026-06-24.
No code changed for this. Written so it can be routed to the multi-tenant (db-per-tenant) session.

## TL;DR
An Applicant or Defense **attorney who was named on an appointment before they have a portal
account cannot register** -- the sign-up request returns HTTP 500. In the normal Gesco flow this
is the common case (a paralegal books and names the opposing attorney, who has no account yet, then
that attorney tries to sign up later). Claim examiners and patients are NOT affected. This is the
"deeper layer" of F-006/F-019 that was intentionally reverted on `feat/frontend-rework` to avoid
risk; it remains unfixed and now surfaces as a hard 500 (previously it was a silent duplicate row).

## What exactly is blocked (and what is NOT)
- BLOCKED: registration (`POST /api/public/external-signup/register`, which the Sign-up UI calls)
  for an **Applicant Attorney or Defense Attorney** whose email already appears on an appointment
  as the named applicant/defense attorney. They get 500 and cannot create the account at all.
- NOT blocked:
  - Attorneys who register BEFORE being named on any appointment (registration-first). Works.
  - Claim Examiners named-before-register. Works (data-model difference -- see below).
  - Patients named-before-register. Works (the patient path already ADOPTS the existing record).
  - Booking, approval, change requests, packets, etc. -- all unaffected.

## The data model (why attorneys collide but CE does not)
Two record kinds per party:
1. **Master** -- one row per attorney per tenant: `AppApplicantAttorneys` / `AppDefenseAttorneys`
   (and `AppClaimExaminers`). Holds firm/contact + `IdentityUserId` (the login, once they have one).
   Each has a UNIQUE index `IX_App<Party>_TenantId_Email` (verified live: all three exist).
2. **Appointment link** -- `AppAppointmentApplicantAttorneys` / `AppAppointmentDefenseAttorneys`
   carry `{AppointmentId, <Party>AttorneyId (-> master), IdentityUserId}`. The link points at a
   master row, so **booking an attorney creates or reuses a master**.

Key difference: the **claim-examiner appointment record** (`AppAppointmentClaimExaminers`) stores
the CE's own name/email inline and does NOT reference an `AppClaimExaminers` master. So booking a CE
does NOT create an `AppClaimExaminers` master row -> no pre-existing row -> CE register-after-booking
inserts cleanly. Attorneys DO get a master row at booking -> register tries to insert a second one
-> unique-index violation.

## Root cause (exact)
`src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs`,
`RegisterAsync`, Defense-Attorney branch ~line 912-933 (Applicant-Attorney branch ~895-911 is
symmetric):

```csharp
var existingDefenseAttorney = await _defenseAttorneyRepository
    .FirstOrDefaultAsync(a => a.IdentityUserId == user.Id);   // <-- only dedups by THIS user's id
if (existingDefenseAttorney == null)
{
    await _defenseAttorneyManager.CreateAsync(
        stateId: null, identityUserId: user.Id,
        firmName: input.FirmName?.Trim(), email: input.Email,  // <-- inserts a row with this email
        firstName: user.Name, lastName: user.Surname);
}
```

When a booking named this attorney earlier, a master row already exists with that
`(TenantId, Email)` and `IdentityUserId = NULL` (a placeholder). The lookup above queries by
`IdentityUserId == user.Id` (the brand-new user's id), so it does NOT find the null-id placeholder
-> it calls `CreateAsync` -> `INSERT` a second row with the same email -> unique index
`IX_AppDefenseAttorneys_TenantId_Email` rejects it.

Observed failure (live):
```
Microsoft.Data.SqlClient.SqlException (2601): Cannot insert duplicate key row in object
'dbo.AppDefenseAttorneys' with unique index 'IX_AppDefenseAttorneys_TenantId_Email'.
The duplicate key value is (dfed8778-...-b86a, defatty2@gesco.com).
  ... -> Microsoft.EntityFrameworkCore.DbUpdateException
  at ...ExternalSignupAppService.RegisterAsync(...) :line 962
-> HTTP 500
```

## Reproduction (clean)
1. Book an appointment (any booker) naming a NEW defense-attorney email, e.g. `defatty1@gesco.com`,
   that has no account. (Booking creates an `AppDefenseAttorneys` row, `IdentityUserId` NULL.)
2. As that email, register via the Sign-up page (or POST /api/public/external-signup/register,
   userType=4 DefenseAttorney). -> HTTP 500; no account created.
   Same for Applicant Attorney (userType=3).
3. Control: a CE email named the same way (userType=2) registers fine (204).

This run: A00001 named defatty1, A00002 named defatty2 (both before registration). Registering
defatty1/defatty2 -> 500 (locked out). defatty3 (never named first) + claimE1/claimE2 -> 204.

## What was reverted (history)
- Prior pass (feat/frontend-rework) found F-006/F-019: AA registration created a master with a NULL
  email, so booking's find-by-email could not match -> duplicate AA masters.
- The PRIMARY fix LANDED and is on main: AA + DA registration now pass `email` into
  `Manager.CreateAsync`, so the master has its email (visible in the code above).
- A DEEPER fix was then drafted: make AA/DA/CE registration ADOPT an existing unclaimed email master
  (the booking placeholder) instead of inserting a new one -- mirroring the patient adopt-by-email
  path. Adrian asked to REVERT it (risk of breaking the app) and schedule it properly. That reverted
  change is exactly what would prevent F-H01. Without it, register-after-booking for attorneys now
  hits the unique index and 500s.

## The fix (when scheduled) -- small + contained
In `RegisterAsync`, for the AA and DA branches, look up the master by EMAIL (unclaimed) as well as
by IdentityUserId, and ADOPT it rather than insert a new one. Sketch:

```csharp
var existing = await _defenseAttorneyRepository.FirstOrDefaultAsync(
    a => a.IdentityUserId == user.Id || (a.IdentityUserId == null && a.Email == input.Email));
if (existing == null) { /* CreateAsync as today */ }
else if (existing.IdentityUserId == null) {
    existing.IdentityUserId = user.Id;
    existing.FirmName ??= input.FirmName?.Trim();           // backfill blanks
    existing.FirstName ??= user.Name; existing.LastName ??= user.Surname;
    await _defenseAttorneyRepository.UpdateAsync(existing);
}
```
This is the same "adopt unclaimed email master" pattern the Patient branch already uses. Apply
symmetrically to AA. CE needs no change. Add tests for: register-after-booking adopts (no 500, no
dup, link still resolves) for AA + DA; registration-first still works.

## Relationship to the multi-tenant (db-per-tenant) work -- the question to answer
- The fix lives in `ExternalSignupAppService.RegisterAsync` (Application layer) and touches the
  attorney master entities, which ARE `IMultiTenant`. It does NOT depend on db-per-tenant mechanics.
- The unique index is `(TenantId, Email)`; under db-per-tenant the TenantId column still exists, so
  the collision and the fix are unchanged in shape.
- DECISION for the other session: fold this small adopt-existing-master fix into the multi-tenant
  branch (since it touches the same IMultiTenant attorney entities/app-service and you want to avoid
  conflicting edits to `ExternalSignupAppService` from two branches), OR keep it as a separate
  follow-up sequenced AFTER the multi-tenant merge. Either is viable; the deciding factor is whether
  the multi-tenant branch already edits `ExternalSignupAppService` / the attorney managers (if yes,
  fold it in; if no, a separate small PR avoids coupling).

## Severity
HIGH -- blocks a core real-user action (an attorney creating their account) in the most common
booking flow, with a 500 and no user-facing guidance. Not a data-corruption or security issue.

## Workaround used for this QA run (no code change)
Proceed with `defatty3` (registered, never named-first) as the registered Defense Attorney booker;
keep `defatty1`/`defatty2` as named (login-less) parties on A00001/A00002. This is realistic (not
every named attorney has a portal account) and preserves the repro.
