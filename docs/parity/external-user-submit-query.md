---
feature: external-user-submit-query
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\UserQueryModule\UserQueryDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\UserQuery\UserQueriesController.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\user-query\
old-docs:
  - socal-project-overview.md (lines 435-437)
  - data-dictionary-table.md (UserQueries)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: ClinicStaff / StaffSupervisor / ITAdmin (recipients); any logged-in user submits
depends-on: []
required-by: []
---

# External user (and any logged-in user) -- Submit query

## Purpose

A logged-in user (any role) submits a free-text message to clinic staff. System triggers an email to the staff inbox. No further tracking; no thread or reply mechanism. Like a basic contact form, but auth-gated.

**Strict parity with OLD.**

## OLD behavior (binding)

Per spec lines 435-437:

> "A logged in user will be able to submit a query to the Clinic staff after logging in to the system. The user will enter his message and submit the query. This will trigger an email to the staff email account. The system will not track any further correspondence related to this feature."

### Schema (`UserQueries` table)

- `UserQueryId` (PK)
- `UserId` (FK, the submitter)
- `Message` (varchar 500)
- `CreatedDate, CreatedById, ModifiedDate, ModifiedById`

### Endpoint

- `POST /api/UserQueries` -- create a query

### Email trigger

- Sends to a configured staff email address (TO VERIFY where this is configured -- likely `SystemParameters` or `appsettings`).
- Subject: "Patient Appointment Portal - User Query" (TO VERIFY).
- Body: includes user's name, role, email, and the message text.

### Critical OLD behaviors

- **No reply mechanism in the system.** OLD spec explicitly says "system will not track any further correspondence".
- **Stored as a row** in `UserQueries` -- audit trail of who submitted what, even though no reply tracked.
- **Logged-in users only** (any role).
- **500-char limit** on message.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/UserQueryModule/UserQueryDomain.cs` (179 lines) | Add + email trigger + CRUD |
| `PatientAppointment.Api/Controllers/Api/UserQuery/UserQueriesController.cs` | API |
| `patientappointment-portal/.../user-query/user-queries/...` | Form UI (single textarea + submit) |

## NEW current state

- TO VERIFY: NEW has `UserQueries/` folder.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| `UserQuery` entity | OLD | TO VERIFY | **Add `UserQuery : FullAuditedEntity<Guid>, IMultiTenant`** with Message (500 chars max), UserId | B |
| `IUserQueriesAppService` | OLD | -- | **Add `SubmitAsync(SubmitUserQueryDto { Message })`** | B |
| Email trigger on submit | OLD | -- | **Subscribe to `UserQuerySubmittedEto` event** -> send email to configured staff inbox | I |
| Configured staff inbox | OLD: TO VERIFY config location | -- | **Add `appsettings.json` setting `Notifications:UserQueryRecipientEmail`** | I |
| Permissions | OLD: any logged-in user | -- | **`[Authorize]` on `SubmitAsync`** (any authenticated user) | -- |
| List/admin view | -- | -- | **Add `GetListAsync` for IT Admin** to see history (audit trail) | I |
| 500-char message limit | OLD | -- | **`[StringLength(500)]` on DTO** | I |

## Internal dependencies surfaced

- Notification template for the email (the body needs user identity + message).

## Branding/theming touchpoints

- Form UI (logo, primary color, textarea styling).
- Email template (subject + body).

## Replication notes

### ABP wiring

- Standard CRUD AppService; only `SubmitAsync` exposed publicly.
- `UserQuerySubmittedEto` published on submit; `UserQueryEmailHandler` sends to configured inbox.
- Configured email address via ABP setting (per-tenant or app-wide).

### Verification

1. Logged-in user submits query with message -> success
2. Configured staff inbox receives email with user details + message
3. Try to submit with no auth -> 401
4. Try to submit message > 500 chars -> validation error
