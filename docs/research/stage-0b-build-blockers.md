# Stage 0b -- Smoke-test Build Blockers (B1, B2, B3)

Status: research only. No code changes here. Defects ordered by blocker severity.
B1 must land before any other gate task because the Angular build is currently broken.

---

## Important correction up front

The kickoff prompt characterised SendBack as "a real OLD-app feature that internal
staff need". That is incorrect. Verified by independent grep across
`P:\PatientPortalOld\PatientAppointment.{Domain,Models,DbEntities,Api,Infrastructure}\`
and `patientappointment-portal/`: zero matches for `SendBack`, `SentBack`,
`sendback`, `Reassign`, or any equivalent in the OLD codebase. OLD's appointment
state machine is `Pending -> Approved | Rejected` with a free-text
`RejectionNotes` column on `Appointment` (cited in OLD
`AppointmentDomain.cs:997-1011`), and that is the only "return reason" surface.

`docs/parity/_cleanup-tasks.md` Task B (lines 80-111) confirms the same:
"NEW has a 'send back' feature (`AppointmentSendBackInfo` entity) where staff
returns an appointment to the user for changes. **Not in OLD spec. Remove.**"
Phase 0.2 (commit `d1bbdab`) executed that removal at the backend.

Implication: A1 is described in the ledger as "Clinic staff approval UI +
**send-back modal**". If A1 actually re-introduces the SendBack flow it is a
**NEW-stack design addition that violates strict-parity**, not a port. Adrian
should resolve before A1 starts:

- Option 1 (strict parity): A1 ships only Approve / Reject; "send back" never
  comes back; B1 strip-list deletes everything (no commented placeholders).
- Option 2 (deliberate deviation): A1 re-adds the SendBack feature with
  Adrian's explicit sign-off documented as a parity flag; B1 strip-list
  comments out the HTML section + leaves a `// TODO(stage-A1)` marker.

The B1 strip-list below covers both options -- delete-list is identical, the
only difference is whether to comment-out the HTML modal block.

---

## Defect B1 -- Angular dangling SendBack references (TOP PRIORITY)

### Build error

```
Could not resolve "./send-back-appointment-modal.component"
NG1010 'imports' must be an array
```

Caused by Phase 0.2 deleting
`angular/src/app/appointments/appointment/components/send-back-appointment-modal.component.{ts,html}`
and `angular/src/app/appointments/appointment/send-back-fields.ts`, plus the
proxy regen which dropped `AppointmentSendBackInfoDto` from
`proxy/appointments/models.ts` -- but leaving every consumer in
`appointment-view.component.{ts,html}` unchanged.

### Dangling reference inventory

All citations are absolute, against the live working tree.

`W:\patient-portal\replicate-old-app\angular\src\app\appointments\appointment\components\appointment-view.component.ts`
(file is 1604 lines):

| Line | Reference | Action |
|------|-----------|--------|
| 15 | `AppointmentSendBackInfoDto,` (named import from `proxy/appointments/models`) | DELETE |
| 31 | `import { SendBackAppointmentModalComponent } from './send-back-appointment-modal.component';` | DELETE |
| 34 | `import { buildFlaggedFieldLookup } from '../send-back-fields';` | DELETE |
| 36 | `type TransitionAction = 'approve' \| 'reject' \| 'sendBack';` | EDIT to `'approve' \| 'reject'` |
| 105 | `SendBackAppointmentModalComponent,` inside `imports: [...]` | DELETE |
| 124 | `sendBackModalVisible = false;` | DELETE |
| 125 | `latestSendBackInfo: AppointmentSendBackInfoDto \| null = null;` | DELETE |
| 126 | `private readonly flaggedFieldLookup = buildFlaggedFieldLookup();` | DELETE |
| 127-129 | `flaggedFieldsCache` field + JSDoc | DELETE |
| 329 | `this.maybeLoadLatestSendBackInfo(...)` | DELETE |
| 394-405 | `flaggedFieldsSet` getter + JSDoc | DELETE |
| 407-433 | `canEdit(fieldName)` method + JSDoc | DELETE entirely; replace every `[disabled]="!canEdit('xxx')"` HTML binding with a fixed value (see HTML strip-list) |
| 463-477 | `availableActions` getter | EDIT: drop the `'sendBack'` entry from the `Pending` branch |
| 479-491 | `isResubmitMode` getter | DELETE |
| 493-509 | `flaggedSections` getter | DELETE |
| 511-524 | `flaggedFieldLabels` getter | DELETE |
| 526-542 | `dispatchAction` switch | EDIT: drop `case 'sendBack':` arm |
| 544-571 | `onActionSucceeded` -- the `maybeLoadLatestSendBackInfo` call on line 568 | EDIT line 568 only (delete that single statement); keep the rest of the method |
| 573-609 | `saveAndResubmit` async method + JSDoc | DELETE |
| 611-630 | `maybeLoadLatestSendBackInfo` private method | DELETE |

