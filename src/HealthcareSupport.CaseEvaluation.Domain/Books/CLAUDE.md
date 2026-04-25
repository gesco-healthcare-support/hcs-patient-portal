---
feature: Books
status: vestigial
last-verified: 2026-04-24
product-doc: none -- excluded from product intent (vestigial ABP scaffold)
---

# Books

Vestigial ABP Framework sample entity left over from initial scaffolding. Not a business feature; retained because the test project's `BookAppServiceTests` still exercises the ABP application-service plumbing (one of the few features with any test coverage at all).

> Books has no product-intent doc -- vestigial ABP scaffold. Excluded from `docs/product/` per the 2026-04-23 gap-analysis. Do not extend; remove together with the seed contributor and tests when ABP scaffold cleanup is scheduled.

## File Map

| Layer | File | Purpose |
| --- | --- | --- |
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Books/BookType.cs` | Enum: Undefined, Adventure, Biography, Dystopia, Fantastic, Horror, Science, ScienceFiction, Poetry |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Books/Book.cs` | Aggregate root (`AuditedAggregateRoot<Guid>`) |
| Domain (seeder) | `src/HealthcareSupport.CaseEvaluation.Domain/BookStoreDataSeederContributor.cs` | Seeds two books on first run if table is empty |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Books/BookDto .cs` | Read DTO (note: filename has trailing space before `.cs`) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Books/CreateUpdateBookDto.cs` | Combined create/update DTO with DataAnnotations |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Books/IBookAppService.cs` | `ICrudAppService<BookDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateBookDto>` |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/Books/BookAppService.cs` | Hand-written `ApplicationService` (NOT `CrudAppService`); 5 CRUD methods over `IRepository<Book, Guid>` |
| Application (mapper) | `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` | Riok.Mapperly partial classes (lines 30-52) |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Books/BookController.cs` | `api/app/book` (5 endpoints) -- forwards to `IBookAppService` |
| Test | `test/HealthcareSupport.CaseEvaluation.Application.Tests/Books/BookAppServiceTests.cs` | 3 facts: list, create-valid, create-without-name-throws |
| Permissions | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs` | `CaseEvaluation.Books{,.Create,.Edit,.Delete}` |
| Permissions | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs` | Registers Books permission tree |

## Entity Shape

```text
Book : AuditedAggregateRoot<Guid>     (audited but NOT FullAudited -- no soft delete; NOT IMultiTenant)
  Name        : string    (no DB length constraint; DTO enforces [StringLength(128)])
  Type        : BookType  (enum, defaults Undefined on DTO)
  PublishDate : DateTime
  Price       : float     (single-precision; intentional ABP sample shape)
```

No state machine. Plain CRUD shape.

## Relationships

None. No FKs in or out. Not configured in any `DbContext` partial -- ABP's generic repository handles the table via convention.

## Multi-tenancy

IMultiTenant: No. No DbContext entry, so no `IsHostDatabase()` guard either. Treated as host-scoped by default.

## Mapper Configuration

Riok.Mapperly (NOT AutoMapper). Found in `CaseEvaluationApplicationMappers.cs`:

| Mapper Class | Direction | AfterMap |
| --- | --- | --- |
| `CaseEvaluationBookToBookDtoMapper : MapperBase<Book, BookDto>` | Entity -> DTO | None |
| `CaseEvaluationCreateUpdateBookDtoToBookMapper : MapperBase<CreateUpdateBookDto, Book>` | DTO -> Entity | None; ignores `ConcurrencyStamp`, `CreationTime`, `CreatorId`, `LastModificationTime`, `LastModifierId` via `[MapperIgnoreTarget]` on both `Map` overloads |

No `LookupDto<Guid>` mapper -- Books is not a lookup target.

## Permissions

```text
CaseEvaluation.Books         (Default)
CaseEvaluation.Books.Create
CaseEvaluation.Books.Edit
CaseEvaluation.Books.Delete
```

`BookAppService` is class-level `[Authorize(CaseEvaluationPermissions.Books.Default)]`; `Create` / `Update` / `Delete` add their own `[Authorize(...)]`. `GetAsync` and `GetListAsync` inherit only the Default permission.

## Business Rules

Standard CRUD with two notable wrinkles:

- **Validation comes from DTO DataAnnotations**, not the entity: `Name` is `[Required, StringLength(128)]`, `Type` / `PublishDate` / `Price` are `[Required]` with `Type` defaulting to `BookType.Undefined`. The "without Name" test relies on this.
- **`AppService` is hand-written, not `CrudAppService`**: it injects `IRepository<Book, Guid>` directly and writes its own `GetListAsync` with dynamic `OrderBy` (defaulting to `Name`). This deviates from typical ABP scaffolds and from the project's Reference Pattern.
- No uniqueness checks, no computed fields, no frozen fields, no lookup filtering.

## Angular UI Surface

No Angular UI -- this entity is managed via API only.

## Known Gotchas

1. **AppService disables remote service** (`[RemoteService(IsEnabled = false)]`) but `BookController` re-exposes the same interface at `api/app/book` with `[RemoteService]`. Effective surface is the controller; client proxies generated against the controller will work, against the AppService will not.
2. **Hand-written `ApplicationService`, not `CrudAppService`.** Earlier doc revision claimed `CrudAppService` -- corrected here. Means future regeneration via ABP Suite will likely overwrite it.
3. **Combined `CreateUpdateBookDto`** -- diverges from project convention of separate Create/Update DTOs.
4. **Filename `BookDto .cs`** has a stray space before the extension. Builds fine on Windows, fragile on case- and whitespace-sensitive filesystems.
5. **Seeder always runs** (`BookStoreDataSeederContributor : ITransientDependency, IDataSeedContributor`) inserting "1984" and "The Hitchhiker's Guide to the Galaxy" if the Books table is empty. Synthetic data only -- safe under HIPAA, but pollutes empty databases.
6. **Float for Price.** ABP sample artifact; would be `decimal` in any real money column.
7. **No DbContext configuration** -- table created by ABP convention only; cannot add length / index constraints without adding an explicit entity config.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern-appointments](/CLAUDE.md#reference-pattern-appointments)
- Product doc: none (vestigial scaffold; excluded from `docs/product/`)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
