<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Books/CLAUDE.md on 2026-04-08 -->

# Books

Demo/sample entity from ABP Framework scaffolding. Not a business feature -- used for learning and testing. Has a `BookType` enum, basic CRUD AppService, and existing tests (one of the few features with test coverage).

## Entity Shape

```
Book : AuditedAggregateRoot<Guid>     (NO FullAudited, NO soft delete, NO IMultiTenant)
├── Name        : string       (no max length constraint)
├── Type        : BookType     (Undefined..Poetry)
├── PublishDate : DateTime
└── Price       : float
```

## Multi-tenancy

**IMultiTenant: No.** Not tenant-scoped. No DbContext configuration found -- uses ABP's generic repository.

## Permissions

```
CaseEvaluation.Books          (Default)
CaseEvaluation.Books.Create
CaseEvaluation.Books.Edit
CaseEvaluation.Books.Delete
```

## Mapper Configuration

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `CaseEvaluationBookToBookDtoMapper` | Entity → DTO | No |
| `CaseEvaluationCreateUpdateBookDtoToBookMapper` | DTO → Entity (reverse mapping) | No |

**Note:** Uses `CreateUpdateBookDto` (combined create/update DTO) -- deviates from ABP convention of separate DTOs.

## Known Gotchas

1. **Not a real feature** -- demo entity from ABP scaffolding
2. **AuditedAggregateRoot** -- no soft delete (different from all other entities)
3. **Combined CreateUpdateBookDto** -- violates project convention of separate Create/Update DTOs
4. **No Angular UI**
5. **Has tests** -- one of the few features with test coverage in test projects

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/Books/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