Reference count in `.ts`: 33 distinct lines covering the 13 logical groups
above. The kickoff estimate of "~25" is accurate within fence-post error.

`W:\patient-portal\replicate-old-app\angular\src\app\appointments\appointment\components\appointment-view.component.html`
(file is 1035 lines):

| Line | Reference | Action |
|------|-----------|--------|
| 41-43 | `@case ('sendBack') { {{ '::Appointment:Action:SendBack' \| abpLocalization }} }` | DELETE the case (and its localization key from `en.json` IF Option 1) |
| 57-66 | `@if (isResubmitMode)` ... Save & Resubmit button block | DELETE the `@if` and its branch; keep the `@else` branch (the regular Save button) by un-nesting it |
| 98-149 | The whole `@if (isResubmitMode && latestSendBackInfo) { <amber banner> }` block | DELETE |
| 167-171 | `<app-send-back-appointment-modal ...></app-send-back-appointment-modal>` | DELETE under Option 1 / COMMENT-OUT with `<!-- TODO(stage-A1): ... -->` under Option 2 |
| 212 | `[disabled]="!canEdit('panelNumber')"` | EDIT to remove the binding (or change to a static `[disabled]="false"`) |
| 246, 254, 262, 277, 300, 330, 338, 350, 367, 375, 383, 391, 402, 410, 422, 436, 448, 457, 466, 485, 494, 503, 513, 522, 531, 540, 556, 590, 619, 628, 637, 646, 655, 664, 673, 682, 706, 740, 769, 778, 787, 796, 805, 814, 823, 832 | All `[disabled]="!canEdit('xxx')"` bindings | EDIT: remove all (or replace with a static disabled rule keyed off appointmentStatus -- see "Replacement decision" below) |

Reference count in `.html`: 52 distinct lines (including the 46 `canEdit`
bindings). Total across both files: 85 line-level edits / deletes.

### Replacement decision for `canEdit`

`canEdit` is purely a read-only-edit-mode gate that depends on
`latestSendBackInfo.flaggedFields`. With SendBackInfo gone, the gate
collapses to: "external roles read-only; internal admin always editable",
which is already the server-side authority. Two options:

- **Drop the bindings entirely.** Cleaner; relies on the server's
  permission attributes (`Appointments.Edit`, `Patients.Edit`) to reject
  unauthorized writes. Stage A1 can re-introduce a per-field gate then.
- **Replace with a single class-level getter** like `get isReadOnly(): boolean`
  that returns `this.isPatientUser` (i.e. external roles get read-only fields
  on the view page). One getter, one binding pattern. Lower diff churn when
  A1 lands.

Recommend the second: keep one `isReadOnly` getter (or accept Adrian's call
on the name), bind every input as `[disabled]="isReadOnly"`. Net diff is
removing the `canEdit('xxx')` arg from each line.

### Send-back data already in NEW (status check on B1 prerequisites)

- `src\HealthcareSupport.CaseEvaluation.Domain.Shared\Enums\AppointmentStatusType.cs`
  has 13 values, lines 8-23. **No `SentBack`. No `AwaitingMoreInfo`.** The
  comment on lines 4-7 explicitly notes "the NEW-only AwaitingMoreInfo=14
  state was removed when the SendBack flow was deleted in Phase 0.2".
