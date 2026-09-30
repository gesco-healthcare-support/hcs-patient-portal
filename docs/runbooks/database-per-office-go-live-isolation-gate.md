# Runbook: database-per-office go-live isolation gate

> Purpose: prove that no office can read another office's data before database-per-office goes to
> production.
> Audience: whoever signs off the database-per-office migration before production.
> Owner: the portal maintainer.
> **Last tested: run at least once, and signed off; confirmed by the portal maintainer on
> 2026-09-28; date of the run not recorded.**
> Companion: [ADR-017](../decisions/017-database-per-office-isolation.md).

## When you need this page

- Before promoting database-per-office to any production environment for the first time.
- Before promoting a change that touches tenant resolution, office provisioning, connection
  strings, the host-operator switch-in, or any query that reads across offices.

This is the final security/HIPAA gate. Do not go to production until every check below
passes. The rule is **deny-by-default: any cross-office PHI read through any pathway is a
blocking failure** -- there is no "low severity" cross-office leak.

## 1. Automated gate (must be green)

Run the backend suite; the multi-office isolation tests are the gate.

```bash
cd test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests
dotnet test --filter "FullyQualifiedName~MultiOffice"
```

Expect `Passed!` with `Failed: 0`. Check the exit status (`echo $?` prints `0`) rather than
reading the summary line.

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

- Stack up from a clean state: `docker compose down -v && docker compose up -d --build`.
  **`down -v` deletes the stack's databases.** Use it only on a disposable local or staging
  stack, never on a server holding data you need. Confirm with `docker compose ps` that
  `db-migrator` exited 0 and `api` and `authserver` report `(healthy)`. Ports come from your
  `.env`; if another stack is running, stop it first rather than guessing new ports.
- Two offices provisioned, each with its own database (e.g. the seeded office plus a
  second office created via the SaaS admin UI / office-creation flow). Capture each
  office's tenant id from `CaseEvaluation.dbo.SaasTenants` (re-query after any
  `down -v`, the ids regenerate). Query SQL Server inside the container so the SA password
  never prints:
  `MSYS_NO_PATHCONV=1 docker compose exec -T sql-server bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT Id, Name FROM CaseEvaluation.dbo.SaasTenants"'`.
  Expect one row per office.

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
5. **Operators.** A host IT Admin switches into an office holding that office's `admin` role,
   and a host Staff Supervisor holding its Staff Supervisor role, each as their own per-office
   user. A host Intake operator is limited, and is denied switching into an UNASSIGNED office;
   unassigning an office revokes access.
6. **Branding.** Each office shows its own name/logo; the host shows the default; the
   pre-auth branding fetch returns only the resolved subdomain's office.
7. **Connection strings.** Grep application logs -> no connection string is ever logged.
8. **Fail-closed.** Provisioning an office with no Default connection string is rejected
   (does not seed into the host database).

## 3. Sign-off

Record the date, the two office databases checked, and the result of each check above in
the deployment ticket. Only then promote.

## Abort

Any failed check, automated or manual, means **do not promote**. Record which check failed and
what you observed, and leave production on its current release. There is nothing to roll back at
this point, because nothing has been promoted. Do not re-run until the cause is understood: a
check that fails once and passes on retry is not a pass.

## Escalation

Immediately, with no time box to work around it. A failed isolation check goes to the portal
maintainer the same day, with the check number, the two office ids, and the request or query that
crossed the boundary. It is a potential exposure of patient data, not a defect to schedule.
