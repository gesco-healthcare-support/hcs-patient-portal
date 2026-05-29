---
status: draft
goal: Fix the 13 userflow findings filed in docs/runbooks/findings/2026-05-28-userflow-findings.md.
input: docs/runbooks/findings/2026-05-28-userflow-findings.md
related:
  - docs/runbooks/findings/bugs/BUG-008-put-me-concurrency.md
  - docs/runbooks/findings/bugs/BUG-009-leadtime-internal-error.md
  - docs/runbooks/findings/bugs/BUG-033-kind3-packet-generation-cascade-failure.md
  - docs/runbooks/findings/bugs/BUG-036-packet-generation-silently-fails-for-some-appointments.md
  - docs/runbooks/findings/bugs/OBS-23-no-ame-role-gate.md
  - docs/runbooks/findings/bugs/OBS-27-invite-email-empty-greeting.md
---

# Plan: Userflow finding fixes (2026-05-28)

Cluster fixes from this hardening run, ranked by impact and grouped by component
so a single PR per fix can ship independently. Every fix is read-and-write
isolated -- no fix blocks another. Diagnosis comes from 4 parallel research
agents that each read the running code (file:line citations below); nothing
here is speculative.

**Adrian's decision required: choose which fixes to authorize.** This plan
does not modify any code. Each fix is a separate self-contained patch that
can be greenlit or deferred individually.

---

## Inventory