- `AppointmentChangeRequest` entity exists at
  `src\HealthcareSupport.CaseEvaluation.Domain\AppointmentChangeRequests\AppointmentChangeRequest.cs`
  but its consts (`AppointmentChangeRequestConsts.cs`) only define
  `ReasonMaxLength`. There is no `ChangeRequestType` enum, so SendBack is
  NOT currently modeled as a ChangeRequest variant. If A1 re-introduces it
  via ChangeRequest, that is design work for stage A1, not B1.
- OLD's nearest equivalent is `Appointment.RejectionNotes` (free-text,
  populated when staff Rejects). OLD has no "return for changes" loop.

### Acceptance for B1

```bash
cd angular
npx ng build --configuration development
```

Exit 0, no NG1010 errors, no esbuild "Could not resolve" errors. Subsequent
`npx serve -s dist/CaseEvaluation/browser -p 4200` should serve the SPA and
navigating to `/appointments/view/<any-uuid>` should not 500 in the browser
console. Manual smoke: Approve and Reject still dispatch correctly.

---

## Defect B2 -- IT Admin seed user fails password policy

### Root cause: 7-character password vs 8-character minimum

`src\HealthcareSupport.CaseEvaluation.Domain\Identity\InternalUsersDataSeedContributor.cs`
line 34 declares:

```csharp
public const string DefaultPassword = "1q2w3E*";
```

That string is **7 characters** (`1`, `q`, `2`, `w`, `3`, `E`, `*`). The
JSDoc on lines 30-31 incorrectly claims the password "matches ABP's stock
password policy" -- ABP's stock minimum length is 6, so the comment was
true at the time of writing but Phase 2 raised the floor.

`src\HealthcareSupport.CaseEvaluation.Domain\Identity\ChangeIdentityPasswordPolicySettingDefinitionProvider.cs`
lines 46-50 set `RequiredLength = "8"`. Lines 22-44 also flip
`RequireNonAlphanumeric = true` and `RequireDigit = true` (matching OLD's
regex `^(?=.*[0-9])(?=.*[a-zA-Z])(?=.*[-.!@#$%^&*()_=+/\\'])...` per the
JSDoc on lines 8-12). `RequireUppercase` and `RequireLowercase` are flipped
to `false`. So the active policy is: **digit + non-alphanumeric + length>=8**.

`1q2w3E*` satisfies digit + non-alphanumeric but **not** length>=8. ABP's
`IdentityUserManager.CreateAsync` rejects it with the exact log line shown
in the failure report.

### Seeded users (impact scope)

`InternalUsersDataSeedContributor` seeds:

- 1 host-side user: `it.admin@hcs.test` (line 35), role `IT Admin`.
- Per tenant (lines 111-116): `admin@<slug>.test`, `supervisor@<slug>.test`,
  `staff@<slug>.test`, all using the same `DefaultPassword`. **All of them
  fail the same way.** The error report only mentioned `it.admin` because
  it is seeded first.

### Recommended fix

Promote `DefaultPassword` to an 8-char string that satisfies the policy.
Pick `1q2w3E*r` (existing constant + one extra alpha; clearly throwaway,
explains itself as "qwerty walk + special + filler"). Drop-in replacement,
single-line constant change, no other code path needs to know.

Alternative: `Pass@word1` (10 chars, meets policy, but reads as something
a malicious test fixture might have used in production -- weaker as a
"clearly throwaway" signal). I prefer `1q2w3E*r` for the same shape as the
existing string with one more character.

ABP's stock seed contributor pattern (`Volo.Abp.Identity.Pro.IdentityDataSeedContributor`)
hardcodes `"1q2w3E*"` for the same reason -- this codebase inherited the
literal directly. So bumping it to a length-compliant variant is consistent
with ABP convention rather than a deviation.

### Configurable vs hardcoded

Status quo (hardcoded constant) is acceptable for development seeding.
ABP's own `IdentityDataSeedContributor` reads `AdminPassword` from
configuration, so for parity with the framework the cleanest path is:

1. Add `"App:DefaultSeedPassword"` to `appsettings.Development.json`.
2. Inject `IConfiguration` into `InternalUsersDataSeedContributor` and read
   that key, falling back to the existing constant when absent.

