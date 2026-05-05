# Proxy Regen: `getIdentityUserLookup` Fix

Research note for G2.1 (Angular proxy regen after identity merge). Investigates a
broken consumer reference at
`angular/src/app/doctors/doctor/services/doctor-detail.abstract.service.ts:22`
after `chore(proxy): regenerate Angular proxy after identity merge` (commit
`4fa1329`) dropped `getIdentityUserLookup` from the generated `DoctorService`.

**Bottom line:** the field was removed for a reason. The identity-merge commit
`d1bbdab` deliberately stripped `IdentityUserId` from the `Doctor` entity
because OLD treats Doctor as a non-user reference entity managed by Staff
Supervisor. The fix is to delete the consumer line and the matching template
control, NOT to restore the backend method.

---

## 1. OLD-side: doctor edit has NO IdentityUser dropdown

OLD's doctor-edit UI does not link a Doctor to any user account. There is no
endpoint, no DTO field, and no UI control for IdentityUser on the Doctor entity.

### OLD controller

`P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\DoctorManagement\DoctorsController.cs:14-75`
exposes only standard CRUD: `GET /api/Doctors`, `GET /api/Doctors/{id}`,
`POST`, `PUT/{id}`, `PATCH/{id}`, `DELETE/{id}`. No user-lookup endpoint.

The two lookup controllers do NOT lookup users for doctors either:
- `DoctorManagementLookupsController.cs:14-48` exposes `AppointmentTypeLookUps`,
  `DoctorPreferredLocationLookUps`, `DoctorsAvailabilitiesLookUps`,
  `GenderLookUps`, `LocationLookUps`. No user lookup.
- `UserLookupsController.cs:14-48` exposes city/role/gender/state lookups.
  No user-by-email or user-list endpoint.

### OLD Angular doctor-edit

`P:\PatientPortalOld\patientappointment-portal\src\app\components\doctor-management\doctors\edit\doctor-edit.component.ts:51-58`:

```ts
ngOnInit(): void {
  this.doctorsService.group([this.doctorId], [DoctorManagementLookups.genderLookUps,]).then(
    (response: DoctorLookupGroup) => {
      this.doctorLookupGroup = response;
      ...
    });
}
```

Loads ONE lookup: `genderLookUps`.

`doctor-edit.component.html:13-39` renders four fields only: `firstName`,
`lastName`, `email`, `genderId` (radio group). No IdentityUser control.

`doctor.models.ts:6-9`:

```ts
export class DoctorLookupGroup {
  genderLookUps : vGenderLookUp[];
  doctor : Doctor;
}
```

Confirms the form's lookup model carries gender lookups + the doctor record only.

**Verdict:** OLD has no `User` <-> `Doctor` link. Doctor is a reference entity.
Strict parity demands no IdentityUser dropdown on the doctor-edit form.

---

## 2. NEW pre-regen DoctorService DID expose `getIdentityUserLookup`

`git show 4fa1329^:angular/src/app/proxy/doctors/doctor.service.ts`:

```ts
getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
  this.restService.request<any, PagedResultDto<LookupDto<string>>>(
    {
      method: 'GET',
      url: '/api/app/doctors/identity-user-lookup',
      params: { ... },
    },
    { apiName: this.apiName, ...config },
  );
```

Pre-regen URL: `GET /api/app/doctors/identity-user-lookup`. Returned a paged
`LookupDto<string>` for an ABP `<abp-lookup-select>` control.

The pre-regen `GetDoctorsInput` also carried `identityUserId` as a filter
field (visible in the diff above), and `Doctor.cs` had a `Guid? IdentityUserId`
property. All three were generated from the (now-removed) backend surface.

---

## 3. NEW post-regen: method gone, candidates listed

### `proxy/doctors/doctor.service.ts:1-91` (current)

Lookup methods present: `getAppointmentTypeLookup`, `getLocationLookup`,
`getTenantLookup`. No `getIdentityUserLookup`. URL `/api/app/doctors/identity-user-lookup`
no longer exists on the backend.

### Candidate replacements in the regenerated proxy

