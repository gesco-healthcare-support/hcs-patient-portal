---
feature: step-5-1-party-emails-ui-parity
date: 2026-04-30
status: draft
base-branch: feat/mvp-wave-2
related-issues: []
---

# Step 5.1 -- Party Emails, UI Parity, CE Extraction

## Goal

Fix appointment submission so all 4 involved parties (Patient, Applicant Attorney, Defense Attorney,
Claim Examiner) receive email notification. Simultaneously fix the DA form layout to match AA,
extract the CE section from the Claim Information modal into the main form, and enable
auto-link and auto-populate for known roles.

## Context

W-A-2 finding: email fan-out on appointment submit fires only to the booker. Two root causes:

1. **Timing**: `AppointmentSubmittedEto` is published during `CreateAsync` before Angular's
   subsequent upsert calls (AA join row, DA join row) have been made. The `AppointmentRecipientResolver`
   then finds empty join tables and falls back to office+booker only.

2. **Non-registered guard**: Angular's `upsertApplicantAttorneyForAppointmentIfProvided` and
   `upsertDefenseAttorneyForAppointmentIfProvided` both bail early if no `IdentityUserId` is present,
   so parties that typed an email but are not yet registered users never get a join row at all.

**Design fix**: store all 4 party emails directly on the `Appointment` entity at create-time.
The `SubmissionEmailHandler` reads these stored emails (not join rows) to fan out. Join rows still get
created when the party IS registered (for access-control / S-NEW-2 visibility); the email pathway
is now independent of join-row existence.

Additional UI work requested in the same push:
- DA section must visually match AA (was using wider column widths)
- CE sub-section must move from the Claim Information modal to the main form (name + email only now;
  full CE entity fields deferred)

## Approach

Seven ordered tasks. Tasks T-1 and T-2 are pure frontend with no backend dependency; T-3 (migration)
must merge before T-4 (email handler) because the ETO now carries columns added in T-3.
T-5 through T-7 are independent after T-3.

Rejected alternatives:
- **Move ETO publication to after all upsert calls** -- requires Angular to call a separate "submit
  complete" endpoint after its multi-step sequence, adds round-trip, and breaks the clean
  domain-event pattern.
- **Batch everything into a single Angular POST** -- would require a large new aggregate endpoint;
  deferred to a later refactor pass.

## Tasks

---

### T-1: DA UI Parity (HTML only)
- **description**: Fix Defense Attorney section column widths in `appointment-add.component.html`
  to match the Applicant Attorney section layout.
- **approach**: `code`
- **files-touched**: `angular/src/app/appointments/appointment-add.component.html`
- **acceptance**: DA and AA cards visually identical when both "Include" toggles are on.

**Exact changes** (lines 627-769 current):

Email search row:
- Current: `col-md-7` (email + Load inline) + `col-md-5` (select)
- Target: `col-md-5` (email input only) + `col-md-4 d-flex align-items-end` (Load button) +
  `col-md-3` (select) -- matching AA lines 471-505.

Detail fields (inside `<div class="row">`):
- `defenseAttorneyFirstName`: `col-md-6` -> `col-md-3`
- `defenseAttorneyLastName`: `col-md-6` -> `col-md-3`
- `defenseAttorneyEmail`: `col-md-12` -> `col-md-3`
- `defenseAttorneyFirmName`: `col-md-12` -> `col-md-3`
- `defenseAttorneyWebAddress`: `col-md-12` -> `col-md-6`
- `defenseAttorneyPhoneNumber`: `col-md-6` -> `col-md-3`
- `defenseAttorneyFaxNumber`: `col-md-6` -> `col-md-3`
- `defenseAttorneyStreet`: `col-md-6` -> `col-md-3`
- `defenseAttorneyCity`: `col-md-6` -> `col-md-3`
- State `abp-lookup-select`: `col-md-6` -> `col-md-3`
- `defenseAttorneyZipCode`: `col-md-6` -> `col-md-3`

---

### T-2: CE Section Extraction (Frontend)
- **description**: Remove the Claim Examiner card from the Claim Information modal and add a new
  "Claim Examiner Details" card to the main form (between DA section and Claim Information card).
  Capture name + email only for now (all other CE fields deferred to when the full CE entity is built).
- **approach**: `code`
- **files-touched**:
  - `angular/src/app/appointments/appointment-add.component.html`
  - `angular/src/app/appointments/appointment-add.component.ts`

