[Home](../../INDEX.md) > [Issues](../) > Research > ARC-01

# ARC-01: Vestigial Books Entity from ABP Scaffold -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Domain/Books/Book.cs`
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Books/BookType.cs`
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Books/`
- `src/HealthcareSupport.CaseEvaluation.Application/Books/BookAppService.cs`
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/BookController.cs`
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Books/EfCoreBookRepository.cs`
- `src/HealthcareSupport.CaseEvaluation.Domain/BookStoreDataSeederContributor.cs`
- `angular/src/app/proxy/books/`
- `test/HealthcareSupport.CaseEvaluation.Application.Tests/Books/` (3 tests)

---

## Current state (verified 2026-04-17)

All artefacts still present. `Menu:Books` and book-CRUD localization keys also present in `en.json`. This is the canonical [ABP Book Store tutorial](https://abp.io/docs/latest/tutorials/book-store/part-01?UI=MVC&DB=EF) sample that ships as scaffolded example code in the commercial startup template -- not part of the domain model.

The `BookStoreDataSeederContributor` runs on every `DbMigrator` execution and inserts "1984" and "The Hitchhiker's Guide" into production databases. `BookController` exposes live `GET/POST/PUT/DELETE /api/app/books` endpoints. 3 test methods inflate the headline coverage count without exercising real business logic.

---

## Official documentation

- [ABP Book Store Tutorial Part 1](https://abp.io/docs/latest/tutorials/book-store/part-01?UI=MVC&DB=EF) -- canonical origin of the `Book` entity and `BookType` enum (Dystopia, ScienceFiction, Poetry, Fantastic).
- [ABP Commercial Book Store tutorial](https://docs.abp.io/en/commercial/latest/tutorials/book-store/part-1) -- commercial variant.
- [ABP EF Core Database Migrations](https://abp.io/docs/latest/framework/data/entity-framework-core/migrations) -- pattern: remove `builder.ConfigureBooks()` / DbSet registration, run `Add-Migration`, EF generates `migrationBuilder.DropTable()` automatically.
- [Microsoft -- Managing EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing) -- official guidance on destructive schema changes.
- [EF Core migration files guide](https://www.learnentityframeworkcore.com/migrations/migration-files) -- `migrationBuilder.DropTable(name: "AppBooks")` signature.

## Community findings

- [abpframework/abp #7709 -- Properly remove all abp-related tables from migration](https://github.com/abpframework/abp/issues/7709) -- confirms removing `builder.ConfigureXxx()` lines + DbSet + re-running `Add-Migration` emits the right `DropTable` calls.
- [ABP Community -- Unifying DbContexts / Removing EF Core Migrations Project](https://abp.io/community/articles/unifying-dbcontexts-for-ef-core-removing-the-ef-core-migrations-project-nsyhrtna) -- pattern for safely restructuring without data loss.
- [devart -- EF Core Migrations: Create, Update, Remove, Revert](https://www.devart.com/dotconnect/ef-core-migrations.html) -- third-party walkthrough of destructive migrations.

## Recommended approach

1. Remove Books files layer-by-layer (Domain -> Application.Contracts -> Application -> HttpApi -> EntityFrameworkCore).
2. Remove `BookStoreDataSeederContributor` registration from the Domain module.
3. Remove `DbSet<Book>` and `builder.ConfigureBooks()` (or inline config) from `CaseEvaluationDbContext`.
4. Run `dotnet ef migrations add RemoveBooks` -- EF Core auto-emits `DropTable("AppBooks")`.
5. Delete `angular/src/app/proxy/books/` and its barrel imports; run `abp generate-proxy` as safety net (proxy-gen writes new files but does not remove orphans).
6. Grep for `Menu:Books`, `L["Books"]`, and `BookType` enum values to catch localization residue.
7. Delete the 3 test methods; coverage headline drops -- announce in the PR description.

## Gotchas / blockers

- EF Core only emits `DropTable` if entity is removed from BOTH `OnModelCreating` AND DbContext `DbSet` property. Miss one, migration is empty.
- `BookStoreDataSeederContributor` may be named exactly that (tutorial default); grep `[DependsOn]` and test seeding.
- Removing the 3 tests drops the pass count; cross-reference CI coverage gates before deleting.
- Books tutorial is host-only in ABP; confirm with `if (builder.IsHostDatabase())` guard in current DbContext.

## Open questions

- Any Angular routes/menu contributions reference Books in `app.routes.ts` or a `route.provider.ts`? (Repo check needed.)
- Does tenant vs host DbContext split both reference Books, or only one?

## Related

- [FEAT-07](FEAT-07.md) -- the 3 Books tests are part of the near-zero coverage picture
- [docs/issues/ARCHITECTURE.md#arc-01](../ARCHITECTURE.md#arc-01-vestigial-books-entity-from-abp-scaffold)
