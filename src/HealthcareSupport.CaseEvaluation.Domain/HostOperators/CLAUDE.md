# HostOperators -- host-scoped operators and the intake office-assignment gate

Host-level operators who act across offices (Phase D of the database-per-office epic).
The host Staff Supervisor switches into an office as its admin; the host Intake operator
holds only a gated office-switch capability and lands in the target office as a per-tenant
"Intake Staff" shadow user. This folder holds the server-enforced assignment gate and the
shadow-user provisioning that back those flows.

## What lives here

| File | Purpose |
|---|---|
| `IntakeOfficeAssignment.cs` | Host `FullAuditedAggregateRoot<Guid>` mapping an operator user to an office (`OperatorUserId` + `OfficeId`, unique). Host-only; never lives in an office database. |
| `IIntakeAssignmentChecker.cs` / `IntakeAssignmentChecker.cs` | Deny-by-default gate: `IsAssignedAsync(operatorUserId, officeId)`, read at host scope via `CurrentTenant.Change(null)`. |
| `IIntakeShadowUserProvisioner.cs` / `IntakeShadowUserProvisioner.cs` | Provisions/locates the per-office shadow user the operator lands as. Since 2026-10-27 a shadow is stamped with the operator's host user id (extra property `HostOperatorUserId`) and found by it; a shadow made before the stamp is found by the operator's address (never their username: the System owner and every office admin are all "admin") and stamped then. An unstamped account holding the office admin role is never adopted (it is the office's own administrator), so an IT Admin's pre-stamp shadow is refused and needs a manual fix (none existed on Azure on 2026-10-27). Each ensure syncs the address and leaves exactly one shadow role. Also disables or re-addresses an operator's shadows in every office. |
| `HostOperatorOfficeLogins.cs` | The office side of a host account change (2026-10-27): switched off or deleted locks every shadow; switched back on wakes an Intake operator's assigned ones; a new address moves them; a role change removes Intake assignment rows and locks every shadow. Office staff mail goes to the shadow, so this is what stops it reaching a person who is gone, moved, or readdressed. |
| `IntakeAssignmentRevoker.cs` | Takes an operator off an office: deletes the assignment row and locks the office's shadow user. Used by Staff Assignments' Unassign (one office) and by Staff Management's Change role from Intake Staff to Staff Supervisor (every office, 2026-10-26), so both leave the same state. |

## Conventions

- The assignment gate is server-enforced (never client-trusted): the custom OpenIddict
  impersonation grant (`AuthServer/OpenIddict/HostIntakeImpersonationExtensionGrant`)
  calls the checker and forbids switching into an unassigned office; unassigning revokes
  access.
- Assignment CRUD is exposed via `Application/HostOperators/IntakeAssignmentsAppService`
  with the host-central management UI under `angular/.../host-operators`.