**HTML changes**:
1. Delete lines 1079-1192 (the entire `<div class="card mb-3">` block for Claim Examiner in modal).
2. Add a new `<div class="card mb-4">` block immediately after the DA closing `</div>` (after
   line 769) following the same include-toggle pattern as AA/DA:

```html
<!-- CE Details card -->
<div class="card mb-4">
  <div class="card-header d-flex justify-content-between align-items-center">
    <h5 class="card-title mb-0 fw-bold">Claim Examiner Details</h5>
    <div class="form-check form-switch">
      <input class="form-check-input" type="checkbox" id="claim-examiner-enabled"
             formControlName="claimExaminerEnabled" />
      <label class="form-check-label" for="claim-examiner-enabled">Include</label>
    </div>
  </div>
  @if (form.get('claimExaminerEnabled')?.value) {
    <div class="card-body">
      <div class="row">
        <div class="col-md-6 mb-3">
          <label class="form-label fw-semibold">Name</label>
          <input class="form-control" formControlName="claimExaminerName"
                 placeholder="FULL NAME" maxlength="100" />
        </div>
        <div class="col-md-6 mb-3">
          <label class="form-label fw-semibold">Email</label>
          <input type="email" class="form-control" formControlName="claimExaminerEmail"
                 placeholder="EMAIL" maxlength="255" />
        </div>
      </div>
    </div>
  }
</div>
```

**TS changes in `appointment-add.component.ts`**:
1. Add 3 form controls to `FormBuilder` group (around line 325):
   ```ts
   claimExaminerEnabled: [false],
   claimExaminerName: [null as string | null, [Validators.maxLength(100)]],
   claimExaminerEmail: [null as string | null, [Validators.maxLength(255), Validators.email]],
   ```
2. Add `valueChanges` subscription for `claimExaminerEnabled` (same pattern as lines 373-378 for DA):
   ```ts
   this.form.get('claimExaminerEnabled')?.valueChanges.subscribe((enabled) => {
     this.applyConditionalEmailValidator('claimExaminerEmail', !!enabled);
   });
   ```
3. In `reset()` / `makeEmptyInjuryDraft()`: no change needed (form reset handles it).

---

### T-3: Store Party Emails on Appointment Entity
- **description**: Add 4 nullable email columns to the `Appointment` entity, expose them in
  `AppointmentCreateDto`, populate in `CreateAsync`, and create the EF migration.
- **approach**: `code`
- **files-touched**:
  - `src/.../Domain/Appointments/Appointment.cs`
  - `src/.../Application.Contracts/Appointments/AppointmentCreateDto.cs`
  - `src/.../Application/Appointments/AppointmentsAppService.cs`
  - `src/.../Application/Appointments/AppointmentsMappers.cs` (Riok.Mapperly mapper)
  - `src/.../EntityFrameworkCore/Appointments/` (EF config)
  - migration file (auto-named)

**Entity changes** (`Appointment.cs`):
Add 4 nullable string properties (email max 255):
```csharp
public string? PatientEmail { get; private set; }
public string? ApplicantAttorneyEmail { get; private set; }
public string? DefenseAttorneyEmail { get; private set; }
public string? ClaimExaminerEmail { get; private set; }
```
Add setter method (or expose via constructor / domain method):
```csharp
public Appointment SetPartyEmails(
    string? patientEmail, string? aaEmail,
    string? daEmail, string? ceEmail)
{
    PatientEmail = patientEmail;
    ApplicantAttorneyEmail = aaEmail;
    DefenseAttorneyEmail = daEmail;
    ClaimExaminerEmail = ceEmail;
    return this;
}
```

**DTO changes** (`AppointmentCreateDto.cs`):
```csharp
public string? PatientEmail { get; set; }
public string? ApplicantAttorneyEmail { get; set; }
public string? DefenseAttorneyEmail { get; set; }
public string? ClaimExaminerEmail { get; set; }
```

**AppService changes** (`AppointmentsAppService.CreateAsync`):
After `AppointmentManager.CreateAsync(...)`, call:
```csharp
appointment.SetPartyEmails(
    input.PatientEmail,
    input.ApplicantAttorneyEmail,
    input.DefenseAttorneyEmail,
    input.ClaimExaminerEmail);
```

**EF config**: add `HasMaxLength(255)` + `IsRequired(false)` for all 4 columns in
`AppointmentConfiguration.cs` (or wherever `Appointment` is configured).

