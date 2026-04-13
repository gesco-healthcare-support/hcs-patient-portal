[Home](../INDEX.md) > Runbooks > Local Development Troubleshooting

# Local Development Troubleshooting

> This is a **"when things go wrong" reference**, not a setup guide. For first-time setup, see [Getting Started](../onboarding/GETTING-STARTED.md).

Playbook for the five most common failures during local (non-Docker) development. For Docker-based dev, see [DOCKER-DEV.md](DOCKER-DEV.md).

---

## Service Startup Order (mandatory)

Never skip this order. Race conditions at startup cause silent permission-seeding failures.

1. **SQL Server** -- start and wait for ready. Verify: `sqlcmd -S <instance> -Q "SELECT 1"` returns `1`.
2. **AuthServer** -- `dotnet run --project src/HealthcareSupport.CaseEvaluation.AuthServer`. Wait until `curl -sk https://localhost:44368/.well-known/openid-configuration` returns `200`.
3. **HttpApi.Host** -- `dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host`. Wait until `curl -sk https://localhost:44327/swagger/index.html` returns `200`.
4. **Angular** -- `npx ng build --configuration development && npx serve -s dist/CaseEvaluation/browser -p 4200`. Never `ng serve` (see Problem 2).

---

## Problem 1: SNI.dll load failure (Windows)

**Symptom:**
```
System.IO.FileLoadException: Could not load file or assembly
'Microsoft.Data.SqlClient.SNI.dll'
```

**Cause:** Project path exceeds the 260-character native DLL loading limit on Windows. `SqlClient.SNI.dll` uses native Win32 path APIs that do not support long paths.

**Fix:**
- Clone the repo to a short path such as `P:\hcs\`, `C:\dev\hcs\`, or `D:\src\`.
- Alternatively, use `subst` to create a drive alias:
  ```cmd
  subst P: "C:\Users\RajeevG\Long\Nested\Documents\Projects\Patient Portal\hcs-case-evaluation-portal"
  cd /d P:\
  ```
- `subst` assignments are per-session; add the command to a login script if needed.

**Prevention:** Always keep the repo at a short path on Windows. Not an issue on macOS / Linux.

---

## Problem 2: NullInjectorError: No provider for CORE_OPTIONS (Angular)

**Symptom:** Browser console shows:
```
NullInjectorError: R3InjectorError(Standalone[AppComponent])
  [CORE_OPTIONS -> CORE_OPTIONS]:
    NullInjectorError: No provider for CORE_OPTIONS!
```

**Cause:** Angular 20's Vite-based dev server (used by `ng serve`) splits `@abp/ng.core` across chunks. This creates **two separate instances** of the `CORE_OPTIONS` InjectionToken. Angular DI uses `===` identity to match tokens, so the second instance never matches the provider registration.

**Fix:** Never use commands that invoke the Vite dev pipeline:
- **Do not run:** `ng serve`, `yarn start`, `npm start`, `ng build --watch`
- **Instead:**
  ```bash
  cd angular
  npx ng build --configuration development
  npx serve -s dist/CaseEvaluation/browser -p 4200
  ```
- For iterative development, rerun the build manually after changes. Angular esbuild (used by `ng build`) does not have this bug.

**Prevention:** This is enforced in the root [CLAUDE.md](../../CLAUDE.md) Critical Constraints section and in [ADR-005](../decisions/005-no-ng-serve-vite-workaround.md).

---

## Problem 3: SQL Server connection errors / empty database

**Symptom:**
- `A network-related or instance-specific error occurred`
- `Cannot open database "CaseEvaluation" requested by the login`
- Permission seeding silently fails; all API calls return 403

**Cause A (cold start):** .NET service started before SQL Server was ready. ABP's `UsePermissionSeeder` runs during startup; if the DB connection fails, the seeder silently skips without retrying.

**Fix A:** Always verify SQL Server is fully ready before launching AuthServer / HttpApi.Host. For LocalDB:
```cmd
sqllocaldb start MSSQLLocalDB
sqllocaldb info MSSQLLocalDB
```
Look for `State: Running` before starting .NET services.

**Cause B (missing DB / migrations not applied):** The application DB does not exist or is at an older migration.

**Fix B:** Run the migrator:
```bash
dotnet run --project src/HealthcareSupport.CaseEvaluation.DbMigrator
```
This creates the database if missing, applies all migrations, and seeds initial data.

**Cause C (wrong connection string):** `appsettings.json` points to a different instance than the one running.

**Fix C:** Verify `ConnectionStrings:Default` in:
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json`
- `src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.json`
- `src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.json`

LocalDB connection string pattern: `Server=(LocalDB)\\MSSQLLocalDB;Database=CaseEvaluation;Trusted_Connection=True`

---

## Problem 4: ABP license errors at build or runtime

**Symptom:**
- Build error: `ABP Commercial license could not be verified`
- Runtime error: `ABP_LICENSE_CODE is invalid or missing`
- LeptonX theme missing / 404 on theme assets

**Cause:** `appsettings.secrets.json` missing or contains invalid `AbpLicenseCode`.

**Fix:** Create `appsettings.secrets.json` in each of:
- `src/HealthcareSupport.CaseEvaluation.AuthServer/`
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/`
- `src/HealthcareSupport.CaseEvaluation.DbMigrator/`
- `test/HealthcareSupport.CaseEvaluation.TestBase/`

Contents:
```json
{ "AbpLicenseCode": "<YOUR-ABP-COMMERCIAL-LICENSE-CODE>" }
```

Also verify `NuGet.Config` (generated from `NuGet.Config.template`) contains a valid `ABP_NUGET_API_KEY` for restoring commercial packages.

---

## Problem 5: Permission seeding fails / admin sees empty permission tree

**Symptom:** Admin UI at `https://localhost:44327/Identity/Roles` shows no permissions under the CaseEvaluation group, or API returns 403 for all authenticated requests that should succeed.

**Cause:** The permission definition provider registers permissions, but the seeder that grants them to the `admin` role failed. Usually caused by:
- SQL Server was not ready during service startup
- `DbMigrator` was not run recently enough to include new permission definitions

**Fix:**
1. Stop all .NET services.
2. Run the migrator: `dotnet run --project src/HealthcareSupport.CaseEvaluation.DbMigrator`.
3. Restart services in order (SQL -> AuthServer -> HttpApi.Host -> Angular).
4. If still empty, check `CaseEvaluationPermissionDefinitionProvider.cs` (`src/.../Application.Contracts/Permissions/`) for missing registrations -- a new permission added to `CaseEvaluationPermissions.cs` will not appear unless it is also registered here.

---

## Verification Commands

Run these to confirm a healthy local environment:
```bash
# AuthServer
curl -sk -o /dev/null -w "%{http_code}\n" https://localhost:44368/.well-known/openid-configuration   # expect 200

# HttpApi.Host
curl -sk -o /dev/null -w "%{http_code}\n" https://localhost:44327/swagger/index.html                 # expect 200

# Angular
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:4200/                                       # expect 200
```

---

## Related Documents

- [Getting Started](../onboarding/GETTING-STARTED.md) -- initial setup
- [Docker Development](DOCKER-DEV.md) -- alternative Docker-based setup
- [Incident Response](INCIDENT-RESPONSE.md) -- when things go wrong in a production-incident sense
- [ADR-005: No ng serve](../decisions/005-no-ng-serve-vite-workaround.md)
