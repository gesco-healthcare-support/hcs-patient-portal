---
feature: it-admin-user-management
date: 2026-05-04
phase: 2-frontend (backend InternalUsersAppService + registration flows done; Angular UI delegated to ABP Identity)
status: draft
old-source: patientappointment-portal/src/app/components/user/users/
old-components:
  - list/user-list.component.ts + .html (Users list page)
  - view/user-view.component.ts + .html (shared Add/Edit popup)
  - add/ (external user self-registration page -- not IT Admin use case)
  - edit/ (My Profile / self-service page -- not IT Admin use case)
new-feature-path: angular/src/app/identity/users/ (ABP Identity module, LeptonX)
shell: internal-user-authenticated (top-bar + side-nav)
screenshots: pending (partial -- old/admin/identity-users captured 2026-04-24)
---

# Design: IT Admin -- User Management

## Overview and Delegation Model

In OLD, IT Admin has a custom `/users` page backed by a shared `UserViewComponent`
popup that handles both Add and Edit. In NEW, **user management is delegated to
ABP's built-in LeptonX Identity module** at `/identity/users`. ABP provides:
- Full user list with search, pagination, and column sorting
- Add/Edit modals with role assignment
- Lockout (block user) support
- Soft delete

The status tracker note "ABP-Identity-delegated; capture branding overrides only"
means: NEW does NOT need a custom user management page. The design doc maps the
OLD user list and popup fields onto ABP's standard UI, and flags the ONE custom
behavior that must be added: **internal user creation with auto-generated password
and welcome email**, which ABP does not provide out of the box.

---

## 1. Routes

| | OLD | NEW |
|---|---|---|
| URL | `/users` | `/identity/users` (ABP Identity module standard) |
| Guard | `PageAccess` `applicationModuleId: 8` | ABP `[Authorize(IdentityPermissions.Users.Default)]` (and `Create/Update/Delete` sub-permissions) |

No custom route needed. The side-nav item "Users" links to `/identity/users`.

---

## 2. Shell

Internal-user authenticated shell. Side-nav item "Users" under an IT Admin section.
ABP LeptonX renders the identity pages inside the standard shell automatically.

---

## 3. OLD Page Layout (for reference)

### 3a. Users List Page

```
+-------------------------------------------------------+
| [H2] Users                        [Search input] [X] |
+-------------------------------------------------------+
| [Card]                                               |
|   [Card header]              [Add + button -- right] |
|   [Table]                                            |
|   Email | User Type | Role | Full Name | Status | Verified | Action |
+-------------------------------------------------------+
```

OLD source: `list/user-list.component.html:1-91`

### 3b. OLD Table Columns

| Column | OLD field | Notes |
|---|---|---|
| Email | `emailId` | Login email |
| User Type | `userType` | "Internal"/"External" string |
| Role | `userRole` | Role name (e.g., "Clinic Staff", "Staff Supervisor") |
| Full Name | `userName` | Computed display name |
| Status | `statusId` | "Active" (green) or "Inactive" (red) via custom template |
| Verified | `isVerified` | "Yes" (green) or "No" (red) via custom template |
| Action | -- | Pencil (edit) + trash (delete) icons |

Commented out: Phone No, alternative Status string column.

OLD source: `list/user-list.component.html:40-83`

### 3c. OLD Add/Edit Modal (`UserViewComponent`)

Both Add and Edit reuse the same popup. Header changes based on operation:
- Add: "Add User Details"
- Edit: "Edit User Details"

```
+-----------------------------------+
| Add/Edit User Details       [X]  |
| {{bodyContent}}                  |
+-----------------------------------+
| First Name      [text]           |   <- disabled in Edit
| Last Name       [text]           |   <- disabled in Edit
| Email Id        [email]          |   <- disabled in Edit
| User Role       [select]         |   <- always editable
| Is Active       [checkbox]       |   <- Edit only
+-----------------------------------+
| [Add/Edit User ]   [Cancel]      |
+-----------------------------------+
```

**Fields in ADD mode:**
- First Name, Last Name, Email Id: editable (inside `fieldset [disabled]="isEdit"`, `isEdit=false` in Add)
- User Role select: editable, filtered to `userTypeId == InternalUser`,
  `RoleEnum.ITAdmin` excluded (IT Admin cannot create another IT Admin via this UI)