**Migration**:
```bash
dotnet ef migrations add AddPartyEmailsToAppointment \
  --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
  --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
```

**Angular proxy**: after backend compiles cleanly, run:
```bash
abp generate-proxy -t ng --module app
```
Then update the payload construction in `appointment-add.component.ts` to include:
```ts
patientEmail: rawAfter.patientEmail ?? undefined,
applicantAttorneyEmail: rawAfter.applicantAttorneyEmail ?? undefined,
defenseAttorneyEmail: rawAfter.defenseAttorneyEmail ?? undefined,
claimExaminerEmail: raw.claimExaminerEnabled ? (rawAfter.claimExaminerEmail ?? undefined) : undefined,
```
Note: `patientEmail` is already a form control (line 52 in snapshot); it just isn't currently
included in the `AppointmentCreateDto` payload.

---

### T-4: Fix Email Fan-Out (ETO + Handler)
- **description**: Populate `AppointmentSubmittedEto` with the 4 stored party emails. Update
  `SubmissionEmailHandler` to use those emails directly (bypassing join-row lookup) and send
  role-appropriate emails: "log in to view" for registered parties, "register as [role]" for
  non-registered.
- **approach**: `code`
- **files-touched**:
  - `src/.../Domain.Shared/Appointments/AppointmentSubmittedEto.cs`
  - `src/.../Application/Appointments/AppointmentsAppService.cs` (ETO population)
  - `src/.../Domain/Appointments/Handlers/SubmissionEmailHandler.cs`
  - `src/.../Domain/Appointments/Notifications/AppointmentRecipientResolver.cs` (fallback cleanup)

**ETO changes** (`AppointmentSubmittedEto.cs`):
Add:
```csharp
public string? PatientEmail { get; set; }
public string? ApplicantAttorneyEmail { get; set; }
public string? DefenseAttorneyEmail { get; set; }
public string? ClaimExaminerEmail { get; set; }
public string? TenantName { get; set; }  // needed for pre-filled register link
```

**AppService**: populate these fields when publishing the ETO:
```csharp
await _localEventBus.PublishAsync(new AppointmentSubmittedEto
{
    AppointmentId = appointment.Id,
    TenantId = appointment.TenantId,
    BookerUserId = appointment.IdentityUserId,
    PatientId = appointment.PatientId,
    RequestConfirmationNumber = appointment.RequestConfirmationNumber,
    AppointmentDate = appointment.AppointmentDate,
    SubmittedAt = Clock.Now,
    PatientEmail = appointment.PatientEmail,
    ApplicantAttorneyEmail = appointment.ApplicantAttorneyEmail,
    DefenseAttorneyEmail = appointment.DefenseAttorneyEmail,
    ClaimExaminerEmail = appointment.ClaimExaminerEmail,
    TenantName = CurrentTenant.Name,
});
```

**SubmissionEmailHandler changes**:
Replace the resolver-based lookup with direct email fan-out. For each of the 4 party emails:
1. Check if `IIdentityUserRepository.FindByNormalizedEmailAsync(email)` returns a user.
2. If user exists AND is in the expected ABP role for that party:
   - Send "Your appointment has been submitted. Confirmation # {X}. Log in at {loginUrl}."
3. If user does not exist OR is in the wrong role:
   - Send "An appointment has been submitted on your behalf. Register as a {role} at {registerUrl}."
   - `registerUrl` = `{authServerUrl}/Account/Register?__tenant={TenantName}&email={email}&returnUrl=...`

Keep the existing office+booker email path as-is (does not change).

Role name constants to match: use ABP role names `patient`, `applicantattorney`, `defenseattorney`,
`claimexaminer` (verify exact strings against seed data).

**Recipient resolver**: no structural change needed; resolver continues to serve status-change
emails which still rely on join rows (those exist by the time a status change fires).

---