| Finding | Severity | Root cause | Fix | Effort | Component | Independent? |
| --- | --- | --- | --- | --- | --- | --- |
| **A6**  | medium-high | `IsAttorneyClaimExaminerType` substring match misses `"Panel QME"` (space breaks `Contains("PQME")`) | Token-aware match | S | Domain/Packet | YES |
| **A1**  | medium | en.json key prefix `Foo:Bar` instead of full ABP code `CaseEvaluation:Foo.Bar` -- affects ~50 codes | Sweep en.json keys | M | Domain.Shared/Localization | YES |
| **A2**  | medium | `UpdateMyProfileAsync` returns DTO BEFORE UoW SaveChanges, so stale concurrencyStamp echoed back to client | Add explicit `SaveChangesAsync` before mapping | S | Application/Patients | YES |
| **A7**  | low-medium | Patient is in BOTH "Approved" stakeholder fan-out AND PacketGenerated handler -- 2 emails | Exclude Patient from Approved stakeholder list | S | Application/Notifications | YES (do AFTER A6 lands) |
| **A10** | low | Invite-flow registration sends a second verify email and leaves `EmailConfirmed=false`; invite token already proves ownership | Set `EmailConfirmed=true` when invite redeemed; skip second email | S | Application/ExternalSignups | YES |
| **A8**  | low | `GetInternalUserLookupAsync` returns dev-seed `*.test` accounts in approval modal | Blacklist `.test` + `admin@abp.io` in lookup | S | Application/Appointments | YES |
| **A9**  | low-medium | Appointment-row emails written verbatim while every read path defensively lowercases; CI collation masks the asymmetry | Normalize-on-write + one-time SQL backfill | S-M | Application/Appointments | YES |
| **A11** | low | `InviteExternalUser.html` interpolates `##PatientFullName##` which is always empty at invite time | Drop the greeting line; re-seed template | S | Domain/NotificationTemplates | YES |
| **A12** | low | CE name prefill composes `name + surname` correctly; symptom is empty `Surname` on the IdentityUser row (set during invite acceptance) | Defensive fallback now; capture Surname on invite later | S + M | angular booking-form; ExternalSignups | YES (Part 1 first) |
| **F2**  | low | `ResetPassword.cshtml.cs OnGet` does not verify the token; consumed/expired URL still renders form | Promote to `OnGetAsync`, call `VerifyUserTokenAsync` | S | AuthServer Pages | YES |
| **A5**  | low | English-language `disable()` lock on interpreter radio is intentional (PARITY-FLAG-NEW-004) but surprises users | Replace hard lock with default-only nudge | S | angular appointment-add | YES |
| **A4**  | low | Date widget inconsistency: ngbDatepicker (DOB + ApptDate) vs native `type="date"` (DOI) -- piecemeal authorship | Standardize on ngbDatepicker; needs FormControl format adapter | M | angular claim-info component | YES |
| **A3**  | low | HARDENING-TEST-SUITE Phase 0 references phantom `doctorId` + wrong route | Doc-only edit | S | docs/runbooks | YES |
| **F1**  | low | `/api/public/external-account/forgot-password` 404 is by design (PR #201 moved flow to Razor); 13x timing is rate-limiter partition warm-start, not user-existence oracle | Close as not-a-bug; document rationale | S | docs/runbooks | YES |
| **G1**  | observational | OpenIddict `Password` grant was removed 2026-05-19; HARDENING-TEST-SUITE Phase 9.3 still says `grant_type=password` | Rewrite Phase 9.3 to use Playwright AuthorizationCode+PKCE flow | S | docs/runbooks | YES |

13 findings spanning 9 components. Total: ~6 S-effort code fixes, 2 M-effort code fixes, 3 doc-only edits, 1 split (S + M deferred).

---

## Ranked execution order

Rank by risk-adjusted impact. Each ranked fix is shippable as a standalone
PR. Order matters only where annotated.

### Phase 1 -- Critical correctness (ship first)

1. **A6 -- Kind=3 packet generation for Panel QME**
   - Highest impact. Silently drops AA/DA/CE packet attachments on every
     Panel QME approval. HIPAA-adjacent (recipients get emails referencing
     a packet they never received).
   - Single function change. Smallest risk.
   - Reference: pre-existing diagnoses BUG-033 / BUG-036 are NOT current
     cause -- their fixes already landed. This is a separate substring bug.

2. **A2 -- PUT /patients/me concurrency stamp regression**
   - Blocks user retries across the booking form. Every booking failure
     forces a full reload.
   - Single-line server-side fix in 3 methods. Safe duplicate-flush.
   - Reference: BUG-008 already open; this run identifies the actual root
     cause (mapping fires before UoW commit), which BUG-008 hypothesized.

3. **A1 -- BusinessException localization sweep**
   - User-visible: every business rule violation currently shows
     "An internal error occurred during your request!"
   - Mechanical key-rename sweep in en.json. ~50 keys.
   - Reference: OBS-23 documented for the AME case; A1 fix is the global
     sweep that retires the family.

### Phase 2 -- Notification correctness (ship after A6)

4. **A7 -- Duplicate approval email to patient**
   - Dependency note: better to ship AFTER A6 lands, because A6 restores
     the AttyCE packet path. After A6 + A7 the email graph becomes:
     - Patient: 1 email (PatientPacket with attachment)
     - AA / DA / CE: 1 email each (StatusChange/Approved with attachment via A6)
     vs. today: Patient gets 2; AA/DA/CE get 1 each (no attachment).
   - Safe to ship without A6 -- they don't interact at code level -- but
     the UX outcome is cleanest when sequenced.

5. **A11 -- Invite email `"Hi ,"` greeting**
   - Template-only edit + variable bag cleanup. Requires re-seeding.
   - Independent of A10 even though both touch the invite flow.

6. **A10 -- Invite-flow second verification email**
   - Sets `EmailConfirmed=true` on invite redemption; skips the second email.
   - Reduces invite-acceptance friction.

### Phase 3 -- Security / data hygiene

7. **A8 -- Approve modal exposes seed test users**
   - Two-layer fix: short-term blacklist `.test` + `admin@abp.io`; longer-term
     `IsSeedAccount` marker on dev seeds. Recommend shipping Layer 1 only
     now; Layer 2 in a follow-up.

8. **A9 -- Appointment-row email case normalization**
   - Server-side normalize-on-write helper + one-time SQL backfill on 3
     existing rows. Today masked by SQL Server CI collation; fixes the
     footgun before any collation or migration change exposes it.

9. **F2 -- Consumed reset URL re-renders form**
   - AuthServer Razor page hardening. Token verified at GET, not just POST.
   - Cosmetic UX, but the change is a 20-line addition with one inject.

### Phase 4 -- UX polish (lowest impact, ship anytime)

10. **A5 -- English interpreter lock**
    - Replace hard `.disable()` with a default-only nudge so a user who
      genuinely needs an interpreter (e.g., ASL) can request one even
      when language is English.
    - REQUIRES Adrian's call: keep current behavior (option A: helper text)
      or change semantics (option B: remove the lock). Defaulting to
      option B; flag for confirmation.

11. **A12 (Part 1) -- CE name prefill defensive fallback**
    - Frontend-only patch in `appointment-add.component.ts`. Handles the
      missing-Surname case until invite flow captures it.

12. **A4 -- Standardize date widgets**
    - DOI claim modal: convert from native `<input type="date">` to ngbDatepicker.
    - Requires a `NgbDateStruct <-> ISO string` format adapter at submit time.
    - Medium effort. Lowest user impact among code fixes.

### Phase 5 -- Doc-only edits

13. **A3 -- Phase 0 doctorId + endpoint**
14. **F1 -- Document forgot-password public API rationale**
15. **G1 -- Rewrite Phase 9.3 to AuthorizationCode+PKCE**

### Phase 6 -- Deferred for follow-up

16. **A12 (Part 2) -- Capture Surname on invite acceptance**
    - Backend + Angular invite-accept form change. File separately;
      out of scope for this batch.

---

## Per-fix detail

### A6 -- Kind=3 packet for `"Panel QME"` (S, 1 file)

**File:** `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs:71-75`

**Change:**
```csharp
private static bool IsAttorneyClaimExaminerType(string? typeName)
{
    var name = (typeName ?? string.Empty).ToUpperInvariant();
    // Tokenize on non-alphanumeric boundaries so "PANEL QME" yields tokens
    // ["PANEL", "QME"]; substring-only matching missed this because the
    // PQME short-code is contiguous but the long name is whitespace-split.
    var tokens = System.Text.RegularExpressions.Regex
        .Split(name, "[^A-Z0-9]+");
    return tokens.Contains("PQME")
        || tokens.Contains("AME")
        || (tokens.Contains("PANEL") && tokens.Contains("QME"))
        || name.Contains("PQME")  // keep substring for "PQME-REVAL" composites
        || name.Contains("AME");  // keep substring for "AMEREEVAL"
}
```

**Test:** add unit test mapping `{shortCode, longName} -> expectedBool` covering
`"QME"`, `"Panel QME"`, `"PQME"`, `"PQME-REVAL"`, `"AME"`, `"AME-REVAL"`,
`"Agreed Medical Examination (AME)"`, `"SupReport"`, `"Deposition"`,
`"Record Review"`.

**Smoke:** approve A00003 again post-deploy; verify `AppAppointmentPackets`
has Kind=3 row with Status=2 and a Gotenberg-rendered PDF in MinIO.

**Risk:** false positive on a hypothetical future `"PANEL diagnostic Q-ME"`
type. Acceptable -- only 8 seeded types exist.

---

### A2 -- PUT /patients/me stale concurrency stamp (S, 1 file, 3 methods)

**File:** `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs`

**Change:** insert `await CurrentUnitOfWork!.SaveChangesAsync();` before the
`return ObjectMapper.Map<Patient, PatientDto>(patient)` in each of:
- `UpdateMyProfileAsync` (lines 493-526)
- `UpdatePatientForAppointmentBookingAsync` (lines 343-371)
- admin `UpdateAsync` (lines 472-490)

**Test:** integration test that submits the booking form twice with no
reload between attempts; both PUT /me responses must carry distinct
`concurrencyStamp` values and the form must not 409 on the second submit.

**Risk:** an extra SaveChanges per request; idempotent in EF Core for
already-tracked rows. No new failure modes.

---

### A1 -- en.json key prefix sweep (M, ~50 keys, 1 file)

**File:** `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`

**Pattern:** rename every key matching `^[A-Z][a-zA-Z]+:[A-Z]` (e.g.
`Appointment:AmeRequiresAttorneyRole`) to the full ABP error-code format
matching `CaseEvaluationDomainErrorCodes.cs` constants:

```json
"CaseEvaluation:Appointment.AmeRequiresAttorneyRole":
  "Only Applicant Attorneys and Defense Attorneys can request an AME or AME-REVAL appointment. Please contact your attorney to schedule this evaluation.",
```

**Pre-check:** grep for `L["Foo:Bar"]` literal usages BEFORE renaming. Any
display-side call site that uses the OLD key needs an alias kept (duplicate
the key entry under both old + new prefix) until call sites migrate.

**Reference:**
- ABP docs (Exception Handling > Localization): keys must use the full
  `Namespace:Code` form, where Namespace is mapped via `MapCodeNamespace`.
- `CaseEvaluationDomainSharedModule.cs:89` maps `"CaseEvaluation" ->
  CaseEvaluationResource`.

**Test:** run a smoke that triggers every BusinessException code (AME rule,
lead-time violation, lockout, invalid transition, etc.) and assert the UI
toast shows the localized message, not "An internal error occurred".

**Risk:** mis-renaming a key that's also used as a display label silently
falls back to the key string. Mitigation: keep both old and new keys for
one release cycle, then audit usages and drop the old keys.

---

### A7 -- Patient gets 2 approval emails (S, 1 file)

**File:** `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/StatusChangeEmailHandler.cs:266-284`

**Change:** in `DispatchApprovedAsync`, exclude Patient from the
stakeholder fan-out:

```csharp
var stakeholdersForExt = stakeholders
    .Where(r => r.Role != RecipientRole.Patient)
    .ToList();

if (stakeholdersForExt.Count > 0)
{
    await _ccAppender.AppendAsync(
        stakeholdersForExt,
        contextTagForLogging: $"Approved/Stakeholders/{eventData.AppointmentId}");

    var extVars = BuildVariables(...);
    await _dispatcher.DispatchAsync(
        templateCode: NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt,
        recipients: stakeholdersForExt,
        variables: extVars,
        contextTag: $"StatusChange/Approved/Stakeholders/{eventData.AppointmentId}");
}
```

**Scope:** this branch only. Other status transitions (Rejected,
CheckedIn, CheckedOut, CancelledNoBill) keep their stakeholder fan-out
to the patient because they have no paired packet email.

**Test:** approve an appointment, assert `HangFire.Job` for `patient1@gesco.com`
shows exactly ONE Approved-context email (the `AppointmentDocumentAddWithAttachment`
one), not two.

**Risk:** if `PacketGeneratedEto -> PatientPacketEmailHandler` ever fails
(template missing, packet generation crashes), the patient gets ZERO
approval emails. Mitigation: A6 fixes the highest-frequency cause; add a
defensive `try/catch` log in PatientPacketEmailHandler if the dispatch
returns failure, fall back to `PatientAppointmentApprovedExt`.

---

### A10 -- Invite-flow skip second verify (S, 1 file)

**File:** `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs`

**Changes:**
1. After `createResult.Succeeded` (around line 551), if `acceptedInvitation != null`:
   ```csharp
   if (acceptedInvitation != null)
   {
       user.SetEmailConfirmed(true);
       await _userManager.UpdateAsync(user);
   }
   ```
2. Wrap the existing `_accountEmailer.SendEmailConfirmationLinkAsync(...)`
   block (lines 665-674) in `if (acceptedInvitation == null) { ... }`.

**Test:** complete an invite-flow registration (Patrick invites Henry as CE),
assert Henry can log in immediately without clicking a second email, and
that `AbpUsers.EmailConfirmed = 1` after step 1 alone.

**Risk:** if an invite is delivered to a mistyped email and an unintended
recipient registers, they'd be auto-confirmed. The invite issuer (Staff
Supervisor) is responsible for the email address; same trust model as before.

