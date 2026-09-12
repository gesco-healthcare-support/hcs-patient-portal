# Branding -- per-office display name and logo (host-scoped)

Per-office branding (display name + logo) shown in the app shell post-auth and on the
login page pre-auth (resolved by subdomain). Added in the database-per-office epic
(Phase E). The entity is HOST-scoped, not tenant-scoped: under database-per-office a
tenant setting would land in the office database and force an office-DB hop at login, so
branding lives in the host database and is read by subdomain before authentication.

## What lives here

| File | Purpose |
|---|---|
| `OfficeBranding.cs` | Host `FullAuditedAggregateRoot<Guid>` keyed by `OfficeId` (unique). Holds `DisplayName`, `LogoBlobName`, `LogoContentType`. Methods: `SetDisplayName` (trims/clears + length check), `SetLogo`, `ClearLogo`. |

## Conventions

- Mapped in the `IsHostDatabase()` block of `CaseEvaluationDbContext` (never in an office
  database). The logo image is a host-scoped blob (`OfficeLogosContainer`) keyed by
  office id.
- Read at host scope via `CurrentTenant.Change(null)`; see `Application/Branding/
  BrandingAppService.cs` (anonymous subdomain resolve + gated host-central edit) and the
  AuthServer `BrandingHead` layout-hook that injects the per-office `--lpx-logo` CSS var
  on the login page.
- Length constants live on the entity (`DisplayNameMaxLength`, `LogoBlobNameMaxLength`,
  `LogoContentTypeMaxLength`).