### T-5: Upsert Non-Registered Parties (Frontend Guard Removal)
- **description**: The Angular upsert calls currently bail if `IdentityUserId` is missing.
  Remove or relax that guard so that when a party email IS in the system under the correct role
  (even if the booker didn't "Load" them from the dropdown), the upsert still fires.
  Non-registered parties still skip the upsert (no join row needed -- email-only fan-out covers them).
- **approach**: `code`
- **files-touched**: `angular/src/app/appointments/appointment-add.component.ts`

**Current guard** (line 1576 for DA, similar for AA):
```ts
if (!appointmentId || !raw.defenseAttorneyEnabled || !raw.defenseAttorneyIdentityUserId) {
  return;
}
```

**Change**: keep the guard only for `!raw.defenseAttorneyEnabled`; check separately whether
`defenseAttorneyIdentityUserId` is present to decide if the upsert fires.
The email fan-out (T-4) handles the non-registered case independently.
```ts
if (!appointmentId || !raw.defenseAttorneyEnabled) return;
if (!raw.defenseAttorneyIdentityUserId) return; // no registered user -- email fan-out covers it
```
(No functional change yet -- this is a no-op refactor that documents intent. The real change is
in T-4 and T-6 where non-registered parties get contacted differently.)

---

### T-6: Auto-Link on Registration
- **description**: When a user completes registration, find any appointments where their email
  matches a stored party email slot under the correct role, and create the join row.
- **approach**: `code`
- **files-touched**:
  - `src/.../Application/ExternalSignup/ExternalSignupAppService.cs`
  - (possibly a new `AutoLinkService` extracted if the method gets long)

**Logic in `RegisterAsync`** (after user is created):
```csharp
// Determine party slot from the user's role
var role = await _identityUserManager.GetRolesAsync(newUser);
if (role.Contains("patient"))
    await AutoLinkPatientAppointmentsAsync(newUser.Email, newUser.Id);
else if (role.Contains("applicantattorney"))
    await AutoLinkApplicantAttorneyAppointmentsAsync(newUser.Email, newUser.Id);
else if (role.Contains("defenseattorney"))
    await AutoLinkDefenseAttorneyAppointmentsAsync(newUser.Email, newUser.Id);
else if (role.Contains("claimexaminer"))
    await AutoLinkClaimExaminerAppointmentsAsync(newUser.Email, newUser.Id);
```

Each `AutoLink*` method queries appointments where `{PartyEmail} == email` AND no join row
exists yet, then creates the join row using the existing upsert path.

Verify exact role name strings against `CaseEvaluationPermissions.cs` and seeded role data
before implementing.

---

### T-7: Auto-Populate for Patient and AA
- **description**: When the logged-in user is a Patient, pre-fill the patient demographics
  section from their profile. When the logged-in user is an Applicant Attorney, pre-fill the
  AA section and set `applicantAttorneyEnabled = true`.
  Defense Attorney and Claim Examiner: no pre-fill (per D-2 decision).
- **approach**: `code`
- **files-touched**: `angular/src/app/appointments/appointment-add.component.ts`

Note: the component already has `isApplicantAttorney` flag and hides the AA email search row
when set. Verify how it is set (`ngOnInit`) and whether auto-populate already partially works
before implementing. If auto-populate is already done for Patient via the `Existing Patients`
dropdown default behavior, scope this task to AA only.

Investigation step: grep for `isApplicantAttorney` and `currentUser` in the component to
determine what already exists before writing any code.

---

## Risk / Rollback

Blast radius: appointment create flow only. Other flows (read, status change, cancellation) are
unaffected by T-3 through T-7. T-1 and T-2 are UI-only with no server dependency.

Rollback:
- T-1/T-2: revert HTML/TS changes.
- T-3: `dotnet ef database update <migration-before>` + revert entity/DTO changes + revert proxy.
- T-4/T-5/T-6/T-7: revert respective TS/C# files.

## Verification

After all tasks:
1. Build Angular: `npx ng build --configuration development` -- no errors.
2. Build .NET: `dotnet build` -- no errors.
3. Run migrations: `dotnet run --project src/.../DbMigrator`.
4. Full lifecycle smoke test logged in as `maria.rivera@hcs.test` (Patient, Dr Rivera 2):
   - Fill in all 4 party emails (use synthetic `@hcs.test` addresses, one registered per role,
     one unregistered).
   - Submit appointment.
   - Verify: DA section visually matches AA; CE section appears in main form (not modal).
   - Verify: appointment record in DB has all 4 email columns populated.
   - Verify: submission emails land in MailDev (or equivalent dev mailbox) for all 4 parties.
   - Verify: registered party receives "log in" email; unregistered receives "register" email.
5. Update lifecycle tracker: `docs/reports/2026-04-29-wave-2-demo-lifecycle.md` Step 5.1 -> DONE.