---

### A11 -- Invite email greeting (S, 2 files)

**File 1:** `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/InviteExternalUser.html:7`
- Delete: `<p>Hi ##PatientFullName##,</p>`
- Replace with: `<p>Hello,</p>`

**File 2:** `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:991-1003`
- Remove `["PatientFullName"] = string.Empty` from the variable bag (dead key).

**Re-seed:** template content is seeded by `NotificationTemplateDataSeedContributor`.
Increment seed version OR re-run idempotent seed in `CaseEvaluationDbMigrationService`
so existing tenants pick up the new template.

**Test:** issue a fresh invite, inspect the rendered body in Hangfire job
arguments; the literal `Hi ,` must be gone.

**Risk:** tenants that customized the template via the per-tenant editor
and re-added `##PatientFullName##` will still render `Hi ,`. Acceptable --
the editor surfaces the empty substitution and the tenant can fix it.

---

### A8 -- Hide seed test users in approve modal (S, 1 file)

**File:** `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.Approval.cs:204`

**Layer 1 change:**
```csharp
private static bool IsSeedOrSystemAccount(string? email)
{
    if (string.IsNullOrWhiteSpace(email)) return false;
    var lower = email.Trim().ToLowerInvariant();
    return lower == "admin@abp.io"
        || lower.EndsWith(".test")
        || lower.EndsWith("@hcs.test");
}

// in GetInternalUserLookupAsync:
IEnumerable<IdentityUser> filtered = byId.Values
    .Where(u => !IsSeedOrSystemAccount(u.Email));
```

