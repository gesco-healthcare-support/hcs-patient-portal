# UserQueries -- Contact-Us / "Help / Need Question?" submissions

Submit-only free-text questions from external users, captured through the
navbar Help modal. Port of OLD `spm.UserQueries`. There is no read / list /
update / delete surface -- a query is written once and emailed to staff.

## What lives here

| Layer | Key file | Purpose |
|---|---|---|
| Domain | `UserQuery.cs` | Aggregate root: `FullAuditedAggregateRoot<Guid>`, `IMultiTenant`, `[Audited]`; single `Message` field (required, max `UserQueryConsts.MessageMaxLength` = 500) |
| Domain | `UserQueryManager.cs` | `CreateAsync(message)` -- stamps id + `InsertAsync(autoSave: true)`; the entity is the sole invariant holder |
| Contracts | `Application.Contracts/UserQueries/` | `UserQueryCreateDto`, `IUserQueryAppService` |
| Application | `Application/UserQueries/UserQueryAppService.cs` | Submit-only; persists then publishes `UserQuerySubmittedEto` (commit-then-mail) |
| Application | `Application/Notifications/Handlers/UserQuerySubmittedEmailHandler.cs` | Sends the staff notification email on the published event |
| HttpApi | `HttpApi/Controllers/UserQueries/UserQueryController.cs` | Manual controller, single `POST api/app/user-queries` |

## Conventions

### Submitter + tenant come from ABP, not columns

OLD stored a redundant `UserId` FK alongside `CreatedById`/`CreatedDate` (all
the same submitter). ABP's audit columns (`CreatorId`/`CreationTime`) capture
the submitter and the `IMultiTenant` filter captures the tenant, so no extra FK
is persisted. OLD's transient `RequestConfirmationNumber`/`AppointmentId`
routing inputs are not stored -- they travel on `UserQuerySubmittedEto` instead.

### Authorization -- any authenticated user

`UserQueryAppService` carries class-level `[Authorize]` only (no per-action
permission): external roles are not granted feature permissions, and the navbar
surfaces the Help button only to them. Mirrors OLD's `UserTypeEnum.ExternalUser`
gate.

### Commit-then-mail

The AppService persists via the manager, then publishes the submitted event so
the email is a handler side-effect after commit -- matching OLD's order and
every other NEW notification flow. Do not send mail inline in the AppService.

## Related

- docs/business-domain/ (notification flows)