| Service | Method | Returns | Filter | Fit for doctor-edit dropdown? |
|---|---|---|---|---|
| `proxy/external-users/external-user.service.ts:8-28` | `inviteExternalUser`, `getMyProfile` | -- | -- | NO -- no list/lookup method |
| `proxy/external-signups/external-signup.service.ts:14-22` | `getExternalUserLookup(filter?)` | `ListResultDto<ExternalUserLookupDto>` | external (Patient / Adjuster / Attorney / Accessor) | NO -- external users only; OLD doctor edit doesn't pick a user at all |
| `proxy/users/user-extended.service.ts:131-140` | `getList(GetIdentityUsersInput)` | `PagedResultDto<IdentityUserDto>` | full ABP user list | Possible but oversized; not a `LookupDto<string>` |

`ExternalSignupAppService.GetExternalUserLookupAsync` lists external users
(Patient / Adjuster / Attorney / Accessor). It does NOT list internal staff or
all users. Wiring a doctor record to an external-user account is meaningless
(externals are not internal staff and a Doctor record is not an external-user
linkage).

---

## 4. Backend: `GetIdentityUserLookupAsync` was deliberately removed in `d1bbdab`

`git log --oneline -- src/HealthcareSupport.CaseEvaluation.Application/Doctors/`:

```
d1bbdab chore(domain): remove Doctor user role and AppointmentSendBackInfo
459451b feat(mvp-wave-0): land 8 foundation caps for MVP demo
ce26a83 fix(build): eliminate 480 nullability + RMG012 warnings (B-2.1 fixup of #76) (#80)
4f227d1 Initial commit: Patient Portal (workers' comp IME scheduling)
```

`git show d1bbdab` body:

> Drops Doctor role + Doctor login flow because OLD treats Doctor as a
> non-user reference entity managed by Staff Supervisor on its behalf.
> Internal-role tier collapses to IT Admin (host) + Staff Supervisor +
> Clinic Staff (per tenant).
> ...
> Adds 2 EF migrations dropping the Doctors.IdentityUserId FK + column

The diff for `DoctorsAppService.cs` (this commit) removes:
- the `IdentityUserManager _userManager` field + ctor param
- the `IRepository<IdentityUser, Guid> _identityUserRepository` field + ctor param
- the entire `GetIdentityUserLookupAsync(LookupRequestDto)` method (~10 lines)
- the IdentityUser sync block in `UpdateAsync` (~28 lines)
- the `IdentityUserId` argument from `_doctorManager.CreateAsync` and
  `UpdateAsync`

`Doctor.cs` (current) at `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/Doctor.cs:14-52`:
no `IdentityUserId` property. Constructor signature is
`Doctor(Guid id, string firstName, string lastName, string email, Gender gender)`.

`IDoctorsAppService.cs` (current) at `.Application.Contracts/Doctors/IDoctorsAppService.cs:10-21`:
9 methods (`GetListAsync`, `GetWithNavigationPropertiesAsync`, `GetAsync`,
`GetTenantLookupAsync`, `GetAppointmentTypeLookupAsync`, `GetLocationLookupAsync`,
`DeleteAsync`, `CreateAsync`, `UpdateAsync`). No `GetIdentityUserLookupAsync`.

`DoctorCreateDto.cs:8-25`: no `IdentityUserId`. Same for `DoctorUpdateDto`,
`DoctorWithNavigationPropertiesDto`, `GetDoctorsInput` (per commit diff
showing `IdentityUserId` removed from each).

**The current `Doctor` entity is now structurally aligned with OLD.** The
proxy regen at `4fa1329` correctly reflects this aligned state.

> NOTE: the Domain `CLAUDE.md` at
> `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md` still
> describes the pre-merge entity (claims `IdentityUserId` exists, claims
> `UpdateAsync` syncs the IdentityUser, etc.). That doc is stale. Per branch
> CLAUDE.md, layer-level CLAUDE.md files describe the current crude state and
> are NOT prescriptive -- the OLD app and source code are the binding sources.
> This doc is fine as a navigational artifact but should be flagged for refresh
> when Adrian touches the Doctors feature.

---

## 5. The consumer at `doctor-detail.abstract.service.ts:22`