**Layer 2 (follow-up):** add `IsSeedAccount = "true"` ExtraProperty on
seeded users in `InternalUsersDataSeedContributor.cs`; switch filter from
email-suffix to property check. Survives email-domain renames.

**Test:** as Rachel, open approve modal for any pending appointment;
Responsible User dropdown must show only `clistaff1@gesco.com` and
`stafsuper1@gesco.com` (plus any future production users).

**Risk:** any legitimate `@*.test` mailbox is hidden. Reserved by RFC 2606;
never appears in customer email in practice.

---

### A9 -- Normalize appointment-row emails (S-M, 1 file + SQL)

**File:** `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`

**Helper:**
```csharp
private static string? NormalizeEmail(string? email)
    => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
```

**Call sites:** lines 784-787 (Create) and 962-965 (Update) -- replace
verbatim assignment with `NormalizeEmail(input.X)`. Same for
`AppointmentClaimExaminer.Email` writes.

**One-time backfill SQL** (run AFTER code deploy, in a maintenance window):
```sql
-- Verify count before deploy:
SELECT COUNT(*) FROM AppAppointments
WHERE ApplicantAttorneyEmail <> LOWER(LTRIM(RTRIM(ApplicantAttorneyEmail)))
   OR DefenseAttorneyEmail   <> LOWER(LTRIM(RTRIM(DefenseAttorneyEmail)))
   OR ClaimExaminerEmail     <> LOWER(LTRIM(RTRIM(ClaimExaminerEmail)));

UPDATE AppAppointments
SET ApplicantAttorneyEmail = LOWER(LTRIM(RTRIM(ApplicantAttorneyEmail))),
    DefenseAttorneyEmail   = LOWER(LTRIM(RTRIM(DefenseAttorneyEmail))),
    ClaimExaminerEmail     = LOWER(LTRIM(RTRIM(ClaimExaminerEmail))),
    PatientEmail           = LOWER(LTRIM(RTRIM(PatientEmail)));

UPDATE AppAppointmentClaimExaminers
SET Email = LOWER(LTRIM(RTRIM(Email)));
```

