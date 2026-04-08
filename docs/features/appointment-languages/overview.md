<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/AppointmentLanguages/CLAUDE.md on 2026-04-08 -->

# Appointment Languages

Host-scoped lookup table for languages used in appointments (e.g., interpreter requirements). Referenced by `Patient.AppointmentLanguageId`. Simple name-only entity with standard CRUD. Has Angular UI for management.

## Entity Shape

```
AppointmentLanguage : FullAuditedEntity<Guid>     (NO IMultiTenant — host-scoped)
└── Name : string [max 50, required]
```

## Multi-tenancy

**IMultiTenant: No.** Host-scoped lookup. DbContext config inside `IsHostDatabase()`.

## Permissions

```
CaseEvaluation.AppointmentLanguages          (Default)
CaseEvaluation.AppointmentLanguages.Create
CaseEvaluation.AppointmentLanguages.Edit
CaseEvaluation.AppointmentLanguages.Delete
```

## Inbound FKs

- `Patient.AppointmentLanguageId` → SetNull. The patient's preferred language for interpreter needs.

## Business Rules

1. **CreateDto defaults Name to "English"** -- the DTO has a default value, though the AppService does not enforce it.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| AppointmentLanguageComponent | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.component.ts` | `/appointment-management/appointment-languages` | List view |
| AbstractAppointmentLanguageComponent | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.abstract.component.ts` | -- | Base directive |
| AppointmentLanguageDetailModalComponent | `angular/src/app/appointment-languages/appointment-language/components/appointment-language-detail.component.ts` | -- | Modal for create/edit |

**Pattern:** ABP Suite abstract/concrete (`AbstractAppointmentLanguageComponent` → `AppointmentLanguageComponent`)

**Forms:**
- name: text (maxLength: 50, required, default: "English")

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.AppointmentLanguages`)
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Create'"` -- create button
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Edit'"` -- edit action
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Delete'"` -- delete action

**Services injected:**
- `ListService`, `AppointmentLanguageViewService`, `AppointmentLanguageDetailViewService`, `PermissionService`

## Known Gotchas

1. **FullAuditedEntity, not AggregateRoot** -- simpler base class
2. **No tests**

## Related Features

- [Patients](../patients/overview.md) -- `Patient.AppointmentLanguageId` references this entity (SetNull)

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentLanguages/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- UI detail: [ui.md](ui.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
