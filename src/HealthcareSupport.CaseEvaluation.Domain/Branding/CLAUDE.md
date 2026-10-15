# Branding -- per-office display name and logo (host-scoped)

Per-office branding (display name + logo) shown in the app shell post-auth and on the
login page pre-auth (resolved by subdomain). Added in the database-per-office epic
(Phase E). The entity is HOST-scoped, not tenant-scoped: under database-per-office a
tenant setting would land in the office database and force an office-DB hop at login, so
branding lives in the host database and is read by subdomain before authentication.

## What lives here

| File | Purpose |
|---|---|
| `OfficeBranding.cs` | Host `FullAuditedAggregateRoot<Guid>` keyed by `OfficeId` (unique). Holds `DisplayName`, `LogoBlobName`, `LogoContentType`, and the packet letterhead fields (2026-10-09). Methods: `SetDisplayName` (trims/clears + length check), `SetLogo`, `ClearLogo`, `SetLetterhead` / `GetLetterhead`. |
| `OfficeLetterheadValues.cs` | `OfficeLetterheadValues` record: the letterhead fields as entered, nulls meaning "derive it". |
| `OfficeLetterhead.cs` | `OfficeLetterhead` record: the EFFECTIVE letterhead a packet prints. `Compose` fills every gap -- physician defaults to "Dr. {First} {Last}" from the office's doctor, the heading to the physician, the practice name to the display name -- so a new practice prints its own doctor with no setup. Optional lines (fax, fee, addresses) are empty strings and the templates omit them. |
| `OfficeLetterheadResolver.cs` | `OfficeLetterheadResolver`: reads the doctor from the OFFICE database and the branding row from the HOST database (`CurrentTenant.Change(null)`), returns `OfficeLetterhead`; `LoadSourceAsync` returns the raw `OfficeLetterheadSource` for the editor. Used by `PacketTokenResolver` (the `##Office.*##` tokens) and `OfficeLetterheadAppService`. |

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
- **Packet letterhead (2026-10-09, walkthrough Q5).** The packet templates in
  `tools/packet-templates` used to hardcode one practice's letterhead, physician, address and
  phone, so every office's packets carried that practice's identity. They now print
  `##Office.*##` tokens (`PacketTokenMap`), and a template line whose token is empty is
  omitted (`data-if`). Field lengths are in `Domain.Shared/Branding/OfficeLetterheadConsts.cs`
  because the update DTO validates against them. The host migration `Added_OfficeLetterhead`
  pre-fills Falkinstein's row with exactly what its packets printed before.