**Test:** Create + Update appointments with mixed-case emails; assert
persisted column values are lowercase regardless of input casing.

**Risk:** audit views that display the typed-case email will show
lowercase instead. SMTP local-part is case-insensitive per RFC 5321 5.1;
deliverability unaffected. Audit log table (if separate) is untouched.

---

### F2 -- ResetPassword GET token verify (S, 1 file)

**File:** `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/ResetPassword.cshtml.cs:76-84`

**Change:** promote `OnGet` to `OnGetAsync`, inject `IdentityUserManager`,
verify token before rendering:

```csharp
public async Task<IActionResult> OnGetAsync()
{
    if (UserId == Guid.Empty || string.IsNullOrWhiteSpace(ResetToken))
    {
        return RedirectToForgotWithError(
            "That reset link doesn't work anymore. Request a new one below.");
    }

    var user = await _userManager.FindByIdAsync(UserId.ToString());
    if (user == null)
    {
        return RedirectToForgotWithError(
            "That reset link doesn't work anymore. Request a new one below.");
    }

    var isValid = await _userManager.VerifyUserTokenAsync(
        user,
        _userManager.Options.Tokens.PasswordResetTokenProvider,
        UserManager<Volo.Abp.Identity.IdentityUser>.ResetPasswordTokenPurpose,
        ResetToken);
    if (!isValid)
    {
        return RedirectToForgotWithError(
            "That reset link doesn't work anymore. Request a new one below.");
    }

    return Page();
}
```

