# DbMigrator -- one-shot console app: apply EF Core migrations + run seed contributors, then exit

## What lives here

- `Program.cs` -- parses `--disable-redis`, builds the generic host, calls `RunConsoleAsync`.
- `DbMigratorHostedService.cs` -- bootstraps ABP, resolves `CaseEvaluationDbMigrationService`, calls `MigrateAsync()`, then stops the host.
- `CaseEvaluationDbMigratorModule.cs` -- module wiring: sets `Redis:IsEnabled=false` when `--disable-redis` is present; disables Hangfire job execution (`IsJobExecutionEnabled = false`) so the migrator never drains the queue.

## Where the real work lives (NOT here)

- `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationDbMigrationService.cs` -- migration orchestration (host DB + every tenant DB).
- `src/HealthcareSupport.CaseEvaluation.Domain/<Feature>/*DataSeedContributor.cs` -- `IDataSeedContributor` impls; ABP DI discovers them automatically.
- Add new seed contributors in the Domain layer, not here.

## Conventions

**Run via docker-compose (preferred).**
The `db-migrator` service in `docker-compose.yml` sets `restart: "no"` and waits on
`sql-server`, `redis`, and `minio` healthy before starting.
`authserver` and `api` use `condition: service_completed_successfully` on `db-migrator`,
so they never start against an unmigrated DB. See docs/database/MIGRATION-GUIDE.md for
the equivalent local `dotnet run` invocation.

**Local dev without Redis.**
Pass `--disable-redis`; the module sets `Redis:IsEnabled=false` in `PreConfigureServices`
before any Redis connection is attempted.

**Config layering.**
`appsettings.Local.json` is loaded as an optional override (not in source control).
`appsettings.secrets.json` is mounted read-only in Docker; keep it out of commits.

## Gotchas

- IMPORTANT: Never call `EnsureCreated()` or `context.Database.Migrate()` at runtime in
  application code. All schema changes go through this migrator only.
- The migrator exits with a non-zero code on failure -- Docker's `service_completed_successfully`
  condition propagates the failure and blocks dependent services. Fix the migration, do not
  suppress the exit code.
- `BookStoreDataSeederContributor.cs` in Domain is ABP scaffold residue; it seeds nothing
  meaningful and is safe to delete when the scaffold is cleaned up.
- Logs are written to `Logs/logs.txt` relative to the working directory. In Docker that is
  inside the ephemeral container; mount a volume if you need log persistence.

## Related

- docs/database/MIGRATION-GUIDE.md
- docs/database/DATA-SEEDING.md
