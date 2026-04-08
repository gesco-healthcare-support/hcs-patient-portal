# AppointmentLanguages

Host-scoped lookup table for languages used in appointments (e.g., interpreter requirements). Referenced by Patient.AppointmentLanguageId. Simple name-only entity with standard CRUD. Has Angular UI for management.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/AppointmentLanguages/AppointmentLanguageConsts.cs` | NameMaxLength=50, default sort |
| Domain | `src/.../Domain/AppointmentLanguages/AppointmentLanguage.cs` | Entity (FullAuditedEntity, NOT AggregateRoot) — no IMultiTenant |
| Domain | `src/.../Domain/AppointmentLanguages/AppointmentLanguageManager.cs` | DomainService — create/update |
| Contracts | `src/.../Application.Contracts/AppointmentLanguages/` | DTOs, service interface |
| Application | `src/.../Application/AppointmentLanguages/AppointmentLanguagesAppService.cs` | CRUD with proper permission enforcement |
| HttpApi | `src/.../HttpApi/Controllers/AppointmentLanguages/AppointmentLanguageController.cs` | 5 endpoints at `api/app/appointment-languages` |
| Angular | `angular/src/app/appointment-languages/` | List + detail modal (abstract/concrete pattern) |

## Entity Shape

```
AppointmentLanguage : FullAuditedEntity<Guid>     (NO IMultiTenant — host-scoped)
└── Name : string [max 50, required]
```

## Inbound FKs

- `Patient.AppointmentLanguageId` → SetNull. The patient's preferred language for interpreter needs.

## Multi-tenancy

**IMultiTenant: No.** Host-scoped lookup. DbContext config inside `IsHostDatabase()`.

## Permissions

```
CaseEvaluation.AppointmentLanguages          (Default)
CaseEvaluation.AppointmentLanguages.Create
CaseEvaluation.AppointmentLanguages.Edit
CaseEvaluation.AppointmentLanguages.Delete
```

## Business Rules

1. **CreateDto defaults Name to "English"** — the DTO has a default value, though the AppService doesn't enforce it.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| AppointmentLanguageComponent | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.component.ts` | `/appointment-management/appointment-languages` | List view |
| AbstractAppointmentLanguageComponent | `angular/src/app/appointment-languages/appointment-language/components/appointment-language.abstract.component.ts` | — | Base directive |
| AppointmentLanguageDetailModalComponent | `angular/src/app/appointment-languages/appointment-language/components/appointment-language-detail.component.ts` | — | Modal for create/edit |

**Pattern:** ABP Suite abstract/concrete (`AbstractAppointmentLanguageComponent` → `AppointmentLanguageComponent`)

**Forms:**
- name: text (maxLength: 50, required, default: "English")

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.AppointmentLanguages`)
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Create'"` — create button
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.AppointmentLanguages.Delete'"` — delete action

**Services injected:**
- `ListService`, `AppointmentLanguageViewService`, `AppointmentLanguageDetailViewService`, `PermissionService`

## Known Gotchas

1. **FullAuditedEntity, not AggregateRoot** — simpler base class
2. **No tests**

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