**Test:** issue a fresh reset URL, click it twice; second click redirects
to /Account/ForgotPassword with the expired-link flash, no form rendered.
Also mutate the token by one char; same redirect.

**Risk:** `VerifyUserTokenAsync` is read-only; does not consume the token.
Safe to call on GET.

---

### A5 -- English interpreter lock UX (S, 1 file)

**Status:** REQUIRES ADRIAN'S CONFIRMATION before implementing. Two options:

**Option A (preserve current semantics, add disclosure):**
- Keep `.disable()` lock at `appointment-add.component.ts:1141-1153`.
- Add help text below the radio: `"Not applicable when the patient's language is English."`

**Option B (remove the lock):**
- Replace `.disable()` with a default-only setValue:
  ```typescript
  if (isEnglish) {
      if (interpreterCtrl.value !== false) {
          interpreterCtrl.setValue(false, { emitEvent: false });
      }
      // Leave the control enabled. A user who needs an interpreter
      // for an English-speaker (e.g. ASL) must be able to flip Yes.
  }
  ```
- Remove the `else { enable(); }` branch entirely.

**Recommendation:** Option B (the lock came in as PARITY-FLAG-NEW-004,
NEW-only behavior). Wrong default for accessibility (deaf English speakers
needing ASL). Confirm with Adrian.

---

### A12 (Part 1) -- CE name prefill fallback (S, 1 file)

**File:** `angular/src/app/appointments/appointment-add.component.ts:192-196`

**Change:**
```typescript
const composed = [user.name, user.surname].filter(Boolean).join(' ').trim();
const fallback =
    composed
    || user.userName
    || (user.email ? user.email.split('@')[0] : null);
return {
    name: fallback,
    email: user.email ?? null,
};
```