- Is Active checkbox: NOT shown (`*ngIf="isEdit"` hides it in Add -- new users always Active)

**Fields in EDIT mode:**
- First Name, Last Name, Email Id: DISABLED (fieldset disabled)
- User Role select: editable (IT Admin still excluded from role options)
- Is Active checkbox: shown, pre-filled from `userFormGroup.value.statusId`

**Submit button:** "Add User " or "Edit User " (trailing space in OLD HTML)

**Success toasts:** "User added successfully" / "User updated successfully"

**No password field in Add modal:** IT Admin uses this popup for internal users.
The backend `AddInternalUser()` generates a random `{4chars}@{4chars}` password
and emails it to the new user. The password is never shown in the UI.

OLD source: `view/user-view.component.html:1-50`, `view/user-view.component.ts:81-110`

### 3d. OLD Delete

`dialog.confirmation([userName], "delete")` then `usersService.delete(userId)`.
Soft delete: `StatusId = Status.Delete`. User can no longer log in; audit data
is preserved.

---

## 4. NEW Mapping onto ABP Identity UI

ABP LeptonX's `/identity/users` page provides near-identical functionality:

| OLD behavior | ABP equivalent | Custom work needed |
|---|---|---|
| List with Email, Role, Status, search, pagination | ABP identity list -- Email, Username, Roles, Active/Locked columns | Map column headers; no new code |
| Add Internal User (First Name, Last Name, Email, Role) | ABP "New User" modal -- Username, Email, Name, Surname, Password, Roles, Active flag | Custom `InternalUsersAppService.CreateInternalUserAsync` (see Section 5) |
| Edit User (Name/Email disabled, Role editable) | ABP "Edit User" modal -- same fields, all editable | Minor: ABP allows editing email; OLD did not. Flag to Adrian (Exception 1) |
| Is Active checkbox on Edit | ABP "Active" toggle on Edit modal | No custom work -- ABP already has it |
| Block external user | ABP Lockout: `IsActive = false` on external user's identity record | IT Admin can toggle Active on any user via ABP |
| Verified column | ABP `EmailConfirmed` property | ABP list does not show this by default; add via custom column or omit (Exception 2) |
| Delete (soft) | ABP soft delete via `ISoftDelete` | No custom work |
| IT Admin excluded from role list | ABP permission system prevents role elevation to IT Admin for non-IT Admin users | No custom work -- ABP role management is permission-gated |

---

## 5. Custom Behavior: `IInternalUsersAppService`

ABP's create-user flow requires IT Admin to type a password. OLD generates it
automatically and emails it. NEW must replicate this:

### `CreateInternalUserAsync` contract (per parity audit)

1. Accept `CreateInternalUserDto { Email, FirstName, LastName, RoleId }` -- no password field.
2. Generate `{4chars}@{4chars}` password via `RandomNumberGenerator` (strict parity with OLD).
3. Call `UserManager.CreateAsync(user, password)`.
4. Set `EmailConfirmed = true` (internal users auto-verified, no email confirmation step).
5. Set `IsExternalUser = false` on the identity user's extra properties.
6. Call `UserManager.AddToRoleAsync(user, roleName)`.
7. Publish `InternalUserCreatedEto { UserId, Email, GeneratedPassword }`.
8. Email welcome handler sends template `AddInternalUser` with: username + generated password.

**Angular UI for this custom flow:**
Rather than using ABP's standard "New User" modal (which asks for a password), the
"Add +" button should open a **custom Add Internal User modal** with only:
- First Name, Last Name, Email, Role select
- No password field
- Subtitle: "A welcome email with login credentials will be sent automatically."

This custom modal calls `POST api/app/internal-users` (the custom endpoint), not
ABP's standard `POST api/identity/users`.

For **Edit** and **Delete**: use ABP's standard endpoints. No custom work.
For **Block/Unblock external users**: use ABP's `IsActive` toggle on the external
user's identity record. IT Admin navigates to `/identity/users`, finds the external
user, and toggles Active.

---

## 6. Role Visibility Matrix

