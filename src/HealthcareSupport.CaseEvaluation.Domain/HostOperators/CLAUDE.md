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
| `IIntakeShadowUserProvisioner.cs` / `IntakeShadowUserProvisioner.cs` | Provisions/locates the per-office Intake Staff shadow user (username == operator email) the operator lands as. |

## Conventions

- The assignment gate is server-enforced (never client-trusted): the custom OpenIddict
  impersonation grant (`AuthServer/OpenIddict/HostIntakeImpersonationExtensionGrant`)
  calls the checker and forbids switching into an unassigned office; unassigning revokes
  access.
- Assignment CRUD is exposed via `Application/HostOperators/IntakeAssignmentsAppService`
  with the host-central management UI under `angular/.../host-operators`.