**Test:** as a CE user with `Surname=null` (mimic Henry's pre-fix state),
open booking form, open claim modal, verify CE Name field shows the
username/email-local-part rather than empty.

**Risk:** if both name + surname blank, shows username. Better than blank.

**Part 2 (deferred):** capture First/Last on invite-acceptance form so
the IdentityUser row has both fields. Separate finding; out of scope.

---

### A4 -- Standardize date widgets (M, 2 files + adapter)

**File 1:** `angular/src/app/appointments/.../appointment-add-claim-information.component.html`
- Lines 140-149 (DOI): replace native `<input type="date">` with
  ngbDatepicker variant; add `(click)="injuryDateOfInjuryPicker.open()"`.
- Lines 154-159 (To Date): same.

**File 2:** `angular/src/app/appointments/.../appointment-add-claim-information.component.ts`
- Import `NgbDatepickerModule`.
- Add `todayNgb: NgbDateStruct` getter for `[maxDate]` constraint.
- Add ISO-string <-> NgbDateStruct serialization adapter for submit.

**Test:** create + update an appointment via claim modal with all three
date paths; verify the persisted DateOfInjury matches what the user picked.

**Risk:** the FormControl currently stores ISO `YYYY-MM-DD` string (native
input convention); ngb stores `NgbDateStruct`. Parent submit serializer
must convert. If missed, submit posts `[object Object]` silently.

**Counter-option (S effort):** leave as-is. Two-widget inconsistency is
cosmetic only. Recommend Adrian decide -- the M-effort fix is not urgent.

---

### A3, F1, G1 -- Doc-only edits (S each)

**A3:** `docs/runbooks/HARDENING-TEST-SUITE.md:243-247` -- replace phantom
`doctorId` payload + `/generate-preview` route with the real
`/preview` endpoint + actual `DoctorAvailabilityGenerateInputDto` array
shape.

**F1:** Add a note to the findings file documenting that
`/api/public/external-account/forgot-password` is intentionally absent
(PR #201 commit `1c79858`, 2026-05-15). The 13x timing variance is
rate-limiter partition warmup, not a user-existence oracle. Close as
not-a-bug.

**G1:** Rewrite `docs/runbooks/HARDENING-TEST-SUITE.md` Phase 9.3 to use
Playwright's AuthorizationCode+PKCE flow (the same harness that Phase 9.4
already uses). Drop `grant_type=password` references -- the grant was
intentionally removed 2026-05-19 (audit D-14 per
`OpenIddictDataSeedContributor.cs:77-88`). The runbook is the only
consumer that's out of date.

---

## Risk + rollback

### Independent rollback per fix

Every fix touches a distinct file or a distinct method, so rolling back
any one fix is `git revert <commit>` with zero blast on the others.

### Highest-risk fixes (verify smoke before merge)

1. **A6** -- if the token tokenizer over-matches, future appointment types
   could erroneously generate Kind=3 packets. Unit-test mapping catches this.
2. **A9** -- one-time SQL backfill on production data. Run during a quiet
   window; have a `SELECT COUNT(*)` pre-check ready. The code change alone
   is safe; the backfill is the destructive step.
3. **A1** -- mis-renaming a key that's also a display label silently breaks
   the UI string. Mitigation: alias both old + new keys for one release.

### Lowest-risk fixes (ship anytime)

A3, A8 (Layer 1), F1, G1, F2, A11, A12 Part 1. None modify request flow,
all are read-only or string-only.

---

## Test plan

### Automated (per-fix unit/integration tests; see fix detail above)

- A6: TypeMatcher unit test with 10+ cases.
- A2: Submit-twice-no-reload integration test on PatientsAppService.
- A1: Localization round-trip test that throws each BusinessException code and
  asserts the response `message` is not "An internal error occurred".
- A7: Approve-flow integration test asserting exactly one Approved email per
  recipient role.
- A10: Invite-redeem integration test asserting `EmailConfirmed=true` post-
  redemption and zero verification emails sent.
- A11: Snapshot test on invite email body.
- A8: Lookup test asserting `.test` emails filtered.
- A9: Create+Update test asserting persisted email is lowercase.
- F2: Reset URL re-click test asserting GET redirects when token consumed.
- A5 / A12 / A4: Angular component tests.

### Manual smoke (post-merge to development branch)

Re-run Scenarios A through G from the 2026-05-28 userflow run end-to-end.
Expected differences:
- Daniel (Patient) attempting AME shows the localized "Only attorneys..." message (A1).
- After any booking failure + retry, second submit succeeds without reload (A2).
- After Rachel approves any Panel QME, `AppAppointmentPackets` has 3 rows
  with all Status=2 and Kind=3 PDF in MinIO (A6).
- Patient receives exactly one approval email (A7).
- Gregory's invite-redeem completes in one step, no second email (A10).
- Invite email body starts `Hello,` not `Hi ,` (A11).
- Rachel's approve modal shows only `clistaff1@gesco.com` and `stafsuper1@gesco.com` (A8).
- All appointment-row emails persist lowercase (A9).
- Consumed reset URL redirects immediately on second click (F2).

---

## Approval checklist

Adrian to mark each fix one of: `ship`, `defer`, `discuss`.

| Fix | Decision |
| --- | --- |
| A6 packet type matcher | __ |
| A2 patient /me concurrency stamp | __ |
| A1 localization sweep | __ |
| A7 patient duplicate approval email | __ |
| A11 invite email greeting | __ |
| A10 invite skip second verify | __ |
| A8 hide seed test users | __ |
| A9 normalize appointment emails | __ |
| F2 reset URL GET token verify | __ |
| A5 interpreter lock (Option A or B?) | __ |
| A12 Part 1 CE name fallback | __ |
| A4 date widget standardize (or accept inconsistency?) | __ |
| A3 doc fix | __ |
| F1 doc + close | __ |
| G1 doc rewrite Phase 9.3 | __ |

Once you mark decisions, I'll generate per-fix patches one at a time, each
gated on your explicit `go` before I touch any code.