Full context (`angular/src/app/doctors/doctor/services/doctor-detail.abstract.service.ts:11-65`):

```ts
export abstract class AbstractDoctorDetailViewService {
  ...
  public readonly proxyService = inject(DoctorService);
  public readonly list = inject(ListService);

  public readonly getAppointmentTypeLookup = this.proxyService.getAppointmentTypeLookup;
  public readonly getLocationLookup        = this.proxyService.getLocationLookup;
  public readonly getIdentityUserLookup    = this.proxyService.getIdentityUserLookup;  // <-- BROKEN line 22
  public readonly getTenantLookup          = this.proxyService.getTenantLookup;
  ...

  buildForm() {
    const { firstName, lastName, email, gender, identityUserId, tenantId } =
      this.selected?.doctor || {};
    ...
    this.form = this.fb.group({
      ...
      identityUserId: [identityUserId ?? null, []],   // <-- field on the form
      tenantId:       [tenantId ?? null, []],
      ...
    });
  }
}
```

The form control feeds `doctor-detail.component.html:76-85`:

```html
<div class="mb-3">
  <label class="form-label" for="doctor-identity-user-id">
    {{ '::IdentityUser' | abpLocalization }}
  </label>
  <abp-lookup-select
    cid="doctor-identity-user-id"
    formControlName="identityUserId"
    [getFn]="service.getIdentityUserLookup"
  ></abp-lookup-select>
</div>
```

`<abp-lookup-select [getFn]>` expects a function that takes
`LookupRequestDto` and returns `Observable<PagedResultDto<LookupDto<TKey>>>`.

**What the consumer needed:** a list of identity users (id + display name) for
a single-select dropdown bound to `Doctor.IdentityUserId`.

**What now exists:** no such field, no such backend method, no such proxy
method, and no such control in OLD's doctor-edit form.

---

## 6. Recommended fix: option (c) -- delete the references

Reframing the choice: this is not (a) restore on backend or (b) retarget to a
different lookup. Both options preserve dead UI. The correct fix is

**(c) Delete the IdentityUser dropdown from the doctor-detail form, the
`identityUserId` form control, the `getIdentityUserLookup` reference, and the
field destructure in `buildForm()`. This matches OLD parity and the post-merge
backend contract.**

### Why not (a) restore on backend?

