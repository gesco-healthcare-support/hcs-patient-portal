# Appendix B: The blocking questions, raw research

> Four targeted research passes: the three questions the brief named as decision-blocking, plus an
> independent confirmation of the Angular CVEs (deliberately redundant with the baseline, which is
> how three errors in the baseline's CVE table were found) and a first-party .NET 10 / ABP CI
> guidance pass.
>
> Each records the answer, the reasoning, every evidence item with its URL and settling quote, the
> recommended action, and what could not be resolved.
>
> Produced 2026-08-28.

---

## B1. ABP Commercial 10.0.2 and Angular 20.3.27

**Question as posed:** Does ABP Commercial 10.0.2 support Angular 20.3.27, such that the dependabot.yml blocker ("until ABP Commercial supports Angular 20.3+") is stale and the closed Dependabot PRs #415/#416/#417 (20.3.19 -> 20.3.27) can be re-opened and merged?

**Answer: `qualified-yes`**

### Reasoning

The blocker is stale and, as written, self-refuting -- but not for the reason the comment implies. (a) ABP 10.0.x targets the Angular 20 MAJOR and specifically the 20.0.x line: ABP's own 10.0 quick-start docs state "In this version ABP uses Angular 20.0.x version", ABP's 10.0.2 Angular app template pins every @angular/* package at ~20.0.0, and the published @abp/ng.core 10.0.2 bundle carries Angular partial-compilation markers of version "20.0.7" with linker minVersion 12.0.0/14.0.0/17.0.0. Critically, this holds for ABP COMMERCIAL too, not just open-source: @volo/abp.ng.account 10.0.2 is mirrored on public npm and is likewise compiled at "20.0.7" with no Angular constraint. (c) is the decisive finding: @abp/ng.core 10.0.2 declares NO peerDependencies field at all -- verified twice, in the registry manifest and by extracting package/package.json from the published tarball -- and the same is true of ng.theme.shared, ng.identity, ng.account, ng.setting-management, ng.permission-management, ng.tenant-management and @volo/abp.ng.account. ABP does express peer ranges when it wants to (@abp/ng.theme.lepton-x 5.0.2 declares peers on @abp/ng.core ~10.0.2), so the absence of an @angular peer is a deliberate convention, not an oversight; consequently no semver peer machinery in Yarn 4 or npm can flag a 20.3.x bump. (b) follows: ABP's ~20.0.0 template pin excludes 20.3.19 and 20.3.27 EQUALLY, so there is no coherent reading in which the repo's current 20.3.19 is acceptable but 20.3.27 is not -- the team has already been running three minors beyond ABP's stated target, apparently without issue. Angular's own release policy classifies a patch as a "Low risk, bug fix release. No developer assistance is expected during update," and minors as "fully backward-compatible." The comment was almost certainly written about the Angular 20->21 MAJOR jump -- a real ABP constraint, since ABP only moved its template to ~21.2.0 at 10.3.0 and ~22.0.1 at 10.6.0 -- and then over-applied by disabling version updates wholesale. On the CVEs: OSV shows 10 distinct CVEs (not six) affecting 20.3.19 across @angular/core, @angular/common and @angular/compiler, with fix boundaries at 20.3.22, 20.3.25 and 20.3.27; 20.3.27 is exactly the version that closes all of them, cross-confirmed for CVE-2026-69151 by the GitHub Advisory (patched 20.3.27). The residual qualification is that "no declared incompatibility" is not "vendor-tested" -- ABP does not test against 20.3.x -- so merge behind a build plus a login/tenant-resolution smoke test rather than blind. Note also that 20.3.27 is now stale: the line has reached 20.3.30 (current v20-lts), and Angular 20 leaves LTS on 2026-11-28.

### Evidence

**B1e1.** ABP's own 10.0 documentation states the targeted Angular line explicitly, giving the authoritative answer to (a).

- Source: <https://abp.io/docs/10.0/framework/ui/angular/quick-start>
- Quote: "In this version ABP uses Angular 20.0.x version. You don't have to install Angular CLI globally"
- Accessed: 2026-08-28

**B1e2.** Decisive evidence for (c): the published @abp/ng.core 10.0.2 tarball's own package.json contains no peerDependencies key whatsoever -- only dependencies on @abp/utils, just-clone, just-compare, ts-toolbelt, tslib and luxon. No @angular/* constraint exists, so no peer-range check can flag a 20.3.x bump. Verified by extracting package/package.json from the tarball, not by reading a summary.

- Source: <https://registry.npmjs.org/@abp/ng.core/-/ng.core-10.0.2.tgz>
- Quote: ""dependencies": { "@abp/utils": "~10.0.2", "just-clone": "^6.0.0", "just-compare": "^2.0.0", "ts-toolbelt": "^9.0.0", "tslib": "^2.0.0", "luxon": "^3.0.0" }  -- no peerDependencies field present"
- Accessed: 2026-08-28

**B1e3.** Same result from the registry version manifest, confirming the tarball is not an anomaly. Also confirmed null peerDependencies for @abp/ng.theme.shared, ng.setting-management, ng.permission-management, ng.identity, ng.account and ng.tenant-management at 10.0.2.

- Source: <https://registry.npmjs.org/@abp%2Fng.core/10.0.2>
- Quote: "version: 10.0.2 / peerDependencies: null"
- Accessed: 2026-08-28

**B1e4.** COMMERCIAL-side evidence: @volo/abp.ng.account 10.0.2 (an ABP Commercial package, mirrored on public npm) also declares no peerDependencies and its fesm2022 bundle is compiled by the Angular compiler at 20.0.7 -- 449 occurrences of the version marker. This extends the finding from open-source ABP to ABP Commercial, which is what the question asks about.

- Source: <https://registry.npmjs.org/%40volo%2Fabp.ng.account/10.0.2>
- Quote: "peerDependencies: null ; embedded marker: version: "20.0.7" (449 occurrences)"
- Accessed: 2026-08-28

**B1e5.** ABP 10.0.2's shipped Angular app template pins every Angular package at ~20.0.0, i.e. >=20.0.0 <20.1.0. This range excludes 20.3.19 and 20.3.27 identically -- proving the current pin and the proposed bump have the same ABP support status, so the blocker cannot distinguish them.

- Source: <https://raw.githubusercontent.com/abpframework/abp/10.0.2/templates/app/angular/package.json>
- Quote: "@angular/core ~20.0.0 ; @angular/common ~20.0.0 ; @angular/compiler ~20.0.0 ; @angular/cli ~20.0.0"
- Accessed: 2026-08-28

**B1e6.** ABP tracks Angular majors closely and moved on well after 10.0.2 -- the template pin is ~21.2.0 at ABP 10.3.0 and ~22.0.1 at ABP 10.6.0. This supports the reading that the dependabot comment was really about the Angular 20->21 MAJOR jump, which is a genuine ABP constraint, rather than about 20.3 patches.

- Source: <https://raw.githubusercontent.com/abpframework/abp/10.6.0/templates/app/angular/package.json>
- Quote: "abp 10.3.0: @angular/core ~21.2.0 | abp 10.6.0: @angular/core ~22.0.1"
- Accessed: 2026-08-28

**B1e7.** Angular's own release policy classifies patch releases as low-risk and backward-compatible, and confirms Angular 20 is in LTS ending 2026-11-28 -- a separate, real deadline the team must plan for.

- Source: <https://angular.dev/reference/releases>
- Quote: "Patch release: "Low risk, bug fix release. No developer assistance is expected during update." Minor releases are "fully backward-compatible; no developer assistance is expected during update." v20.0.0 -- LTS ends 2026-11-28."
- Accessed: 2026-08-28

**B1e8.** OSV query for 20.3.19 returns 10 distinct CVEs across @angular/core (4), @angular/common (5) and @angular/compiler (3), with 20.x fix boundaries at 20.3.22, 20.3.25 and 20.3.27. 20.3.27 closes all of them; queries for 20.3.27 and 20.3.30 return zero vulns for all three packages. @angular/platform-browser, /router and /forms at 20.3.19 return zero.

- Source: <https://api.osv.dev/v1/query>
- Quote: "CVE-2026-50170, 50171 -> fixed 20.3.22 (common); CVE-2026-54266, 54268 -> 20.3.25 (common); CVE-2026-68945 -> 20.3.27 (common); CVE-2026-50557, 52725 -> 20.3.22 (core/compiler); CVE-2026-54265 -> 20.3.25 (compiler); CVE-2026-54267 -> 20.3.25 (core); CVE-2026-69151 -> 20.3.27 (core/compiler). 20.3.27 and 20.3.30: 0 vulns."
- Accessed: 2026-08-28

**B1e9.** Second, independent source confirming the 20.3.27 fix boundary (the rules of evidence require cross-referencing anything load-bearing). GitHub Advisory agrees with OSV.

- Source: <https://github.com/advisories/GHSA-jj27-h5hq-8x99>
- Quote: "Affected: >= 20.0.0-next.0, < 20.3.27. Patched versions: 22.0.1, 21.2.19, 20.3.27. CVSS High 7.6. Angular i18n: Cross-Site Scripting (XSS) via event-handler attributes."
- Accessed: 2026-08-28

**B1e10.** The Angular 20.3 line has moved past .27: 20.3.27 shipped 2026-07-29 (so PR #415-#417 on 2026-08-03 were current then), but 20.3.28/.29/.30 have since shipped and 20.3.30 (2026-08-26) is the current v20-lts. Re-targeting to .30 is strictly better than reviving the .27 PRs.

- Source: <https://registry.npmjs.org/@angular/core>
- Quote: "20.3.27 2026-07-29 ; 20.3.28 2026-08-13 ; 20.3.29 2026-08-19 ; 20.3.30 2026-08-26 ; dist-tags: "v20-lts": "20.3.30", "latest": "22.1.4""
- Accessed: 2026-08-28

**B1e11.** ABP demonstrably does declare peerDependencies when it intends a constraint -- @abp/ng.theme.lepton-x 5.0.2 constrains its ABP peers. This shows the total absence of any @angular peer range across the ABP suite is a deliberate convention, strengthening the inference that ABP does not machine-enforce an Angular version.

- Source: <https://registry.npmjs.org/@abp%2Fng.theme.lepton-x/5.0.2>
- Quote: "peerDependencies: {"@abp/ng.core": "~10.0.2", "@abp/ng.account.core": "~10.0.2", "@abp/ng.theme.shared": "~10.0.2"}"
- Accessed: 2026-08-28

**B1e12.** Supports the recommendation to enable Dependabot security updates independently: they do not depend on the dependabot.yml version-update block, so CVE-driven PRs can flow even while version updates stay restricted.

- Source: <https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/about-dependabot-security-updates>
- Quote: "There is no interaction between the settings specified in the dependabot.yml file and Dependabot security alerts, other than the fact that alerts will be closed when related pull requests generated by Dependabot for security updates are merged."
- Accessed: 2026-08-28

### Recommended action

Treat the blocker as stale and remove it, but re-target the bump. Concretely, in priority order: (1) Bump @angular/* to 20.3.30, not 20.3.27 -- .27 closes all 10 CVEs but three further patches have shipped and .30 is the current v20-lts; do it as one atomic PR across all @angular/* packages (they must move in lockstep), not by reviving the three separate Dependabot PRs. (2) Delete or rewrite the dependabot.yml comment -- as written it blocks on a condition ("20.3+") the repo already satisfies, and it is unfalsifiable because ABP publishes no Angular peer range to test against; replace the blanket version-update disable with an `ignore` rule scoped to Angular MAJOR bumps only (update-types: version-update:semver-major), which preserves the real ABP constraint while letting patch/minor security fixes flow. (3) Confirm Dependabot security updates are enabled at repo level -- they are independent of the disabled version-updates block, so this restores CVE coverage across NuGet and GitHub Actions too, which the wholesale disable also silenced. (4) Gate the merge on a `yarn build --configuration production` plus a manual smoke test of login, password reset, document upload and -- given database-per-tenant and Host-header tenancy -- tenant resolution across two different office hostnames; that is the whole of the residual risk and costs under an hour. Budget roughly half a developer-day total. Separately, schedule the real blocker: ABP 10.0.2 -> 10.3+ is what actually gates Angular 21/22, and Angular 20 leaves LTS on 2026-11-28, about three months out -- that upgrade is a multi-day job and should be planned into a monthly slot now rather than discovered in November. A larger company would additionally wire an SCA gate that fails CI on High/Critical CVEs with a documented exception workflow; for a two-person team, enabling Dependabot security updates plus a monthly triage slot gets most of that value without the process overhead.

### What could not be resolved, and what was tried

Two limitations worth flagging. First, I could not read the actual portal repository: the working directory /home/user/Aditya-gam contains only a personal profile README and resume PDFs, no .github/dependabot.yml, package.json or yarn.lock. Every repo-specific fact (the pin at ~20.3.19, the comment text, PR numbers #415/#416/#417, the "six CVEs" count) is taken from the prompt and not independently verified against the codebase -- in particular I could not confirm whether the app uses Angular SSR/hydration, which matters because several of the CVEs (CVE-2026-54266, CVE-2026-50170, CVE-2026-68945 in HttpTransferCache, and CVE-2026-54267 hydration DOM clobbering) are SSR-only and would not be exploitable in a plain SPA; the i18n and template-sanitization XSS issues (CVE-2026-69151, CVE-2026-50557, CVE-2026-52725, CVE-2026-54265) are SPA-relevant regardless. Second, ABP Commercial's private registry npm.abp.io is unreachable from this sandbox (proxy returned CONNECT tunnel failed / 502), so I could not enumerate the full @volo/* commercial package set behind the licence wall. I mitigated this rather than guessing: several @volo/* commercial packages are mirrored on public npm, and I inspected @volo/abp.ng.account 10.0.2 directly -- it has no peerDependencies and is compiled at Angular 20.0.7, identical to the open-source packages. I therefore rate the commercial-side conclusion high confidence, though strictly it is an inference from a sampled commercial package rather than from the complete licensed set. WebSearch was unavailable (session budget of 200 calls exhausted before this task began), so all sourcing was done via direct WebFetch and curl against primary registries, raw GitHub, angular.dev, abp.io docs, OSV and GitHub Advisories.

## B2. SQLite-only backend testing

**Question as posed:** Is SQLite-only backend testing defensible for this app, or must a SQL Server test path exist? (2,261 EF Core tests on Data Source=:memory:, production SQL Server, database-per-tenant, 90 host + 15 tenant migrations, a filtered-unique-index migration now causing a SQLite UNIQUE constraint failure.)

**Answer: `qualified-no`**

### Reasoning

SQLite-only is not defensible here, but the fix is a narrow SQL Server path, not a rewrite of 2,261 tests. Microsoft's own position is explicit and unusually strong: "we recommend either writing your tests against your real database, or if using a test double is an absolute necessity, taking onboard the cost of a repository pattern," and "testing against SQLite does not guarantee the same results as against SQL Server." The decisive finding is not a general principle but a structural one specific to this codebase: ABP's own documentation shows the `*.EntityFrameworkCore.Tests` module builds the SQLite schema with `context.GetService<IRelationalDatabaseCreator>().CreateTables()`, which generates the schema from the EF Core model, not from migrations -- so all 105 migrations that actually create your production databases have zero automated coverage, and the schema under test is a SQLite-flavoured artifact that no environment ever runs. That is almost certainly the direct cause of the reported flake: if `Fix_UniqueIndexesExcludeSoftDeleted` wrote the filter into the migration file only and not into `OnModelCreating` via `HasFilter`, `CreateTables()` produces a plain unfiltered unique index and a soft-deleted row keeps blocking reuse of `RequestConfirmationNumber`. I verified that SQLite is not the limiting factor for filtered indexes at all: SQLite has supported UNIQUE partial indexes since 3.8.0 (2013), and EF Core's relational generator appends `" WHERE " + createIndexOperation.Filter` with no override in `EFCore.Sqlite.Core`, so a model-level `HasFilter` would have been honoured -- which makes the flake a true-positive signal of model/migration drift, not a SQLite defect to be suppressed. Beyond that one bug, four divergences bite this specific app: uniqueness-with-NULL semantics are opposite (SQL Server "cannot create a unique index on a single column if that column contains NULL in more than one row"; SQLite "NULL values are considered distinct from all other values, including other NULLs"), and EF Core only injects the compensating `IS NOT NULL` filter via the SQL-Server-only `SqlServerIndexConvention`; string comparison defaults are opposite (SQL Server US-English default `SQL_Latin1_General_CP1_CI_AS` is case-insensitive, SQLite defaults to `BINARY` and even `NOCASE` folds "only ASCII characters"); `rowversion`/database-generated concurrency tokens are listed by Microsoft as flatly unsupported on SQLite; and `decimal` and `DateTimeOffset` are types "SQLite doesn't natively support" where "comparison and ordering will require evaluation on the client." For a PHI-handling, host-header-tenanted, database-per-tenant system about to face the public internet, the item I would not ship without is the multi-office tenant-isolation harness: it currently proves isolation against a fake, and cross-tenant leakage is your stated highest-consequence failure. Testcontainers for .NET is alive and well-maintained (Testcontainers.MsSql 4.14.0 published 2026-08-14, 32.4M downloads) and is the right choice over a GitHub Actions `services:` block for a two-person team, because the same tests then run unchanged on both developers' laptops -- which is what actually keeps a suite green -- whereas `services:` works only inside CI, cannot be used inside composite actions, and needs a hand-written mssql health check against `/opt/mssql-tools18/bin/sqlcmd` with `-No`, a well-known time sink.

### Evidence

**B2e1.** Microsoft's headline recommendation is to test against the real production database system; SQLite as a fake is explicitly discouraged as a general strategy.

- Source: <https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy>
- Quote: "Unfortunately, the above limitations tend to eventually become problematic when testing EF Core applications, even if they don't seem to be at the beginning. As a result, we recommend either writing your tests against your real database, or if using a test double is an absolute necessity, taking onboard the cost of a repository pattern as discussed below. ... We recommend that developers have good test coverage of their application running against their actual production database system."
- Accessed: 2026-08-28

**B2e2.** Microsoft states directly that SQLite results do not guarantee SQL Server results, and names case-sensitivity as a concrete example of tests passing on SQLite that would fail on SQL Server.

- Source: <https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy>
- Quote: "Fundamentally, this means that testing against SQLite does not guarantee the same results as against SQL Server, or any other database. ... The same LINQ query may return different results on different providers. For example, SQL Server does case-insensitive string comparison by default, whereas SQLite is case-sensitive. This can make your tests pass against SQLite where they would fail against SQL Server (or vice versa)."
- Accessed: 2026-08-28

**B2e3.** The EF Core testing overview page states that at least some tests against the real database are usually necessary, and pushes back on the belief that DB tests are slow.

- Source: <https://learn.microsoft.com/en-us/ef/core/testing/>
- Quote: "Writing at least some tests against your database is usually necessary in any case - to make sure your application actually works against your production database - and tests not involving the database can be limited in what they allow you to test. ... SQLite in-memory mode offers better compatibility with production relational databases, since SQLite is itself a full-fledged relational database. However, there will still be some important discrepancies between SQLite and your production database, and some features cannot be tested at all."
- Accessed: 2026-08-28

**B2e4.** Microsoft argues DB-backed testing is cheaper than teams assume, citing EF Core's own 30,000+ SQL Server tests running in CI on every commit, and names Testcontainers by name.

- Source: <https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy>
- Quote: "Container-based technologies such as Docker can make this very easy, and libraries like Testcontainers can help automate the lifecycle of containerized databases in your tests. ... EF Core itself contains over 30,000 tests against SQL Server alone; these complete reliably in a few minutes, execute in CI on every single commit, and are very frequently executed by developers locally. Some developers turn to an in-memory database (a "fake") in the belief that this is needed for speed - this is almost never actually the case."
- Accessed: 2026-08-28

**B2e5.** DECISIVE: ABP's own docs show the startup template's EntityFrameworkCore.Tests module builds the SQLite test schema with IRelationalDatabaseCreator.CreateTables() from the MODEL, bypassing migrations entirely. This means all 90 host + 15 tenant migrations have zero test coverage, and explains the filtered-index flake as model/migration drift.

- Source: <https://raw.githubusercontent.com/abpframework/abp/dev/docs/en/framework/data/entity-framework-core/migrations.md>
- Quote: "private static SqliteConnection CreateDatabaseAndGetConnection()
{
    var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();

    var options = new DbContextOptionsBuilder<BookStoreDbContext>()
        .UseSqlite(connection)
        .Options;

    using (var context = new BookStoreDbContext(options))
    {
        context.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    return connection;
}"
- Accessed: 2026-08-28

**B2e6.** SQLite DOES support UNIQUE partial (filtered) indexes, since 3.8.0 (2013-08-26). SQLite is therefore not the blocker for the filtered-index migration -- this rules out the obvious explanation for the flake.

- Source: <https://www.sqlite.org/partialindex.html>
- Quote: "A partial index definition may include the UNIQUE keyword. If it does, then SQLite requires every entry in the index to be unique. This provides a mechanism for enforcing uniqueness across some subset of the rows in a table. ... The expression following the WHERE clause may contain operators, literal values, and names of columns in the table being indexed. The WHERE clause may not contain subqueries, references to other tables, non-deterministic functions, or bound parameters."
- Accessed: 2026-08-28

**B2e7.** EF Core's shared relational migrations generator emits the index filter as a WHERE clause, and EFCore.Sqlite.Core contains no IndexOptions override (search for 'IndexOptions' in src/EFCore.Sqlite.Core returned 0 results). So a model-level HasFilter WOULD be translated on SQLite -- confirming the flake points at drift, not a provider gap.

- Source: <https://github.com/dotnet/efcore/blob/main/src/EFCore.Relational/Migrations/MigrationsSqlGenerator.cs>
- Quote: "if (operation is CreateIndexOperation createIndexOperation && !string.IsNullOrEmpty(createIndexOperation.Filter)) { ... .Append(" WHERE ").Append(createIndexOperation.Filter); }"
- Accessed: 2026-08-28

**B2e8.** EF Core auto-generates 'IS NOT NULL' filters for unique indexes over nullable columns ONLY on SQL Server, via SqlServerIndexConvention. This convention never runs in the SQLite test harness, so nullable-column uniqueness is enforced under different rules in test and production.

- Source: <https://github.com/dotnet/efcore/blob/main/src/EFCore.SqlServer/Metadata/Conventions/SqlServerIndexConvention.cs>
- Quote: "if (index.IsUnique && index.IsClustered() != true && GetNullableColumns(index) is { Count: > 0 } nullableColumns) ... .Append(_sqlGenerationHelper.DelimitIdentifier(nullableColumns[i])).Append(" IS NOT NULL");"
- Accessed: 2026-08-28

**B2e9.** SQL Server and SQLite have OPPOSITE NULL semantics in unique indexes. SQL Server treats multiple NULLs as duplicates; SQLite treats every NULL as distinct. Any uniqueness rule over a nullable column is tested under the wrong semantics today.

- Source: <https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-unique-indexes>
- Quote: "You cannot create a unique index on a single column if that column contains NULL in more than one row. Similarly, you cannot create a unique index on multiple columns if the combination of columns contains NULL in more than one row. These are treated as duplicate values for indexing purposes."
- Accessed: 2026-08-28

**B2e10.** SQLite's counterpart rule, from sqlite.org -- the exact opposite of SQL Server's.

- Source: <https://www.sqlite.org/lang_createtable.html>
- Quote: "For each UNIQUE constraint on the table, each row must contain a unique combination of values in the columns identified by the UNIQUE constraint. For the purposes of UNIQUE constraints, NULL values are considered distinct from all other values, including other NULLs."
- Accessed: 2026-08-28

**B2e11.** Collation divergence, both sides confirmed. SQL Server's default for US-English locale is case-insensitive; SQLite defaults to BINARY and its NOCASE only folds ASCII -- so non-ASCII patient/provider names behave differently again.

- Source: <https://www.sqlite.org/datatype3.html>
- Quote: "Every column of every table has an associated collating function. If no collating function is explicitly defined, then the collating function defaults to BINARY. ... NOCASE - ... the 26 upper case characters of ASCII are folded to their lower case equivalents before the comparison is performed. Note that only ASCII characters are case folded. SQLite does not attempt to do full UTF case folding due to the size of the tables required."
- Accessed: 2026-08-28

**B2e12.** SQL Server side of the collation divergence: the default installation collation for OS locale English (United States) is SQL_Latin1_General_CP1_CI_AS -- the _CI suffix meaning case-insensitive.

- Source: <https://learn.microsoft.com/en-us/sql/relational-databases/collations/collation-and-unicode-support>
- Quote: "For example, for the OS locale "English (United States)" (code page 1252), the default collation during setup is SQL_Latin1_General_CP1_CI_AS ... Case-sensitive (_CS) Distinguishes between uppercase and lowercase letters. ... If this option isn't selected, the collation is case-insensitive."
- Accessed: 2026-08-28

**B2e13.** EF Core's SQLite limitations page: schemas, sequences and database-generated concurrency tokens (rowversion) are unsupported; decimal, DateTimeOffset, TimeSpan and ulong are not natively supported and fall back to client evaluation for comparison/ordering.

- Source: <https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations>
- Quote: "A couple of these concepts are not supported by the SQLite provider. - Schemas - Sequences - Database-generated concurrency tokens ... SQLite doesn't natively support the following data types. EF Core can read and write values of these types, and querying for equality (where e.Property == value) is also supported. Other operations, however, like comparison and ordering will require evaluation on the client. - DateTimeOffset - decimal - TimeSpan - ulong"
- Accessed: 2026-08-28

**B2e14.** rowversion/[Timestamp] concurrency tokens are a SQL-Server-only feature that some databases, SQLite named explicitly, cannot do at all. Any optimistic-concurrency test on SQLite is testing a different mechanism than production.

- Source: <https://learn.microsoft.com/en-us/ef/core/saving/concurrency>
- Quote: "The rowversion type shown above is a SQL Server-specific feature; the details on setting up an automatically-updating concurrency token differ across databases, and some databases don't support these at all (e.g. SQLite)."
- Accessed: 2026-08-28

**B2e15.** SQLite has no date/time storage class at all and DECIMAL gets NUMERIC affinity with silent int coercion -- relevant to datetime2 precision assertions and any monetary/decimal fields.

- Source: <https://www.sqlite.org/datatype3.html>
- Quote: "SQLite does not have a storage class set aside for storing dates and/or times. Instead, the built-in Date And Time Functions of SQLite are capable of storing dates and times as TEXT, REAL, or INTEGER values ... If a floating point value that can be represented exactly as an integer is inserted into a column with NUMERIC affinity, the value is converted into an integer."
- Accessed: 2026-08-28

**B2e16.** SQL Server datetime2 precision that SQLite cannot model: default 7 fractional digits, 100 ns accuracy.

- Source: <https://learn.microsoft.com/en-us/sql/t-sql/data-types/datetime2-transact-sql>
- Quote: "Precision, scale | 0 to 7 digits, with an accuracy of 100 nanoseconds (100 ns). The default precision is 7 digits. ... Accuracy | 100 nanoseconds"
- Accessed: 2026-08-28

**B2e17.** Testcontainers for .NET is actively maintained: Testcontainers.MsSql 4.14.0 published 2026-08-14 (two weeks before this research), with a steady monthly-to-six-weekly release cadence and 32.4M total downloads.

- Source: <https://www.nuget.org/packages/Testcontainers.MsSql>
- Quote: "4.14.0 (8/14/2026); 4.13.0 (7/2/2026); 4.12.0 (5/19/2026); 4.11.0 (3/12/2026); 4.10.0 (1/1/2026). 32.4 million total downloads."
- Accessed: 2026-08-28

**B2e18.** The Testcontainers MsSql module's default image, useful for pinning.

- Source: <https://dotnet.testcontainers.org/modules/mssql/>
- Quote: "Default Docker image tag: "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04""
- Accessed: 2026-08-28

**B2e19.** The GitHub Actions `services:` alternative has two constraints that matter for a small team: it cannot be used inside composite actions, and jobs running directly on the runner must reach it via localhost with explicit port mapping.

- Source: <https://docs.github.com/en/actions/using-containerized-services/about-service-containers>
- Quote: "you cannot create and use service containers inside a composite action ... Jobs on runner machines: You must use localhost:<port> or 127.0.0.1:<port> to access services, requiring explicit port mapping."
- Accessed: 2026-08-28

**B2e20.** Resource headroom check: a private-repo GitHub-hosted Ubuntu runner has 2 CPUs, 8 GB RAM, 14 GB SSD, comfortably above SQL Server's container minimums of 2 GB RAM and 2 GB disk. Developer edition is free and is what the container runs by default.

- Source: <https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker>
- Quote: "At least 2 GB of disk space. At least 2 GB of RAM. ... By default, this quickstart creates a container with the Developer edition of SQL Server."
- Accessed: 2026-08-28

**B2e21.** GitHub-hosted runner specs (private repos), confirming the SQL Server container fits.

- Source: <https://docs.github.com/en/actions/reference/runners/github-hosted-runners>
- Quote: "Private repository runners provide: 2 CPUs, 8 GB RAM, 14 GB SSD storage (x64 architecture)."
- Accessed: 2026-08-28

**B2e22.** A near-free CI gate exists that would have caught the filtered-index drift with no database at all: dotnet ef migrations has-pending-model-changes, added in EF Core 8.0.

- Source: <https://learn.microsoft.com/en-us/ef/core/cli/dotnet>
- Quote: "dotnet ef migrations has-pending-model-changes -- Note: This command was added in EF Core 8.0. Checks if any changes have been made to the model since the last migration."
- Accessed: 2026-08-28

**B2e23.** Microsoft's documented isolation techniques for real-DB tests -- transaction rollback per test, one DB per test class for parallelism, Respawn for cleanup -- i.e. the harness patterns to copy rather than invent.

- Source: <https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database>
- Quote: "One common technique to isolate writing tests is to wrap the test in a transaction, and to have that transaction rolled back at the end of the test. Since nothing is actually committed to the database, other tests don't see any modifications and interference is avoided. ... You may also want to consider using the respawn package, which efficiently clears out a database."
- Accessed: 2026-08-28

**B2e24.** Respawn is still maintained if cleanup later becomes the bottleneck: 7.0.0 published 2025-11-30, 32.4M total downloads. Secondary caveat: the NuGet page did not itself state SQL Server support in the fetched content, though it is Respawn's primary target.

- Source: <https://www.nuget.org/packages/Respawn>
- Quote: "The current version is 7.0.0, released on 11/30/2025. ... The package has accumulated 32.4 million total downloads across all versions."
- Accessed: 2026-08-28

### Recommended action

VERDICT: Keep SQLite as the fast inner loop; add ONE small SQL Server test project. Do NOT port 2,261 tests, and do NOT "fix" the flaky test by relaxing the SQLite assertion -- that deletes a true signal.

STEP 0 -- free, do it this week (~2 hours, no database)
Add `dotnet ef migrations has-pending-model-changes` as a required CI step for both DbContexts (EF Core 8.0+, verified). This alone catches the class of bug you just hit. Then diagnose the actual flake: check whether `Fix_UniqueIndexesExcludeSoftDeleted` put `HasFilter` in `OnModelCreating` or only in the migration file. SQLite supports UNIQUE partial indexes and EF Core's SQLite provider does emit `WHERE <filter>`, so if the filter is in the model the SQLite schema would have it. It almost certainly is not -- because ABP's test module builds the schema with `IRelationalDatabaseCreator.CreateTables()` from the model, never from your 105 migrations. Secondary hypothesis worth 20 minutes: "flakes" rather than "fails" also fits test-order/data accumulation in the single shared `:memory:` database plus a colliding RequestConfirmationNumber generator. Check both.

STEP 1 -- the SQL Server path (budget ~3-5 dev-days, fits one month's allocation)
One new xUnit project, Testcontainers.MsSql (4.14.0, published 2026-08-14 -- actively maintained), image pinned to the SQL Server version you will actually run in production. Target 30-60 tests, not 2,261. Contents, in priority order:

1. Migration gate: apply all 90 host and all 15 tenant migrations from empty against real SQL Server, assert success. Today nothing tests the artifact that builds production.
2. Tenant isolation harness -- MOVE IT, do not copy it. Two real tenant databases, real host-header resolution. Cross-tenant leakage is your highest-consequence failure and it is currently proven only against a fake.
3. Every uniqueness/constraint rule: RequestConfirmationNumber, email/username, and specifically soft-delete + filtered-unique-index interactions. SQL Server and SQLite have opposite NULL-uniqueness semantics and EF Core's `IS NOT NULL` auto-filter is SQL-Server-only.
4. Case-sensitivity-dependent lookups (email, name, confirmation-number search). SQL Server US-English default is CI; SQLite is BINARY.
5. Any `rowversion`/`[Timestamp]` concurrency token (SQLite cannot model these at all) and any `decimal` precision assertion.
Isolation: use Microsoft's documented pattern -- transaction-per-test rolled back, one database per test class for the write-heavy classes. Skip Respawn until cleanup is measurably the bottleneck.

TESTCONTAINERS vs GITHUB ACTIONS `services:` -- pick Testcontainers.
`services:` is marginally less YAML, but it only works in CI. Testcontainers makes the identical test run on both laptops with zero setup, which is what actually determines whether a two-person team keeps a suite green rather than routing around it. Concretely: `services:` cannot be used inside a composite action, requires explicit port mapping plus a hand-written mssql health check against `/opt/mssql-tools18/bin/sqlcmd` with `-No` (a well-known trap since the tools18 secure-by-default change), and needs a second connection-string code path for local runs. Testcontainers handles readiness, random ports and generated passwords in ~10 lines of C#. Resource fit is fine either way: private-repo Ubuntu runners give 2 CPU / 8 GB / 14 GB SSD against SQL Server's 2 GB RAM / 2 GB disk minimum.

MERGE GATE SHAPE
SQLite suite: every push and every PR. SQL Server suite: required on PRs to main. Given PHI, first public exposure and database-per-tenant, I would not demote it to nightly-only. Cache the image pull. If wall-clock becomes painful, shed test 4-5 to nightly before you shed 1-3.

WHAT A LARGER COMPANY WOULD DO THAT IS NOT WORTH IT HERE

- Running all 2,261 tests against SQL Server. The EF Core team does exactly this with 30,000 tests -- they have dozens of engineers. Porting yours is months of work and ~90% of those tests exercise application logic where the provider is irrelevant.
- Adopting the repository pattern, which is Microsoft's own first-choice recommendation for test doubles. It is a rearchitecture of 53k lines and fights ABP's built-in repository/DDD conventions. Explicitly reject this one; the docs even concede it "can incur significant cost to implement and maintain."
- Per-test database provisioning, Respawn-based reset infrastructure, or a version matrix across SQL Server 2019/2022/2025. Pin one version; add the rest only when something forces it.
- Mutation testing and dedicated DB performance benchmarks. Later, if ever.

ONE INFRASTRUCTURE-DEPENDENT LINE (not designed here): confirm the collation your production SQL Server databases are actually created with and pin the same value on the test container via MSSQL_COLLATION -- otherwise the new SQL Server tests validate the wrong string semantics.

### What could not be resolved, and what was tried

UNVERIFIED -- CI wall-clock cost of a SQL Server container on GitHub Actions. I could not find a primary-source benchmark for SQL Server container pull + startup time on GitHub-hosted runners; the WebSearch budget for this session (200/200) was exhausted before I could look for one, and neither Microsoft Learn nor the Testcontainers docs publish timings. I deliberately did not state a number from memory. What IS verified: the private-repo Ubuntu runner is 2 CPU / 8 GB RAM / 14 GB SSD, and SQL Server needs at least 2 GB RAM and 2 GB disk, so it fits. Measure it before committing to a merge gate: run the container job once and read the step timings; the image pull dominates and is cacheable. Budget the decision on the measured number, not an estimate.

ALSO UNVERIFIED -- I could not inspect the actual codebase. The working directory (/home/user/Aditya-gam) is a personal resume repo, not the scheduling portal, so every claim about your specific harness is inferred from ABP's published startup-template documentation rather than read from your source. In particular, verify directly: (a) whether your `*.EntityFrameworkCore.Tests` module really calls `IRelationalDatabaseCreator.CreateTables()` (ABP's documented default) or has been changed to run migrations; and (b) whether `Fix_UniqueIndexesExcludeSoftDeleted` added `HasFilter` to `OnModelCreating` or only to the migration file. Those two checks confirm or refute the model/migration-drift diagnosis in about ten minutes, and the whole Step 0 recommendation hinges on them.

SECONDARY SOURCE FLAG: the Respawn NuGet page did not explicitly state SQL Server support in the content returned, though SQL Server is its primary target. Since Respawn is a later optimization rather than a load-bearing recommendation, I did not spend further budget confirming it.

## B3. Angular CVE confirmation, independent of the baseline

**Question as posed:** Confirm the six claimed open Angular CVEs against @angular/* pinned at ~20.3.19: do they exist as described, with what CVSS scores, affected ranges, first fixed versions and real impact? (a) Which apply to a client-side-rendered SPA with no SSR? (b) Is 20.3.27 sufficient, or is a minor/major bump needed?

**Answer: `qualified-yes`**

### Reasoning

All seven CVE IDs resolve in NVD and every one has a matching GitHub Security Advisory authored by Angular as CNA, so the baseline is not fabricated -- but it is wrong in three material ways. First, it lists "CVE-2026-54268 / CVE-2026-50171" as one item; these are two distinct CVEs with separate advisories, separate functions (formatDate vs formatNumber digitsInfo) and different fix versions (20.3.25 vs 20.3.22), so the real count is seven, not six. Second, and most important for urgency: four of the seven are contingent on Angular SSR. GHSA-39pv-4j6c-2g6v states plainly that "Applications that do not employ SSR with hydration are unaffected", GHSA-q6f4-qqrg-jv6x lists "SSR and Hydration Enabled" as mandatory precondition #1, and angular.dev/guide/hydration states "Hydration can be enabled for server-side rendered (SSR) applications only" -- HttpTransferCache is a child of hydration (disabled via withNoHttpTransferCache()), so no SSR means no hydration means no transfer cache and no reachable code path. That makes CVE-2026-54267, CVE-2026-68945, CVE-2026-50170 and CVE-2026-54266 not exploitable in a pure CSR SPA -- a real drop in urgency, subject to the caveat below. Third, the baseline omits two @angular/core CVEs that the advisory database does list: CVE-2026-52725 (createComponent namespace bypass XSS, fixed 20.3.22, still open at 20.3.19) and CVE-2026-27970 (i18n ICU XSS, fixed 20.3.17, therefore already closed at 20.3.19). Sources disagree on severity in a way worth stating: NVD's primary CVSS v3.1 scores several of these as 6.1 MEDIUM while Angular's own CVSS v4.0 secondary scores are 8.6-8.8 HIGH; NVD's v3.1 vector for CVE-2026-50171 (C:L/I:L/A:N) is also internally inconsistent with a denial-of-service bug, so I would treat the Angular CNA v4.0 scores as the better guide. On (b): 20.3.27 is sufficient for all seven baseline CVEs plus both omitted ones -- the highest first-fixed version among them is 20.3.27 (CVE-2026-69151 and CVE-2026-68945) -- and no minor or major bump is required, because Angular v20 is still under LTS with dist-tag v20-lts currently at 20.3.30, which I would take instead for headroom. One honest limitation: I could not confirm whether this app uses SSR, because the working directory (/home/user/Aditya-gam) is a personal profile repo containing only resumes, a README and a headshot -- not the scheduling portal -- so the SSR conclusion is conditional on the team verifying it. Note also that the "~20.3.19" range in package.json permits anything under 20.4.0, so the yarn.lock resolved version, not the manifest, determines actual exposure and may already be higher.

### Evidence

**B3e1.** CVE-2026-69151 confirmed. i18n event-handler attribute XSS in @angular/core and @angular/compiler. CVSS v4.0 7.6 HIGH, vector CVSS:4.0/AV:N/AC:L/AT:P/PR:N/UI:P/VC:H/VI:H/VA:N/SC:N/SI:N/SA:N. Affects >=20.0.0-next.0 <20.3.27 (also <=19.2.25, 21.x <21.2.19, 22.x <22.0.1). FIRST FIXED in the 20.x line: 20.3.27. NOT SSR-contingent -- affects any app using Angular i18n translation files. Highest fix requirement of the whole set.

- Source: <https://services.nvd.nist.gov/rest/json/cves/2.0?cveId=CVE-2026-69151>
- Quote: "Angular compiler i18n pipeline permits i18n-onerror and other i18n-on event-handler attributes, allowing a lower-trust translation file to replace a static handler with executable JavaScript."
- Accessed: 2026-08-28

**B3e2.** CVE-2026-69151 advisory (GHSA-jj27-h5hq-8x99) confirms patched versions 20.3.27 / 21.2.19 / 22.0.1 and that exploitation requires attacker control or influence over the translation files. Because Angular i18n translation catalogs are build-time assets held in the repo, this is a supply-chain / insider-controlled-input risk rather than an anonymous-internet risk -- relevant given the app is about to face public traffic.

- Source: <https://github.com/angular/angular/security/advisories/GHSA-jj27-h5hq-8x99>
- Quote: "When exploited, this vulnerability allows arbitrary JavaScript execution within the context of the vulnerable application's domain if an attacker can control or influence the translation files used during localization."
- Accessed: 2026-08-28

**B3e3.** CVE-2026-54267 confirmed (GHSA-rgjc-h3x7-9mwg), @angular/core, client hydration DOM clobbering and response-cache poisoning. CVSS v4.0 8.6 HIGH; NVD primary v3.1 is only 6.1 MEDIUM (CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:C/C:L/I:L/A:N) -- sources disagree on severity. First fixed 20.3.25. SSR-CONTINGENT: the vulnerable path is Angular's hydration feature, enabled via provideClientHydration().

- Source: <https://github.com/angular/angular/security/advisories/GHSA-rgjc-h3x7-9mwg>
- Quote: "To optimize client-side bootstrap in Server-Side Rendered (SSR) environments, Angular supports Hydration via provideClientHydration()"
- Accessed: 2026-08-28

**B3e4.** CVE-2026-68945 confirmed (GHSA-jhpw-976m-542j), @angular/common, HttpTransferCache cache-key ambiguity causing cross-request response reuse. CVSS v4.0 8.8 HIGH (NVD primary v3.1 6.1 MEDIUM -- disagreement again). First fixed 20.3.27 -- tied with CVE-2026-69151 as the highest patch requirement. SSR-CONTINGENT.

- Source: <https://github.com/angular/angular/security/advisories/GHSA-jhpw-976m-542j>
- Quote: "Angular's HttpTransferCache caches HTTP requests made during Server-Side Rendering (SSR) so that they can be reused during client-side hydration."
- Accessed: 2026-08-28

**B3e5.** CVE-2026-54266 confirmed (GHSA-39pv-4j6c-2g6v), @angular/common, weak 32-bit DJB2-like cache key hashing in HttpTransferCache. CVSS v4.0 8.8 HIGH. First fixed 20.3.25. This advisory contains the single clearest statement that the entire HttpTransferCache CVE cluster is inert without SSR -- the load-bearing sentence for question (a). It also documents the workaround withNoHttpTransferCache(), which confirms transfer caching is on by default once hydration is enabled.

- Source: <https://github.com/angular/angular/security/advisories/GHSA-39pv-4j6c-2g6v>
- Quote: "Applications that do not employ SSR with hydration are unaffected."
- Accessed: 2026-08-28

**B3e6.** CVE-2026-50170 confirmed (GHSA-q6f4-qqrg-jv6x), @angular/common, information leak via default caching of credentialed responses. CVSS v4.0 8.2 HIGH / v3.1 7.5 HIGH (the two scores agree here). First fixed 20.3.22 -- the LOWEST bar of the set, meaning 20.3.19 is exposed by only three patch releases. Requires THREE simultaneous preconditions, all absent in a CSR app with no shared HTML cache.

- Source: <https://github.com/angular/angular/security/advisories/GHSA-q6f4-qqrg-jv6x>
- Quote: "SSR and Hydration Enabled: The application must use Server-Side Rendering with hydration features (e.g., provideClientHydration())"
- Accessed: 2026-08-28

**B3e7.** CVE-2026-54268 confirmed (GHSA-48r7-hpm6-gfxm), @angular/common, DoS via OOM in formatDate/DatePipe. CVSS v3.1 7.5 HIGH (A:H) and v4.0 8.2 HIGH. First fixed 20.3.25. NOT SSR-contingent -- the advisory explicitly covers client-side rendering, where the effect is a frozen browser tab rather than a server crash. Exploitation requires the format string itself to be attacker-controlled; hardcoded format strings are safe.

- Source: <https://github.com/angular/angular/security/advisories/GHSA-48r7-hpm6-gfxm>
- Quote: "The date format string passed to these utilities must be customizable or directly controlled by untrusted user input"
- Accessed: 2026-08-28

**B3e8.** CVE-2026-50171 confirmed (GHSA-p3vc-36g9-x9gr), @angular/common, DoS via OOM in formatNumber digitsInfo (DecimalPipe/PercentPipe/CurrencyPipe). CVSS v4.0 8.2 HIGH; NVD primary v3.1 is 6.1 with vector C:L/I:L/A:N, which is an XSS-shaped vector inconsistent with a DoS impact -- an apparent NVD data-quality error, since the sibling DoS CVE-2026-54268 is scored A:H. First fixed 20.3.22. NOT SSR-contingent. This is a SEPARATE CVE from CVE-2026-54268, contradicting the baseline's pairing of the two.

- Source: <https://github.com/angular/angular/security/advisories/GHSA-p3vc-36g9-x9gr>
- Quote: "The digitsInfo parameter passed to these utilities must be customizable or directly controlled by untrusted user input"
- Accessed: 2026-08-28

**B3e9.** Second, independent source confirming the SSR dependency chain that makes four of the seven CVEs unreachable in a CSR-only app. Angular's own documentation states hydration cannot exist without SSR; combined with GHSA-39pv-4j6c-2g6v (HttpTransferCache is a hydration feature), no-SSR implies no hydration implies no transfer cache implies no reachable vulnerable code path.

- Source: <https://angular.dev/guide/hydration>
- Quote: "Hydration can be enabled for server-side rendered (SSR) applications only."
- Accessed: 2026-08-28

**B3e10.** BASELINE GAP #1 -- CVE-2026-52725 (GHSA-692r-grfm-v8x7), @angular/core, dynamic component namespace bypass leading to XSS. Moderate, CVSS v4.0 5.3. Affects >=20.0.0-next.0 <20.3.22, so it IS open at 20.3.19 and the baseline misses it. NOT SSR-contingent. Preconditions: user-controlled input reaching createComponent as selector/host element without separate sanitization. Fixed by the same 20.3.27+ upgrade.

- Source: <https://github.com/advisories/GHSA-692r-grfm-v8x7>
- Quote: "mount a dynamic component on a script tag, bypassing core dynamic component creation safeguards to execute arbitrary JavaScript"
- Accessed: 2026-08-28

**B3e11.** BASELINE GAP #2 -- CVE-2026-27970 (GHSA-prjf-86w9-mfqv), @angular/core i18n ICU-message XSS, High, CVSS 7.0. Patched in 20.3.17, which is BELOW the pinned 20.3.19 -- so this one is already closed and should not be added to the open list. Included to show the omission was checked, not assumed. Mitigated further by strict CSP or Trusted Types.

- Source: <https://github.com/advisories/GHSA-prjf-86w9-mfqv>
- Quote: "Patched Versions: 21.2.0, 21.1.6, 20.3.17, 19.2.19"
- Accessed: 2026-08-28

**B3e12.** Answer to (b), with maintenance check. The npm dist-tags show v20-lts at 20.3.30 and latest at 22.1.4, confirming Angular v20 is still receiving LTS patches and the project is actively maintained (advisories published as recently as 3 Aug 2026). Since the highest first-fixed version across all nine CVEs examined is 20.3.27, staying on the 20.3.x LTS line fixes everything -- NO minor or major bump is required, and there is no need to move to v21 or v22.

- Source: <https://registry.npmjs.org/-/package/@angular/core/dist-tags>
- Quote: "v20-lts: 20.3.30 ... latest: 22.1.4"
- Accessed: 2026-08-28

**B3e13.** Cross-check of the complete 2026 advisory set for @angular/common, confirming exactly five advisories and that no @angular/common advisory requires a fix above 20.3.27. This bounds the answer to (b): nothing in the current advisory database pushes the requirement past 20.3.27.

- Source: <https://github.com/advisories?query=ecosystem%3Anpm+affects%3A%40angular%2Fcommon>
- Quote: "GHSA-jhpw-976m-542j ... Published: August 3, 2026 ... GHSA-48r7-hpm6-gfxm ... GHSA-39pv-4j6c-2g6v ... GHSA-p3vc-36g9-x9gr ... GHSA-q6f4-qqrg-jv6x ... Published: June 15, 2026"
- Accessed: 2026-08-28

**B3e14.** Cross-check of the 2026 advisory set for @angular/core, which is how the two baseline omissions were discovered. Listed CVE-2026-52725 and CVE-2026-27970 alongside the expected CVE-2026-54267.

- Source: <https://github.com/advisories?query=ecosystem%3Anpm+affects%3A%40angular%2Fcore>
- Quote: "GHSA-692r-grfm-v8x7 - Angular Template and Dynamic Component Namespace Bypass leading to Cross-Site Scripting (XSS) ... GHSA-rgjc-h3x7-9mwg ... GHSA-prjf-86w9-mfqv"
- Accessed: 2026-08-28

### Recommended action

Do one thing: bump @angular/* to 20.3.30 (current v20-lts) in a single PR. That closes all seven baseline CVEs plus the two the baseline missed, stays inside the v20 LTS line, and requires no minor or major migration -- it is a patch-range move, so the yarn.lock resolution is the only thing that actually changes. Budget it as a half-day of one developer's time, not a project. Before that PR, spend fifteen minutes confirming the SSR question, because it determines whether this is urgent or routine: grep package.json for "@angular/ssr", look for server.ts / main.server.ts, check angular.json for a "server", "ssr" or "prerender" builder target, and grep the app bootstrap for provideClientHydration. If all four are absent, then CVE-2026-54267, CVE-2026-68945, CVE-2026-50170 and CVE-2026-54266 are NOT exploitable in this app and the baseline should be corrected to say so -- the remaining real exposure is the two DoS bugs and the two XSS bugs, none of which is a cross-tenant risk. Also correct the baseline document itself on three points: split CVE-2026-54268 and CVE-2026-50171 into separate rows, add CVE-2026-52725, and drop CVE-2026-27970 as already-fixed at 20.3.19. For the DoS pair, additionally grep for DatePipe/DecimalPipe/CurrencyPipe usages and formatDate/formatNumber calls where the format string or digitsInfo comes from user input, tenant config or a query parameter; if every format string is a hardcoded literal (the normal case) those two CVEs have no reachable trigger regardless of version, and the patch is belt-and-braces. Given two SDE-1s at roughly one developer-week per month, do NOT stand up a separate Angular CVE triage process for this -- put `yarn npm audit` (or Dependabot on the private repo, which is free) as a non-blocking CI job that opens a PR, and reserve the human review for advisories that are actually reachable. One line on the out-of-scope boundary: CVE-2026-50170's third precondition is a shared CDN or reverse-proxy cache in front of the app, which is an infrastructure decision covered by the separate exercise -- but it is moot here if there is no SSR.

### What could not be resolved, and what was tried

Two things remain unverified. (1) Whether this app actually uses SSR -- I could not check, because the working directory /home/user/Aditya-gam is a personal profile repo (three resume PDFs, README.md, headshot1.webp) with no package.json, angular.json or Angular source anywhere; the scheduling portal is not present in this session. Every SSR conclusion is therefore stated conditionally, with the exact greps to run in the recommended action. ABP Commercial Angular apps are conventionally CSR, but ABP does ship SSR support, so I deliberately did not assume. (2) I could not open angular.dev/api/platform-browser/withHttpTransferCache to get first-party confirmation that transfer caching is on-by-default under hydration -- angular.dev is a client-rendered SPA and WebFetch returned the marketing homepage instead of the API page. I substituted two acceptable sources: the withNoHttpTransferCache() opt-out workaround documented in GHSA-39pv-4j6c-2g6v (an opt-out implies default-on) and the "Information Leak via Default Caching" title of GHSA-q6f4-qqrg-jv6x. Tooling notes: api.github.com/repos/angular/angular/releases returned HTTP 403 through the proxy and the GitHub MCP list_releases was denied ("repository angular/angular is not configured for this session; allowed repositories: aditya-gam/aditya-gam"), so I established Angular's release recency and active maintenance from the npm registry dist-tags plus the August 2026 advisory publication dates instead. WebSearch hit its 200-call session budget partway through, so all remaining verification was done via direct WebFetch against NVD and GitHub advisory URLs; every source_url listed above is one I actually opened and got content back from.

## B4. First-party .NET 10 / ABP CI guidance

**Question as posed:** What do Microsoft and ABP themselves publish about building and testing a .NET 10 / ABP application in CI -- .NET 10 support lifecycle, dotnet test vs Microsoft.Testing.Platform, NuGet lockfiles and RestoreLockedMode, dotnet package list --vulnerable/--deprecated, TreatWarningsAsErrors vs EnforceCodeStyleInBuild, ABP's own CI guidance -- and specifically, are the built-in CA security analyzers (CA2100, CA3001-CA3012, CA5xxx) enabled by AnalysisLevel=latest, or do they require explicit opt-in via AnalysisMode?

**Answer: `qualified-no`**

### Reasoning

The headline answer is no: `AnalysisLevel` selects *which version* of the analyzer rule set ships, not *how many* rules are on, and its default is already `latest`, so setting it changes nothing about security coverage. Rule enablement is governed by `AnalysisMode`, whose default is `Default`, described by Microsoft as a mode where "only a small number of rules are enabled as build warnings" -- and the .NET 10 default-enabled table on that same page contains rules from Interoperability, Performance, Reliability and Usage only, with **not one rule from the Security category**. I verified this at the individual rule level: both CA2100 (Review SQL queries for security vulnerabilities) and CA3001 (Review code for SQL injection vulnerabilities) state "Enabled by default in .NET 10: **No**". So on a HIPAA-regulated, internet-facing ABP app, the entire CA2100/CA3xxx/CA5xxx security band is currently dead code in the build -- this is the free, zero-cost win the task suspected, and it is real. The documented opt-in is the category-scoped property `<AnalysisModeSecurity>All</AnalysisModeSecurity>` (Microsoft documents `AnalysisMode<Category>` with `Security` among the valid categories), which turns on security rules *without* the noise of `AnalysisMode=All` across Design/Naming/Documentation -- the single most important configuration line in this whole report, and the only one I'd call unambiguously worth a two-person team's time. Caveat before you gate on it: CA3001-class taint rules "can't track data across assemblies" and have a configurable interprocedural depth limit, so in a layered ABP solution (Application -> EntityFrameworkCore -> Domain) they will miss cross-project flows and are best treated as warnings on the HTTP-facing projects, not a hard merge gate on day one. On the other four areas, first-party guidance is more reassuring than expected. .NET 10 is **LTS**, GA 2025-11-11, supported to **2028-11-14** (current patch 10.0.11, 2026-08-11) -- a correct platform choice with a three-year runway; note in passing that .NET 8 and .NET 9 both fall out of support on 2026-11-10, roughly ten weeks out, so any tooling or container image still pinned to those needs to move. On testing, .NET 10 introduced *runner selection* -- MTP is opted into via `global.json` `{"test":{"runner":"Microsoft.Testing.Platform"}}` -- but Microsoft is explicit that "VSTest ... is the current default", declines to declare a winner ("If your specific use case isn't listed, both platforms are valid choices"), and warns "Don't mix VSTest-based and MTP-based .NET test projects in the same solution"; decisively, ABP's own repo at v10.7 still ships **xunit 2.9.3 + xunit.runner.visualstudio 3.1.4 + Microsoft.NET.Test.Sdk 17.14.1**, i.e. VSTest and xunit v2, so migrating to MTP/xunit v3 means diverging from the framework's template for no gate-related benefit -- my recommendation is stay on VSTest and revisit when ABP moves. On dependencies, the team is likely already better covered than it knows: `NuGetAuditMode` **defaults to `all` for projects targeting net10.0+**, so restore already audits transitive packages and emits NU1901-NU1904, meaning the cheap gate is a CI-only `WarningsAsErrors` on those codes (Microsoft publishes the exact conditional-property pattern for this) rather than bolting on OWASP Dependency-Check; by contrast `dotnet package list --vulnerable` does *not* include transitive packages unless you pass `--include-transitive`, and cannot be combined with `--deprecated`, so it needs two invocations and is the weaker mechanism. Two live gaps remain: `RestoreLockedMode=false` in the repo is only a real problem if CI does not pass `dotnet restore --locked-mode` -- the documented pattern is exactly that split (property false so developers can add packages locally, flag on in CI for repeatable restore), so verify the workflow before "fixing" the property, and pair it with `global.json` `rollForward: disable` because SDK-implicit PackageReferences otherwise break locked mode; and `EnforceCodeStyleInBuild=false` does mean IDE* rules never run on a command-line or CI build (code-style analysis is "disabled, by default, for all .NET projects on command-line builds"), though this is cosmetic, not a security gap, and even setting it true does nothing until severities are raised in `.editorconfig` -- so I would leave it off and spend the week on `AnalysisModeSecurity` instead. Finally, ABP publishes essentially **no** CI guidance and ships **no** analyzers: its docs have a Deployment section but no CI/GitHub Actions page, its testing docs stop at xunit/NSubstitute/Shouldly and layered test projects, and its repo-wide `Directory.Build.props`/`common.props` set no `TreatWarningsAsErrors`, no `AnalysisMode`, no `EnforceCodeStyleInBuild` and no lockfiles -- the only observable ABP "standard" is its own workflow (dotnet 10.0.x, build-all.ps1/test-all.ps1, Codecov, plus a separate CodeQL workflow), which is worth copying only in that last respect. Two flags on sources disagreeing, and one on what I could not verify, are in the evidence and unresolved sections below.

### Evidence

**B4e1.** .NET 10 is an LTS release: GA 2025-11-11, end of support 2028-11-14, latest patch 10.0.11 (2026-08-11), support phase Active. Same table: .NET 9 (STS) and .NET 8 (LTS) BOTH end support 2026-11-10 -- about ten weeks from today.

- Source: <https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core>
- Quote: ".NET 10 | November 11, 2025 | 10.0.11 | August 11, 2026 | LTS | Active | November 14, 2028"
- Accessed: 2026-08-28

**B4e2.** AnalysisMode's DEFAULT value is `Default`, in which only a handful of rules are build warnings. The .NET 10 'enabled by default' table on this page lists ONLY Interoperability, Performance, Reliability and Usage rules -- no Security-category rule appears. AnalysisLevel's default is `latest`, which governs rule *vintage*, not rule *count*. The documented escape hatch under -warnaserror is CodeAnalysisTreatWarningsAsErrors=false.

- Source: <https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview>
- Quote: "In the default analysis mode (`Default`), only a small number of rules are enabled as build warnings. ... The default value for the `AnalysisLevel` property is `latest`, which means you always get the latest code analysis rules as you move to newer versions of the .NET SDK."
- Accessed: 2026-08-28

**B4e3.** Rule-level confirmation #1 that security rules are off by default in .NET 10. CA2100 is the classic 'SQL built from string concatenation' rule -- directly relevant to a PHI system on SQL Server.

- Source: <https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2100>
- Quote: "| **Rule ID** | CA2100 | ... | **Category** | Security | ... | **Enabled by default in .NET 10** | No |"
- Accessed: 2026-08-28

**B4e4.** Rule-level confirmation #2, plus the two limits that should shape expectations: no cross-assembly taint tracking, and a configurable interprocedural depth cap. In a layered ABP solution this means real misses -- treat as a warning-level signal, not proof of absence.

- Source: <https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca3001>
- Quote: "**Enabled by default in .NET 10** | No ... This rule can't track data across assemblies. For example, if one assembly reads the HTTP request input and then passes it to another assembly that executes the SQL command, this rule won't produce a warning. ... There is a configurable limit to how deep this rule will analyze data flow across method calls."
- Accessed: 2026-08-28

**B4e5.** THE FIX. Microsoft documents category-scoped analysis properties. `Security` is a valid category, so security rules can be turned on to `All` without dragging in Design/Naming/Documentation noise. Valid categories: Design, Documentation, Globalization, Interoperability, Maintainability, Naming, Performance, SingleFile, Reliability, Security, Style, Usage.

- Source: <https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#analysismodecategory>
- Quote: "This property is the same as AnalysisMode, except that it only applies to a specific category of code-analysis rules... <PropertyGroup>
  <AnalysisModeSecurity>All</AnalysisModeSecurity>
</PropertyGroup>"
- Accessed: 2026-08-28

**B4e6.** EnforceCodeStyleInBuild=false is why IDExxxx rules never fire in CI -- code-style analysis is off by default on command-line builds. But turning it on is not sufficient on its own: each IDE rule must also be raised to warning/error in .editorconfig, and a handful remain VS-IDE-only for performance reasons. This is a style gap, not a security gap.

- Source: <https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview>
- Quote: "Command-line build: Code-style analysis is *disabled*, by default, for all .NET projects on command-line builds. ... (However, for performance reasons, a handful of code-style rules will still apply only in the Visual Studio IDE.)"
- Accessed: 2026-08-28

**B4e7.** TreatWarningsAsErrors semantics, plus a caveat that matters for CI scripting: the csproj property is compiler-scoped, while the MSBuild -warnaserror command-line switch covers all tasks and (unlike the property) still emits the warning text. For a CI gate you generally want the command-line switch so failures are readable in the log.

- Source: <https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/errors-warnings>
- Quote: "TreatWarningsAsErrors only impacts the C# compiler, not any other MSBuild tasks in your *csproj* file. The `warnaserror` command line switch impacts all tasks. Secondly, the compiler doesn't produce any output on any warnings when *TreatWarningsAsErrors* is used."
- Accessed: 2026-08-28

**B4e8.** NuGetAudit runs during restore and DEFAULTS TO TRANSITIVE COVERAGE on net10.0+ (NuGetAuditMode=all), at NuGetAuditLevel=low, emitting NU1901-NU1904. Microsoft publishes a ready-made CI pattern for failing only an audit pipeline on these codes. There is also a documented way to assert audit actually ran (RestoreProjectsAuditedCount vs RestoreProjectCount) -- worth having, since a silently-skipped audit is worse than none.

- Source: <https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages>
- Quote: "`NuGetAuditMode` defaults to `all` when a project targets `net10.0` or higher. Otherwise `NuGetAuditMode` defaults to `direct`. ... <WarningsAsErrors Condition=" '$(AuditPipeline)' == 'true' ">$(WarningsAsErrors);$(NuGetAuditCodes)</WarningsAsErrors>"
- Accessed: 2026-08-28

**B4e9.** dotnet package list --vulnerable does NOT include transitive packages unless --include-transitive is passed, and cannot be combined with --deprecated or --outdated (so deprecation needs a second invocation). --vulnerable requires .NET SDK 9.0.300+. The auditing page states the transitive caveat explicitly.

- Source: <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list>
- Quote: "`--vulnerable` Lists packages that have known vulnerabilities. Cannot be combined with `--deprecated` or `--outdated` options. Available starting in **.NET SDK 9.0.300** ... [and, from the auditing page] Note that `--include-transitive` is not default, so should be included."
- Accessed: 2026-08-28

**B4e10.** What locked mode actually guarantees, and the documented CI split: keep the property off for local dev, pass --locked-mode in CI. Critical gotcha for an ABP repo -- SDK-implicit PackageReferences change with SDK version and will break locked mode unless global.json pins rollForward: disable.

- Source: <https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files>
- Quote: "If locked mode is `true`, restore will either restore the exact packages as listed in the lock file or fail if you updated the defined package dependencies for the project after lock file was created. ... For CI/CD and other scenarios, where you would not want to change the package dependencies on the fly, you can do so by setting the `lockedmode` to `true` ... you can use a global.json file, setting the global.json rollForward Policy to disable."
- Accessed: 2026-08-28

**B4e11.** Lock files are framed by Microsoft as a supply-chain control (content hashing / repeatability), not just a convenience -- relevant to a HIPAA posture. Same page recommends a repo-root nuget.config with <clear/>, and flags that CI agents not reset between builds carry credential-leak risk.

- Source: <https://learn.microsoft.com/en-us/nuget/concepts/security-best-practices>
- Quote: "Lock files store the hash of your package's content. If the content hash of a package you want to install matches with the lock file, it will ensure package repeatability."
- Accessed: 2026-08-28

**B4e12.** SOURCES DISAGREE on when transitive auditing became the default. This page says .NET 9 / VS 17.12; the auditing-packages page says NuGetAuditMode defaults to `all` only for net10.0+ projects and lists the default change under NuGet 7.0 / .NET 10 SDK. I treat auditing-packages as authoritative (more specific, corroborated by its own feature-availability table). Practical impact is nil here since the project targets net10.0 either way.

- Source: <https://learn.microsoft.com/en-us/nuget/concepts/security-best-practices>
- Quote: ".NET 8 and Visual Studio 17.8 added NuGetAudit, which will warn about direct packages with known vulnerabilities during restore. .NET 9 and Visual Studio 17.12 changed the default to warn about transitive packages as well."
- Accessed: 2026-08-28

**B4e13.** APPARENT CONFLICT, resolved: the C# compiler docs say TreatWarningsAsErrors is compiler-only, but the NuGet docs say NuGet observes it. Resolution -- NuGet's restore task reads the MSBuild property itself, so NU-coded restore/audit warnings DO respond to it. Net effect: if you set TreatWarningsAsErrors=true repo-wide, a newly published advisory will break the build at restore time; Microsoft's own suggested mitigation is WarningsNotAsErrors for NU1901-NU1904.

- Source: <https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files>
- Quote: "NuGet observes the following warning properties: `TreatWarningsAsErrors`, treat all warnings as errors. ... you can use <WarningsNotAsErrors>$(WarningsNotAsErrors);NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors> to prevent vulnerabilities discovered in the future from breaking your build."
- Accessed: 2026-08-28

**B4e14.** .NET 10 added runner selection to dotnet test, but VSTest remains the default and MTP is strictly opt-in via global.json. Cross-referenced against the What's New page, which states the same thing.

- Source: <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test>
- Quote: "Test runner selection is available starting with .NET 10 SDK. In earlier versions of .NET, tests are always executed with VSTest. ... Note: `VSTest` is a valid value for test runner. It is the current default and can be omitted."
- Accessed: 2026-08-28

**B4e15.** Microsoft explicitly declines to declare MTP the winner, and warns against mixing platforms in one solution -- which is decisive for an ABP solution with 5+ template test projects. None of the listed 'choose MTP' triggers (Native AOT, WinUI 3 unpackaged, dotnet run/watch test apps) apply to this app.

- Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/test-platforms-overview>
- Quote: "Don't mix VSTest-based and MTP-based .NET test projects in the same solution or run configuration because that scenario isn't supported. ... If your specific use case isn't listed, both platforms are valid choices."
- Accessed: 2026-08-28

**B4e16.** MTP supports xunit only via xunit.net's own v3 (or v2) integration -- so 'move to MTP' on this stack implies 'migrate to xunit v3'. MTP requires .NET 8+ and is a separate NuGet-delivered platform, not an SDK default.

- Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro>
- Quote: "xUnit.net. For more information, see Microsoft Testing Platform (xUnit.net v3) ... MTP supports .NET (.NET 8 and later)"
- Accessed: 2026-08-28

**B4e17.** xunit v3 has MTP support built in natively (v2 needed shims), and on SDK 10+ the dotnet test wiring is via global.json rather than the older TestingPlatformDotnetTestSupport property. xunit even advises keeping xunit.runner.visualstudio + Microsoft.NET.Test.Sdk during transition -- i.e. the migration is not a clean swap.

- Source: <https://xunit.net/docs/getting-started/v3/microsoft-testing-platform>
- Quote: "Unlike our support for VSTest, our support for Microsoft Testing Platform is built natively into xUnit.net v3. ... [keep xunit.runner.visualstudio and Microsoft.NET.Test.Sdk] until you can be certain that all your supported versions of your development environments are using MTP instead of VSTest."
- Accessed: 2026-08-28

**B4e18.** DECISIVE for the MTP question: ABP itself, on the dev branch at v10.7 targeting net10.0, is still on xunit v2 and VSTest via central package management. Migrating this app to MTP/xunit v3 would put it ahead of the framework whose template it uses, with no gate benefit.

- Source: <https://raw.githubusercontent.com/abpframework/abp/dev/Directory.Packages.props>
- Quote: "xunit: 2.9.3 | xunit.runner.visualstudio: 3.1.4 | Microsoft.NET.Test.Sdk: 17.14.1 | Shouldly: 4.3.0 | NSubstitute: 5.3.0 | coverlet.collector: 6.0.4 | ManagePackageVersionsCentrally: true. Not found: xunit.v3, Microsoft.Testing.Platform."
- Accessed: 2026-08-28

**B4e19.** ABP's published testing guidance is framework/layout only -- xunit, NSubstitute, Shouldly, and the Domain.Tests / Application.Tests / EntityFrameworkCore.Tests / Web.Tests / TestBase project split. It explicitly presents these as replaceable defaults, and says nothing about CI, coverage thresholds, or SQLite in-memory.

- Source: <https://abp.io/docs/latest/testing>
- Quote: "xUnit as the test framework ... NSubstitute as the mocking library ... Shouldly as the assertion library ... you are free to replace them with your favorite tools."
- Accessed: 2026-08-28

**B4e20.** ABP publishes NO CI/CD guidance. The docs tree has Get Started, Tutorials, Tools, Framework, Low-Code, Solution Templates, Modules, UI Themes, Testing, Deployment, Samples, Books, Release Info, Others -- Deployment covers SSL/OpenIddict/clustering/forwarded headers, but there is no GitHub Actions, pipeline, analyzer, or code-quality page. Conclusion: there is no ABP house standard to align to; Microsoft's guidance is the only first-party authority for this repo's CI.

- Source: <https://abp.io/docs/latest>
- Quote: "The documentation does not appear to contain dedicated sections for GitHub Actions, NuGet package sources, code analyzers, or explicit CI/CD pipeline configuration guidance."
- Accessed: 2026-08-28

**B4e21.** ABP ships no repo-wide quality bar of its own -- no TreatWarningsAsErrors, no AnalysisMode/AnalysisLevel, no EnforceCodeStyleInBuild, no lockfiles. common.props sets only LangVersion=latest, NoWarn CS1591;CS0436, GenerateDocumentationFile and SourceLink; Directory.Build.props only detects test projects and adds coverlet. So adopting AnalysisModeSecurity is a net-new control, not a deviation from ABP.

- Source: <https://raw.githubusercontent.com/abpframework/abp/dev/common.props>
- Quote: "<LangVersion>latest</LangVersion> ... <NoWarn>$(NoWarn);CS1591;CS0436</NoWarn> ... <PackageReference Include="Microsoft.SourceLink.GitHub">"
- Accessed: 2026-08-28

**B4e22.** ABP's DE FACTO CI standard, from its own workflow: .NET SDK 10.0.x on ubuntu-22.04, build-all.ps1 + test-all.ps1, NuGet caching, Codecov, 50-minute timeout -- plus a SEPARATE codeql-analysis.yml. The transferable lesson is the shape: one build+test job, and security scanning as its own scheduled workflow rather than a PR-blocking step.

- Source: <https://raw.githubusercontent.com/abpframework/abp/dev/.github/workflows/build-and-test.yml>
- Quote: "dotnet-version: 10.0.x ... ./build-all.ps1 ... ./test-all.ps1 ... [workflows include] build-and-test.yml, codeql-analysis.yml"
- Accessed: 2026-08-28

**B4e23.** MAINTENANCE CHECK (required by rules of evidence): ABP is actively maintained -- stable 10.6.0 on 2026-07-27 and 10.7.0-rc.3 on 2026-08-18, i.e. a monthly minor cadence. The app is pinned to ABP 10.0.2, roughly six minor versions behind on a framework that ships monthly; that upgrade lag is itself a supply-chain finding, though ABP 10.x is the current major line so this is drift, not obsolescence.

- Source: <https://github.com/abpframework/abp/releases.atom>
- Quote: "10.7.0-rc.3 -- Updated: 2026-08-18T09:38:41Z; 10.7.0-rc.2 -- 2026-08-11T15:59:17Z; 10.7.0-rc.1 -- 2026-08-04T11:46:07Z; 10.6.0 -- 2026-07-27T08:14:12Z"
- Accessed: 2026-08-28

**B4e24.** Package pruning is ON by default for net10.0+ targets, which reduces false positives from dependency scanners -- relevant because it means the audit signal on this repo should already be relatively clean, weakening the case for adding a second scanner such as OWASP Dependency-Check.

- Source: <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk>
- Quote: "Starting in .NET 10, the NuGet Audit feature can prune framework-provided package references that aren't used by the project. This feature is enabled by default for all frameworks of a project that targets >= .NET 10.0 ... It also can lead to a reduction in false positives from NuGet Audit and other dependency-scanning mechanisms."
- Accessed: 2026-08-28

### Recommended action

Adopt five first-party controls, in this order. They cost roughly one developer-week total to stand up and near-zero per-PR maintenance, which is the whole point for two SDE 1s.

1. TURN ON THE SECURITY ANALYZERS (highest value, ~1 hour, free). Add to Directory.Build.props: `<AnalysisModeSecurity>All</AnalysisModeSecurity>`. Leave AnalysisLevel at its default `latest` -- it is already `latest` and is not the lever. Do NOT use `AnalysisMode=All`, which enables Design/Naming/Documentation and will bury two people. Run it once locally, triage the list, baseline anything you will not fix now via `dotnet_diagnostic.CAxxxx.severity` in .editorconfig with a dated comment, then let it run at warning severity. Promote to error only for the handful you have actually driven to zero (CA2100, CA3001, CA5350/CA5351-class crypto) -- a category-wide error gate on day one is exactly the thirty-checks failure mode. Expect a build-time increase from the dataflow rules; if it hurts, scope AnalysisModeSecurity to the HttpApi/Web projects only.

2. GATE ON THE AUDIT YOU ALREADY HAVE (~2 hours). Because the projects target net10.0, restore is already auditing transitive packages at severity `low`. Do not add a new scanner. Use Microsoft's published conditional pattern: define NuGetAuditCodes = NU1900;NU1901;NU1902;NU1903;NU1904;NU1905, set WarningsAsErrors on them only when an `AuditPipeline` property is true, and WarningsNotAsErrors otherwise. Run `dotnet restore -p:AuditPipeline=true` in a nightly/weekly workflow, not on every PR, so a newly published advisory never blocks an unrelated bug fix at 5pm. Consider `NuGetAuditLevel=moderate` if low-severity noise proves unactionable. Add the RestoreProjectsAuditedCount vs RestoreProjectCount assertion in Directory.Solution.targets so a silently-skipped audit fails loudly.

3. MAKE CI RESTORE REPEATABLE (~half a day). First check the workflow before changing anything: `RestoreLockedMode=false` in the props file is the *documented* arrangement if CI passes `--locked-mode`. If it does not, the fix is `dotnet restore --locked-mode` in CI, not flipping the property (flipping it blocks developers from adding packages locally). Commit packages.lock.json for the deployable projects. Pin the SDK in global.json with `"rollForward": "disable"` at the same time, or SDK-implicit PackageReferences will break locked mode on the next runner image update and you will disable the whole thing in frustration.

4. LEAVE THE TEST RUNNER ALONE. Stay on VSTest + xunit v2. ABP itself is still there at v10.7, Microsoft still defaults to it, and Microsoft warns against mixing platforms in one solution. Revisit only when ABP's template moves to xunit v3 -- then it is a global.json one-liner plus a package swap. Spend the testing budget on integration tests through ABP's TestBase for tenant-resolution and authorization paths instead, which is where this app's actual risk lives.

5. RUN CODEQL WEEKLY, NOT PER-PR. ABP's own repo separates codeql-analysis.yml from build-and-test.yml; copy that shape. GitHub Actions on a private repo, default C#/TypeScript queries, scheduled -- findings land in the Security tab as a queue to work rather than a merge blocker.

WHAT A LARGER COMPANY WOULD DO THAT IS NOT WORTH IT HERE YET, and why: (a) a second dependency scanner such as OWASP Dependency-Check on top of NuGetAudit -- NuGetAudit already covers transitive packages by default on net10.0 and pruning cuts its false positives, so the second tool buys duplicate findings and a second triage queue; (b) `AnalysisMode=All` or `latest-all` repo-wide -- hundreds of Design/Naming/Documentation diagnostics that two people will suppress wholesale, destroying the signal from the security rules that actually matter; (c) migrating to MTP/xunit v3 now -- pure churn against both Microsoft's default and ABP's own choice, with zero effect on defect escape rate; (d) `EnforceCodeStyleInBuild=true` with escalated IDE severities as a merge gate -- it is formatting, it fails builds for whitespace, and `dotnet format --verify-no-changes` covers the same ground more cheaply if you want it at all; (e) TreatWarningsAsErrors=true repo-wide -- tempting, but it makes any newly published advisory (NU1901-1904) break unrelated PRs at restore time; if you do adopt it, add Microsoft's WarningsNotAsErrors for those four codes and keep the failure in the audit pipeline instead. One infrastructure line, then dropping it per scope: whether GitHub-hosted runners can reach the ABP Commercial authenticated NuGet feed is a hosting/licensing question for the separate infrastructure exercise.

CAVEAT ON THIS REPORT'S SCOPE: I could not inspect the actual application. The session's working directory is /home/user/Aditya-gam, an unrelated personal repo (resumes, README.md, headshot) with no .sln, Directory.Build.props, Directory.Packages.props or global.json. Every claim above about the app's current settings -- RestoreLockedMode=false, EnforceCodeStyleInBuild=false, AnalysisLevel=latest, ABP 10.0.2 -- is taken from the task brief, not verified against source. Before acting, confirm against the real Directory.Build.props and .github/workflows, especially whether CI already passes --locked-mode (item 3), since that changes the recommendation.

### What could not be resolved, and what was tried

Three things I could not verify, marked unverified rather than guessed:

1. ABP COMMERCIAL CI AUTHENTICATION -- unverified, and a genuine gap. ABP's CLI docs describe `login`/`logout` as interactive commands and say nothing about non-interactive CI authentication: no ABP_API_KEY, no access-token flow, no private-feed configuration guidance. I fetched <https://abp.io/docs/latest/cli> and the docs root <https://abp.io/docs/latest> and found no CI/CD or package-source page anywhere in the tree. Since this app is on ABP Commercial 10.0.2, the GitHub Actions workflow must authenticate to the commercial feed somehow, and there is no first-party documented pattern I could cite. Ask the team how their current workflow authenticates, and treat any answer as unvalidated against vendor guidance.

2. "ABP SHIPS NO ANALYZERS" -- moderate confidence, a negative I could not prove exhaustively. Basis: no analyzer PackageReference in ABP's common.props or Directory.Build.props, and no analyzer page in the docs tree. I did not enumerate every Volo.Abp.* package on nuget.org, so a niche analyzer package could exist. Safe conclusion either way: ABP publishes no *recommended analyzer configuration*, so there is no ABP standard to conflict with.

3. EXIT-CODE BEHAVIOUR OF `dotnet package list --vulnerable` -- unverified. The Microsoft reference page documents the options and the `--format json` output but states no non-zero exit code on findings, and I could not confirm one without running it. Do not build a merge gate on `dotnet package list --vulnerable` alone assuming it fails the step; either parse the JSON output explicitly, or use the restore-time NU1901-1904 route in recommendation 2, which has documented failure semantics.

Additional constraint on this session: the WebSearch budget was exhausted (200/200 calls) before I began, so all 20+ sources were reached by direct WebFetch against URLs I constructed from known primary-source domains (learn.microsoft.com, dotnet.microsoft.com, abp.io, xunit.net, raw.githubusercontent.com). Every source_url listed is one I actually opened and read; none are cited from memory. The GitHub MCP tools were unavailable for this research -- they are scoped to aditya-gam/aditya-gam only and returned "Access denied" for abpframework/abp -- so ABP repo facts came from raw.githubusercontent.com and the releases atom feed instead. No OWASP, NIST, W3C or web.dev sources are cited here because this sub-task was scoped to Microsoft and ABP first-party guidance; the ASVS/WCAG/Core Web Vitals areas are presumably covered by a sibling task.
