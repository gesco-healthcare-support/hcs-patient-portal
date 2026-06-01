# NotificationTemplates -- per-tenant email/SMS template management

Seeded catalog of template codes; IT Admin can read + update bodies but cannot create or
delete rows.

## What lives here

| File | Purpose |
|---|---|
| `NotificationTemplate.cs` | Aggregate root; `TemplateCode` string (max 100), `BodyEmail`, `BodySms`, `Subject`, `IsActive` |
| `NotificationTemplateType.cs` | Lookup: Email (seeded) + SMS (seeded); host-scoped |
| `EmailBodyResources.cs` | Loads embedded HTML from `EmailBodies/*.html`; returns `null` on a missing file |
| `EmailSubjects.cs` | `ByCode` dict of OLD-verbatim subjects keyed by template code |
| `NotificationTemplateDataSeedContributor.cs` | Seeds 2 type rows (host pass) then one template row per code per tenant (tenant pass) |
| `EmailBodies/` | Embedded `.html` body files; some codes are stub-only (no HTML file yet) |

Domain.Shared constants: `NotificationTemplateConsts` (`Domain.Shared/NotificationTemplates/`).
Variable substitution utility: `Domain/Notifications/TemplateVariableSubstitutor.cs`.

## Conventions

### Template addressing

Always address templates by the string constants in `NotificationTemplateConsts.Codes`, never
by inline string literals. Codes split into three origin groups:
- DB-managed codes (OLD `TemplateCode` int enum).
- On-disk HTML codes (OLD `EmailTemplate` static class / `wwwroot/EmailTemplates/`).
- NEW codes added post-OLD (Phase 2.A per-recipient dispatch + invite/internal-user welcome).

### Adding a new template code

1. Add the `const string` to `NotificationTemplateConsts.Codes` and append it to `Codes.All`.
2. Add the OLD-verbatim subject to `EmailSubjects.ByCode`.
3. Add the HTML body as `EmailBodies/<Code>.html` marked `<EmbeddedResource>` in the `.csproj`.
4. The seeder picks it up automatically on the next tenant-seed run.

Skipping step 3 is allowed for stub phases -- the seeder falls back to a placeholder body
and the row is still created. The risk: a missing `.html` file is SILENT at runtime;
`TryLoadBody` returns `null`, `LoadBody` throws. Use `TryLoadBody` in the seeder and
`LoadBody` only at startup when a missing file is a build error.

### Seeder update behavior

When a `.html` file exists for a code, the seeder OVERWRITES the DB row's subject + body on
every run. This propagates template corrections to existing tenants automatically. Stubs
(no `.html` file) are written once and then preserved, so IT Admin edits to stub codes
survive re-seed.

### Variable substitution

`TemplateVariableSubstitutor.Substitute(body, variables)` replaces `##Var##` placeholders
using the `IReadOnlyDictionary<string, object?>` passed by each notification handler.
Unknown placeholders are left in place. `DateTime`/`DateTimeOffset` format as `MM/dd/yyyy`
(invariant). Null values render as the empty string.

### AppService is Read + Update only (no Create/Delete)

`NotificationTemplatesAppService` extends `ApplicationService` (known deviation from the
project's `CaseEvaluationAppService` base -- do not replicate). Handlers that need a
template at runtime call `INotificationTemplateRepository.FindByCodeAsync`; the AppService
is for the IT Admin UI only.

## Gotchas

- IMPORTANT: A missing `.html` file under `EmailBodies/` does NOT throw at send time --
  the email goes out with an empty body. Verify the embedded resource exists before shipping
  a new notification handler.
- `TemplateCode`, `TemplateTypeId`, and `Description` are immutable on the update path;
  only `Subject`, `BodyEmail`, `BodySms`, and `IsActive` are writable by IT Admin.
- Three OLD typos were corrected in NEW codes (see `NotificationTemplateConsts.cs` XML doc).
  Do not re-introduce the OLD spellings: `RejectedJoinDeclarationDocument`,
  `AppointmentApprovedStackholderEmails`, `PatientAppointmentCancellationApprvd`.
- `Codes.All` is the seed contract. Adding a code to the `Codes` class without appending it
  to `Codes.All` means the row is never seeded and handlers will throw `NotFound`.

## Related

- `docs/design/it-admin-notification-templates-design.md`
- `docs/runbooks/findings/template-review-2026-05-21.md`
- `docs/runbooks/findings/bugs/OBS-36-23-stub-templates-pending-parity.md`