- OLD has no User-Doctor link. Restoring an identity-user-lookup on
  `DoctorAppService` recreates a feature the strict-parity directive
  explicitly excludes (see commit `d1bbdab` rationale: "OLD treats Doctor as
  a non-user reference entity").
- It would also require reverting the EF migrations that dropped the
  `Doctors.IdentityUserId` column.
- Net effect: re-introduce dead schema and UI surface for a flow OLD does not
  have.

### Why not (b) retarget to `ExternalSignupService.getExternalUserLookup`?

- That endpoint lists external users (patient, adjuster, attorney, accessor),
  not internal staff. A Doctor in the new model is provisioned via tenant
  bootstrap (`DoctorTenantAppService`); external users are unrelated to it.
- Retargeting would point the dropdown at a semantically wrong audience and
  silently let a doctor be linked to e.g. a patient-role user. That is worse
  than the current build break.

### Why (c) is correct

- Matches OLD UI exactly: first/last name, email, gender. No user dropdown.
- Aligns the Angular form to the post-merge DTO surface (no `identityUserId`
  on `DoctorCreateDto` / `DoctorUpdateDto`).
- Removes a dead control instead of wiring it to a placeholder, so no parity
  flag is required.

---

## 7. Exact code changes

### 7.1 `angular/src/app/doctors/doctor/services/doctor-detail.abstract.service.ts`

Replace lines 11-65 (relevant parts):

**Remove line 22:**

```ts
public readonly getIdentityUserLookup = this.proxyService.getIdentityUserLookup;
```

**Update `buildForm()` (lines 50-66) -- remove `identityUserId` from the
destructure and the form group:**

```ts
buildForm() {
  const { firstName, lastName, email, gender, tenantId } =
    this.selected?.doctor || {};

  const { appointmentTypes = [], locations = [] } = this.selected || {};

  this.form = this.fb.group({
    firstName: [firstName ?? null, [Validators.required, Validators.maxLength(50)]],
    lastName:  [lastName  ?? null, [Validators.required, Validators.maxLength(50)]],
    email:     [email     ?? null, [Validators.required, Validators.maxLength(49), Validators.email]],
    gender:    [gender    ?? null, [Validators.required]],
    tenantId:  [tenantId  ?? null, []],
    appointmentTypeIds: [appointmentTypes, []],
    locationIds:        [locations, []],
  });
}
```

(Note: also re-evaluate `tenantId` -- the post-merge `Doctor` still has
`TenantId` from `IMultiTenant`, but no UI in OLD edits it. Out of scope for
this fix; leaving it as-is preserves current behavior.)

### 7.2 `angular/src/app/doctors/doctor/components/doctor-detail.component.html`

Remove lines 76-85 (the IdentityUser block):

```html
<div class="mb-3">
  <label class="form-label" for="doctor-identity-user-id">
    {{ '::IdentityUser' | abpLocalization }}
  </label>
  <abp-lookup-select
    cid="doctor-identity-user-id"
    formControlName="identityUserId"
    [getFn]="service.getIdentityUserLookup"
  ></abp-lookup-select>
</div>
```

### 7.3 (Verify) localization keys

Check `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`
for an `IdentityUser` key. If only used by this control, remove it. If
referenced elsewhere, leave it.

---

## 8. Acceptance criteria

- [ ] `npx ng build --configuration development` succeeds with no
  TypeScript errors referencing `getIdentityUserLookup` or `identityUserId`
  on the Doctor form.
- [ ] Doctor list page loads (`/doctors` route) without console errors.
- [ ] Open the doctor-create modal: form shows First Name, Last Name, Email,
  Gender, AppointmentTypes tab, Locations tab. NO IdentityUser dropdown.
- [ ] Open the doctor-edit modal for an existing doctor: same fields render,
  pre-populated from `DoctorWithNavigationPropertiesDto`.
- [ ] Submit create + update -- both succeed against the post-merge
  `IDoctorsAppService.CreateAsync` / `UpdateAsync` (which take no
  `IdentityUserId`).
- [ ] No 404 to `/api/app/doctors/identity-user-lookup` in the network tab.

---

## 9. Parity-flag

None required. Removal aligns the new app with OLD's UI exactly. Track only as
the resolved consumer-side cleanup of the G2.1 proxy regen task.

---

## Sources cited

OLD:
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\DoctorManagement\DoctorsController.cs:14-75`
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Lookups\DoctorManagementLookupsController.cs:14-48`
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Lookups\UserLookupsController.cs:14-48`
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\doctor-management\doctors\edit\doctor-edit.component.ts:51-58`
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\doctor-management\doctors\edit\doctor-edit.component.html:13-39`
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\doctor-management\doctors\domain\doctor.models.ts:1-9`

NEW (current):
- `angular/src/app/doctors/doctor/services/doctor-detail.abstract.service.ts:11-105`
- `angular/src/app/doctors/doctor/components/doctor-detail.component.html:76-85`
- `angular/src/app/proxy/doctors/doctor.service.ts:1-91`
- `angular/src/app/proxy/external-users/external-user.service.ts:1-28`
- `angular/src/app/proxy/external-signups/external-signup.service.ts:14-22`
- `angular/src/app/proxy/users/user-extended.service.ts:131-140`
- `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorsAppService.cs:1-125`
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/IDoctorsAppService.cs:10-21`
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/DoctorCreateDto.cs:8-25`
- `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/Doctor.cs:14-52`

NEW (pre-regen, via git):
- `git show 4fa1329^:angular/src/app/proxy/doctors/doctor.service.ts`
  (showed `getIdentityUserLookup` calling
  `GET /api/app/doctors/identity-user-lookup`)
- `git show d1bbdab -- src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorsAppService.cs`
  (the deliberate removal of `GetIdentityUserLookupAsync` and the IdentityUser
  sync block, with rationale "OLD treats Doctor as a non-user reference
  entity")