| Role | Access |
|---|---|
| IT Admin | Full CRUD on internal users; can block/unblock external users |
| Staff Supervisor | No access to `/identity/users` |
| Clinic Staff | No access |
| External users | No access |

---

## 7. Branding Tokens

| Element | Token |
|---|---|
| Page heading | LeptonX default -- `--brand-primary` for heading |
| Modal header | LeptonX standard modal header |
| Add button | `--brand-primary` via `btn-primary` |
| Status "Active" label | `--status-approved` (green) |
| Status "Inactive" label | `--status-rejected` (red) |

ABP LeptonX already applies the app's LeptonX theme to the identity pages.
No custom CSS overrides are needed beyond ensuring the LeptonX theme uses the
Gesco brand tokens from `_design-tokens.md`.

---

## 8. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Edit user -- Email field | Email disabled in Edit modal | ABP Edit User allows changing email | ABP doesn't easily disable individual fields in its identity modal; consider this a minor deviation or customize the modal. Surface to Adrian |
| 2 | Verified column in list | OLD shows "Verified Yes/No" as a colored badge | ABP list does not show `EmailConfirmed` column by default | For internal users, `EmailConfirmed` is always true (set on create). Column adds no value. Omit, or add as an informational-only column |
| 3 | Auto-generated password with `{4chars}@{4chars}` format | OLD: `a3F9@k2P` style | Match the format exactly | Strict parity: the format is the documented behavior in the parity audit. A stronger password format is desirable security-wise but parity wins; mark as `// PARITY-FLAG: weak password format` |
| 4 | IT Admin excluded from role dropdown in Add/Edit | `roleId != RoleEnum.ITAdmin` filter on lookups | ABP role management: IT Admin role assignment may be permission-controlled separately | Verify ABP's role assignment permissions cover this. If not, filter the role list at `InternalUsersAppService` level |
| 5 | Welcome email subject "Welcome to socal" (lowercase) | Hardcoded `"Welcome to socal"` | Replace with `"Welcome to {ClinicName}"` (from `SystemParameters` or ABP tenant name) | Per parity audit: the "socal" string is a tenant-specific value, not a brand name. Lowercase preserved for parity but the template variable is a deliberate improvement |

---

## 9. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `list/user-list.component.html` | 1-91 | Users list (search, columns, action) |
| `list/user-list.component.ts` | 59-64 | `showUserAddComponent()` passes `UserTypeEnum.InternalUser` |
| `list/user-list.component.ts` | 66-78 | `deleteUser()` inline confirmation |
| `view/user-view.component.html` | 1-50 | Shared Add/Edit popup (all fields, Is Active condition) |
| `view/user-view.component.ts` | 45-110 | `ngOnInit()` (Add vs Edit branch, role filter) + `manageUser()` |
| `users.routing.ts` | 10 | `applicationModuleId: 8` |
| `docs/parity/it-admin-user-management.md` | all | Full parity audit (AddInternalUser flow, block external user, ABP mapping) |

---

## 10. Verification Checklist

- [ ] IT Admin navigates to `/identity/users` and sees the user list
- [ ] List shows Email, User Type, Role, Full Name, Active status columns
- [ ] Search by name or email filters the list
- [ ] "Add +" button opens the custom Add Internal User modal (not ABP's standard modal)
      with First Name, Last Name, Email, Role select -- no password field
- [ ] Saving creates the internal user; welcome email arrives at the specified address
      with auto-generated `{4chars}@{4chars}` credentials
- [ ] New internal user logs in with emailed password without an email-verify step
- [ ] Edit pencil opens ABP's Edit User modal; changes to role and active state save
- [ ] Toggling Active=false on an internal user prevents login for that user
- [ ] IT Admin locates an external user in the list and toggles Active=false (block)
- [ ] Blocked external user cannot log in; receives a clear error message
- [ ] IT Admin toggles external user Active=true (unblock); user can log in again
- [ ] Delete icon (or ABP's delete action) soft-deletes the user; record preserved
- [ ] IT Admin cannot assign the IT Admin role to another user (role excluded from dropdown)
- [ ] Non-IT-Admin roles see 403 when accessing `/identity/users`