That is more work than the constant bump and the bump is sufficient for
B2. Recommend constant bump now; configuration-isation is a follow-up
chore (not a B2 blocker).

### Reseed procedure

The seeder is idempotent (lines 142-167 in
`InternalUsersDataSeedContributor.cs`): `if (user == null) { create }`.
Existing failed-creation attempts left no row, so changing the constant
and re-running the migrator on the same DB **will** work without volume
reset.

For a clean reseed (safer because tenant rows may have partial state):

```bash
docker compose down -v
docker compose up -d --build
```

`-v` removes the named SQL volume; the rebuild replays migrations and the
seed contributor with the new password.

### Acceptance for B2

After the constant bump and reseed:

1. `docker compose logs db-migrator | grep InternalUsersDataSeedContributor`
   shows `created user it.admin@hcs.test (tenant ).` and four created lines
   per tenant -- no `Passwords must be at least 8 characters.`
2. `https://localhost:44368` (AuthServer) Login form accepts
   `it.admin@hcs.test` + `1q2w3E*r` and redirects to the post-login page.
3. Same with the per-tenant admin/supervisor/staff accounts.

---

## Defect B3 -- AuthServer health-check internal probe (LOW)

### Root cause: missing env var on the authserver container

`docker-compose.yml`:

- API container has `App__HealthUiCheckUrl: "http://localhost:8080/health-status"`
  on line 97.
- AuthServer container (lines 51-83) **does not** set
  `App__HealthUiCheckUrl`. It only sets `App__SelfUrl`, `App__CorsOrigins`
  and the AuthServer-specific keys.

`src\HealthcareSupport.CaseEvaluation.AuthServer\HealthChecks\HealthChecksBuilderExtensions.cs`
line 18 reads `configuration["App:HealthCheckUrl"]`, which is set via
`appsettings.json` line 8 to `"/health-status"` (a relative path). Line 28
falls back to that relative path when `App:HealthUiCheckUrl` is missing:

```csharp
settings.AddHealthCheckEndpoint(
    "CaseEvaluation AuthServer Health Status",
    configuration["App:HealthUiCheckUrl"] ?? healthCheckUrl);
```

The HealthChecks UI library, given a relative path, expands it against the
process's first bound listener address. In containers `ASPNETCORE_URLS` is
`http://+:8080` (line 59), which the runtime binds as `[::]:8080`
(IPv6 dual-stack `0.0.0.0`). The probe then tries to GET
`http://0.0.0.0:8080/health-status` and fails with the documented error
because `0.0.0.0` is a wildcard, not a connect target.

### Standard fix

Mirror the API container's environment block: add one line to
`docker-compose.yml` under the `authserver:` `environment:` map:

```yaml
App__HealthUiCheckUrl: "http://localhost:8080/health-status"
```

Why `localhost:8080` and not `authserver:8080`? Because the in-process
HealthChecks UI hits the URL from inside the same container -- localhost
on the bound port. `authserver:8080` would also work (Docker DNS) but is
inconsistent with the API container's pattern; matching the existing
pattern keeps both services symmetric.

### Acceptance for B3

After the docker-compose edit + `docker compose up -d --build authserver`:

1. `curl http://localhost:44368/health-status` returns 200 with healthy
   payload (already true today, regression check).
2. `curl http://localhost:44368/health-ui` renders the dashboard and the
   "CaseEvaluation AuthServer Health Status" entry shows green status,
   not the red `IPv4 address 0.0.0.0 ... cannot be used as a target`
   error.
3. AuthServer container logs no longer show the `GetHealthReport` failure
   stack trace at probe interval (default 10s).

---

## Order of operations

1. **B1 first.** Without this nothing else can be smoke-tested via the
   Angular SPA.
2. **B2 second.** Without this no internal user can log in to verify B1's
   approve / reject paths or any other internal-side feature.
3. **B3 last.** Cosmetic; does not block any functional verification.

Each defect is a single-PR-sized change. Recommend three separate commits
on `feat/replicate-old-app` (or a sub-branch) so a regression in any one
can be reverted independently.
