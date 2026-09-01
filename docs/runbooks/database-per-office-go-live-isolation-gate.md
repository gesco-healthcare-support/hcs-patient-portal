# Runbook: database-per-office go-live isolation gate

**Audience:** whoever signs off the database-per-office migration before production.
**Companion:** [ADR-017](../decisions/017-database-per-office-isolation.md).

This is the final security/HIPAA gate. Do not go to production until every check below
passes. The rule is **deny-by-default: any cross-office PHI read through any pathway is a
blocking failure** -- there is no "low severity" cross-office leak.

## 1. Automated gate (must be green)

Run the backend suite; the multi-office isolation tests are the gate.

```
cd test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests
dotnet test --filter "FullyQualifiedName~MultiOffice"
```

Required:
- `MultiOfficeHarnessSelfValidationTests` passes -- office A's row is invisible to office
  B even with the `IMultiTenant` filter disabled (proves physical separation, not
  filter-only).
- `MultiOfficeIsolationMatrixTests` passes -- cross-office reads of patients (incl. the
  full-SSN reveal), appointments and catalogs are denied; `ITenantWorkRunner` visits both
  offices each scoped to its own data.
- The full backend suite is green with the previously-skipped catalog/booking tests now
  running on the multi-office harness (skip count materially reduced).

If any multi-office test fails, STOP. A failure here means isolation is not proven.

## 2. Manual final real-database check (F6)

The automated gate uses in-memory SQLite. This step closes the SQLite-vs-SQL-Server
fidelity gap on real databases. Use the docker stack (real SQL Server container) or a
staging SQL Server.

Preconditions:
- Stack up: `docker compose down -v && PACKET_RENDERER_PORT=3011 docker compose up -d --build`
  (db-per-tenant worktree ports: authserver 44438, api 44397 HTTP, angular 4270). Confirm
  db-migrator exited 0 and api/authserver are Healthy.
- Two offices provisioned, each with its own database (e.g. the seeded office plus a
  second office created via the SaaS admin UI / office-creation flow). Capture each
  office's tenant id from `CaseEvaluation.dbo.SaasTenants` (re-query after any
  `down -v`, the ids regenerate). Query SQL Server inside the container so the SA password
  never prints:
  `MSYS_NO_PATHCONV=1 docker exec -i db-per-tenant-sql-server-1 bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C' <<'SQL' ... SQL`

Checks (each must pass):
1. **Physical separation.** Confirm two distinct office databases exist
   (`SELECT name FROM sys.databases` -> `CaseEvaluation_{slugA}`, `CaseEvaluation_{slugB}`).
   Confirm office A's patients/appointments rows exist only in office A's database and are
   absent from office B's database (query each database directly).
2. **Operational data.** Authenticated as an office-A user, attempt to read an office-B
   appointment/patient by id via the API (URL/id tampering) -> denied (404/forbidden).
3. **Full SSN.** Attempt the SSN-reveal endpoint for an office-B patient as an office-A
   user -> denied; confirm no full SSN crosses the office boundary.
4. **Catalogs.** An office-A catalog edit (appointment type / location / language) is not
   visible in office B.
5. **Operators.** A host Supervisor switches into an office as admin; a host Intake
   operator is limited and is denied switching into an UNASSIGNED office; unassigning an
   office revokes access.
6. **Branding.** Each office shows its own name/logo; the host shows the default; the
   pre-auth branding fetch returns only the resolved subdomain's office.
7. **Connection strings.** Grep application logs -> no connection string is ever logged.
8. **Fail-closed.** Provisioning an office with no Default connection string is rejected
   (does not seed into the host database).

## 3. Sign-off

Record the date, the two office databases checked, and the result of each check above in
the deployment ticket. Only then promote.
