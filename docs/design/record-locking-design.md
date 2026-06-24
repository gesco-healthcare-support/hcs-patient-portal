---
feature: record-locking
date: 2026-05-04
phase: 2-frontend (ChangeDetectionGuard = unsaved-changes protection; full pessimistic lock was never activated in OLD)
status: draft
old-source: patientappointment-portal/src/app/domain/authorization/change-detection-guard.ts + user-authorization.service.ts
new-feature-path: n/a (no equivalent in angular/src/app/ -- see Section 4 for recommended NEW approach)
shell: cross-cutting (applies to any form page)
screenshots: n/a (no visible UI element)
---

# Design: Record Locking / Unsaved Changes Guard

## Overview

"Record locking" in the context of this app covers two related but distinct behaviors:

1. **Unsaved-changes detection (ChangeDetectionGuard):** Warns the user when they try to
   navigate away from a form with unsaved changes. This WAS active in OLD.

2. **Pessimistic record locking (api/recordlocks):** Prevents two users from editing the
   same record simultaneously by creating a server-side lock. This was PARTIALLY
   implemented in OLD but the lock-creation code was commented out and **never shipped**.

For Phase 19b UI build, only (1) needs to be implemented in NEW. The pessimistic lock
API does not need to be ported because it was never active in OLD.

---

## 1. What OLD Actually Did

### ChangeDetectionGuard (active)

Located at `domain/authorization/change-detection-guard.ts`.

`CanDeactivate` guard that fires when a user navigates away from a form route. Flow:

1. Check `ApplicationConfiguration.get("changeDetection")` -- if feature flag is OFF,
   always allow navigation (no dialog shown).
2. If ON: call `component.canDeactivate()` to check whether the form has unsaved changes.
3. If unsaved changes: show `RxDialog.confirmation([], 'dataLost')` dialog.
4. If user confirms "Yes, leave": call `unLockRecord()` then allow navigation.
5. If user cancels: stay on the current page.

The "data lost" dialog text was from `ApplicationConfiguration.get("validation.message.custom.dataLost")`.

### api/recordlocks (never activated)

`UserAuthorizationService` had:
- `checkLock(data)` -- `POST api/recordlocks` -- creates a lock record for a given `{applicationModuleId, mainRecordId, childModuleName}`.
- `unLockRecord(data)` -- `DELETE api/recordlocks{moduleId}/{recordId}/{childModuleName}` -- removes the lock.

The `checkLock()` call was present in `page-access.ts` but **commented out**:
```
//    var lockRecord = { applicationModuleId: ..., childModuleName: ..., mainRecordId: ... };
//    this.authorizationService.checkLock(lockRecord).subscribe(t => {
```

Lock creation was never called. `unLockRecord` was called (in `ChangeDetectionGuard.unLock()`),
but since no lock was ever created, these DELETE calls were no-ops.

**Conclusion:** The only active feature was the unsaved-changes navigation warning.
The pessimistic lock never existed at the app layer.

---

## 2. Form-Level Unsaved-Changes Behavior

Components that implement `ComponentCanDeactivate` expose:
```ts
canDeactivate(): boolean | Observable<boolean>
```

- Returns `true` if no unsaved changes (allow navigation).
- Returns `false` if unsaved changes exist (show dialog).

This pattern was applied to form pages (appointment edit, system parameters edit, etc.).

---

## 3. API

| Operation | OLD | NEW |
|---|---|---|
| Create lock | `POST api/recordlocks` -- COMMENTED OUT; never called | Not needed |
| Remove lock | `DELETE api/recordlocks{moduleId}/{recordId}/{childModuleName}` -- called but no-op | Not needed |
| Unsaved changes check | `component.canDeactivate()` | Angular `CanDeactivate` guard |

---

## 4. NEW Recommended Approach

Implement unsaved-changes protection using Angular's standard `CanDeactivate` guard:

```ts
// shared/guards/unsaved-changes.guard.ts
export const unsavedChangesGuard: CanDeactivateFn<ComponentWithUnsavedChanges> =
  (component) => {
    if (component.hasUnsavedChanges?.()) {
      return confirm('You have unsaved changes. Leave anyway?');
    }
    return true;
  };
```

Apply to routes that have edit forms:
- Appointment edit form
- System parameters edit
- Doctor edit modal (if full-page)
- Any other form with significant data entry

Do NOT implement pessimistic record locking (`api/recordlocks`) -- it was never active
in OLD and adds significant complexity for no current user-facing benefit.

---

## 5. Role Visibility

Cross-cutting. Applies to any form page accessed by any internal user.
External users: apply to appointment booking form (multi-step form navigation).

---

## 6. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Pessimistic record locking | `POST api/recordlocks` to create server-side lock | Not implemented | Lock creation was commented out in OLD and never shipped; zero parity requirement |
| 2 | `RxDialog.confirmation([], 'dataLost')` | Custom rx-dialog with "data lost" confirmation | Angular `confirm()` or `MatDialog` | Framework replacement; same UX intent |
| 3 | `changeDetection` feature flag | Guard only active when `ApplicationConfiguration.get("changeDetection")` is true | Always active in NEW (remove the feature flag) | The flag was a development convenience in OLD; NEW should always warn on unsaved changes |

---

## 7. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `domain/authorization/change-detection-guard.ts` | 1-83 | `CanDeactivate` implementation: canDeactivate check, dialog, unLock call |
| `domain/authorization/user-authorization.service.ts` | 35-48 | `checkLock()` (POST) and `unLockRecord()` (DELETE) -- `api/recordlocks` |
| `domain/authorization/page-access.ts` | (commented lines) | Lock creation commented out; confirms lock was never activated |

---

## 8. Verification Checklist

- [ ] Navigating away from appointment edit form with unsaved changes shows confirmation dialog
- [ ] Confirming "leave" in the dialog discards changes and navigates away
- [ ] Canceling the dialog keeps the user on the form
- [ ] Navigating away with NO unsaved changes does NOT show a dialog
- [ ] Form routes that should have the guard: appointment edit, system parameters, doctor edit
- [ ] No server-side `api/recordlocks` calls are made (Exception 1)
