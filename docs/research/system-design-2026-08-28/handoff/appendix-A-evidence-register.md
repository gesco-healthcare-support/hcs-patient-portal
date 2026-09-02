# Appendix A: evidence register

> Every sourced claim produced by the fourteen area agents and the five question agents, with its
> source URL, access date, the agent's self-declared confidence, and - where the adversarial
> verification pass judged that specific claim - its verdict.

**Verdict key.** `yes` the page supports the claim as stated. `partially` the substance holds but
something in the claim is imprecise, mis-scoped, mis-attributed or stale - the correction is in the
note. `no` the page does not support the claim. `could-not-check` the source was unreachable.

**`REASONING` in the confidence column** means the agent flagged the item as its own judgement
rather than sourced fact. Those are argued in `system-design-target.md`, not cited.

**Totals: 526 claims re-verified - 347 yes, 161 partially, 12 no, 6 could-not-check.**

---

## Area: tenancy-data

Verification verdict for this area: **material-errors** (40 claims checked)

### A1.1  [yes]

**Claim.** Microsoft's tenancy model comparison table rates scale as: standalone app medium (1-100s), database-per-tenant high (1-100,000s), sharded multitenant unlimited (1-1,000,000s); and rates tenant isolation as high, high, and low respectively. Operational complexity for database-per-tenant is rated 'Low-Medium. Patterns address complexity at scale.'

**Limit or threshold asserted.** database-per-tenant: 1-100,000s of tenants

- Source: Microsoft Learn - Multitenant SaaS Patterns, Azure SQL Database
- URL: <https://learn.microsoft.com/en-us/azure/azure-sql/database/saas-tenancy-app-design-patterns>
- Second source: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/storage-data>
- Accessed: 2026-08-31
- Confidence: verified

### A1.2  [yes]

**Claim.** Microsoft states that a multitenant (shared) database 'necessarily sacrifices tenant isolation' and that 'the Azure system has no built-in way to monitor or manage the use of these resources by an individual tenant', creating increased noisy-neighbour risk.

- Source: Microsoft Learn - Multitenant SaaS Patterns, Azure SQL Database, section E
- URL: <https://learn.microsoft.com/en-us/azure/azure-sql/database/saas-tenancy-app-design-patterns>
- Accessed: 2026-08-31
- Confidence: verified

### A1.3  [partially]

**Claim.** AWS states that tenant isolation is separate from authentication and authorisation: 'the fact that a tenant user is authenticated does not mean that your system has achieved isolation... a user could be authenticated and authorized, and still access the resources of another tenant.' Data partitioning describes how data is stored; it does not by itself achieve isolation.

- Source: AWS Whitepaper - SaaS Architecture Fundamentals, Tenant isolation
- URL: <https://docs.aws.amazon.com/whitepapers/latest/saas-architecture-fundamentals/tenant-isolation.html>
- Second source: <https://docs.aws.amazon.com/wellarchitected/latest/saas-lens/silo-isolation.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The authentication/authorisation quotes are verbatim and correct. Two problems. (a) The second sentence of the claim ('Data partitioning describes how data is stored; it does not by itself achieve isolation') does not appear on this page at all -- the page never discusses data partitioning; that point lives on a different page of the whitepaper and must be cited separately or dropped. (b) The page opens with 'This whitepaper is for historical reference only. Some content might be outdated and some links might not be available.' -- it is a deprecated source, which claim 5 implies applies only to the tenant-isolation-strategies paper. Cite the current Well-Architected SaaS Lens for anything load-bearing.

### A1.4  [yes]

**Claim.** AWS's SaaS Lens silo-model cons state that 'having 20 siloed accounts for each of your tenants might be manageable. However, if you have a thousand tenants, that number would likely begin to impact operational efficiency and agility', and name onboarding automation and decentralised management and monitoring as the specific overheads.

**Limit or threshold asserted.** ~20 silos manageable; ~1000 degrades operations

- Source: AWS Well-Architected SaaS Lens - Silo isolation
- URL: <https://docs.aws.amazon.com/wellarchitected/latest/saas-lens/silo-isolation.html>
- Accessed: 2026-08-31
- Confidence: verified

### A1.5  [partially]

**Claim.** The AWS SaaS Tenant Isolation Strategies whitepaper landing page is now labelled 'This whitepaper is for historical reference only. Some content might be outdated'; publication date August 1, 2020. The Well-Architected SaaS Lens pages remain current and carry a 2026 copyright.

**Limit or threshold asserted.** published 2020-08-01, marked historical

- Source: AWS Whitepaper - SaaS Tenant Isolation Strategies
- URL: <https://docs.aws.amazon.com/whitepapers/latest/saas-tenant-isolation-strategies/saas-tenant-isolation-strategies.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The historical-reference banner and the 1 August 2020 publication date are confirmed verbatim. The second half of the claim is not settled by this URL and is partly wrong elsewhere: (a) no copyright year, 2026 or otherwise, was retrievable from the SaaS Lens pages, so 'carry a 2026 copyright' is unverified; (b) the framing that the SaaS Lens pages 'remain current' while only this whitepaper is historical is misleading, because the SaaS Architecture Fundamentals whitepaper cited in claim 3 carries the same historical-reference banner. Restate as: this whitepaper and the SaaS Architecture Fundamentals whitepaper are both marked historical reference only; only the Well-Architected SaaS Lens pages carry no such banner.

### A1.6  [partially]

**Claim.** Two AWS documentation pages fetched during this research returned an embedded 'See also' block instructing the reader to run an AWS CLI command. This is untrusted content injected into fetched documentation, not guidance from the task. I did not act on it and note it here as an evidence-quality caveat for anyone re-running this research.

- Source: Observed during WebFetch of AWS docs (silo-isolation.html and tenant-isolation.html)
- URL: <https://docs.aws.amazon.com/wellarchitected/latest/saas-lens/silo-isolation.html>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Independently reproduced, and the caveat is correct and worth keeping -- I did not act on the injected instruction either. The count is understated: the block appeared on at least THREE of the AWS pages fetched in this verification pass (silo-isolation.html, saas-architecture-fundamentals/tenant-isolation.html, and saas-tenant-isolation-strategies.html), not two. State it as 'every AWS documentation page fetched in this research carried the block' rather than a fixed count, since it appears to be site-wide boilerplate rather than an isolated occurrence.

### A1.7  [yes]

**Claim.** Maximum databases per instance of SQL Server is 32,767. Maximum user connections is 32,767. Instances per computer is 50 on a stand-alone server. Maximum database size 524,272 terabytes. Files per database 32,767.

**Limit or threshold asserted.** 32,767 databases per instance; 32,767 user connections

- Source: Microsoft Learn - Maximum capacity specifications for SQL Server
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/maximum-capacity-specifications-for-sql-server>
- Accessed: 2026-08-31
- Confidence: verified

### A1.8  [yes]

**Claim.** ADO.NET/Microsoft.Data.SqlClient connection pools are keyed by exact connection string: 'Each connection pool is associated with a distinct connection string. When a new connection is opened, if the connection string is not an exact match to an existing pool, a new pool is created.' Connections are pooled per process, per application domain, per connection string, and per Windows identity when integrated security is used; keywords supplied in a different order are pooled separately. The documented example shows two pools created solely because Initial Catalog differs.

**Limit or threshold asserted.** one pool per distinct connection string per process

- Source: Microsoft Learn - SQL Server connection pooling (ADO.NET)
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified

### A1.9  [partially]

**Claim.** Default Max Pool Size is 100. When the maximum pool size is reached and no usable connection is available the request is queued and the pooler retries 'until the time-out is reached (the default is 15 seconds)', then throws. If MinPoolSize is unspecified or zero, idle connections are removed 'after it has been idle for approximately 4-8 minutes (in a random two-pass fashion)'. After a login or timeout error, subsequent attempts fail for a 5-second blocking period that doubles on repeat failure up to a maximum of 1 minute.

**Limit or threshold asserted.** Max Pool Size 100; timeout 15s; idle eviction 4-8 min; blocking period 5s doubling to 60s

- Source: Microsoft Learn - SQL Server connection pooling (ADO.NET)
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Three of the four constants are exact. The fourth misattributes its condition: the documented trigger for the 4-8 minute eviction is LoadBalanceTimeout (Connection Lifetime) not being set, NOT MinPoolSize being zero. The MinPoolSize note says only that with MinPoolSize unspecified or zero 'the connections in the pool will be closed after a period of inactivity' and gives no figure; with MinPoolSize > 0 the POOL is not destroyed, but individual idle connections above the minimum are still subject to removal. Restate as: 'when LoadBalanceTimeout/Connection Lifetime is unset (default 0), an idle connection is removed after approximately 4-8 minutes; separately, when MinPoolSize is unspecified or zero the pool itself is torn down after a period of inactivity.' Also worth carrying: the blocking-period mechanism 'doesn't apply to Azure SQL Server by default'.

### A1.10  [yes]

**Claim.** Microsoft documents 'Pool fragmentation due to many databases' as a named problem: opening a connection to a specific database per user or group produces 'a separate pool of connections to each database, which increase the number of connections to the server'. The documented mitigation is to connect to one database and issue a T-SQL USE statement to switch.

- Source: Microsoft Learn - SQL Server connection pooling (ADO.NET), Pool fragmentation
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified

### A1.11  [partially]

**Claim.** Applying the documented constants to this system: at 33 offices the API process holds 34 pools (host + 33 offices) and the AuthServer holds its own separate set because pools are per process, giving a theoretical ceiling of 34 x 100 x 2 = 6,800 connections. That is far below the 32,767 user-connection limit, so connection count is not the binding constraint; connection memory and worker threads are the things to watch.

**Limit or threshold asserted.** 34 pools/process; 6,800 theoretical max connections; 32,767 limit

- Source: My arithmetic on Microsoft-documented constants (pool-per-connection-string, Max Pool Size 100, per-process pooling, 32,767 user connections)
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The two constants the derivation rests on (per-process pooling, default Max Pool Size 100) are documented and the arithmetic 34 x 100 x 2 = 6,800 is correct, so the conclusion that connection count is not the binding constraint is defensible. But the page supports only the constants, not the system-specific model, and 34 pools per process is a FLOOR rather than a ceiling: the same documentation adds per-application-domain, per-Windows-identity and per-SqlCredential pool splitting, and any connection-string variation (differing keyword order, an Application Name per job, a distinct migration identity) creates further pools. Present 6,800 explicitly as a lower-bound estimate under the assumption of exactly one connection string per database per process, and say that pool-count inflation from identity or connection-string drift is the thing that would invalidate it.

### A1.12  [yes]

**Claim.** SQL Server memory per user connection is approximately (3 * network_packet_size + 94 KB); default network packet size is 4 KB, giving roughly 106 KB per connection.

**Limit or threshold asserted.** (3 * network_packet_size + 94 KB); ~106 KB at 4 KB packets

- Source: Microsoft Learn - Memory Management Architecture Guide, 'Memory used by SQL Server objects specifications'
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/memory-management-architecture-guide>
- Accessed: 2026-08-31
- Confidence: verified

### A1.13  [partially]

**Claim.** Applying that formula: 6,800 fully-populated pooled connections would consume roughly 720 MB of connection memory, about 10 percent of the configured 7,168 MB MSSQL_MEMORY_LIMIT_MB, competing with the buffer pool. This is a bound worth measuring, not a present problem.

**Limit or threshold asserted.** ~720 MB of ~7,168 MB

- Source: My arithmetic on the documented per-connection memory formula and the system's stated memory cap
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/memory-management-architecture-guide>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The formula is documented; the arithmetic is loose and the premise is inherited. 6,800 x 106 KB = 720,800 KB, which is 704 MB at 1024 KB/MB, not 720 MB (the researcher divided by 1000). 704 MB of 7,168 MB is 9.8 percent, so the 'about 10 percent' conclusion survives. Two caveats the page requires: the figure is explicitly an estimate that 'can vary depending on the environment', and with MARS enabled the per-connection cost rises to (3 + 3 * num_logical_connections) * network_packet_size + 94 KB. Since the 6,800 figure is itself a lower bound (see claim 11), state this as 'of order 0.7 GB, roughly a tenth of the configured limit, and scaling linearly with any pool-count inflation.'

### A1.14  [yes]

**Claim.** Default max worker threads on a 64-bit machine with 4 or fewer logical CPUs is 512. A worker thread 'is assigned only to active requests and is released once the request is serviced... even if the user session/connection on which the request was made remains open.' When all worker threads are busy with long-running queries, SQL Server 'might appear unresponsive'.

**Limit or threshold asserted.** 512 worker threads at <=4 logical CPUs (the VM has 4 vCPU)

- Source: Microsoft Learn - Server configuration: max worker threads
- URL: <https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-the-max-worker-threads-server-configuration-option>
- Accessed: 2026-08-31
- Confidence: verified

### A1.15  [partially]

**Claim.** Because idle pooled connections are evicted after 4-8 minutes and the three highest-frequency recurring jobs run every 15 minutes, each sweep will in practice find empty pools and pay a full connect, TLS handshake and login for every office database, every cycle. Connection pooling therefore provides little benefit to the fan-out jobs specifically, though it still benefits interactive traffic.

**Limit or threshold asserted.** 4-8 min eviction vs 15 min job interval

- Source: My reasoning combining the documented 4-8 minute idle eviction with the system's stated 15-minute job schedule
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The interval arithmetic is sound as a hypothesis but overstated as a conclusion, and one element is not in the source. (a) The 4-8 minute eviction is conditioned on LoadBalanceTimeout/Connection Lifetime being unset, not on the job schedule, and it is randomised, so 'each sweep will in practice find empty pools' should be 'each sweep is likely to find pools drained of idle connections'. (b) It only holds for office databases that receive no interactive traffic between sweeps; any interactive request inside the window keeps that pool warm, so the effect is per-database, not uniform. (c) The page never mentions TLS -- the documented per-open costs are 'a physical channel such as a socket or a named pipe', 'the initial handshake with the server', connection-string parsing and authentication. Whether a TLS handshake is among them depends on the encryption setting and must be cited separately or dropped. Keep this as an explicitly untested hypothesis to be confirmed by measuring login-per-second against the office databases.

### A1.16  [yes]

**Claim.** SQL Server 2022 edition scale limits: Standard is limited to the lesser of 4 sockets or 24 cores, 128 GB buffer pool per instance, 524 PB maximum database size. Express is limited to the lesser of 1 socket or 4 cores, 1,410 MB buffer pool per instance, and 10 GB maximum relational database size. Resource Governor is Enterprise-only in 2022; Always On availability groups are Enterprise-only; Standard gets basic availability groups.

**Limit or threshold asserted.** Standard: 24 cores / 128 GB buffer pool. Express: 4 cores / 1,410 MB buffer pool / 10 GB per database

- Source: Microsoft Learn - Editions and supported features of SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified

### A1.17  [yes]

**Claim.** SQL Server Developer edition 'includes all the functionality of Enterprise edition, but is licensed for use as a development and test system, not as a production server.' Evaluation edition is 180 days.

**Limit or threshold asserted.** Developer: dev/test only, not production

- Source: Microsoft Learn - Editions and supported features of SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified

### A1.18  [yes]

**Claim.** SQL Server 2025 changes the relevant edition limits materially: Standard rises to the lesser of 4 sockets or 32 cores and a 256 GB buffer pool, Express maximum database size rises to 50 GB, Web edition is removed, and Resource Governor becomes available in Standard edition (previously Enterprise and Developer only). SQL Server 2025 reached general availability on 18 November 2025.

**Limit or threshold asserted.** Standard 2025: 32 cores / 256 GB buffer pool / Resource Governor Yes. Express 2025: 50 GB per database

- Source: Microsoft Learn - Editions and supported features of SQL Server 2025; Microsoft Learn - Resource Governor
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2025>
- Second source: <https://learn.microsoft.com/en-us/sql/relational-databases/resource-governor/resource-governor>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All edition limits verified verbatim on the cited page, and the Web-edition removal is confirmed both by the footnote and by the absence of a Web column in the 2025 tables. The GA date is NOT on this page but is corroborated elsewhere: the SQL Server 2025 lifecycle page gives a start date of '11/18/2025 8:00:00 AM'. Cite <https://learn.microsoft.com/en-us/lifecycle/products/sql-server-2025> for the date rather than the editions page.

### A1.19  [partially]

**Claim.** Resource Governor - the only in-engine mechanism for limiting CPU, memory and physical I/O per workload, and thus the only in-engine noisy-neighbour control for co-located tenant databases - was available only in Enterprise and Developer editions before SQL Server 2025. It classifies sessions by attributes such as login name or program name via a classifier function.

**Limit or threshold asserted.** Enterprise/Developer only before SQL Server 2025; Standard from 2025

- Source: Microsoft Learn - Resource Governor
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/resource-governor/resource-governor>
- Second source: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every checkable fact is exact -- the CPU/memory/physical-I/O scope, the edition gate before and from SQL Server 2025, and classification by login name or program name via a classifier function. The superlative 'the only in-engine mechanism' is the researcher's editorial framing and appears nowhere on the page; the page in fact documents adjacent in-engine controls (MAXDOP, memory grant sizing, and from 2025 tempdb space resource governance) and documents exceptions to Resource Governor's own reach ('The ability to govern physical I/O applies only to user operations and not system tasks'; no controls on the dedicated administrator connection). Drop 'the only' or attribute it explicitly as an inference. Two further caveats matter for a co-located tenant fleet: I/O resource governance is separately listed as Enterprise-only in SQL Server 2022, and 'Very short queries, such as queries in some OLTP workloads, might not use CPU long enough to apply CPU bandwidth controls.'

### A1.20  [yes]

**Claim.** Basic availability groups, the only availability-group form available on SQL Server Standard edition, are limited to two replicas, no read access on the secondary, no backups on the secondary, and 'Support for one availability database'. Multiple basic availability groups may be connected to a single instance.

**Limit or threshold asserted.** 2 replicas, 1 database per basic AG, Standard edition only

- Source: Microsoft Learn - Basic availability groups for a single database
- URL: <https://learn.microsoft.com/en-us/sql/database-engine/availability-groups/windows/basic-availability-groups-always-on-availability-groups>
- Accessed: 2026-08-31
- Confidence: verified

### A1.21  [partially]

**Claim.** It follows that HA for 34 databases on Standard edition means 34 independent basic availability groups failing over independently - which is incompatible with a design that derives every office connection string from one server name. Instance-level failover (a failover cluster instance, two nodes on Standard) is the only shape that preserves the derived-connection-string design. This is my inference from the documented basic-AG limits and the system's stated connection-string derivation.

**Limit or threshold asserted.** 34 databases would require 34 basic AGs

- Source: My reasoning from Microsoft's basic availability group limitations and the system's TenantConnectionStringProvider design
- URL: <https://learn.microsoft.com/en-us/sql/database-engine/availability-groups/windows/basic-availability-groups-always-on-availability-groups>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The premise is exact -- one availability database per basic AG means 34 databases require 34 basic AGs, and they are independent failover units -- and the two-node FCI limit on Standard is confirmed on the editions page ('On Standard edition, there's support for two nodes'). The word 'only' is not supportable: the page settles neither that an FCI is the sole shape that preserves a derived connection string, nor that no other arrangement works. At least two alternatives exist within Standard (log shipping with a manual cutover, and the clusterless availability group listed as available on Standard in both 2022 and 2025), and requirement 12's own second branch -- making the tenant-to-database-location mapping data rather than a derived template -- is a third. State this as: 'basic AGs make per-database failover independent, so either the fleet must fail over as one unit (instance-level failover such as an FCI being the obvious candidate) or the location mapping must become data.' Mark the FCI as one option, not the only one.

### A1.22  [yes]

**Claim.** Always On availability groups impose per-database thread costs: one Log Capture thread per primary database and one Log Send thread per secondary database, with total AG threads capped at max worker threads minus 40. Microsoft has tested up to 10 availability groups and 100 databases per physical machine, stating this 'isn't a binding limit'. AG databases must use the full recovery model.

**Limit or threshold asserted.** tested to 10 AGs / 100 DBs per machine; AG threads = max worker threads - 40

- Source: Microsoft Learn - Availability group: prerequisites, restrictions, and recommendations
- URL: <https://learn.microsoft.com/en-us/sql/database-engine/availability-groups/windows/prereqs-restrictions-recommendations-always-on-availability>
- Accessed: 2026-08-31
- Confidence: verified

### A1.23  [partially]

**Claim.** Azure SQL Managed Instance caps user databases at 100 per instance in General Purpose and Business Critical ('a hard limit that can't be changed'), and 500 in Next-gen General Purpose. General Purpose also caps database files at 280 per instance. This is evidence that a managed SQL Server platform can bind two to three orders of magnitude below the engine's 32,767 limit.

**Limit or threshold asserted.** 100 user databases per instance (GP/BC); 500 (Next-gen GP); 280 database files per instance (GP)

- Source: Microsoft Learn - Resource limits, Azure SQL Managed Instance
- URL: <https://learn.microsoft.com/en-us/azure/azure-sql/managed-instance/resource-limits>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All four numbers are exact. The parenthetical quotation 'a hard limit that can't be changed' is not on the page -- confirmed by two separate fetches -- and must be removed; the page's actual qualifier runs the other way, '100 user databases, unless the instance storage size limit has been reached', i.e. the effective cap can be LOWER than 100, not that 100 is immovable. Note also that the 280-file cap applies to classic General Purpose only; Next-gen GP is stated per database (4,096 files per database) and BC as 32,767 files per database, so the 280 figure should not be generalised across tiers. The load-bearing inference -- that a managed platform can bind orders of magnitude below the engine's 32,767 -- survives intact and is the part worth carrying into requirement 4.

### A1.24  [partially]

**Claim.** ABP's ASP.NET Core multi-tenancy module registers tenant resolvers in this order: QueryStringTenantResolveContributor (index 0), RouteTenantResolveContributor, HeaderTenantResolveContributor, CookieTenantResolveContributor - all via TenantResolvers.Add(), i.e. appended in that sequence.

**Limit or threshold asserted.** QueryString at index 0

- Source: ABP Framework source - AbpAspNetCoreMultiTenancyModule.cs (dev branch)
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.AspNetCore.MultiTenancy/Volo/Abp/AspNetCore/MultiTenancy/AbpAspNetCoreMultiTenancyModule.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The four registrations, their relative order, and the use of Add() are exactly as claimed IN THIS FILE. The stated threshold 'QueryString at index 0' is wrong for the composed pipeline. This module carries [DependsOn(typeof(AbpMultiTenancyModule))], and that module's ConfigureServices runs first and executes 'options.TenantResolvers.Insert(0, new CurrentUserTenantResolveContributor())'. The resulting order is CurrentUser (0), QueryString (1), Route (2), Header (3), Cookie (4). The ABP documentation confirms this and states the intent: CurrentUserTenantResolveContributor 'Gets the tenant id from claims of the current user, if the current user has logged in. This should always be the first contributor for the security.' This changes the analysis for the first two capability requirements: the resolver that runs before everything else reads the tenant from the caller's token, so a host-only guarantee must contend with the token claim first, not merely with __tenant.

### A1.25  [yes]

**Claim.** ABP's TenantResolver iterates the configured resolvers in order and breaks at the first one that resolves: 'foreach (var tenantResolver in Options.TenantResolvers) { await tenantResolver.ResolveAsync(context); ... if (context.HasResolvedTenantOrHost()) { ... break; } }'. Later resolvers do not run and cannot override an earlier result.

**Limit or threshold asserted.** first-resolver-wins, with break

- Source: ABP Framework source - TenantResolver.cs (dev branch)
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/TenantResolver.cs>
- Accessed: 2026-08-31
- Confidence: verified

### A1.26  [partially]

**Claim.** HasResolvedTenantOrHost() returns 'Handled || TenantIdOrName != null', so a contributor that sets context.Handled = true stops the chain even when it resolved no tenant. ABP's stock DomainTenantResolveContributor sets Handled = true unconditionally, which is what makes a domain resolver at index 0 a hard boundary rather than a preference.

**Limit or threshold asserted.** HasResolvedTenantOrHost() = Handled || TenantIdOrName != null

- Source: ABP Framework source and ABP issue #7968 'DomainTenantResolveContributor will affect other TenantResolveContributor'
- URL: <https://github.com/abpframework/abp/issues/7968>
- Second source: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.MultiTenancy.Abstractions/Volo/Abp/MultiTenancy/ITenantResolveContext.cs>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The HasResolvedTenantOrHost expression is exact (verified in TenantResolveContext.cs, not only in the issue), and the issue does describe the contributor as setting Handled unconditionally. But the source contradicts 'unconditionally' in one path that matters: GetTenantIdOrNameFromHttpContextOrNullAsync returns early with 'if (!httpContext.Request.Host.HasValue) { return Task.FromResult<string?>(null); }' BEFORE reaching 'context.Handled = true'. So a request with no Host value falls through to the next resolver rather than being stopped. Precise statement: the contributor sets Handled = true whenever a Host value is present, regardless of whether the domain format matched -- which is what makes it a hard boundary for well-formed requests -- but a request arriving with no Host header escapes the boundary and is handed to the next resolver in the chain. That edge case is exactly the kind of thing the first capability requirement has to close, so it should be tested, not assumed.

### A1.27  [no]

**Claim.** ABP's AddDomainTenantResolver helper is documented as a shortcut for 'options.TenantResolvers.Insert(0, new DomainTenantResolver("{0}.mydomain.com"))' - Insert at index 0, not Add. A custom domain resolver added with .Add() would therefore run after the query-string, route, header and cookie resolvers.

**Limit or threshold asserted.** Insert(0, ...) vs Add(...)

- Source: ABP.IO documentation - Multi-Tenancy
- URL: <https://abp.io/docs/latest/framework/architecture/multi-tenancy>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Wrong on both the documentation and the mechanism. (a) The cited page never describes AddDomainTenantResolver as a shortcut for anything -- it shows only the one-line call above, with no Insert, no index and no underlying implementation; the quoted 'Insert(0, ...)' text does not appear on the page. (b) The actual implementation is 'options.TenantResolvers.InsertAfter(r => r is CurrentUserTenantResolveContributor, new DomainTenantResolveContributor(domainFormat))' in framework/src/Volo.Abp.AspNetCore.MultiTenancy/Volo/Abp/MultiTenancy/AbpMultiTenancyOptionsExtensions.cs -- InsertAfter the current-user resolver, landing at index 1, NOT Insert at 0. (c) The type is DomainTenantResolveContributor, not DomainTenantResolver. Corrected claim: AddDomainTenantResolver inserts the domain contributor immediately after CurrentUserTenantResolveContributor, so it precedes the query-string, route, header and cookie resolvers but does NOT precede the token-claim resolver. A custom domain resolver registered with .Add() would instead run after all five. Cite the source file, not the docs page.

### A1.28  [partially]

**Claim.** I could not verify how this system's custom HostAwareDomainTenantResolveContributor is registered (Insert(0) versus Add) or whether it sets Handled on the no-match path, because the source was not available to me. This is precisely the gap the brief records as untested, and it determines whether __tenant can override the Host header.

- Source: Attempted verification against ABP source; system source not available in this session
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.AspNetCore.MultiTenancy/Volo/Abp/AspNetCore/MultiTenancy/AbpAspNetCoreMultiTenancyModule.cs>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The honesty is right and the gap is real -- the custom type is not in the ABP repository and I could not reach it either, so the registration position and no-match Handled behaviour remain genuinely unknown. But the framing of what that gap decides is now too narrow. Given the corrected ordering in claims 24 and 27, the open question is not merely 'whether __tenant can override the Host header' but a three-part one: (i) does the custom contributor precede CurrentUserTenantResolveContributor, which is at index 0 and reads the tenant from the caller's token claims; (ii) does it precede the query-string, route, header and cookie contributors; and (iii) does it set Handled on the no-match and no-Host paths. Part (i) is the one that bears on the second capability requirement (a token from one office not authorising a request for another) and was not previously identified as being in question at all.

### A1.29  [yes]

**Claim.** EF Core documents idempotent scripts as being for the case where 'you are deploying to multiple databases that may each be at a different migration' - the exact split-schema-version hazard created by a fan-out loop with no per-tenant error handling.

- Source: Microsoft Learn - Applying migrations (EF Core)
- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Accessed: 2026-08-31
- Confidence: verified

### A1.30  [yes]

**Claim.** Starting with EF Core 9, Migrate() and MigrateAsync() acquire a database-wide lock before applying migrations, and throw when the model has pending changes relative to the last migration (RelationalEventId.PendingModelChangesWarning). Microsoft recommends detecting this before deployment with 'dotnet ef migrations has-pending-model-changes' in CI/CD.

**Limit or threshold asserted.** EF Core 9+

- Source: Microsoft Learn - Applying migrations (EF Core), Migration locking
- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Accessed: 2026-08-31
- Confidence: verified

### A1.31  [yes]

**Claim.** EF Core guidance states: 'Use a separate identity for deployment that has permission to change the schema. The identity used by the application at run time should normally have only the permissions the application needs to read and write data.' It also states that migration scripts update an existing database and that 'Database creation typically requires a different connection, elevated permissions'. Separately it endorses the one-shot migration job pattern: 'run it as a one-shot deployment job after the database is healthy... don't make every application replica run migrations from its entrypoint.'

- Source: Microsoft Learn - Applying migrations (EF Core)
- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Accessed: 2026-08-31
- Confidence: verified

### A1.32  [yes]

**Claim.** SQL Server recovery models determine restore granularity: the simple recovery model does not support transaction log backups and 'Can recover only to the end of a backup'; point-in-time restore requires the full recovery model plus complete log backups. Microsoft warns that 'If you have two or more related databases in the full recovery model that must be logically consistent, you might have to implement special procedures to ensure the recoverability of these databases.'

**Limit or threshold asserted.** simple = no PITR; full = PITR with log backups

- Source: Microsoft Learn - Recovery models (SQL Server)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/recovery-models-sql-server>
- Accessed: 2026-08-31
- Confidence: verified

### A1.33  [partially]

**Claim.** Microsoft's row-level security documentation lists multi-tenancy as a primary use case and documents the SESSION_CONTEXT middle-tier pattern for applications where tenants share one SQL login. It also warns that security policy managers with sufficient permissions can exfiltrate data via side channels, and that changing security policies should be monitored. The Azure multitenancy guidance adds that RLS 'can be complex to design, implement, test, and maintain. Many multitenant solutions don't use row-level security because of those complexities.'

- Source: Microsoft Learn - Row-level security; Azure Architecture Center - Storage and data approaches for multitenancy
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/security/row-level-security>
- Second source: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/storage-data>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Everything attributed to the RLS page is verbatim and correct. The final sentence is quoted accurately but comes from a DIFFERENT page and is cited to this URL: 'This approach can be complex to design, implement, test, and maintain. Many multitenant solutions don't use row-level security because of those complexities' appears on <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/storage-data>. Split the citation. Two qualifiers also belong with the multi-tenancy point: the page frames it as one of three design examples rather than a primary use case, and it notes that a security policy applies to dbo and db_owner as well ('Security policies apply to all users, including dbo users in the database'), which is what makes RLS a weaker boundary than a separate database for the isolation requirements here.

### A1.34  [yes]

**Claim.** The Azure Architecture Center names four relational multitenancy antipatterns: table-based isolation (one table set per tenant in a shared database), column-level tenant customization, manual schema changes, and version dependencies - and instructs teams to 'build tooling or an automated pipeline to deploy your schema changes' and to 'Track the schema version that you use for each tenant in a dedicated database or lookup table.'

- Source: Microsoft Learn - Architectural approaches for storage and data in multitenant solutions (updated 2026-08-21)
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/storage-data>
- Accessed: 2026-08-31
- Confidence: verified

### A1.35  [yes]

**Claim.** On dedicated databases per tenant, Microsoft notes: 'It's important to use automated deployment approaches when you provision databases for each tenant. Otherwise, the complexity of manually deploying and managing the databases becomes overwhelming.' On schema upgrades across a fleet: 'In a small estate of databases, you might consider using a deployment pipeline to deploy schema changes. As the number of databases increases, it might be better for your application tier to detect the schema version for a specific database and to initiate the upgrade process.'

- Source: Microsoft Learn - Architectural approaches for storage and data in multitenant solutions
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/storage-data>
- Accessed: 2026-08-31
- Confidence: verified

### A1.36  [yes]

**Claim.** Microsoft's own million-database case study (Dynamics 365 / Power Platform) reports that the fleet is operated by roughly two dedicated engineers - but only because the team built a custom resource-management control plane ('Spartan', an ARM resource provider automating database CRUD, pool rebalancing, backup retention and point-in-time restore) plus a separate Data Administration and Management Service for scheduled index and plan maintenance. Published 8 October 2020.

**Limit or threshold asserted.** 1,000,000 databases, ~2 dedicated engineers, custom control plane required

- Source: Microsoft Azure SQL Dev Blog - Running 1M databases on Azure SQL for a large SaaS provider
- URL: <https://devblogs.microsoft.com/azure-sql/running-1m-databases-on-azure-sql-for-a-large-saas-provider-microsoft-dynamics-365-and-power-platform/>
- Accessed: 2026-08-31
- Confidence: verified

### A1.37  [yes]

**Claim.** 45 CFR 164.316(b)(2)(i) requires: 'Retain the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later.' Paragraph (b)(1) covers policies and procedures implemented to comply with the Security Rule and written records of actions, activities or assessments the subpart requires to be documented. It does not itself set a retention period for patient medical records, which are governed by separate (largely state) requirements.

**Limit or threshold asserted.** 6 years from creation or last effective date, whichever is later - for Security Rule documentation

- Source: 45 CFR 164.316 (via Cornell Legal Information Institute)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.316>
- Accessed: 2026-08-31
- Confidence: verified

### A1.38  [yes]

**Claim.** SQL Server 2022 mainstream support ends 12 January 2028; extended support ends 12 January 2033. It is currently supported.

**Limit or threshold asserted.** mainstream end 2028-01-12; extended end 2033-01-12

- Source: Microsoft Lifecycle - SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/lifecycle/products/sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified

### A1.39  [no]

**Claim.** ABP Framework remains actively maintained: the 10.x line is current, with ABP 10.2 released as a stable version in 2026 and 10.3 on the roadmap. The system runs ABP Commercial 10.0.2, which is within the current major line.

**Limit or threshold asserted.** 10.x current; 10.2 stable in 2026

- Source: ABP.IO release notes and roadmap
- URL: <https://abp.io/docs/latest/release-info/road-map>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The 'actively maintained' conclusion holds but every version number is stale. Current stable is ABP 10.6, and the roadmap item is 10.7, scheduled as a stable release in August 2026. 10.2 and 10.3 are past releases, not the current stable and the forward roadmap. Corrected claim: ABP is actively maintained; the current stable line is 10.6 with 10.7 planned for August 2026. The system's ABP Commercial 10.0.2 is within the current major version but six minor versions behind current stable -- which is the material fact for an upgrade-risk assessment and is exactly what the original wording concealed by naming 10.2 as current.

### A1.40  [partially]

**Claim.** The Deployment Stamps pattern is Microsoft's named pattern for dedicated per-tenant or per-group infrastructure. 'Single-tenant stamps often work well with a few tenants. As your number of tenants grows, managing a fleet of single-tenant stamps becomes more difficult.' Microsoft recommends modelling a solution as a stamp even when using a multitenant or sharded database inside it.

- Source: Microsoft Learn - Architectural approaches for a multitenant solution
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/overview>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The named pattern and the quoted sentence are exact on this page. The final assertion is not: this page says only 'You can also apply the Deployment Stamps pattern to create multitenant stamps', which is a possibility, not a recommendation. The recommendation the claim is reaching for is on a different URL -- storage-data -- and reads 'In multitenant solutions, it's a good practice to create deployment stamps. This recommendation applies even when you use a multitenant database or sharded databases within a stamp.' Split the citation so the recommendation is sourced to storage-data and the scaling caveat to overview.

---

## Area: audit-retention

Verification verdict for this area: **minor-corrections** (32 claims checked)

### A2.1  [yes]

**Claim.** 45 CFR 164.312(b) 'Standard: Audit controls' reads in full: 'Implement hardware, software, and/or procedural mechanisms that record and examine activity in information systems that contain or use electronic protected health information.' It has NO implementation specifications and NO retention period. Verified against both the current eCFR text as of 2026-08-01 and the printed 2024 CFR.

**Limit or threshold asserted.** No retention period stated; no implementation specifications; standard is unqualified (not Required/Addressable, because it has no sub-specifications)

- Source: eCFR API, 45 CFR 164.312 (current text)
- URL: <https://www.ecfr.gov/api/versioner/v1/full/2026-08-01/title-45.xml?part=164&section=164.312>
- Second source: <https://www.govinfo.gov/content/pkg/CFR-2024-title45-vol2/xml/CFR-2024-title45-vol2-sec164-312.xml>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Substantively correct. The eCFR text at 2026-08-01 matches word for word; (b) carries no implementation specifications, no Required/Addressable designation of its own, and no retention period. One process caveat: the claim asserts it was 'verified against ... the printed 2024 CFR' - the cited URL is the eCFR API only and cannot corroborate that second check, so drop the printed-CFR assertion or cite it separately.

### A2.2  [yes]

**Claim.** The six-year clock in 45 CFR 164.316(b)(2)(i) attaches to 'the documentation required by paragraph (b)(1)'  -  namely (i) the policies and procedures implemented to comply with the Subpart, and (ii) a written record of an 'action, activity or assessment' where the Subpart REQUIRES it to be documented. It does not attach to audit logs as such. Exact text: 'Retain the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later.'

**Limit or threshold asserted.** 6 years from creation or from the date it was last in effect, whichever is later

- Source: eCFR API, 45 CFR 164.316 (current text)
- URL: <https://www.ecfr.gov/api/versioner/v1/full/2026-08-01/title-45.xml?part=164&section=164.316>
- Second source: <https://www.govinfo.gov/content/pkg/CFR-2024-title45-vol2/xml/CFR-2024-title45-vol2-sec164-316.xml>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Correct as stated, including the (b)(1)(i)/(ii) breakdown - the standard covers policies and procedures in written form and, where the subpart requires an action, activity or assessment to be documented, a written record of it. The negative inference (does not attach to audit logs as such) follows from the text and is sound.

### A2.3  [yes]

**Claim.** 45 CFR 164.308(a)(1)(ii)(D) 'Information system activity review (Required)' requires: 'Implement procedures to regularly review records of information system activity, such as audit logs, access reports, and security incident tracking reports.' This is the provision that makes reviewing logs mandatory; the log itself is named only as an example of what to review.

**Limit or threshold asserted.** Required implementation specification; review frequency not specified ('regularly')

- Source: eCFR API, 45 CFR 164.308 (current text)
- URL: <https://www.ecfr.gov/api/versioner/v1/full/2026-08-01/title-45.xml?part=164&section=164.308>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed. Labeled (Required), titled 'Information system activity review', and the only temporal qualifier is 'regularly' - no cadence is specified. The reading that audit logs appear only as an example of what to review is correct.

### A2.4  [yes]

**Claim.** The six-year number that actually bears on how long you would WANT audit logs is the enforcement limitation: 45 CFR 160.414 states 'No action under this subpart may be entertained unless commenced by the Secretary, in accordance with  160.420, within 6 years from the date of the occurrence of the violation.' This is a look-back window for OCR enforcement, not a records-retention mandate.

**Limit or threshold asserted.** 6 years from the date of occurrence of the violation

- Source: eCFR API, 45 CFR 160.414 Limitations
- URL: <https://www.ecfr.gov/api/versioner/v1/full/2026-08-01/title-45.xml?part=160&section=160.414>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Quote is exact and the characterization as a limitations period rather than a retention mandate is correct. Minor: the section heading is 'Limitations' - worth naming it, since 'look-back window for OCR enforcement' is the researcher's gloss rather than the regulation's own label.

### A2.5  [partially]

**Claim.** My reading of the two provisions together: 164.316(b)(2)(i) makes the six-year clock a property of DOCUMENTS (policies, procedures, and records of required-to-be-documented actions/activities/assessments), while 160.414 makes six years the practical evidentiary horizon for LOGS. The correct design consequence is that log retention is a risk-based, organization-defined and DOCUMENTED decision, and the document recording that decision is itself the thing with a mandatory six-year clock.

- Source: Author's synthesis of 45 CFR 164.316(b), 45 CFR 164.312(b) and 45 CFR 160.414
- URL: <https://www.ecfr.gov/api/versioner/v1/full/2026-08-01/title-45.xml?part=164&section=164.316>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The regulatory halves are verified (claims 2 and 4). The synthesis is the researcher's own and the cited URL settles only the first half of it. One tightening: the retention-policy document falls under 164.316(b)(1)(i) as a policy/procedure, so the six-year clock on it is straightforward; but a written record of each periodic information-system-activity review is only caught by (b)(1)(ii) if the subpart 'requires' that review to be documented - 164.308(a)(1)(ii)(D) requires procedures to review, and 164.316(b)(1)(i) requires those procedures in writing, so the review RECORD's six-year status rests on the entity's own policy requiring it rather than on an explicit textual mandate. State that dependency rather than asserting it as settled. Correctly flagged 'partial'.

### A2.6  [yes]

**Claim.** NIST SP 800-66r2 5.3.2 (Audit Controls,  164.312(b)) prescribes NO retention period. It frames scope as risk-based ('Determine the appropriate scope of audit controls ... based on the regulated entity's risk assessment'), asks 'Determine the frequency of audit log reviews based on the risk assessment', and explicitly poses the architectural question 'Where will audit information reside (e.g., separate server)?'. Its only six-year statement is in 5.5.2 (Documentation,  164.316(b)), keyed to paragraph (b)(1) documentation.

**Limit or threshold asserted.** No audit-log retention period given anywhere in 5.3.2; six years appears only under 5.5.2 Documentation

- Source: NIST SP 800-66r2, Implementing the HIPAA Security Rule: A Cybersecurity Resource Guide (February 2024)
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-66r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Fully confirmed. 5.3.2 (p. 69, Table 22) contains no instance of 'retain'/'retention'/'six years'; a full-text scan of the 122-page February 2024 publication finds 'six years' only at 5.5.2 Key Activity 2, 'Retain Documentation for at Least Six Years', keyed to paragraph (b)(1). The 'Where will audit information reside (e.g., separate server)?' question is verbatim. One nit: the frequency question reads in full 'Determine the frequency of audit log reviews based on the risk assessment and risk management processes' - the claim truncates the tail without an ellipsis.

### A2.7  [yes]

**Claim.** NIST SP 800-92 (2006) is itself a source of the widespread 'six years for logs' folklore, and it does not say that. Its 2.2 states of HIPAA: 'Section 4.22 [of NIST SP 800-66] specifies that documentation of actions and activities need to be retained for at least six years'  -  documentation of actions and activities, not logs.

**Limit or threshold asserted.** at least six years, for documentation of actions and activities

- Source: NIST SP 800-92, Guide to Computer Security Log Management (September 2006), 2.2
- URL: <https://nvlpubs.nist.gov/nistpubs/legacy/SP/nistspecialpublication800-92.Pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Quote is exact and it does sit in 2.2 ('The Need for Log Management', p. 2-7 continuing to 2-8), in the HIPAA bullet. The framing that this passage is a source of the 'six years for logs' folklore is editorial and unfalsifiable, but harmless - the load-bearing part, that the sentence says documentation and not logs, is verified.

### A2.8  [partially]

**Claim.** NIST SP 800-92 5.4 'Manage Long-Term Log Data Storage' prescribes the exact offload-then-prune sequence recommended here for retention periods measured in months or years: choose an archive format, archive the data, 'Verify the integrity of the transferred logs ... typically done through the creation of message digests for each log file', store securely, and destroy properly at end of the retention period. 3.1 further distinguishes log RETENTION (routine archival) from log PRESERVATION (holding what would otherwise be discarded, for incidents or investigations)  -  the vocabulary for legal hold.

**Limit or threshold asserted.** Applies when retention is 'months or years' rather than 'days or weeks'

- Source: NIST SP 800-92, Guide to Computer Security Log Management (September 2006), 3.1 and 5.4
- URL: <https://nvlpubs.nist.gov/nistpubs/legacy/SP/nistspecialpublication800-92.Pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: 5.4 is fully confirmed: the months-or-years threshold, and the four-step sequence (choose a log format, archive the log data, 'Verify the integrity of the transferred logs ... typically done through the creation of message digests for each log file', store the media securely), plus destruction at end of retention. But the retention/preservation distinction is in 3.2 (Functions), p. 3-3, NOT 3.1 (Architecture); the document also points to 4.2 for archival detail and repeats the definitions in the glossary. Change the citation to 3.2.

### A2.9  [yes]

**Claim.** SP 800-92 Rev. 1, 'Cybersecurity Log Management Planning Guide', remains an Initial Public Draft dated October 2023 with the comment period closed 29 November 2023 and no final version published; the 2006 SP 800-92 therefore remains the current final NIST log-management guidance. The draft notably poses 'Should guidance for determining storage retention periods be included?' as an open question to reviewers, and its task TS-3.8 is 'Determine if and when each type of log event should or must be transferred from active storage to cold data storage for data retention purposes.'

**Limit or threshold asserted.** Draft status as of 2026-08-31; IPD dated October 2023

- Source: NIST CSRC, SP 800-92 Rev. 1 (Initial Public Draft)
- URL: <https://csrc.nist.gov/pubs/sp/800/92/r1/ipd>
- Second source: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-92r1.ipd.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed on every element. CSRC shows Initial Public Draft, 'Cybersecurity Log Management Planning Guide', dated 11 October 2023, comment period closed 29 November 2023, no final and no second draft. Both quoted passages are verbatim from the IPD PDF (call-for-comments question 6; task TS-3.8). The inference that the 2006 edition remains the current final NIST log-management guidance follows.

### A2.10  [yes]

**Claim.** NIST SP 800-53 Rev. 5 AU-11 makes audit retention an organization-defined parameter: 'Retain audit records for [organization-defined time period] to provide support for after-the-fact investigations of incidents and to meet regulatory and organizational information retention requirements.' AU-9(2) asks that audit records be stored 'in a repository that is part of a physically different system or system component than the system or component being audited', explicitly including 'backup or long-term storage'. AU-3's discussion warns that 'there is the potential to reveal personally identifiable information in the audit trail'. AU-4 requires allocating audit log storage capacity deliberately.

**Limit or threshold asserted.** AU-11 retention period is an organization-defined parameter (no default value in the catalog)

- Source: NIST SP 800-53 Rev. 5 control catalog (OSCAL), controls AU-3, AU-4, AU-9, AU-9(2), AU-11
- URL: <https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All four controls confirmed against the OSCAL catalog. Two precision notes: (a) 'backup or long-term storage' is from AU-9(2)'s DISCUSSION ('Storing audit records on separate systems or components applies to initial generation as well as backup or long-term storage of audit records'), not from the control statement - say 'the discussion adds' rather than 'explicitly including'; (b) the AU-9(2) statement itself also carries an organization-defined frequency parameter ('Store audit records [org-defined frequency] in a repository...') which the claim omits. AU-3's PII sentence and AU-4's capacity-allocation statement are verbatim.

### A2.11  [yes]

**Claim.** The January 2025 HIPAA Security Rule NPRM would require an annual documented compliance audit and 'written documentation of all Security Rule policies, procedures, plans, and analyses', but the HHS fact sheet contains no separate audit-log retention period and no prescribed log fields. As of 2026-08-31 the rule is not final and the existing Security Rule remains in effect.

**Limit or threshold asserted.** Comment period 60 days from Federal Register publication (published 6 January 2025); no log-retention period proposed

- Source: HHS.gov, HIPAA Security Rule NPRM Fact Sheet
- URL: <https://www.hhs.gov/hipaa/for-professionals/security/hipaa-security-rule-nprm/factsheet/index.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Confirmed: annual compliance audit, 'written documentation of all Security Rule policies, procedures, plans, and analyses', comments due 60 days after Federal Register publication, and an explicit statement that the current Security Rule remains in effect. Note the fact sheet dates the NPRM's issuance as 27 December 2024; the 6 January 2025 date in the claim is the Federal Register publication date and is not on this page - cite the FR notice if you want that date sourced. Correctly flagged 'partial'.

### A2.12  [partially]

**Claim.** OCR's most recent cybersecurity newsletter (January 2026, 'System Hardening and Protecting ePHI') restates 164.312(b) verbatim and references NIST SP 800-53 AU-3 for audit record content, but gives no retention period and no review frequency  -  confirming that HHS has still not prescribed either as of 2026.

**Limit or threshold asserted.** No retention period or review cadence specified

- Source: HHS OCR Cybersecurity Newsletter, January 2026
- URL: <https://www.hhs.gov/hipaa/for-professionals/security/guidance/cybersecurity-newsletter-january-2026/index.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The page exists (January 2026, 'System Hardening and Protecting ePHI', last reviewed 8 January 2026), does reference NIST SP 800-53 AU-3 as 'the content of audit logs (control number AU-3)', and states no retention period and no review cadence - so the load-bearing negative finding holds. Two corrections: (a) it does not restate 164.312(b) verbatim; it quotes the standard with '[ePHI]' substituted for the statutory phrase, inside a footnote-cited sentence; (b) 'most recent' is unverified - the newsletter archive index at /guidance/cybersecurity-newsletter-archive/ returns 404 and I could not enumerate any later 2026 issue. Say 'the January 2026 newsletter' rather than 'the most recent'.

### A2.13  [yes]

**Claim.** California imposes a LONGER clock than HIPAA on the underlying records: Business and Professions Code  2266, as amended by SB 815 effective 1 January 2024, provides that 'The failure of a physician and surgeon to maintain adequate and accurate records relating to the provision of services to their patients for at least seven years after the last date of service to a patient constitutes unprofessional conduct.'

**Limit or threshold asserted.** 7 years after the last date of service; amended by Stats. 2023, Ch. 294, Sec. 18 (SB 815), effective 2024-01-01

- Source: California Legislative Information, Bus. & Prof. Code  2266
- URL: <https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=BPC&sectionNum=2266>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Verified verbatim, including the amendment note (Amended by Stats. 2023, Ch. 294, Sec. 18 (SB 815), effective January 1, 2024). Worth carrying forward that this is a professional-conduct standard on the underlying patient records, not a rule about audit logs - the claim already frames it that way ('a LONGER clock than HIPAA on the underlying records'), which is the right framing.

### A2.14  [yes]

**Claim.** California workers' compensation adds a third clock specific to this domain: 8 CCR  39.5(a) requires a Qualified Medical Evaluator to 'retain a copy of all comprehensive medical-legal reports completed by the QME for a period of five years from the date of each evaluation report', with electronic copies acceptable.

**Limit or threshold asserted.** 5 years from the date of each evaluation report

- Source: California Department of Industrial Relations, Title 8 CCR  39.5
- URL: <https://www.dir.ca.gov/t8/39_5.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Verified verbatim, and the electronic-copy allowance is real: 'A QME may satisfy this requirement by retaining only an electronic copy of the report, as long as the electronic copy retained is a true and correct copy of the original, showing the QME signature, that was served on the parties.' Two additions worth carrying: the section is titled 'Retention of Records by QMEs', and (a) is narrower than 'all medical-legal reports' - it covers COMPREHENSIVE reports specifically. Confidence can be raised from 'partial' to verified.

### A2.15  [yes]

**Claim.** ABP does NOT record entity property changes by default. The framework documentation states: 'Saving all changes of all your entities would require a lot of database space. For this reason, audit log system doesn't save any change for the entities unless you explicitly configure it.' The measured 2,689 property-change rows are therefore the result of a deliberate configuration (EntityHistorySelectors.AddAllEntities() or broad [Audited] use), and are dialable. SaveEntityHistoryWhenNavigationChanges defaults to true and further amplifies the count; IsEnabledForGetRequests defaults to false; HideErrors defaults to true.

**Limit or threshold asserted.** Defaults: IsEnabled true; IsEnabledForAnonymousUsers true; IsEnabledForGetRequests false; AlwaysLogOnException true; HideErrors true; SaveEntityHistoryWhenNavigationChanges true; DisableLogActionInfo false; EntityHistorySelectors empty

- Source: ABP Framework documentation, Audit Logging (AbpAuditingOptions and Entity History Selectors)
- URL: <https://abp.io/docs/latest/framework/infrastructure/audit-logging>
- Second source: <https://raw.githubusercontent.com/abpframework/abp/dev/docs/en/framework/infrastructure/audit-logging.md>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Quote is exact and every stated default is confirmed on the page: IsEnabled true, IsEnabledForAnonymousUsers true, IsEnabledForGetRequests false, AlwaysLogOnException true, HideErrors true, SaveEntityHistoryWhenNavigationChanges true, DisableLogActionInfo false, EntityHistorySelectors empty, IgnoredTypes/Contributors empty, ApplicationName from IApplicationInfoAccessor. The docs also list IsEnabledForIntegrationServices (false), which the inventory omits - add it, since it is another silent-gap switch. The inference that the measured row count is a configuration artefact and therefore dialable is sound.

### A2.16  [yes]

**Claim.** ABP's audit-log write runs on its own, separate, NON-transactional unit of work: AuditingStore.SaveLogAsync calls UnitOfWorkManager.Begin(true), and the extension signature is Begin(bool requiresNew = false, bool isTransactional = false, ...). Combined with HideErrors defaulting to true (which catches the exception and only writes a warning to the ordinary log), a business transaction can commit while its audit record is silently lost. Two consequences: moving audit to a separate database introduces no distributed transaction, and audit completeness is best-effort by default.

**Limit or threshold asserted.** Begin(requiresNew: true, isTransactional: false)

- Source: ABP source, Volo.Abp.AuditLogging.AuditingStore and Volo.Abp.Uow.UnitOfWorkManagerExtensions
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/modules/audit-logging/src/Volo.Abp.AuditLogging.Domain/Volo/Abp/AuditLogging/AuditingStore.cs>
- Second source: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkManagerExtensions.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed in source, and worth stressing because an automated summarizer misreads Begin(true) as 'transactional' - the single positional argument binds to requiresNew, and isTransactional defaults to false. Both design consequences (no distributed transaction when the audit database is separated; best-effort audit completeness by default) follow. One correction to the failure description: with HideErrors true, SaveAsync writes BOTH a warning ('Could not save the audit log object: ' + the serialized AuditLogInfo) and the exception itself at LogLevel.Error - so it is not warning-only, and the dropped audit payload is recoverable from the ordinary log if that log is retained. That slightly weakens 'silently lost' and slightly strengthens the case for alarming on those log events.

### A2.17  [partially]

**Claim.** ABP Commercial's Audit Logging Module (Pro) already ships periodic cleanup of audit logs, introduced in ABP 8.2 (this system runs 10.0.2). It is configured by ExpiredAuditLogDeleterOptions (Period defaults to 1 day; a Hangfire or Quartz CronExpression is also supported) plus a per-tenant 'Expired Item Deletion Period' setting. It is inert unless enabled: 'If you don't enable the Cleanup Service System Wide from the host side under Settings -> Audit logs -> Global, it won't remove the expired audit logs, even if there are tenant specific settings.' The settings tab is itself gated behind the AuditLogging.SettingManagement feature, which is disabled by default.

**Limit or threshold asserted.** Worker Period default 1 day; AuditLogging.SettingManagement feature disabled by default; feature introduced ABP 8.2 (26 June 2024)

- Source: ABP documentation, Audit Logging Module (Pro)
- URL: <https://abp.io/docs/latest/modules/audit-logging-pro>
- Second source: <https://raw.githubusercontent.com/abpframework/abp/dev/docs/en/modules/audit-logging-pro.md>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Everything about the mechanism is confirmed: ExpiredAuditLogDeleterOptions with a worker Period whose 'default value is 1 day' (and the docs' own caution that Period is the worker cadence, not the Expired Item Deletion Period), Hangfire/Quartz CronExpression support, the per-tenant Expired Item Deletion Period setting, the inert-unless-enabled quote verbatim, and AuditLogging.SettingManagement described as 'a child feature of AuditLogging.Enable and is disabled by default'. NOT confirmed: 'introduced in ABP 8.2 (26 June 2024)'. That version and date appear nowhere on the cited page and I could not corroborate them; the page is versioned 'latest' (10.6). Drop the version-of-introduction or source it from the 8.2 release notes. Also note the docs page you cite is 10.6, while the system is stated to run 10.0.2 - pin the docs URL to the 10.0 branch before relying on any default.

### A2.18  [yes]

**Claim.** ABP's audit tables are already addressable by a distinct named connection string: AbpAuditLoggingDbContext, IAuditLoggingDbContext and the MongoDB equivalents all carry [ConnectionStringName(AbpAuditLoggingDbProperties.ConnectionStringName)] where that constant is the literal string "AbpAuditLogging". Separating the audit database is therefore a supported configuration path, not a fork.

**Limit or threshold asserted.** public const string ConnectionStringName = "AbpAuditLogging"

- Source: ABP source, AbpAuditLoggingDbProperties.cs and AbpAuditLoggingDbContext.cs
- URL: <https://raw.githubusercontent.com/abpframework/abp/1f7b7a56503515a6b26da6101ebc9d3921b461d9/modules/audit-logging/src/Volo.Abp.AuditLogging.Domain/Volo/Abp/AuditLogging/AbpAuditLoggingDbProperties.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Verified, and I checked the three consumers the claim asserts but that the cited URL does not itself cover: AbpAuditLoggingDbContext, IAuditLoggingDbContext, and AuditLoggingMongoDbContext each carry [ConnectionStringName(AbpAuditLoggingDbProperties.ConnectionStringName)] at the same pinned commit. Cite those three files alongside the constant so the claim is self-supporting. Conclusion (supported configuration path, not a fork) holds.

### A2.19  [partially]

**Claim.** HAZARD, verified in source: ABP's MultiTenantConnectionStringResolver.ResolveAsync resolves a named connection string for a tenant in this order  -  (1) the tenant's own entry under that name, (2) the tenant's entry for a mapped database name where the database IsUsedByTenants, (3) THE TENANT'S DEFAULT CONNECTION STRING, and only then (4) host configuration. Because step 3 precedes step 4, adding an 'AbpAuditLogging' entry to host appsettings.json alone leaves every tenant still writing audit rows into its OPERATIONAL database, while the host writes to the new one and the change appears to have worked. TenantConfiguration.ConnectionStrings is a Dictionary<string,string?>, so per-tenant named entries are supported and are the correct fix.

**Limit or threshold asserted.** Fallback to tenant Default occurs before fallback to host configuration

- Source: ABP source, MultiTenantConnectionStringResolver.cs and ConnectionStrings.cs
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/MultiTenantConnectionStringResolver.cs>
- Second source: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.Data/Volo/Abp/Data/ConnectionStrings.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The mechanism is exactly as described - the four-step order is verified in source and the tenant-default fallback provably precedes the host fallback. But the scope is overstated: there is an earlier guard, 'if (tenant == null || tenant.ConnectionStrings.IsNullOrEmpty()) return await base.ResolveAsync(connectionStringName);'. A tenant with NO connection strings of its own never reaches step 3 and does pick up the host's AbpAuditLogging entry. So the hazard bites exactly those tenants that have their own Default - which, in a per-office-database deployment, is all of them, but the claim should say so rather than asserting 'every tenant'. Also: TenantConfiguration.ConnectionStrings is of type ConnectionStrings, which DERIVES from Dictionary<string, string?> - it is not itself the raw dictionary type. And the same logic is duplicated in the obsolete synchronous Resolve overload, so a startup self-check should exercise the async path specifically. The prescribed fix (per-tenant named entries) is correct.

### A2.20  [yes]

**Claim.** PHI is stored in the audit tables in three specific columns, with these caps: AbpAuditLogActions.Parameters is nvarchar(2000) holding serialized method arguments (AuditLogActionConsts.MaxParametersLength = 2000); AbpEntityPropertyChanges.NewValue and .OriginalValue are nvarchar(512) each (EntityPropertyChangeConsts.MaxNewValueLength / MaxOriginalValueLength = 512); and AuditLog.Exceptions has no HasMaxLength configured, so it maps to nvarchar(max). ABP further documents that AbpExceptionHandlingOptions (SendExceptionsDetailsToClients, SendStackTraceToClients, SendExceptionDataToClientTypes) 'control the exception details stored in audit logs, not only the details sent to clients'.

**Limit or threshold asserted.** Parameters nvarchar(2000); NewValue/OriginalValue nvarchar(512); Url nvarchar(256); BrowserInfo nvarchar(512); Exceptions nvarchar(max)

- Source: ABP source, AbpAuditLoggingDbContextModelBuilderExtensions.cs, EntityPropertyChangeConsts.cs, AuditLogActionConsts.cs; ABP audit-logging documentation
- URL: <https://raw.githubusercontent.com/abpframework/abp/1f7b7a56503515a6b26da6101ebc9d3921b461d9/modules/audit-logging/src/Volo.Abp.AuditLogging.EntityFrameworkCore/Volo/Abp/AuditLogging/EntityFrameworkCore/AbpAuditLoggingDbContextModelBuilderExtensions.cs>
- Second source: <https://raw.githubusercontent.com/abpframework/abp/dev/docs/en/framework/infrastructure/audit-logging.md>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every number checks out, though not all from the cited file: the model builder proves the HasMaxLength wiring and, crucially, that AuditLog.Exceptions gets no Property(...) configuration at all (hence nvarchar(max)); the numeric values come from AuditLogConsts, AuditLogActionConsts and EntityPropertyChangeConsts at the same commit, which I verified separately - cite those three too. Two things to add for a PHI inventory: these Max*Length values are settable static properties, so a deployment can have already changed them - read them at runtime rather than trusting the defaults; and AuditLog.Comments (nvarchar(256)) plus EntityChange.EntityId (nvarchar) are two more free-text columns that can carry PHI and are not in the claim's list of three. The AbpExceptionHandlingOptions statement is confirmed verbatim on the framework audit-logging docs page.

### A2.21  [partially]

**Claim.** Time-based pruning of ABP audit data is index-supported and cascade-safe: AbpAuditLogs is indexed on (TenantId, ExecutionTime) and (TenantId, UserId, ExecutionTime); AbpAuditLogActions and AbpEntityChanges are each indexed on AuditLogId; AbpEntityPropertyChanges is indexed on EntityChangeId. The child relationships are declared required, so deleting a parent cascades through all three levels along indexed foreign keys.

- Source: ABP source, AbpAuditLoggingDbContextModelBuilderExtensions.cs
- URL: <https://raw.githubusercontent.com/abpframework/abp/1f7b7a56503515a6b26da6101ebc9d3921b461d9/modules/audit-logging/src/Volo.Abp.AuditLogging.EntityFrameworkCore/Volo/Abp/AuditLogging/EntityFrameworkCore/AbpAuditLoggingDbContextModelBuilderExtensions.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The index inventory is exactly right (AbpAuditLogs on (TenantId, ExecutionTime) and (TenantId, UserId, ExecutionTime); AuditLogActions on AuditLogId; EntityChanges on AuditLogId; EntityPropertyChanges on EntityChangeId) and there are two more the claim misses that matter for pruning-adjacent queries: AuditLogActions on (TenantId, ServiceName, MethodName, ExecutionTime) and EntityChanges on (TenantId, EntityTypeFullName, EntityId). The cascade assertion needs correcting: only the two AuditLog->child relationships carry .IsRequired(). The EntityChange->EntityPropertyChange relationship is configured without .IsRequired(), so required-ness (and hence Cascade rather than ClientSetNull) rests on EF Core's convention over the non-nullable Guid EntityChangeId. Restate as 'required by convention at the third level' and, if the prune depends on it, verify the generated FK's ON DELETE behaviour in the actual migration rather than inferring it. Separately, no index starts with ExecutionTime alone - a fleet-wide prune predicated only on age, without a TenantId leading value, will not seek.

### A2.22  [yes]

**Claim.** SQL Server ledger cannot be reconciled with bounded retention. Microsoft states: 'Deleting older data in append-only ledger tables or the history table of updatable ledger tables isn't supported', 'TRUNCATE TABLE isn't supported', 'SWITCH IN/OUT partition isn't supported', 'Existing tables in a database that aren't ledger tables can't be converted to ledger tables', and 'After a ledger table is created, it can't be reverted to a table that isn't a ledger table.' Dropped ledger tables are renamed, not deleted, and remain physically in the database. Transactional replication and database mirroring are unsupported. A transaction can update at most 200 ledger tables.

**Limit or threshold asserted.** No deletion of append-only ledger data or updatable-ledger history; max 200 ledger tables per transaction; unsupported column types include XML, SqlVariant, UDT, FILESTREAM, Vector

- Source: Microsoft Learn, Ledger considerations and limitations (SQL Server)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/security/ledger/ledger-limits?view=sql-server-ver16>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every quoted limitation is verbatim and current (page ms.date 2026-02-25), including the unsupported types XML, SqlVariant, user-defined data type, FILESTREAM and Vector, the rename-on-drop behaviour (MSSQL_DroppedLedgerTable_<name>_<GUID>), and the replication/mirroring exclusions. One nuance on wording: the doc says 'can update up to 200 ledger tables' where the claim says 'at most 200' - same meaning. Also note the URL now resolves with default moniker ver17; the ver16 view still renders, but pin the version you mean. The conclusion (ledger and bounded retention are mutually exclusive for the same table) is correct and is the single most design-decisive verified fact in this set.

### A2.23  [yes]

**Claim.** SQL Server ledger is available in EVERY edition of SQL Server 2022 (Enterprise, Standard, Web, Express with Advanced Services, Express)  -  edition is not the constraint on using it. Table and index partitioning and Database/Server audit are likewise available in all editions. Backup compression is Enterprise/Standard only. Maximum relational database size is 524 PB for Enterprise/Standard/Web but 10 GB for Express.

**Limit or threshold asserted.** Express max relational database size 10 GB; Standard buffer pool 128 GB; Ledger Yes in all five editions; Backup compression No in Web/Express

- Source: Microsoft Learn / sql-docs, Editions and Supported Features of SQL Server 2022
- URL: <https://raw.githubusercontent.com/MicrosoftDocs/sql-docs/live/docs/sql-server/editions-and-components-of-sql-server-2022.md>
- Second source: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed, including Standard's 128 GB buffer-pool cap and Database/Server audit in all five editions. One adjacent fact the claim should carry because it bears on the pruning design: 'Partitioned table parallelism' is Enterprise/Standard/Web only (not the Express editions), even though partitioning itself is in all editions. And on the 10 GB Express cap - that is per database, so an audit database split out under Express gets its own 10 GB, which is a real argument for separation rather than against it.

### A2.24  [yes]

**Claim.** On SQL Server on-premises, AUTOMATIC ledger digest storage 'only supports Azure Storage accounts' (and requires a SAS-based SQL Server credential; managed identities 'aren't supported for SQL Server on-premises deployments'). MANUAL digest generation is unconstrained: 'You can also generate a database digest on demand so that you can manually store the digest in any service or device that you consider a trusted storage destination. For example, you might choose an on-premises write once, read many (WORM) device as a destination', via EXECUTE sp_generate_database_ledger_digest, which requires the GENERATE LEDGER DIGEST permission.

**Limit or threshold asserted.** Automatic digests generated at a 30-second interval when transactions occurred; on-prem automatic storage limited to Azure Storage accounts

- Source: Microsoft Learn, Ledger digest management (SQL Server)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/security/ledger/ledger-digest-management?view=sql-server-ver16>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All quotes verbatim; the 30-second interval and the 'if no transactions occur ... a digest won't be generated' condition are both confirmed, as is the GENERATE LEDGER DIGEST permission. Three details to carry: the stored procedure's full name is sys.sp_generate_database_ledger_digest; for SQL Server the sqldbledgerdigests container must be created manually; and the managed-identity exclusion has a boundary - since SQL Server 2022 CU17 managed identities work for SQL Server on Azure VMs and Arc-enabled SQL Server 2025, just not for on-premises deployments. The design point (manual digest generation is the unconstrained path to a non-cloud WORM destination) is correct and is the right lever for the tamper-evidence requirement.

### A2.25  [yes]

**Claim.** Contrary to a common belief that it is Azure-only, the temporal-table automatic retention policy HISTORY_RETENTION_PERIOD applies to 'SQL Server 2017 (14.x) and later versions, Azure SQL Database, Azure SQL Managed Instance, and SQL database in Microsoft Fabric'. Two operational traps: retention defaults to INFINITE if the clause is omitted or if SYSTEM_VERSIONING is set OFF and back ON without restating it; and after a point-in-time restore the Database Engine 'sets it to OFF automatically', silently stopping cleanup until TEMPORAL_HISTORY_RETENTION is set back ON. Rowstore cleanup deletes in chunks of up to 10,000 rows.

**Limit or threshold asserted.** SQL Server 2017+; cleanup chunk size up to 10,000 rows; is_temporal_history_retention_enabled set OFF after PITR

- Source: Microsoft Learn, Manage Historical Data in System-Versioned Temporal Tables
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/tables/manage-retention-of-historical-data-in-system-versioned-temporal-tables?view=sql-server-ver16>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every element verified, including 'Cleanup logic for the rowstore clustered index deletes aged rows in smaller chunks (up to 10,000)'. Two operational traps the claim misses and that belong with the two it names: a finite retention period requires the history table's clustered rowstore index to START with the end-of-period column or configuration fails with Msg 13765 (and that index then cannot be dropped, Msg 13766); and queries against the temporal table auto-filter rows past the retention period, so direct history-table reads and FOR SYSTEM_TIME reads diverge. Note the enclosing article's own Applies-to line is SQL Server 2016+, while the SQL Server 2017+ line is specific to the retention-policy section the claim relies on - the claim quotes the right one.

### A2.26  [yes]

**Claim.** The same Microsoft guidance gives the three sanctioned retention mechanisms for history data and their trade-offs: engine retention policy (simplest, deletes outright), table partitioning with a sliding window using SWITCH PARTITION + MERGE RANGE + SPLIT RANGE (use when you want to ARCHIVE before removing; switching works while SYSTEM_VERSIONING is ON; use RANGE LEFT to avoid data movement), and a custom cleanup script (requires SYSTEM_VERSIONING = OFF, and warns that 'deleting more than 10,000 rows in a single transaction might impose a significant penalty').

**Limit or threshold asserted.** 10,000 rows per delete transaction as the stated caution threshold

- Source: Microsoft Learn, Manage Historical Data in System-Versioned Temporal Tables
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/tables/manage-retention-of-historical-data-in-system-versioned-temporal-tables?view=sql-server-ver16>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed against the article's own three-row comparison table, whose 'when to use it' column matches the claim's trade-offs exactly - retention policy 'when you can delete aged history outright', partitioning 'when you want to archive historical data before you remove it', custom script 'when a retention policy isn't available for your table, and partitioning isn't viable'. The RANGE LEFT rationale is confirmed: in a sliding window you always remove the lowest boundary, which under RANGE LEFT belongs to the already-emptied partition 1, so MERGE RANGE moves no data. Add one requirement the claim omits: to switch partitions the history table's clustered index must be aligned with the partitioning scheme (must contain ValidTo). Also the custom-script path needs CONTROL permission on both current and history tables.

### A2.27  [partially]

**Claim.** Filegroup-based separation of audit data is possible but edition-limited: 'All editions of SQL Server support offline piecemeal restores. In the Enterprise edition, a piecemeal restore can be either online or offline.' A partial-restore sequence must start with the PRIMARY filegroup and READ_WRITE_FILEGROUPS, and read-only filegroups may be restored later or left offline while the database is usable.

**Limit or threshold asserted.** Online piecemeal restore is Enterprise-only; offline piecemeal restore is all editions

- Source: Microsoft Learn, Piecemeal Restores (SQL Server)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/piecemeal-restores-sql-server?view=sql-server-ver16>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The edition sentence is verbatim and the read-only-filegroup behaviour is right ('any unrestored filegroups remain offline and are not accessible. Any offline filegroups, however, can be restored and brought online later by a file restore'). But the READ_WRITE_FILEGROUPS requirement is over-generalized - the doc scopes it to the SIMPLE recovery model initial stage, 'Specify the READ_WRITE_FILEGROUPS option if the backup is a full database backup', and says the partial-restore sequence minimally restores primary 'and, under the simple recovery model, all read/write filegroups'. Under FULL or bulk-logged recovery the partial-restore sequence restores PRIMARY plus optionally some secondary filegroups, with PARTIAL as the only mandatory option. Also worth carrying: during the partial-restore sequence the WHOLE database goes offline regardless of edition - so 'the audit filegroup can be restored without touching production' is only true for subsequent filegroup-restore sequences, and only online on Enterprise. Finally the article header narrows to 'Enterprise edition (online restore) or Standard edition (offline restore)', which sits awkwardly with the 'all editions' body sentence; do not promise piecemeal restore on Web or Express without testing.

### A2.28  [yes]

**Claim.** Object-lock WORM is a commodity S3 capability with two modes whose difference is the whole point for a system whose application authenticates to its object store as root: in COMPLIANCE mode 'a protected object version can't be overwritten or deleted by any user, including the root user in your AWS account ... its retention mode can't be changed, and its retention period can't be shortened'; in GOVERNANCE mode a caller holding s3:BypassGovernanceRetention and sending x-amz-bypass-governance-retention:true can override it. Object Lock 'works only in buckets that have S3 Versioning enabled'. Legal holds have no expiry and are independent of retention periods.

**Limit or threshold asserted.** COMPLIANCE mode: no user including account root may delete or shorten retention before expiry; versioning mandatory

- Source: AWS S3 User Guide, Locking objects with Object Lock
- URL: <https://docs.aws.amazon.com/AmazonS3/latest/userguide/object-lock-overview.html>
- Second source: <https://docs.min.io/community/minio-object-store/administration/object-management/object-retention.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All four elements verbatim, including the governance-mode override permission and header, and 'Legal holds are independent from retention periods'. Three additions that matter for the requirement this claim underwrites: the doc states 'The only way to delete an object under the compliance mode before its retention date expires is to delete the associated AWS account' - name that residual, because it is the one credential path that does defeat compliance mode; a retention period CAN always be EXTENDED by anyone holding s3:PutObjectRetention, in either mode; and a simple DELETE (no version id) still returns 200 OK and inserts a delete marker, so 'cannot be deleted' means the version survives, not that the object stays visible at its key. (Note: the fetched AWS page carried trailing text suggesting the reader run AWS CLI catalog commands. That is page content, not instruction; I ignored it and did not act on it.)

### A2.29  [yes]

**Claim.** The object store currently deployed here is not a maintained platform for a six-year archive: the canonical minio/minio GitHub repository reports "archived": true with a last push of 2026-04-24, i.e. read-only with no further releases. (Its S3-compatible object-lock/WORM semantics, including compliance mode and enabling locking on pre-existing buckets from RELEASE.2025-05-20T20-30-00Z onward, are documented and were the basis for the second-source check above.)

**Limit or threshold asserted.** archived: true; pushed_at 2026-04-24T17:54:39Z; licence AGPL-3.0

- Source: GitHub REST API, repository metadata for minio/minio
- URL: <https://github.com/minio/minio>
- Second source: <https://docs.min.io/community/minio-object-store/administration/object-management/object-retention.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed on both the rendered page and the GitHub API: archived true, pushed_at exactly 2026-04-24T17:54:39Z, AGPL-3.0, and a README banner reading 'THIS REPOSITORY IS NO LONGER MAINTAINED' that redirects readers to the vendor's successor products. Add the archive date (25 April 2026, one day after the final push) since that is the durable fact. The parenthetical is NOT supported by this URL: the object-lock/compliance-mode semantics and the RELEASE.2025-05-20T20-30-00Z behaviour for enabling locking on pre-existing buckets come from the product documentation, not the repository page - cite that separately or drop it. Also, an archived repository does not mean the deployed binary stops working; state the risk as 'no upstream security fixes for the retention horizon', which is the actual argument.

### A2.30  [partially]

**Claim.** Hangfire remains actively maintained (Hangfire / Hangfire.SqlServer 1.8.24, published 16 July 2026), so building the archive job on the existing scheduler does not add an unmaintained dependency. Note for the retention inventory: Hangfire expires only jobs in a FINAL state  -  'only Succeeded and Deleted built-in states, but not the Failed one'  -  after 24 hours by default, so failed jobs accumulate indefinitely in the HOST database.

**Limit or threshold asserted.** 1.8.24, 2026-07-16; default job expiration 24 hours for Succeeded and Deleted states only

- Source: NuGet Gallery, Hangfire.SqlServer; Hangfire documentation, Background Methods
- URL: <https://www.nuget.org/packages/Hangfire.SqlServer/>
- Second source: <https://docs.hangfire.io/en/latest/background-methods/index.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The maintenance conclusion holds and is in fact stronger than stated, but the version is stale by three days: latest is Hangfire.SqlServer 1.8.25, published 2026-08-28 (1.8.24 of 2026-07-16 is the prior release). Update the number and the date. Separately, the job-expiration behaviour - the quoted 'only Succeeded and Deleted built-in states, but not the Failed one' and the 24-hour default - is not on the cited NuGet page at all; it comes from the Hangfire configuration documentation. Either cite that page or mark the retention-inventory note as uncited. The underlying point (failed jobs are never auto-expired and accumulate in the host database, so they belong in the retention inventory) is worth keeping once properly sourced.

### A2.31  [partially]

**Claim.** Sizing inversion (MY REASONING, from the stated measurements, not a sourced fact): the 16-appointment sample yields ~259 counted audit rows per appointment (1,450 + 2,689 across two of four tables; the two uncounted tables sit between them, so the true figure is higher). Taking a blended ~0.8 KB per row  -  defensible from the column widths above but NOT measured  -  the ~9.7 GB of free disk on the current VM holds on the order of 12 million audit rows, i.e. roughly 46,000 appointments' worth across the entire fleet, ever, before the disk is the binding constraint. At 11 offices that is about 4,200 appointments per office. This is the number the team should replace with a measured one before choosing a hot-window length.

**Limit or threshold asserted.** ~259 counted audit rows per appointment; ~12M rows to fill 9.7 GB at an ASSUMED 0.8 KB/row

- Source: Author's calculation from the supplied measurements and the ABP column widths cited above
- URL: <https://raw.githubusercontent.com/abpframework/abp/1f7b7a56503515a6b26da6101ebc9d3921b461d9/modules/audit-logging/src/Volo.Abp.AuditLogging.EntityFrameworkCore/Volo/Abp/AuditLogging/EntityFrameworkCore/AbpAuditLoggingDbContextModelBuilderExtensions.cs>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Correctly self-flagged as reasoning, and the arithmetic is internally consistent: (1,450 + 2,689) / 16 = 258.7 rows per appointment; 9.7 GB / 0.8 KB = ~12.7M rows; 12.7M / 259 = ~49,000 appointments (the claim's ~46,000 is slightly conservative, fine); / 11 offices = ~4,200. But the cited URL supports only the column widths, none of the numbers, so it should not be presented as the source. Three ways the estimate is optimistic and should be labelled as a floor, not a range: it omits index storage (AbpAuditLogs alone carries two composite indexes plus the clustered PK, and the child tables one or two each - typically 30-60% on top of heap size); the 0.8 KB blended figure assumes Parameters and Exceptions are near-empty, whereas Exceptions is nvarchar(max) and Parameters is nvarchar(2000), so a single exception-bearing request can dwarf the average; and the two uncounted tables are acknowledged but not bounded. The recommendation - replace this with a measured sp_spaceused/sys.dm_db_partition_stats figure per table before choosing a hot-window length - is the right call and should be stated as a blocking prerequisite, not an improvement.

### A2.32  [yes]

**Claim.** SQL Server Audit gives an engine-level trail that application-level audit structurally cannot, which matters here because every service connects as sa: it is available in all editions, writes to a FILE target, and its ON_FAILURE option is the lever for choosing loud failure  -  CONTINUE (default; 'Audit records aren't retained ... can allow unaudited activity'), FAIL_OPERATION ('Database actions fail if they cause audited events'), or SHUTDOWN. MAX_ROLLOVER_FILES defaults to UNLIMITED and MAXSIZE defaults to UNLIMITED, so an unbounded audit file target will fill the disk; MAX_FILES instead fails new audited actions when the limit is reached. QUEUE_DELAY defaults to 1000 ms (0 = synchronous).

**Limit or threshold asserted.** ON_FAILURE default CONTINUE; MAXSIZE default UNLIMITED; MAX_ROLLOVER_FILES default UNLIMITED; QUEUE_DELAY default/minimum 1000 ms; MAXSIZE minimum 2 MB

- Source: Microsoft Learn, CREATE SERVER AUDIT (Transact-SQL)
- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/create-server-audit-transact-sql?view=sql-server-ver16>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every default and threshold is verbatim, including MAXSIZE minimum 2 MB (below which MSG_MAXSIZE_TOO_SMALL) and QUEUE_DELAY 0 for synchronous delivery. The 'available in all editions' half is not on this page - it comes from the editions-and-components page (claim 23), which does confirm Server audit and fine-grained auditing as Yes in all five SQL Server 2022 editions; cite it there. Two additions worth carrying: MAX_ROLLOVER_FILES deletes only ONE old file per evaluation, so lowering the value does not shrink an existing file set without manual deletion - meaning an over-large audit directory does not self-heal; and RESERVE_DISK_SPACE (default OFF) preallocates to MAXSIZE, which is the cleaner way to make audit-tier exhaustion visible early rather than as a full disk. The design point - that engine-level audit is the only trail that observes direct sa access, which application-level audit structurally cannot - is correct.

---

## Area: scaling-topology

Verification verdict for this area: **material-errors** (40 claims checked)

### A3.1  [yes]

**Claim.** Hangfire's RecurringJobScheduler acquires a single distributed lock on the resource 'recurring-jobs:lock' with a 1-minute timeout; on DistributedLockTimeoutException it logs at Debug level with the message 'The recurring jobs have not been handled this time' and simply skips that pass.

**Limit or threshold asserted.** LockTimeout = TimeSpan.FromMinutes(1); resource = 'recurring-jobs:lock'; log level = Debug

- Source: Hangfire source, RecurringJobScheduler.cs (main branch)
- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/main/src/Hangfire.Core/Server/RecurringJobScheduler.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Accurate. One nuance worth carrying forward: the code comment immediately above the catch states 'DistributedLockTimeoutException here doesn't mean that recurring jobs weren't scheduled. It just means another Hangfire server did this work.' The skip is by design, not a failure.

### A3.2  [yes]

**Claim.** With multiple Hangfire servers against one storage, coordination is by distributed lock: 'Each server use distributed locks to perform the coordination logic.' Server identifiers combine a server name (default: machine name) with a process ID.

- Source: Hangfire Documentation  -  Running Multiple Server Instances
- URL: <https://docs.hangfire.io/en/latest/background-processing/running-multiple-server-instances.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: No correction needed.

### A3.3  [yes]

**Claim.** Hangfire documents that duplicate execution cannot be fully prevented: 'there's no reliable way to prevent multiple executions of the same background job other than by using transactions in background job method itself.' DisableConcurrentExecution 'heavily relies on an active connection, which may be broken (and lock is released) without any notification.' Mutexes and semaphores are part of Hangfire.Throttling, available only on a private commercial feed, not the open-source package.

**Limit or threshold asserted.** Hangfire.Throttling is part of Hangfire.Ace, private NuGet feed

- Source: Hangfire Documentation  -  Concurrency & Rate Limiting (Throttling)
- URL: <https://docs.hangfire.io/en/latest/background-processing/throttling.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: No correction needed.

### A3.4  [partially]

**Claim.** QueuePollInterval = TimeSpan.Zero does NOT mean continuous polling. It enables long polling with a default 200 ms delay, clamped between 100 ms and 1000 ms. Hangfire's own documentation calls TimeSpan.Zero with SlidingInvisibilityTimeout 'the recommended value'.

**Limit or threshold asserted.** DefaultPollingDelayMs = 200; MinPollingDelayMs = 100; PollingQuantumMs = 1000; LongPollingThreshold = 1 second

- Source: Hangfire source SqlServerJobQueue.cs; Hangfire 1.8.0 release notes; Hangfire SQL Server configuration docs
- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/main/src/Hangfire.SqlServer/SqlServerJobQueue.cs>
- Second source: <https://www.hangfire.io/blog/2023/04/28/hangfire-1.8.0.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The four constants and the clamping are confirmed by the cited source file. The documentation quote is NOT on the cited URL  -  it is on <https://docs.hangfire.io/en/latest/configuration/using-sql-server.html>, and the actual sentence is 'This is the recommended value in that version, but you can decrease the polling interval if your background jobs can tolerate additional delay before the invocation.' Split this into two claims with two URLs; the source file alone does not support the 'recommended' assertion.

### A3.5  [partially]

**Claim.** DisableGlobalLocks = true removes the sp_getapplock-based global lock used around queue fetch and requires Schema 7. Since Hangfire 1.8, TryAutoDetectSchemaDependentOptions (default true) auto-detects it from the current schema version. It does not disable the distributed-lock API used by the recurring job scheduler.

**Limit or threshold asserted.** Schema 7 required; TryAutoDetectSchemaDependentOptions default true; DisableGlobalLocks default false

- Source: Hangfire Documentation  -  Using SQL Server; Hangfire 1.8.0 release notes; SqlServerStorageOptions.cs
- URL: <https://docs.hangfire.io/en/latest/configuration/using-sql-server.html>
- Second source: <https://raw.githubusercontent.com/HangfireIO/Hangfire/main/src/Hangfire.SqlServer/SqlServerStorageOptions.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Only two parts survive: (a) Schema 7 is required for DisableGlobalLocks, and (b) TryAutoDetectSchemaDependentOptions defaults to true and auto-detects schema-dependent options in Hangfire 1.8. The page says NOTHING about sp_getapplock, nothing about the lock being 'around queue fetch', does not state a default of false for DisableGlobalLocks, and does not state that it leaves the distributed-lock API intact. Those are component behaviours asserted without documentary support on this URL  -  either drop them or cite the SqlServerStorage/SqlServerDistributedLock source.

### A3.6  [yes]

**Claim.** Hangfire's default worker count is Environment.ProcessorCount * 5.

**Limit or threshold asserted.** WorkerCount = Environment.ProcessorCount * 5

- Source: Hangfire Documentation  -  Configuring the Degree of Parallelism
- URL: <https://docs.hangfire.io/en/latest/background-processing/configuring-degree-of-parallelism.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: No correction needed.

### A3.7  [partially]

**Claim.** Hangfire warns that during a version upgrade with multiple servers: 'Before performing this step, ensure all your processing servers successfully migrated to the new version. Otherwise you may get exceptions or even undefined behavior.'

- Source: Hangfire Documentation  -  Upgrading to Hangfire 1.7
- URL: <https://docs.hangfire.io/en/latest/upgrade-guides/upgrading-to-hangfire-1.7.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The quote is truncated in a way that widens its scope. The doc's sentence ends '...caused by custom JSON serialization settings', and it is scoped to one specific upgrade step (enabling the new JSON serialization/compatibility settings), not to version upgrades generally. Quote the sentence in full or restate as: Hangfire warns that enabling the new 1.7 serialization settings before every processing server has migrated can cause exceptions or undefined behaviour.

### A3.8  [yes]

**Claim.** Hangfire.SqlServer is actively maintained: latest version 1.8.25 published 2026-08-28. The deployed version 1.8.21 dates from 2025-08-12, four releases and roughly twelve months behind.

**Limit or threshold asserted.** 1.8.25 on 2026-08-28; 1.8.21 on 2025-08-12

- Source: NuGet Gallery  -  Hangfire.SqlServer
- URL: <https://www.nuget.org/packages/HangFire.SqlServer/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed exactly: four intervening releases (1.8.22, .23, .24, .25) and 12.5 months. Note separately that version 1.8.16 is flagged deprecated on NuGet 'because it has critical bugs'  -  not the deployed version, but relevant if any rollback target is chosen.

### A3.9  [yes]

**Claim.** A duplicate recurring-job execution race with multiple BackgroundJobServers is a reported open issue: the scheduler truncates timestamps to whole seconds and, if SetRangeInHash fails after triggering, LastExecution is not persisted, permitting a re-trigger. No maintainer resolution or fix version was evident on the issue.

**Limit or threshold asserted.** Reported against 1.7.0-beta1; status open, no assignee, no linked PR at time of access

- Source: HangfireIO/Hangfire issue #1208
- URL: <https://github.com/HangfireIO/Hangfire/issues/1208>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Supported as stated. Keep the 'partial' confidence: the issue is a user report against a 2019 beta, not a maintainer-confirmed defect in current code, so it evidences an unresolved concern rather than a live defect in 1.8.21.

### A3.10  [yes]

**Claim.** ABP explicitly warns about background workers in clustered environments: 'Be careful if you run multiple instances of your application simultaneously in a clustered environment. In that case, every application runs the same worker which may create conflicts if your workers are running on the same resources.' Its recommended remedies are a distributed lock, setting AbpBackgroundWorkerOptions.IsEnabled to false on all instances but one, or a dedicated background application.

**Limit or threshold asserted.** AbpBackgroundWorkerOptions.IsEnabled = false

- Source: ABP.IO Documentation  -  Background Workers
- URL: <https://abp.io/docs/latest/framework/infrastructure/background-workers>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: No correction needed. All three remedies are present verbatim.

### A3.11  [yes]

**Claim.** AbpBackgroundJobOptions.IsJobExecutionEnabled = false disables job EXECUTION while other instances can still QUEUE jobs. It is a different option from AbpBackgroundWorkerOptions.IsEnabled, which controls workers.

- Source: ABP.IO Documentation  -  Background Jobs
- URL: <https://abp.io/docs/latest/framework/infrastructure/background-jobs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed, including the queue-while-disabled parenthetical (verified against the doc source at raw.githubusercontent.com/abpframework/abp/dev/docs/en/framework/infrastructure/background-jobs/index.md, line 411). The rendered abp.io page's short 'Disable Job Execution' snippet alone does not state it  -  cite the full 'Clustered Environments' section of that page. The contrast with AbpBackgroundWorkerOptions.IsEnabled is documented on the background-workers page, not this one.

### A3.12  [yes]

**Claim.** ABP's clustered-environment deployment guidance covers distributed cache, BLOB storage, background jobs, distributed locking, background workers and SignalR  -  and does NOT cover ASP.NET Core Data Protection. It states the default (database) background job manager 'uses a distributed lock to ensure that jobs are executed only in a single application instance at a time', and that the file-system BLOB provider must not be used in a clustered environment.

- Source: ABP.IO Documentation  -  Deploying to a Clustered Environment
- URL: <https://abp.io/docs/latest/deployment/clustered-environment>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Substantively correct, including the notable omission of Data Protection. Minor: the quoted sentence reads 'ensure that the jobs are executed', not 'ensure that jobs are executed'. Fix the quote if reproducing verbatim.

### A3.13  [partially]

**Claim.** ABP's default distributed event bus implementation is LocalDistributedEventBus, which works in-process exactly like the local event bus when no real distributed provider (RabbitMQ, Kafka, Azure Service Bus) is configured. Distributed events therefore do not cross application instances.

- Source: ABP.IO Documentation  -  Distributed Event Bus
- URL: <https://docs.abp.io/en/abp/latest/Distributed-Event-Bus>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Two problems. (1) The cited URL is stale: docs.abp.io/en/abp/latest/Distributed-Event-Bus returns HTTP 308 to <https://abp.io/docs/en/abp/latest/Distributed-Event-Bus>. Cite the current URL. (2) 'Distributed events therefore do not cross application instances' is your inference, not a documented sentence  -  the page states the default is in-process and behaves like the local bus, from which the conclusion follows, but no sentence on the page asserts it. Present it as an inference from the documented default, or state it as a fact you verified by reading LocalDistributedEventBus.

### A3.14  [yes]

**Claim.** ABP's default tenant resolvers run in this order: CurrentUserTenantResolveContributor (documented as 'This should always be the first contributor for the security'), then QueryString (__tenant), Route (__tenant), Header (__tenant), Cookie (__tenant). The domain/subdomain resolver is not among the defaults and is added separately via AbpTenantResolveOptions.AddDomainTenantResolver().

**Limit or threshold asserted.** Default parameter/header/cookie/route name is __tenant

- Source: ABP.IO Documentation  -  Multi-Tenancy
- URL: <https://abp.io/docs/latest/framework/architecture/multi-tenancy>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed, and directly load-bearing for the requirement that a request must not be able to select a tenant by any means other than the Host header  -  all four of QueryString/Route/Header/Cookie are enabled by default and must be explicitly removed. The page also notes nginx may strip headers containing underscores, which matters for the __tenant header path.

### A3.15  [yes]

**Claim.** SetApplicationName sets DataProtectionOptions.ApplicationDiscriminator. To share protected payloads across apps, BOTH conditions are required: configure SetApplicationName with the same value in each app, AND 'Use the same version of the Data Protection API stack across the apps'  -  either the same shared framework version or the same Data Protection package version. By default apps are isolated by content root path even when sharing a physical key repository.

**Limit or threshold asserted.** Two conditions, both mandatory

- Source: Microsoft Learn  -  Configure ASP.NET Core Data Protection
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Fully confirmed, both conditions and the discriminator mechanism. The same page also supplies the exact startup-logging snippet the requirement asks for: reading IOptions<DataProtectionOptions>.Value.ApplicationDiscriminator and logging it after the WebApplication is built.

### A3.16  [yes]

**Claim.** The Data Protection key ring is cached in memory and the backing store is re-checked 'approximately every 24 hours or when the current default key expires, whichever comes first'. New keys are created with an activation date of now + 2 days and expiry of now + 90 days; the 2-day delay exists specifically 'to allow other applications pointing at the backing store to observe the key at their next auto-refresh period'. Default key lifetime is 90 days and cannot be set shorter than 7 days. Deleting a key is described as 'truly destructive behavior'  -  all data protected by it becomes permanently undecipherable.

**Limit or threshold asserted.** ~24 h refresh; activation now+2 days; expiry now+90 days; minimum lifetime 7 days

- Source: Microsoft Learn  -  Key management in ASP.NET Core
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every threshold confirmed verbatim. Worth adding for the deployment story: the page also notes that any operation which modifies the key ring invalidates the in-memory cache locally, and that a key may be created with immediate activation (no 2-day delay) when the app hasn't run for a while and all keys are expired  -  which is exactly the cold-start case a second instance would hit.

### A3.17  [yes]

**Claim.** Microsoft's Data Protection configuration guidance states that 'Only Redis versions supporting Redis Data Persistence should be used to store keys.'

- Source: Microsoft Learn  -  Configure ASP.NET Core Data Protection, 'Persisting keys with Redis'
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: No correction needed.

### A3.18  [partially]

**Claim.** Redis eviction: the default maxmemory-policy is noeviction and maxmemory is unset by default (no limit on 64-bit). allkeys-lru / allkeys-lfu / allkeys-lrm / allkeys-random evict ANY key, including keys with no TTL. volatile-* policies evict only keys with an associated expiration and 'behave like noeviction if no keys have an associated expiration'. Under noeviction, Redis returns errors on commands that would use more memory but continues to serve read-only commands.

**Limit or threshold asserted.** Default maxmemory-policy = noeviction; default appendonly = no

- Source: Redis Documentation  -  Key eviction; redis.conf 7.4
- URL: <https://redis.io/docs/latest/develop/reference/eviction/>
- Second source: <https://raw.githubusercontent.com/redis/redis/7.4/redis.conf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Two stated defaults are NOT on this page and must be re-sourced or dropped. (1) The page never says the default maxmemory-policy is noeviction  -  it documents the policy list and noeviction's behaviour but states no default. (2) The page never mentions appendonly at all, so 'default appendonly = no' has no support here; source it from redis.conf or the persistence docs. Everything else checks out, including 'allkeys-lrm' (real, added in Redis 8.6, Least Recently Modified)  -  that spelling is correct and not a typo for allkeys-lru. This matters because the key-ring-eviction requirement leans on the claimed default: assert the policy explicitly rather than relying on an unverified default.

### A3.19  [partially]

**Claim.** DistributedLock.Redis (Medallion.Threading.Redis) is currently maintained: version 1.1.1 published 2025-10-26, following 1.1.0 on 2025-08-10. It implements the RedLock algorithm; robustness increases only by constructing the lock with multiple independent databases, where 'the lock is only considered acquired if it is successfully acquired on more than half of the databases'. Held leases are extended automatically in the background until the handle is disposed.

**Limit or threshold asserted.** 1.1.1 on 2025-10-26; majority quorum = more than half of N databases; depends on StackExchange.Redis >= 2.7.33

- Source: NuGet  -  DistributedLock.Redis; madelson/DistributedLock Redis documentation
- URL: <https://www.nuget.org/packages/DistributedLock.Redis/>
- Second source: <https://github.com/madelson/DistributedLock/blob/master/docs/DistributedLock.Redis.md>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The version and dependency facts are confirmed. The behavioural claims are NOT on the cited URL  -  I scraped the full NuGet page and 'redlock' appears only as a package tag; there is no README section on the page describing the majority-of-databases quorum or background lease extension. Re-cite those to the project's own documentation (github.com/madelson/DistributedLock, docs/DistributedLock.Redis.md) or drop the quotation marks. Also note the maintenance argument is thinner than stated: the most recent release is 2025-10-26, roughly ten months before today, which passes the twelve-month bar but only just.

### A3.20  [yes]

**Claim.** The correctness of Redis-based distributed locking is contested. Redlock is designed for N independent Redis masters with majority acquisition; a single instance loses the lock entirely if the node fails, and a primary/replica failover can allow two clients to hold the same lock. Kleppmann argues Redlock's safety rests on timing assumptions and is unsuitable for correctness-critical locks; antirez's Redlock write-up defends it. Sources disagree.

**Limit or threshold asserted.** Quorum = N/2+1 independent masters

- Source: Redis  -  Distributed Locks with Redis; Martin Kleppmann  -  How to do distributed locking; antirez  -  The Redlock Algorithm
- URL: <https://redis.io/docs/latest/develop/clients/patterns/distributed-locks/>
- Second source: <https://martin.kleppmann.com/2016/02/08/how-to-do-distributed-locking.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Fully supported, and stronger than claimed: the page itself carries a 'Disclaimer about consistency' telling readers to implement fencing tokens and warning that Redis does not use a monotonic clock for TTL expiration, so a wall-clock shift can hand the same lock to two processes. Add that  -  it is the sharpest argument against using a Redis lock as the sole guard on a non-idempotent job.

### A3.21  [partially]

**Claim.** Starting with EF Core 9, Migrate() and MigrateAsync() automatically acquire a database-wide lock before applying migrations. 'This protects against database corruption that could result from multiple application instances running migrations concurrently, which is a common scenario when applying migrations at runtime.' The lock is held for the migration and any seeding code. On SQL Server it is implemented as a session-scoped sp_getapplock on resource '__EFMigrationsLock' in Exclusive mode.

**Limit or threshold asserted.** EF Core 9 and later; sp_getapplock @Resource='__EFMigrationsLock', @LockOwner='Session', @LockMode='Exclusive'

- Source: Microsoft Learn  -  Applying Migrations (EF Core); dotnet/efcore issue #34439
- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Second source: <https://github.com/dotnet/efcore/issues/34439>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The EF Core 9 lock, its purpose, and its coverage of seeding are confirmed verbatim. The SQL Server implementation detail is NOT on this page: it never names sp_getapplock, the resource '__EFMigrationsLock', @LockOwner='Session' or @LockMode='Exclusive'. The page explicitly says the mechanism is provider-specific and points readers to provider docs. Either drop the sp_getapplock specifics or source them from the SqlServerMigrationsLock implementation in the efcore repo; do not attribute them to this page.

### A3.22  [yes]

**Claim.** Microsoft's current EF Core deployment guidance for containers directly endorses the one-shot migrator pattern this system already uses: 'Generate the bundle during the build and run it as a one-shot deployment job after the database is healthy. Don't install the SDK or run dotnet ef in the application image, and don't make every application replica run migrations from its entrypoint. Configure the deployment platform not to restart the migration container after it exits successfully.'

- Source: Microsoft Learn  -  Applying Migrations (EF Core), 'Containers and deployment jobs'
- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Verbatim match under the heading 'Containers and deployment jobs'. Note the guidance is recent  -  the page's ms.date is 2026-08-05  -  so this section did not exist in older versions of the article; cite the current revision.

### A3.23  [yes]

**Claim.** Starting with EF Core 9, Migrate()/MigrateAsync() throws when the model has pending changes compared to the last migration (RelationalEventId.PendingModelChangesWarning). Microsoft recommends detecting this before deployment with `dotnet ef migrations has-pending-model-changes` in CI, and states that suppressing the warning 'is generally not recommended in production scenarios'.

**Limit or threshold asserted.** EF Core 9 and later; command: dotnet ef migrations has-pending-model-changes

- Source: Microsoft Learn  -  Applying Migrations (EF Core), 'Migration locking'
- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: No correction needed; every element including the command name and the suppression caveat is verbatim.

### A3.24  [yes]

**Claim.** ADO.NET connection pools are keyed per process, per application domain, per connection string (exact string match, keyword order significant), and per Windows identity where integrated security is used. 'A connection pool is created for each unique connection string.' Default Max Pool Size is 100. On exhaustion the request is queued and an exception is thrown after the connection timeout, default 15 seconds. Idle connections are removed after approximately 4-8 minutes when MinPoolSize is 0 or unset.

**Limit or threshold asserted.** Max Pool Size default 100; connection timeout default 15 s; idle reaping 4-8 minutes; login-failure blocking period 5 s doubling to max 1 minute

- Source: Microsoft Learn  -  SQL Server connection pooling (ADO.NET)
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every threshold confirmed. One precision worth adding: the 4-8 minute reaping is conditioned on LoadBalanceTimeout/Connection Lifetime being unset (default 0); the MinPoolSize=0 note is a separate sentence saying the pool's connections 'will be closed after a period of inactivity'.

### A3.25  [yes]

**Claim.** Microsoft names this system's exact architecture as a connection-pooling anti-pattern: under 'Pool fragmentation due to many databases' it states that opening a connection to a specific database per user or group means 'there is a separate pool of connections to each database, which increase the number of connections to the server.'

- Source: Microsoft Learn  -  SQL Server connection pooling (ADO.NET), 'Pool fragmentation'
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Quote is accurate. Temper the framing: Microsoft calls this 'a side-effect of the application design' under a section titled 'Pool fragmentation', not an 'anti-pattern', and its suggested remedy (connect to one database and issue USE to switch) is a shared-server pattern that does not apply cleanly to a per-office-database fleet. Cite it as the documented mechanism, not as Microsoft condemning this system's architecture.

### A3.26  [yes]

**Claim.** SQL Server's automatically configured max worker threads for a 64-bit machine with 4 or fewer logical CPUs is 512. The default max worker threads setting is 0, meaning auto-configure.

**Limit or threshold asserted.** 512 worker threads at <=4 logical CPUs, 64-bit; formula = Default Max Workers + ((logical CPUs - 4) * Workers per CPU)

- Source: Microsoft Learn  -  Server Configuration: max worker threads
- URL: <https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-the-max-worker-threads-server-configuration-option>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed. Two footnotes matter for the sizing argument: starting with SQL Server 2017 the Default Max Workers value is halved on machines with less than 2 GB of memory, and the page states worker threads are assigned only to active requests and released when serviced  -  an idle pooled connection consumes no worker thread.

### A3.27  [yes]

**Claim.** SQL Server Developer edition 'includes all the functionality of Enterprise edition, but is licensed for use as a development and test system, not as a production server.' Standard edition is limited to the lesser of 4 sockets or 24 cores and a 128 GB buffer pool per instance; Express is limited to the lesser of 1 socket or 4 cores, 1,410 MB buffer pool, and a 10 GB maximum relational database size.

**Limit or threshold asserted.** Standard: 128 GB buffer pool, lesser of 4 sockets/24 cores. Express: 1,410 MB buffer pool, 10 GB max DB size. Developer: not licensed for production.

- Source: Microsoft Learn  -  Editions and Supported Features of SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All five figures confirmed. Note the page is version-scoped to SQL Server 2022 (16.x) and links a separate article for SQL Server 2025 (17.x); if the fleet is or will be on 2025, re-verify against editions-and-components-of-sql-server-2025 (that page also drops Web edition and changes Express).

### A3.28  [partially]

**Claim.** Always On availability groups are Enterprise-only; Standard edition supports only Basic availability groups, whose documented limitations include 'Support for one availability database', 'Limit of two replicas (primary and secondary)', 'No read access on secondary replica', and 'No backups on secondary replica'. Standard edition does support Always On failover cluster instances with two nodes.

**Limit or threshold asserted.** One database per Basic AG; two replicas; Standard FCI = 2 nodes

- Source: Microsoft Learn  -  Basic Availability Groups; Editions and Supported Features of SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/sql/database-engine/availability-groups/windows/basic-availability-groups-always-on-availability-groups>
- Second source: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All four Basic AG limitations are verbatim on this page. But the two other assertions are not: this page never states that full Always On AGs are Enterprise-only, and it says nothing about failover cluster instances or a two-node Standard limit. Both come from the editions article  -  the HA table shows 'Always On availability groups: Enterprise Yes, Standard No', and footnote 4 reads 'On Enterprise edition, the maximum number of nodes is 16. On Standard edition, there's support for two nodes.' Cite editions-and-components-of-sql-server-2022 for those two. Also add the Linux caveat this page carries: Basic AGs on SQL Server 2017 on Linux support an extra configuration-only replica.

### A3.29  [yes]

**Claim.** Microsoft states that 'All FCIs require some shared storage' and that 'the underlying shared storage is a single point of failure since there's one copy of the data.' On Linux, 'Linux only supports a single installation of SQL Server per host, so all FCIs are a default instance', and for availability groups on Linux 'you should configure an AG with a minimum of three replicas, due to the way that the underlying clustering works.' For containers, orchestrator-based recovery 'isn't truly highly available as it would be if using an availability group or FCI.' Microsoft further states: 'The SQL Server availability features don't replace the requirement to have a robust, well tested backup and restore strategy. A backup and restore strategy is the most fundamental building block of any availability solution.'

**Limit or threshold asserted.** Minimum 3 replicas for a Linux AG; 1 SQL installation per Linux host

- Source: Microsoft Learn  -  Business Continuity and Database Recovery, SQL Server on Linux
- URL: <https://learn.microsoft.com/en-us/sql/linux/business-continuity/overview>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every quotation confirmed verbatim, including the exact wording of the backup statement. Minor: the first quote continues '...even if it's network defined'  -  include the full clause. This page is the strongest single source for the requirement that HA design be evaluated against the real fleet, since it also states a Standard-edition AG carries a single database per AG.

### A3.30  [partially]

**Claim.** nginx does NOT preserve the Host header through proxy_pass by default. The documented default is 'proxy_set_header Host $proxy_host;'  -  'By default, the header fields "Host" and "Connection" from the original request are not passed to the proxied server.' Separately, upstream names in proxy_pass are resolved at configuration/worker start unless the value contains a variable, in which case the name is resolved at runtime using a configured resolver.

**Limit or threshold asserted.** Default: proxy_set_header Host $proxy_host; Connection close

- Source: nginx documentation  -  ngx_http_proxy_module, proxy_set_header and proxy_pass
- URL: <https://nginx.org/en/docs/http/ngx_http_proxy_module.html#proxy_set_header>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The Host-header half is fully confirmed and is the load-bearing part for the tenant-resolution requirement. The resolution-timing half is only half-documented: the page confirms the variable/resolver runtime path but never states that a non-variable upstream name is resolved at configuration time or at worker start. Either drop that half of the sentence or cite it to nginx's proxy_pass/resolver documentation explicitly rather than asserting it as documented here.

### A3.31  [yes]

**Claim.** Microsoft's multitenancy guidance names this system's shape 'horizontally partitioned deployment'  -  a shared application tier with individual databases per tenant  -  and identifies its documented risk as: 'you still need to consider the automated deployment and management of your components, especially the components that a single tenant uses.' The mapping of tenants to deployments (stamps/supertenants) is the mechanism for scaling beyond one deployment.

- Source: Microsoft Learn  -  Azure Architecture Center, Tenancy models for a multitenant solution
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/tenancy-models>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed verbatim. Worth also carrying the stated benefit, which is the counterweight: horizontal partitioning 'can help you mitigate a noisy neighbor problem' because one tenant's database load does not degrade the others. Note this is Azure Architecture Center content  -  cite it as a description of the tenancy-model class, not as endorsement of an Azure deployment.

### A3.32  [yes]

**Claim.** The MinIO open-source object storage server repository is archived and unmaintained. GitHub displays 'This repository was archived by the owner on Apr 25, 2026. It is now read-only.' and the README states 'THIS REPOSITORY IS NO LONGER MAINTAINED.' The most recent release is RELEASE.2025-10-15T17-29-55Z, which was itself a security fix for 'Privilege Escalation via Session Policy Bypass in Service Accounts and STS'.

**Limit or threshold asserted.** Archived 2026-04-25; last release 2025-10-15

- Source: GitHub  -  minio/minio repository and releases
- URL: <https://github.com/minio/minio>
- Second source: <https://github.com/minio/minio/releases>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed, though the release facts required the /releases page  -  the repository landing page alone does not show them, so cite <https://github.com/minio/minio/releases> for the release tag and CVE. Note the releases page displays the publication date as Oct 16 2025 for the RELEASE.2025-10-15T17-29-55Z tag. This is the clearest instance of the 'component without a release in twelve months needs a dated replacement plan' requirement biting.

### A3.33  [partially]

**Claim.** Microsoft.Data.SqlClient 4.0 changed the default of the Encrypt connection setting from false to true, which is why TrustServerCertificate=True is required to connect to a server without a client-trusted certificate. Every pooled connection establishment therefore performs a TLS handshake.

**Limit or threshold asserted.** Encrypt default changed to true in Microsoft.Data.SqlClient 4.0

- Source: Microsoft Learn  -  Encryption and certificate validation (ADO.NET)
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/encryption-and-certificate-validation>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The version-4.0 default change and the TrustServerCertificate consequence are confirmed exactly. The third sentence is wrong as written and should be struck: nothing on this page says a TLS handshake occurs per connection establishment, and the mechanism contradicts the implication  -  the handshake happens when a *physical* connection is opened, whereas a pooled Open() that reuses an existing physical connection performs no handshake at all. The correct statement is that a handshake is paid whenever a new physical connection is created, i.e. on cold pools and after idle reaping. This directly weakens claim [39]'s cost model only in wording, not in substance.

### A3.34  [yes]

**Claim.** 45 CFR 164.316(b)(2)(i) requires retention for 6 years, but its scope is the DOCUMENTATION required by 164.316(b)(1)  -  'the policies and procedures implemented to comply with this subpart' and 'a written (which may be electronic) record of the action, activity, or assessment'. It reads: 'Retain the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later.' The rule does not by its own terms set a retention period for an application's entity-level audit trail; treating the AuditLogs and property-change tables as in scope is a defensible organisational policy decision, not a literal statutory mandate on those tables.

**Limit or threshold asserted.** 6 years from creation or last effective date, whichever is later

- Source: 45 CFR 164.316 (via GovInfo, CFR Title 45)
- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-316.xml>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The regulatory text and the scoping analysis are both correct  -  the section is about documentation of policies, procedures, actions and assessments, and never mentions audit trails or application logs. This is a rare case where the researcher correctly resisted an overstated compliance mandate. One caveat to note: this is the 2023 annual CFR edition; confirm no amendment since if the retention analysis is load-bearing.

### A3.35  [yes]

**Claim.** ABP's distributed cache automatically adds the current tenant id to the cache key for IDistributedCache<T>, and supports AbpDistributedCacheOptions.KeyPrefix for sharing a cache server across applications. The HideErrors option defaults to true, meaning cache failures are logged rather than thrown outside Development. ABP's documentation does not describe cross-instance invalidation or pub/sub for the plain distributed cache; a separate HybridCache feature keeps a local in-process cache backed by the distributed cache.

**Limit or threshold asserted.** HideErrors default true (disabled in Development)

- Source: ABP.IO Documentation  -  Distributed Caching
- URL: <https://abp.io/docs/latest/framework/fundamentals/caching>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed, including the negative finding that no pub/sub invalidation is documented. The HideErrors default is operationally significant for the observability requirement: with it true outside Development, a distributed cache outage is logged rather than thrown, so a degraded cache is silent to callers.

### A3.36  [partially]

**Claim.** ABP's dynamic permission/feature/setting definition stores keep an in-process memory cache validated against a 'cache stamp' held in the distributed cache, with a periodic re-check rather than immediate invalidation. Convergence across application instances is therefore eventual and bounded by that check interval, not immediate.

**Limit or threshold asserted.** Exact default check interval not confirmed; direct source read of DynamicPermissionDefinitionStore.cs returned 404 on the dev branch path tried

- Source: ABP support/community discussion of DynamicPermissionDefinitionStore and DynamicPermissionDefinitionStoreInMemoryCache (CacheStamp, LastCheckTime)
- URL: <https://abp.io/support/questions/8410/Unable-to-save-the-permission-after-creating-it-in-an-App-Service>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The existence of an in-memory cache guarded by LastCheckTime and CacheStamp is confirmed, so the eventual-convergence conclusion is directionally right. But the source is a support forum thread, not documentation, and it states no check interval and does not say the stamp lives in the distributed cache. Keep the 'partial' rating and do not quote an interval. To close this properly, read DynamicPermissionDefinitionStore/DynamicPermissionDefinitionStoreInMemoryCache in the abpframework/abp repo (the earlier 404 was a wrong path, not a missing file) and cite the concrete default; the requirement demands a 'stated, tested bound', which cannot be satisfied by a forum post.

### A3.37  [yes]

**Claim.** The ABP stack in use is materially behind current: Volo.Abp.BackgroundJobs.HangFire latest stable is 10.6.0 (2026-07-27) against the deployed ABP 10.0.2.

**Limit or threshold asserted.** 10.6.0 published 2026-07-27; 10.7.0-rc.3 on 2026-08-18

- Source: NuGet Gallery  -  Volo.Abp.BackgroundJobs.HangFire
- URL: <https://www.nuget.org/packages/Volo.Abp.BackgroundJobs.HangFire/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Both version facts confirmed. 'Materially behind' is a judgement, not a sourced fact  -  the package has a current release, so it passes the twelve-month recency bar; the gap is six minor versions of the researcher's own stack, which is an upgrade-debt argument, not a maintenance-risk one. Keep those two arguments separate.

### A3.38  [partially]

**Claim.** MY REASONING, not a sourced fact: at 33 offices, one API process holding a distinct connection string per office database can open up to 33 x 100 = 3,300 pooled connections by default, and two processes up to 6,600, against a SQL Server instance whose auto-configured worker thread limit at 4 logical CPUs is 512. Pool exhaustion and worker starvation are different limits reached by different paths, but both are plausible well before the tenant ceiling is reached, and neither is visible without instrumentation.

**Limit or threshold asserted.** 33 x 100 = 3,300 per process; 512 worker threads at 4 CPUs

- Source: Derived from Microsoft connection pooling and max worker threads documentation
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Second source: <https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-the-max-worker-threads-server-configuration-option>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The premises are sound and the arithmetic is right, but the juxtaposition of 3,300 against 512 invites a false comparison and should be rewritten. Per the max-worker-threads page, 'A worker thread is assigned only to active requests and is released once the request is serviced. This happens even if the user session/connection on which the request was made remains open'  -  so 3,300 open connections do not consume 3,300 worker threads, and SQL Server routinely holds far more connections than workers. The defensible statement is that concurrent *active* requests, not open connections, are what meet the 512 ceiling. Also, 3,300 is a per-process ceiling only if all 33 connection strings differ in more than the database name in ways the app actually opens concurrently; add the host/Hangfire storage pools to the count. Correct as reasoning, mislabelled if presented as two comparable numbers.

### A3.39  [partially]

**Claim.** MY REASONING, not a sourced fact: because pooled connections are reaped after 4-8 minutes of idleness when MinPoolSize is 0, and the three 15-minute recurring sweeps touch every office database once per cycle, each sweep almost always pays a full login and TLS handshake per office rather than reusing a warm connection. This cost scales linearly with office count and would double with a second processing server. It is invisible in application logs.

**Limit or threshold asserted.** Idle reaping 4-8 min vs 15-min sweep interval

- Source: Derived from Microsoft connection pooling documentation and the stated 15-minute job schedule
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The mechanism is correctly sourced and the 15-minute > 8-minute inference is valid for a database touched only by the sweep. Two qualifications: the reaping window is conditioned on LoadBalanceTimeout/Connection Lifetime being unset (default 0), which should be stated; and 'almost always' overreaches for any office database that also receives interactive request traffic inside the window, which will keep its pool warm. The TLS-handshake component depends on Encrypt being effectively true (see claim 33) and applies to new *physical* connections  -  which is exactly the case here, so the conclusion holds, but say 'new physical connection' rather than 'pooled connection establishment'.

### A3.40  [partially]

**Claim.** MY REASONING, not a sourced fact: the recurring-job scheduler's lock-timeout path logs at Debug level, so under a typical Information-level Serilog configuration a server that never wins the lock emits nothing. If the lock holder stalls, all twelve recurring jobs stop fleet-wide with no log line above Debug. This is the archetypal silent-degradation failure the team has stated it cannot detect.

**Limit or threshold asserted.** Log level Debug; lock timeout 1 minute

- Source: Derived from Hangfire RecurringJobScheduler source (log level Debug on DistributedLockTimeoutException)
- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/main/src/Hangfire.Core/Server/RecurringJobScheduler.cs>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The first half is correct and important: the timeout path is Debug-only, so at Information level a losing server is silent. The second half overstates. The source's own comment says the timeout normally means another server did the work, so silence is the expected healthy steady state, not evidence of stoppage  -  which is precisely why it is a poor signal, but it is not the same as 'all twelve jobs stop'. A stalled holder is a real but separate failure mode whose duration is bounded by the storage's lock expiry, not by the 1-minute acquisition timeout, and it is not established by this file. Rewrite as: the Debug-only log means neither the healthy nor the stalled case is distinguishable above Debug, so the absence of recurring-job execution is undetectable from logs  -  which is the actual gap, and is a stronger argument than the overstated one.

---

## Area: state-session

Verification verdict for this area: **minor-corrections** (32 claims checked)

### A4.1  [yes]

**Claim.** Microsoft explicitly warns that Redis does not persist by default and that this can cause Data Protection to issue new keys, invalidating previously protected data.

**Limit or threshold asserted.** Verbatim: 'When using Redis to persist data protection keys, be aware that Redis doesn't persist data by default when restarting. This can cause Data Protection to issue new keys, invalidating previously protected data.' Page ms.date 2025-11-07, updated 2026-02-11.

- Source: Microsoft Learn  -  Key storage providers in ASP.NET Core (aspnetcore-10.0)
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified

### A4.2  [partially]

**Claim.** Specifying an explicit key persistence location deregisters the default key-encryption-at-rest mechanism, so keys are no longer encrypted at rest; Microsoft recommends specifying an explicit encryption mechanism for production.

**Limit or threshold asserted.** Verbatim: 'If you specify an explicit key persistence location, the data protection system deregisters the default key encryption at rest mechanism, so keys are no longer encrypted at rest. It's recommended that you additionally specify an explicit key encryption mechanism for production deployments.'

- Source: Microsoft Learn  -  Key storage providers / Key encryption at rest
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0>
- Second source: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Substance is correct, but the text quoted as 'verbatim' is NOT the wording on the cited page. The quoted sentence ('...so keys are no longer encrypted at rest. It's recommended that you additionally specify...') appears on key-storage-providers, not key-encryption-at-rest. The key-encryption-at-rest page words it as two sentences with 'Consequently' and 'We recommend'. Attribute the quote to the correct page or use the correct wording.

### A4.3  [yes]

**Claim.** Deleting a Data Protection key permanently destroys all data protected by it; there is no emergency override, unlike revocation.

**Limit or threshold asserted.** Verbatim: 'At that point, all data protected by the key is permanently undecipherable, and there's no emergency override like there's with revoked keys. Deleting a key is truly destructive behavior.' And in default-settings: 'We recommend not deleting data protection keys.'

- Source: Microsoft Learn  -  Key management in ASP.NET Core
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0>
- Second source: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified

### A4.4  [yes]

**Claim.** Losing the Data Protection key ring logs every user out of an app using standard ASP.NET Core cookie authentication.

**Limit or threshold asserted.** Verbatim (slot-swap scenario): 'any app using Data Protection won't be able to decrypt stored data using the key ring inside the previous slot. This leads to users being logged out of an app that uses the standard ASP.NET Core cookie authentication, as it uses Data Protection to protect its cookies.'

- Source: Microsoft Learn  -  Data Protection key management and lifetime
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified

### A4.5  [partially]

**Claim.** Data Protection key lifetime is 90 days by default; new keys are created with activation now+2 days and expiry now+90 days; a replacement key is auto-persisted when the default key will expire within 2 days; minimum settable lifetime is 7 days; AutoGenerateKeys defaults to true.

**Limit or threshold asserted.** 90 days default; +2 days activation delay; 7 days minimum ('The default key lifetime cannot be shorter than 7 days'); source constants: KeyPropagationWindow 'currently fixed at 48 hours', KeyRingRefreshPeriod 'currently fixed at 24 hours', AutoGenerateKeys default true, NewKeyLifetime default 90 days with a 7-day floor throwing ArgumentOutOfRangeException.

- Source: Microsoft Learn  -  Key management; and dotnet/aspnetcore KeyManagementOptions.cs source
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0>
- Second source: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/DataProtection/DataProtection/src/KeyManagement/KeyManagementOptions.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The four documented facts are fully supported. The four SOURCE CONSTANTS are not on this page at all  -  the cited Learn URL contains no mention of KeyPropagationWindow, KeyRingRefreshPeriod, AutoGenerateKeys or ArgumentOutOfRangeException. I verified them independently in src/DataProtection/DataProtection/src/KeyManagement/KeyManagementOptions.cs: _keyPropagationWindow = TimeSpan.FromDays(2) with remark 'This value is currently fixed at 48 hours'; _keyRingRefreshPeriod = TimeSpan.FromHours(24) with remark 'currently fixed at 24 hours'; AutoGenerateKeys default true; _newKeyLifetime = TimeSpan.FromDays(90) with a `if (value < TimeSpan.FromDays(7)) throw new ArgumentOutOfRangeException` guard. All four are ACCURATE but must be cited to KeyManagementOptions.cs, not to the docs page. (Bonus fact the researcher missed: _maxServerClockSkew = TimeSpan.FromMinutes(5).)

### A4.6  [yes]

**Claim.** The key ring is cached in memory and the backing store is only checked approximately every 24 hours or when the current default key expires, whichever comes first  -  so the key store is not on the request hot path.

**Limit or threshold asserted.** Verbatim: 'When the data protection system initializes, it reads the key ring from the underlying repository and caches it in memory. This cache allows Protect and Unprotect operations to proceed without hitting the backing store. The system will automatically check the backing store for changes approximately every 24 hours or when the current default key expires, whichever comes first.'

- Source: Microsoft Learn  -  Key management, 'Automatic key ring refresh'
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified

### A4.7  [yes]

**Claim.** When the key store is briefly unreachable and a cached key ring already exists, ASP.NET Core extends the cached ring's lifetime by 2 minutes and rethrows the exception to the immediate caller only  -  so the symptom is roughly one failed request per 2-minute window, not an outage. On a cold start with no cached ring, all callers block and each retries until one succeeds.

**Limit or threshold asserted.** 2 minutes. Verbatim code comments: 'we'll create a new keyring object whose expiration is now + some short period of time (currently 2 min)'; 'The immediate caller should fail so that they can report the error up the chain.'; 'if there is no usable existing cached keyring, all callers must block until the keyring exists'; 'If we don't have an existing keyring (perhaps because this is the first call), then there's nothing to extend, so each subsequent caller will keep going down this code path until one succeeds.'

- Source: dotnet/aspnetcore source  -  KeyRingProvider.cs (read directly)
- URL: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/DataProtection/DataProtection/src/KeyManagement/KeyRingProvider.cs>
- Accessed: 2026-08-31
- Confidence: verified

### A4.8  [yes]

**Claim.** The Redis Data Protection provider stores the entire key ring as a single Redis LIST key with no TTL, using LRANGE/RPUSH equivalents.

**Limit or threshold asserted.** One key (configurable RedisKey; the app uses 'CaseEvaluation-Protection-Keys'); operations ListRange(_key) and ListRightPush(_key, element); no expiry set; no connection-failure handling in the repository class.

- Source: dotnet/aspnetcore source  -  RedisXmlRepository.cs
- URL: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/DataProtection/StackExchangeRedis/src/RedisXmlRepository.cs>
- Accessed: 2026-08-31
- Confidence: verified

### A4.9  [partially]

**Claim.** Redis AOF with the default appendfsync everysec can lose up to one second of writes; snapshotting alone is explicitly 'not very durable'; the shipped redis.conf defaults are appendonly no and appendfsync everysec.

**Limit or threshold asserted.** appendfsync everysec = 'fsync every second... you may lose 1 second of data if there is a disaster'; 'you can only lose one second worth of writes'; 'Snapshotting is not very durable.' redis.conf defaults: 'appendonly no', 'appendfsync everysec', '# maxmemory-policy noeviction', 'aof-load-truncated yes', save 3600/1 300/100 60/10000.

- Source: Redis Docs  -  Redis persistence; redis/redis redis.conf (8.0)
- URL: <https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/>
- Second source: <https://raw.githubusercontent.com/redis/redis/8.0/redis.conf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The three durability quotes are exact. The redis.conf DEFAULT LINES are not on this page and cannot be cited to it. The page never prints 'appendonly no', never prints the save 3600 1 / 300 100 / 60 10000 tuple (its only save example is the illustrative 'save 60 1000'), and never prints 'aof-load-truncated yes' as a config line  -  it only shows that string inside a sample log message ('AOF loaded anyway because aof-load-truncated is enabled'). Cite the shipped redis.conf itself (github.com/redis/redis/blob/<version>/redis.conf) for any default-value claim, and pin the version  -  redis.conf defaults are version-specific.

### A4.10  [yes]

**Claim.** Redis states that if you want data safety comparable to PostgreSQL you should use both AOF and RDB, and that an unclean shutdown can truncate the AOF tail (loaded anyway by default, discarding the last malformed command).

**Limit or threshold asserted.** Verbatim: 'The general indication you should use both persistence methods is if you want a degree of data safety comparable to what PostgreSQL can provide you.' And: 'the last command in the AOF could be truncated... AOF loaded anyway because aof-load-truncated is enabled.'

- Source: Redis Docs  -  Redis persistence
- URL: <https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/>
- Accessed: 2026-08-31
- Confidence: verified

### A4.11  [yes]

**Claim.** Redis's own documentation advises running two separate Redis instances rather than mixing caching and persistent keys in one instance.

**Limit or threshold asserted.** Verbatim: 'The volatile-lru, volatile-lrm, and volatile-random policies are mainly useful when you want to use a single Redis instance for both caching and for a set of persistent keys. However, you should consider running two separate Redis instances in a case like this, if possible.'

- Source: Redis Docs  -  Key eviction
- URL: <https://redis.io/docs/latest/develop/reference/eviction/>
- Accessed: 2026-08-31
- Confidence: verified

### A4.12  [partially]

**Claim.** Redis maxmemory defaults to 0 (no limit) on 64-bit systems, and the default eviction policy is noeviction, under which write commands error while reads continue to work.

**Limit or threshold asserted.** maxmemory 0 = unlimited, 'This is the default behavior for 64-bit systems, while 32-bit systems use an implicit memory limit of 3GB.' noeviction: 'Keys are not evicted but the server will return an error when you try to execute commands that cache new data... commands that only read existing data still work as normal.' redis.conf ships '# maxmemory-policy noeviction'.

- Source: Redis Docs  -  Key eviction; redis.conf
- URL: <https://redis.io/docs/latest/develop/reference/eviction/>
- Second source: <https://raw.githubusercontent.com/redis/redis/8.0/redis.conf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Two of three parts hold. The page NEVER states that noeviction is the default maxmemory-policy  -  it lists ten policies (noeviction, allkeys-lru, allkeys-lrm, allkeys-lfu, allkeys-random, volatile-lru, volatile-lrm, volatile-lfu, volatile-random, volatile-ttl) without designating a default, and the redis.conf line '# maxmemory-policy noeviction' does not appear. This matters: the 'memory pressure causes eviction not termination' requirement rests on noeviction being the shipped default, so cite the versioned redis.conf for it. Also note the page has been updated since the researcher's memory of it  -  allkeys-lrm/volatile-lrm (Least Recently Modified, new in Redis 8.6) are now listed, and the noeviction error condition is qualified with 'If your database uses replication then this condition only applies to the primary database.'

### A4.13  [yes]

**Claim.** A container that exceeds its Docker memory limit is killed by the kernel OOM killer by default.

**Limit or threshold asserted.** Verbatim: 'By default, if an out-of-memory (OOM) error occurs, the kernel kills processes in a container.'

- Source: Docker Docs  -  Runtime options with Memory, CPUs, and GPUs
- URL: <https://docs.docker.com/engine/containers/resource_constraints/>
- Accessed: 2026-08-31
- Confidence: verified

### A4.14  [yes]

**Claim.** Redis replication is asynchronous; acknowledged writes can still be lost during a failover, and min-replicas-to-write is only a best-effort mechanism.

**Limit or threshold asserted.** Verbatim: 'acknowledged writes can still be lost during a failover, depending on the exact configuration of the Redis persistence'; 'because Redis uses asynchronous replication it is not possible to ensure the replica actually received a given write, so there is always a window for data loss'; 'You may think of it as a best effort data safety mechanism'.

- Source: Redis Docs  -  Redis replication
- URL: <https://redis.io/docs/latest/operate/oss_and_stack/management/replication/>
- Accessed: 2026-08-31
- Confidence: verified

### A4.15  [yes]

**Claim.** Redis Sentinel requires at least three instances on independently-failing machines, does not guarantee retention of acknowledged writes, and is documented as breaking under Docker port remapping.

**Limit or threshold asserted.** Verbatim: 'You need at least three Sentinel instances for a robust deployment.'; 'Sentinel + Redis distributed system does not guarantee that acknowledged writes are retained during failures, since Redis uses asynchronous replication.'; 'Sentinel, Docker, or other forms of Network Address Translation or Port Mapping should be mixed with care: Docker performs port remapping, breaking Sentinel auto discovery of other Sentinel processes and the list of replicas for a master.'; 'please deploy at least three Sentinels in three different boxes always.'

- Source: Redis Docs  -  High availability with Redis Sentinel
- URL: <https://redis.io/docs/latest/operate/oss_and_stack/management/sentinel/>
- Accessed: 2026-08-31
- Confidence: verified

### A4.16  [yes]

**Claim.** A single-instance Redis lock is documented as acceptable only where an occasional race is tolerable; failover-based (primary+replica) Redis locking is an explicit safety violation; guaranteeing lock safety across restarts requires fsync=always, or a delayed restart longer than the max lock TTL.

**Limit or threshold asserted.** Verbatim: 'Client B acquires the lock to the same resource A already holds a lock for. SAFETY VIOLATION!'; 'this is actually a viable solution in applications where a race condition from time to time is acceptable'; 'In theory, if we want to guarantee the lock safety in the face of any kind of instance restart, we need to enable fsync=always'; 'Using delayed restarts it is basically possible to achieve safety even without any kind of Redis persistence'; also 'Redis is not using monotonic clock for TTL expiration mechanism' and a recommendation to implement fencing tokens.

- Source: Redis Docs  -  Distributed Locks with Redis (Redlock)
- URL: <https://redis.io/docs/latest/develop/clients/patterns/distributed-locks/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Verified in full. One nuance worth carrying into the design doc: immediately after the SAFETY VIOLATION passage the page adds 'Sometimes it is perfectly fine that, under special circumstances, for example during a failure, multiple clients can hold the lock at the same time. If this is the case, you can use your replication based solution.' So Redis frames failover-based locking as unsafe-for-correctness rather than categorically forbidden  -  which is exactly the distinction the capability requirement already draws.

### A4.17  [yes]

**Claim.** ABP's AbpDistributedCacheOptions.HideErrors defaults to true, so cache read/write failures are hidden and logged and ABP queries the original source instead; tenant-id prefixing of cache keys is a behaviour of the generic IDistributedCache<TCacheItem>, disableable per cache item with IgnoreMultiTenancy.

**Limit or threshold asserted.** HideErrors bool, default true; 'In the development environment, this option is disabled'; behaviour is to 'silently hide (and log) the error and query from the original source'; GlobalCacheEntryOptions default SlidingExpiration 20 minutes; 'automatically adds the current tenant id to the cache key'; IgnoreMultiTenancy attribute disables it.

- Source: ABP.IO Documentation  -  Distributed Caching
- URL: <https://abp.io/docs/latest/framework/fundamentals/caching>
- Second source: <https://github.com/abpframework/abp/blob/dev/docs/en/framework/fundamentals/caching.md>
- Accessed: 2026-08-31
- Confidence: verified

### A4.18  [yes]

**Claim.** Sharing a key ring between two processes requires the same application name AND the same version of the Data Protection API stack across the apps.

**Limit or threshold asserted.** Verbatim: 'To share protected payloads among apps: Configure SetApplicationName in each app with the same value. Use the same version of the Data Protection API stack across the apps.' Page updated 2026-07-22.

- Source: Microsoft Learn  -  Configure ASP.NET Core Data Protection, 'Set the application name'
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified

### A4.19  [partially]

**Claim.** On Linux with no cloud KMS, the only in-box cross-platform key-encryption-at-rest mechanism for the key ring is an X.509 certificate (ProtectKeysWithCertificate), with UnprotectKeysWithAnyCertificate for rotation; DPAPI and DPAPI-NG are Windows-only.

**Limit or threshold asserted.** DPAPI: 'Only applies to Windows deployments.' DPAPI-NG: 'available only on Windows 8/Windows Server 2012 or later.' X.509 and Azure Key Vault are the remaining in-box options; UnprotectKeysWithAnyCertificate accepts an array of certificates for rotation. Caveat noted in the docs  -  'Due to .NET Framework limitations, only certificates with CAPI private keys are supported'  -  is scoped to .NET Framework, not .NET 10 on Linux.

- Source: Microsoft Learn  -  Key encryption at rest; Configure ASP.NET Core Data Protection
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0>
- Second source: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Two defects. (a) UnprotectKeysWithAnyCertificate does NOT appear anywhere on this page. It is documented on configuration/overview ('## Unprotect keys with any certificate (UnprotectKeysWithAnyCertificate)'  -  'You can rotate certificates and decrypt keys at rest using an array of X509Certificate2 certificates'). Re-cite that half. (b) The claim that the CAPI-private-key caveat is 'scoped to .NET Framework, not .NET 10 on Linux' is the researcher's inference, not the page's statement. The sentence sits unqualified inside the X.509 section whose moniker range explicitly includes aspnetcore-10.0, and the page offers no Linux carve-out. This is load-bearing  -  the whole recommended Linux at-rest mechanism depends on it  -  so it must be settled by test (load a PFX and call ProtectKeysWithCertificate on .NET 10/Linux) rather than asserted. Also note the page title is 'Key encryption at rest in Windows and Azure', which is a signal it was never written to answer the Linux question.

### A4.20  [yes]

**Claim.** SQL Server transactions are fully durable by default: log records are persisted to disk before commit returns to the client, and DELAYED_DURABILITY defaults to DISABLED at database level.

**Limit or threshold asserted.** Verbatim: 'SQL Server transaction commits can be either fully durable, the SQL Server default, or delayed durable'; 'Durability is guaranteed on commit. Corresponding log records are persisted to disk before the transaction commit succeeds and returns control to the client.' DELAYED_DURABILITY DISABLED is the database-level default.

- Source: Microsoft Learn  -  Control Transaction Durability
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/logs/control-transaction-durability?view=sql-server-ver17>
- Accessed: 2026-08-31
- Confidence: verified

### A4.21  [partially]

**Claim.** OpenIddict issues JWT tokens by default; ASP.NET Core Data Protection token format is opt-in via UseDataProtection(). Therefore key-ring loss does not by itself invalidate issued OpenIddict access tokens  -  it invalidates cookies, antiforgery tokens, and Identity email-confirmation/password-reset tokens.

**Limit or threshold asserted.** JWT is the default; Data Protection is 'optionally configured'; identity tokens are always JWT. Marked partial because whether this specific deployment calls UseDataProtection() was not verifiable from the brief  -  it must be checked in source. If it does, refresh tokens and authorization codes are also in the key ring's blast radius.

- Source: OpenIddict documentation  -  Token formats / ASP.NET Core Data Protection integration
- URL: <https://documentation.openiddict.com/configuration/token-formats.html>
- Second source: <https://documentation.openiddict.com/integrations/aspnet-core-data-protection>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The two load-bearing facts (JWT is the default; Data Protection is optional) are confirmed verbatim. Two details are not settled by this page: (a) the method name UseDataProtection() never appears  -  the page only links out to a separate 'ASP.NET Core Data Protection integration' page, so cite that page for the API; (b) 'identity tokens are always JWT' is not stated. The only relevant sentence is about the typ header  -  'access tokens produced by OpenIddict 3.0+ are always issued with a "typ": "at+jwt" header while identity tokens still use "typ": "JWT" for backward compatibility'  -  which is about header values under the JWT format, not about whether identity tokens are exempt from the Data Protection format. The researcher's own caveat stands and is the right one: read the deployment's AddOpenIddict() configuration before relying on the blast-radius scoping.

### A4.22  [yes]

**Claim.** ASP.NET Core Identity's DataProtectorTokenProvider (email confirmation, password reset) has a default token lifespan of 1 day, bounding the blast radius of key-ring loss on in-flight email links to at most 24 hours of issued links.

**Limit or threshold asserted.** TimeSpan.FromDays(1). Marked partial because ABP or this application may override TokenLifespan; that must be checked in source before relying on the 24-hour bound.

- Source: Microsoft Learn  -  DataProtectionTokenProviderOptions.TokenLifespan
- URL: <https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.dataprotectiontokenprovideroptions.tokenlifespan>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Documented default confirmed. The researcher's caveat is correct and should be kept: ABP or the app may override TokenLifespan, so the 24-hour bound must be confirmed in source before it is relied on.

### A4.23  [yes]

**Claim.** HIPAA requires a contingency plan with Required implementation specifications for a data backup plan ('retrievable exact copies of electronic protected health information'), a disaster recovery plan ('procedures to restore any loss of data'), and an emergency mode operation plan; testing and revision of contingency plans is Addressable.

**Limit or threshold asserted.** 164.308(a)(7)(ii)(A) Required; (B) Required; (C) Required; (D) Addressable; (E) Addressable.

- Source: 45 CFR 164.308(a)(7) (Cornell LII)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.308>
- Accessed: 2026-08-31
- Confidence: verified

### A4.24  [partially]

**Claim.** HIPAA requires an emergency access procedure (164.312(a)(2)(ii), Required); encryption/decryption of ePHI is Addressable (164.312(a)(2)(iv)); documentation must be retained six years (164.316(b)(2)(i), Required).

**Limit or threshold asserted.** 164.312(a)(2)(ii) Required: 'Establish (and implement as needed) procedures for obtaining necessary electronic protected health information during an emergency.' 164.312(a)(2)(iv) Addressable. 164.316(b)(2)(i) Required: 'Retain the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later.'

- Source: 45 CFR 164.312 and 164.316 (Cornell LII)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.312>
- Second source: <https://www.law.cornell.edu/cfr/text/45/164.316>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: CITATION ERROR. The two 164.312 items are exactly right, but 164.316(b)(2)(i)  -  the six-year retention requirement  -  is not on this page and cannot be. 45 CFR 164.312 is 'Technical safeguards'; 164.316 is 'Policies and procedures and documentation requirements', a separate section at a separate URL (<https://www.law.cornell.edu/cfr/text/45/164.316>). Split the citation. Also worth adding for completeness: (a)(2)(i) Unique user identification is Required and (a)(2)(iii) Automatic logoff is Addressable.

### A4.25  [partially]

**Claim.** The Data Protection key ring is not ePHI, so HIPAA's Required data-backup specification does not directly mandate backing it up; the specifications it actually engages are emergency access (164.312(a)(2)(ii)) and emergency mode operation (164.308(a)(7)(ii)(C)), because losing it denies all authorised users access to the system.

**Limit or threshold asserted.** n/a  -  this is a scoping judgement, stated explicitly to avoid the over-claim that HIPAA mandates key-ring backup. The operational conclusion (back it up) does not depend on the regulatory hook.

- Source: My reading of 45 CFR 164 as applied to this system
- URL: <https://www.law.cornell.edu/cfr/text/45/164.308>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Correctly self-labelled as a scoping judgement, and it is a defensible one  -  the regulation nowhere defines key material as ePHI, and 164.308(a)(7)(ii)(A) speaks only of 'retrievable exact copies of electronic protected health information'. Two notes. (a) The 164.312(a)(2)(ii) half of the claim is not on the cited 164.308 URL; it needs the 164.312 citation. (b) The judgement is arguable in the opposite direction and should be presented as contested, not settled: a key ring whose loss renders all stored ePHI permanently undecipherable is plausibly within the reach of (a)(7)(ii)(A)'s 'retrievable' and (B)'s 'procedures to restore any loss of data', since an unreadable copy is not a retrievable one. The researcher's own framing  -  that the operational conclusion does not depend on which hook you pick  -  is the safe way to write this.

### A4.26  [partially]

**Claim.** The 2025 HIPAA Security Rule NPRM  -  which would remove the Required/Addressable distinction and mandate encryption and MFA  -  is still not final as of August 2026, with final action reportedly pushed to July 2027.

**Limit or threshold asserted.** NPRM published in the Federal Register 6 January 2025; ~4,745 comments; OCR's agenda listed final action May 2026, which passed without publication; OMB now shows July 2027. Marked partial: secondary sources only, primary Federal Register and reginfo.gov entries were not fetched. Design implication: plan for encryption-at-rest of key material as if it will become Required, but do not cite it as current law.

- Source: HIPAA Journal / OMB unified agenda reporting
- URL: <https://www.hipaajournal.com/hipaa-security-rule-update-postponed/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Publication date, the May 2026 -> July 2027 slip, and not-yet-final status all confirmed. The comment count is not: the page says 'almost 5,000', not the precise 4,745 the claim states. Either soften to 'nearly 5,000' or source 4,745 from regulations.gov. The 'secondary sources only' caveat remains correct and important  -  this is a trade publication reading the OMB/reginfo.gov entry, so the design doc's framing ('plan for it, do not cite it as current law') is the right one and should be kept.

### A4.27  [yes]

**Claim.** Microsoft's multitenancy guidance for caches states that in a shared cache the application alone is responsible for tenant separation, and that cache contents are not encrypted in memory  -  recommending application-level encryption before writing sensitive data to a cache.

**Limit or threshold asserted.** Verbatim: 'your application is solely responsible for keeping tenant data separate'; 'The service doesn't encrypt data stored in memory. If your tenants have strict data protection requirements, consider implementing application-level encryption before writing sensitive data to the cache.' Page ms.date 2026-04-08. Cited as evidence of what this class of component does, not as a product recommendation.

- Source: Azure Architecture Center  -  Managed Redis considerations for multitenancy
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/service/managed-redis>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Verified, including the ms.date. The researcher's framing note is also correct and should be preserved: this is cited as evidence of what an in-memory shared cache does as a class, not as a recommendation to adopt the named service.

### A4.28  [yes]

**Claim.** The Data Protection Redis provider package is current and actively released: Microsoft.AspNetCore.DataProtection.StackExchangeRedis 10.0.11 published 2026-08-11, with monthly servicing releases.

**Limit or threshold asserted.** 10.0.11 (2026-08-11), 10.0.10 (2026-07-14), 10.0.9 (2026-06-09)  -  well inside a 12-month recency bar.

- Source: NuGet  -  Microsoft.AspNetCore.DataProtection.StackExchangeRedis
- URL: <https://www.nuget.org/packages/Microsoft.AspNetCore.DataProtection.StackExchangeRedis/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Material claim confirmed  -  10.0.11 exists and was published 2026-08-11, well inside a 12-month recency bar, and the unbroken 10.0.0-10.0.11 line establishes the monthly cadence. Minor: the listing page itself surfaces 7/14/2026 and 6/9/2026 against 11.0.0-preview.6 and 11.0.0-preview.5; the researcher appears to have read those dates across to 10.0.10 and 10.0.9. Those dates are consistent with ASP.NET Core's same-day servicing cadence but were not individually shown, so drop the two secondary version-date pairs or confirm them from the version table.

### A4.29  [yes]

**Claim.** Medallion DistributedLock is actively maintained, with a recent release (2.8.3) and both a Redis provider (DistributedLock.Redis 1.1.1) and a SQL Server provider (DistributedLock.SqlServer 1.0.7) behind the same interface.

**Limit or threshold asserted.** Latest release 2.8.3 (15 July). Marked partial: the release year was not resolvable from the fetched page and the GitHub API was not reachable from this session (repo not in the allowed set). Verify the exact release date before relying on the recency claim; the existence of the SqlServer provider behind IDistributedLockProvider is not in doubt.

- Source: madelson/DistributedLock releases
- URL: <https://github.com/madelson/DistributedLock/releases>
- Second source: <https://github.com/madelson/DistributedLock>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: UPGRADE from partial to verified  -  the blocker the researcher hit is resolved. The release year is 2026, not 2024: DistributedLock 2.8.3 shipped 2026-07-15, roughly six weeks before today, so the recency claim is comfortably sound. Both provider versions are confirmed and current (Redis 1.1.1 2025-10-26; SqlServer 1.0.7 2026-02-14), the repo is not archived, and the SqlServer provider behind IDistributedLockProvider is real. Note the researcher used a page whose relative dates omit the year and could not reach the GitHub API; the NuGet v3 registration index (api.nuget.org/v3/registration5-semver1/<id>/index.json) resolves absolute publish dates without GitHub access and should be the default tool for this kind of recency check.

### A4.30  [partially]

**Claim.** Redis licensing differs sharply by version: 7.2 and earlier are BSD-3-Clause; 7.4.x-7.8.x are RSALv2/SSPLv1 (source-available, not open source); 8.0 and later add AGPLv3 as a tri-licence option. Valkey (Linux Foundation fork from Redis 7.2.4) remains BSD.

**Limit or threshold asserted.** 'Redis 7.2.x of Redis Open Source and earlier versions remain subject to the BSD3 license.' 'Redis Community Edition 7.4.x to 7.8.x remain subject to the dual RSALv2/SSPLv1 license.' 'Redis 8 in Redis Open Source and later versions are available under our tri-license... RSALv2, SSPLv1, and AGPLv3.' Valkey licence and Linux Foundation governance corroborated separately.

- Source: Redis  -  Licenses
- URL: <https://redis.io/legal/licenses/>
- Second source: <https://valkey.io/topics/license/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The three Redis licence tiers are confirmed verbatim on the cited page. The Valkey sentence is not  -  redis.io/legal/licenses says nothing about Valkey, its BSD licence, its Linux Foundation governance, or the 7.2.4 fork point, and the claim's own '(corroborated separately)' does not name that separate source. Attach a real citation (valkey.io or the LICENSE file in the valkey-io/valkey repo) or drop the sentence. Flagging it also because the Valkey mention is the one place in this claim set that drifts from 'here is what the licence says' toward 'here is an alternative product', which is the line the neutrality rule polices.

### A4.31  [yes]

**Claim.** StackExchange.Redis defaults AbortOnConnectFail to true (except Azure endpoints), so a startup-time Redis outage throws rather than returning a disconnected multiplexer; ConnectTimeout and SyncTimeout default to five seconds; the library does not retry commands automatically.

**Limit or threshold asserted.** 'The default is true, except for Azure endpoints, where it defaults to false.' 'By default, the timeout is five seconds for ConnectTimeout and SyncTimeout'. ConnectRetry default 3. 'StackExchange.Redis doesn't provide an automated retry mechanism for commands.'

- Source: Redis Docs  -  .NET client production usage
- URL: <https://redis.io/docs/latest/develop/clients/dotnet/produsage/>
- Accessed: 2026-08-31
- Confidence: verified

### A4.32  [partially]

**Claim.** Redis is not, at this system's data volumes, carrying a cache workload that would justify it on performance grounds; its two real jobs are holding the key ring and acting as a cross-process coherence bus between exactly two processes (API and AuthServer). Removing the shared cache would leave permission and setting changes made in one process invisible to the other until restart.

**Limit or threshold asserted.** n/a  -  architectural judgement, not a sourced fact. The underlying ABP mechanism (removal of a distributed cache entry on permission/setting change, visible to all processes sharing the cache) is documented cache-aside behaviour; the conclusion about this system's scale is mine and should be argued with.

- Source: My reasoning from the measured system facts (16 appointments, 11 offices, two processes, ABP cache-aside semantics)
- URL: <https://abp.io/docs/latest/framework/fundamentals/caching>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The architectural judgement is honestly self-labelled and is fine to argue. But the one sentence presented as sourced  -  'the underlying ABP mechanism (removal of a distributed cache entry on permission/setting change, visible to all processes sharing the cache) is documented cache-aside behaviour'  -  is NOT documented on the cited page. I read the full page: it covers the cache abstraction and its options, and says nothing about permission or setting invalidation or cross-process coherence. Cite the Permission Management / Setting Management module docs, or better, verify against PermissionStore/SettingStore cache-invalidation code in the ABP source, since the whole 'keep the shared cache' argument turns on this mechanism actually existing and firing across process boundaries. Until then it should carry the same 'my judgement, argue with it' label as the rest of the claim.

---

## Area: availability-dr

Verification verdict for this area: **minor-corrections** (34 claims checked)

### A5.1  [yes]

**Claim.** 45 CFR 164.308(a)(7) Contingency plan makes three implementation specifications (Required): (A) Data backup plan  -  'Establish and implement procedures to create and maintain retrievable exact copies of electronic protected health information.'; (B) Disaster recovery plan  -  'Establish (and implement as needed) procedures to restore any loss of data.'; (C) Emergency mode operation plan. Two are (Addressable): (D) Testing and revision procedures  -  'Implement procedures for periodic testing and revision of contingency plans.'; (E) Applications and data criticality analysis.

**Limit or threshold asserted.** (A),(B),(C) Required; (D),(E) Addressable

- Source: eCFR, 45 CFR 164.308 (retrieved via eCFR versioner API, XML, as in force 2026-08-01)
- URL: <https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.308>
- Second source: <https://www.hhs.gov/hipaa/for-professionals/security/laws-regulations/index.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All five implementation specifications and their Required/Addressable designations are exact. Note on access: the ecfr.gov HTML URL 302-redirects bot traffic to unblock.federalregister.gov; I confirmed the text via the official eCFR renderer API for the identical section and independently via Cornell LII. The URL is correct and works in a browser.

### A5.2  [yes]

**Claim.** 'Addressable' is not optional. 45 CFR 164.306(d)(3) requires the entity to assess whether the specification is reasonable and appropriate and either implement it, or 'Document why it would not be reasonable and appropriate to implement' it and 'Implement an equivalent alternative measure if reasonable and appropriate.' 164.306(b)(2) lists size, complexity and capabilities of the entity, and cost, as factors that may properly be weighed  -  which is the clause a two-person team relies on to justify a proportionate design.

**Limit or threshold asserted.** 164.306(d)(3)(ii)(B)(1)-(2); factors at 164.306(b)(2)(i)-(iv)

- Source: eCFR, 45 CFR 164.306 Security standards: General rules
- URL: <https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.306>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Subparagraph numbering 164.306(d)(3)(ii)(B)(1)-(2) and factors at (b)(2)(i)-(iv) are both exact. Worth adding for completeness: the four factors also include (ii) technical infrastructure, hardware and software security capabilities and (iv) the probability and criticality of potential risks - (iv) cuts against a proportionality argument as often as (i) and (iii) cut for it, so a two-person team's cost defence is not unconditional.

### A5.3  [yes]

**Claim.** The brief's premise that '[a] six-year retention expectation applies (HIPAA 45 CFR 164.316(b)(2)(i))' is a misreading and should be corrected before it drives backup sizing. That paragraph reads: 'Retain the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later.' Paragraph (b)(1) is policies and procedures, and written records of actions/activities/assessments the Security Rule requires to be documented. It is a documentation-retention rule. It is not a retention period for ePHI, audit logs, or database backups. HIPAA sets no ePHI retention period at all.

**Limit or threshold asserted.** 6 years, applies to documentation under 164.316(b)(1) only

- Source: eCFR, 45 CFR 164.316 Policies and procedures and documentation requirements
- URL: <https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.316>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None - this is a correct catch and the strongest single finding in the set. The 6 years attaches only to (b)(1) documentation. The section also carries two further Required specs the brief should not overlook: (ii) Availability (make documentation available to those responsible for implementing the procedures) and (iii) Updates (review periodically and update in response to environmental or operational changes) - the latter is what turns a DR plan into a maintained artefact rather than a one-off.

### A5.4  [yes]

**Claim.** The actual retention driver for a California workers' compensation IME platform is state regulation, not HIPAA. 8 CCR  39.5: 'All QMEs shall retain a copy of all comprehensive medical-legal reports completed by the QME for a period of five years from the date of each evaluation report,' and an electronic copy satisfies this 'as long as the electronic copy retained is a true and correct copy of the original, showing the QME signature.' The same section also requires that, on written request, original radiological films, imaging studies and original medical records be returned to whoever supplied them or to the injured worker.

**Limit or threshold asserted.** 5 years from date of each evaluation report

- Source: California Department of Industrial Relations, Title 8 CCR  39.5, Records, Retention by QMEs
- URL: <https://www.dir.ca.gov/t8/39_5.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; five years and the electronic-copy and originals-return provisions all check out. Two refinements for backup sizing: (a) the electronic-copy allowance is conditioned on the copy being the one 'that was served on the parties', which is a stronger fidelity requirement than merely retaining a PDF - the served artefact and its signature must be preserved, not regenerated; (b) the same section imposes a duty to submit reports to the Medical Director on request, which is a retrieval-latency requirement on the archive, not just a retention-period one.

### A5.5  [yes]

**Claim.** NIST SP 800-34 Rev. 1 definitions, verbatim: MTD  -  'The MTD represents the total amount of time the system owner/authorizing official is willing to accept for a mission/business process outage or disruption and includes all impact considerations.' RTO  -  'RTO defines the maximum amount of time that a system resource can remain unavailable before there is an unacceptable impact on other system resources, supported mission/business processes, and the MTD.' RPO  -  'The RPO represents the point in time, prior to a disruption or system outage, to which mission/business process data can be recovered ... Unlike RTO, RPO is not considered as part of MTD.' The publication also states 'the RTO must normally be shorter than the MTD.'

**Limit or threshold asserted.** n/a  -  definitions, not thresholds

- Source: NIST Special Publication 800-34 Rev. 1, Contingency Planning Guide for Federal Information Systems (Ch. 3, BIA)
- URL: <https://nvlpubs.nist.gov/nistpubs/legacy/sp/nistspecialpublication800-34r1.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All four quotations are verbatim from Section 3.2 (p. 15 of the PDF). One caution if these definitions are reused: the Appendix ISCP template on a later page states RPO slightly differently - 'to which mission/business process data must be recovered' rather than 'can be recovered' - so quote the Section 3.2 wording, which is the one reproduced here.

### A5.6  [yes]

**Claim.** NIST SP 800-34 Rev. 1 Table 3-2 maps FIPS 199 availability impact to strategy: Low -> 'Backup: Tape backup / Strategy: Relocate or Cold site'; Moderate -> 'Backup: Optical backup, WAN/VLAN replication / Strategy: Cold or Warm site'; High -> 'Backup: Mirrored systems and disc replication / Strategy: Hot site'. Section 3.5.4 ties exercise rigour to the same axis: 'For low-impact systems, a tabletop exercise ... is sufficient'; 'For moderate-impact systems, a functional exercise ... Exercise procedures should be developed to include an element of system recovery from backup media'; 'For high-impact systems, a full-scale functional exercise ... should include a system failover to the alternate location.'

**Limit or threshold asserted.** Moderate availability -> cold or warm site; functional exercise including recovery from backup media

- Source: NIST SP 800-34 Rev. 1, 3.4.1, Table 3-2, 3.5.3-3.5.4, Table 3-6
- URL: <https://nvlpubs.nist.gov/nistpubs/legacy/sp/nistspecialpublication800-34r1.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; Table 3-2 rows and the 3.5.4 bullets are exact, and the section number 3.5.4 ('TT&E Program Summary') is correctly assigned - the low/moderate/high bullets fall under 3.5.4, not under 3.5.3 ('Exercises') where the exercise types are defined. The framing sentence immediately above the bullets is worth quoting too because it is the one that authorises keying all of this to availability specifically: 'The depth and rigor of ISCP TT&E activities increases with the FIPS 199 availability security objective.' Also note Table 3-2's own caption calls these 'Examples', so it is illustrative guidance, not a mandate.

### A5.7  [yes]

**Claim.** FIPS 199 defines AVAILABILITY as 'Ensuring timely and reliable access to and use of information...' and sets MODERATE impact as a 'serious adverse effect', amplified as effects that 'cause a significant degradation in mission capability to an extent and duration that the organization is able to perform its primary functions, but the effectiveness of the functions is significantly reduced ... or (iv) result in significant harm to individuals that does not involve loss of life or serious life threatening injuries.' HIGH is reserved for 'severe or catastrophic adverse effect'.

**Limit or threshold asserted.** Moderate = serious adverse effect, explicitly excluding loss of life / life-threatening injury

- Source: FIPS PUB 199, Standards for Security Categorization of Federal Information and Information Systems
- URL: <https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.199.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None of substance. Trivial fidelity point: the glossary rendering of the availability definition ends with a period, not an ellipsis - 'AVAILABILITY: Ensuring timely and reliable access to and use of information. [44 U.S.C., SEC. 3542]' - so the trailing '...' in the claim is the researcher's, not the document's. HIGH as 'severe or catastrophic adverse effect' is confirmed in the categorisation table.

### A5.8  [partially]

**Claim.** A scheduling platform for IME appointments plausibly categorises as FIPS 199 MODERATE for availability and HIGH for confidentiality. Unavailability delays legally-scheduled evaluations and degrades the practices' effectiveness but does not involve loss of life; the FIPS 199 MODERATE amplification language ('significant harm to individuals that does not involve loss of life or serious life threatening injuries') fits. This categorisation is the single load-bearing judgement behind rejecting rungs 4-6, so it should be made explicitly and by the business owner, not assumed by engineers.

- Source: My architectural judgement applying FIPS 199 to this system
- URL: <https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.199.pdf>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The DOCUMENT supports the quoted amplification language but cannot support the categorisation itself, which is a judgement no standard can settle - the claim is right to say so and right to push it to the business owner. Two additions make it defensible rather than merely plausible. First, state the per-objective high-water-mark rule quoted above: it is what lets availability=MODERATE coexist with confidentiality=HIGH without the system becoming a 'HIGH-impact system' for contingency-planning purposes, and it is the precise reason SP 800-34's moderate-tier guidance applies. Second, FIPS 199 requires INTEGRITY to be rated too - 'the value of not applicable cannot be assigned to any security objective' - and the claim omits it. For a platform whose outbox tables can re-send to external parties on restore (claim [33]), integrity is not obviously MODERATE and should be rated explicitly rather than left blank.

### A5.9  [yes]

**Claim.** SQL Server recovery models: only FULL supports point-in-time restore. Microsoft's table gives Simple: 'Can recover only to the end of a backup'; Full: work loss 'Normally none' and 'Can recover to a specific point in time'; Bulk-logged: 'Point-in-time recovery isn't supported.' The Simple model explicitly cannot be used with log shipping, availability groups/mirroring, or point-in-time restores. Note: 'SQL Server Enterprise and Standard editions use the full recovery model by default, while SQL Server Express edition uses the simple recovery model by default.'

**Limit or threshold asserted.** PITR requires FULL recovery model + unbroken log chain

- Source: Microsoft Learn  -  Recovery Models (SQL Server)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/recovery-models-sql-server>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; every quoted cell and the edition-defaults note are verbatim. Two small completions: the Simple-model exclusion list has a fourth entry the claim drops, 'Media recovery without data loss', which is directly relevant to a DR argument; and the Full row's work-loss cell is conditional in full - 'Normally none. If the tail of the log is damaged, changes since the most recent log backup must be redone' - which is the clause that makes tail-log backup capability a requirement rather than a nicety.

### A5.10  [yes]

**Claim.** THE LOG TRAP. Microsoft: 'If a transaction log is never truncated, it eventually fills all the disk space allocated to physical log files.' Under full recovery, 'truncation occurs after a log backup'. Critically: 'When you first create a database that uses the full recovery model, the transaction log is reused as needed (similar to a database using the simple recovery model), up until the time you create a full database backup.' The stall is diagnosable as sys.databases.log_reuse_wait_desc = 'LOG_BACKUP'  -  'A log backup is required before the transaction log can be truncated.'

**Limit or threshold asserted.** log_reuse_wait value 2 = LOG_BACKUP

- Source: Microsoft Learn  -  The Transaction Log (SQL Server), Transaction log truncation / Factors that can delay log truncation
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/logs/the-transaction-log-sql-server>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; all three quotations and the numeric code 2 are exact. One precision worth carrying into the runbook: the page's full statement of full-recovery truncation is conditional - 'if a checkpoint has occurred since the previous backup, truncation occurs after a log backup (unless it's a copy-only log backup)'. The copy-only exclusion matters operationally, because a monitoring script that takes copy-only log backups will not relieve the condition it is monitoring.

### A5.11  [partially]

**Claim.** Applying that to this system: office databases are created by EF Core Database.MigrateAsync() and inherit the model database's recovery model, which on Developer edition (all Enterprise functionality) is FULL. They are therefore in the 'pseudo-simple' grace state today. The first successful BACKUP DATABASE ends that grace state permanently, and with no log backups scheduled the log grows until the volume  -  which has roughly 9.7 GB free  -  fills, producing error 9002 and read-only databases. Turning the existing backup script on reliably is the action that breaks the box.

**Limit or threshold asserted.** ~9.7 GB free disk stated in brief; recovery model not directly observed by me

- Source: My inference chaining the Microsoft transaction-log and recovery-model documentation onto the stated system facts
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/logs/the-transaction-log-sql-server>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The grace-state mechanism is exactly right and correctly cited; three links in the chain are not established by the cited page. (1) Developer edition's default recovery model is nowhere stated - the recovery-models page names only Enterprise/Standard (full) and Express (simple), and 'Developer includes all the functionality of Enterprise edition' is a feature statement, not a defaults statement. Inheritance from model is likewise not stated on either page. Settle this by observing SELECT name, recovery_model_desc FROM sys.databases before relying on it. (2) The read-only outcome is real but documented elsewhere - the error 9002 page states 'If the log fills while the database is online, the database remains online but can only be read, not updated' (and marks the database RESOURCE PENDING if the log fills during recovery instead). (3) 'Grows until the volume fills' presumes unlimited autogrow; the same page lists 'Log size is set to a fixed maximum value or autogrow is disabled' as an independent 9002 cause, so the failure may arrive at a configured MAXSIZE well before 9.7 GB. The ~9.7 GB free figure is from the brief and I could not verify it.

### A5.12  [yes]

**Claim.** Documented single-database point-in-time restore sequence: 'As a prerequisite to a point-in-time restore, you must first restore a full database backup whose endpoint is earlier than your target restore time'; 'In every RESTORE LOG statement of the restore sequence, you must specify your target time or transaction in an identical STOPAT clause'; recommendations are 'Use STANDBY to find unknown point in time' and 'Specify the point in time early in a restore sequence'. A tail-log backup is taken first where required. If the log backup does not contain the requested time 'a warning is generated and the database remains unrecovered.'

**Limit or threshold asserted.** identical STOPAT on every RESTORE LOG

- Source: Microsoft Learn  -  Restore a SQL Server Database to a Point in Time (Full Recovery Model)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-a-sql-server-database-to-a-point-in-time-full-recovery-model>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; all five elements verify verbatim, including both Recommendations headings and the tail-log step ('Take tail-log backup before restore will be selected if it is necessary for the point in time that you have selected'). Add one caveat the page flags in an Important box and the claim omits: 'Under the bulk-logged recovery model, if a log backup contains bulk-logged changes, point-in-time recovery is not possible to a point within that backup' - relevant because index rebuilds under bulk-logged silently forfeit PITR for that window.

### A5.13  [yes]

**Claim.** SQL Server offers NO automatic cross-database point-in-time consistency. The recovery-models page notes: 'If you have two or more related databases in the full recovery model that must be logically consistent, you might have to implement special procedures.' The documented mechanism is marked transactions: 'If a marked transaction spans multiple databases on the same database server or on different servers, the marks must be recorded in the logs of all the affected databases', restored with WITH STOPATMARK / STOPBEFOREMARK. This requires the application to issue BEGIN TRANSACTION ... WITH MARK spanning the databases.

**Limit or threshold asserted.** marks must exist in the logs of all affected databases

- Source: Microsoft Learn  -  Recover related databases with marked transaction
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/recovery-of-related-databases-that-contain-marked-transaction>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. The companion quote from the recovery-models page ('If you have two or more related databases in the full recovery model that must be logically consistent, you might have to implement special procedures') is also verbatim. Two operational details the claim could add: marks are recorded in msdb..logmarkhistory on EACH server, so cross-server recovery requires reading that table on every instance; and the page notes 'recovering to a mark is disallowed when the database is undergoing operations that are bulk-logged', the same restriction that limits ordinary PITR.

### A5.14  [partially]

**Claim.** Marked transactions are unavailable to this system as built. The brief states there is no raw SQL anywhere in src, no cross-database queries, and host/tenant data are joined in memory. Marked transactions require raw T-SQL spanning the host and tenant databases in one transaction. Therefore the only route to a cross-database consistent recovery point without a code change is instance-wide snapshot coordination.

- Source: My inference from the stated system facts against the Microsoft marked-transaction documentation
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/recovery-of-related-databases-that-contain-marked-transaction>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The documentary half holds: marks are created only by BEGIN TRANSACTION ... WITH MARK, so they cannot arise from EF Core's generated SQL without deliberate T-SQL. The codebase premises (no raw SQL in src, no cross-database queries, in-memory joins) are the brief's, not the document's, and I cannot verify them. The word 'only' in the conclusion is too strong on the evidence given: T-SQL snapshot backup is one route, but so are (a) quiescing writes at the application layer for the duration of a coordinated backup round, and (b) storage-level crash-consistent snapshots of all volumes, which give a consistent point without SQL Server's cooperation at the cost of crash-recovery on restore. Recommend rewording to 'the only route that does not require an application change or a write freeze imposed outside the database'.

### A5.15  [yes]

**Claim.** SQL Server 2022 T-SQL snapshot backup can freeze write I/O across all user databases at once and record one consistent recovery point: 'ALTER SERVER CONFIGURATION SET SUSPEND_FOR_SNAPSHOT_BACKUP = ON;' then 'BACKUP SERVER TO DISK = ... WITH METADATA_ONLY, FORMAT;'. Microsoft: 'write operations are paused on SQL Server (read requests are still allowed), and control is handed over to the backup application to complete the snapshot.' It supports point-in-time recovery 'using log backups taken with the normal streaming approach after the snapshot FULL backup'. HARD LIMIT: 'The maximum number of databases you can back up with this feature is 64.' System databases (master, model, msdb) cannot be suspended. Available in every edition per the 2022 editions table ('Snapshot backup: Yes' across Enterprise/Standard/Web/Express).

**Limit or threshold asserted.** 64 databases maximum; error Msg 925 above that; system databases excluded

- Source: Microsoft Learn  -  Create a Transact-SQL snapshot backup
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/create-a-transact-sql-snapshot-backup>
- Second source: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; syntax, the pause statement, the 64-database limit with Msg 925, system-database exclusion and PITR support ('Perform point-in-time recovery using log backups taken with the normal streaming approach after the snapshot FULL backup') all verify. The edition claim, which the claim transparently attributes to the 2022 editions table rather than this page, is also correct: Snapshot backup is Yes across Enterprise, Standard, Web, Express with Advanced Services and Express. Two design-relevant details omitted: SUSPEND_FOR_SNAPSHOT_BACKUP clears the differential bitmap by default (use MODE = COPY_ONLY to avoid invalidating subsequent differentials), and a restore requires copying the database files from the snapshot URI to the mount point BEFORE issuing RESTORE - the .bkm file is metadata only, so the restore runbook has a manual storage step that the backup runbook does not.

### A5.16  [yes]

**Claim.** SQL Server 2022 can back up and restore directly to any S3-compatible object store over the REST API using s3:// URLs, and it is edition-gated: 'Backup and restore to S3-compatible object storage over REST API' is Yes for Enterprise, Standard and Web, No for Express. TLS is mandatory: 'URLs beginning with s3:// always assume that the underlying protocol is https' and 'Back up to S3-compatible object storage with a nonsecure http URL isn't supported.' On Linux 'the CA must be placed on a predefined location to be created at /var/opt/mssql/security/ca-certificates, only the first 50 certificates can be stored ... The CA must be in place before SQL Server process is started.' Buckets 'can't be created or configured from SQL Server 2022'. Limits: 10,000 parts per file, MAXTRANSFERSIZE 5-20 MB (default 10 MB), 'A single backup file can be up to 200,000 MiB per URL (with MAXTRANSFERSIZE set to 20 MB)', striping across a maximum of 64 URLs, path or virtual-host style, total URL length 259 characters. RESTORE VERIFYONLY and STOPAT/STOPATMARK are supported over S3; NOINIT/INIT (appending) is not  -  use WITH FORMAT.

**Limit or threshold asserted.** 10,000 parts x MAXTRANSFERSIZE 5-20 MB; 64 URLs; 200,000 MiB/URL; 50 CA certs on Linux; URL <=259 chars

- Source: Microsoft Learn  -  SQL Server back up to URL for S3-compatible object storage
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/sql-server-backup-to-url-s3-compatible-object-storage>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every stated limit is exact, and the edition gating is confirmed by the editions table (Yes/Yes/Yes for Enterprise/Standard/Web, No for both Express variants). Three omissions worth adding, one of them consequential. (1) CONSEQUENTIAL: 'The use of COMPRESSION is required in order to change MAXTRANSFERSIZE values' - so the 200,000 MiB/URL ceiling presupposes a compressed backup, and compression is Enterprise/Standard-only, making that ceiling unreachable on Web edition. (2) The page recommends staying under 200 characters of URL, not 259: 'the usable limit is 254 characters. However, we recommend sticking to a limit of 200 characters.' (3) Failed or cancelled backups leave uncommitted multipart data in the bucket that SQL Server does not clean up - 'aren't removed if there are backup failures' - which interacts badly with an object-lock retention policy and with storage-cost monitoring.

### A5.17  [partially]

**Claim.** SQL Server 2022 edition limits that bind an availability design for database-per-tenant. Always On availability groups: Enterprise only. Basic availability groups: Standard only, and the documented limitations are 'Limit of two replicas', 'No read access on secondary replica', 'No backups on secondary replica', 'No integrity checks on secondary replicas', 'Support for one availability database', and 'Basic availability groups can't be part of a distributed availability group'  -  though 'You might have multiple Basic availability groups connected to a single instance'. Always On failover cluster instances: Standard supports two nodes. Log shipping: Enterprise/Standard/Web yes, Express no. Backup compression and Encrypted backup: Enterprise/Standard only. Express: 10 GB maximum relational database size, 1,410 MB buffer pool, no SQL Server Agent.

**Limit or threshold asserted.** Basic AG = 2 replicas, 1 database; Standard FCI = 2 nodes; Standard buffer pool 128 GB / 24 cores; Express 10 GB per database

- Source: Microsoft Learn  -  Editions and Supported Features of SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every FACT is correct, but the verbatim Basic AG limitation list is attributed to the wrong page. The cited editions page carries only footnote 6's one-sentence summary; the seven quoted bullets ('Limit of two replicas', 'No read access on secondary replica', 'No backups on secondary replica', 'No integrity checks on secondary replicas', 'Support for one availability database', "Basic availability groups can't be part of a distributed availability group", 'You might have multiple Basic availability groups connected to a single instance') appear on <https://learn.microsoft.com/en-us/sql/database-engine/availability-groups/windows/basic-availability-groups-always-on-availability-groups>, where I confirmed all seven verbatim. Re-cite them there. Two additions from that page that strengthen the design argument: Basic AGs 'can't be upgraded to advanced availability groups' without dropping and recreating, and on Linux they support an extra configuration-only replica. Also confirmed: Standard buffer pool 128 GB and 'lesser of 4 sockets or 24 cores'.

### A5.18  [yes]

**Claim.** SQL Server Developer edition 'includes all the functionality of Enterprise edition, but is licensed for use as a development and test system, not as a production server.' The Linux/container environment-variable documentation likewise describes MSSQL_PID='Developer' as 'the freely licensed Developer Edition of SQL Server for non-production use'. The current deployment sets MSSQL_PID=Developer.

**Limit or threshold asserted.** Developer = non-production licensing

- Source: Microsoft Learn  -  Editions and Supported Features of SQL Server 2022; Configure Environment Variables for SQL Server on Linux
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Second source: <https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-configure-environment-variables>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; both quotes are verbatim and the claim transparently attributes the second to the Linux environment-variable documentation, where I confirmed it ('MSSQL_PID specifies the freely licensed Developer Edition of SQL Server for non-production use'). One forward-looking note if the platform requirement in this exercise permits 'the same SQL Server major version or higher': in SQL Server 2025 (17.x) the MSSQL_PID value 'Developer' no longer exists - it is replaced by 'EnterpriseDeveloper' and 'StandardDeveloper' - so a container definition carrying MSSQL_PID=Developer is not portable to a 2025 image unchanged. The deployment's actual MSSQL_PID setting is a repo fact I could not verify.

### A5.19  [partially]

**Claim.** SQL Server Agent is disabled by default in the Linux container: MSSQL_AGENT_ENABLED 'Enables SQL Server Agent. For example, true enables, and false disables the agent. By default, the agent is disabled.' Log shipping is built entirely from SQL Server Agent jobs  -  a backup job on the primary, a copy job and a restore job on each secondary, and an alert job on the monitor  -  and is configured per database. 'A log shipping configuration doesn't automatically fail over from the primary server to the secondary server. If the primary database becomes unavailable, any of the secondary databases can be brought online manually.' It uniquely offers a 'user-specified delay between when the primary server backs up the log ... and when the secondary servers must restore', documented as useful 'if data is accidentally changed on the primary database'.

**Limit or threshold asserted.** per database; ~3-4 Agent jobs per configuration; manual failover only

- Source: Microsoft Learn  -  About Log Shipping (SQL Server); Configure Environment Variables for SQL Server on Linux
- URL: <https://learn.microsoft.com/en-us/sql/database-engine/log-shipping/about-log-shipping-sql-server>
- Second source: <https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-configure-environment-variables>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The log shipping substance is fully verified: all four job types and their locations (backup job on primary, copy and restore jobs on each secondary, alert job on the monitor), per-database configuration ('When log shipping is enabled on a database, the job category ... is created'), manual failover, and the delay benefit. The MSSQL_AGENT_ENABLED quote is misattributed - it is not on this page. It is verbatim at <https://learn.microsoft.com/en-us/sql/linux/configure/environment-variables>: 'Enables SQL Server Agent. For example, true enables, and false disables the agent. By default, the agent is disabled.' Re-cite it there. Also note the monitor server is optional ('An optional third server instance'), so the minimum is three job types, not four - the claim's '~3-4 Agent jobs per configuration' is right but the reason should be stated. One hard constraint the claim omits: 'Once the monitor server has been configured, it can't be changed without removing log shipping first.'

### A5.20  [partially]

**Claim.** Restoring one database is version-portable; restoring an instance is not. 'No SQL Server backup can be restored to an earlier version of SQL Server than the version on which the backup was created,' and 'Backups of master, model and msdb that were created by using an earlier version of SQL Server cannot be restored by SQL Server.' Restoring master requires starting the instance in single-user mode (-m/-f); 'After master is restored, the instance of SQL Server shuts down and terminates the sqlcmd process'; and Microsoft warns the recovery instance 'should be the same version, edition, and patch level, and it should have the same selection of features and the same external configuration (hostname, cluster membership, and so on) as the original instance. Doing otherwise can result in undefined SQL Server instance behavior.'

**Limit or threshold asserted.** Backups restore forward-only; master/model/msdb not restorable across versions

- Source: Microsoft Learn  -  RESTORE (Transact-SQL), Compatibility support; Restore the master Database (Transact-SQL)
- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-transact-sql>
- Second source: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-the-master-database-transact-sql>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The first two quotes are verbatim on the cited page and the claim's headline finding is sound. The master-restore material is misattributed: I searched the full RESTORE (Transact-SQL) page and it contains no mention of single-user mode, of the instance shutting down, of terminating sqlcmd, or of matching version/edition/patch level. All of it is verbatim on <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-the-master-database-transact-sql>, including 'Doing otherwise can result in undefined SQL Server instance behavior, with inconsistent feature support, and isn't guaranteed to be viable.' Re-cite. Two additions from the cited page that reinforce the same conclusion: restoring an earlier-version database created in an old default path requires the MOVE option, and a database attached or restored to a NEW instance needs OPEN MASTER KEY / ALTER MASTER KEY REGENERATE before its database master key works - a silent failure mode in a rebuild-onto-fresh-host runbook.

### A5.21  [yes]

**Claim.** Backup verification semantics. 'If a backup contains a backup checksum, RESTORE and RESTORE VERIFYONLY statements can check for errors.' Conversely, 'If there is no backup checksum, either restore operation proceeds without any verification; this is because without a backup checksum, restore cannot reliably verify page checksums.' If a page error is found during backup, 'the backup fails' by default; RESTORE VERIFYONLY by default 'continues'. A damaged backup set is flagged in msdb..backupset.is_damaged.

**Limit or threshold asserted.** WITH CHECKSUM is the precondition for meaningful verification

- Source: Microsoft Learn  -  Media errors: Backup and Restore (SQL Server)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/possible-media-errors-during-backup-and-restore-sql-server>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; all elements verify verbatim, and the conclusion that WITH CHECKSUM is the precondition for meaningful verification is exactly what the page supports. Two additions that sharpen the requirement it underwrites: backup checksums also set has_backup_checksums in msdb..backupset, which is the column an external monitor should assert on to prove the checksum policy is actually in force; and the page notes checksums carry real CPU cost ('carefully monitor the CPU overhead incurred'), which is worth acknowledging rather than treating WITH CHECKSUM as free.

### A5.22  [yes]

**Claim.** Backup encryption creates a second, absolute single point of failure. 'It's very important to back up the certificate or asymmetric key, and preferably to a different location than the backup file it was used to encrypt. Without the certificate or asymmetric key, you can't restore the backup, rendering the backup file unusable.' And: 'the certificate shouldn't be renewed on expiry or changed in any way. Renewal can result in updating the certificate triggering the change of the thumbprint, therefore making the certificate invalid for the backup file.' Express and Web editions cannot encrypt during backup (they can restore encrypted backups).

**Limit or threshold asserted.** certificate must never be renewed for the life of the backups it encrypts

- Source: Microsoft Learn  -  Backup encryption (SQL Server)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/backup-encryption>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; all three quotations are verbatim and the 'never renew for the life of the backups it encrypts' framing is correct. Three additions for the key-material requirement: a database master key in master is a prerequisite for backup encryption at all; the restoring account needs VIEW DEFINITION on the certificate, so key custody is an authorisation problem as well as a possession problem; and 'Appending to an existing backup set option isn't supported for encrypted backups', which independently reinforces the WITH FORMAT discipline that claim [16] derives from the S3 path.

### A5.23  [yes]

**Claim.** msdb backup history grows per operation and must be pruned. 'More rows are added to the backup and restore history tables after each backup or restore operation is performed; therefore, we recommend that you periodically execute sp_delete_backuphistory.' It affects backupfile, backupfilegroup, backupmediafamily, backupmediaset, backupset, restorefile, restorefilegroup, restorehistory. 'The physical backup files are preserved, even if all the history is deleted.'

- Source: Microsoft Learn  -  sp_delete_backuphistory (Transact-SQL)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-delete-backuphistory-transact-sql>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; the recommendation, all eight tables (backupfile, backupfilegroup, backupmediafamily, backupmediaset, backupset, restorefile, restorefilegroup, restorehistory) and the file-preservation sentence are exact. One operational consequence the claim should draw out, because it cuts against a naive pruning schedule: SSMS's restore dialog populates its database list 'only [with] databases that have been backed up according to the msdb backup history', and the error-9002 diagnostic script in Microsoft's own troubleshooting article queries msdb.dbo.backupset. Pruning history therefore degrades both the recovery UI and the RPO-observability requirement, so the retention window for history should be set deliberately against those needs rather than minimised.

### A5.24  [partially]

**Claim.** Concurrent backups are memory-bounded, not only I/O-bounded. 'Your BACKUP operation will consume BUFFERCOUNT * MAXTRANSFERSIZE in RAM', and Books Online warns that 'large numbers of buffers might cause "out of memory" errors'; the archived Microsoft support post documents a real failure with 'Msg 701 ... There is insufficient system memory to run this query' at 200 MB of total buffer space. Default BUFFERCOUNT for a database backup is (NumberOfBackupDevices x (1+GetSuggestedIoDepth)) + NumberOfBackupDevices + (2 x DatabaseDeviceCount).

**Limit or threshold asserted.** memory per backup = BUFFERCOUNT x MAXTRANSFERSIZE; S3 MAXTRANSFERSIZE up to 20 MB; instance limit here is MSSQL_MEMORY_LIMIT_MB 7168

- Source: Microsoft Learn (archived MSDN support blog)  -  Incorrect BufferCount data transfer option can lead to OOM condition
- URL: <https://learn.microsoft.com/en-us/archive/blogs/sqlserverfaq/incorrect-buffercount-data-transfer-option-can-lead-to-oom-condition>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Two corrections, one of them decision-affecting. FIRST: the sentence quoted as 'Your BACKUP operation will consume BUFFERCOUNT * MAXTRANSFERSIZE in RAM' does not appear on this page in any form. The page establishes the relationship by demonstration (buffercount 10 x 4 MB = 'Total buffer space: 40 MB'), not by that sentence. Either drop the quotation marks and state it as the arithmetic the page demonstrates, or find its true source. SECOND and more important: the Msg 701 failure is explicitly a 32-BIT phenomenon caused by contiguous virtual-address-space exhaustion in the MemToLeave region, as the quoted line shows. On 64-bit SQL Server on Linux with MSSQL_MEMORY_LIMIT_MB=7168 that mechanism does not exist, so 200 MB is not a threshold that binds here and should not be used to cap backup concurrency. The default BUFFERCOUNT formula IS correct as quoted - (NumberOfBackupDevices x (1+GetSuggestedIoDepth)) + NumberOfBackupDevices + (2 x DatabaseDeviceCount) - but note it comes from the summary table at the foot of the post, which differs from the formula given in the body text earlier, and the author concedes in the comments that the post's GetSuggestedIoDepth treatment needed correction. Finally, the page is a 2010 archived blog marked NOINDEX,NOFOLLOW - acceptable as colour, not as a limit to design against.

### A5.25  [yes]

**Claim.** SQL Server capacity ceilings that are NOT the binding constraint here: 32,767 databases per instance, 32,767 user connections, 524,272 TB database size. Nothing about 33 tenant databases stresses the engine; the constraints that bind are per-feature (basic AG = 1 database; snapshot backup = 64 databases), not per-instance.

**Limit or threshold asserted.** 32,767 databases/instance; 32,767 user connections

- Source: Microsoft Learn  -  Maximum Capacity Specifications for SQL Server
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/maximum-capacity-specifications-for-sql-server>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; all three figures are exact, and the reasoning that per-feature limits bind before per-instance ones is correct and well supported by claims [15] and [17]. Worth adding a fourth ceiling from the same table that is closer to binding than any of the three cited, given a 33-database estate that will grow: 'Nested stored procedure levels | 32', whose note reads 'If a stored procedure accesses more than 64 databases, or more than two databases in interleaving, you'll receive an error.' That 64-database figure is a genuine cross-database access limit sitting at the same magnitude as the snapshot-backup limit, and it deserves a place in the same list.

### A5.26  [yes]

**Claim.** Redis AOF with the default policy loses up to one second: 'appendfsync everysec: fsync every second. Fast enough ... and you may lose 1 second of data if there is a disaster.' More importantly for backup design, naively copying the Redis volume is unsafe: since 7.0 the AOF is multi-part in a directory, and 'if this is done during a rewrite, you might end up with an invalid backup'  -  you must set auto-aof-rewrite-percentage 0, confirm INFO persistence shows aof_rewrite_in_progress = 0, copy, then restore the setting. Redis also advises 'At least one time every day make sure to transfer an RDB snapshot outside your data center or at least outside the physical machine' and 'You also need some kind of independent alert system if the transfer of fresh backups is not working.' (The BACKUP command family that makes this clean requires Redis 8.10.0; this deployment is on Redis 7.)

**Limit or threshold asserted.** appendfsync everysec  <=1 s loss; BACKUP command family requires Redis >= 8.10.0

- Source: Redis documentation  -  Redis persistence
- URL: <https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; every quotation and the 8.10.0 version gate verify exactly, and the four-step safe-copy procedure matches the page. Two additions that reduce the operational cost of the workaround: the page documents a hard-link optimisation that shortens the window during which rewrites are disabled ('you may create hard links to the files in appenddirname ... and then re-enable rewrites'), and it warns that if the server restarts mid-backup the disabled-rewrite setting is lost unless persisted with CONFIG REWRITE - a failure mode that silently re-arms the very risk being mitigated. The deployment being on Redis 7 is a repo fact I could not verify.

### A5.27  [partially]

**Claim.** The MinIO open-source repository is archived and unmaintained. The repository banner reads 'This repository was archived by the owner on Apr 25, 2026. It is now read-only,' with a notice 'THIS REPOSITORY IS NO LONGER MAINTAINED,' and the README states 'The MinIO community edition is now distributed as source code only. We will no longer provide pre-compiled binary releases for the community version.' Users are directed to AIStor Free or AIStor Enterprise. Separately, object locking (WORM) documentation is now published under AIStor, with GOVERNANCE and COMPLIANCE retention modes, requiring versioning, and  -  since RELEASE.2025-05-20T20-30-00Z  -  settable on existing buckets via mc retention set --default.

**Limit or threshold asserted.** archived 2026-04-25; source-only distribution; no community binaries

- Source: github.com/minio/minio repository banner and README; MinIO AIStor object retention documentation
- URL: <https://github.com/minio/minio>
- Second source: <https://docs.min.io/aistor/administration/object-locking-and-immutability/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The archival findings are fully verified - banner, date of Apr 25 2026, the maintenance notice, the source-only distribution statement, and the redirection to AIStor Free and AIStor Enterprise - and this is a genuinely important catch for any design that assumed a maintained upstream. The second half is NOT supported by this URL and I could not verify it: the GitHub page says nothing about GOVERNANCE/COMPLIANCE retention modes, versioning prerequisites, mc retention set --default, or RELEASE.2025-05-20T20-30-00Z, and the AIStor object-retention documentation path I tried (docs.min.io/aistor/administration/object-management/object-retention/) 301-redirects to a 404. Either supply a working citation for the object-lock behaviour or downgrade it to unverified. Note also the README still offers a build path ('go install github.com/minio/minio@latest' and a Dockerfile), so 'unmaintained' is the accurate characterisation rather than 'unavailable' - the risk is absence of security patching, not absence of software.

### A5.28  [yes]

**Claim.** nginx does not preserve the client Host header to upstreams by default. The proxy_set_header documentation gives the default block verbatim as 'proxy_set_header Host $proxy_host; proxy_set_header Connection close;' and explains 'By default, the header fields "Host" and "Connection" from the original request are not passed to the proxied server.' Because tenancy in this system resolves from the Host header and nothing else, any recovery or failover path that introduces or reconfigures a proxy without an explicit Host directive returns 'Tenant not found' for every office.

**Limit or threshold asserted.** default is $proxy_host, not $host

- Source: nginx documentation  -  ngx_http_proxy_module, proxy_set_header
- URL: <https://nginx.org/en/docs/http/ngx_http_proxy_module.html#proxy_set_header>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None; the default block and the explanatory sentence are verbatim, and $proxy_host (not $host) is confirmed as the default value. One addition that matters for any recovery path terminating HTTP/2: 'For HTTP/2, the ":authority" pseudo-header field with the $proxy_host value is sent by default, unless it is replaced with an explicit "Host" header field' - so the same tenant-resolution failure reaches an HTTP/2 upstream through a different header, and a fix that only sets Host on the HTTP/1.1 path is incomplete. The claim's downstream assertion that tenancy in this system resolves from the Host header alone is a repo fact I could not verify.

### A5.29  [yes]

**Claim.** Both recommended tooling options are current and actively maintained. Ola Hallengren's SQL Server Maintenance Solution most recent release 16 August 2026 (prior releases 9 August 2026 and 8 August 2026); supports SQL Server 2017+ including 2022 and 2025, SQL Server on Linux, and backup destinations including AWS S3 (S3 support was added in collaboration with AWS and is SQL Server 2022+ only). dbatools, which provides Test-DbaLastBackup (restore under a different name + DBCC CHECKDB + drop), latest published version 2.8.4 on 2026-07-31.

**Limit or threshold asserted.** Ola: 2026-08-16; dbatools: 2.8.4, published 2026-07-31

- Source: ola.hallengren.com version history; PowerShell Gallery package feed for dbatools
- URL: <https://ola.hallengren.com/versions.html>
- Second source: <https://dbatools.io/Test-DbaLastBackup/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The load-bearing dates both verify: Ola's most recent release is 16 August 2026 with the two prior entries on 9 and 8 August 2026, and dbatools 2.8.4 was published 2026-07-31 on the PowerShell Gallery. The minimum-version floor of SQL Server 2017 is confirmed from the 13 January 2025 entry, and S3 support is confirmed as added 27 October 2024. Two sub-details I could not confirm from the cited page and which should be dropped or re-sourced: that S3 support was added 'in collaboration with AWS', and that it is 'SQL Server 2022+ only' (plausible, since claim [16] establishes the engine feature is 2022+, but the version history does not say it). The description of Test-DbaLastBackup's behaviour (restore under a different name, DBCC CHECKDB, drop) is also not evidenced by either cited page - cite the dbatools command documentation for it. Both projects are unambiguously active, which is the point being made.

### A5.30  [partially]

**Claim.** The 72-hour restoration figure is PROPOSED, not in force. HHS's own NPRM fact sheet describes a requirement to 'Establish written procedures to restore the loss of certain relevant electronic information systems and data within 72 hours,' plus a criticality analysis to prioritise restoration and a 24-hour business-associate notification on contingency-plan activation, and it proposes removing the required/addressable distinction. The NPRM was published in the Federal Register on 6 January 2025 and the comment period closed 7 March 2025. As of 2026 no final rule has issued; secondary reporting states HHS moved RIN 0945-AA22 to the Unified Agenda's Long-Term Actions with July 2027 as the anticipated final action date (slipped from a prior May 2026 target). I could not fetch reginfo.gov directly to confirm the agenda entry first-hand.

**Limit or threshold asserted.** 72 hours (proposed); 24 hours BA notification (proposed); final action targeted July 2027 (unverified against reginfo.gov)

- Source: HHS.gov  -  HIPAA Security Rule NPRM fact sheet (primary, for the proposal's content); law-firm and trade reporting (secondary, for the delay)
- URL: <https://www.hhs.gov/hipaa/for-professionals/security/hipaa-security-rule-nprm/factsheet/index.html>
- Second source: <https://www.clarkhill.com/news-events/news/hipaa-security-rule-update-delayed-until-2027/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The three substantive quotations verify verbatim and the central finding - that 72 hours is proposed and not in force - is correct and important. The dates are misattributed but independently correct: the HHS fact sheet states only that the NPRM was issued 27 December 2024 with comments due 60 days after Federal Register publication. I verified the rest against the Federal Register API: publication_date 2025-01-06, comments_close_on 2025-03-07, citation 90 FR 898, RIN 0945-AA22. I also confirmed something the claim asserts but did not evidence - querying the Federal Register for RIN 0945-AA22 returns exactly one document, of type Proposed Rule, so NO FINAL RULE HAS ISSUED as of this check. Cite federalregister.gov for the dates and for the no-final-rule finding rather than the fact sheet. The July 2027 Unified Agenda target remains unverified, as the claim honestly states; keep that caveat.

### A5.31  [could-not-check]

**Claim.** CISA's #StopRansomware Guide recommends maintaining offline, encrypted backups that are regularly tested, and recommends immutable backups. I could not fetch the guide PDF directly (cisa.gov returned HTTP 403 to my fetch; a state-government mirror of StopRansomware-Guide-508C-v3_1.pdf exists), so I have NOT verified the widely-repeated vendor claim that CISA 'explicitly endorses 3-2-1-1-0'. Treat 3-2-1 as the well-established baseline (three copies, two media types, one offsite) and 3-2-1-1-0 (adding one immutable/air-gapped copy and zero verified recovery errors) as a widely-used industry extension repeated mainly in vendor material, not as a cited government standard. The defensible primary anchors for the same behaviour are NIST SP 800-34 3.4.2 offsite-storage criteria and 45 CFR 164.308(a)(7)(ii)(A).

**Limit or threshold asserted.** 3-2-1 baseline verified as industry convention; '3-2-1-1-0 endorsed by CISA' NOT verified

- Source: CISA #StopRansomware Guide (October 2023, v3.1)  -  attempted primary fetch; NIST SP 800-34 Rev. 1 3.4.2 used as the substitute primary anchor
- URL: <https://www.cisa.gov/resources-tools/resources/stopransomware-guide>
- Second source: <https://dir.texas.gov/sites/default/files/2024-01/StopRansomware-Guide-508C-v3_1.pdf>
- Accessed: 2026-08-31
- Confidence: unverified
- Verifier note: I reproduce the researcher's failure exactly: both <https://www.cisa.gov/resources-tools/resources/stopransomware-guide> and the direct PDF return 403 to automated fetches, so the CISA text remains unchecked. The claim's handling of this is the correct behaviour and should be preserved - it declines to assert the 3-2-1-1-0 attribution rather than repeating it, which is precisely the failure mode this review exists to catch. Both fallback anchors it proposes DO verify: NIST SP 800-34 Rev. 1 section 3.4.2 is indeed titled 'Backup Methods and Offsite Storage' and contains the offsite-storage criteria, and 45 CFR 164.308(a)(7)(ii)(A) is the Required Data backup plan specification confirmed in claim [1]. Recommendation: build the requirement on those two primary anchors alone and drop the CISA citation entirely rather than carrying an unverifiable URL in an evidence base - nothing in the design depends on it.

### A5.32  [yes]

**Claim.** Hangfire's model is retry-based re-entrancy, not exactly-once. Its Best Practices documentation states 'Reentrancy means that a method can be interrupted in the middle of its execution and then safely called again' and that 'The interruption can be caused by many different things (i.e. exceptions, server shut-down), and Hangfire will attempt to retry processing many times.' The consequence for DR: restoring the host database (which holds the Hangfire schema) to an earlier point re-arms jobs that already ran, and running a second processing server during a failover double-executes them.

**Limit or threshold asserted.** n/a  -  Hangfire documents retry/re-entrancy; it does not use the phrase 'at least once' on this page

- Source: Hangfire documentation  -  Best Practices
- URL: <https://docs.hangfire.io/en/latest/best-practices.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: None. Both quotations are verbatim, and the claim's own caveat is accurate and unusually scrupulous: the page does not use the phrase 'at least once', and I confirmed it does not appear anywhere on the page. The DR consequence (restore re-arms completed jobs; a second processing server double-executes) is inference rather than documentation, but it follows directly from retry semantics plus the fact that job state lives in the restored database, and the claim presents it as a consequence rather than as a quotation. The requirement it underwrites - exactly one active processing server against the Hangfire schema, plus idempotent handlers - is the right shape given that the documentation promises re-entrancy safety only if the developer provides it.

### A5.33  [partially]

**Claim.** Restoring one office backward while the host stays current produces externally visible side effects, not merely internal inconsistency: the office's AppNotificationOutboxItems and AppIntegrationOutboxItems roll back to unsent, so the drain jobs re-send  -  duplicate ex-parte-addressed emails to real external parties and duplicate messages to the Case Tracker partner, whose reconciliation runs every 15 minutes. MinIO objects written in the lost window are not rolled back and become orphaned PHI with no referencing row. Audit rows for the window are destroyed and cannot be reconstructed, which touches 45 CFR 164.312(b) Audit controls. Therefore the recovery design requirement is reconcilability (idempotent drains keyed on a partner-visible identifier, plus an orphan-object sweep), not consistency.

- Source: My architectural analysis of the stated outbox/blob/audit design against the restore semantics documented by Microsoft
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-a-sql-server-database-to-a-point-in-time-full-recovery-model>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The regulatory citation is correct - I verified 45 CFR 164.312(b) is indeed the Audit controls standard and quoted it above - but the cited SQL Server URL supports none of this claim. The PITR page documents restore mechanics and says nothing about outbox tables, object stores, audit rows or duplicate external sends; those are all application-level consequences drawn from the brief's description of the system, which I cannot verify. Re-cite: use 164.312(b) from eCFR for the audit point, and either drop the Microsoft URL or replace it with a statement it actually supports (that a point-in-time restore rolls the database back to a chosen time, from which the application-level consequences follow). The reasoning itself is sound and the reconcilability-over-consistency conclusion is the most useful design judgement in the set; it just needs its citations attached to the right propositions. Note also that 164.312(b) requires mechanisms that 'record and examine' activity - destroying audit rows in a restore window is a gap in the recording arm specifically, which is worth saying precisely.

### A5.34  [partially]

**Claim.** Recovery has a fleet-wide single point of failure that only manifests during recovery. Per the stated facts, db-migrator loops offices with no per-tenant error handling, and both authserver and api gate on its service_completed_successfully. During a rebuild, one office database whose schema application fails mid-loop therefore prevents the entire stack  -  all offices  -  from starting, with no report of which succeeded. This inverts the usual tenant-isolation benefit exactly when isolation matters most.

- Source: My analysis of the stated docker-compose gating and migration-runner behaviour
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-and-recovery-overview-sql-server>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The cited URL is reachable but irrelevant to the claim - it is a SQL Server restore-and-recovery overview and cannot speak to db-migrator, service_completed_successfully gating, or per-tenant error handling, all of which are Compose/application facts from the brief that I could not verify. This claim needs a code citation (the migrator source and the Compose dependency declarations), not a Microsoft documentation link; carrying an unrelated URL here weakens an otherwise strong argument. The underlying observation - that a fail-fast, all-or-nothing migration step inverts tenant isolation precisely during recovery - is a genuine architectural finding and pairs directly with the requirement that recovery of one office must not block any other, including during the migration/seed step on every bring-up. Recommend restating it as an observed property of the codebase with file references, and adding that the fix has two parts: per-tenant error isolation in the loop, and a machine-readable report of which offices succeeded, since without the latter an operator cannot resume a partial recovery.

---

## Area: environments

Verification verdict for this area: **material-errors** (34 claims checked)

### A6.1  [yes]

**Claim.** The twelve-factor dev/prod parity factor names three gaps  -  time, personnel, tools  -  and states 'Keep development, staging, and production as similar as possible'. The canonical site still serves text last updated 2017.

**Limit or threshold asserted.** Page footer: 'Last updated 2017'

- Source: The Twelve-Factor App  -  X. Dev/prod parity
- URL: <https://12factor.net/dev-prod-parity>
- Accessed: 2026-08-31
- Confidence: verified

### A6.2  [partially]

**Claim.** Heroku open-sourced the twelve-factor definition as a community-maintained project in November 2024 under CC-BY-4.0, stating the original examples reflect outdated practices while 'the core concepts are still remarkably relevant'. The rewrite lives at github.com/twelve-factor/twelve-factor and 'will ultimately replace the one hosted at 12factor.net'.

**Limit or threshold asserted.** Announced 2024-11-12; call for participation 2024-08-28, updated 2024-09-10; licence CC-BY-4.0

- Source: Heroku Blog  -  Heroku Open Sources the Twelve-Factor App Definition / Updating Twelve-Factor: A Call for Participation
- URL: <https://www.heroku.com/blog/heroku-open-sources-twelve-factor-app-definition/>
- Second source: <https://www.heroku.com/blog/updating-twelve-factor-call-for-participation/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Only the 2024-11-12 date and the repo URL come from the cited Heroku page. CC-BY-4.0 and the 'will ultimately replace' quote must be cited to github.com/twelve-factor/twelve-factor (README/LICENSE), not the blog. The quote 'the core concepts are still remarkably relevant' could not be located on either source  -  it appears to be from the separate call-for-participation post and should be dropped or re-sourced. The 2024-08-28 / 2024-09-10 call-for-participation dates are unsupported by the cited URL.

### A6.3  [yes]

**Claim.** The community rewrite has stalled: the `next` branch's newest commit is 2025-07-10 and `main`'s is 2024-11-21. The revised dev/prod parity text restructures the factor into numbered principles with a 'Guidance' section but does not change its substance. Treat twelve-factor as a durable principle, not as actively-evolving guidance.

**Limit or threshold asserted.** next branch newest commit 2025-07-10 (~14 months stale at access date); main newest commit 2024-11-21

- Source: twelve-factor/twelve-factor GitHub repository (next and main branches, content/dev-prod-parity.md)
- URL: <https://github.com/twelve-factor/twelve-factor/commits/next>
- Second source: <https://raw.githubusercontent.com/twelve-factor/twelve-factor/next/content/dev-prod-parity.md>
- Accessed: 2026-08-31
- Confidence: verified

### A6.4  [yes]

**Claim.** A serious 2026 counter-position exists: 'any effort that goes into delivering software to staging is inherently wasteful', staging 'presents a misalignment of incentives between developers and users', it 'reduces the throughput of changes to production', and 'the data in staging is often unrepresentative of data in production'. The alternative proposed requires five practices: TDD, ephemeral local environments, feature flagging, continuous deployment and production monitoring.

**Limit or threshold asserted.** Published 2026-01-08. Three of its five named preconditions (feature flags, continuous deployment, production monitoring) are absent from this system.

- Source: Tom Phillips  -  Staging is a wasteful lie: the case for the mono-environment
- URL: <https://www.tomwphillips.co.uk/2026/01/staging-is-a-wasteful-lie-the-case-for-the-mono-environment/>
- Accessed: 2026-08-31
- Confidence: verified

### A6.5  [yes]

**Claim.** NIST SP 800-53 Rev 5 CM-4(1) requires analysing changes 'in a separate test environment before implementation in an operational environment' but explicitly permits logical separation: 'Separate environments can be achieved by physical or logical means.' CM-2(6) adds that test configurations 'mirror configurations in the operational environment to the extent practicable' and that 'Separate baseline configurations do not necessarily require separate physical environments.'

**Limit or threshold asserted.** Controls CM-4(1), CM-2(6), SA-3(1), CP-4, CP-9(1), PM-25

- Source: NIST SP 800-53 Rev 5 control catalog (OSCAL JSON, first-party)
- URL: <https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json>
- Second source: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-53r5.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Minor: the catalog served at that URL is now labelled 'SP 800-53 Rev 5.2.0' (metadata last-modified 2026-05-11). Cite it as Rev 5.2.0 rather than bare 'Rev 5' if release precision matters.

### A6.6  [yes]

**Claim.** NIST SP 800-53 Rev 5 PM-25 requires policies limiting PII in testing and states: 'When possible, organizations use placeholder data to avoid exposure of personally identifiable information when conducting testing, training, and research.' CP-9(1) requires testing backup information 'to verify media reliability and information integrity'.

**Limit or threshold asserted.** PM-25 'Minimization of Personally Identifiable Information Used in Testing, Training, and Research'; CP-9(1) 'Testing for Reliability and Integrity'

- Source: NIST SP 800-53 Rev 5 control catalog (OSCAL JSON, first-party)
- URL: <https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json>
- Accessed: 2026-08-31
- Confidence: verified

### A6.7  [yes]

**Claim.** HIPAA 45 CFR 164.308(a)(7)(ii)(D) 'Testing and revision procedures (Addressable): Implement procedures for periodic testing and revision of contingency plans.' (a)(7)(ii)(A) Data backup plan and (B) Disaster recovery plan are Required. 164.308(a)(1)(ii)(A) requires a risk analysis covering ePHI 'held by the covered entity or business associate'  -  which is the hook that pulls any PHI-holding non-production environment into scope.

**Limit or threshold asserted.** (a)(7)(ii)(D) is Addressable, not Required; (a)(7)(ii)(A)-(C) are Required

- Source: 45 CFR  164.308  -  Administrative safeguards (Cornell LII)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.308>
- Second source: <https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.308>
- Accessed: 2026-08-31
- Confidence: partial

### A6.8  [yes]

**Claim.** 45 CFR 164.514(b) offers two de-identification routes: expert determination that 'the risk is very small that the information could be used... to identify an individual', or safe harbor removal of 18 identifier classes plus lack of actual knowledge of re-identifiability. Both are recurring work on every refresh, which is why a prod-to-nonprod copy is expensive as a standing practice.

**Limit or threshold asserted.** 18 identifier classes under the safe harbor method (b)(2)

- Source: 45 CFR  164.514  -  De-identification (Cornell LII)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.514>
- Accessed: 2026-08-31
- Confidence: partial

### A6.9  [partially]

**Claim.** The January 2025 HIPAA Security Rule NPRM  -  which would add mandatory contingency testing and a 72-hour restoration expectation  -  is NOT final as of August 2026, with final action signalled for July 2027. Design to the current rule; record restore timings now so the evidence exists if it lands.

**Limit or threshold asserted.** NPRM published 2025-01-06; comment period closed 2025-03-07; final action target July 2027. Secondary sources only  -  I could not retrieve the Unified Agenda entry (RIN 0945-AA22) directly.

- Source: HIPAA Journal  -  HIPAA Security Rule Update Postponed / Clark Hill  -  HIPAA Security Rule Update Delayed Until 2027
- URL: <https://www.hipaajournal.com/hipaa-security-rule-update-postponed/>
- Second source: <https://www.clarkhill.com/news-events/news/hipaa-security-rule-update-delayed-until-2027/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The cited page supports: FR publication 2025-01-06, not-yet-final status, July 2027 final-action target, and RIN 0945-AA22. It does NOT state the comment period closed 2025-03-07, and it does NOT mention any 72-hour restoration expectation. Drop or separately source both. (The NPRM's 72-hour element, if retained, must be cited to the Federal Register notice itself, and characterised precisely  -  the proposal is restoration of certain relevant electronic information systems and data within 72 hours, not a blanket 72-hour restore SLA.)

### A6.10  [yes]

**Claim.** NIST SP 800-34 Rev 1 names 'System recovery on an alternate platform from backup media' as an element that 'should be addressed in a contingency plan test', and defines the graduated ladder of tabletop (discussion only), functional (simulated operational environment) and full-scale functional exercises. This gives a defensible cheap-to-expensive sequence for restore rehearsal.

**Limit or threshold asserted.** Published May 2010, updated 2010-11-11. It is old  -  I flag that honestly; NIST SP 800-53r5 CP-4 is the current normative control and points at the same practice.

- Source: NIST SP 800-34 Rev 1, Contingency Planning Guide for Federal Information Systems, 3.5
- URL: <https://nvlpubs.nist.gov/nistpubs/legacy/sp/nistspecialpublication800-34r1.pdf>
- Accessed: 2026-08-31
- Confidence: verified

### A6.11  [yes]

**Claim.** NIST's Interaction Rule: 'Most failures are induced by single factor faults or by the joint combinatorial effect (interaction) of two factors, with progressively fewer failures induced by interactions between three or more factors.' Empirical data suggest failures are triggered by six or fewer interacting variables, and 'pairwise testing may miss 10% to 40% or more of system bugs'.

**Limit or threshold asserted.** Published October 2010; interaction strengths 1-6; pairwise miss rate 10-40%

- Source: NIST SP 800-142, Practical Combinatorial Testing
- URL: <https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-142.pdf>
- Accessed: 2026-08-31
- Confidence: verified

### A6.12  [partially]

**Claim.** Microsoft's multitenant guidance states that a canary deployment ring 'includes your own test tenants and customers who want to receive updates as soon as they're available'. Its antipatterns list names 'Manual deployment and testing' and 'Specialized customizations for tenants'. Its checklist requires using 'automation to manage the tenant life cycle, such as onboarding, deployment, provisioning, and configuration' and to 'Continuously test your isolation model'.

- Source: Azure Architecture Center  -  Multitenant solutions: updates, deployment and configuration, checklist
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/updates>
- Second source: <https://raw.githubusercontent.com/MicrosoftDocs/architecture-center/main/docs/guide/multitenant/checklist.md>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Material citation error: three separate pages are attributed to one URL. Only the canary-ring sentence is on .../considerations/updates. The two checklist items belong to <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/checklist>. The named antipatterns ('Manual deployment and testing', 'Specialized customizations for tenants') are on the deployment-and-configuration approaches page (.../multitenant/approaches/deployment-configuration#antipatterns-to-avoid)  -  the checklist page paraphrases them as 'running separate versions of the solution for each tenant, hard-coding tenant-specific configurations or logic, and relying on manual deployments'. Split the citation three ways before publishing.

### A6.13  [yes]

**Claim.** AWS Well-Architected SaaS Lens REL 3 requires automated tests that 'Validate tenant isolation' with tests that 'search for potential opportunities to subvert the isolation model', and separately 'Validate the scale and repeatability of tenant onboarding'. It gives no numeric test-tenant count, only 'a tenant count that represents a meaningful load on the system'.

**Limit or threshold asserted.** No numeric threshold is published by AWS  -  I searched for one specifically and there is none.

- Source: AWS Well-Architected SaaS Lens, REL 3
- URL: <https://wa.aws.amazon.com/saas.question.REL_3.en.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Minor: 'Validate tenant isolation' and 'Validate the scale and repeatability of tenant onboarding' are best-practice headings; the supporting prose is as quoted above. Present them as headings rather than as prose quotations. The claim that no numeric threshold is published is confirmed  -  this correctly means requirement 15's '33 tenant databases' is a locally-chosen number with no external authority behind it.

### A6.14  [yes]

**Claim.** Microsoft's safe deployment practices guidance states as a core principle: 'The same tools used to deploy in production should be used in development and test environments.' It describes progressive exposure through tiers where tier 0 is 'Internal only, high tolerance for risk and bugs', and recommends a bake time of roughly 24 hours including a peak-usage period.

**Limit or threshold asserted.** Page updated 2025-10-27; bake time guidance 24 hours including peak usage

- Source: Microsoft Learn  -  Safe deployment practices
- URL: <https://learn.microsoft.com/en-us/devops/operate/safe-deployment-practices>
- Accessed: 2026-08-31
- Confidence: verified

### A6.15  [partially]

**Claim.** RFC 9525 6.3 constrains TLS wildcards: 'There is only one wildcard character' and 'The wildcard character appears only as the complete content of the left-most label', and 'A wildcard in a presented identifier can only match one label in a reference identifier.' Therefore {office}.<base>, {office}.api.<base> and {office}.auth.<base> require three separate wildcard certificates per environment.

**Limit or threshold asserted.** 6.3; published November 2023; obsoletes RFC 6125

- Source: RFC 9525, Service Identity in TLS
- URL: <https://www.rfc-editor.org/rfc/rfc9525.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The RFC text is quoted accurately, but the operational conclusion is wrong. RFC 9525 constrains each wildcard NAME, not the number of certificates. Three wildcard names (*.base, *.api.base, *.auth.base) plus an exact-match host can all live as SANs in ONE certificate; ACME/Let's Encrypt explicitly issues multi-SAN certificates containing wildcards. Correct statement: 'requires three distinct wildcard identifiers, which may be carried in one multi-SAN certificate or in separate certificates.' Capability requirement 2 inherits this error and should be reworded to 'at least three distinct wildcard identifiers at three DNS depths' rather than 'three distinct wildcard certificates'.

### A6.16  [yes]

**Claim.** DNS wildcards match differently from TLS wildcards  -  they DO synthesise across multiple labels. RFC 4592 2.2.1: for 'QNAME=foo.bar.example. QTYPE=TXT' with only *.example. in the zone, 'the answer will be "foo.bar.example. IN TXT ..." because bar.example. does not exist, but the wildcard does.' One DNS wildcard resolves every tier; one TLS wildcard covers exactly one.

**Limit or threshold asserted.** 2.2.1 worked example; published July 2006

- Source: RFC 4592, The Role of Wildcards in the Domain Name System
- URL: <https://www.rfc-editor.org/rfc/rfc4592>
- Accessed: 2026-08-31
- Confidence: verified

### A6.17  [yes]

**Claim.** Wildcard certificates can only be issued via DNS-01. Let's Encrypt: HTTP-01 'cannot be used to issue wildcard certificates'; TLS-ALPN-01 'cannot be used to validate wildcard domains'; DNS-01 'allows you to issue wildcard certificates' and 'it only makes sense to use DNS-01 challenges if your DNS provider has an API you can use to automate updates.' This makes an automatable DNS API a hard platform requirement generated by the tenancy model.

- Source: Let's Encrypt  -  Challenge Types
- URL: <https://letsencrypt.org/docs/challenge-types/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Minor wording: the DNS-01 line is quoted as 'allows you to issue wildcard certificates'; the page's actual sentence is 'You can use this challenge to issue certificates containing wildcard domain names.' Substance unaffected.

### A6.18  [yes]

**Claim.** Let's Encrypt production rate limits: 'Up to 50 certificates can be issued per registered domain... every 7 days' and 'Up to 5 certificates can be issued per exact same set of identifiers every 7 days', with 'Up to 5 authorization failures per identifier... every hour'. A staging environment for rehearsing cert automation exists at acme-staging-v02.api.letsencrypt.org with far higher limits, but its roots are 'not present in browser/client trust stores'.

**Limit or threshold asserted.** 50/registered domain/7d; 5/exact identifier set/7d; 5 auth failures/identifier/hour. Rate-limits page last updated 2026-08-05; staging page 2026-04-10.

- Source: Let's Encrypt  -  Rate Limits and Staging Environment
- URL: <https://letsencrypt.org/docs/rate-limits/>
- Second source: <https://letsencrypt.org/docs/staging-environment/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Minor: the staging endpoint, trust-store sentence and higher limits are on letsencrypt.org/docs/staging-environment/, not the cited rate-limits URL. Cite both.

### A6.19  [partially]

**Claim.** Every publicly-trusted TLS certificate is published to public, append-only Certificate Transparency logs and is searchable via aggregators such as crt.sh; CT logging is enforced by the Chrome Root Program. Issuing a certificate for a non-production hostname therefore publicly announces that environment's existence.

**Limit or threshold asserted.** Chrome Root Program Policy v1.8 (2026-06-15) strengthened CT requirements; Chrome 148 (2026-05-05) removed SCT delivery via stapled OCSP. I verified the policy exists and mandates logging; I did not independently confirm the v1.8 date beyond secondary reporting.

- Source: Chrome Certificate Transparency Policy; MDN Certificate Transparency
- URL: <https://googlechrome.github.io/CertificateTransparency/ct_policy.html>
- Second source: <https://developer.mozilla.org/en-US/docs/Web/Security/Certificate_Transparency>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The cited page supports CT enforcement and the Chrome-148 change point for SCT-via-stapled-OCSP, but states no version number and no dates. 'Chrome Root Program Policy v1.8 (2026-06-15)' is a different document (the Chrome Root Program policy on chromium.org, not this CT policy page) and remains unverified  -  drop the version/date or cite the Root Program policy directly. The 2026-05-05 Chrome 148 date is also unsupported here. crt.sh is not mentioned on this page (uncontroversial, but it is not evidenced by the citation).

### A6.20  [yes]

**Claim.** HSTS preload submission requires max-age of at least 31536000, the includeSubDomains directive and the preload directive, and the form warns it 'will prevent all subdomains and nested subdomains from being accessed without a valid HTTPS certificate'. Removal is slow: 'it takes months for a change to reach users with a Chrome update and we cannot make guarantees about other browsers.'

**Limit or threshold asserted.** max-age >= 31536000 seconds (1 year); removal latency measured in months

- Source: hstspreload.org
- URL: <https://hstspreload.org/>
- Accessed: 2026-08-31
- Confidence: verified

### A6.21  [yes]

**Claim.** SQL Server Developer edition 'includes all the functionality of Enterprise edition, but is licensed for use as a development and test system, not as a production server.' Standard edition caps the buffer pool at 128 GB and compute at the lesser of 4 sockets or 24 cores; Express caps database size at 10 GB. Always On availability groups are Enterprise-only; Standard offers only basic availability groups, which support 'two replicas, with one database'.

**Limit or threshold asserted.** Standard: 128 GB buffer pool, 4 sockets/24 cores; Express: 10 GB max database size, 1410 MB buffer pool; basic AG = 2 replicas, 1 database. Page ms.date 2025-11-27, updated 2026-08-12.

- Source: Microsoft Learn  -  Editions and supported features of SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified

### A6.22  [yes]

**Claim.** Databases carrying edition-restricted features cannot be moved across editions: 'A database that contains these features can't be moved to an edition of SQL Server that doesn't support them.' `SELECT feature_name FROM sys.dm_db_persisted_sku_features` reports them (ChangeCapture, ColumnStoreIndex, Compression, MultipleFSContainers, InMemoryOLTP, Partitioning, TransparentDataEncryption) and returns no rows when the database is portable.

**Limit or threshold asserted.** Requires VIEW DATABASE PERFORMANCE STATE on SQL Server 2022+. Page updated 2026-08-24.

- Source: Microsoft Learn  -  sys.dm_db_persisted_sku_features (Transact-SQL)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-db-persisted-sku-features-transact-sql>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Minor: the URL redirects  -  canonical path is /system-dynamic-management-objects/ not /system-dynamic-management-views/. Also worth carrying forward: the page's note that since SQL Server 2016 SP1 all of these except TransparentDataEncryption are available across multiple editions, which materially weakens the practical portability risk for a Developer-vs-Standard split.

### A6.23  [yes]

**Claim.** 'No SQL Server backup can be restored to an earlier version of SQL Server than the version on which the backup was created.' Build parity between environments is therefore one-directional and must be managed deliberately if restore rehearsal is to work.

**Limit or threshold asserted.** Absolute, no exceptions stated. Page updated 2026-08-24.

- Source: Microsoft Learn  -  RESTORE (Transact-SQL)
- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-transact-sql>
- Accessed: 2026-08-31
- Confidence: verified

### A6.24  [yes]

**Claim.** Docker Compose: 'If an environment variable is not set, Compose substitutes with an empty string.' The fail-fast forms are `${VAR:?error}`  -  'value of VAR if set and non-empty, otherwise exit with error'  -  and `${VAR?error}`. Multiple --env-file options are read in order with later files overriding earlier ones.

- Source: Docker Docs  -  Compose variable interpolation
- URL: <https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/>
- Accessed: 2026-08-31
- Confidence: verified

### A6.25  [yes]

**Claim.** `docker compose config` 'merges the Compose files set by -f flags, resolves variables in the Compose file, and expands short-notation into the canonical format'. `--resolve-image-digests` pins image tags to their digest values; `--quiet` validates without printing. Together these give a two-person team build-once-deploy-many with no extra tooling.

- Source: Docker Docs  -  docker compose config CLI reference
- URL: <https://docs.docker.com/reference/cli/docker/compose/config/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The doc claim is sound; the inference 'gives a two-person team build-once-deploy-many with no extra tooling' is the researcher's own, not the doc's. Note that --resolve-image-digests pins at render time and does not by itself satisfy requirement 13's 'identical pinned set across environments'  -  that needs the rendered output to be stored as the deployed artifact.

### A6.26  [yes]

**Claim.** SQL Server regular identifiers permit only letters, digits and the symbols _ @ # $, must start with a letter or _ @ #, and 'Identifiers that don't comply with the rules for regular identifiers must be delimited'. DNS labels, by contrast, permit hyphens (letter-digit-hyphen syntax, RFC 1035 2.3.1). An office slug is legal DNS and illegal as a bare SQL identifier fragment.

**Limit or threshold asserted.** Identifiers 1-128 characters; permitted symbols _ @ # $; RFC 1035 <let-dig-hyp> ::= <let-dig> | "-". MS page ms.date 2026-04-08.

- Source: Microsoft Learn  -  Database Identifiers; RFC 1035 2.3.1
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/databases/database-identifiers>
- Second source: <https://www.rfc-editor.org/rfc/rfc1035.txt>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Minor: the RFC 1035 2.3.1 <let-dig-hyp> half of the claim was not independently fetched (it is standard and uncontroversial). Also note the page adds a constraint the claim omits  -  a regular identifier must not be a T-SQL reserved word  -  which matters for auto-generated per-office identifiers.

### A6.27  [yes]

**Claim.** OpenIddict's own documentation separates 'Registering a development certificate (recommended for local development)' from 'Registering a certificate (recommended for production)', and states that in production 'it is recommended to use two RSA certificates, distinct from the certificate(s) used for HTTPS: one for encryption, one for signing.' A non-production environment using the development path never exercises the production PFX load path.

- Source: OpenIddict documentation  -  Encryption and signing credentials
- URL: <https://documentation.openiddict.com/configuration/encryption-and-signing-credentials>
- Second source: <https://raw.githubusercontent.com/openiddict/openiddict-documentation/dev/configuration/encryption-and-signing-credentials.md>
- Accessed: 2026-08-31
- Confidence: verified

### A6.28  [yes]

**Claim.** ABP's Angular UI supports runtime environment configuration via a `remoteEnv` property with a required `url` and `mergeStrategy` (deepmerge, overwrite, or a custom function), letting one build be deployed to multiple environments and multiple tenants without rebuilding  -  which is what twelve-factor's build/release/run separation demands and which per-tenant API hostnames make mandatory here.

**Limit or threshold asserted.** mergeStrategy values: deepmerge | overwrite | customMergeFn

- Source: ABP.IO Documentation  -  Angular Environment
- URL: <https://abp.io/docs/latest/framework/ui/angular/environment>
- Accessed: 2026-08-31
- Confidence: verified

### A6.29  [yes]

**Claim.** DORA's test data management guidance cautions against 'Using a full copy of the production database, rather than identifying relevant or important portions', and holds that 'Test data for automated test suites can be acquired on demand'. This supports generating production-shaped data rather than copying production data.

**Limit or threshold asserted.** No version or date shown on the page.

- Source: DORA  -  Test data management capability
- URL: <https://dora.dev/capabilities/test-data-management/>
- Accessed: 2026-08-31
- Confidence: partial

### A6.30  [partially]

**Claim.** Testcontainers for .NET is current and actively maintained: Testcontainers 4.14.0 published 2026-08-14, with 4.13.0 (2026-07-02), 4.12.0 (2026-05-19), 4.11.0 (2026-03-12) and 4.10.0 (2026-01-01)  -  a release roughly every six weeks through 2026.

**Limit or threshold asserted.** Latest 4.14.0 dated 2026-08-14; targets .NET 8.0 and .NET Standard 2.0

- Source: NuGet Gallery  -  Testcontainers / Testcontainers.MsSql
- URL: <https://www.nuget.org/packages/Testcontainers.MsSql>
- Second source: <https://github.com/testcontainers/testcontainers-dotnet/releases>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All five versions and dates are correct and the package is clearly current. The cadence characterisation is wrong: the intervals are ~10 weeks (1/1->3/12), ~10 weeks (3/12->5/19), ~6 weeks (5/19->7/2), ~6 weeks (7/2->8/14). Say 'roughly every six to ten weeks', or simply 'five releases in the eight months to August 2026'. Also, the package targets netstandard2.1, net9.0 and net10.0 in addition to the two the claim names.

### A6.31  [yes]

**Claim.** Martin Fowler's snowflake-server diagnosis applies directly to a long-lived staging environment maintained by hand: 'The first problem with a snowflake server is that it's difficult to reproduce... You can't easily mirror your production environment for testing.'

**Limit or threshold asserted.** Published 2012-07-10

- Source: martinfowler.com  -  bliki: SnowflakeServer
- URL: <https://martinfowler.com/bliki/SnowflakeServer.html>
- Accessed: 2026-08-31
- Confidence: verified

### A6.32  [partially]

**Claim.** nip.io is now hosted by the sslip.io team and both provide wildcard-DNS-for-any-IP, which is one way to give a Host-header-resolved app a dev environment without owning DNS  -  but they do not support wildcard certificates, are unsigned (no DNSSEC), share Let's Encrypt rate limits with all other users, and offer no SLA.

**Limit or threshold asserted.** No wildcard certificate support; no DNSSEC; shared LE rate limits. Maintenance-status claim rests on secondary sources.

- Source: nip.io / sslip.io; ETOOBUSY comparison
- URL: <https://nip.io/>
- Second source: <https://github.polettix.it/ETOOBUSY/2022/10/09/nip-io-sslip-io/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Shared operation and the no-wildcard-certificate limitation are now settled by the primary source (upgrade from 'secondary sources only'). Two sub-claims are NOT supported by the page: 'unsigned (no DNSSEC)' and 'no SLA'  -  both are absent; drop them or source them elsewhere. The rate-limit framing also needs nuance: Let's Encrypt has raised nip.io's registered-domain limit to 250,000 certificates per period rather than leaving it at the standard 50, though that pool is still shared across all users of the domain  -  which is the point the claim is actually making.

### A6.33  [partially]

**Claim.** A second full application stack cannot run on the existing VM. The eight long-running services' declared memory caps total 17,336 MB (~16.93 GB) against 16 GB of physical RAM  -  the single stack is already over-committed by ~0.93 GB  -  and only ~9.7 GB of 48 GB disk is free, which will not hold a second SQL data volume plus its backups.

**Limit or threshold asserted.** 17,336 MB of caps vs 16 GB RAM; ~9.7 GB of 48 GB disk free; MSSQL_MEMORY_LIMIT_MB 7168

- Source: Arithmetic on the memory caps and disk figures supplied in the system brief
- URL: n/a  -  computed from brief-supplied figures (api 2048, authserver 1500, packet-renderer 1500, sql-server 10240, redis 512, minio 1024, angular 256, proxy 256 MB)
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The arithmetic is exact and the disk figure is plausible, but the inference is overstated in two ways. (1) A Docker mem_limit is a ceiling, not a reservation  -  caps summing above physical RAM does not establish that the running stack is 'already over-committed'; it establishes only that simultaneous peak usage could exceed RAM. The stated MSSQL_MEMORY_LIMIT_MB of 7168 against a 10240 MB container cap is itself evidence that headroom exists between cap and actual use. Restate as 'the sum of declared ceilings exceeds physical RAM, so the caps cannot all be honoured simultaneously' and support the stronger claim with measured RSS if it is to carry weight. (2) The underlying figures come from the brief and were not independently verified here; label them as supplied, not measured.

### A6.34  [yes]

**Claim.** I could not find an OCR/HHS enforcement action specifically citing PHI exposed in a test or development environment. I searched HHS press releases and resolution agreements and found adjacent cases (misconfigured servers, PACS servers, software-vendor breaches) but nothing naming a non-production environment. The legal exposure argument therefore rests on 164.308(a)(1)(ii)(A)'s scope language and PM-25, not on precedent.

- Source: HHS.gov Resolution Agreements index (searched, no matching case found)
- URL: <https://www.hhs.gov/hipaa/for-professionals/compliance-enforcement/agreements/index.html>
- Accessed: 2026-08-31
- Confidence: unverified
- Verifier note: The negative finding is honest and reproduced. Keep it labelled 'absence of evidence in the resolution-agreements index' rather than 'no such enforcement exists'  -  the index does not cover the OCR breach portal or informal resolutions, which are the more likely place such a case would surface.

---

## Area: network-exposure

Verification verdict for this area: **material-errors** (52 claims checked)

### A7.1  [yes]

**Claim.** nginx does NOT preserve the client Host header by default when proxying. The documented default is 'proxy_set_header Host $proxy_host', i.e. the upstream's name from the proxy_pass directive. For HTTP/2 the ':authority' pseudo-header is sent with the $proxy_host value by default.

**Limit or threshold asserted.** proxy_set_header Host $proxy_host; proxy_set_header Connection close;

- Source: nginx documentation  -  Module ngx_http_proxy_module
- URL: <https://nginx.org/en/docs/http/ngx_http_proxy_module.html>
- Second source: <https://httpd.apache.org/docs/2.4/mod/mod_proxy.html>
- Accessed: 2026-08-31
- Confidence: verified

### A7.2  [yes]

**Claim.** nginx proxy_set_header directives are inherited from the previous configuration level ONLY IF no proxy_set_header directive is defined at the current level  -  so adding any single proxy_set_header inside a location silently discards the inherited Host line.

**Limit or threshold asserted.** All-or-nothing inheritance per configuration level

- Source: nginx documentation  -  Module ngx_http_proxy_module
- URL: <https://nginx.org/en/docs/http/ngx_http_proxy_module.html>
- Accessed: 2026-08-31
- Confidence: verified

### A7.3  [partially]

**Claim.** When a proxy_pass value contains variables and the address is a domain name, the name is resolved using a resolver rather than being fixed at worker start  -  which is the documented mechanism behind the 'stale container IP' trap the team has already hit for the object-store block.

- Source: nginx documentation  -  Module ngx_http_proxy_module
- URL: <https://nginx.org/en/docs/http/ngx_http_proxy_module.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The page documents the resolver path but omits two things the claim asserts. (a) It states the name is FIRST searched among the described upstream server groups and only falls back to the resolver if not found  -  a variable proxy_pass that matches an upstream{} block name is not re-resolved. (b) The page never states the contrasting behaviour (that a literal, variable-free proxy_pass domain name is resolved once at configuration load and cached for the worker's life), nor anything about container IP staleness. State the design requirement as 'the edge must re-resolve backend names at request time' and cite the resolver directive docs for TTL behaviour, not this sentence alone.

### A7.4  [yes]

**Claim.** An nginx server_name wildcard matches SEVERAL name parts: '*.example.org' matches not only <www.example.org> but <www.sub.example.org> as well. Matching precedence is exact name, then longest wildcard starting with an asterisk, then longest wildcard ending with an asterisk, then first matching regular expression.

**Limit or threshold asserted.** Multi-label wildcard match; 4-step precedence order

- Source: nginx documentation  -  Server names
- URL: <https://nginx.org/en/docs/http/server_names.html>
- Accessed: 2026-08-31
- Confidence: verified

### A7.5  [yes]

**Claim.** If the Host value matches no server name, or the request contains no Host field, nginx routes the request to the default server for that port; without an explicit default_server parameter the default server is the FIRST server block for that port. default_server is a property of the listen directive, not of server_name.

**Limit or threshold asserted.** Fallback = first server block for the port

- Source: nginx documentation  -  How nginx processes a request
- URL: <https://nginx.org/en/docs/http/request_processing.html>
- Second source: <https://nginx.org/en/docs/http/server_names.html>
- Accessed: 2026-08-31
- Confidence: verified

### A7.6  [yes]

**Claim.** nginx returns 421 Misdirected Request when a client requests a server name different from the one negotiated in TLS, which occurs when HTTP/2 clients coalesce connections across hostnames covered by one wildcard certificate on one IP. The nginx maintainer states this is correct specification behaviour and that browsers should open a new connection on receiving 421.

**Limit or threshold asserted.** Log line: 'client attempted to request the server name different from that one was negotiated'

- Source: nginx Trac ticket #1252  -  Multiplexing different hosts into one HTTP/2 connection leads to 421
- URL: <https://trac.nginx.org/nginx/ticket/1252>
- Second source: <https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/421>
- Accessed: 2026-08-31
- Confidence: verified

### A7.7  [yes]

**Claim.** nginx limit_req_zone accepts a key composed of variables including $host and $server_name, and limit_req_status defaults to 503 (settable to 429). This makes per-office and per-client rate limiting expressible at the single point both the API process and the AuthServer process traverse.

**Limit or threshold asserted.** limit_req_status default 503

- Source: nginx documentation  -  Module ngx_http_limit_req_module
- URL: <https://nginx.org/en/docs/http/ngx_http_limit_req_module.html>
- Accessed: 2026-08-31
- Confidence: verified

### A7.8  [yes]

**Claim.** nginx client-certificate verification (ssl_verify_client) defaults to off and accepts on | optional | optional_no_ca, with the trust anchor set by ssl_client_certificate or ssl_trusted_certificate. It is available per server block, so mTLS can be applied to an administrative hostname without affecting tenant traffic.

**Limit or threshold asserted.** Default: ssl_verify_client off;

- Source: nginx documentation  -  Module ngx_http_ssl_module
- URL: <https://nginx.org/en/docs/http/ngx_http_ssl_module.html>
- Accessed: 2026-08-31
- Confidence: verified

### A7.9  [yes]

**Claim.** Apache mod_proxy also rewrites Host by default: ProxyPreserveHost defaults to Off, meaning the proxy uses the hostname from the ProxyPass line rather than the incoming Host header. The documentation notes it is 'mostly useful in special configurations like proxied mass name-based virtual hosting'.

**Limit or threshold asserted.** ProxyPreserveHost Off (default)

- Source: Apache HTTP Server documentation  -  mod_proxy
- URL: <https://httpd.apache.org/docs/2.4/mod/mod_proxy.html>
- Accessed: 2026-08-31
- Confidence: verified

### A7.10  [yes]

**Claim.** Traefik preserves the client Host header by default: passHostHeader is true. Traefik service health checks additionally support a 'hostname' field used as the Host header value on checks, plus arbitrary custom headers.

**Limit or threshold asserted.** passHostHeader default true

- Source: Traefik documentation  -  HTTP Services / load balancing
- URL: <https://doc.traefik.io/traefik/reference/routing-configuration/http/load-balancing/service/>
- Accessed: 2026-08-31
- Confidence: verified

### A7.11  [partially]

**Claim.** HAProxy forwards the client request including its Host header by default, and HTTP health checks can carry an explicit Host via 'http-check send ... hdr Host <name>'. Without http-check send, checks issue an OPTIONS request to '/'.

**Limit or threshold asserted.** http-check send meth HEAD uri /healthz ver HTTP/1.1 hdr Host test.local

- Source: HAProxy configuration tutorials  -  Health checks
- URL: <https://www.haproxy.com/documentation/haproxy-configuration-tutorials/reliability/health-checks/>
- Second source: <https://docs.haproxy.org/3.2/configuration.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Only the health-check half is settled by this page. The first clause  -  that HAProxy forwards the client's Host header by default when proxying  -  is not stated anywhere on this health-checks tutorial; cite the HAProxy configuration manual (http-request set-header / option forwardfor sections) for proxy-path Host behaviour instead. The page also does not state the default HTTP version used when no http-check send is configured, and the example path is /health, not /healthz.

### A7.12  [partially]

**Claim.** Envoy modifies the :authority header only when a host-rewrite specifier is configured (host_rewrite_literal, auto_host_rewrite, host_rewrite_header, host_rewrite_path_regex); the original value is placed in x-envoy-original-host and appended to x-forwarded-host only when the authority is modified. The documentation does not state the no-rewrite default explicitly, but preservation is the necessary implication.

**Limit or threshold asserted.** n/a  -  default preservation is implied, not stated

- Source: Envoy documentation  -  HTTP route components / HTTP header manipulation
- URL: <https://www.envoyproxy.io/docs/envoy/latest/api-v3/config/route/v3/route_components.proto>
- Second source: <https://www.envoyproxy.io/docs/envoy/latest/configuration/http/http_conn_man/headers>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Two conditions the claim drops, both design-relevant. x-forwarded-host is appended ONLY when append_x_forwarded_host is explicitly set to true  -  it is not automatic on rewrite; and x-envoy-original-host is suppressed when suppress_envoy_headers is true. The claim's own caveat is right that no default-preservation statement exists on this page: preservation is inferred from the absence of a rewrite specifier, not documented.

### A7.13  [yes]

**Claim.** Kong Gateway defaults to REWRITING the upstream Host: preserve_host defaults to false, in which case the upstream Host header is the Gateway Service's configured host rather than the client's.

**Limit or threshold asserted.** preserve_host default false

- Source: Kong documentation  -  Routes
- URL: <https://developer.konghq.com/gateway/entities/route/>
- Second source: <https://developer.konghq.com/deck/gateway/defaults/>
- Accessed: 2026-08-31
- Confidence: partial

### A7.14  [yes]

**Claim.** Azure API Management overrides the Host sent to the backend with the host component of the API's web service URL by default; preserving the client Host requires an explicit inbound set-header policy. Azure Front Door preserves the request hostname only when the origin host header is left blank (originHostHeader = null). Azure Application Gateway preserves it only when host-name override is turned off.

**Limit or threshold asserted.** originHostHeader = null; hostName = null and pickHostNameFromBackendAddress = false

- Source: Microsoft Learn  -  Host name preservation (Azure Architecture Center)
- URL: <https://learn.microsoft.com/en-us/azure/architecture/best-practices/host-name-preservation>
- Second source: <https://learn.microsoft.com/en-us/azure/frontdoor/origin>
- Accessed: 2026-08-31
- Confidence: verified

### A7.15  [yes]

**Claim.** Microsoft's own architecture guidance states 'In most cases, you shouldn't override the host name. Pass the incoming host name unmodified to the back end', and specifically that in multitenant scenarios 'you can't statically define a single domain'. The same article warns 'Never use the value of the host in a security mechanism. The browser or another user agent provides the value, and a user can change it.'

- Source: Microsoft Learn  -  Host name preservation (Azure Architecture Center)
- URL: <https://learn.microsoft.com/en-us/azure/architecture/best-practices/host-name-preservation>
- Accessed: 2026-08-31
- Confidence: verified

### A7.16  [yes]

**Claim.** A next-generation firewall placed between reverse proxy and backend may explicitly verify that the HTTP Host header resolves to the target IP address, requiring split-horizon DNS so the public hostname resolves to the backend from the firewall's vantage point.

- Source: Microsoft Learn  -  Host name preservation (Azure Architecture Center)
- URL: <https://learn.microsoft.com/en-us/azure/architecture/best-practices/host-name-preservation>
- Accessed: 2026-08-31
- Confidence: verified

### A7.17  [partially]

**Claim.** AWS Application Load Balancer HTTP health checks send a Host header containing the target's private IP address, plus the health check port when it is not the default (e.g. 'Host: 10.0.0.10:8080'). AWS states that 'some applications require additional configuration to respond to health checks, such as a virtual host configuration to respond to the HTTP host header sent by the load balancer'. The health check settings list contains no Host option. User-Agent is ELB-HealthChecker/2.0.

**Limit or threshold asserted.** Host: <target private IP>[:health check port]; no configurable Host setting

- Source: AWS documentation  -  Troubleshoot your Application Load Balancers
- URL: <https://docs.aws.amazon.com/elasticloadbalancing/latest/application/load-balancer-troubleshooting.html>
- Second source: <https://docs.aws.amazon.com/elasticloadbalancing/latest/application/target-group-health-checks.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Everything about the Host value and User-Agent is confirmed verbatim. The sub-claim 'the health check settings list contains no Host option' is NOT on this troubleshooting page  -  that settings table lives at docs.aws.amazon.com/elasticloadbalancing/latest/application/target-group-health-checks.html and must be cited separately if the absence of a Host setting is load-bearing.

### A7.18  [yes]

**Claim.** AWS Network Load Balancer HTTP/HTTPS health checks send a Host header containing the IP address of the LOAD BALANCER NODE and the listener port  -  not the target's address. The configurable settings are Protocol, Port, Path, Timeout, Interval, Healthy/Unhealthy threshold and Matcher; there is no Host setting.

**Limit or threshold asserted.** Host = load balancer node IP + listener port; no Host setting in the settings table

- Source: AWS documentation  -  Health checks for Network Load Balancer target groups
- URL: <https://docs.aws.amazon.com/elasticloadbalancing/latest/network/target-group-health-checks.html>
- Accessed: 2026-08-31
- Confidence: verified

### A7.19  [yes]

**Claim.** Azure Application Gateway's DEFAULT health probe uses '<protocol>://127.0.0.1:<port>/'  -  the Host is literally 127.0.0.1 unless a hostname is set in Backend Settings. A CUSTOM probe exposes a 'Host' field; in the v1 SKU it is used only as the probe's host header, and in the v2 SKU it is used both as host header AND as SNI.

**Limit or threshold asserted.** Default probe URL <protocol>://127.0.0.1:<port>/; default host 127.0.0.1

- Source: Microsoft Learn  -  Health monitoring overview for Azure Application Gateway
- URL: <https://learn.microsoft.com/en-us/azure/application-gateway/application-gateway-probe-overview>
- Accessed: 2026-08-31
- Confidence: verified

### A7.20  [yes]

**Claim.** Azure guidance states that because health probes are sent outside the context of an incoming request they 'can't dynamically determine the correct host name', and instructs operators to create a custom probe and explicitly specify the host name.

- Source: Microsoft Learn  -  Host name preservation (Azure Architecture Center)
- URL: <https://learn.microsoft.com/en-us/azure/architecture/best-practices/host-name-preservation>
- Accessed: 2026-08-31
- Confidence: verified

### A7.21  [partially]

**Claim.** Google Cloud health checks using HTTP, HTTPS or HTTP/2 allow specifying an HTTP Host header via a --host flag; the gcloud reference states that if the host is omitted the IP address of the load balancer's forwarding rule is used.

**Limit or threshold asserted.** --host flag; default = forwarding rule IP address

- Source: Google Cloud documentation  -  Health checks overview / gcloud compute health-checks create http
- URL: <https://docs.cloud.google.com/load-balancing/docs/health-check-concepts>
- Second source: <https://docs.cloud.google.com/sdk/gcloud/reference/compute/health-checks/create/http>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Only the existence of --host is settled. The stated default  -  'if the host is omitted the IP address of the load balancer's forwarding rule is used'  -  appears nowhere on this page; the claim itself attributes it to 'the gcloud reference', a different URL that was not cited and could not be retrieved (cloud.google.com/sdk/... 301-redirects to docs.cloud.google.com and the flag table did not render). Do not state that default as verified; either cite gcloud compute health-checks create http directly or drop the default. For the design, what matters is only that an operator-specified Host is available, which is confirmed.

### A7.22  [yes]

**Claim.** Kubernetes HTTP probes send to the Pod's IP address unless overridden by httpGet.host; the documentation states that if the pod relies on virtual hosts you should NOT use the host field but should set the Host header in httpHeaders instead. The kubelet also sends User-Agent (kube-probe/<version>) and Accept.

**Limit or threshold asserted.** httpGet.host defaults to Pod IP; use httpHeaders Host for virtual hosts

- Source: Kubernetes documentation  -  Liveness, Readiness, and Startup Probes
- URL: <https://kubernetes.io/docs/concepts/workloads/pods/probes/>
- Accessed: 2026-08-31
- Confidence: partial

### A7.23  [partially]

**Claim.** Cloudflare sends the original custom hostname as the Host header to the origin by default. An Origin Rule that overrides the Host header ALSO updates the SNI value of the request to the same value  -  i.e. Host and SNI are coupled by that product's override.

- Source: Cloudflare documentation  -  Origin Rules settings / Override HTTP Host headers
- URL: <https://developers.cloudflare.com/rules/origin-rules/features/>
- Second source: <https://developers.cloudflare.com/load-balancing/additional-options/override-http-host-headers/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The SNI-coupling half is confirmed verbatim and is the design-relevant part. The first half  -  that the original custom hostname is sent to the origin as Host by default  -  is not on this page; it belongs to the Cloudflare custom-hostnames / SSL for SaaS docs. Cite it there or drop it, and do not present the default as verified.

### A7.24  [partially]

**Claim.** CloudFront-class CDNs forward the viewer Host only when an origin request policy includes it (e.g. Managed-AllViewer); when the viewer Host is removed, CloudFront adds a new Host header containing the origin's domain name. Certain origin types require the origin domain in Host and break if the viewer Host is forwarded.

- Source: AWS documentation  -  Use managed origin request policies (CloudFront)
- URL: <https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/using-managed-origin-request-policies.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The mechanism named in the claim is inverted. The policy that produces the described behaviour is AllViewerExceptHostHeader (which strips Host), not 'Managed-AllViewer'. The page also does not establish the claim's premise that the viewer Host is forwarded ONLY when an origin request policy includes it  -  the no-policy default behaviour is documented elsewhere (origin request policy / cache behaviour docs), not here. Restate as: some CDNs strip the viewer Host and substitute the origin domain, and some origin types break if the viewer Host is forwarded.

### A7.25  [yes]

**Claim.** For CDN backend services the default cache key is the complete request URI, which includes the host and protocol. The documentation warns that force-caching 'might not be appropriate if the backend serves private, per-user (user identifiable) content, such as dynamic HTML or API responses' and directs operators to set Cache-Control: private or no-store on responses that must not be stored.

**Limit or threshold asserted.** Default cache key for backend services = complete request URI (includes host and protocol); backend buckets default = URI without protocol or host

- Source: Google Cloud documentation  -  Caching overview (Cloud CDN)
- URL: <https://docs.cloud.google.com/cdn/docs/caching>
- Accessed: 2026-08-31
- Confidence: verified

### A7.26  [yes]

**Claim.** A TLS wildcard certificate matches exactly ONE label, and the wildcard character must appear only as the complete content of the leftmost label. Therefore *.example.com does NOT cover office.api.example.com. RFC 9525 (published 2023) obsoletes RFC 6125.

**Limit or threshold asserted.** 6.3: 'A wildcard in a presented identifier can only match one label in a reference identifier'; wildcard must be the complete left-most label

- Source: RFC 9525  -  Service Identity in TLS, 6.3
- URL: <https://www.rfc-editor.org/rfc/rfc9525.html>
- Second source: <https://cabforum.org/working-groups/server/baseline-requirements/requirements/>
- Accessed: 2026-08-31
- Confidence: verified

### A7.27  [partially]

**Claim.** A DNS wildcard, unlike a TLS wildcard, CAN synthesise answers for names with more than one extra label  -  but only while no node exists at the intermediate label. RFC 4592 2.2.1's own example: a query for foo.bar.example. is answered from *.example. 'because bar.example. does not exist, but the wildcard does'. Conversely, a query for _telnet._tcp.host1.example. gets no wildcard synthesis because '_tcp.host1.example. exists (without data)'  -  i.e. an EMPTY NON-TERMINAL blocks wildcard synthesis.

**Limit or threshold asserted.** 2.2.1 worked example; empty non-terminals block synthesis

- Source: RFC 4592  -  The Role of Wildcards in the Domain Name System, 2.2.1 (ICANN annotated edition)
- URL: <https://rfc-annotations.research.icann.org/rfc4592.html>
- Second source: <https://www.rfc-editor.org/rfc/rfc4592.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The technical content is correct, but the cited host (rfc-annotations.research.icann.org) is not reliably reachable  -  it returned 503 Service Unavailable on repeated fetches. Replace the citation with the canonical <https://www.rfc-editor.org/rfc/rfc4592.html>, where both worked examples and the empty-non-terminal rule appear verbatim in section 2.2.1.

### A7.28  [partially]

**Claim.** Creating the DNS-01 challenge record needed to obtain a *.api.<base> certificate places a TXT record at _acme-challenge.api.<base>, which creates the empty non-terminal api.<base>. By RFC 4592 2.2.1 that empty non-terminal becomes the closest encloser for office.api.<base> and blocks synthesis from *.<base>  -  so unless a wildcard exists at *.api.<base>, the act of setting up the certificate can stop every office API hostname from resolving.

**Limit or threshold asserted.** n/a  -  inference from a verified RFC rule; not observed in this system

- Source: Derived from RFC 4592 2.2.1 (empty non-terminal case) applied to ACME DNS-01 record placement
- URL: <https://rfc-annotations.research.icann.org/rfc4592.html>
- Second source: <https://letsencrypt.org/docs/challenge-types/>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Reachability aside, this is an inference the RFC does not make and it is stated more absolutely than it holds. The rule is right (an empty non-terminal at api.<base> blocks *.<base> from synthesising office.api.<base>), but two qualifiers are missing: (a) the ENT is created by ANY descendant record under api.<base>, so in practice a deployment already serving office.api.<base> will normally already have such records  -  the ACME TXT is one cause among many, not the cause; (b) the effect is scoped to the zone that is authoritative for <base>, and disappears the moment a wildcard or explicit record exists at api.<base>. Present it as a modelled failure mode to test in the zone, not an observed behaviour. Cite the canonical rfc-editor.org URL.

### A7.29  [yes]

**Claim.** Wildcard certificates can only be validated via the DNS-01 challenge; HTTP-01 cannot issue wildcards. This makes a DNS provider with an automatable API a hard requirement for any multi-level-wildcard naming scheme.

**Limit or threshold asserted.** DNS-01 required for wildcards; TXT record at _acme-challenge.<DOMAIN>

- Source: Let's Encrypt  -  Challenge Types
- URL: <https://letsencrypt.org/docs/challenge-types/>
- Second source: <https://datatracker.ietf.org/doc/rfc8555/>
- Accessed: 2026-08-31
- Confidence: partial

### A7.30  [partially]

**Claim.** Maximum public TLS certificate validity is stepping down on a published schedule under CA/Browser Forum ballot SC-081v3 (passed April 2025): 200 days from 15 March 2026, 100 days from 15 March 2027, 47 days from 15 March 2029. Let's Encrypt is separately moving default lifetimes from 90 days toward 45 days, roughly doubling renewal frequency; renewals remain exempt from issuance rate limits, and a single certificate may carry up to 100 identifiers.

**Limit or threshold asserted.** 200 days from 2026-03-15; 100 days from 2027-03-15; 47 days from 2029-03-15; LE 90->64->45 days; 100 identifiers per certificate

- Source: CA/Browser Forum Ballot SC-081v3 and Let's Encrypt  -  Shorter Certificate Lifetimes and Rate Limits
- URL: <https://cabforum.org/2025/04/11/ballot-sc081v3-introduce-schedule-of-reducing-validity-and-data-reuse-periods/>
- Second source: <https://letsencrypt.org/2026/02/24/rate-limits-45-day-certs>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Three problems. (1) The specific 200/100/47-day dates are stated from memory: the cited ballot page gives only the March 2026 -> March 2029 envelope and the 398->47 endpoint, so cite the SC-081v3 redline/Baseline Requirements section 6.3.2 if the exact step dates are load-bearing. (2) The Let's Encrypt '90 -> 64 -> 45 days' progression is unsupported by any cited source and does not match Let's Encrypt's published direction, which is a 90-day default plus an opt-in short-lived (roughly 6-day) certificate profile  -  do not plan renewal cadence on a '45-day default'. (3) The renewal exemption is narrower than stated: per letsencrypt.org/docs/rate-limits only ARI-driven renewals are 'exempt from all rate limits'; non-ARI renewals with an identical identifier set are exempt only from New Orders per Account and New Certificates per Registered Domain, and remain subject to the duplicate-identifier-set and authorization-failure limits. The 100-identifier figure is correct but comes from the rate-limits page, not this ballot.

### A7.31  [yes]

**Claim.** ASP.NET Core's Forwarded Headers Middleware sets HttpContext.Request.Host from the X-Forwarded-Host header (persisting the old value in X-Original-Host). It is NOT enabled by default and its default ForwardedHeaders value is ForwardedHeaders.None. KnownProxies defaults to a single entry for IPv6 loopback and KnownNetworks to loopback/8; ForwardedHeadersOptions.AllowedHosts defaults to an EMPTY list, which allows ALL hosts.

**Limit or threshold asserted.** Default ForwardedHeaders.None; AllowedHosts default empty = all allowed; ForwardLimit default 1

- Source: Microsoft Learn  -  Configure ASP.NET Core to work with proxy servers and load balancers
- URL: <https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer>
- Accessed: 2026-08-31
- Confidence: verified

### A7.32  [yes]

**Claim.** Microsoft Security Advisory CVE-2018-0787 describes an elevation-of-privilege vulnerability arising when an ASP.NET Core application is 'hosted behind a proxy which does not validate or restrict host headers to known good values', and notes that Kestrel itself cannot validate host headers. Mitigation is a validating proxy or Host Filtering Middleware.

**Limit or threshold asserted.** CVE-2018-0787

- Source: ASP.NET Core Announcements  -  Microsoft Security Advisory CVE-2018-0787
- URL: <https://github.com/aspnet/Announcements/issues/295>
- Second source: <https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer>
- Accessed: 2026-08-31
- Confidence: verified

### A7.33  [partially]

**Claim.** ASP.NET Core Host Filtering Middleware is disabled by default and is enabled by defining an AllowedHosts key; the default project templates ship "AllowedHosts": "*", which permits all non-empty hosts. Subdomain wildcards are supported but do not match the root domain.

**Limit or threshold asserted.** Template default "AllowedHosts": "*"

- Source: Microsoft Learn  -  Host filtering with ASP.NET Core Kestrel web server
- URL: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/host-filtering>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Only the first half is settled here. This page never mentions the project template default of "AllowedHosts": "*", nor the wildcard-matching semantics. The wildcard text the claim paraphrases ('A top-level wildcard * allows all non-empty hosts' and 'Subdomain wildcards are permitted but don't match the root domain. For example, *.contoso.com matches the subdomain foo.contoso.com but not the root domain contoso.com') is on the proxy-load-balancer page and describes ForwardedHeadersOptions.AllowedHosts  -  a different option on a different middleware. Do not transfer it to HostFilteringOptions without verifying against the HostFilteringOptions API reference, and cite the template's appsettings.json separately.

### A7.34  [partially]

**Claim.** ABP's ASP.NET Core multi-tenancy module registers four tenant resolve contributors in order  -  QueryString, Route, Header, Cookie  -  with the parameter name __tenant by default; the core module additionally contributes CurrentUserTenantResolveContributor. Adding a domain resolver supplements rather than replaces these.

**Limit or threshold asserted.** Four resolvers; __tenant default key

- Source: ABP Framework source  -  AbpAspNetCoreMultiTenancyModule.cs and ABP multi-tenancy documentation
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.AspNetCore.MultiTenancy/Volo/Abp/AspNetCore/MultiTenancy/AbpAspNetCoreMultiTenancyModule.cs>
- Second source: <https://abp.io/docs/latest/framework/architecture/multi-tenancy>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The four contributors and their registration order are confirmed exactly. Two sub-claims are not: this file contains no '__tenant' literal (the default key lives in MultiTenancyConsts / TenantResolverConsts), and it does not register CurrentUserTenantResolveContributor (that comes from AbpMultiTenancyModule, a separate file). Also note this is the 'dev' branch  -  a moving target; pin to a tag or commit SHA so the citation stays valid, since the whole design decision rests on this registration list.

### A7.35  [partially]

**Claim.** ABP's DomainTenantResolveContributor sets context.Handled = true unconditionally  -  regardless of whether the domain format matched  -  which causes the resolver loop to exit early and prevents subsequent contributors from running. This, rather than any explicit removal, is what neutralises the __tenant resolvers in a domain-resolver deployment.

**Limit or threshold asserted.** context.Handled = true set unconditionally

- Source: ABP GitHub issue #7968  -  DomainTenantResolveContributor will affect other TenantResolveContributor
- URL: <https://github.com/abpframework/abp/issues/7968>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The issue says exactly what the claim says it says  -  but it is a CLOSED issue, and the page shows no fix or PR link. The claim asserts this as ABP's current runtime behaviour, which the issue cannot establish: a closed bug report is evidence the behaviour was reported, not that it persists. Before relying on 'the domain resolver neutralises the __tenant resolvers', read the current DomainTenantResolveContributor source at a pinned commit and confirm whether Handled is still set unconditionally, or verify empirically by sending ?__tenant= against a domain-resolver deployment. Note also that if the behaviour was fixed, the design premise inverts: the __tenant query/route/header/cookie selectors would still be live and would need explicit removal or edge stripping.

### A7.36  [yes]

**Claim.** Host header attack techniques for bypassing validation include: absolute URLs in the request line, duplicate Host headers (implementations differ on which wins), line-wrapped/indented headers, and the override headers X-Forwarded-Host, X-Host, X-Forwarded-Server, X-HTTP-Host-Override and Forwarded. Routing-based SSRF exploits intermediary components that can be manipulated into misrouting requests.

- Source: PortSwigger Web Security Academy  -  HTTP Host header attacks / Exploiting
- URL: <https://portswigger.net/web-security/host-header/exploiting>
- Second source: <https://portswigger.net/web-security/host-header>
- Accessed: 2026-08-31
- Confidence: verified

### A7.37  [partially]

**Claim.** OWASP's Web Security Testing Guide instructs validating the Host header on every incoming request and rejecting unexpected hostnames, and lists impacts including dispatching requests to the first virtual host on the list, redirect to an attacker-controlled domain, cache poisoning, password-reset manipulation, and access to virtual hosts not intended to be externally accessible.

- Source: OWASP Web Security Testing Guide  -  Testing for Host Header Injection
- URL: <https://owasp.org/www-project-web-security-testing-guide/latest/4-Web_Application_Security_Testing/07-Input_Validation_Testing/17-Testing_for_Host_Header_Injection>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The impacts list is confirmed verbatim. The remediation half is not: WSTG-INPV-17 is a testing procedure and contains no instruction to 'validate the Host header on every incoming request and reject unexpected hostnames'. Attributing that prescription to OWASP from this page overstates it  -  either drop the attribution and state the control in your own voice, or cite a source that actually prescribes it (e.g. the OWASP Cheat Sheet Series, or CVE-2018-0787's mitigation text already cited at claim 32).

### A7.38  [could-not-check]

**Claim.** Academic measurement found deployed HTTP implementations parse and interpret the Host header inconsistently: 21 of 33 implementations tested did not normalise requests sufficiently when forwarding them upstream, using multiple Host headers, space-surrounded Host headers and absolute-URI request targets; around 97% of users served by transparent caches were subject to cache-poisoning attacks.

**Limit or threshold asserted.** 21 of 33 implementations; ~97% of transparent-cache users

- Source: Host of Troubles: Multiple Host Ambiguities in HTTP Implementations (ACM CCS 2016)
- URL: <https://dl.acm.org/doi/10.1145/2976749.2978394>
- Second source: <https://hostoftroubles.com/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Neither number is verified. dl.acm.org is paywalled/bot-blocked, so this DOI cannot serve as a checkable citation in the deliverable. Either cite an openly retrievable copy of the paper (the CCS 2016 'Host of Troubles' author PDF) and re-verify both figures against its abstract and evaluation section, or drop the two statistics and keep only the qualitative point  -  that deployed HTTP implementations disagree on Host header parsing  -  which is independently supported by claim 36's PortSwigger material. Do not carry precise-sounding figures no reader can check.

### A7.39  [partially]

**Claim.** Virtual host confusion arises because routing decisions rely on unauthenticated data (IP, port, SNI, Host); when the Host header matches no configured virtual host, systems fall back to the default vhost or the first one, and TLS sessions/tickets opened for one virtual host can be resumed against another unless the server name is bound into the ticket. Mitigations include strict Host/SNI consistency checking and an explicit block-by-default routing action.

- Source: Vhost Confusion (Tempesta Technologies knowledge base), building on Delignat-Lavaud & Bhargavan, Black Hat 2014 / WWW'15
- URL: <https://tempesta-tech.com/knowledge-base/Vhost-Confusion/>
- Second source: <https://www.blackhat.com/docs/us-14/materials/us-14-Delignat-The-BEAST-Wins-Again-Why-TLS-Keeps-Failing-To-Protect-HTTP-wp.pdf>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The mechanism and the TLS-ticket resumption point are supported, but this is a vendor knowledge-base page and the 'mitigations' it lists are that vendor's own product features (http_chain, http_strict_host_checking, ticket binding), not neutral guidance. Use it as evidence of the behaviour class only; state the mitigations as capability requirements in your own voice (strict Host/SNI consistency check, explicit terminal default route, server-name-bound session tickets) rather than as a cited recommendation, and prefer a neutral source for the routing guidance if one is available.

### A7.40  [yes]

**Claim.** HTTP request smuggling exploits front-end/back-end disagreement about request boundaries; because the front end 'commonly reuses the same connection for multiple requests', part of an attacker's request can be interpreted by the back end as the start of the NEXT request and prepended to it, interfering with how that request is processed.

- Source: PortSwigger Web Security Academy  -  HTTP request smuggling
- URL: <https://portswigger.net/web-security/request-smuggling>
- Accessed: 2026-08-31
- Confidence: verified

### A7.41  [no]

**Claim.** Istio sidecars enable auto_sni and auto_san_validation by default: when no explicit SNI is set in a DestinationRule, the transport socket SNI for new upstream connections is derived from the downstream HTTP host/authority header, and certificate SAN validation follows from the same value.

**Limit or threshold asserted.** auto_sni and auto_san_validation enabled by default

- Source: Istio documentation  -  Understanding TLS Configuration
- URL: <https://istio.io/latest/docs/ops/configuration/traffic-management/tls-configuration/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: This is asserted entirely from memory against a page that does not discuss it. In Istio these behaviours are controlled by istiod feature flags (ENABLE_AUTO_SNI and ENABLE_AUTO_SAN_VALIDATION), whose defaults have changed across releases  -  so 'enabled by default' is version-dependent and cannot be stated flatly for 2026. If the design depends on upstream SNI tracking the client Host, cite the istiod configuration/feature-flag reference for the specific Istio version in use and verify empirically; otherwise remove the claim. Treating SNI-follows-Host as a given is a live failure mode for the mTLS and certificate-matching requirements.

### A7.42  [partially]

**Claim.** nginx's stream ssl_preread module extracts the SNI server name from the ClientHello without terminating TLS, exposing $ssl_preread_server_name for L4 routing. It is not built by default (--with-stream_ssl_preread_module) and cannot be used on a listener with the ssl parameter.

**Limit or threshold asserted.** Requires --with-stream_ssl_preread_module; incompatible with 'listen ... ssl'

- Source: nginx documentation  -  Module ngx_stream_ssl_preread_module
- URL: <https://nginx.org/en/docs/stream/ngx_stream_ssl_preread_module.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The first three parts are confirmed verbatim. The incompatibility clause is not documented on this page  -  nginx nowhere states that ssl_preread 'cannot be used on a listener with the ssl parameter'. It is a true consequence of the design (preread inspects the raw ClientHello at the preread phase, before any TLS termination the ssl parameter would perform) but it is an inference, not a documented limit. State it as a mechanism consequence, or verify it against a configuration test, rather than presenting it as a documented constraint.

### A7.43  [could-not-check]

**Claim.** CISA Binding Operational Directive 23-02 requires federal civilian agencies either to remove identified networked management interfaces from the public internet or to protect them with Zero Trust capabilities that implement a policy enforcement point SEPARATE FROM THE INTERFACE ITSELF, within 14 days of discovery. A networked management interface is defined as a device interface accessible over network protocols meant exclusively for authorised users to perform administrative activities.

**Limit or threshold asserted.** 14 days from discovery; policy enforcement point separate from the interface

- Source: CISA  -  BOD 23-02: Mitigating the Risk from Internet-Exposed Management Interfaces
- URL: <https://www.cisa.gov/news-events/directives/binding-operational-directive-23-02>
- Second source: <https://www.cisa.gov/news-events/directives/bod-23-02-implementation-guidance-mitigating-risk-internet-exposed-management-interfaces>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Nothing here is verified  -  neither the 14-day deadline, the definition of a networked management interface, nor the 'policy enforcement point separate from the interface' wording. cisa.gov blocks automated retrieval, so this URL cannot function as a checkable citation. Retrieve the directive manually and re-verify each element, and note that BOD 23-02 binds federal civilian executive branch agencies only  -  it is at most an analogy for a private multi-tenant system, so do not let an unverified deadline become a requirement threshold in the deliverable.

### A7.44  [no]

**Claim.** NIST SP 800-53 SC-7 (Boundary Protection) requires monitoring and control of communications at external and key internal boundaries, physical or logical separation of publicly accessible components from internal networks, and connection to external networks only through managed interfaces. Related guidance separates user functionality from system management functionality and routes networked privileged access through a dedicated managed interface.

**Limit or threshold asserted.** n/a  -  exact Rev 5 control-enhancement numbers not verified against the NIST source

- Source: NIST SP 800-53  -  SC-7 Boundary Protection (via CSF Tools reference)
- URL: <https://csf.tools/reference/nist-sp-800-53/r4/sc/sc-7/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Two separate defects. (1) Reachability: csf.tools is behind a bot challenge and returns no content to automated retrieval, so it cannot serve as verifiable evidence. (2) Version: the cited path is /r4/  -  NIST SP 800-53 Revision 4 was withdrawn on 23 September 2021 and is superseded by Revision 5. Control-enhancement numbering under SC-7 differs between the revisions, so an r4 citation cannot support statements about Rev 5 enhancements (and the claim's own caveat concedes the enhancement numbers were never checked). Cite the authoritative NIST source for Rev 5 (csrc.nist.gov SP 800-53 Rev. 5 / the OSCAL catalog) and quote SC-7 and the specific enhancements (e.g. SC-7(a)-(c) and the management-interface-related enhancements) directly.

### A7.45  [yes]

**Claim.** 45 CFR 164.312(e)(1) Transmission Security is a required standard: 'Implement technical security measures to guard against unauthorized access to electronic protected health information that is being transmitted over an electronic communications network.' Its two implementation specifications  -  (e)(2)(i) Integrity controls and (e)(2)(ii) Encryption  -  are both ADDRESSABLE, not required. 164.312(a)(1) Access control and 164.312(d) Person or entity authentication are also standards.

**Limit or threshold asserted.** 164.312(e)(1) standard; (e)(2)(i) and (e)(2)(ii) Addressable

- Source: 45 CFR  164.312  -  Technical safeguards (Cornell Legal Information Institute)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.312>
- Accessed: 2026-08-31
- Confidence: verified

### A7.46  [partially]

**Claim.** HHS published a Notice of Proposed Rulemaking to modernize the HIPAA Security Rule on 6 January 2025 (comments closed 7 March 2025, ~4,745 comments). It proposes mandatory encryption, mandatory MFA, mandatory network segmentation, and removal of the 'addressable' category. As of mid-2026 NO final rule has been published and no compliance date is in force.

**Limit or threshold asserted.** NPRM 2025-01-06; comments closed 2025-03-07; no final rule as of 2026

- Source: Reporting on the January 2025 HIPAA Security Rule NPRM and its status
- URL: <https://www.federalregister.gov/documents/2025/01/06/2024-30983/hipaa-security-rule-to-strengthen-the-cybersecurity-of-electronic-protected-health-information>
- Second source: <https://medcurity.com/hipaa-security-rule-changes-2026/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Document type and both dates are confirmed, but only through the Federal Register API  -  the cited HTML URL blocks automated retrieval, so it is not independently checkable as given. Three elements remain unverified: the ~4,745 comment count (not returned by the metadata queried), the specific proposals (mandatory encryption, mandatory MFA, mandatory network segmentation, removal of the addressable category), and the negative assertion that no final rule exists as of mid-2026. A negative of that kind cannot be established from the NPRM's own page at all  -  it requires a search of the rule's regulatory docket or the Unified Agenda. Since this claim is what makes the HIPAA controls advisory rather than binding, verify the no-final-rule status against the docket before relying on it.

### A7.47  [yes]

**Claim.** Microsoft's multitenant domain-name guidance covers wildcard DNS for subdomain-per-tenant, warns that all web components must know how to handle each tenant's Host header, notes that rewriting Host 'can cause other problems', requires domain-ownership validation before onboarding, and describes dangling-DNS / subdomain-takeover attacks plus CAA records as a control on who may issue certificates for a domain.

- Source: Microsoft Learn  -  Domain Name Considerations in Multitenant Solutions
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/domain-names>
- Accessed: 2026-08-31
- Confidence: verified

### A7.48  [partially]

**Claim.** HSTS preload list eligibility requires max-age >= 31536000, includeSubDomains, the preload directive, an HTTP-to-HTTPS redirect on the apex, and every subdomain serving valid HTTPS. Removal from the preload list is deliberately slow  -  a request, review, queue, and a stable browser release, realistically months.

**Limit or threshold asserted.** max-age >= 31536000 (1 year); includeSubDomains + preload required

- Source: HSTS Preload List Submission (hstspreload.org) and OWASP HSTS Cheat Sheet
- URL: <https://hstspreload.org/>
- Second source: <https://cheatsheetseries.owasp.org/cheatsheets/HTTP_Strict_Transport_Security_Cheat_Sheet.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Every threshold is confirmed; two details are embellished. The redirect requirement is worded 'from HTTP to HTTPS on the same host, if you are listening on port 80'  -  it is conditional on listening on port 80 and is not specifically about the apex. And the removal process is not described as a 'request, review, queue, and a stable browser release'; the site says only that removal is possible but 'takes months' to reach users via a Chrome update, with no guarantees for other browsers. The operational conclusion (preload is effectively one-way on a months timescale) stands; trim the invented procedural detail.

### A7.49  [no]

**Claim.** Encrypted Client Hello, which would conceal the SNI, is not reliably deployable on self-hosted edges as of 2026: browser support is broadly default-on but self-hosted server software supporting ECH key generation and DNS publication remains limited. Public-facing TLS endpoints therefore continue to expose the requested hostname in cleartext.

- Source: Reporting on ECH deployment status (drawing on IETF draft-campling-ech-deployment-considerations)
- URL: <https://datatracker.ietf.org/doc/html/draft-campling-ech-deployment-considerations-12>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The URL is live and the draft is current, but it supports none of the three assertions. It is an individual-submission draft about enterprise operational impacts of ECH, not a deployment-status survey; it states no browser support level and nothing about self-hosted server support. Also note the status caveat the draft itself carries  -  an expired-in-January-2027 individual I-D with no IETF endorsement is weak evidence for a 2026 capability statement. If the SNI-exposure point matters, support it from RFC 8446/RFC 9525-adjacent material (SNI is sent in the cleartext ClientHello) and verify ECH server-side availability against the specific edge software's release notes. As written, 'browser support is broadly default-on' is unsourced.

### A7.50  [partially]

**Claim.** Every publicly trusted certificate is logged to Certificate Transparency and is searchable, so certificates that enumerate individual hostnames publish those hostnames; a wildcard certificate publishes only the wildcard name, not the specific subdomains beneath it.

- Source: Certificate Transparency log search practice (crt.sh) and subdomain-enumeration literature
- URL: <https://crt.sh/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: crt.sh demonstrates that logged certificates are searchable  -  which is one third of the claim  -  but it asserts nothing about the other two. The premise that EVERY publicly trusted certificate is logged is a browser root-program policy (Chrome and Apple CT policies require SCTs for public trust), not something crt.sh states, and it is a policy that has exceptions and changes over time. The wildcard point (a *.example.com certificate discloses only the wildcard label, not the names served beneath it) is a property of the certificate's SAN list, not of crt.sh. Cite the Chrome/Apple CT policies for the logging requirement and state the wildcard point as a straightforward consequence of SAN contents; keep crt.sh only as evidence that the logs are publicly queryable.

### A7.51  [partially]

**Claim.** Docker Compose healthchecks run a command inside the container (test as a list beginning with CMD or CMD-SHELL), so a container-level HTTP check can set an arbitrary Host header (e.g. curl -H 'Host: ...'). This makes container-level checks unconditionally compatible with Host-bound applications, unlike most external load balancer probes.

**Limit or threshold asserted.** test: ["CMD", "curl", "-f", ...]

- Source: Docker documentation  -  Define services in Docker Compose (healthcheck)
- URL: <https://docs.docker.com/reference/compose-file/services/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The mechanism is confirmed: the check is a command executed inside the container, so an arbitrary Host header is expressible. The word 'unconditionally' is not earned and the page does not support it. The check depends on a suitable HTTP client actually being present in the image (curl or wget is not guaranteed  -  many distroless and slim images ship neither), on the shell existing for CMD-SHELL form, and the docs give no -H 'Host: ...' example. Restate as: container-level checks CAN carry an operator-specified Host header provided the image contains an HTTP client capable of setting it  -  which then becomes an image requirement to verify per service, not a free property.

### A7.52  [no]

**Claim.** The number of edge routing rules required by this system is four (SPA wildcard, API wildcard, Auth wildcard, object-store exact) and does not grow with the number of offices, because every office hostname is served by the same three processes and the office is selected inside the application. Therefore the edge never needs per-tenant configuration and 'add an office without a deployment' is preserved by any wildcard-capable edge and destroyed by any per-hostname-configuration edge.

**Limit or threshold asserted.** 4 routing rules regardless of office count (11 today, 33 headroom)

- Source: My architectural reasoning applied to the supplied system facts (one application instance; tenancy resolved in-process from Host; wildcard DNS and certificates)
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/domain-names>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The specific '4 routing rules, invariant in office count' figure is this system's own design conclusion, not something the cited page establishes  -  and the page's nearest statement points the other way for the non-wildcard pattern. Two substantive qualifications are also missing. First, invariance holds only WITHIN an already-provisioned label depth: by claim 26, a TLS wildcard matches exactly one label, so any new depth (office.api.<base> under an existing *.<base>) requires a new certificate and a new edge rule  -  the count is invariant in offices, not in naming shape. Second, by claim 27/28 it also requires that no node or empty non-terminal exist at the intermediate label without a wildcard at that depth. Restate as a conditional: the rule count is invariant in office count provided every served label depth already has its own DNS wildcard and its own wildcard certificate. Cite this page only for the wildcard-DNS mechanism and the Host-header-handling warning.

---

## Area: secrets-config

Verification verdict for this area: **material-errors** (29 claims checked)

### A8.1  [yes]

**Claim.** Docker states that build arguments and environment variables are inappropriate for passing secrets to a build because they persist in the final image; the supported mechanism is RUN --mount=type=secret, which mounts the secret at /run/secrets/<id> for the duration of one instruction and does not write it to any layer.

**Limit or threshold asserted.** Quote: "Build arguments and environment variables are inappropriate for passing secrets to your build, because they persist in the final image." Default mount path /run/secrets/<id>; options target= and env=.

- Source: Docker Docs  -  Build secrets
- URL: <https://docs.docker.com/build/building/secrets/>
- Accessed: 2026-08-31
- Confidence: verified

### A8.2  [partially]

**Claim.** BuildKit provenance attestations in mode=max expose the values of build arguments, and Docker explicitly directs users passing credentials via build args to refactor to secret mounts. Secret mounts are never included in provenance attestations. Provenance is not generated by default.

**Limit or threshold asserted.** Quotes: "Note that mode=max exposes the values of build arguments." / "If you're misusing build arguments to pass credentials, authentication tokens, or other secrets, you should refactor your build to pass the secrets using secret mounts instead." / "Secret mounts don't leak outside of the build and are never included in provenance attestations."

- Source: Docker Docs  -  SLSA provenance attestations
- URL: <https://docs.docker.com/build/metadata/attestations/slsa-provenance/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The first three quotes are verbatim correct. The fourth assertion is WRONG and reverses a design-relevant default: Buildx DOES generate provenance by default at mode=min (<https://docs.docker.com/build/metadata/attestations/> - 'Provenance attestations with the mode=min level are added to images by default'; opt out with --provenance=false or BUILDX_NO_DEFAULT_ATTESTATIONS). Correct statement: provenance IS emitted by default at mode=min, which does not include build-arg values; only mode=max, which must be requested explicitly, exposes them. A requirement that credentials must not appear 'in build provenance output' therefore has to account for an attestation that is produced whether or not anyone asks for one.

### A8.3  [yes]

**Claim.** The contents of build secrets are NOT part of the Docker build cache key, so changing a secret's value does not invalidate the cache; only the secret's id and mount path participate in the cache checksum.

**Limit or threshold asserted.** Quote: "The contents of build secrets are not part of the build cache. Changing the value of a secret doesn't result in cache invalidation." Docker's suggested workaround is to pass a build argument alongside the secret and change its value to force invalidation.

- Source: Docker Docs  -  Build cache invalidation
- URL: <https://docs.docker.com/build/cache/invalidation/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Substantively correct. One wording nit: the doc says 'Properties of secrets SUCH AS IDs and mount paths', which is non-exhaustive, so the word 'only' in the claim is slightly stronger than the source.

### A8.4  [yes]

**Claim.** Docker Compose, when it cannot resolve a substituted variable and no default is defined, displays a WARNING and substitutes an empty string. The ${VAR:?error} form exits with an error if the variable is unset or empty; ${VAR?error} exits if it is unset.

**Limit or threshold asserted.** Quote: "If Compose can't resolve a substituted variable and no default value is defined, it displays a warning and substitutes the variable with an empty string." / "${VAR:?error} -> value of VAR if set and non-empty, otherwise exit with error"

- Source: Docker Docs  -  Compose interpolation
- URL: <https://docs.docker.com/reference/compose-file/interpolation/>
- Accessed: 2026-08-31
- Confidence: verified

### A8.5  [yes]

**Claim.** Docker documents that injecting passwords and API keys as environment variables risks unintentional information exposure, that environment variables are often available to all processes and hard to track access to, and that they can be printed in logs during debugging. Compose secrets are mounted at /run/secrets/<secret_name> and support file: and environment: sources.

**Limit or threshold asserted.** Quotes: "If you're injecting passwords and API keys as environment variables, you risk unintentional information exposure." / "Environment variables are often available to all processes, and it can be difficult to track access. They can also be printed in logs when debugging errors without your knowledge."

- Source: Docker Docs  -  Use secrets in Compose
- URL: <https://docs.docker.com/compose/how-tos/use-secrets/>
- Accessed: 2026-08-31
- Confidence: verified

### A8.6  [yes]

**Claim.** Docker's swarm-secret guarantees (encrypted Raft log, mutual-TLS distribution, in-memory filesystem mount, never exposed as environment variables) apply to SWARM SERVICES ONLY, not to standalone containers. Plain Compose secrets therefore give the delivery shape without the at-rest encryption.

**Limit or threshold asserted.** Quotes: "The secret is stored in the Raft log, which is encrypted." / "Docker secrets are only available to swarm services, not to standalone containers."

- Source: Docker Docs  -  Manage sensitive data with Docker secrets (swarm)
- URL: <https://docs.docker.com/engine/swarm/secrets/>
- Accessed: 2026-08-31
- Confidence: verified

### A8.7  [yes]

**Claim.** .NET ships a first-party Key-per-file configuration provider (AddKeyPerFile) explicitly intended for Docker hosting: the file name is the configuration key, the file contents the value, and double-underscore in the file name is the section delimiter. This is the supported path from /run/secrets into IConfiguration with no custom code.

**Limit or threshold asserted.** Quote: "The Key-per-file configuration provider is used in Docker hosting scenarios." directoryPath must be absolute; Logging__LogLevel__System produces Logging:LogLevel:System.

- Source: Microsoft Learn  -  Configuration providers in .NET
- URL: <https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers>
- Accessed: 2026-08-31
- Confidence: verified

### A8.8  [partially]

**Claim.** Options validation in .NET is lazy by default (it runs when the options instance is created); ValidateOnStart / AddOptionsWithValidateOnStart forces it at application start and throws OptionsValidationException, and ValidateDataAnnotations enables [Required]-style checks.

**Limit or threshold asserted.** Pattern: services.AddOptions<T>().BindConfiguration(path).ValidateDataAnnotations().ValidateOnStart(). AddOptionsWithValidateOnStart calls ValidateOnStart internally. ValidateOnStart introduced in .NET 6.

- Source: Microsoft Learn  -  Options pattern in ASP.NET Core
- URL: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0>
- Second source: <https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.optionsservicecollectionextensions.addoptionswithvalidateonstart?view=net-10.0-pp>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The lazy-by-default behaviour, ValidateOnStart, ValidateDataAnnotations and OptionsValidationException are all on the page and correct. Three sub-claims are NOT on this page and were asserted from memory: (a) the string 'AddOptionsWithValidateOnStart' does not appear anywhere on the page (0 matches), so 'AddOptionsWithValidateOnStart calls ValidateOnStart internally' is unsourced here; (b) 'BindConfiguration' does not appear either - the page's pattern is .Bind(Configuration.GetSection(...)), not .BindConfiguration(path); (c) the page never says ValidateOnStart was 'introduced in .NET 6' - it can only be inferred from the ValidateOnStart section being scoped to the aspnetcore-6.0..11.0 monikers. Cite the OptionsBuilderExtensions API page for (a) and (c) instead.

### A8.9  [yes]

**Claim.** RequiredAttribute.AllowEmptyStrings defaults to FALSE, meaning an empty string FAILS [Required] validation by default. This makes [Required] + ValidateOnStart sufficient to catch the present-but-blank failure mode, not just the absent one.

**Limit or threshold asserted.** "true if an empty string is allowed; otherwise, false. The default value is false."

- Source: Microsoft Learn  -  RequiredAttribute.AllowEmptyStrings Property
- URL: <https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations.requiredattribute.allowemptystrings>
- Accessed: 2026-08-31
- Confidence: verified

### A8.10  [partially]

**Claim.** Pointing ASP.NET Core Data Protection at an explicit key repository DEREGISTERS the default key-encryption-at-rest mechanism, so keys are stored unencrypted unless an explicit mechanism is configured. Microsoft recommends specifying one for production.

**Limit or threshold asserted.** Quote: "If you specify an explicit key persistence location, the data protection system deregisters the default key encryption at rest mechanism. Consequently, keys are no longer encrypted at rest. We recommend that you specify an explicit key encryption mechanism for production deployments." ProtectKeysWithCertificate is available cross-platform; DPAPI/DPAPI-NG are Windows-only.

- Source: Microsoft Learn  -  Key encryption at rest in ASP.NET Core
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0>
- Second source: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The load-bearing quote is verbatim correct, as is the Windows-only scoping of DPAPI and DPAPI-NG. The claim that 'ProtectKeysWithCertificate is available cross-platform' is NOT stated on this page - the page is titled 'Key encryption at rest in Windows and Azure', the X.509 section makes no platform statement, and it adds a restriction the claim omits: 'Due to .NET Framework limitations, only certificates with CAPI private keys are supported.' Either drop the cross-platform assertion or source it elsewhere and carry the CAPI caveat with it, since the design depends on this mechanism working on Linux containers.

### A8.11  [partially]

**Claim.** The Data Protection key ring auto-rotates: keys get an activation date of now+2 days and expiry of now+90 days; created, active and expired keys can all unprotect payloads; deleting a key permanently destroys the data it protected with no override. UnprotectKeysWithAnyCertificate exists so the wrapping certificate can be rotated while old keys stay readable. SetApplicationName sets the ApplicationDiscriminator and is what allows two apps sharing one key repository to read each other's payloads.

**Limit or threshold asserted.** 90-day default lifetime, minimum configurable lifetime 7 days, 2-day activation delay, ~24h key-ring refresh. Quote: "all data protected by the key is permanently undecipherable, and there's no emergency override... Deleting a key is truly destructive behavior." / "By default, the Data Protection system isolates apps from one another based on their content root paths, even if they share the same physical key repository."

- Source: Microsoft Learn  -  Key management in ASP.NET Core / Configure Data Protection
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0>
- Second source: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every numeric threshold (90 days, 7-day minimum, 2-day activation, ~24h refresh) and both quoted warnings check out verbatim on this page. But two sub-claims are attributed to a page that does not contain them: 'UnprotectKeysWithAnyCertificate' appears nowhere on the key-management page, and neither does SetApplicationName, ApplicationDiscriminator, or the quoted sentence 'By default, the Data Protection system isolates apps from one another based on their content root paths, even if they share the same physical key repository.' That sentence and both APIs live on the Data Protection configuration/overview page. Re-cite those two to the configuration overview - they carry the requirement that the wrapping certificate be rotatable without invalidating old sessions, so a bad citation there is a real audit gap.

### A8.12  [yes]

**Claim.** NIST SP 800-57 Part 1 Rev 5 Table 7 marks private signature keys as generally NOT to be backed up, and Appendix B.3.1.1 recommends generating a second signature key pair and distributing its public key as the alternative to backing up the private key. The same Table 7 marks symmetric data-encryption keys as OK to back up.

**Limit or threshold asserted.** Table 7 (8.2.2.1): "Private signature key  -  No (in general); support for non-repudiation would be in question... Symmetric data encryption key  -  OK". B.3.1.1: "Instead of backing up the private signature key, a second private signature key and corresponding public key could be generated and the public key distributed in accordance with Section 8.1.5.1 for use if the primary private signature key becomes unavailable." 8.3.1: archive "shall continue to provide the appropriate protections" and "will require a strong access-control mechanism". 8.2.2.2: the key-recovery decision "should be made on a case-by-case basis" based on key type and application.

- Source: NIST SP 800-57 Part 1 Rev. 5, Recommendation for Key Management
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-57pt1r5.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All five citations verified verbatim against the PDF (Table 7 on pp. 105-106, B.3.1.1 on pp. 136-137, 8.3.1, 8.2.2.2). Worth carrying forward: Table 7's full entry contains an explicit exception the claim truncates - 'However, backup may be warranted in some cases, such as a CA's private signing key' - which materially softens claim [29]'s 'never escrow the signing key' conclusion.

### A8.13  [yes]

**Claim.** NIST defines split knowledge as splitting a key into n shares such that knowledge of any k-1 shares reveals nothing about the key, and requires that each share be distributed separately to its intended recipient when used for manual key distribution.

**Limit or threshold asserted.** Glossary: "A process by which a cryptographic key is split into n key shares, each of which provides no knowledge of the key... knowledge of any k - 1 key shares provides no information about the key other than, possibly, its length." 8.1.5.2.2.1: "each key share shall be distributed separately to its intended recipient."

- Source: NIST SP 800-57 Part 1 Rev. 5 (glossary; 8.1.5.2.1, 8.1.5.2.2.1)
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-57pt1r5.pdf>
- Accessed: 2026-08-31
- Confidence: verified

### A8.14  [yes]

**Claim.** OpenIddict supports registering multiple signing/encryption credentials for key rotation and selects deterministically: X.509 certificates are ordered by NotBefore/NotAfter, not-yet-valid certificates are skipped, and the furthest expiration date is preferred. It recommends two RSA certificates in production, distinct from the HTTPS certificate  -  one for signing, one for encryption.

**Limit or threshold asserted.** Quote: "certificates with the furthest expiration date are always preferred"; "it is recommended to use two RSA certificates, distinct from the certificate(s) used for HTTPS: one for encryption, one for signing." The docs do NOT state whether previously issued tokens survive a signing-key change  -  unverified there.

- Source: OpenIddict documentation  -  Encryption and signing credentials
- URL: <https://documentation.openiddict.com/configuration/encryption-and-signing-credentials>
- Accessed: 2026-08-31
- Confidence: verified

### A8.15  [yes]

**Claim.** ABP's AbpStringEncryptionOptions has a published, well-known DEFAULT passphrase ("gsKnGZ041HLL4IM8"), default salt and default IV; ABP recommends changing it. In the ABP framework repository IStringEncryptionService is referenced by only a handful of components  -  notably SettingEncryptionService (encrypted settings) and the AWS/Aliyun blob-storing client factories for cached temporary credentials.

**Limit or threshold asserted.** DefaultPassPhrase = "gsKnGZ041HLL4IM8"; DefaultSalt = ASCII "hgt!16kl"; InitVectorBytes = ASCII "jkE49230Tf093b42" (16 bytes); Keysize = 256. Framework references: Volo.Abp.Settings/SettingEncryptionService.cs, Volo.Abp.BlobStoring.Aws/DefaultAmazonS3ClientFactory.cs, Volo.Abp.BlobStoring.Aliyun/DefaultOssClientFactory.cs. ABP COMMERCIAL modules are closed-source and were not searchable  -  additional uses there are UNVERIFIED.

- Source: ABP.IO Documentation  -  String Encryption; abpframework/abp code search
- URL: <https://abp.io/docs/latest/framework/infrastructure/string-encryption>
- Second source: <https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Security/Volo/Abp/Security/Encryption/AbpStringEncryptionOptions.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Defaults verified on the doc page. I independently re-ran the repository search: GitHub code search over abpframework/abp for IStringEncryptionService returns exactly 6 files - IStringEncryptionService.cs, StringEncryptionService.cs and AbpStringEncryptionOptions.cs (the definitions), plus Volo.Abp.Settings/SettingEncryptionService.cs, Volo.Abp.BlobStoring.Aws/DefaultAmazonS3ClientFactory.cs and Volo.Abp.BlobStoring.Aliyun/DefaultOssClientFactory.cs. The three consumer paths in the claim are exactly right, and the closed-source-commercial caveat is properly flagged.

### A8.16  [partially]

**Claim.** ABP does NOT encrypt tenant connection strings by default; the SaaS/tenant-management module validates and stores them as plain text, and encrypting them requires overriding the connection-string resolver. This means the string-encryption passphrase is not, by default, load-bearing for tenant database access.

**Limit or threshold asserted.** Support-forum guidance rather than reference documentation; treat as strong indication, not specification. Additionally irrelevant here because this system DERIVES office connection strings from a template rather than storing them.

- Source: ABP.IO support  -  How to send encrypted connection string (#9738); abpframework/abp issue #4648
- URL: <https://abp.io/support/questions/9738/How-to-send-encrypted-connection-string>
- Second source: <https://github.com/abpframework/abp/issues/4648>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The resolver-override half is directly supported and the thread's whole premise (a customer building their own encryption because none exists) implies no built-in encryption. But the page never affirmatively states that the SaaS/tenant-management module 'validates and stores them as plain text' - that is inference from a support thread, not documentation. The claim's own 'partial' rating is the right one; do not upgrade it. Since the design derives office connection strings from a template, this is not load-bearing either way.

### A8.17  [partially]

**Claim.** The ABP Commercial NuGet feed URL embeds a per-customer identifier in the path (<https://nuget.abp.io/><GUID>/v3/index.json), so the NuGet.config file itself functions as a credential. The AbpLicenseCode is additionally checked at RUNTIME (not only at build), can be supplied via appsettings.secrets.json or an environment variable, and an expired licence stops deployed applications functioning.

**Limit or threshold asserted.** Feed URL format is consistently reported across multiple ABP support threads; the runtime licence check is evidenced by the ABP-LIC-ERROR / ABP-LIC-0020 support threads. Not stated in a single canonical reference page I could fetch  -  abp.io/docs/latest/others/nuget-packages-source-code returned 404. Treat the runtime-check claim as PARTIAL and verify locally by starting the app with AbpLicenseCode unset.

- Source: ABP.IO support threads on Docker/CI NuGet access and license errors
- URL: <https://abp.io/support/questions/1809/Abp-Commercial-Nuget-docker--409-Conflict>
- Second source: <https://abp.io/support/questions/9442/ABP-LIC-0020---License-code-not-found>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The cited thread supports only that the feed URL embeds a per-customer key in the path - and even then it shows the redacted form '[key removed]', so it does not establish that the identifier is a GUID specifically. The thread contains NO mention of AbpLicenseCode, runtime licence checking, appsettings.secrets.json, or expired licences halting deployed apps; that entire second half of the claim is unsupported by this URL (the claim points to separate ABP-LIC-ERROR threads that were not supplied for checking). The design-relevant conclusion - treat NuGet.config as a credential - survives on the first half alone. Verify the runtime-check behaviour empirically by starting the app with AbpLicenseCode unset, as the claim itself advises, before writing it into a requirement.

### A8.18  [partially]

**Claim.** MSSQL_SA_PASSWORD is honoured only at first container initialisation; on a container reusing an existing data volume the environment variable does not change the password. Microsoft also documents that the value stays discoverable inside the container and recommends changing the sa password with ALTER LOGIN via sqlcmd.

**Limit or threshold asserted.** Quote: "After you create your SQL Server container, the MSSQL_SA_PASSWORD environment variable you specified is discoverable by running echo $MSSQL_SA_PASSWORD in the container." "For security purposes, change your sa password" using docker exec + sqlcmd ALTER LOGIN. Init-only behaviour on persistent volumes is corroborated by microsoft/mssql-docker issue #471.

- Source: MicrosoftDocs/sql-docs  -  change-docker-password include; microsoft/mssql-docker issue #471
- URL: <https://github.com/MicrosoftDocs/sql-docs/blob/live/docs/linux/includes/change-docker-password.md>
- Second source: <https://github.com/microsoft/mssql-docker/issues/471>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The discoverability quote and the ALTER LOGIN remediation are verbatim correct. The PRIMARY assertion of the claim - that the variable is honoured only at first initialisation and is inert on a container reusing an existing data volume - is NOT in this document at all; the page addresses only post-creation password hygiene. That behaviour is corroborated by microsoft/mssql-docker#471, which is a community issue thread, not vendor documentation. Rotating the sa password must therefore be specified as an in-database ALTER LOGIN operation (with the env var updated to match), and the init-only behaviour should be labelled as issue-thread evidence, not documented behaviour.

### A8.19  [partially]

**Claim.** MinIO root credentials are set only by environment variables, and MinIO uses those values in encrypting its backend config/IAM data, so rotating them is a server-level operation with migration semantics rather than a simple credential change; mismatched _OLD variables produce an 'invalid AEAD algorithm ID' safe-mode failure.

**Limit or threshold asserted.** Behaviour is documented in project issues and community docs; I could not fetch the canonical MinIO reference page (docs.min.io redirected to a 404; minio.community returned HTTP 429). Treat the exact rotation procedure as UNVERIFIED and test it on a scratch instance before attempting it. The general shape  -  root credentials are env-only and are entangled with backend encryption  -  is corroborated across two independent issue threads.

- Source: minio/minio issues #13447 and #10911; MinIO root-credentials reference
- URL: <https://github.com/minio/minio/issues/13447>
- Second source: <https://github.com/minio/minio/issues/10911>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Two problems. (1) The cited issue does establish that rotating root credentials via env vars breaks a running server and that maintainers call it 'working as intended', but it does NOT mention MINIO_ROOT_USER_OLD/MINIO_ROOT_PASSWORD_OLD, backend config/IAM encryption keyed on the root credentials, or the 'invalid AEAD algorithm ID' error - the error shown is a different one ('The access key ID you provided does not exist in our records'). Those specifics are unsupported by this URL. (2) More seriously, and not flagged anywhere in the research: minio/minio was ARCHIVED by its owner on 25 April 2026 and is read-only, with users directed to AIStor. An archived, no-longer-patched object store is a currency finding that outranks the rotation mechanics and belongs in the design write-up; the claim's currency bar (a release within 12 months) was applied to the five tools in claim [26] but never to this incumbent component.

### A8.20  [yes]

**Claim.** Microsoft warns that environment variables are commonly stored as plain, unencrypted text and are accessible to untrusted parties if the machine or process is compromised; that the Secret Manager tool does not encrypt secrets and is development-only; and that secrets should never be stored in source code or configuration files.

**Limit or threshold asserted.** Quotes: "Never store passwords or other sensitive data in source code or configuration files." / "Environment variables are commonly stored as plain, unencrypted text. If the machine or process is compromised, environment variables are accessible to untrusted parties." / "Secret Manager doesn't encrypt the stored secrets and shouldn't be treated as a trusted store. It's for development purposes only."

- Source: Microsoft Learn  -  Safe storage of app secrets in development in ASP.NET Core
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified

### A8.21  [yes]

**Claim.** OWASP advises against environment variables for secrets, recommends automating rotation because manual rotation causes mistakes, prefers the consumer retrieving secrets over a pipeline injecting them, and frames secret auditing around who REQUESTED and USED a secret  -  an access log, not a change log.

**Limit or threshold asserted.** Quotes: "environment variables are generally accessible to all processes and may be included in logs or system dumps. Using environment variables is therefore not recommended unless the other methods are not possible." / "Key rotation is a challenging process when implemented manually, and can lead to mistakes. It is therefore better to automate the rotation of keys" / audit must record "Who requested a secret and for what system and role... When the secret was used and by whom/what."

- Source: OWASP Secrets Management Cheat Sheet
- URL: <https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All four quotes verified verbatim against the cheat sheet source (lines 44, 211-214, 371, 511 of the upstream markdown). This is the most accurately cited claim in the set.

### A8.22  [yes]

**Claim.** 45 CFR 164.312(a)(2)(iv) Encryption and decryption is ADDRESSABLE, as is 164.312(e)(2)(ii) Encryption. 164.312(a)(2)(i) Unique user identification is REQUIRED. 164.312(d) Person or entity authentication is a standard with no implementation specifications.

**Limit or threshold asserted.** 164.312(a)(2)(iv): "Implement a mechanism to encrypt and decrypt electronic protected health information." (Addressable). 164.312(d): "Implement procedures to verify that a person or entity seeking access to electronic protected health information is the one claimed." 164.312(a)(2)(i): "Assign a unique name and/or number for identifying and tracking user identity." (Required). eCFR was unreachable from this environment (302 to an unblock interstitial); Cornell LII used as the source of record.

- Source: 45 CFR  164.312  -  Technical safeguards
- URL: <https://www.law.cornell.edu/cfr/text/45/164.312>
- Accessed: 2026-08-31
- Confidence: verified

### A8.23  [yes]

**Claim.** 45 CFR 164.306(d)(3) makes 'addressable' mean assess-then-implement-or-document: a covered entity must assess whether the specification is reasonable and appropriate, and if not, document why and implement an equivalent alternative measure if reasonable and appropriate. Addressable does not mean optional.

**Limit or threshold asserted.** 164.306(d)(3): assess "whether each implementation specification is a reasonable and appropriate safeguard in its environment"; then implement, or document why not and "Implement an equivalent alternative measure if reasonable and appropriate."

- Source: 45 CFR  164.306  -  Security standards: General rules
- URL: <https://www.law.cornell.edu/cfr/text/45/164.306>
- Accessed: 2026-08-31
- Confidence: verified

### A8.24  [yes]

**Claim.** 45 CFR 164.308(a)(5)(ii)(D) Password management (procedures for creating, changing, and safeguarding passwords) is ADDRESSABLE. 164.308(a)(1)(ii)(D) Information system activity review is REQUIRED.

**Limit or threshold asserted.** 164.308(a)(5)(ii)(D): "Procedures for creating, changing, and safeguarding passwords." (Addressable). 164.308(a)(1)(ii)(D): "Implement procedures to regularly review records of information system activity, such as audit logs, access reports, and security incident tracking reports." (Required).

- Source: 45 CFR  164.308  -  Administrative safeguards
- URL: <https://www.law.cornell.edu/cfr/text/45/164.308>
- Accessed: 2026-08-31
- Confidence: verified

### A8.25  [yes]

**Claim.** HHS published a HIPAA Security Rule NPRM on 6 January 2025 (90 FR 898, RIN 0945-AA22) that would remove the required/addressable distinction and make all implementation specifications required, with proposed encryption and MFA requirements. It is NOT final as of August 2026; OMB's agenda now shows a July 2027 target for final action.

**Limit or threshold asserted.** Publication 2025-01-06, 90 FR 898, doc 2024-30983, RIN 0945-AA22, comment period closed 2025-03-07. federalregister.gov was unreachable from this environment (302 to an unblock interstitial); GovInfo record and multiple secondary legal analyses used. The July 2027 target is from OMB's Unified Agenda as reported by secondary sources and is UNVERIFIED against the agenda itself; agenda dates are non-binding. Do NOT cite the NPRM as current law.

- Source: Federal Register / GovInfo  -  HIPAA Security Rule To Strengthen the Cybersecurity of Electronic Protected Health Information
- URL: <https://www.govinfo.gov/app/details/FR-2025-01-06/2024-30983>
- Second source: <https://www.clarkhill.com/news-events/news/hipaa-security-rule-update-delayed-until-2027/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Every element verified. Note the govinfo /app/details/ URL is a JavaScript shell that returns no readable content to a fetcher - use <https://www.govinfo.gov/content/pkg/FR-2025-01-06/html/2024-30983.htm> as the citation instead. Also, the July 2027 date the claim flags as UNVERIFIED is in fact directly verifiable at the source: reginfo.gov's Unified Agenda entry for RIN 0945-AA22 (<https://www.reginfo.gov/public/do/eAgendaViewRule?pubId=202510&RIN=0945-AA22>) shows 'Timetable: NPRM 01/06/2025 90 FR 898 | Final Action 07/00/2027' and 'Stage of Rulemaking: Long-Term Actions'. That also independently confirms the rule is not final. Upgrade this from partial to verified, keeping the caveat that agenda dates are non-binding.

### A8.26  [yes]

**Claim.** SOPS and age are both actively maintained as of August 2026: SOPS v3.13.3 (23 July 2026), a CNCF Sandbox project since 2023 under getsops maintainers; age v1.3.2 (29 August 2026). Sealed-secrets is also current (v0.39.1, 20 August 2026) but is Kubernetes-only. OpenBao is current (v2.6.2, 18 August 2026). Gitleaks is current (v8.30.1, 21 March 2026).

**Limit or threshold asserted.** All five have a release within the last ~5 months, well inside the 12-month currency bar. SOPS supports YAML, JSON, ENV, INI and BINARY and encrypts with age, PGP and several cloud KMS backends.

- Source: GitHub release pages (getsops/sops, FiloSottile/age, bitnami-labs/sealed-secrets, openbao/openbao, gitleaks/gitleaks)
- URL: <https://github.com/getsops/sops/releases>
- Second source: <https://github.com/FiloSottile/age/releases>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All five version numbers and dates confirmed against the respective GitHub releases pages, and none of the five repositories is archived. The CNCF-Sandbox-since-2023 and the format list are confirmed verbatim in the sops README. This is the only claim in the set where the currency bar was actually applied - see claim [19], where the same bar would have caught an archived incumbent.

### A8.27  [no]

**Claim.** Vault/OpenBao-class stores provide, as a capability, an audit device logging every request and response, and a Shamir-based seal splitting the root key into shares with a default of 5 shares and a threshold of 3. OpenBao can configure the audit device in its config file so that requests are audited from the very first one, whereas in Vault requests made before the audit device is configured are not logged.

**Limit or threshold asserted.** Default 5 shares / threshold 3. Cited here strictly as EVIDENCE of what this class of component does, not as a product recommendation. The Vault-vs-OpenBao audit-bootstrapping difference comes from a secondary comparison and is UNVERIFIED against OpenBao's own reference.

- Source: HashiCorp Vault docs (Seal/Unseal, Audit Devices); OpenBao docs (Seal/Unseal)
- URL: <https://developer.hashicorp.com/vault/docs/concepts/seal>
- Second source: <https://openbao.org/docs/concepts/seal/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The cited page supports the existence of Shamir-based sealing but settles NEITHER stated threshold. It gives no numeric defaults, so '5 shares / threshold 3' is stated from memory here (the 5/3 default is real but belongs to the `vault operator init` CLI/API reference, which defaults key-shares=5 and key-threshold=3 - cite that page instead). The page also does not mention audit devices at all, so the entire audit-device half of the claim - both the 'logs every request and response' capability and the Vault-vs-OpenBao bootstrapping difference - has no support at this URL. Re-cite to the audit-device concept page and the operator-init reference, and keep the OpenBao config-file bootstrapping difference marked unverified as the claim already does.

### A8.28  [partially]

**Claim.** MY REASONING, NOT A SOURCED FACT: because this system persists the Data Protection key ring to Redis via an explicit provider, and Microsoft documents that an explicit persistence location deregisters at-rest encryption, the master keys protecting login state and email-confirmation tokens for BOTH the AuthServer and the API are stored unencrypted in Redis, in its AOF file on the redisdata volume, and in any backup of that volume. Read access to Redis is therefore equivalent to the ability to forge authentication payloads across every office.

**Limit or threshold asserted.** Verifiable in ten minutes: read the Redis key CaseEvaluation-Protection-Keys and inspect whether each <key> element carries a plaintext <masterKey> or an <encryptedKey>/EncryptedXmlDecryptor descriptor. Whether Redis additionally requires AUTH in this deployment is UNVERIFIED and materially changes the urgency.

- Source: Derived from Microsoft Data Protection documentation applied to the stated system facts
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The premise is correctly sourced and the inference is sound in shape - a Redis-backed key ring is an explicit persistence location, so the deregistration warning does apply. Two things keep this from 'yes'. First, it is labelled reasoning, and the doc supports only the premise, not the conclusion about this deployment. Second, the cross-application scope needs one more check the claim does not mention: whether AuthServer and API share a key ring at all depends on ApplicationDiscriminator/SetApplicationName - if the two processes carry different discriminators they do not read each other's payloads even from one Redis instance, which narrows 'forge authentication payloads across every office' to 'forge them for whichever apps share the discriminator'. Verify the discriminator alongside the ten-minute masterKey/encryptedKey inspection the claim already prescribes.

### A8.29  [partially]

**Claim.** MY REASONING, NOT A SOURCED FACT: the two secrets the brief calls unrecoverable belong to different NIST key classes with opposing guidance, so a single 'escrow both' policy is wrong on one of them. The signing certificate should get a pre-provisioned standby credential (which OpenIddict natively supports) rather than a backup; the string-encryption passphrase is the one that warrants escrow.

**Limit or threshold asserted.** Both underlying facts are individually verified; the synthesis is mine and is arguable  -  a reasonable engineer could say the non-repudiation concern NIST cites does not apply to an OIDC access-token signing key used purely for integrity, and therefore escrow is acceptable. If you take that view, escrow it AND provision the standby; the standby costs almost nothing either way.

- Source: Derived from NIST SP 800-57 Pt1 Rev5 Table 7 / B.3.1.1 applied to OpenIddict's documented multi-credential behaviour
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-57pt1r5.pdf>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Both underlying facts are verified (claims 12 and 14), and the claim is honestly labelled as the researcher's synthesis, so 'verified' overstates it - synthesis is not a citable fact and should be marked partial. NIST's own text also undercuts the strong form of the conclusion more than the claim admits: Table 7's entry is not a flat 'no' but 'No (in general) ... However, backup may be warranted in some cases, such as a CA's private signing key. When required, any backed up keys shall be stored under the owner's control.' An OIDC token-signing key used for integrity rather than non-repudiation sits squarely in that exception. The claim's own fallback - escrow it AND provision the standby - is the defensible recommendation and should be the primary one, not the alternative.

---

## Area: observability

Verification verdict for this area: **material-errors** (42 claims checked)

### A9.1  [yes]

**Claim.** 45 CFR 164.312(b) Audit Controls is a Required standard with no implementation specifications: 'Implement hardware, software, and/or procedural mechanisms that record and examine activity in information systems that contain or use electronic protected health information.'

**Limit or threshold asserted.** 164.312(b), Required (no R/A implementation specs beneath it)

- Source: 45 CFR 164.312 (GPO govinfo, CFR Title 45 Vol 2)
- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-312.xml>
- Second source: <https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.312>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Substantively correct. Pedantic caveat: the CFR does not print '(Required)' next to 164.312(b)  -  the (R)/(A) labels attach only to implementation specifications. 164.312(b) is a bare standard, and standards are mandatory by 164.306(c). Say 'a standard with no implementation specifications, and therefore mandatory in full' rather than 'a Required standard'.

### A9.2  [yes]

**Claim.** 45 CFR 164.312(c) Integrity is a standard; its implementation specification 'Mechanism to authenticate electronic protected health information' is Addressable, not Required: 'Implement electronic mechanisms to corroborate that electronic protected health information has not been altered or destroyed in an unauthorized manner.'

**Limit or threshold asserted.** 164.312(c)(2), Addressable

- Source: 45 CFR 164.312 (GPO govinfo)
- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-312.xml>
- Accessed: 2026-08-31
- Confidence: verified

### A9.3  [yes]

**Claim.** 45 CFR 164.308(a)(1)(ii)(D) Information System Activity Review is Required: 'Implement procedures to regularly review records of information system activity, such as audit logs, access reports, and security incident tracking reports.' Recording logs without reviewing them does not satisfy the rule.

**Limit or threshold asserted.** 164.308(a)(1)(ii)(D), Required; no frequency specified in the rule

- Source: 45 CFR 164.308 (GPO govinfo)
- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-308.xml>
- Accessed: 2026-08-31
- Confidence: verified

### A9.4  [yes]

**Claim.** The six-year retention in 45 CFR 164.316(b)(2)(i) attaches to 'the documentation required by paragraph (b)(1)' - policies, procedures, and written records of actions/activities/assessments required by the subpart - measured 'from the date of its creation or the date when it last was in effect, whichever is later'. It does not on its face say audit logs must be kept six years.

**Limit or threshold asserted.** 6 years from creation or last effective date, whichever is later

- Source: 45 CFR 164.316 (GPO govinfo)
- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-316.xml>
- Accessed: 2026-08-31
- Confidence: verified

### A9.5  [partially]

**Claim.** Sources genuinely disagree on whether HIPAA requires six-year audit log retention. A HIPAA audit firm states plainly that 'the HHS has not actually defined if all details captured in audit logs are considered an action, activity, or assessment', recommends a risk-based approach, and advises six years only where not cost-prohibitive. Other widely-cited compliance vendors assert six years flatly. I could find no OCR guidance resolving it.

**Limit or threshold asserted.** No regulatory number exists; 6 years is an interpretation of 164.316(b)(1)'s word 'activity'

- Source: Schellman, 'How Long Should I Keep HIPAA Audit Logs?' (pub. 2019-04-11, updated 2026-02-13)
- URL: <https://www.schellman.com/blog/healthcare-compliance/hipaa-audit-log-retention-policy>
- Second source: <https://www.ispartnersllc.com/blog/hipaa-audit-log-retention-six-years/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The Schellman half is fully supported. The other two assertions in the claim are not evidenced by any cited source: 'other widely-cited compliance vendors assert six years flatly' has no citation, and 'I could find no OCR guidance resolving it' is an absence-of-evidence statement that cannot be verified from this page. Either cite a second, contrasting source explicitly or drop the 'sources genuinely disagree' framing and state only that the single cited auditor treats it as undefined.

### A9.6  [yes]

**Claim.** NIST SP 800-66r2 lists five Key Activities for the Audit Controls standard and asks, as a sample question under 'Develop and Deploy the Information System Activity Review/Audit Policy', 'Where will audit information reside (e.g., separate server)?' and, under Standard Operating Procedures, 'Has the organization considered the use of automation to assist in the monitoring and review of system activity?'

**Limit or threshold asserted.** Rev. 2, February 2024; Section 5.3.2 / Table 22, pages 69-70

- Source: NIST SP 800-66r2, Implementing the HIPAA Security Rule: A Cybersecurity Resource Guide, Section 5.3.2, Table 22 (February 2024)
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-66r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Verified against the PDF text: five key activities, Sec. 5.3.2 / Table 22, pages 69-70, NIST SP 800-66r2 February 2024. One caveat: the PDF is a multi-column table, so attributing 'Where will audit information reside' specifically to Key Activity 3 rather than the shared question column is an inference from row ordering, not an explicit label.

### A9.7  [partially]

**Claim.** The January 2025 HIPAA Security Rule NPRM has not been finalised. In the Fall 2026 Unified Agenda, HHS moved it to Long-Term Actions with July 2027 as the anticipated date for final action. It would remove the required/addressable distinction, mandate a technology asset inventory and network map reviewed at least every 12 months, and require a compliance audit at least every 12 months.

**Limit or threshold asserted.** NPRM published 90 FR 898 on 2025-01-06; comments closed 2025-03-07; final action anticipated July 2027

- Source: Clark Hill, 'HIPAA Security Rule Update Delayed Until 2027' (2026-07-13); HHS OCR NPRM factsheet
- URL: <https://www.clarkhill.com/news-events/news/hipaa-security-rule-update-delayed-until-2027/>
- Second source: <https://www.hhs.gov/hipaa/for-professionals/security/hipaa-security-rule-nprm/factsheet/index.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every element is factually correct  -  I independently confirmed 90 FR 898 / 2025-01-06 / comments close 2025-03-07 via the Federal Register API, and both 12-month figures in the NPRM text at govinfo (FR-2025-01-06 doc 2024-30983). But the single cited URL (Clark Hill) carries none of the numeric detail: no FR citation, no comment-close date, no 12-month intervals. Add <https://www.federalregister.gov/d/2024-30983> (or the govinfo FR-2025-01-06 HTM) as the citation for those specifics and keep Clark Hill only for the Unified Agenda / July 2027 point.

### A9.8  [yes]

**Claim.** 45 CFR 164.314(a)(2)(i) requires a business associate contract to provide that the business associate will comply with the applicable Security Rule requirements, ensure subcontractors that create/receive/maintain/transmit ePHI agree to the same by contract, and report security incidents including breaches of unsecured PHI to the covered entity.

**Limit or threshold asserted.** 164.314(a)(2)(i)(A)-(C)

- Source: 45 CFR 164.314 (GPO govinfo)
- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-314.xml>
- Accessed: 2026-08-31
- Confidence: verified

### A9.9  [yes]

**Claim.** The OWASP Logging Cheat Sheet's exclusion list explicitly names health data: do not log 'Application source code, Session identification values, Access tokens, Sensitive personal data and some forms of personally identifiable information (PII) e.g. health, government identifiers, vulnerable people, Authentication passwords, Database connection strings, Encryption keys and other primary secrets, Bank account or payment card holder data.' It also requires 'Tamper detection so you know if a record has been modified or deleted', storing/copying log data to read-only media as soon as possible, and that 'All access to the logs must be recorded and monitored'.

- Source: OWASP Cheat Sheet Series - Logging Cheat Sheet
- URL: <https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Supported. Minor: the exclusion list also contains 'Data of a higher security classification than the logging system is allowed to store', which the claim omits and which is directly load-bearing for a PHI-in-telemetry argument  -  worth adding.

### A9.10  [partially]

**Claim.** The OWASP Logging Vocabulary Cheat Sheet defines a standard security event vocabulary including sensitive_create / sensitive_read / sensitive_update / sensitive_delete, authn_login_success/fail, authz_fail, privilege_permissions_changed, excess_rate_limit_exceeded, and sys_monitor_disabled, with a recommended flat JSON schema (datetime, appid, event, level, description, source_ip, host_ip, hostname, request_uri, request_method).

- Source: OWASP Cheat Sheet Series - Application Logging Vocabulary Cheat Sheet
- URL: <https://cheatsheetseries.owasp.org/cheatsheets/Logging_Vocabulary_Cheat_Sheet.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All ten event names are confirmed. The JSON schema list is incomplete: the published schema also includes useragent, protocol, port, region and geo. The cheat sheet also states that all fields after the event type 'should be considered optional' subject to data-governance and privacy requirements  -  a caveat that matters here, since useragent/geo/region are exactly the fields a PHI-minimisation policy would want to drop. Restate as 'a flat JSON schema whose fields include ...' and add the optionality caveat.

### A9.11  [partially]

**Claim.** OpenTelemetry specification status as of access: Tracing API/SDK/OTLP Stable; Metrics API Stable, SDK Mixed, OTLP Stable; Logs Bridge API Stable, SDK Stable, Data Model Stable; Baggage Stable; Profiles protocol Development. OTLP itself is 'Stable for the trace, metric and log signals. Development for the profiles signal' and supports gRPC, HTTP/protobuf and HTTP/JSON.

**Limit or threshold asserted.** Profiles = Development (experimental); everything else load-bearing here = Stable

- Source: OpenTelemetry Specification Status Summary; OTLP Specification
- URL: <https://opentelemetry.io/docs/specs/status/>
- Second source: <https://opentelemetry.io/docs/specs/otlp/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The status table is exactly as stated. But the verbatim OTLP quotation and the transport list (OTLP/gRPC; OTLP/HTTP with binary-protobuf and JSON-protobuf encodings) are NOT on the cited /docs/specs/status/ page  -  they are on <https://opentelemetry.io/docs/specs/otlp/>. Cite that second URL for the OTLP sentence and transports.

### A9.12  [no]

**Claim.** OpenTelemetry .NET reports Traces, Metrics and Logs all Stable. OpenTelemetry.Instrumentation.AspNetCore is at 1.17.0, last updated 2026-07-17 - actively maintained within the last 12 months.

**Limit or threshold asserted.** AspNetCore instrumentation 1.17.0, 2026-07-17

- Source: OpenTelemetry Status page (language SDK table); NuGet
- URL: <https://opentelemetry.io/status/>
- Second source: <https://www.nuget.org/packages/opentelemetry.instrumentation.aspnetcore/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Two errors. (1) The version is superseded: the current release is 1.18.0 published 2026-08-21, not 1.17.0 dated 2026-07-17. (2) The cited URL cannot support the version claim at all  -  opentelemetry.io/status/ carries only per-language signal maturity, no package versions or publication dates. Split the claim: cite opentelemetry.io/status/ for 'Traces, Metrics and Logs Stable in .NET', and <https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore/> for '1.18.0, published 2026-08-21'. The 'actively maintained within 12 months' conclusion still holds, and holds more strongly.

### A9.13  [yes]

**Claim.** The OpenTelemetry Collector documentation states that direct export without a collector is acceptable at small scale - 'in a development or small-scale environment you can get decent results without a collector' - while recommending a collector in general because it handles 'retries, batching, encryption or even sensitive data filtering'.

- Source: OpenTelemetry Collector documentation
- URL: <https://opentelemetry.io/docs/collector/>
- Accessed: 2026-08-31
- Confidence: verified

### A9.14  [partially]

**Claim.** The OpenTelemetry Collector redaction processor - the component most often proposed as a PHI scrubbing control - is beta for traces but alpha for logs and metrics. Its README directs users to the attributes processor with delete actions for logs and metrics instead.

**Limit or threshold asserted.** Stability: alpha (logs, metrics); beta (traces)

- Source: opentelemetry-collector-contrib, processor/redactionprocessor README
- URL: <https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/main/processor/redactionprocessor/README.md>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The stability levels are exactly right (beta: traces; alpha: logs, metrics). The second half is fabricated: I fetched the raw README from main and grepped it  -  the string 'attributes processor' does not appear anywhere in the file, and the README presents the redaction processor itself as the mechanism for spans, logs AND metric datapoints. Delete the 'directs users to the attributes processor' sentence. The usable finding is simply that a PHI-scrubbing control on the log signal would rest on an alpha-stability component.

### A9.15  [partially]

**Claim.** Microsoft.Extensions.Compliance.Redaction is current and maintained: version 10.9.0 published 2026-08-11, targeting .NET 8/9/10, 4.0M total downloads, not deprecated. It provides ErasingRedactor and HmacRedactor; HmacRedactor is still experimental and raises diagnostic EXTEXP0002.

**Limit or threshold asserted.** 10.9.0, published 2026-08-11; HmacRedactor experimental (EXTEXP0002)

- Source: NuGet - Microsoft.Extensions.Compliance.Redaction; Microsoft Learn 'Data redaction in .NET'
- URL: <https://www.nuget.org/packages/Microsoft.Extensions.Compliance.Redaction/>
- Second source: <https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Version, date, downloads and non-deprecation are correct (add .NET Framework 4.6.2 to the target list). The experimental marking is superseded: in the 10.9.0 source neither HmacRedactor nor SetHmacRedactor is decorated [Experimental], so no EXTEXP0002 diagnostic is raised. EXTEXP0002 is still the repo's Compliance experiment ID but is no longer applied to these APIs. Drop 'HmacRedactor is still experimental (EXTEXP0002)'; if the design depends on that caveat, it no longer applies. Note also the NuGet page alone does not evidence which redactor types ship  -  that needs the API reference.

### A9.16  [yes]

**Claim.** Microsoft's redaction only applies through the compile-time logging source generator: it covers objects decorated with [LogProperties] and [TagProvider] used with [LoggerMessage], requires the Microsoft.Extensions.Telemetry 'extended logger' and builder.Logging.EnableRedaction(), and does not redact values passed as ordinary template parameters or via string interpolation - 'it will be written to the logs, regardless of whether it has a data classification'.

**Limit or threshold asserted.** Requires [LoggerMessage] source generator + Microsoft.Extensions.Telemetry + EnableRedaction()

- Source: Andrew Lock, 'Redacting sensitive data in logs with Microsoft.Extensions.Compliance.Redaction' (2023-12-12); Microsoft Learn data-redaction
- URL: <https://andrewlock.net/redacting-sensitive-data-with-microsoft-extensions-compliance/>
- Second source: <https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Supported. Caveat: this is a personal blog, not first-party documentation. For a compliance argument the same limitation should be corroborated against Microsoft Learn's redaction/telemetry docs before it is load-bearing.

### A9.17  [yes]

**Claim.** Bridging Microsoft's redaction to Serilog requires a third-party community package (Serilog.Redaction, by an individual author, v1.2.0 published 2026-03-02, ~356k downloads) - it is not part of the official Serilog organisation.

**Limit or threshold asserted.** 1.2.0, 2026-03-02, community-maintained

- Source: NuGet - Serilog.Redaction
- URL: <https://www.nuget.org/packages/Serilog.Redaction>
- Accessed: 2026-08-31
- Confidence: verified

### A9.18  [yes]

**Claim.** Destructurama.Attributed - the Serilog-native masking approach - is current: 5.3.0 published 2026-04-12, 46.3M total downloads, requires Serilog >= 4.3.0, targets netstandard2.0. It provides [NotLogged], [LogMasked], [LogReplaced], [LogWithName], [NotLoggedIfNull], [NotLoggedIfDefault], [LogAsScalar].

**Limit or threshold asserted.** 5.3.0, published 2026-04-12

- Source: NuGet - Destructurama.Attributed
- URL: <https://www.nuget.org/packages/destructurama.attributed/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All figures confirmed. The package also exposes MetadataTypeAttribute and AllowDestructuringOnlyExplicitlyMarkedProperties, which the claim's attribute list omits; the second is directly relevant to an allow-list rather than deny-list logging posture and is worth adding.

### A9.19  [yes]

**Claim.** Serilog.Enrichers.Sensitive (regex-based masking across whole log messages) is maintained under serilog-contrib: 2.1.0 published 2025-08-18, 23.2M total downloads, not deprecated.

**Limit or threshold asserted.** 2.1.0, 2025-08-18 (~12 months old at access date)

- Source: NuGet - Serilog.Enrichers.Sensitive
- URL: <https://www.nuget.org/packages/Serilog.Enrichers.Sensitive>
- Accessed: 2026-08-31
- Confidence: verified

### A9.20  [partially]

**Claim.** Serilog core is actively maintained (Serilog 4.4.0; Serilog.AspNetCore 10.0.0 with .NET 10 and AOT/trimming support). The official OTLP bridge, Serilog.Sinks.OpenTelemetry, is owned by the Serilog organisation with 53.4M downloads, but its latest release is 4.2.0 published 2025-05-31 - roughly 15 months old at access date, slightly outside a 12-month recency heuristic.

**Limit or threshold asserted.** Serilog 4.4.0 current; Sinks.OpenTelemetry 4.2.0 dated 2025-05-31

- Source: NuGet - Serilog, Serilog.Sinks.OpenTelemetry
- URL: <https://www.nuget.org/packages/Serilog.Sinks.OpenTelemetry>
- Second source: <https://www.nuget.org/packages/serilog/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The sink figures and the 15-month age are correct, and Serilog 4.4.0 is confirmed on its own NuGet page. Two gaps: (a) the cited URL is the sink's page and says nothing about Serilog core or Serilog.AspNetCore  -  cite <https://www.nuget.org/packages/Serilog> and <https://www.nuget.org/packages/Serilog.AspNetCore> separately; (b) 'Serilog.AspNetCore 10.0.0 with .NET 10 and AOT/trimming support' is unverified by any cited source and should be checked or dropped. Also state ownership precisely: owners are 'serilog' AND 'augustoproiete', not the Serilog organisation alone.

### A9.21  [partially]

**Claim.** .NET provides first-party log sampling via Microsoft.Extensions.Telemetry: AddTraceBasedSampler() and AddRandomProbabilisticSampler() with per-category, per-level and per-EventId probability rules, hot-reloadable through IOptionsMonitor. Microsoft's own guidance table says to apply sampling at Information, consider it at Warning, and not to sample Error or Critical. Only one sampler can be active at a time. Log buffering (circular in-memory buffers, flushed on demand) is a companion feature.

**Limit or threshold asserted.** Microsoft.Extensions.Telemetry 10.8.0; probability 0..1; never sample Error/Critical

- Source: Microsoft Learn - 'Log sampling' and 'Log buffering'
- URL: <https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/log-sampling>
- Second source: <https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/log-buffering>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The API names, rule dimensions, IOptionsMonitor hot-reload, 0..1 probability, one-sampler-at-a-time and the full level guidance table are all confirmed. Two things are not on this page: (a) the version '10.8.0'  -  the page shows only Version="*", so the version claim is unsourced and, given Microsoft.Extensions.Compliance.Redaction is at 10.9.0, likely already superseded; (b) log buffering, which is documented on a separate page (.../logging/log-buffering) and must be cited there. Drop the version or cite NuGet for it.

### A9.22  [yes]

**Claim.** ABP's audit log stores HTTP action parameters as serialised JSON: AuditLogActionInfo.Parameters is 'A JSON formatted text representing the parameters passed to the method'. AuditLogInfo also stores Url, HttpMethod, ClientIpAddress, BrowserInfo, UserId/UserName, TenantId. Entity changes are captured with per-property OriginalValue and NewValue.

- Source: ABP Framework documentation - Audit Logging
- URL: <https://abp.io/docs/latest/framework/infrastructure/audit-logging>
- Accessed: 2026-08-31
- Confidence: verified

### A9.23  [partially]

**Claim.** ABP provides levers to keep sensitive values out of audit logs: AbpAuditingOptions.IgnoredTypes ('This list is also used while serializing the action parameters') and the [DisableAuditing] attribute applied at entity-property level (Microsoft's own example being a Password property).

- Source: ABP Framework documentation - Audit Logging
- URL: <https://docs.abp.io/en/abp/latest/Audit-Logging>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Content confirmed, but two citation defects. (1) The cited URL is stale: docs.abp.io/en/abp/latest/Audit-Logging returns HTTP 308 to <https://abp.io/docs/en/abp/latest/Audit-Logging>  -  cite the live URL. (2) The Password example is ABP's own documentation example, not 'Microsoft's own example'; that attribution is wrong and should be corrected to ABP.

### A9.24  [yes]

**Claim.** ABP Commercial's Audit Logging (Pro) module already ships audit-log retention: an ExpiredAuditLogDeleter background worker (ExpiredAuditLogDeleterOptions.Period, default 1 day, plus CronExpression for Hangfire/Quartz), a host-level 'Cleanup Service System Wide' toggle and an 'Expired Item Deletion Period' setting, overridable per tenant. Crucially: 'If you don't enable the Cleanup Service System Wide from the host side under Settings -> Audit logs -> Global, it won't remove the expired audit logs, even if there are tenant specific settings.' Access is gated by AuditLogging.AuditLogs, .Export and .SettingManagement permissions.

**Limit or threshold asserted.** Period default = 1 day (worker cadence, not retention); cleanup does nothing unless host-wide toggle is on

- Source: ABP Audit Logging Module (Pro) documentation
- URL: <https://abp.io/docs/latest/modules/audit-logging-pro>
- Second source: <https://github.com/abpio/abp-commercial-docs/blob/dev/en/modules/audit-logging.md>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed, including the crucial host-toggle gate. Two additions worth carrying: the module also defines per-entity permissions of the form AuditLogging.ViewChangeHistory:{EntityTypeFullName}, and the settings path for 'Expired Item Deletion Period' is Settings -> Audit Log -> General (distinct from the Global tab that carries the system-wide toggle).

### A9.25  [partially]

**Claim.** ABP swallows audit-store failures. In AuditingStore.cs the save is wrapped in catch (Exception ex) { Logger.LogWarning("Could not save the audit log object: " + Environment.NewLine + auditInfo.ToString()); Logger.LogException(ex, LogLevel.Error); }. The request is not failed. Separately, AuditLogInfo.ToString() emits 'AUDIT LOG: [status: METHOD] {Url}', '- UserName - UserId : ...', '- ClientIpAddress : ...', '- ExecutionDuration : ...' - so an audit-write failure copies request URL, username, user id and client IP into the ordinary application log.

**Limit or threshold asserted.** Exception is caught and logged; HTTP request still succeeds

- Source: abpframework/abp source: AuditingStore.cs and AuditLogInfo.cs (ref 1f7b7a56503515a6b26da6101ebc9d3921b461d9)
- URL: <https://github.com/abpframework/abp/blob/dev/modules/audit-logging/src/Volo.Abp.AuditLogging.Domain/Volo/Abp/AuditLogging/AuditingStore.cs>
- Second source: <https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Auditing/Volo/Abp/Auditing/AuditLogInfo.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The swallow is CONDITIONAL, which the claim states unconditionally. The try/catch executes only when AbpAuditingOptions.HideErrors is true; when HideErrors is false, SaveLogAsync is awaited bare and the exception propagates. HideErrors defaults to true (AbpAuditingOptions ctor: HideErrors = true), so the described behaviour is the default  -  but it is a one-line configuration flip, which is material to the remediation, not merely a footnote. Second correction, in the claim's favour: AuditLogInfo.ToString() leaks MORE than stated. Beyond the 'AUDIT LOG: [status: METHOD] {Url}', UserName/UserId, ClientIpAddress and ExecutionDuration lines, it also emits every action's Parameters (the serialised JSON payload) and, for each entity change, 'PropertyName: OriginalValue -> NewValue'. So an audit-write failure can copy request bodies and old/new field values into the ordinary application log, not just URL/user/IP.

### A9.26  [partially]

**Claim.** ABP's default background worker manager is an in-process AbpAsyncTimer and does not persist state or maintain execution records. Persistent scheduling requires the separate Volo.Abp.BackgroundWorkers.Hangfire integration (HangfirePeriodicBackgroundWorkerAdapter), which is a different package from Volo.Abp.BackgroundJobs.HangFire (the on-demand job manager).

**Limit or threshold asserted.** Default worker manager = in-memory timer, zero persistence

- Source: ABP Framework documentation - Background Workers; Background Workers Hangfire
- URL: <https://abp.io/docs/latest/framework/infrastructure/background-workers/index>
- Second source: <https://docs.abp.io/en/abp/latest/Background-Workers-Hangfire>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The substance (in-process timer, no persistence, no execution records) is supported in effect, but the page does not name the default manager implementation, does not use the words 'HangfirePeriodicBackgroundWorkerAdapter', and does not give either NuGet package ID. The persistence sentence it does carry is scoped to dynamic worker handlers, not to worker execution history generally. Also note the page lists a THIRD integration, TickerQ, which the claim omits. Cite the Hangfire background-workers integration page for the package/adapter names, and soften 'default worker manager = in-memory timer, zero persistence' to what the page actually says.

### A9.27  [partially]

**Claim.** Hangfire's SQL Server schema gives two directly queryable liveness signals: [Server] (Id, Data, LastHeartbeat) with an index IX_HangFire_Server_LastHeartbeat, updated roughly every 5 seconds; and [Set] (Key, Score, Value, ExpireAt) where recurring jobs are stored under Key='recurring-jobs' with Score carrying the next-execution timestamp. A recurring-job scheduler component checks on a minute-based interval.

**Limit or threshold asserted.** Server heartbeat ~5s; recurring-job scheduler polls per minute

- Source: Hangfire SQL Server Install.sql; Hangfire documentation - Performing Recurrent Tasks
- URL: <https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.SqlServer/Install.sql>
- Second source: <https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The schema half is confirmed (both tables, both column sets, IX_HangFire_Server_LastHeartbeat). Four things the cited Install.sql does NOT and cannot support: the ~5 second heartbeat cadence; the Key='recurring-jobs' convention (the string 'recurring' does not appear in the file at all); Score carrying the next-execution timestamp; and the minute-based scheduler interval (that one IS supported, but by the recurrent-tasks doc page, not this file). Two schema details to fix: [Set] also has an [Id] IDENTITY column, and the current [Set] score index is the composite IX_HangFire_Set_Score on ([Key],[Score]), not a single-column index. Cite Install.sql for the schema and the Hangfire docs (or source) for the cadences and key conventions.

### A9.28  [partially]

**Claim.** Hangfire's documented and observed silent-failure mode is that the dashboard continues to display a plausible 'next execution' for a recurring job indefinitely while no server is running to enqueue it; Hangfire's documentation warns 'Your Hangfire Server instance should be always on to perform scheduling and processing logic', and manually triggering a job does not recalculate its next execution time.

- Source: Hangfire documentation - Performing Recurrent Tasks; Hangfire recurring-job monitoring analysis
- URL: <https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html>
- Second source: <https://cronradar.com/blog/hangfire-monitoring>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Two of the three elements are verbatim-confirmed. The first  -  'the dashboard continues to display a plausible next execution indefinitely while no server is running to enqueue it'  -  is NOT in the documentation; the page does not address dashboard behaviour when no server is running. Drop the word 'documented' from 'documented and observed silent-failure mode' and present that part as an observed behaviour needing its own local evidence, or cite a Hangfire issue. The claim's own 'partial' confidence is the right label.

### A9.29  [yes]

**Claim.** SQL Server 2022 'Ledger for SQL Server' is available in every edition - Enterprise, Standard, Web, Express with Advanced Services, and Express. It is therefore not blocked by an edition constraint even if MSSQL_PID moves off Developer.

**Limit or threshold asserted.** Ledger = Yes in all editions. Express max database size 10 GB; Standard buffer pool 128 GB

- Source: Microsoft Learn - Editions and Supported Features of SQL Server 2022 (Security table)
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022?view=sql-server-ver17>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed exactly. Note the page's own footnotes if this design outlives SQL Server 2022: Web edition is not available in SQL Server 2025 (17.x) and later, and from SQL Server 2025 Express includes what was Express with Advanced Services  -  so the edition matrix cited here is version-specific.

### A9.30  [yes]

**Claim.** SQL Server ledger provides tamper-evidence, not tamper-prevention: 'Ledger can't prevent such attacks but guarantees that any tampering will be detected when the ledger data is verified.' Append-only ledger tables 'block updates and deletions at the API level'. Database digests 'can be periodically generated and stored outside the database in tamper-proof storage' - and an attacker able to modify the digests can defeat the whole scheme.

**Limit or threshold asserted.** SHA-256 Merkle tree; append-only tables forbid UPDATE and DELETE

- Source: Microsoft Learn - Ledger Overview
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/security/ledger/ledger-overview?view=sql-server-ver16>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Fully supported, including the attacker-modifies-digests scenario. One presentational caveat: the ver16 URL resolves to a page whose default moniker is now ver17; the content applies to 'SQL Server 2022 (16.x) and later versions', so the citation is sound but the ?view= parameter is doing no work.

### A9.31  [yes]

**Claim.** ASP.NET Core emits built-in metrics through System.Diagnostics.Metrics with no third-party library, across meters including Microsoft.AspNetCore.Hosting (http.server.request.duration), Microsoft.AspNetCore.Server.Kestrel (kestrel.active_connections), Microsoft.AspNetCore.RateLimiting, Microsoft.AspNetCore.Diagnostics (aspnetcore.diagnostics.exceptions), and - new in ASP.NET Core 10.0 - Microsoft.AspNetCore.Authentication and Microsoft.AspNetCore.Authorization (aspnetcore.authentication.challenges, aspnetcore.authorization.attempts).

**Limit or threshold asserted.** Auth/authz metrics require ASP.NET Core 10.0 or later

- Source: Microsoft Learn - ASP.NET Core built-in metrics
- URL: <https://learn.microsoft.com/en-us/aspnet/core/log-mon/metrics/built-in?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed. Note the page is now an index that fans out to per-topic pages (http, diagnostics, blazor, security); the canonical path is /aspnet/core/metrics/built-in and instrument-level detail (types, units, attributes) lives on the child pages, so cite the child page if any specific instrument's attributes become load-bearing.

### A9.32  [partially]

**Claim.** Microsoft.Data.SqlClient exposes 16 event counters including active-hard-connections, hard-connects/disconnects, soft-connects/disconnects and number-of-non-pooled-connections, available from SqlClient 3.0.0 on .NET Core 3.1+, and consumable by OpenTelemetry via EventCountersInstrumentation against the 'Microsoft.Data.SqlClient.EventSource' source.

**Limit or threshold asserted.** Requires Microsoft.Data.SqlClient >= 3.0.0

- Source: Microsoft Learn - Event counters in SqlClient
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/event-counters?view=sql-server-ver16>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Counts, names, version floor and EventSource name are confirmed (precise names are active-soft-connects / soft-connects / soft-disconnects). Two corrections. (1) The cited page says nothing about OpenTelemetry or EventCountersInstrumentation  -  that half is unsourced. (2) More importantly, that consumption path is not production-grade: OpenTelemetry.Instrumentation.EventCounters has never shipped a stable release; its current NuGet version is 1.18.0-alpha.1 (2026-08-21), i.e. prerelease-only. Presenting it as an available bridge without flagging alpha/prerelease-only status overstates its readiness. Also add '.NET Standard 2.1+' to the framework floor.

### A9.33  [yes]

**Claim.** ASP.NET Core health checks support RequireHost and RequireAuthorization, and Microsoft warns that host-based APIs are spoofable: 'API that relies on the Host header, such as HttpRequest.Host and RequireHost, are subject to potential spoofing by clients.' Microsoft also advises against query-based DB probes: 'Merely making a successful connection to the database is sufficient... choose a simple SELECT query, such as SELECT 1.'

**Limit or threshold asserted.** Healthy/Degraded = 200, Unhealthy = 503 by default

- Source: Microsoft Learn - Health checks in ASP.NET Core
- URL: <https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Fully supported. Worth making explicit in the design: because Degraded also returns 200, a load balancer or uptime probe keyed on status code alone cannot distinguish Healthy from Degraded  -  ResultStatusCodes must be overridden or the response body parsed.

### A9.34  [partially]

**Claim.** The AspNetCore.Diagnostics.HealthChecks / HealthChecks UI project (Xabaril) has paused upstream maintenance. A community continuation, DotNetDiag HealthChecks, states: 'A community-maintained continuation of Xabaril/AspNetCore.Diagnostics.HealthChecks... With upstream maintenance paused, DotNetDiag carries the work forward.' The Xabaril packages are at 9.0.0 and are not maintained or supported by Microsoft.

**Limit or threshold asserted.** Xabaril packages at 9.0.0; upstream maintenance paused

- Source: DotNetDiag HealthChecks; NuGet AspNetCore.HealthChecks.UI
- URL: <https://dotnetdiag.github.io/>
- Second source: <https://www.nuget.org/packages/AspNetCore.HealthChecks.UI>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The pause-and-fork quote is verbatim-confirmed. Two gaps: (a) the '9.0.0' version is not on the cited page  -  I confirmed it independently on NuGet (AspNetCore.HealthChecks.SqlServer 9.0.0, 2024-12-19, ~20 months stale at access date, and NOT flagged deprecated on NuGet), so cite NuGet for it; (b) 'not maintained or supported by Microsoft' does not appear on dotnetdiag.github.io either  -  that statement comes from Microsoft's own health-checks documentation and should be cited there. Also note the source of the 'paused' assertion is the fork's own marketing page, i.e. an interested party; corroborate with the upstream repository's commit/release activity before relying on it.

### A9.35  [yes]

**Claim.** RFC 9110 section 7.2: 'A user agent MUST generate a Host header field in a request unless it sends that information as an :authority pseudo-header field.' The Host field 'provides the host and port information from the target URI, enabling the origin server to distinguish among resources while servicing requests for multiple host names.' It further warns that 'Since the host and port information acts as an application-level routing mechanism, it is a frequent target for malware seeking to poison a shared cache or redirect a request to an unintended server.'

**Limit or threshold asserted.** MUST-level requirement on user agents

- Source: RFC 9110, HTTP Semantics, Section 7.2 Host and :authority
- URL: <https://www.rfc-editor.org/rfc/rfc9110.txt>
- Accessed: 2026-08-31
- Confidence: verified

### A9.36  [yes]

**Claim.** RFC 6066 section 3 defines Server Name Indication: clients MAY include a 'server_name' extension in the ClientHello to 'facilitate secure connections to servers that host multiple virtual servers at a single underlying network address'. A probe addressed by IP therefore supplies neither a usable SNI name nor a usable Host header.

- Source: RFC 6066, TLS Extensions, Section 3 Server Name Indication
- URL: <https://www.rfc-editor.org/rfc/rfc6066.txt>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Supported, and stronger than stated. The decisive sentence for an IP-addressed probe is 'Literal IPv4 and IPv6 addresses are not permitted in "HostName"' (s3)  -  cite that rather than resting the point on the MAY. Minor: the RFC writes multiple 'virtual' servers with quote marks around virtual; keep them in a verbatim quotation.

### A9.37  [yes]

**Claim.** Google SRE guidance: 'The four golden signals of monitoring are latency, traffic, errors, and saturation.' On alert load: 'I can only react with a sense of urgency a few times a day before I become fatigued.' On design: 'Every page should be actionable'; 'If a page merely merits a robotic response, it shouldn't be a page'; and 'it's better to spend much more effort on catching symptoms than causes'.

**Limit or threshold asserted.** 'a few times a day' is the stated human limit; no numeric alert cap given

- Source: Google SRE Book, Chapter 6 - Monitoring Distributed Systems
- URL: <https://sre.google/sre-book/monitoring-distributed-systems/>
- Accessed: 2026-08-31
- Confidence: verified

### A9.38  [yes]

**Claim.** Google SRE guidance on routing and on black-box monitoring: 'Teams send their page-worthy alerts to their on-call rotation and their important but subcritical alerts to their ticket queues. All other alerts should be retained as informational data for status dashboards.' White-box monitoring alone is insufficient because 'queries lost due to a server crash never make a sound'; black-box probing detects DNS and traffic-path failures outside the service itself.

- Source: Google SRE Book, Chapter 10 - Practical Alerting
- URL: <https://sre.google/sre-book/practical-alerting/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Supported. Quote the full sentence rather than the fragment  -  'The queries that never make it due to a DNS error are invisible, while queries lost due to a server crash never make a sound'  -  since the DNS half is the part that actually justifies probing by hostname rather than by IP.

### A9.39  [yes]

**Claim.** Google SRE Workbook on alert quality: alerting strategies should be evaluated on precision, recall, detection time and reset time; a naive short-window alert on a 99.9% SLO could 'receive up to 144 alerts per day every day, not act upon any alerts, and still meet the SLO'. It recommends multiwindow multi-burn-rate alerts with page at 2% budget in 1 hour or 5% in 6 hours, and a ticket at 10% budget in 3 days.

**Limit or threshold asserted.** Page: 2%/1h or 5%/6h. Ticket: 10%/3d. Counter-example: 144 alerts/day

- Source: Google SRE Workbook, Chapter 5 - Alerting on SLOs
- URL: <https://sre.google/workbook/alerting-on-slos/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Confirmed. Add the context the number depends on, or it is meaningless when transplanted: 144/day is the worst case for a 99.9% SLO over a 30-day window alerted on a 10-minute window. Also note the Workbook presents 2%/1h, 5%/6h, 10%/3d as a suggested starting point, not a prescription.

### A9.40  [yes]

**Claim.** The dead-man's-switch / Watchdog pattern is established practice: an alert that is 'always firing, and should always be firing in Alertmanager and always fire against a receiver', routed to an external service with a short repeat interval, so that its absence indicates the alerting pipeline itself has failed.

- Source: kube-prometheus runbooks - Watchdog
- URL: <https://runbooks.prometheus-operator.dev/runbooks/general/watchdog/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Supported. The claim's phrasing 'always firing, and should always be firing' compresses the source's 'always firing, therefore it should always be firing'  -  quote it as written. The 'short repeat interval' detail is not on this page and should be dropped or sourced elsewhere.

### A9.41  [partially]

**Claim.** NIST SP 800-92 Rev. 1, 'Cybersecurity Log Management Planning Guide', remains an Initial Public Draft. Released 2023-10-11, comment period closed 2023-11-29, no final publication as of access date. It should be cited as draft guidance, not settled standard. The 2006 SP 800-92 final remains the only finalised version.

**Limit or threshold asserted.** Draft since 2023-10-11; no final as at 2026-08-31

- Source: NIST CSRC - SP 800-92 Rev. 1 (Draft)
- URL: <https://csrc.nist.gov/pubs/sp/800/92/r1/ipd>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The draft status, both dates and the absence of a final are confirmed, and the 'cite as draft guidance' conclusion holds. But the last sentence  -  'The 2006 SP 800-92 final remains the only finalised version'  -  is not evidenced by this page, which does not mention the 2006 publication at all. Either cite <https://csrc.nist.gov/pubs/sp/800/92/final> for it or drop it.

### A9.42  [could-not-check]

**Claim.** Storage projection for audit data. From the measured 16 appointments -> ~1,450 AbpAuditLogs rows and ~2,689 entity property-change rows, the coefficient is ~91 audit rows and ~168 property-change rows per appointment. Assuming ~1.5 KB per audit row (they carry Url, BrowserInfo, exceptions) and ~0.3 KB per property-change row, plus ~40% index overhead, gives roughly 0.25-0.35 MB per appointment. At 11 offices and a modest 5 appointments per office per business day (13,750/yr) that is ~4-5 GB/yr; at 20/day (55,000/yr) it is ~17-22 GB/yr; at 33 offices, triple. Six-year retention at the moderate figure is of order 100-130 GB for 11 offices and 300-400 GB for 33. Free disk today is ~9.7 GB. Note the measurement omits AbpAuditLogActions and AbpEntityChanges rows, so it understates.

**Limit or threshold asserted.** ~0.25-0.35 MB per appointment (my estimate); row sizes must be measured with sp_spaceused before relying on this

- Source: My arithmetic on the brief's measured figures
- URL: n/a
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: No source given, and the measured inputs (1,450 audit rows / 2,689 property-change rows from 16 appointments, 9.7 GB free disk) are not independently checkable here. The per-appointment coefficients are arithmetically right (1450/16 = 90.6; 2689/16 = 168.1). The stated per-appointment size is right at the low end and the row-size assumptions are the author's own: 91 x 1.5 KB + 168 x 0.3 KB = 187 KB, x1.4 index overhead = 0.26 MB, so the honest band is ~0.26-0.30 MB, not 0.25-0.35. The downstream figures then drift high by roughly 15-25%: 55,000 appointments/yr x 0.26-0.35 MB = 14-19 GB/yr (claimed 17-22); six years at that rate = 86-115 GB for 11 offices (claimed 100-130) and 258-346 GB for 33 (claimed 300-400). The claim's own caveat is the right one and should be binding: measure with sp_spaceused before any capacity decision depends on this. Two further gaps  -  the 250-business-day figure behind 13,750/yr and 55,000/yr is never stated, and the projection assumes a flat rate with no growth. Keep this labelled unverified; it must not be cited as a finding.

---

## Area: iac-reproducibility

Verification verdict for this area: **minor-corrections** (44 claims checked)

### A10.1  [partially]

**Claim.** Docker Compose substitutes an empty string for an interpolation variable that is not set, rather than failing  -  this is the exact mechanism by which omitting --env-file silently blanks every secret.

**Limit or threshold asserted.** Unset variable -> empty string; a WARN line is printed but the command proceeds

- Source: Docker Docs  -  Set or change predefined environment variables / Variable interpolation
- URL: <https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/>
- Second source: <https://github.com/docker/compose/issues/4987>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The empty-string substitution is confirmed verbatim. The 'WARN line is printed' half is NOT on this page - the page mentions no warning output at all (it illustrates the hazard with an invalid 'postgres:' image reference instead). Compose does emit 'WARN[0000] The "X" variable is not set. Defaulting to a blank string.' in practice, but cite CLI behaviour for that, not this page. State the mechanism as: unset -> empty string -> command proceeds.

### A10.2  [yes]

**Claim.** Compose supports mandatory-variable interpolation forms: ${VAR:?error} uses the variable if set and non-empty, 'otherwise exit with error'; ${VAR?error} uses it if set, otherwise exit with error. This is the documented fail-fast fix for the --env-file trap.

**Limit or threshold asserted.** ${VAR:?err} and ${VAR?err} both exit with error

- Source: Docker Docs  -  Interpolation (Compose file reference)
- URL: <https://docs.docker.com/reference/compose-file/interpolation/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both required-value forms are documented exactly as claimed, alongside the default (:-, -) and alternative (:+, +) forms.

### A10.3  [partially]

**Claim.** Compose precedence for interpolation is: shell environment, then a file set by --env-file, then an .env file in the project directory if --env-file is not set. Passing --env-file overrides the default file path. So naming the secrets file .env in the project directory removes the need for the flag entirely.

**Limit or threshold asserted.** 3-level precedence; project directory resolved from --project-directory, then the directory of the first -f file, then PWD

- Source: Docker Docs  -  Variable interpolation
- URL: <https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The 3-level precedence and the project-directory resolution order are confirmed verbatim. Two qualifications the claim omits: (a) this governs INTERPOLATION of the Compose file only - it does not populate container environments, which is env_file/environment, so a .env rename does not by itself carry secrets into containers; (b) because the project directory falls back to PWD when no -f is given, an automatic .env still depends on invocation location, which only partially satisfies the 'must not depend on working directory' requirement. Pair the .env rename with -f/--project-directory or with ${VAR:?} guards.

### A10.4  [yes]

**Claim.** Compose env_file long syntax supports required: true (the default), which makes Compose fail to process the configuration when the file is missing; required: false makes Compose silently ignore the entry. Introduced in Compose 2.20.0.

**Limit or threshold asserted.** required defaults to true; introduced in Compose v2.20.0

- Source: Docker Docs  -  Services top-level element, env_file
- URL: <https://docs.docker.com/reference/compose-file/services/#env_file>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Default true, silent-ignore on false, and the 2.20.0 introduction note are all present on the page.

### A10.5  [yes]

**Claim.** nginx resolves upstream server addresses at configuration load and the resulting IPs are held in worker memory for the lifetime of that configuration; when a backend IP changes nginx keeps routing to the stale address until reloaded. This is the mechanism behind trap (b).

**Limit or threshold asserted.** Resolution occurs once at configuration load

- Source: GetPageSpeed  -  NGINX Upstream Resolve: Dynamic DNS for Load Balancing (updated 7 June 2026)
- URL: <https://www.getpagespeed.com/server-setup/nginx/nginx-upstream-resolve>
- Second source: <https://nginx.org/en/docs/http/ngx_http_upstream_module.html#server>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Supported, but note the source is a commercial third-party blog (GetPageSpeed sells nginx modules), not nginx.org. Primary corroboration exists: nginx.org's own 'resolve' parameter text ('monitors changes of the IP addresses ... without the need of restarting nginx') only makes sense against exactly this default. Prefer citing nginx.org for the mechanism and keep this URL as supporting colour.

### A10.6  [yes]

**Claim.** Since nginx 1.27.3 (26 Nov 2024) the 'resolve' parameter of the 'server' directive in an 'upstream' block is available in open-source nginx, along with 'resolver' and 'resolver_timeout' directives inside the upstream block. Before 1.27.3 'resolve' was commercial-subscription only. It requires the server group to reside in shared memory (the 'zone' directive) and a resolver to be configured.

**Limit or threshold asserted.** OSS from 1.27.3 mainline (26 Nov 2024); requires 'zone' + 'resolver'; stable branch from 1.28.0; current nginx 1.31.4 dated 19 Aug 2026

- Source: nginx CHANGES + ngx_http_upstream_module documentation
- URL: <https://nginx.org/en/docs/http/ngx_http_upstream_module.html#server>
- Second source: <https://mailman.nginx.org/pipermail/nginx-announce/2024/WSOA5BERDSWSFOBY6H5VO7SICBG6R5B5.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None on substance. Cross-check on nginx.org confirms mainline nginx-1.31.4 released 2026-08-19; note for accuracy that the current STABLE line is 1.30.4 (2026-07-15), not 1.28.x - 1.28.0 was merely the first stable branch to carry the feature, which is what the claim says.

### A10.7  [partially]

**Claim.** The variable-in-proxy_pass workaround (the fix currently applied only to the MinIO block) forces per-request resolution but forfeits load balancing across multiple servers, passive health checking via max_fails, backup server failover, and keepalive connection pooling, and adds per-request DNS overhead. nginx also documents that when variables are used and a URI is specified in the directive, 'it is passed to the server as is, replacing the original request URI'.

- Source: nginx ngx_http_proxy_module documentation + GetPageSpeed analysis
- URL: <https://nginx.org/en/docs/http/ngx_http_proxy_module.html#proxy_pass>
- Second source: <https://www.getpagespeed.com/server-setup/nginx/nginx-upstream-resolve>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The URI-replacement quote is verbatim and correct, and the resolver requirement is documented. The forfeited-capability list (load balancing, max_fails, backup, keepalive, per-request DNS cost) is NOT stated anywhere on this page - it is inference. Note also the page contradicts the flat framing: with a variable, a domain name IS first searched among described server groups, so an upstream block named in a variable proxy_pass is still matched (and then behaves statically). Either drop the unsourced list or label it as reasoning from the upstream-module semantics rather than as documented behaviour.

### A10.8  [yes]

**Claim.** nginx's resolver directive caches answers using the response TTL by default; an optional valid=<time> parameter overrides it. Before 1.1.9 nginx always cached for 5 minutes.

**Limit or threshold asserted.** Default = response TTL; overridable with valid=<time>

- Source: nginx ngx_http_core_module  -  resolver
- URL: <https://nginx.org/en/docs/http/ngx_http_core_module.html#resolver>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All three elements are verbatim.

### A10.9  [yes]

**Claim.** Docker's embedded DNS server for user-defined networks is at 127.0.0.11 and there is no IPv6 equivalent  -  this is the resolver address an nginx container must be pointed at for dynamic re-resolution of sibling service names.

**Limit or threshold asserted.** 127.0.0.11, IPv4 only

- Source: Docker Docs  -  Networking / DNS services
- URL: <https://docs.docker.com/engine/network/#dns-services>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. 127.0.0.11 and the explicit no-IPv6-equivalent statement are both on the page.

### A10.10  [partially]

**Claim.** Compose determines the project name by precedence: -p flag, then COMPOSE_PROJECT_NAME, then the top-level name: attribute, then the base name of the project directory. Compose uses the project name to isolate environments from each other.

**Limit or threshold asserted.** 4-level precedence; default is the directory basename

- Source: Docker Docs  -  Specify a project name
- URL: <https://docs.docker.com/compose/how-tos/project-name/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The precedence has FIVE levels, not four. The claim drops level 5, 'the base name of the current directory if no Compose file is specified'. That omission matters here precisely because it is a second, distinct directory-name dependency - exactly the failure mode the requirement is meant to close. State it as a 5-level precedence.

### A10.11  [partially]

**Claim.** Because Compose namespaces containers, networks and named volumes by project name and the default project name is the directory basename, a rebuild performed in a differently-named directory attaches to a fresh, empty set of sqldata/redisdata/miniodata volumes and comes up healthy but empty  -  a false pass on a reproducibility rehearsal. Pinning a top-level name: prevents this.

- Source: Derived from the documented project-name precedence above
- URL: <https://docs.docker.com/compose/how-tos/project-name/>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The cited page supports environment isolation by project name and supports that the default name is the directory basename, so the 'pin a top-level name:' remedy is sound. It does NOT state that containers, networks and named volumes are namespaced by project name, and it says nothing about the empty-volume false-pass scenario. That is correct Compose behaviour but is unsourced here - cite the volumes/networks top-level reference (which documents the project-prefixed naming and the external: escape hatch) as well, or present the false-pass narrative as reasoned consequence rather than documented behaviour.

### A10.12  [partially]

**Claim.** Compose depends_on supports condition: service_completed_successfully, meaning the dependency must run to successful completion before the dependent starts  -  the mechanism gating authserver and api on db-migrator. However, a reported defect has Compose hang indefinitely rather than exit when a service in an indirect depends_on chain fails: 'This stack never finishes! It reports the failure of step_1 and then does nothing'.

**Limit or threshold asserted.** service_completed_successfully introduced in Compose 2.20.0; hang reported from Docker Desktop 4.20, issue now closed but resolution details not retrievable

- Source: Docker Docs  -  depends_on; docker/compose issue #10728
- URL: <https://docs.docker.com/reference/compose-file/services/#depends_on>
- Second source: <https://github.com/docker/compose/issues/10728>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The condition and its semantics are confirmed verbatim. The version attribution is WRONG: the page carries no version note for service_completed_successfully; the '2.20.0' note on that page belongs to the env_file required attribute (claim 4). service_completed_successfully is part of the Compose Specification and shipped in Compose v2 substantially earlier than 2.20.0 - remove the version claim or source it elsewhere. The hang defect, the Docker Desktop 4.20 attribution and the quoted issue text are supported by NOTHING at this URL; either cite the docker/compose issue directly or drop it. Do not build the 'MUST NOT hang' requirement's justification on an unlocatable, admittedly-closed issue.

### A10.13  [yes]

**Claim.** Compose v5.3.0 (2 July 2026) added native pre_start init containers. They run sequentially in ephemeral containers before the service container; 'If any step exits with a non-zero code, the service will not start'; steps are skipped on subsequent runs if the step previously succeeded and its definition has not changed, unless --force-recreate is used. Docker positions this as preferable to the peer-service + depends_on pattern.

**Limit or threshold asserted.** Compose v5.3.0, 2 July 2026; per_replica: true not yet supported

- Source: Docker Docs  -  Use init containers in Compose
- URL: <https://docs.docker.com/compose/how-tos/init-containers/>
- Second source: <https://github.com/docker/compose/releases>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None on behaviour. The version/date is not stated on the docs page itself but is confirmed independently at github.com/docker/compose/releases: v5.3.0, 2 July 2026, release notes 'This release introduces native support for init containers'. Cite both URLs.

### A10.14  [yes]

**Claim.** Docker Compose is actively and rapidly maintained: v5.5.0 published 17 August 2026, v5.4.0 on 3 August 2026, v5.3.1 on 7 July 2026.

**Limit or threshold asserted.** Latest v5.5.0, 17 Aug 2026

- Source: docker/compose GitHub releases
- URL: <https://github.com/docker/compose/releases>
- Second source: <https://www.gitwatchman.com/track/docker/compose>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All three versions and dates match, and v5.5.0 is the latest.

### A10.15  [partially]

**Claim.** Compose supports gated bring-up: --wait 'Wait for services to be running|healthy. Implies detached mode'; --wait-timeout sets 'Maximum duration in seconds to wait for the project to be running|healthy'; --abort-on-container-failure 'Stops all containers if any container exited with failure'. These are the primitives that make a deploy fail loudly and within a bounded time.

**Limit or threshold asserted.** --wait-timeout is in seconds

- Source: Docker Docs  -  docker compose up CLI reference
- URL: <https://docs.docker.com/reference/cli/docker/compose/up/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All three descriptions are verbatim and --wait-timeout is indeed in seconds - but the claim truncates --abort-on-container-failure's description, dropping 'Incompatible with -d'. Since --wait 'Implies detached mode', the two flags are mutually exclusive and cannot be composed into one gated bring-up. Correct framing: use --wait with --wait-timeout for the bounded-time non-zero-exit requirement (this is the pair that satisfies it); --abort-on-container-failure is a separate, attached-mode alternative. Verify the chosen combination's exit code in the target Compose version before writing it into a runbook.

### A10.16  [yes]

**Claim.** Compose image references may specify a digest: image must follow '[<registry>/][<project>/]<image>[:<tag>|@<digest>]', e.g. redis@sha256:0ed5d59... pull_policy values include always, never, missing (default), build, daily, weekly and every_<duration>.

**Limit or threshold asserted.** pull_policy default is 'missing'

- Source: Docker Docs  -  Services top-level element (image, pull_policy)
- URL: <https://docs.docker.com/reference/compose-file/services/#pull_policy>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None on substance. One nuance to preserve: the page qualifies the default as 'missing' when NOT using the Compose Build Specification - where a build section is present the effective default differs. Say 'missing (absent a build section)' rather than a flat 'default is missing'.

### A10.17  [yes]

**Claim.** NIST SP 800-190 4.2.2 directs that 'operational practices should emphasize accessing images using immutable names that specify discrete versions of images to be used' and cautions that a 'latest' tag 'is only a label attached to the image and not a guarantee of freshness'.

**Limit or threshold asserted.** SP 800-190 published 25 Sept 2017; CSRC shows status Final with no Rev 1 in development as of this access

- Source: NIST SP 800-190, Application Container Security Guide (Sept 2017), 4.2.2
- URL: <https://nvlpubs.nist.gov/nistpubs/specialpublications/nist.sp.800-190.pdf>
- Second source: <https://csrc.nist.gov/pubs/sp/800/190/final>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Verified by text extraction from the PDF; both passages sit in section 4.2.2 'Stale images in registries' as claimed. Publication date September 2017 (CSRC: final, 25 September 2017) and CSRC shows no Rev 1 in development - also confirmed.

### A10.18  [yes]

**Claim.** NIST SP 800-190 states containers 'should be operated as stateless entities that are deployed but not changed'; that with containers 'these updates must be made upstream in the images themselves, which are then redeployed'; and that 'Host OSs should be operated in an immutable manner with no data or state stored uniquely and persistently on the host', which 'provides a more trustworthy way to identify anomalies and configuration drift'.

- Source: NIST SP 800-190 2.3, 3, 4.5.3
- URL: <https://nvlpubs.nist.gov/nistpubs/specialpublications/nist.sp.800-190.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All four quoted fragments verified verbatim in the PDF.

### A10.19  [yes]

**Claim.** NIST SP 800-190 4.1.4 on embedded clear-text secrets: 'Secrets should be stored outside of images and provided dynamically at runtime as needed.'

- Source: NIST SP 800-190 4.1.4
- URL: <https://nvlpubs.nist.gov/nistpubs/specialpublications/nist.sp.800-190.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Quote and section number both correct.

### A10.20  [yes]

**Claim.** Docker documents that environment variables are a poor secrets carrier: 'Environment variables are often available to all processes, and it can be difficult to track access', and they 'can also be printed in logs when debugging errors without your knowledge'. Compose secrets are mounted as files at /run/secrets/<secret_name> and permit granular access control via filesystem permissions.

**Limit or threshold asserted.** Mount path /run/secrets/<secret_name>

- Source: Docker Docs  -  Use secrets in Compose
- URL: <https://docs.docker.com/compose/how-tos/use-secrets/>
- Second source: <https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both quotes and the /run/secrets/<secret_name> mount path are confirmed.

### A10.21  [yes]

**Claim.** OWASP guidance: secrets 'should never be hardcoded using docker ENV or docker ARG commands, as these can easily leak with the container definitions', and 'environment variables are generally accessible to all processes and may be included in logs or system dumps. Using environment variables is therefore not recommended unless the other methods are not possible.' It also recommends secrets detection at developer level via IDE or pre-commit hook, and treating CI/CD tooling as a production environment.

**Limit or threshold asserted.** No specific rotation frequency given; rotation lifetimes 'from minutes to years' depending on secret type

- Source: OWASP Secrets Management Cheat Sheet
- URL: <https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All five elements verified verbatim, including the correctly-stated absence of any prescribed rotation frequency.

### A10.22  [yes]

**Claim.** Terraform stores state as plaintext including secret values: 'Terraform stores your state in a plaintext file, which includes any secret values you defined in your configuration', and values marked sensitive are still stored 'in both state and plan files, and anyone who can access those files can access your sensitive values'. This is the documented anti-pattern at the intersection of declarative IaC and secrets.

- Source: HashiCorp Developer  -  Manage sensitive data in state
- URL: <https://developer.hashicorp.com/terraform/language/state/sensitive-data>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both quotes verbatim.

### A10.23  [yes]

**Claim.** Terraform state locking is automatic and invisible ('You do not see any message that it happens'), 'If state locking fails, Terraform does not continue', and force-unlock carries an explicit caution  -  'Be very careful with this command'  -  because unlocking while another holds the lock 'could cause multiple writers'.

- Source: HashiCorp Developer  -  State locking
- URL: <https://developer.hashicorp.com/terraform/language/state/locking>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All three quotes confirmed on the page.

### A10.24  [yes]

**Claim.** OpenTofu is actively maintained: v1.12.6 and v1.11.14 released 19 August 2026, v1.13.0-beta1 on 27 August 2026.

**Limit or threshold asserted.** Latest stable v1.12.6, 19 Aug 2026

- Source: opentofu/opentofu GitHub releases
- URL: <https://github.com/opentofu/opentofu/releases>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Worth carrying forward: v1.11.14 is flagged as the final planned patch of the v1.11 series, and both 19 Aug releases carry security advisories - relevant if any pinned version is chosen.

### A10.25  [yes]

**Claim.** Ansible is actively maintained: ansible-core 2.21 released 31 May 2026 with EOL 30 Nov 2027; three versions (2.19, 2.20, 2.21) currently supported.

**Limit or threshold asserted.** ansible-core 2.21 released 31 May 2026, EOL 30 Nov 2027; 2.19 EOL 30 Nov 2026. Attempted direct fetch of docs.ansible.com returned HTTP 429

- Source: endoflife.date / Ansible community documentation (release and maintenance)
- URL: <https://endoflife.date/ansible-core>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: None - endoflife.date now loads fine (the earlier 429 was transient) and confirms every figure. Caveat for the write-up: endoflife.date is a community aggregator, not an upstream source; if this drives a support-window decision, corroborate against the Ansible release-and-maintenance page.

### A10.26  [partially]

**Claim.** SOPS, the standard tool for encrypting secrets in-repo alongside declarative configuration, is a CNCF Sandbox project (accepted 17 May 2023) and is actively released: v3.13.3 on 23 July 2026.

**Limit or threshold asserted.** v3.13.3, 23 July 2026

- Source: getsops/sops GitHub releases; CNCF project page
- URL: <https://github.com/getsops/sops/releases>
- Second source: <https://www.cncf.io/projects/sops/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The release fact is exact: v3.13.3, 23 July 2026, actively maintained. CNCF Sandbox status is confirmed only to the YEAR 2023 by the repo README - the precise date '17 May 2023' is supported by neither the cited releases URL nor the README; drop it or cite the CNCF TOC record. Separately, 'the standard tool' is an editorial elevation of a named product and should be rewritten as a capability (see neutrality findings).

### A10.27  [yes]

**Claim.** The OpenGitOps principles are: Declarative; Versioned and Immutable ('Desired state is stored in a way that enforces immutability, versioning and retains a complete version history'); Pulled Automatically; Continuously Reconciled ('Software agents continuously observe actual system state and attempt to apply the desired state').

**Limit or threshold asserted.** OpenGitOps v1.0

- Source: OpenGitOps (CNCF)  -  PRINCIPLES.md
- URL: <https://github.com/open-gitops/documents/blob/main/PRINCIPLES.md>
- Second source: <https://opengitops.dev/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Principles and both quoted definitions confirmed. Minor: the version cannot be read off this file - the heading renders as a literal 'GitOps Principles {{version}}' placeholder on main. If 'v1.0' is asserted, cite the tagged v1.0.0 release of open-gitops/documents rather than the main-branch file.

### A10.28  [yes]

**Claim.** Kief Morris's automation fear spiral: 'I was afraid to turn my back on my automation tools, because I lacked confidence in what they would do. I lacked confidence in my automation because my servers were not consistent. My servers were not consistent because I wasn't running automation frequently and consistently.' The prescribed escape is to run the automation unattended on a small scope 'at least hourly', expanding as confidence builds with monitoring and CI.

**Limit or threshold asserted.** Prescribed cadence: unattended, at least hourly, on a small subset

- Source: Kief Morris  -  The Automation Fear Spiral (8 Mar 2015); Thoughtworks reprint (19 Jan 2016)
- URL: <https://infrastructure-as-code.com/posts/automation-fear-spiral.html>
- Second source: <https://www.thoughtworks.com/insights/blog/infrastructure-code-automation-fear-spiral>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None on substance. The source wording is 'at least once an hour' rather than 'at least hourly' - quote it as written if presenting it as a quotation.

### A10.29  [yes]

**Claim.** Google SRE documents that automation can amplify failure: 'doing automation thoughtlessly can create as many problems as it solves'. In the diskerase incident, automation with an empty set treated as 'everything' wiped disks across a CDN in minutes and cost 'the better part of two days reinstalling', after which the team added sanity checks including rate limiting. It also notes operators are 'progressively more relieved of useful direct contact with the system as the automation covers more and more daily activities'.

- Source: Google SRE Book, Ch. 7  -  The Evolution of Automation at Google
- URL: <https://sre.google/sre-book/automation-at-google/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Substance confirmed: empty set interpreted as 'everything', CDN machines wiped, ~two days reinstalling plus weeks of follow-on work, remediation via rate limiting, sanity checks and making the workflow idempotent. One wording caution: 'in minutes' is not a quoted phrase from the chapter - drop it or replace with the chapter's own account. Also note the remediation list includes idempotency, which is directly relevant to the requirements and worth adding.

### A10.30  [could-not-check]

**Claim.** Lisanne Bainbridge's 'Ironies of Automation' (Automatica, 1983) establishes that automating most of a task leaves operators responsible for the residue, removes the practice that maintains their skill, and therefore requires MORE training rather than less for the rare critical intervention; automation intended to remove human error introduces designer error instead.

**Limit or threshold asserted.** ~1800 citations as of Nov 2016. Abstract page only; full text not retrieved

- Source: Bainbridge, 'Ironies of automation', Automatica 19(6), 1983
- URL: <https://www.sciencedirect.com/science/article/abs/pii/0005109883900468>
- Second source: <https://blog.acolyer.org/2020/01/08/ironies-of-automation/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The cited URL is NOT reachable (403, not merely abstract-only as the researcher recorded), so nothing on it verifies the claim and the '~1800 citations as of Nov 2016' figure is unsupported. The substantive characterisation matches the paper as generally understood (Automatica Vol. 19, No. 6, pp. 775-779, 1983), but if this argument is load-bearing, replace the citation with an openly reachable copy or a citing secondary source, and delete the citation-count figure.

### A10.31  [yes]

**Claim.** Google SRE defines toil as work that is 'manual, repetitive, automatable, tactical, devoid of enduring value, and that scales linearly as a service grows', and caps it: 'Our SRE organization has an advertised goal of keeping operational work (i.e., toil) below 50% of each SRE's time.'

**Limit or threshold asserted.** 50% toil cap

- Source: Google SRE Book, Ch. 5  -  Eliminating Toil
- URL: <https://sre.google/sre-book/eliminating-toil/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both the six-attribute definition and the 50% cap are confirmed, along with the paired commitment that at least 50% goes to engineering project work.

### A10.32  [yes]

**Claim.** Google SRE on simplicity: 'The price of reliability is the pursuit of the utmost simplicity' (Hoare); 'software simplicity is a prerequisite to reliability'; and the explicit framing of eliminating accidental complexity while managing essential complexity.

- Source: Google SRE Book, Ch. 6  -  Simplicity
- URL: <https://sre.google/sre-book/simplicity/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both quotes confirmed, and the chapter does frame essential vs accidental complexity (drawing on Brooks) with the instruction to resist introducing accidental complexity and to eliminate it where it exists.

### A10.33  [yes]

**Claim.** DORA defines deployment automation as deploying 'with the push of a button', states that 'the number of manual steps increases the deployment time as well as the opportunity for error', that deployment processes should be idempotent and order-independent, that teams should be able to 'deploy any version of the artifact to any environment on demand in a fully automated fashion', and recommends keeping deployment scripts simple and storing configuration in version control.

- Source: DORA  -  Deployment automation capability
- URL: <https://dora.dev/capabilities/deployment-automation/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Every element verified. Note the version-control point is stated as keeping environment-specific configuration separate from packages and storing scripts and configuration in version control - quote it that way if precision matters.

### A10.34  [yes]

**Claim.** Microsoft's Well-Architected guidance (OE:05, updated 11 June 2026) defines immutable infrastructure as infrastructure 'intended to be replaced with new infrastructure that runs the new configuration with each deployment. It must not be changed in place', and states 'If your workload is business critical, it's best to use immutable infrastructure', while conversely mutable infrastructure 'can be a better choice if your safe deployment practices dictate that rolling forward with deployments when mitigable deployment issues arise is the preferred option'.

- Source: Microsoft Azure Well-Architected Framework  -  Architecture strategies for using infrastructure as code (OE:05)
- URL: <https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/infrastructure-as-code-design>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. OE:05 is the correct recommendation code and the page metadata shows updated_at 2026-06-11, matching the stated date.

### A10.35  [yes]

**Claim.** The same Well-Architected guidance lists explicit IaC costs  -  'Increased specialization' with a learning curve, 'Increased maintenance effort', 'Increased time for configuration changes', and 'Increased complexity of modularization: Using more modules and parameterization increases the time it takes to debug and document the system and adds a layer of abstraction. Balance the use of modularization to reduce complexity and avoid over-engineering.' It also directs 'avoid using the latest flag... Be intentional about calling the latest known good version', 'Scan your IaC repos for keys and secrets that are exposed', 'Document manual steps... Ensure that these steps are minimized as much as possible and clearly documented', and 'Test routine and non-routine activities. Test deployments, configuration updates, and recovery processes, including deployment-rollback processes.'

- Source: Microsoft Azure Well-Architected Framework  -  OE:05
- URL: <https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/infrastructure-as-code-design>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All four Considerations headings and all four directives verified verbatim. Note the 'latest' guidance is scoped to tool and API versions in deployment code (not container image tags) - do not over-read it as an image-pinning citation; use SP 800-190 sec 4.2.2 for that.

### A10.36  [yes]

**Claim.** A reproducible build is defined as: 'A build is reproducible if given the same source code, build environment and build instructions, any party can recreate bit-by-bit identical copies of all specified artifacts', verified by bit-by-bit comparison using cryptographic hashes.

**Limit or threshold asserted.** Bit-by-bit identity

- Source: Reproducible Builds  -  Definition
- URL: <https://reproducible-builds.org/docs/definition/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Definition verbatim. Minor precision point: bit-by-bit comparison via cryptographically secure hash functions appears in the page's Explanations section as verification method, not inside the definition sentence - present it as such rather than folding it into the definition.

### A10.37  [yes]

**Claim.** Under current HIPAA, 45 CFR 164.308(a)(7)(ii)(D) 'Testing and revision procedures'  -  'Implement procedures for periodic testing and revision of contingency plans'  -  is Addressable, not Required, whereas (A) Data backup plan, (B) Disaster recovery plan and (C) Emergency mode operation plan are Required.

**Limit or threshold asserted.** (ii)(D) and (ii)(E) Addressable; (ii)(A)-(C) Required. eCFR direct access was blocked; Cornell LII used

- Source: 45 CFR 164.308 (Cornell LII)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.308>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. The Required/Addressable split across (A)-(E) is exactly as claimed. Worth stating explicitly in the write-up that 'Addressable' is not optional under 164.306(d)(3) - it requires assessment and either implementation or a documented equivalent/justification - otherwise the finding invites the wrong conclusion about rehearsal cadence.

### A10.38  [yes]

**Claim.** The HIPAA Security Rule NPRM (published 6 Jan 2025) proposes to 'Remove the distinction between required and addressable implementation specifications and make all implementation specifications required'; to 'Establish written procedures to restore the loss of certain relevant electronic information systems and data within 72 hours'; to 'Perform an analysis of the relative criticality of their relevant electronic information systems and technology assets to determine the priority for restoration'; and to require a technology asset inventory and network map revised 'at least once every 12 months'.

**Limit or threshold asserted.** 72 hours restoration; asset inventory and network map at least every 12 months

- Source: HHS OCR  -  HIPAA Security Rule NPRM Fact Sheet
- URL: <https://www.hhs.gov/hipaa/for-professionals/security/hipaa-security-rule-nprm/factsheet/index.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All four proposals verified. Two precision points the claim drops: the removal of the addressable distinction is 'with specific, limited exceptions', and the inventory/network-map revision is 'on an ongoing basis, but at least once every 12 months and in response to a change'. Also note the fact sheet dates the NPRM issuance to 27 December 2024 with Federal Register publication 6 January 2025 - the claim's date is the FR date and is correct, but say which.

### A10.39  [partially]

**Claim.** As of mid-2026 the HIPAA Security Rule update remains a proposed rule; the OMB Unified Agenda shows the final action pushed back to July 2027 (RIN 0945-AA22), and stated government timeframes are not legally binding.

**Limit or threshold asserted.** Final action due July 2027 per OMB Unified Agenda; original target was May 2026. Secondary source; reginfo.gov not directly fetched

- Source: HIPAA Journal  -  HIPAA Security Rule Update Postponed (8 July 2026)
- URL: <https://www.hipaajournal.com/hipaa-security-rule-update-postponed/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The secondary source says exactly what the claim says, including RIN 0945-AA22 and the non-binding caveat. But it remains a single commercial secondary source and reginfo.gov was never fetched, so the July 2027 date is unconfirmed at first hand - keep the researcher's 'partial' confidence and label the date as 'per a secondary report of the Unified Agenda'. Do not let a compliance-relevant date enter the document as settled fact on this basis.

### A10.40  [yes]

**Claim.** NIST SP 800-34 Rev. 1 prescribes exercise type by impact level: for low-impact systems a tabletop exercise; for moderate-impact systems 'a functional exercise at an organization-defined frequency should be conducted' whose 'procedures should be developed to include an element of system recovery from backup media'; for high-impact systems a full-scale functional exercise including 'a full recovery and reconstitution of the information system to a known state'. Its sample policy states 'The plan recovery capabilities and personnel shall be tested annually'. NIST SP 800-53 control CP-4 requires testing at an organization-defined frequency and review of results with corrective actions.

**Limit or threshold asserted.** Moderate impact -> functional exercise including recovery from backup media; sample policy says annually. Published May 2010, updated 11 Nov 2010; CSRC shows no Rev. 2 in development

- Source: NIST SP 800-34 Rev. 1, Contingency Planning Guide for Federal Information Systems, 3.5.4 and Appendix control CP-4
- URL: <https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-34r1.pdf>
- Second source: <https://csrc.nist.gov/pubs/sp/800/34/r1/upd1/final>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All elements verified by PDF text extraction, including the CP-4 reference which does appear in 800-34r1 itself. Two notes for accuracy: (a) the errata table shows 5/21/2010 changes that deliberately REMOVED 'For moderate impact systems, a yearly functional test is required' and the high-impact equivalent as 'not a requirement' - so do not reintroduce an annual functional-exercise mandate; the annual language survives only in the sample policy; (b) the errata table's latest entry is 11/11/2010, consistent with the claimed update date. The CP-4 sentence is a characterization by 800-34, not the control text - cite SP 800-53 directly if the control wording matters.

### A10.41  [yes]

**Claim.** .NET/ASP.NET Core options validation is lazy by default  -  'Options validation runs the first time a TOption instance is created, which is when the first access to IOptionsSnapshot<TOptions>.Value occurs in a request pipeline or when IOptionsMonitor<TOptions>.Get(string) is called'  -  and ValidateOnStart() moves it to application startup, giving in-process fail-fast on bad configuration.

**Limit or threshold asserted.** Pattern: AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()

- Source: Microsoft Learn  -  Options pattern in ASP.NET Core (updated 22 July 2026)
- URL: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. The quote is verbatim and the page shows exactly the claimed pattern: AddOptions<KeyOptions>().Bind(...).ValidateDataAnnotations().ValidateOnStart(). Add the sentence the claim omits - 'Each time options are reloaded, validation runs again' - since it bears on whether a rotated secret can be picked up without restart (requirement on rotation).

### A10.42  [partially]

**Claim.** GitOps tooling for Docker Compose on a single VM is thin: the mature reconcilers (Argo CD, Flux) are Kubernetes-targeted, and the compose-specific options are small community projects such as ComposeFlux, a polling reconciler that runs docker compose up on change detection.

- Source: Veerendra's Blog  -  GitOps for Homeservers (Part 3): ComposeFlux; corroborated by 2026 GitOps tool roundups
- URL: <https://veerendra2.github.io/gitops-for-homeservers-part3/>
- Second source: <https://spacelift.io/blog/gitops-tools>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The polling-reconciler description and the Flux-is-for-Kubernetes implication are supported. Argo CD is not mentioned on the page at all, so that half is unsourced. The page is a personal blog written by ComposeFlux's own author - it is advocacy, not a neutral survey, and cannot establish the negative claim that no mature compose reconcilers exist. Restate as: pull-based reconciliation for single-host compose is served mainly by small community projects; cite this page only as one example.

### A10.43  [partially]

**Claim.** Podman Quadlet (merged into Podman since 4.4; the older podman generate systemd is deprecated) is a maintained alternative that expresses containers as native systemd units, and is argued to be the more natural fit for single-server production where system integration matters.

**Limit or threshold asserted.** Quadlet in Podman >=4.4

- Source: Community analyses of Quadlet vs Compose (2026)
- URL: <https://matduggan.com/replace-compose-with-quadlet/>
- Second source: <https://ebourgess.dev/posts/podman-quadlet-production-containers/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The two factual elements - Quadlet in Podman >= 4.4, and podman generate systemd deprecated in its favour - are supported, though only by a personal blog; source them to Podman's own documentation before relying on them. The 'more natural fit for single-server production' is one author's opinion, and the same page hedges it heavily with rootless-setup complexity. Present as one opinionated data point, not as a finding, and see the neutrality note about recommending a named product.

### A10.44  [yes]

**Claim.** The Fail Fast pattern (Jim Shore, IEEE Software, 2004)  -  fail immediately and visibly rather than continuing with invalid state  -  is the named principle underlying these recommendations.

**Limit or threshold asserted.** n/a  -  martinfowler.com returned HTTP 503 on repeated fetch attempts; cited from recollection and NOT relied upon. The concrete fail-fast mechanisms recommended here are each independently verified above (Compose ${VAR:?}, env_file required, ValidateOnStart)

- Source: Shore, 'Fail Fast', IEEE Software Sept/Oct 2004
- URL: <https://www.martinfowler.com/ieeeSoftware/failFast.pdf>
- Accessed: 2026-08-31
- Confidence: unverified
- Verifier note: The confidence label is wrong in the conservative direction. The PDF fetched successfully (the earlier 503 was transient) and confirms author, venue, year and definition. Upgrade to verified and delete the 'cited from recollection and NOT relied upon' caveat. Bonus: the article's worked example is a missing configuration property that returns a default instead of throwing - a direct match for the Compose empty-string trap in claim 1, so it can be cited as the governing principle rather than held at arm's length.

---

## Area: capacity-model

Verification verdict for this area: **material-errors** (30 claims checked)

### A11.1  [yes]

**Claim.** Microsoft.Data.SqlClient creates a separate connection pool per unique connection string, matched exactly; keywords supplied in a different order are pooled separately. Connections are pooled per process, per application domain, per connection string.

**Limit or threshold asserted.** 1 pool per distinct connection string per process/AppDomain

- Source: Microsoft Learn  -  SQL Server connection pooling (ADO.NET)
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Verified verbatim, but the stated threshold '1 pool per distinct connection string per process/AppDomain' is an UNDER-COUNT for capacity purposes. The same page adds three further pool-splitting axes the claim omits: per Windows identity under integrated security; per SqlCredential instance ('Different instances of SqlCredential will use different connection pools, even if the user ID and password are the same'); and by transaction enlistment ('Connections are also pooled based on whether they are enlisted in a transaction'). A capacity model that multiplies pools only by connection string can undercount real pool count.

### A11.2  [yes]

**Claim.** The default maximum pool size is 100. When the maximum is reached and no usable connection is available the request is queued; the pooler tries to reclaim connections until the timeout is reached, default 15 seconds, then an exception is thrown.

**Limit or threshold asserted.** Max Pool Size default 100; connection timeout default 15 s

- Source: Microsoft Learn  -  SQL Server connection pooling (ADO.NET), 'Add connections'
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: No correction. Both numbers and the queue/reclaim/throw sequence are stated verbatim on the page.

### A11.3  [yes]

**Claim.** If Connection Lifetime / LoadBalanceTimeout is not set (default 0), the connection pooler removes a connection from the pool after it has been idle for approximately 4-8 minutes, in a random two-pass fashion. If MinPoolSize is zero or unspecified, pooled connections are closed after a period of inactivity.

**Limit or threshold asserted.** idle reap ~ 4-8 minutes

- Source: Microsoft Learn  -  SQL Server connection pooling (ADO.NET), 'Remove connections'
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: No correction. Also verified: 'If MinPoolSize is either not specified in the connection string or is specified as zero, the connections in the pool will be closed after a period of inactivity.'

### A11.4  [yes]

**Claim.** After a timeout or login error, subsequent connection attempts fail for the next 5 seconds (the 'blocking period'); each subsequent failure doubles the blocking period up to a maximum of 1 minute.

**Limit or threshold asserted.** 5 s doubling to max 60 s

- Source: Microsoft Learn  -  SQL Server connection pooling (ADO.NET)
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Verified verbatim, but the claim omits an exception stated in the same callout: 'The "blocking period" mechanism doesn't apply to Azure SQL Server by default. This behavior can be changed by modifying the PoolBlockingPeriod property.' If the capacity model ever targets a managed SQL endpoint rather than a self-hosted instance, the 5s/60s backoff does not apply by default.

### A11.5  [yes]

**Claim.** Microsoft documents 'Pool fragmentation due to many databases' as a named problem: connecting to a separate database per user or group produces a separate pool of connections to each database, which increases the number of connections to the server. The documented mitigation is to connect to one database and issue USE to switch  -  which is incompatible with a per-tenant connection-string design such as ABP's.

- Source: Microsoft Learn  -  SQL Server connection pooling (ADO.NET), 'Pool fragmentation'
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Named section and mitigation verified verbatim. The trailing clause ('incompatible with a per-tenant connection-string design such as ABP's') is the researcher's inference, not on the page - correctly framed as such, but note it is an inference the page neither states nor contradicts.

### A11.6  [yes]

**Claim.** SQL Server documented maxima: 32,767 user connections; 32,767 databases per instance; maximum database size 524,272 terabytes; default TDS network packet size 4 KB.

**Limit or threshold asserted.** 32,767 user connections; 32,767 databases per instance

- Source: Microsoft Learn  -  Maximum capacity specifications for SQL Server
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/maximum-capacity-specifications-for-sql-server>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All four values verified verbatim. Note the page applies to SQL Server 2016 (13.x) and later; it is not a statement about managed/cloud SQL offerings, whose connection ceilings are far lower and tier-dependent.

### A11.7  [yes]

**Claim.** The default max worker threads value (setting 0) for a 64-bit machine with 4 or fewer logical CPUs is 512. A worker thread is assigned only to active requests and is released once the request is serviced, even if the connection remains open. When all worker threads are active with long-running queries, SQL Server might appear unresponsive until a worker completes.

**Limit or threshold asserted.** 512 workers at <=4 logical CPUs, 64-bit

- Source: Microsoft Learn  -  Server configuration: max worker threads
- URL: <https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-the-max-worker-threads-server-configuration-option>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Verified, with one qualification the claim states unconditionally: footnote 2 on the same table reads 'Starting with SQL Server 2017 (14.x), the Default Max Workers value is divided by 2 for machines with less than 2 GB of memory.' On a container or VM provisioned under 2 GB the default is 256, not 512 - relevant here because a capacity model for small tenant hosts may sit below that line.

### A11.8  [yes]

**Claim.** Microsoft.Data.SqlClient publishes 16 event counters on the EventSource 'Microsoft.Data.SqlClient.EventSource', including hard-connects, soft-connects, active-hard-connections, number-of-active-connections, number-of-free-connections, number-of-active-connection-pools, number-of-active-connection-pool-groups, and number-of-reclaimed-connections. They can be read out-of-process with dotnet-counters.

**Limit or threshold asserted.** 16 counters; requires Microsoft.Data.SqlClient 3.0.0+ and .NET Core 3.1+

- Source: Microsoft Learn  -  Event counters in SqlClient
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/event-counters>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Count (16), EventSource name, version floors, dotnet-counters usage, and all eight named counters verified exactly. One naming nit for anyone scripting this: the pooled-hit counter is 'soft-connects' (rate) and 'active-soft-connects' (level) - there is no 'active-soft-connections'.

### A11.9  [yes]

**Claim.** dotnet-counters attaches to an already-running .NET process by process id or name with no code change and no restart, and can write a csv or json time series via 'collect --format csv -o'. On Linux it requires the tool and target to share the same TMPDIR and to run as the same user or root; for a process in a container that is not in the current process namespace, the --diagnostic-port option or running the tool inside the container is required.

- Source: Microsoft Learn  -  dotnet-counters diagnostic tool
- URL: <https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All operational constraints verified. Two additions worth carrying into the requirement: the page also warns of bitness matching ('A 32 bit processes cannot access modules of a 64 bit process'), and states 'Counters can be read from applications running .NET 5 or later' - so the attach capability is not available against a .NET Core 3.1 target with a current tool build even though SqlClient counters themselves date from 3.1.

### A11.10  [partially]

**Claim.** Well-known .NET counter names: System.Runtime publishes threadpool-queue-length, threadpool-thread-count, gc-heap-size, working-set, cpu-usage; Microsoft.AspNetCore.Hosting publishes current-requests, failed-requests, requests-per-second, total-requests; Microsoft-AspNetCore-Server-Kestrel publishes request-queue-length, connection-queue-length, current-connections, tls-handshakes-per-second. On .NET 9+ System.Runtime is exposed as a Meter with names such as dotnet.thread_pool.queue.length and dotnet.process.memory.working_set.

- Source: Microsoft Learn  -  Well-known EventCounters in .NET; dotnet-counters
- URL: <https://learn.microsoft.com/en-us/dotnet/core/diagnostics/available-counters>
- Second source: <https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All 13 EventCounter names verified exactly on the cited page. The final sentence is NOT supported by this URL: available-counters.md is the legacy EventCounters reference (ms.date 2020-12-17) and contains no Meter names, no mention of .NET 9, and no 'dotnet.thread_pool.queue.length' or 'dotnet.process.memory.working_set'. It only links out to 'the well-known metrics reference'. The Meter claim is true but must be cited to <https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime> (or to the dotnet-counters page, which notes 'If the app uses .NET version 8 or lower, the System.Runtime Meter doesn't exist in those versions and dotnet-counters will fall back to display the older System.Runtime EventCounters').

### A11.11  [partially]

**Claim.** In Hangfire.SqlServer, QueuePollInterval defaults to TimeSpan.Zero in SqlServerStorageOptions' constructor, SlidingInvisibilityTimeout defaults to 5 minutes and CommandBatchMaxTimeout to 5 minutes; the documented recommended configuration is exactly QueuePollInterval = TimeSpan.Zero with SlidingInvisibilityTimeout set, DisableGlobalLocks = true and UseRecommendedIsolationLevel = true. TimeSpan.Zero is therefore the vendor default and recommendation, not an aggressive override.

**Limit or threshold asserted.** QueuePollInterval default TimeSpan.Zero; SlidingInvisibilityTimeout default 5 min

- Source: Hangfire documentation  -  Using SQL Server; Hangfire source SqlServerStorageOptions.cs
- URL: <https://docs.hangfire.io/en/latest/configuration/using-sql-server.html>
- Second source: <https://raw.githubusercontent.com/HangfireIO/Hangfire/master/src/Hangfire.SqlServer/SqlServerStorageOptions.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Three problems. (1) The CITED URL contradicts the 'vendor default' framing - it explicitly calls these recommended-not-default. The claim is nevertheless TRUE for Hangfire 1.8.x, but only per source I checked separately: SqlServerStorageOptions.cs at tags v1.8.14 and v1.8.20 sets QueuePollInterval = TimeSpan.Zero, SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5), CommandBatchMaxTimeout = TimeSpan.FromMinutes(5), UseRecommendedIsolationLevel = true. At v1.7.34 the same constructor sets QueuePollInterval = TimeSpan.FromSeconds(15) and SlidingInvisibilityTimeout = null. So the answer is version-dependent and the claim must state 'default in 1.8.x, 15 s in 1.7.x'. (2) DisableGlobalLocks = true is NOT a default - the 1.8.x constructor sets DisableGlobalLocks = false. The claim lumps it into the same sentence as the defaults. (3) Correct citation for the default is the source file <https://raw.githubusercontent.com/HangfireIO/Hangfire/v1.8.20/src/Hangfire.SqlServer/SqlServerStorageOptions.cs>, not the docs page.

### A11.12  [partially]

**Claim.** With SlidingInvisibilityTimeout set and QueuePollInterval = TimeSpan.Zero, Hangfire.SqlServer's dequeue loop uses a client-side sleep of 200 ms between attempts (DefaultPollingDelayMs = 200, clamped between MinPollingDelayMs = 100 and PollingQuantumMs = 1000) via WaitHandle.WaitAny, issuing an 'update top (1) ... output inserted.* from JobQueue with (forceseek, readpast, updlock, rowlock)' per attempt. There is no WAITFOR in the T-SQL and the connection is released before waiting, so a poller does not hold a pooled connection while idle.

**Limit or threshold asserted.** 200 ms polling delay -> ~5 statements/second per worker

- Source: Hangfire source  -  SqlServerJobQueue.cs (master)
- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/master/src/Hangfire.SqlServer/SqlServerJobQueue.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The constants, the clamp (Math.Min(Math.Max(200, 100), 1000) = 200 ms), the hints, the absence of WAITFOR, and connection-release-before-wait are all correct: FetchJob() wraps the command in _storage.UseConnection(...) which returns the connection before WaitHandle.WaitAny runs outside it. Two corrections. (1) The OUTPUT clause is not 'inserted.*' - it is an explicit four-column list, OUTPUT INSERTED.Id, INSERTED.JobId, INSERTED.Queue, INSERTED.FetchedAt. (2) MATERIAL OMISSION: the loop is gated by a process-wide SemaphoreSlim(initialCount: 1) keyed on Tuple<SqlServerStorage, queuesString> ('semaphore = Semaphores.GetOrAdd(resource, CreateSemaphoreFunc); semaphore.Wait(cancellationToken);'), active unless DisableFetchSemaphores is set - and DisableFetchSemaphores is NOT set in the SqlServerStorageOptions constructor, so it defaults to false. Only one worker per storage+queue-set is inside the 200 ms polling loop at a time. Omitting this is what makes claim 13 wrong.

### A11.13  [no]

**Claim.** DERIVED, not sourced: the idle Hangfire query floor on the host database is approximately 5 x (worker count) statements per second  -  20/s and about 1.73 million statements per day at 4 workers, 40/s and 3.46 million per day at 8  -  and this floor does not increase with office count.

**Limit or threshold asserted.** 5 x workers per second; worker count is UNKNOWN

- Source: My arithmetic on the Hangfire 200 ms polling delay above
- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/master/src/Hangfire.SqlServer/SqlServerJobQueue.cs>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: WRONG BY A FACTOR OF THE WORKER COUNT. The polling loop in DequeueUsingSlidingInvisibilityTimeout is serialized by a per-(storage, queue-set) SemaphoreSlim with initialCount 1, enabled by default (DisableFetchSemaphores is never set in the constructor, so false). At idle, each worker performs ONE unconditional FetchJob and then blocks on the semaphore; exactly one worker occupies the 200 ms loop. The steady-state idle floor is therefore ~5 statements/second PER STORAGE PER QUEUE-SET - about 432,000 statements/day - not 5 x workers. The claimed 20/s (1.73M/day) at 4 workers and 40/s (3.46M/day) at 8 overstate the floor by 4x and 8x. Two corollaries the claim also misses: the floor scales with the number of DISTINCT QUEUE SETS and the number of SqlServerStorage instances (so a per-tenant storage instance WOULD make it scale with office count, contradicting the claim's closing assertion), and if QueuePollInterval were ever set to >= 1 s the long-polling path is disabled entirely (useLongPolling = configuredPollInterval < LongPollingThreshold, 1 second).

### A11.14  [yes]

**Claim.** ABP auditing defaults: IsEnabled true, IsEnabledForAnonymousUsers true, IsEnabledForGetRequests FALSE, AlwaysLogOnException true, HideErrors true. Controller actions and application service method calls are audited by default. Entity changes are NOT recorded unless explicitly configured  -  'the audit log system doesn't save any change for the entities unless you explicitly configure it'  -  via AddAllEntities() or EntityHistorySelectors.

**Limit or threshold asserted.** IsEnabledForGetRequests default false

- Source: ABP documentation  -  Audit Logging
- URL: <https://abp.io/docs/latest/framework/infrastructure/audit-logging>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All five defaults, the default-audited surfaces, the entity-change sentence, and both configuration mechanisms verified on the page. Note for the capacity model: IsEnabledForGetRequests = false means any measured rows-per-transaction baseline reflects only non-GET traffic, so read-heavy workloads add no audit rows - do not extrapolate the per-appointment figure across total request volume.

### A11.15  [partially]

**Claim.** The ABP audit log schema comprises four tables  -  audit logs, audit log actions, entity changes and entity property changes  -  with nonclustered indexes on (TenantId, ExecutionTime) and (TenantId, UserId, ExecutionTime) for audit logs, (AuditLogId) and (TenantId, ServiceName, MethodName, ExecutionTime) for actions, (AuditLogId) and (TenantId, EntityTypeFullName, EntityId) for entity changes, and (EntityChangeId) for property changes. Index rows are additional storage per audit row and must be counted in any projection.

**Limit or threshold asserted.** 1-2 nonclustered indexes per audit table

- Source: ABP source  -  AbpAuditLoggingDbContextModelBuilderExtensions.cs
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/modules/audit-logging/src/Volo.Abp.AuditLogging.EntityFrameworkCore/Volo/Abp/AuditLogging/EntityFrameworkCore/AbpAuditLoggingDbContextModelBuilderExtensions.cs>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: All seven indexes verified exactly as stated - that half is correct. But the module configures FIVE tables, not four: AbpAuditLogs, AbpAuditLogActions, AbpEntityChanges, AbpEntityPropertyChanges, and AbpAuditLogExcelFiles. The fifth is not written per business transaction, so it does not change the rows-per-appointment arithmetic, but a retention/purge design or a per-database storage inventory that enumerates 'the four audit tables' will silently miss it. Restate as 'four transactional audit tables plus AbpAuditLogExcelFiles'.

### A11.16  [yes]

**Claim.** ABP column length constants that bound audit row size: AuditLogConsts MaxBrowserInfoLength 512, MaxUrlLength 256, MaxUserNameLength 256, MaxClientNameLength 128, MaxApplicationNameLength 96; AuditLogActionConsts MaxServiceNameLength 256, MaxMethodNameLength 128, MaxParametersLength 2000; EntityPropertyChangeConsts MaxNewValueLength 512, MaxOriginalValueLength 512, MaxPropertyNameLength 128, MaxPropertyTypeFullNameLength 512. SOURCES DISAGREE: the model-builder summary reported larger figures (Url 4096, NewValue 4096) than these constants files; the constants are the authoritative defaults and the discrepancy is why the byte-per-row figure must be measured rather than computed.

**Limit or threshold asserted.** nvarchar, 2 bytes per character; worst case ~3.3 KB per entity-property-change row

- Source: ABP source  -  AuditLogConsts.cs, AuditLogActionConsts.cs, EntityPropertyChangeConsts.cs
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/modules/audit-logging/src/Volo.Abp.AuditLogging.Domain.Shared/Volo/Abp/AuditLogging/EntityPropertyChangeConsts.cs>
- Second source: <https://raw.githubusercontent.com/abpframework/abp/dev/modules/audit-logging/src/Volo.Abp.AuditLogging.Domain.Shared/Volo/Abp/AuditLogging/AuditLogConsts.cs>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: All twelve constants are exactly right and the resolution of the source conflict is correct: the model builder calls HasMaxLength(EntityPropertyChangeConsts.MaxNewValueLength) etc., i.e. it consumes these very constants, so there is no independent 4096 figure anywhere - the earlier 'Url 4096 / NewValue 4096' reading was an artifact, not a real disagreement. Arithmetic checks: (512+512+128+512) x 2 bytes = 3,328 bytes ~= 3.3 KB. Two caveats: the cited URL alone covers only the four EntityPropertyChange constants (the other eight are in AuditLogConsts.cs and AuditLogActionConsts.cs in the same folder - cite all three); and these are mutable statics ('public static int ... { get; set; }') that an application can raise at startup, so they bound row size only if unmodified.

### A11.17  [yes]

**Claim.** 45 CFR 164.312(b) (Audit controls) requires 'hardware, software, and/or procedural mechanisms that record and examine activity in information systems that contain or use electronic protected health information' and states NO retention period. 45 CFR 164.316(b)(2)(i) requires retaining 'the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later'  -  paragraph (b)(1) being policies, procedures and records of required actions, activities or assessments. The six-year clock therefore attaches to documentation, not on its face to raw application audit-log rows.

**Limit or threshold asserted.** 6 years from creation or last effective date, whichever is later

- Source: Cornell Law School LII  -  45 CFR 164.312 and 45 CFR 164.316
- URL: <https://www.law.cornell.edu/cfr/text/45/164.316>
- Second source: <https://www.law.cornell.edu/cfr/text/45/164.312>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Both citations verified, including the negative claim: 164.312(b) reads in full 'Implement hardware, software, and/or procedural mechanisms that record and examine activity in information systems that contain or use electronic protected health information' and states no retention period. The legal reading - that the 6-year clock attaches to documentation rather than on its face to raw application audit rows - is correctly hedged. Note this is the federal floor only; state law and payer/accreditation contracts commonly impose longer record retention, so the capacity model should treat 6 years as a lower bound input, not a ceiling.

### A11.18  [partially]

**Claim.** UNVERIFIED: whether the January 2025 HIPAA Security Rule NPRM (proposing stronger technical requirements including logging and asset inventory) has been finalised as of August 2026, and whether it introduces an explicit log-retention period. I attempted federalregister.gov and the request was redirected to an unblock interstitial by the outbound proxy; ecfr.gov redirected likewise. The Cornell text carries the source credit '[68 FR 8376, Feb. 20, 2003, as amended at 78 FR 5695, Jan. 25, 2013]', implying no later amendment reflected there.

- Source: Federal Register (blocked by outbound proxy); Cornell LII source credit
- URL: <https://www.federalregister.gov/documents/2025/01/06/2024-30983/hipaa-security-rule-to-strengthen-the-cybersecurity-of-electronic-protected-health-information>
- Accessed: 2026-08-31
- Confidence: unverified
- Verifier note: The unreachability report is accurate and honest - the HTML URL returns 302 to <https://unblock.federalregister.gov/> and does not resolve. But the open question IS answerable and should not have been left unverified: the Federal Register JSON API (<https://www.federalregister.gov/api/v1/documents.json?conditions[regulation_id_number]=0945-AA22>) is NOT blocked and returns exactly one document for RIN 0945-AA22 - the Proposed Rule of 2025-01-06, comments closed 2025-03-07, with no final rule and no withdrawal. CORRECTED CLAIM: as of 2026-08-31 the January 2025 HIPAA Security Rule NPRM has NOT been finalised; 45 CFR 164.312/164.316 remain as amended at 78 FR 5694-5695 (Jan 25, 2013); no new federal log-retention period is in force. Plan to the existing rule, and treat the NPRM only as a forward risk.

### A11.19  [yes]

**Claim.** Redis maxmemory defaults to zero (no limit) on 64-bit systems. Under allkeys-* eviction policies Redis evicts any key, including keys with no expiry; under noeviction it returns errors on writes instead. Eviction is entirely inactive when maxmemory is 0.

**Limit or threshold asserted.** maxmemory default 0 on 64-bit

- Source: Redis documentation  -  Key eviction
- URL: <https://redis.io/docs/latest/develop/reference/eviction/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Default, allkeys-* semantics and noeviction behaviour all verified. Two notes: 'eviction is entirely inactive when maxmemory is 0' is a sound entailment ('Redis will enforce your chosen eviction policy' only 'when the size of your cache exceeds the limit set by maxmemory') but is not stated in those words. And the page now documents an additional policy family, allkeys-lrm / volatile-lrm (Least Recently Modified, Redis 8.6+), which an eviction-risk requirement written against the older five-policy list will not cover.

### A11.20  [yes]

**Claim.** When a container exceeds its Docker --memory hard limit the kernel OOM killer terminates processes; Docker adjusts the daemon's OOM priority but leaves container priority unadjusted, making containers more expendable. --oom-kill-disable should only be used together with an explicit -m/--memory limit.

- Source: Docker documentation  -  Resource constraints
- URL: <https://docs.docker.com/engine/containers/resource_constraints/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All three assertions verified verbatim on the current page.

### A11.21  [partially]

**Claim.** SQL Server on Linux: memory.memorylimitmb (MSSQL_MEMORY_LIMIT_MB) 'controls the amount of physical memory (in MB) available to SQL Server. The default is 80% of the physical memory, to prevent out-of-memory (OOM) conditions.' It limits physical memory to the process; max server memory (MB) separately governs the buffer pool and can never exceed it.

**Limit or threshold asserted.** default 80% of physical memory

- Source: Microsoft Learn  -  Configure SQL Server settings on Linux (mssql-conf)
- URL: <https://learn.microsoft.com/en-us/sql/linux/configure/mssql-conf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The quoted default (80%) and the memorylimitmb-vs-max-server-memory relationship are verified word-for-word. The parenthetical environment-variable name MSSQL_MEMORY_LIMIT_MB does NOT appear anywhere on this page - it only says 'Some of these settings can also be configured with environment variables' and links to a separate article (/sql/linux/configure/environment-variables). Either drop the env-var alias or cite that second page. Also relevant to an OOM-risk requirement: memory.disablememorypressure defaults to false, and setting it true 'inhibits the signals SQL Server uses to limit its physical memory usage to memory.memorylimitmb, which causes the usage to eventually go beyond that limit.'

### A11.22  [yes]

**Claim.** SQL Server 2022 edition scale limits: Developer edition 'includes all the functionality of Enterprise edition, but is licensed for use as a development and test system, not as a production server.' Enterprise/Standard/Web maximum relational database size 524 PB; Express (and Express with Advanced Services) 10 GB maximum relational database size, 1,410 MB maximum buffer pool per instance, and lesser of 1 socket or 4 cores. Standard: 128 GB buffer pool, lesser of 4 sockets or 24 cores.

**Limit or threshold asserted.** Express: 10 GB per database, 1,410 MB buffer pool. Standard: 128 GB buffer pool.

- Source: Microsoft Learn  -  Editions and supported features of SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every figure verified exactly. Two forward-looking notes from the same page that a 2026 exercise should carry: Web edition 'isn't available in SQL Server 2025 (17.x) and later versions', and 'Starting with SQL Server 2025 (17.x), Express edition includes all the functionality that was available in SQL Server Express edition with Advanced Services' - so an Express-vs-Standard sizing decision framed on the 2022 table may not transfer to a 2025 deployment target.

### A11.23  [partially]

**Claim.** SQL Server exposes Batch Requests/sec, SQL Compilations/sec and SQL Re-Compilations/sec in the SQL Statistics object, and Page life expectancy ('the number of seconds a page will stay in the buffer pool without references'), Buffer cache hit ratio, Free list stalls/sec, Page reads/sec and Checkpoint pages/sec in the Buffer Manager object. All are queryable from T-SQL via sys.dm_os_performance_counters, requiring no SQL Server Agent.

- Source: Microsoft Learn  -  SQL Server SQL Statistics object; SQL Server Buffer Manager object
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/performance-monitor/sql-server-sql-statistics-object>
- Second source: <https://learn.microsoft.com/en-us/sql/relational-databases/performance-monitor/sql-server-buffer-manager-object>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Every fact is true, but the cited URL supports only the first half. The SQL Statistics page contains no Buffer Manager counters. I verified the second half separately at <https://learn.microsoft.com/en-us/sql/relational-databases/performance-monitor/sql-server-buffer-manager-object>, which gives 'Page life expectancy | Indicates the number of seconds a page will stay in the buffer pool without references' (the claim's paraphrase 'without references' is exact), plus Buffer cache hit ratio, Free list stalls/sec, Page reads/sec and Checkpoint pages/sec, and the same sys.dm_os_performance_counters example. Cite both URLs. The rider 'requiring no SQL Server Agent' is a correct inference (a DMV SELECT needs only a connection and VIEW SERVER STATE / VIEW SERVER PERFORMANCE STATE on 2022+) but is stated on neither page - and that permission requirement is itself worth naming in the requirement.

### A11.24  [yes]

**Claim.** 'dbid' is a cached-plan attribute  -  'the ID of the database containing the entity the plan refers to. For ad hoc or prepared plans, it is the database ID from which the batch is executed'  -  and sys.dm_exec_plan_attributes returns an is_cache_key bit per attribute. Whether dbid is a cache key on a given instance is verifiable empirically with the documented query; if it is, identical EF Core queries compile and cache once per office database, so plan-cache and compilation pressure scale with tenant count independently of data volume.

**Limit or threshold asserted.** n/a  -  is_cache_key must be read per instance

- Source: Microsoft Learn  -  sys.dm_exec_plan_attributes (Transact-SQL)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-plan-attributes-transact-sql>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The dbid description, the is_cache_key column, and the empirical query are all verified verbatim, and the claim is properly hedged as 'must be read per instance'. Note the page has moved: the canonical URL is now .../system-dynamic-management-objects/sys-dm-exec-plan-attributes-transact-sql (the cited .../system-dynamic-management-views/... path still resolves). Also note the permission has changed on the relevant version: 'Requires VIEW SERVER PERFORMANCE STATE permission on the server' for SQL Server 2022 and later, not VIEW SERVER STATE.

### A11.25  [yes]

**Claim.** Microsoft's multitenancy guidance on dedicated-database-per-tenant names the operational consequences directly: 'If you use multiple servers, file stores, or databases, plan how to initiate and monitor the maintenance operations for each tenant's resources'; 'As your solution scales, it becomes cumbersome to run queries on each database individually and aggregate the results'; and it advises using 'performance testing and capacity planning to determine when to add resources, and plan to scale out before you approach a service or subscription limit.' It also warns that manual schema changes across an estate are an antipattern.

- Source: Microsoft Azure Architecture Center  -  Architectural approaches for storage and data in multitenant solutions
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/storage-data>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All three quotations verified word-for-word and the antipattern is named as claimed. One more sentence from the same page belongs in a database-per-tenant capacity model and is missing: 'It's important to use automated deployment approaches when you provision databases for each tenant. Otherwise, the complexity of manually deploying and managing the databases becomes overwhelming.' Also note the page discusses 'Version dependencies' as a fourth antipattern directly relevant to a 34-database estate.

### A11.26  [yes]

**Claim.** The USE method: 'For every resource, check utilization, saturation, and errors'  -  utilisation being the average time the resource was busy, saturation the degree of queued work it cannot immediately service, errors the count of error events. It is described as resolving roughly 80% of server issues for minimal effort, while only finding bottlenecks and errors.

- Source: Brendan Gregg  -  The USE Method
- URL: <https://www.brendangregg.com/usemethod.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All quotations and both qualifiers verified. The 'minimal effort' paraphrase is slightly loose - the source says '80% of server issues with 5% of the effort'; quote the 5% figure rather than paraphrasing, since it is the part that makes the claim checkable.

### A11.27  [partially]

**Claim.** Load-testing tool currency: NBomber (C#/F#, .NET) latest release v6.6.0 dated 17 August 2026, with v6.5.0 (July 2026) and v6.4.1 (May 2026) preceding it  -  actively maintained and within 12 months. k6 (Grafana, Go binary, JavaScript scripting) is actively maintained but my fetch of its release list returned internally inconsistent version/date pairs and the GitHub API returned HTTP 403 through the proxy, so I could not pin the exact current k6 version; treat k6's currency as probable but confirm the version before adopting.

**Limit or threshold asserted.** NBomber 6.6.0, 2026-08-17

- Source: GitHub releases  -  PragmaticFlow/NBomber; grafana/k6
- URL: <https://github.com/PragmaticFlow/NBomber/releases>
- Second source: <https://github.com/grafana/k6/releases>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The NBomber half is verified against the cited page and independently against the NuGet registration index - actively maintained, latest stable 6.6.0, with a 6.7.0-beta.0 published 2026-08-31, so currency is not in doubt. Two notes: NuGet dates 6.5.0 to 2026-06-26 versus the GitHub release page's 15 Jul, so 'July 2026' holds only on the GitHub reading - do not treat the month as load-bearing. The k6 half is now RESOLVABLE and should not be left open: GitHub release listings render year-less dates for the current year and the fetch mislabelled them 2024, which is the 'internal inconsistency' observed. Cross-checking two independent registries settles it - Homebrew formula k6.json reports stable 2.2.0, and Docker Hub grafana/k6 shows tag 2.2.0 pushed 2026-08-10 and 1.8.1 (the maintained v1 line) pushed 2026-08-12, with master rebuilt 2026-08-31. CORRECTED CLAIM: k6 current stable is v2.2.0 (2026-08-10), actively maintained, well within 12 months; a v1 LTS line (1.8.1) is maintained in parallel.

### A11.28  [partially]

**Claim.** DERIVED from the brief's job schedule: each office is touched 343 times per day by recurring jobs (3 jobs x 96 fifteen-minute cycles + 2 hourly jobs x 24 + 7 daily jobs). At 11 offices that is 3,773 office-database touches per day; at 33 offices, 11,319. The three 15-minute jobs alone produce 132 touches/hour at 11 offices and 396/hour at 33. Because 15 minutes exceeds the documented 4-8 minute idle-reap window and MinPoolSize is presumed 0, essentially every one of those touches pays a full physical connect (socket, TLS negotiation, login) rather than reusing a pooled connection.

**Limit or threshold asserted.** 343 touches/office/day; ~11,300/day at 33 offices

- Source: My arithmetic over the brief's job schedule, combined with the Microsoft idle-reap and pooling documentation
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Arithmetic is sound: 3x96=288, 2x24=48, +7 = 343; 343x11 = 3,773; 343x33 = 11,319; 3x4x11 = 132/hr and 3x4x33 = 396/hr. The pooling premise is supported by the cited page. But 'essentially every one of those touches pays a full physical connect' OVERSTATES it and should be softened. (a) The reap window is 4-8 minutes randomised, not a hard 15-minute rule - a connection idle 15 minutes is reliably gone, but the claim's own framing ('approximately') means the reap is probabilistic per connection. (b) The premise holds only when the office database sees NO other traffic in the interval. During business hours interactive user traffic against the same office connection string keeps pooled connections warm, so the full-physical-connect cost applies mainly to off-hours cycles, not to all 343 touches. (c) MinPoolSize is explicitly 'presumed 0' and unverified - if it were set >0 the page states the pool is not destroyed until AppDomain unload, and the conclusion collapses. State the physical-connect figure as an off-hours upper bound and mark MinPoolSize as an input to be read from configuration, not presumed.

### A11.29  [partially]

**Claim.** DERIVED: connection-pool ceiling. With one host connection string plus one derived per office, each application process holds (1 + N) pools of up to 100. At N=11 that is 12 pools and 1,200 potential connections per process; at N=33, 34 pools and 3,400. With both HttpApi.Host and AuthServer resolving tenants, the two-process ceiling is 2,400 at 11 offices and 6,800 at 33  -  well under SQL Server's 32,767 user-connection maximum, so the connection count is not the binding limit; per-connection memory and the pool-fragmentation effect are, and both are unmeasured.

**Limit or threshold asserted.** 6,800 potential connections at 33 offices vs 32,767 server maximum

- Source: My arithmetic over the documented default max pool size and per-process pooling semantics
- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Arithmetic checks out (12x100=1,200; 34x100=3,400; x2 processes = 2,400 and 6,800) and the pool-per-connection-string and default-100 premises are on the cited page. Three corrections. (1) CITATION MISMATCH: the 32,767 user-connection maximum is NOT on the pooling page - it is on maximum-capacity-specifications-for-sql-server. Cite both. (2) The ceiling is a FLOOR, not a ceiling: per claim [1] the same page splits pools further by Windows identity, by SqlCredential instance, and by transaction enlistment, and an ABP host typically resolves more than one connection string per tenant if any module overrides its connection string - so (1+N) understates pool count. (3) The conclusion 'connection count is not the binding limit' compares against the wrong ceiling. The binding constraint long before 32,767 is worker threads (512 at <=4 logical CPUs per claim [7]) and per-connection server memory - the claim gestures at the latter as 'unmeasured' but should state the worker-thread number explicitly, since 6,800 potential connections against 512 workers is the real risk statement.

### A11.30  [partially]

**Claim.** DERIVED from the measured baseline: 1,450 + 2,689 = 4,139 rows over 16 appointments is 258.7 rows per appointment, but that covers only two of the four ABP audit tables. Audit log actions are written per audited controller action and application service call, so are at least as numerous as audit logs (>=1,450); entity changes are the parent rows of entity property changes, so at a typical 3-8 changed properties each they number roughly 340-900. A plausible four-table total is therefore ~6,200 rows, or ~390 per appointment  -  roughly 50% higher than the two-table figure the team currently holds.

**Limit or threshold asserted.** 258.7 rows/appointment measured (2 tables); ~390 estimated (4 tables)

- Source: My arithmetic over the brief's measured figures and the ABP audit schema
- URL: <https://abp.io/docs/latest/framework/infrastructure/audit-logging>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The arithmetic is internally consistent (4,139/16 = 258.7; 2,689/8 = 336 and 2,689/3 = 896 bracketing the 340-900 estimate; ~6,200/16 = 388 ~= 390; 390/258.7 = 1.51) and the claim is honestly labelled DERIVED. But the cited URL supports NONE of it - I confirmed the page carries no row counts - so it should not be cited as evidence for the estimate at all. Substantively, the load-bearing assumption is wrong-headed: 'audit log actions ... are at least as numerous as audit logs (>=1,450)' inverts the ABP model. ABP writes ONE AbpAuditLogs row per request/unit of work and one AbpAuditLogActions row per audited controller action AND per audited application-service method call within it - so actions are typically SEVERAL per audit log, not 'at least as many', and 1,450 is a weak lower bound rather than a central estimate. The four-table total is therefore likely higher than 6,200, and the spread is wider than the claim implies. Treat ~390/appointment as an unvalidated lower-mid estimate and satisfy the 'rows and bytes per named business transaction' requirement by measuring all five tables directly (row counts plus sys.dm_db_partition_stats / sp_spaceused deltas) rather than by extrapolation.

---

## Area: migration-path

Verification verdict for this area: **material-errors** (41 claims checked)

### A12.1  [yes]

**Claim.** nginx's documented default rewrites the Host header to the upstream name: the default directives are `proxy_set_header Host $proxy_host;` and `proxy_set_header Connection close;`. Any new proxy or load-balancing layer must therefore be explicitly verified to preserve Host, because rewriting is the documented default of at least one very common L7 proxy.

**Limit or threshold asserted.** Default: proxy_set_header Host $proxy_host;

- Source: nginx ngx_http_proxy_module documentation
- URL: <https://nginx.org/en/docs/http/ngx_http_proxy_module.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Verified verbatim. Worth adding for completeness: the same paragraph states that for HTTP/2 the ":authority" pseudo-header is sent with the $proxy_host value by default unless replaced by an explicit Host field  -  which strengthens the claim.

### A12.2  [partially]

**Claim.** nginx resolves a proxy_pass upstream name at configuration time unless the value contains a variable, in which case it is determined at request time using the configured resolver. This is the documented mechanism behind the stale-container-IP trap.

- Source: nginx ngx_http_proxy_module documentation
- URL: <https://nginx.org/en/docs/http/ngx_http_proxy_module.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The page supports only the second half. Nowhere on ngx_http_proxy_module.html does nginx state that a proxy_pass name WITHOUT a variable is resolved at configuration/startup time  -  I read the full proxy_pass section and it says only 'If a domain name resolves to several addresses, all of them will be used in a round-robin fashion.' The configuration-time resolution behaviour is real but is NOT documented on this URL; it is inferred. Either soften to 'the documentation confirms that variables plus a resolver move name resolution to request time; the converse (startup-only resolution without a variable) is well-established behaviour but is not stated on this page', or cite a page that actually asserts it.

### A12.3  [yes]

**Claim.** No SQL Server backup can be restored to an earlier version of SQL Server than the version on which the backup was created; and after restoring an earlier-version database to a newer SQL Server, the database is automatically upgraded. Together these make 'restore onto a newer major version' a one-way door.

**Limit or threshold asserted.** Target build must be >= source build; upgrade on restore is automatic and not reversible

- Source: Microsoft Learn  -  RESTORE (Transact-SQL)
- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-transact-sql>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both sentences verified verbatim. The page does not use the words 'not reversible', but the two sentences together do entail the one-way-door conclusion, so the framing is sound.

### A12.4  [yes]

**Claim.** A database containing edition-restricted persisted features cannot be moved to an edition that does not support them; sys.dm_db_persisted_sku_features lists them (ChangeCapture, ColumnStoreIndex, Compression, MultipleFSContainers, InMemoryOLTP, Partitioning, TransparentDataEncryption). Since SQL Server 2016 SP1 all of these except TransparentDataEncryption are available across editions.

**Limit or threshold asserted.** TDE is the remaining edition-restricted persisted feature post-2016 SP1

- Source: Microsoft Learn  -  sys.dm_db_persisted_sku_features
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-db-persisted-sku-features-transact-sql>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. The seven feature names match the page exactly and the 2016 SP1 note is verbatim. Note the canonical URL now redirects to .../system-dynamic-management-objects/... (the .../system-dynamic-management-views/... form still resolves).

### A12.5  [yes]

**Claim.** SQL Server 2022 edition scale limits: maximum relational database size 524 PB for Enterprise/Standard/Web and 10 GB for Express; maximum buffer pool 128 GB Standard, 64 GB Web, 1,410 MB Express. Backup compression is available on Enterprise and Standard only. Log shipping is unavailable on Express. Developer edition includes all Enterprise functionality but 'is licensed for use as a development and test system, not as a production server'.

**Limit or threshold asserted.** Express: 10 GB max database, 1410 MB buffer pool; Standard: 128 GB buffer pool, lesser of 4 sockets or 24 cores

- Source: Microsoft Learn  -  Editions and supported features of SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Every number and the Standard 'lesser of 4 sockets or 24 cores' compute limit check out. One nuance worth carrying forward: Web edition also lacks backup compression, encrypted backup, TDE and EKM  -  so 'Enterprise and Standard only' is right but the practical effect is that the edition floor for this system is Standard, not Web.

### A12.6  [yes]

**Claim.** SQL Server log shipping is built entirely from SQL Server Agent jobs (backup job, copy job, restore job, alert job), and 'A log shipping configuration doesn't automatically fail over from the primary server to the secondary server. If the primary database becomes unavailable, any of the secondary databases can be brought online manually.' This stack runs no SQL Server Agent, so log shipping is not a low-cost option here.

**Limit or threshold asserted.** Requires SQL Server Agent; manual failover only

- Source: Microsoft Learn  -  About Log Shipping (SQL Server)
- URL: <https://learn.microsoft.com/en-us/sql/database-engine/log-shipping/about-log-shipping-sql-server>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All four job types are explicitly defined as SQL Server Agent jobs and the failover sentence is verbatim. The downstream inference ('no Agent in this stack, so log shipping is not low-cost here') follows.

### A12.7  [yes]

**Claim.** SQL Server 2022 and later support BACKUP/RESTORE TO/FROM URL against S3-compatible object storage. Requirements and limits: HTTPS only (http unsupported); on Linux the CA must be placed in /var/opt/mssql/security/ca-certificates before SQL Server starts, maximum 50 certificates; total URL length limited to 259 characters (recommended under 200); the secret key must not contain a colon; striping across up to 64 URLs; a single file up to 200,000 MiB at MAXTRANSFERSIZE 20 MB; unsupported on Express editions; both path-style and virtual-host-style URLs supported.

**Limit or threshold asserted.** 10,000 parts x MAXTRANSFERSIZE per URL; 64 URLs; 259-char URL limit; CA loaded at SQL Server startup only

- Source: Microsoft Learn  -  SQL Server back up to URL for S3-compatible object storage
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/sql-server-backup-to-url-s3-compatible-object-storage>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None  -  every limit checks out verbatim. Two operationally material details the claim omits: (a) COMPRESSION is REQUIRED in order to change MAXTRANSFERSIZE at all, so the 200,000 MiB figure is unreachable without compression; (b) failed/cancelled backups leave uncommitted multipart data in the bucket that SQL Server does not clean up. Both belong in a migration runbook.

### A12.8  [yes]

**Claim.** Hangfire's recurring job scheduler acquires a global distributed lock on resource 'recurring-jobs:lock' and then a per-job distributed lock before enqueueing an occurrence. The source comments state that multiple instances can run in separate threads or processes without additional configuration because distributed locks are used, adding fail-over rather than throughput. Hangfire's own docs state each server uses distributed locks to perform coordination logic.

**Limit or threshold asserted.** Lock resource name: recurring-jobs:lock; plus AcquireDistributedRecurringJobLock(recurringJobId)

- Source: Hangfire source  -  RecurringJobScheduler.cs; Hangfire docs  -  Running Multiple Server Instances
- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/master/src/Hangfire.Core/Server/RecurringJobScheduler.cs>
- Second source: <https://docs.hangfire.io/en/latest/background-processing/running-multiple-server-instances.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Lock resource string 'recurring-jobs:lock' and AcquireDistributedRecurringJobLock(recurringJobId) both present in the source. Caveat for the record: this is a raw fetch of the master branch, i.e. a moving target, not a pinned tag  -  cite a tag or commit SHA if this needs to be reproducible.

### A12.9  [partially]

**Claim.** Two concurrent application instances sharing one Hangfire storage will not double-execute the twelve recurring jobs; two instances with SEPARATE Hangfire storage will. Since Hangfire storage here is the HOST connection string, this makes 'move state before compute' the precondition that turns a parallel run from dangerous into safe.

- Source: Derived from the Hangfire locking evidence combined with the stated system fact that Hangfire storage is the host Default connection string
- URL: <https://docs.hangfire.io/en/latest/background-processing/running-multiple-server-instances.html>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Two problems with this URL as the citation. (1) The page says nothing about separate storages double-executing  -  that is an inference, sound but unsourced here. (2) The page's substantive guidance is explicitly banner-marked 'Obsolete since 1.5' (server identifiers are now GUIDs), so it is a weak citation to hang a cutover precondition on. The load-bearing evidence is actually claim [8]'s source file, where the locks are taken against IStorageConnection  -  locks in storage A cannot exclude a scheduler in storage B. Re-anchor claim [9] to the source, keep the 'partial' rating, and state the mechanism (locks are storage-scoped) rather than the conclusion.

### A12.10  [yes]

**Claim.** ASP.NET Core Data Protection isolates applications from one another by content-root path by default, 'even if they share the same physical key repository', and this isolation 'prevents the apps from understanding each other's protected payloads'. SetApplicationName sets DataProtectionOptions.ApplicationDiscriminator; apps must share the same discriminator to read each other's payloads. DisableAutomaticKeyGeneration allows a secondary app to hold a read-only view of the key ring.

- Source: Microsoft Learn  -  Configure ASP.NET Core Data Protection
- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All three sub-claims verified verbatim. One trap on this page that a container migration will hit and that the claim does not mention: .NET 6 WebApplicationBuilder normalises the content root to end with a directory separator while other hosts do not, so the same app can silently derive two different discriminators  -  the page recommends trimming the separator and calling SetApplicationName explicitly.

### A12.11  [yes]

**Claim.** OpenIddict can format all token types except identity tokens using ASP.NET Core Data Protection, enabled per feature with UseDataProtection(). When the authorization server and API are separate applications, Data Protection 'MUST be configured to use the same application name and share the same key ring'. Switching format does not invalidate previously issued JWTs, which remain valid until expiry.

**Limit or threshold asserted.** All token types except identity tokens

- Source: OpenIddict documentation  -  ASP.NET Core Data Protection integration
- URL: <https://documentation.openiddict.com/integrations/aspnet-core-data-protection>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Verified, including the exclusion of identity tokens ('except identity tokens, that are always JWT tokens').

### A12.12  [yes]

**Claim.** OpenIddict supports registering multiple signing/encryption credentials for rotation and chooses the most appropriate key, with 'certificates with the furthest expiration date always preferred'. Development certificates are 'persisted - but not shared across instances' and the documentation cautions against them in hosted production environments, recommending self-signed certificates in the X.509 store instead.

- Source: OpenIddict documentation  -  Encryption and signing credentials
- URL: <https://documentation.openiddict.com/configuration/encryption-and-signing-credentials>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Verified. Precision note for the write-up: the documented failure mode is specifically that development certificates cannot be used on IIS or Azure App Service (an exception is thrown at runtime unless the app pool loads a user profile)  -  the caution is narrower than a general 'not for production' statement, and the page separately recommends two distinct RSA certificates in production, one for encryption and one for signing.

### A12.13  [yes]

**Claim.** Copying the Redis RDB file is safe while the server is running because the RDB is never modified once produced and is renamed atomically. Copying AOF files (a multi-part file set in a directory since Redis 7.0) during a rewrite 'might end up with an invalid backup'; the documented procedure is CONFIG SET auto-aof-rewrite-percentage 0, confirm INFO persistence shows aof_rewrite_in_progress is 0, then copy or hard-link, then re-enable. AOF durability is governed by appendfsync (always / everysec / no), with everysec risking one second of writes.

**Limit or threshold asserted.** appendfsync everysec = up to 1s data loss; AOF copy during rewrite = possibly invalid backup

- Source: Redis documentation  -  Redis persistence
- URL: <https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None  -  every element verified, including the hard-link variant and the multi-part AOF since 7.0.0. Worth noting the page now documents a newer option that supersedes this whole dance: since Redis 8.10.0 the BACKUP command family (BACKUP START / LIST / SEAL / CLEANUP) 'produces a self-contained, restorable backup without stopping writes or manually managing AOF rewrites'. If the target Redis is 8.10+, the auto-aof-rewrite-percentage procedure is the legacy path.

### A12.14  [yes]

**Claim.** Redis replication is asynchronous by default; REPLICAOF seeds a replica from a master and a promoted replica generates a new replication ID. Replication can be used to seed a new instance, with a data-loss window inherent to asynchronous acknowledgement.

**Limit or threshold asserted.** Asynchronous; WAIT reduces but does not eliminate the loss window

- Source: Redis documentation  -  Redis replication
- URL: <https://redis.io/docs/latest/operate/oss_and_stack/management/replication/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All four elements verified, including the threshold framing that WAIT reduces but does not eliminate the loss window.

### A12.15  [yes]

**Claim.** A DNS TTL is an unsigned value with a maximum of 2^31-1, and all resource records in an RRSet must carry the same TTL (RFC 2181 8 and 5.2).

**Limit or threshold asserted.** Max TTL 2147483647; RRSet TTLs must be equal

- Source: RFC 2181  -  Clarifications to the DNS Specification
- URL: <https://www.rfc-editor.org/rfc/rfc2181.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both sections verified and the section numbering (8 TTL, 5.2 TTLs of RRs in an RRSet) is correct. 8 additionally directs implementations to treat a TTL received with the most significant bit set as zero, which is a relevant hazard if any tooling writes a signed value.

### A12.16  [yes]

**Claim.** Negative (NXDOMAIN) answers are cached with a TTL of min(SOA.MINIMUM, SOA.TTL) (RFC 2308 3), and 5 recommends resolvers cap negative caching at one to three hours, noting 'Values exceeding one day have been found to be problematic'. Consequence: querying a hostname before it exists can pin an NXDOMAIN in resolver caches for hours.

**Limit or threshold asserted.** Recommended negative-cache cap 1-3 hours; values over 1 day problematic

- Source: RFC 2308  -  Negative Caching of DNS Queries
- URL: <https://www.rfc-editor.org/rfc/rfc2308.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Verified. The operational consequence drawn (querying a hostname before it exists can pin NXDOMAIN in third-party caches for hours) follows correctly, since the cap is a resolver-side recommendation the zone owner cannot enforce.

### A12.17  [yes]

**Claim.** RFC 8767 serve-stale permits recursive resolvers to answer with expired data when authoritative servers are unreachable, recommending a maximum stale timer between 1 and 3 days (some deployments up to a week) and a TTL of about 30 seconds on stale responses. Consequence: a record's TTL is a floor on cache lifetime, not a ceiling, once your authoritative servers stop answering.

**Limit or threshold asserted.** Max stale timer 1-3 days recommended; stale-response TTL 30s recommended; TTL cap recommendation 604800s

- Source: RFC 8767  -  Serving Stale Data to Improve DNS Resiliency
- URL: <https://www.rfc-editor.org/rfc/rfc8767.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All figures verified. The conclusion ('a record's TTL is a floor on cache lifetime, not a ceiling, once your authoritative servers stop answering') is a fair reading and is the operationally important half  -  it means decommissioning old authoritative servers at cutover can extend, not shorten, the stale window.

### A12.18  [partially]

**Claim.** Public recursive resolvers maintain independent caches that a zone owner cannot flush. Verified empirically on 2026-08-31 by querying two independent public resolvers over DNS-over-HTTPS and observing separate answers and TTLs, and by observing a com. NS RRset with 21,591 seconds remaining in one resolver's cache.

**Limit or threshold asserted.** Observed com. NS TTL remaining 21591s at time of query

- Source: Direct measurement via Google Public DNS and Cloudflare DNS-over-HTTPS JSON APIs
- URL: <https://dns.google/resolve>
- Second source: <https://cloudflare-dns.com/dns-query>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The behaviour class is confirmed and I reproduced it  -  two public resolvers returned wildly different remaining TTLs for the same RRSet at the same moment, which is exactly the point. Three corrections. (1) The URL as cited is not a page: a bare GET to <https://dns.google/resolve> returns HTTP 400; it only works with query parameters (?name=com&type=NS). Cite the parameterised form. (2) The specific figure 21,591 s is a one-shot observation that cannot be re-verified and should not be stated as a threshold. (3) More importantly, that number is being read wrong: ~21,600 s is 6 hours, which is Google's own cache cap on this RRSet, not the delegation TTL. Cloudflare's 157,574 s at the same instant is consistent with the real 172,800 s parent TTL. Presenting 21,591 as a delegation-TTL observation would understate the true propagation window by 8x.

### A12.19  [partially]

**Claim.** The parent-zone delegation NS TTL is set by the TLD operator, not the zone owner, so changing nameservers propagates far more slowly than changing records inside a zone. I could not measure a specific TLD delegation NS TTL from this environment  -  dig is not installed and DNS-over-HTTPS resolvers return authoritative answers rather than referrals. The general principle is established by the DNS specifications; the specific numeric value (commonly cited as 172800 seconds for .com) is UNVERIFIED here.

**Limit or threshold asserted.** Specific .com delegation NS TTL not measured

- Source: RFC 1034 / RFC 2181 delegation semantics; attempted direct measurement
- URL: <https://www.rfc-editor.org/rfc/rfc2181.html>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Credit for flagging this as unverified rather than asserting it  -  that was the right call. But two things need fixing. (1) RFC 2181 does not establish the stated principle; it ranks the credibility of parent-side data, it does not say who sets its TTL. The citation does not support the sentence it is attached to. (2) The 'cannot measure it from this environment' claim is now false: I measured it via DoH. Cloudflare returned com. NS with 157,574 s remaining, which is only consistent with an original TTL of 172,800 s (48 h)  -  so the commonly cited .com figure is corroborated from inside this environment without dig. Upgrade the finding to 'measured indirectly, consistent with 172800s' and drop RFC 2181 as the citation for the ownership principle.

### A12.20  [yes]

**Claim.** Transferring a DNSSEC-signed zone between DNS operators is constrained by the parent DS record TTL: the old DS must expire from caches before the old operator's keys are removed, creating a mandatory waiting period, and non-cooperating operators extend timelines further (RFC 6781 4.3.5, 4.3.5.1, 4.3.5.2).

**Limit or threshold asserted.** Parent DS TTL constrains changeover speed

- Source: RFC 6781  -  DNSSEC Operational Practices, Version 2
- URL: <https://www.rfc-editor.org/rfc/rfc6781.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Section numbering and titles verified against the RFC text. The substance is right  -  a changeover cannot outrun cached DS/DNSKEY data and the non-cooperating case is materially worse. Note this only bites if the zone is actually DNSSEC-signed; if it is not, this constraint does not apply and should not be carried into the timeline as a hard dependency.

### A12.21  [yes]

**Claim.** Wildcard certificates can only be issued via the DNS-01 challenge; 'This challenge cannot be used to issue wildcard certificates' is stated of HTTP-01. DNS-01 requires placing a TXT record, i.e. programmatic control of the zone.

**Limit or threshold asserted.** DNS-01 required for wildcards

- Source: Let's Encrypt  -  Challenge Types
- URL: <https://letsencrypt.org/docs/challenge-types/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Verified verbatim on both sides. Scope caveat: this is one CA's documentation, so the correct generalisation is 'ACME wildcard issuance requires DNS-01' rather than a property of TLS certificates as such  -  which matters because the capability requirement is written vendor-neutrally.

### A12.22  [yes]

**Claim.** Let's Encrypt rate limits: 50 certificates per registered domain per 7 days; 5 certificates per exact same set of identifiers per 7 days; 300 new orders per account per 3 hours; 5 authorization failures per identifier per hour. A staging environment with significantly higher limits exists for testing. Consequence: rehearsing a cutover that reissues the production SAN set is limited to five attempts per week, and a broken DNS-01 automation exhausts the failure budget within minutes.

**Limit or threshold asserted.** 5 duplicate certificates per 7 days; 5 authorization failures per identifier per hour; 50 certs per registered domain per 7 days

- Source: Let's Encrypt  -  Rate Limits
- URL: <https://letsencrypt.org/docs/rate-limits/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. All four numbers verified verbatim, and the operational consequence (five rehearsals per week of the exact production SAN set; a broken DNS-01 loop burns the failure budget in minutes) follows directly. Practical addition: the duplicate-certificate limit keys on the exact identifier set, so a rehearsal cert with one extra or one fewer SAN draws from a different bucket  -  that is the documented way to rehearse without spending the production allowance, alongside staging.

### A12.23  [yes]

**Claim.** HSTS preload submission requires max-age of at least 31536000 seconds (1 year), includeSubDomains, and the preload directive. 'Be aware that inclusion in the preload list cannot easily be undone', and removal 'takes months for a change to reach users with a Chrome update and we cannot make guarantees about other browsers.'

**Limit or threshold asserted.** max-age >= 31536000; removal measured in months

- Source: hstspreload.org (Chromium HSTS preload list submission site)
- URL: <https://hstspreload.org/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Verified verbatim including the exact max-age threshold. Relevant to a migration plan and not mentioned in the claim: includeSubDomains is mandatory for preload, so every subdomain  -  including any temporary or internal host under the apex  -  must be HTTPS-capable before submission, and that constraint persists for the months-long removal window.

### A12.24  [yes]

**Claim.** Amazon S3 currently supports both path-style and virtual-hosted-style URLs in all regions, but 'path-style URLs will be discontinued in the future' (deprecation delayed as of 23 September 2020). Separately, 'When you're using virtual-hosted-style general purpose buckets with SSL, the SSL wildcard certificate matches only buckets that do not contain dots'  -  the same single-label wildcard constraint that forced path-style addressing in this system.

**Limit or threshold asserted.** Path-style still supported; deprecation announced with no current date

- Source: AWS  -  Virtual hosting of general purpose buckets
- URL: <https://docs.aws.amazon.com/AmazonS3/latest/userguide/VirtualHosting.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both quotes verified verbatim and the 'no current date' characterisation is accurate  -  the page announces discontinuation with no replacement date. This is correctly used as evidence of a behaviour class (single-label wildcard matching) rather than as a reason to adopt any particular storage service.

### A12.25  [partially]

**Claim.** S3 object integrity should be verified using explicit checksum algorithms (CRC64NVME default, plus CRC32, CRC32C, SHA1, SHA256, MD5, SHA512 and others); AWS SDKs do not automatically calculate MD5 checksums and the legacy Content-MD5 header applies to single-part uploads. Consequently ETag comparison is not a valid integrity check for multipart objects.

- Source: AWS  -  Checking object integrity in Amazon S3
- URL: <https://docs.aws.amazon.com/AmazonS3/latest/userguide/checking-object-integrity.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The algorithm list and the SDK/MD5 sentence are verified. Two things to fix. (1) The Content-MD5 sentence is narrower than quoted: the page qualifies it as 'single part uploads using SSE-S3 encryption', not single-part uploads generally. (2) The ETag conclusion is not supported by this URL  -  the page never mentions ETag at all. The conclusion is correct in substance (a multipart ETag is a digest-of-digests with a part-count suffix and is not the object's MD5), but it needs a citation that actually says so, or it should be presented as reasoning rather than as sourced fact. Since the capability requirement explicitly forbids ETag comparison, this is load-bearing and should not rest on an unsourced inference.

### A12.26  [no]

**Claim.** mc mirror synchronises content rsync-style with --watch (continuous), --remove and --overwrite, and offers --checksum/--md5 flags for uploads, but 'mc mirror only synchronises the current object without any version information or metadata other than tags' and does not inherently verify object integrity during synchronisation. MinIO's documentation now sits under the MinIO AIStor product line, with client releases referenced as recent as RELEASE.2025-07-05, confirming active maintenance.

**Limit or threshold asserted.** AIStor Client RELEASE.2025-07-05T15-07-57Z referenced

- Source: MinIO documentation  -  mc mirror
- URL: <https://docs.min.io/enterprise/aistor-object-store/reference/cli/mc-mirror/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: This is the material error in the set  -  the exact failure mode of 'tool claimed maintained that is archived'. The upstream MinIO Client repository (github.com/minio/mc) was ARCHIVED by its owner on 14 July 2026 and is read-only; its final release is RELEASE.2025-08-13T08-35-41Z from September 2025. A vendor documentation page for the commercial AIStor product referencing a July 2025 client build is not evidence of active maintenance in August 2026, and the inference 'confirming active maintenance' is false. Secondary corrections: (a) the flag behaviours and the 'only synchronises the current object without any version information or metadata other than tags' quote are correct and verified; (b) 'does not inherently verify object integrity during synchronisation' is NOT stated on the page  -  the page documents --checksum (MD5, CRC32, CRC32C, SHA1, SHA256) and --md5, so this is an inference, not a citation. Practical consequence: do not write a migration plan whose verification step depends on an archived client. This actually reinforces the capability requirement that object migration be verified by independently computed digests and counts rather than by the migration tool's own success report  -  the tool may be unmaintained by the time the cutover runs.

### A12.27  [yes]

**Claim.** EF Core 9 and later automatically acquire a database-wide lock before applying migrations via Migrate/MigrateAsync, migration bundles and the CLI tools, protecting against concurrent migration corruption. EF Core 9+ also throws when the model has pending changes relative to the last migration (RelationalEventId.PendingModelChangesWarning), detectable ahead of deployment with `dotnet ef migrations has-pending-model-changes`. Microsoft's guidance: use migration bundles for automated deployment, do not make every application replica migrate from its entrypoint, and do not restart the migration container after successful exit. A bundle can roll back by passing a target migration (0 reverts all), with data-loss warnings.

**Limit or threshold asserted.** Migration locking from EF Core 9; PendingModelChangesWarning from EF Core 9

- Source: Microsoft Learn  -  Applying Migrations (EF Core)
- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None  -  every element verified verbatim, including the lock covering bundles and CLI but explicitly NOT SQL scripts. Two caveats worth carrying into the runbook: the page warns that the locking mechanism is provider-specific and can leave abandoned locks if a process dies, and that wrapping MigrateAsync in an explicit transaction is unsupported. Neither changes the claim; both matter if the migration job is killed mid-cutover.

### A12.28  [yes]

**Claim.** ABP registers tenant resolve contributors in order: CurrentUserTenantResolveContributor first ('This should always be the first contributor for the security'), then QueryString (__tenant), Route (__tenant), Header (__tenant), Cookie (__tenant). Per-tenant connection strings are stored in tenant configuration; managing them from the UI is an ABP Commercial SaaS module feature rather than open-source. This corroborates the system brief's honest caveat that the four default __tenant resolvers may still be active, and establishes that per-office connection strings are an available mechanism.

**Limit or threshold asserted.** Default tenant key: __tenant

- Source: ABP Framework documentation  -  Multi-Tenancy
- URL: <https://abp.io/docs/latest/framework/architecture/multi-tenancy>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Resolver order, the __tenant key and the SaaS/PRO restriction all verified. Minor wording: the docs say 'SaaS module (PRO)' rather than 'ABP Commercial SaaS module'; same thing, but quote it as written. The two conclusions drawn  -  that the four __tenant resolvers may still be live, and that per-office connection strings are an available mechanism  -  both hold, and the first is a genuine security consideration since query-string and header resolvers are enabled by default.

### A12.29  [yes]

**Claim.** Strangler fig requires identifying architectural seams and accepts transitional architecture as a deliberate cost: 'people often balk at the necessity of building transitional architecture... While this may appear to be a waste, the reduced risk and earlier value from the gradual approach outweigh its costs.' Article last updated 22 August 2024.

- Source: Martin Fowler  -  Strangler Fig Application
- URL: <https://martinfowler.com/bliki/StranglerFigApplication.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Quote, seams discussion and the 22 August 2024 date all verified on the page.

### A12.30  [yes]

**Claim.** Branch by abstraction requires that an abstraction layer or seam already exist 'to allow multiple implementations to co-exist in the software system', and Fowler notes the limitation that 'sometimes you can't swap-out the supplier for only some clients, you have to do it all at once'. That limitation is exactly the current state of the office database connection strings.

- Source: Martin Fowler  -  Branch By Abstraction (7 January 2014)
- URL: <https://martinfowler.com/bliki/BranchByAbstraction.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both quotes verified. The application to the office connection strings is the researcher's own analogy rather than anything on the page, but it is clearly presented as such and is apt.

### A12.31  [yes]

**Claim.** Parallel change (expand-contract) has three phases  -  expand, migrate, contract  -  and allows release at any phase; its stated risk is that 'if the contract phase is not executed you might end up in a worse state than you started'. Author Danilo Sato, 13 May 2014.

- Source: Martin Fowler's site  -  Parallel Change, by Danilo Sato
- URL: <https://martinfowler.com/bliki/ParallelChange.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Phases, release-at-any-phase property, stated risk, author and date all verified.

### A12.32  [yes]

**Claim.** Blue-green deployment identifies the database as the principal difficulty and prescribes schema-first sequencing: 'first apply a database refactoring to change the schema to support both the new and old version of the application, deploy that, check everything is working fine so you have a rollback point, then deploy the new version of the application.' The article does not propose DNS as the switching mechanism, and notes one project switched by bouncing the web server rather than the router.

- Source: Martin Fowler  -  BlueGreenDeployment (1 March 2010)
- URL: <https://martinfowler.com/bliki/BlueGreenDeployment.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Both quotes verified, and the negative claim checks out too  -  DNS is not proposed as the switching mechanism anywhere on the page. Verifying a negative claim is easy to skip and it was correct here; that matters, because it means blue-green cannot be cited as authority for a DNS-based cutover.

### A12.33  [yes]

**Claim.** Canary release requires a router or a partitioning strategy plus monitoring, lists internal staff and geographic/brand partitioning as legitimate selection strategies, and states 'Managing database changes also requires attention when doing canary releases', requiring parallel-change techniques so both versions can run against one schema.

- Source: Martin Fowler  -  CanaryRelease (25 June 2014)
- URL: <https://martinfowler.com/bliki/CanaryRelease.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Verified, including the brand/region partitioning and the Facebook internal-employees canary example.

### A12.34  [partially]

**Claim.** 'Stop the World cutover' is a named legacy-displacement pattern covering pausing business activity for data migration, reconfiguration of integration points and DNS updates. The authors record that 'We've seen some large failures occur following this approach, often when no rehearsal or practice of the cut-over was possible or performed', that 'Roll back in the event of a problem or overrun needs to be planned for', and that resuming normal activity can create very large peak loads. Cartwright, Horn and Lewis, 5 March 2024.

- Source: Martin Fowler's site  -  Patterns of Legacy Displacement: Stop the World cutover
- URL: <https://martinfowler.com/articles/patterns-legacy-displacement/stop-the-world.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All three quotes are verbatim, the authors are correct, and the DNS/integration-point framing is supported ('migration of data, reconfiguration of other systems to point to new integration points, update of DNS to point to new websites'). The date is wrong: this page carries og:article:modified_time 2021-05-26. '05 March 2024' is the date on the parent index page (martinfowler.com/articles/patterns-legacy-displacement/), not on the stop-the-world sub-page. Attribute the date to the parent article or drop it. Also worth capturing, since it directly contradicts a stop-the-world cutover strategy: the authors close the section with 'We prefer more incremental approaches that allow migration of business to happen in smaller less risk increments.'

### A12.35  [partially]

**Claim.** There is no 'Parallel Run' page on martinfowler.com  -  the URL returns HTTP 404. The related catalogued patterns are Dark Launching, Canary Release, Event Interception, Divert the Flow and Transitional Architecture. I therefore do NOT attribute a 'parallel run' pattern to Fowler; my treatment of running two implementations side by side is my own reasoning informed by those catalogued patterns.

- Source: Martin Fowler  -  Patterns of Legacy Displacement catalogue; negative result for /bliki/ParallelRun.html
- URL: <https://martinfowler.com/articles/patterns-legacy-displacement/>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: The 404 is confirmed and the intellectual honesty here is the right instinct  -  do not attribute a pattern to an author who did not write it. But the enumeration is inaccurate: the patterns catalogued on that index are Critical Aggregator, Divert the Flow, Event Interception, Extract Product Lines, Feature Parity, Legacy Mimic, Revert to Source and Transitional Architecture. Dark Launching and Canary Release are separate bliki entries referenced in the narrative, not entries in this catalogue, and Critical Aggregator, Extract Product Lines, Feature Parity, Legacy Mimic and Revert to Source were omitted. Also note the claim as worded ('no Parallel Run page on martinfowler.com') is broader than what was tested  -  only one URL under one article directory was checked, and I could not run a site-wide search to confirm the general negative. Narrow the wording to the directory actually tested. Separately: 'Parallel Run' is a named pattern in Sam Newman's Monolith to Microservices, which is very likely the source of the half-memory being corrected here  -  worth citing properly rather than treating the concept as unattributed.

### A12.36  [partially]

**Claim.** Microsoft's multitenancy guidance describes automated single-tenant / horizontally-partitioned deployments and states that with dedicated per-tenant deployments 'Updates and changes can be rolled out progressively across tenants, which reduces the likelihood of a system-wide outage', while warning that 'ongoing maintenance, like applying new configuration or software updates, can be time consuming' and advising automation of operational processes. This system is a horizontally-partitioned deployment: shared application tier, database per tenant.

- Source: Microsoft Learn  -  Tenancy models for a multitenant solution (Azure Architecture Center, updated 2025-06-11)
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/tenancy-models>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Both quotes are verbatim and are correctly attributed in the claim's own wording to 'dedicated per-tenant deployments'. The problem is the juxtaposition: the claim ends by asserting the system is horizontally partitioned, which invites the reader to apply the progressive-rollout benefit to this system. It does not transfer. Under the page's taxonomy those benefits belong to Automated single-tenant deployments, where the whole stack including compute is per-tenant. In a horizontally partitioned deployment the application tier is SHARED, so an application change lands on every tenant simultaneously and the progressive-rollout property is exactly what this system does NOT have  -  only the per-tenant databases can be staged. The page's own guidance for the horizontally partitioned model is the sentence quoted above. Split these into two statements so the benefit is not silently inherited; this bears directly on the capability requirement that each tenant database be independently addressable, which is the only per-tenant staging lever actually available.

### A12.37  [yes]

**Claim.** 45 CFR 164.316(b)(2)(i) requires retaining 'the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later'. Paragraph (b)(1) is about policies and procedures maintained in written or electronic form. IMPORTANT CORRECTION TO THE BRIEF: this text is about documentation of policies, procedures, actions, activities and assessments  -  whether it captures an application's entity-property-change audit trail is an interpretation, not a plain reading. The sizing consequence is unaffected, because nothing prunes the audit tables regardless of the retention basis.

**Limit or threshold asserted.** 6 years from creation or last effective date, whichever is later

- Source: 45 CFR  164.316 (Cornell Legal Information Institute)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.316>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None, and the self-correction embedded in this claim is the single best piece of work in the set  -  the regulation governs documentation of policies, procedures, actions, activities and assessments, and it is genuinely a stretch to read it as mandating retention of an ORM's property-level change log. Flagging that as interpretation rather than plain reading is correct and should survive into the final document. The sizing conclusion is unaffected as stated, since nothing prunes those tables regardless of the retention basis.

### A12.38  [yes]

**Claim.** 45 CFR 164.308(a)(7) contingency plan: data backup plan (Required), disaster recovery plan (Required), emergency mode operation plan (Required), testing and revision procedures (Addressable), applications and data criticality analysis (Addressable). 164.308(b)(1): a covered entity may permit a business associate to create, receive, maintain or transmit ePHI on its behalf 'only if the covered entity obtains satisfactory assurances... that the business associate will appropriately safeguard the information.' The BAA obligation is conditioned on ePHI being involved  -  which is why the current synthetic-data window permits trialling infrastructure before any BAA exists.

**Limit or threshold asserted.** Data backup, disaster recovery and emergency mode operation are Required; testing/revision is Addressable

- Source: 45 CFR  164.308 (Cornell Legal Information Institute)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.308>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None. Every Required/Addressable designation verified and 164.308(b)(1) quoted verbatim. The inference that the synthetic-data window permits trialling infrastructure before a BAA exists is correct on the face of the text, since the obligation attaches to ePHI. Flag as a project risk rather than a citation error: that permission evaporates the moment real ePHI touches the new infrastructure, so the BAA must be in place before the first production data load, not before go-live.

### A12.39  [yes]

**Claim.** HHS breach-notification guidance: ePHI is 'secured' (and breach notification not triggered) when encrypted per the Security Rule using FIPS 140-2 validated processes, referencing NIST SP 800-111 for data at rest and NIST SP 800-52, SP 800-77 and SP 800-113 for data in motion; media must be destroyed or cleared consistent with NIST SP 800-88. Relevance: an abandoned rollback copy of a PHI-bearing database on unencrypted storage is a breach candidate  -  a risk that does not exist today and will exist permanently after go-live.

**Limit or threshold asserted.** NIST SP 800-111 (at rest), SP 800-52/800-77/800-113 (in motion), SP 800-88 (sanitization), FIPS 140-2 validation

- Source: HHS  -  Guidance to Render Unsecured PHI Unusable, Unreadable, or Indecipherable
- URL: <https://www.hhs.gov/hipaa/for-professionals/breach-notification/guidance/index.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: None  -  all five NIST references and the FIPS 140-2 (not 140-3) designation verified as written on the page. Note for the record that FIPS 140-2 has been superseded by FIPS 140-3 in NIST's own validation programme, but the HHS guidance still says 140-2, so quoting it as 140-2 is correct citation practice. The derived risk  -  an abandoned rollback copy of a PHI-bearing database on unencrypted storage being a breach candidate  -  follows directly from the safe-harbour framing and is the sharpest operational consequence in this set.

### A12.40  [partially]

**Claim.** The HIPAA Security Rule NPRM was published 27 December 2024 (Federal Register 6 January 2025), with a comment period closing 7 March 2025. It proposes, among other things, to expressly require encryption of ePHI with limited exceptions and to require inventorying technology assets and mapping the movement of ePHI. As of this access date no final rule had been located. I could not verify the frequently-repeated claims about a 72-hour restoration requirement or the wholesale removal of the addressable/required distinction from the sources I could reach  -  eCFR and federalregister.gov both refused automated access. Treat the NPRM as a direction of travel, not a current obligation.

**Limit or threshold asserted.** Comment period closed 2025-03-07; no final rule located as of 2026-08-31

- Source: HHS HIPAA Security Rule NPRM page; Federal Register document 2024-30983 via govinfo
- URL: <https://www.hhs.gov/hipaa/for-professionals/security/hipaa-security-rule-nprm/index.html>
- Second source: <https://www.govinfo.gov/content/pkg/FR-2025-01-06/html/2024-30983.htm>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The two load-bearing facts are confirmed on the page: the 27 December 2024 issuance and, decisively, that no final rule has taken effect ('the current Security Rule remains in effect'). Everything else attributed to this URL is not on it  -  the 6 January 2025 Federal Register date, the 7 March 2025 comment close, the express encryption requirement and the asset-inventory/ePHI-mapping proposals all come from the Federal Register notice itself, which the page only links to. Either cite the FR notice directly or attribute those details as 'not verified from the reachable source'. The claim's own 'partial' rating and its explicit refusal to repeat the widely circulated 72-hour restoration and addressable/required-removal claims are correct and should be preserved  -  those are exactly the kind of secondhand figures that propagate unchecked. The conclusion to treat the NPRM as direction of travel rather than current obligation is right, and it means no capability requirement should be justified by the NPRM alone.

### A12.41  [yes]

**Claim.** Microsoft's SQL Server migration guidance frames cutover as: verify data is the same on source and target, then cut over, and 'plan the cutover process with business / application teams to ensure minimal interruption during cutover doesn't affect business continuity.' It prescribes a post-migration validation programme of source/target comparison queries run in an isolated test environment.

- Source: Microsoft Learn  -  SQL Server migration guide (data sync and cutover)
- URL: <https://learn.microsoft.com/en-us/data-migration/sql-server/database/guide>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Quotes verified verbatim. One scoping correction: this page is titled 'SQL Server to Azure SQL Database: Migration Guide' and is specific to one migration target, not general SQL Server migration guidance. The cutover sequencing and validation advice are generic enough to travel, but describe it as what it is, and be aware the surrounding recommendations (log generation rate ceilings, service-tier scaling, ExpressRoute bandwidth) are target-specific and should not be carried across. Citing it as evidence of a practice class is fine; citing it as neutral guidance is not.

---

## Area: admin-surface

Verification verdict for this area: **material-errors** (31 claims checked)

### A13.1  [yes]

**Claim.** Hangfire's Dashboard is restricted to local requests by default; the shipped DashboardOptions default Authorization is a LocalRequestsOnlyAuthorizationFilter. Therefore an authorization filter whose body is 'return true' is an active downgrade of a safe default, not an unset default.

**Limit or threshold asserted.** private static readonly IDashboardAuthorizationFilter[] DefaultAuthorization = new[] { new LocalRequestsOnlyAuthorizationFilter() };

- Source: Hangfire source, src/Hangfire.Core/DashboardOptions.cs and Dashboard/LocalRequestsOnlyAuthorizationFilter.cs (read from a clone of HangfireIO/Hangfire)
- URL: <https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/DashboardOptions.cs>
- Second source: <https://docs.hangfire.io/en/latest/configuration/using-dashboard.html>
- Accessed: 2026-08-31
- Confidence: verified

### A13.2  [yes]

**Claim.** Hangfire's own documentation warns that the dashboard exposes sensitive job information and management actions and that access must be restricted.

**Limit or threshold asserted.** "Hangfire Dashboard exposes sensitive information about your background jobs, including method names and serialized arguments as well as gives you an opportunity to manage them by performing different actions - retry, delete, trigger, etc. So it is really important to restrict access to the Dashboard."

- Source: Hangfire documentation, Using Dashboard
- URL: <https://docs.hangfire.io/en/latest/configuration/using-dashboard.html>
- Accessed: 2026-08-31
- Confidence: verified

### A13.3  [yes]

**Claim.** nginx prefix location matching is case-sensitive on Linux; case-insensitivity applies only on case-insensitive operating systems. nginx matches against a normalized URI after percent-decoding, resolving . and .. and collapsing adjacent slashes.

**Limit or threshold asserted.** "For case-insensitive operating systems such as macOS and Cygwin, matching with prefix strings ignores a case (0.7.7)." and "The matching is performed against a normalized URI, after decoding the text encoded in the \"%XX\" form, resolving references to relative path components \".\" and \"..\", and possible compression of two or more adjacent slashes into a single slash."

- Source: nginx documentation, ngx_http_core_module, location directive
- URL: <https://nginx.org/en/docs/http/ngx_http_core_module.html#location>
- Accessed: 2026-08-31
- Confidence: verified

### A13.4  [partially]

**Claim.** ASP.NET Core path matching is case-insensitive: PathString.StartsWithSegments defaults to StringComparison.OrdinalIgnoreCase, and endpoint route text matching is case-insensitive on the decoded path. Combined with the nginx behaviour above, an nginx prefix deny rule on /hangfire does not block GET /Hangfire, which ASP.NET Core still routes to the dashboard.

**Limit or threshold asserted.** Source: "return StartsWithSegments(other, StringComparison.OrdinalIgnoreCase);" Routing docs: "Text matching is case-insensitive and based on the decoded representation of the URL's path."

- Source: ASP.NET Core framework source (PathString.cs) and Microsoft Learn routing documentation
- URL: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/Http/Http.Abstractions/src/PathString.cs>
- Second source: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Both facts are true, but the cited URL settles only the PathString half. The routing quote ('Text matching is case-insensitive and based on the decoded representation of the URL's path.') is on a different page and must be cited separately: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-10.0> . Verified there verbatim.

### A13.5  [yes]

**Claim.** The conclusion that an edge path deny-list is structurally unsound for this stack is my architectural reasoning, built on the two verified parser facts above rather than being asserted by any single source.

- Source: Analysis by this researcher
- URL: <https://nginx.org/en/docs/http/ngx_http_core_module.html#location>
- Accessed: 2026-08-31
- Confidence: REASONING

### A13.6  [yes]

**Claim.** NIST SP 800-53 Rev 5 SC-2 requires separating user functionality from system management functionality, and its guidance names separate network addresses and isolating administrative interfaces on different domains as accepted means. This is the authoritative statement of the containment-by-position principle.

**Limit or threshold asserted.** SC-2 statement: "Separate user functionality, including user interface services, from system management functionality." Guidance: "Organizations may separate system management functions from user functions by using different computers, instances of operating systems, central processing units, or network addresses... Separation of system and user functions may include isolating administrative interfaces on different domains and with additional access controls."

- Source: NIST SP 800-53 Rev 5 catalog (official OSCAL JSON, usnistgov/oscal-content)
- URL: <https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json>
- Accessed: 2026-08-31
- Confidence: verified

### A13.7  [partially]

**Claim.** NIST SP 800-53 Rev 5 SC-7(5) requires denying network traffic by default and allowing by exception; CM-7(5) requires a deny-all, permit-by-exception policy. This supports the allow-list principle while saying nothing about which layer implements it.

**Limit or threshold asserted.** SC-7(5): "Deny network communications traffic by default and allow network communications traffic by exception." Guidance: "A deny-all, permit-by-exception network communications traffic policy ensures that only those system connections that are essential and approved are allowed." CM-7(5)(b): "Employ a deny-all, permit-by-exception policy..."

- Source: NIST SP 800-53 Rev 5 catalog (official OSCAL JSON)
- URL: <https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: SC-7(5) is confirmed verbatim. CM-7(5) is NOT a general allow-list control: it is titled 'Authorized Software - Allow-by-exception' and its (b) is scoped to 'the execution of authorized software programs on the system'. The claim's ellipsis hides that scope. Drop CM-7(5) from the endpoint/network allow-list argument, or cite it only for software-execution allow-listing; SC-7(5) carries the network claim by itself.

### A13.8  [yes]

**Claim.** OWASP Top 10:2025 is the current edition and Security Misconfiguration moved UP from #5 to #2 (A02:2025); 100% of applications tested showed some form of misconfiguration. Its prevention guidance names segmentation between components or tenants, and CWE-489 Active Debug Code is a mapped CWE.

**Limit or threshold asserted.** "Moving up from #5 in the previous edition, 100% of the applications tested were found to have some form of misconfiguration, with an average incidence rate of 3.00%, and over 719k occurrences." Prevention: "A segmented application architecture provides effective and secure separation between components or tenants, with segmentation, containerization, or cloud security groups (ACLs)." Vulnerable if: "Unnecessary features are enabled or installed (e.g., unnecessary ports, services, pages, accounts, testing frameworks, or privileges)."

- Source: OWASP Top 10:2025, A02:2025 Security Misconfiguration (read from a clone of OWASP/Top10, repo HEAD 2026-08-05)
- URL: <https://owasp.org/Top10/>
- Second source: <https://github.com/OWASP/Top10/blob/master/2025/docs/en/A02_2025-Security_Misconfiguration.md>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Substance verified, but <https://owasp.org/Top10/> is only a redirect notice; cite the category page directly: <https://owasp.org/Top10/2025/A02_2025-Security_Misconfiguration/>

### A13.9  [yes]

**Claim.** OWASP ASVS 5.0 requires that documentation and monitoring endpoints not be exposed unless explicitly intended, and that debug modes be disabled in production. These are the requirement-level statements for diagnostic surface containment.

**Limit or threshold asserted.** 13.4.5 (Level 2): "Verify that documentation (such as for internal APIs) and monitoring endpoints are not exposed unless explicitly intended." 13.4.2 (Level 2): "Verify that debug modes are disabled for all components in production environments to prevent exposure of debugging features and information leakage."

- Source: OWASP ASVS 5.0, V13 Configuration (read from a clone of OWASP/ASVS)
- URL: <https://github.com/OWASP/ASVS/blob/master/5.0/en/0x22-V13-Configuration.md>
- Accessed: 2026-08-31
- Confidence: verified

### A13.10  [yes]

**Claim.** OWASP ASVS 5.0 explicitly states that network location or trusted endpoints must not be the SOLE factors for authorizing access to administrative interfaces. This is the counterweight to any purely network-position containment design.

**Limit or threshold asserted.** 8.4.2 (Level 3): "Verify that access to administrative interfaces incorporates multiple layers of security... ensuring that network location or trusted endpoints are not the sole factors for authorization even though they may reduce the likelihood of unauthorized access."

- Source: OWASP ASVS 5.0, V8.4 Other Authorization Considerations
- URL: <https://github.com/OWASP/ASVS/blob/master/5.0/en/0x17-V8-Authorization.md>
- Accessed: 2026-08-31
- Confidence: verified

### A13.11  [yes]

**Claim.** Setting ASPNETCORE_FORWARDEDHEADERS_ENABLED=true does not restrict which peer IPs may set forwarded headers; Microsoft documents that it clears KnownNetworks and KnownProxies. Defaults are KnownNetworks = 127.0.0.0/8, KnownProxies = IPv6 loopback, ForwardLimit = 1.

**Limit or threshold asserted.** "Warning: This flag uses settings designed for cloud environments and doesn't enable features such as the KnownProxies option to restrict which IPs forwarders are accepted from." Documented behaviour: "options.KnownNetworks.Clear(); options.KnownProxies.Clear();" Also: "Only allow trusted proxies and networks to forward headers. Otherwise, IP spoofing attacks are possible." Defaults: KnownNetworks = "a single entry for new IPNetwork(IPAddress.Loopback, 8)"; KnownProxies = "a single entry for IPAddress.IPv6Loopback"; ForwardLimit default 1.

- Source: Microsoft Learn, Configure ASP.NET Core to work with proxy servers and load balancers
- URL: <https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified

### A13.12  [yes]

**Claim.** That restoring Hangfire's LocalRequestsOnly default behind a Docker reverse proxy creates a spoofable configuration via forwarded headers is my reasoning, composing the verified Hangfire filter source (which compares RemoteIpAddress to loopback) with the verified Microsoft forwarded-headers behaviour. I did not find a source stating this combination explicitly.

- Source: Analysis by this researcher
- URL: <https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Composition is sound and both premises verified; the self-declared 'partial' confidence is appropriate. Worth citing the filter source file explicitly alongside the Microsoft page so the composition is auditable.

### A13.13  [partially]

**Claim.** ASP.NET Core supports constraining an endpoint to a specific port via RequireHost("*:PORT"), and Microsoft's own documentation uses a diagnostic endpoint as the worked example. Hangfire's MapHangfireDashboard returns IEndpointConventionBuilder, so this applies to it.

**Limit or threshold asserted.** "Port: *:5000, matches port 5000 with any host." Example given: app.MapHealthChecks("/healthz").RequireHost("*:8080"); Hangfire: public static IEndpointConventionBuilder MapHangfireDashboard(... ) with default pattern "/hangfire", mapped as endpoints.Map(pattern + "/{**path}", pipeline).

- Source: Microsoft Learn routing documentation; Hangfire source HangfireEndpointRouteBuilderExtensions.cs
- URL: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-10.0>
- Second source: <https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.AspNetCore/HangfireEndpointRouteBuilderExtensions.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Both halves are factually correct, but the cited routing URL settles only the RequireHost half. The Hangfire signature and '/{**path}' mapping are in HangfireEndpointRouteBuilderExtensions.cs (raw.githubusercontent.com/HangfireIO/Hangfire/main/src/Hangfire.AspNetCore/HangfireEndpointRouteBuilderExtensions.cs) and must be cited there. Independently confirmed.

### A13.14  [partially]

**Claim.** AuthorizationOptions.FallbackPolicy provides application-level deny-by-default: it applies where no authorization metadata exists, defaults to null (no effect), and Microsoft recommends it precisely because it protects newly added routes.

**Limit or threshold asserted.** "By default, FallbackPolicy is null, meaning it has no effect unless explicitly set." And: "Setting the fallback authorization policy to require users to be authenticated protects newly added Razor Pages and controllers. Having authorization required by default is more secure than relying on new controllers and Razor Pages to include the [Authorize] attribute." Also: "For requests served by other middleware after the authorization middleware, such as static files, the policy applies to all requests."

- Source: Microsoft Learn, authorization documentation and FallbackPolicy API reference
- URL: <https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authorization.authorizationoptions.fallbackpolicy?view=aspnetcore-10.0>
- Second source: <https://learn.microsoft.com/en-us/aspnet/core/security/authorization/secure-data?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The 'null by default / no effect' fact is on the cited page. The other two quotes are not - they are on <https://learn.microsoft.com/en-us/aspnet/core/security/authorization/secure-data?view=aspnetcore-10.0> ('Setting the fallback authorization policy to require users to be authenticated protects newly added Razor Pages and controllers. Having authorization required by default is more secure than relying on new controllers and Razor Pages to include the [Authorize] attribute.' and 'For requests served by other middleware after the authorization middleware, such as static files, the policy applies to all requests.'). Both verified verbatim there; split the citation.

### A13.15  [yes]

**Claim.** EndpointDataSource exposes an Endpoints property returning a read-only collection of all registered Endpoint instances, making an automated startup/test-time inventory of every route feasible.

**Limit or threshold asserted.** "Endpoints | Returns a read-only collection of Endpoint instances." Available in Microsoft.AspNetCore.App.Ref v10.0.0.

- Source: Microsoft Learn, EndpointDataSource Class (Microsoft.AspNetCore.Routing)
- URL: <https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.routing.endpointdatasource?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified

### A13.16  [partially]

**Claim.** Docker published ports bypass ufw firewall rules because Docker routes container traffic in the nat table before packets reach the INPUT chain. Publishing is insecure by default and binds to all host addresses unless a host IP is specified.

**Limit or threshold asserted.** "Docker and ufw use firewall rules in ways that make them incompatible with each other." "Docker routes container traffic in the nat table, which means that packets are diverted before it reaches the INPUT and OUTPUT chains." Port publishing: "Publishing container ports is insecure by default." "By default, when a container's ports are mapped without any specific host address, the Docker daemon publishes ports to all host addresses (0.0.0.0 and [::])". "If you include the localhost IP address (127.0.0.1, or ::1) with the publish flag, only the Docker host can access the published container port." Caveat: in Docker versions before 28.0.0 localhost-bound ports could be reached by hosts on the same network switch.

- Source: Docker documentation, Packet filtering and firewalls; Docker documentation, Port publishing
- URL: <https://docs.docker.com/engine/network/packet-filtering-firewalls/>
- Second source: <https://docs.docker.com/engine/network/port-publishing/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All stated facts are true, but split across two pages. The port-publishing quotes are on <https://docs.docker.com/engine/network/port-publishing/> : 'Publishing container ports is insecure by default.', 'If you include the localhost IP address (127.0.0.1, or ::1) with the publish flag, only the Docker host can access the published container port.', and 'In releases older than 28.0.0, hosts within the same L2 segment (for example, hosts connected to the same network switch) can reach ports published to localhost.' Verified verbatim there; cite both URLs.

### A13.17  [yes]

**Claim.** nginx selects the first server block for an address:port pair as the default when no server_name matches and no default_server is declared. This explains why the bare apex currently lands on the AuthServer.

**Limit or threshold asserted.** "The default_server parameter, if present, will cause the server to become the default server for the specified address:port pair. If none of the directives have the default_server parameter then the first server with the address:port pair will be the default server for this pair."

- Source: nginx documentation, ngx_http_core_module (listen/server_name)
- URL: <https://nginx.org/en/docs/http/ngx_http_core_module.html#server_name>
- Accessed: 2026-08-31
- Confidence: verified

### A13.18  [yes]

**Claim.** nginx supports client certificate verification per server block via ssl_verify_client, which defaults to off and accepts on | off | optional | optional_no_ca. ssl_verify_depth defaults to 1.

**Limit or threshold asserted.** Syntax: ssl_verify_client on | off | optional | optional_no_ca; Default: ssl_verify_client off; Context: http, server. ssl_verify_depth default 1.

- Source: nginx documentation, ngx_http_ssl_module
- URL: <https://nginx.org/en/docs/http/ngx_http_ssl_module.html>
- Accessed: 2026-08-31
- Confidence: verified

### A13.19  [partially]

**Claim.** RFC 6125 restricts a wildcard certificate to matching only the left-most label, so *.example.com matches foo.example.com but not bar.foo.example.com. An ops hostname at ops.<base> is covered by an existing *.<base> wildcard; ops.api.<base> would need a separate certificate.

**Limit or threshold asserted.** "If the wildcard character is the only character of the left-most label in the presented identifier, the client SHOULD NOT compare against anything but the left-most label of the reference identifier (e.g., *.example.com would match foo.example.com but not bar.foo.example.com or example.com)."

- Source: RFC 6125
- URL: <https://www.rfc-editor.org/rfc/rfc6125>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The quote is accurate but the document is obsolete. RFC 6125 was obsoleted by RFC 9525 (Nov 2023, 'Service Identity in TLS'), which restates the same left-most-label wildcard restriction. Cite RFC 9525 as the normative source in a 2026 design; the operational conclusion (ops.<base> covered by *.<base>, ops.api.<base> not) is unchanged and correct.

### A13.20  [yes]

**Claim.** In .NET 10 the OpenAPI document endpoint is gated behind a Development environment check in Microsoft's documented pattern, and the docs show Swagger UI similarly guarded. Default OpenAPI route is /openapi/{documentName}.json.

**Limit or threshold asserted.** "if (app.Environment.IsDevelopment()) { app.MapOpenApi(); }" Default route: "/openapi/{documentName}.json". Also shown: "app.UseSwaggerUI(); // UseSwaggerUI Protected by if (env.IsDevelopment())".

- Source: Microsoft Learn, Generate OpenAPI documents (ASP.NET Core 10.0)
- URL: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0>
- Accessed: 2026-08-31
- Confidence: verified

### A13.21  [yes]

**Claim.** ABP's exception handling defaults are SendExceptionsDetailsToClients = false and SendStackTraceToClients = true, and both must be true before a stack trace reaches a client. So ABP's default is not to leak stack traces, but the guard is a configuration pair rather than an environment gate.

**Limit or threshold asserted.** SendExceptionsDetailsToClients default false; SendStackTraceToClients default true; "If you want to send the stack trace to the client, you must set both SendStackTraceToClients and SendExceptionsDetailsToClients options to true otherwise, the stack trace will not be sent to the client."

- Source: ABP documentation, Exception Handling
- URL: <https://abp.io/docs/latest/framework/fundamentals/exception-handling>
- Accessed: 2026-08-31
- Confidence: verified

### A13.22  [no]

**Claim.** The AspNetCore.Diagnostics.HealthChecks UI registers at /healthchecks-ui with an API at /healthchecks-api and its README carries no security guidance about protecting that surface. Its latest stable NuGet release is 9.0.0, published 2024-12-19 - more than 12 months before the access date.

**Limit or threshold asserted.** UI path /healthchecks-ui, API /healthchecks-api. Latest stable AspNetCore.HealthChecks.UI = 9.0.0, published 2024-12-19 (queried api.nuget.org registration index).

- Source: Xabaril/AspNetCore.Diagnostics.HealthChecks README; NuGet registration API for AspNetCore.HealthChecks.UI
- URL: <https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks>
- Second source: <https://api.nuget.org/v3/registration5-gz-semver2/aspnetcore.healthchecks.ui/index.json>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The paths and version facts are correct (README: 'This automatically registers a new interface on /healthchecks-ui'; 'the API endpoint served at /healthchecks-api'; NuGet AspNetCore.HealthChecks.UI 9.0.0 published 2024-12-19T10:53:14Z, ~20 months before 2026-08-31). But the 'no security guidance' assertion is false: the README has a dedicated '## Protected HealthChecks.UI with OpenID Connect' section with a linked working sample. Restate as: the UI is unauthenticated by default and the README documents an opt-in OIDC protection pattern rather than shipping one.

### A13.23  [yes]

**Claim.** Hangfire is actively maintained: Hangfire.Core latest stable 1.8.25 published 2026-08-28. The system runs 1.8.21. Swashbuckle.AspNetCore latest stable 10.2.3 published 2026-06-22.

**Limit or threshold asserted.** hangfire.core 1.8.25 published 2026-08-28; swashbuckle.aspnetcore 10.2.3 published 2026-06-22.

- Source: NuGet registration API
- URL: <https://api.nuget.org/v3/registration5-gz-semver2/hangfire.core/index.json>
- Accessed: 2026-08-31
- Confidence: verified

### A13.24  [yes]

**Claim.** Hangfire's DashboardOptions sets DisplayStorageConnectionString = true by default, but SqlServerStorage.ToString() renders only "SQL Server: {server}@{database}" - it parses the connection string and emits the data source and catalogue only, not the password. The dashboard therefore leaks the SQL host and host-database name, not the sa credential.

**Limit or threshold asserted.** DisplayStorageConnectionString = true; ToString() builds from aliases "Data Source", "Server", "Address", "Addr", "Network Address" plus "Database"/"Initial Catalog" and returns $"SQL Server: {builder}".

- Source: Hangfire source: src/Hangfire.Core/DashboardOptions.cs and src/Hangfire.SqlServer/SqlServerStorage.cs
- URL: <https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.SqlServer/SqlServerStorage.cs>
- Accessed: 2026-08-31
- Confidence: verified

### A13.25  [yes]

**Claim.** OWASP WSTG's admin-interface test notes that administrative interfaces are commonly found on a different port than the main application, and recommends IP filtering and separation of duties as hardening.

**Limit or threshold asserted.** Discovery includes "different port on the host than the main application"; remediation includes "IP filtering or other controls" and "clear separation of duties between normal users and site administrators".

- Source: OWASP Web Security Testing Guide, WSTG Enumerate Infrastructure and Application Admin Interfaces
- URL: <https://owasp.org/www-project-web-security-testing-guide/latest/4-Web_Application_Security_Testing/02-Configuration_and_Deployment_Management_Testing/05-Enumerate_Infrastructure_and_Application_Admin_Interfaces>
- Accessed: 2026-08-31
- Confidence: verified

### A13.26  [yes]

**Claim.** OWASP ASVS 5.0 requires backend component authentication to use individual service accounts, short-term tokens or certificates rather than unchanging credentials or shared privileged accounts; requires least privilege for those accounts; and requires that they not be default credentials. This is the requirement basis for not authenticating to object storage as root.

**Limit or threshold asserted.** 13.2.1 (L2): "Authentication must use individual service accounts, short-term tokens, or certificate-based authentication and not unchanging credentials such as passwords, API keys, or shared accounts with privileged access." 13.2.2 (L2): least necessary privileges. 13.2.3 (L2): "the credential being used by the consumer is not a default credential (e.g., root/root or admin/admin)." 13.3.4 (L3): secrets expire and are rotated.

- Source: OWASP ASVS 5.0, V13.2 Backend Communication Configuration
- URL: <https://github.com/OWASP/ASVS/blob/master/5.0/en/0x22-V13-Configuration.md>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All four verified verbatim, including the second sentence of 13.2.1 that a first-pass reading of the table can miss.

### A13.27  [partially]

**Claim.** MinIO's root user has access to all actions and resources on the deployment; MinIO denies any action not explicitly granted; and scoped access keys (formerly service accounts) with additional inline policies are the documented mechanism for restricting an application's access. MinIO also documents an STS AssumeRole family for short-lived credentials.

**Limit or threshold asserted.** Root described as having "access to all actions and resources on the deployment"; "MinIO AIStor denies access to any action or resource that is not explicitly granted"; "create access keys (formerly known as service accounts)" and "assign an additional inline policy to an access key account to further restrict access". STS operations documented: AssumeRole, AssumeRoleWithCertificate, AssumeRoleWithClientGrants, AssumeRoleWithCustomToken, AssumeRoleWithLDAPIdentity, AssumeRoleWithWebIdentity. Marked partial because min.io now redirects its IAM documentation to the commercial AIStor product; I could not retrieve an equivalent community-edition page at the former URLs.

- Source: MinIO documentation, Identity and Access Management
- URL: <https://docs.min.io/enterprise/aistor-object-store/administration/iam/identity/>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Only the STS AssumeRole family is supported by the cited page. The root-privilege, implicit-deny, and scoped-access-key statements are not on it and remain unverified at this URL. The researcher's own 'partial' flag is correct and should be kept; either locate the specific AIStor access-management/policy pages that carry each sentence, or state these as widely-held behaviour without a citation. Note also that this is the commercial AIStor documentation while claim [28] describes the community server - do not blend the two as one product's documented behaviour.

### A13.28  [partially]

**Claim.** MinIO community server ships a web console. The README states the console runs on a random port unless --console-address is given, and the source shows the browser is enabled by default (globalBrowserEnabled = true) with a MINIO_BROWSER environment variable available to change it. Default deployment credentials are minioadmin:minioadmin.

**Limit or threshold asserted.** "MinIO runs console on random port by default, if you wish to choose a specific port use --console-address"; source: globalBrowserEnabled = true; MINIO_BROWSER validated in common-main.go; "--console-address cannot be same as --address"; default root credentials minioadmin:minioadmin.

- Source: minio/minio README and source (cmd/globals.go, cmd/common-main.go)
- URL: <https://raw.githubusercontent.com/minio/minio/master/README.md>
- Second source: <https://raw.githubusercontent.com/minio/minio/master/cmd/common-main.go>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: All facts are true but the README settles only two of them. The source facts are elsewhere and must be cited separately: cmd/globals.go line 192 'globalBrowserEnabled = true'; cmd/common-main.go line 706 'globalBrowserEnabled, err = config.ParseBool(env.Get(config.EnvBrowser, config.EnableOn))' with 'Invalid MINIO_BROWSER value in environment variable'; cmd/common-main.go line 484 '--console-address cannot be same as --address'. All independently confirmed.

### A13.29  [yes]

**Claim.** HIPAA Security Rule 45 CFR 164.312(a)(1) requires technical policies and procedures allowing access to systems maintaining ePHI only to persons or software programs granted access rights, and 164.308(a)(4) requires policies authorizing such access. An unauthenticated administrative surface exposing appointment identifiers and recipient addresses is inconsistent with these once real ePHI is present.

**Limit or threshold asserted.** 164.312(a)(1): "Implement technical policies and procedures for electronic information systems that maintain electronic protected health information to allow access only to those persons or software programs that have been granted access rights as specified in Sec. 164.308(a)(4)." 164.312(b) Audit controls: "Implement hardware, software, and/or procedural mechanisms that record and examine activity in information systems that contain or use electronic protected health information."

- Source: 45 CFR 164.312 and 45 CFR 164.308 (Cornell Legal Information Institute)
- URL: <https://www.law.cornell.edu/cfr/text/45/164.312>
- Second source: <https://www.law.cornell.edu/cfr/text/45/164.308>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Regulatory text verified verbatim (source note: 68 FR 8376, Feb 20 2003, as amended 78 FR 5694, Jan 25 2013). The claim's applicability sentence ('inconsistent with these once real ePHI is present') is legal interpretation, not regulatory text - label it as such.

### A13.30  [could-not-check]

**Claim.** CISA Binding Operational Directive 23-02 (Mitigating the Risk from Internet-Exposed Management Interfaces) requires federal agencies to remove networked management interfaces from the public internet or protect them behind a policy enforcement point. I could NOT verify its exact wording.

**Limit or threshold asserted.** n/a - not retrieved. I attempted the directive page, the alternate bod-23-02 URL, the 508c PDF, and a web archive mirror; cisa.gov returned HTTP 403 to every request (bot protection) and the archive host was unavailable to this tool. Do not cite this directive's wording without independent retrieval. The containment argument does not depend on it: NIST SC-2, SC-7(5), OWASP A02:2025 and ASVS 13.4.5/8.4.2 carry it independently.

- Source: CISA (attempted)
- URL: <https://www.cisa.gov/news-events/directives/binding-operational-directive-23-02>
- Accessed: 2026-08-31
- Confidence: unverified
- Verifier note: Independently confirms the researcher's own 'unverified' flag - I could not retrieve the page either. Do not cite this directive's wording, its scope, or its 14-day remediation timeline without a retrieval that actually succeeds. As the researcher notes, the containment argument stands on SC-2, SC-7(5), A02:2025 and ASVS 13.4.5/8.4.2 without it. Also note BOD 23-02 binds US federal civilian executive branch agencies only, so it would be persuasive rather than governing for a private-sector system.

### A13.31  [yes]

**Claim.** Identity-aware proxies are a current, maintained component class, and they carry their own vulnerability stream that an adopting team must track.

**Limit or threshold asserted.** Latest release v7.15.4 (2026-08-20); v7.15.3 (2026-06-09); v7.15.2 (2026-04-14), whose notes record "Critical security audits performed, resulting in fixes for multiple authentication bypasses and session fixation vulnerabilities".

- Source: oauth2-proxy releases (cited as evidence the component class is current and maintained, not as a product recommendation)
- URL: <https://github.com/oauth2-proxy/oauth2-proxy/releases>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Dates and substance confirmed. The stated LIMIT quote is a paraphrase, not verbatim - use the actual sentence. Worth carrying forward: v7.15.2 also introduced a --trusted-proxy-ip flag, which is direct evidence that forwarded-header trust must be configured explicitly in this component class too (reinforces claims [11] and [12]).

---

## Area: tenant-lifecycle

Verification verdict for this area: **material-errors** (45 claims checked)

### A14.1  [partially]

**Claim.** NIST SP 800-88 Rev. 1 'Guidelines for Media Sanitization' was WITHDRAWN on 26 September 2025 and is 'withdrawn and superseded in its entirety by NIST SP 800-88r2'. Any 2026 design citing r1 is citing an archived document provided 'solely for historical purposes'.

**Limit or threshold asserted.** Withdrawal Date: September 26, 2025; superseded by SP 800-88r2

- Source: NIST, withdrawal notice bound to SP 800-88r1 PDF
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r1.pdf>
- Second source: <https://csrc.nist.gov/pubs/sp/800/88/r2/final>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Withdrawal date (26 Sep 2025) and supersession by r2 are correct, but cite <https://csrc.nist.gov/pubs/sp/800/88/r1/final> (the only page carrying the withdrawal notice) and drop the two quoted phrases  -  neither 'in its entirety' nor 'solely for historical purposes' appears on NIST's page or in the PDF.

### A14.2  [yes]

**Claim.** NIST SP 800-88r2, 'Guidelines for Media Sanitization', Ramaswamy Chandramouli (NIST) and Eric A. Hibbard, published September 2025, 48 pages. This is the current revision.

**Limit or threshold asserted.** September 2025; DOI 10.6028/NIST.SP.800-88r2

- Source: NIST SP 800-88r2
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified

### A14.3  [yes]

**Claim.** Verbatim, SP 800-88r2 3.2.2: 'Sanitization using CE should not be trusted on ISM that have been backed up or escrowed unless the organization has a high level of confidence regarding how and where the keys were stored and managed outside of the ISM. Such backed up or escrowed copies of data, credentials, or keys should be subject to a separate ISM sanitization policy.' This is the authoritative statement of the backups-outlive-deletion problem for crypto-shredding.

**Limit or threshold asserted.** 3.2.2

- Source: NIST SP 800-88r2 3.2.2 Applicability of CE
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified

### A14.4  [yes]

**Claim.** Verbatim precondition for cryptographic erase, SP 800-88r2 3.2.2: 'As a pre-condition for using CE, ISO/IEC 27040 specifies that no sensitive data has previously been stored on the ISM in plaintext form (i.e., not encrypted) as CE can only sanitize keys related to encrypted data.' A footnote adds that for previously sanitized media the prohibition applies since the last sanitization. Consequence: data written before encryption is switched on can never be crypto-shredded.

**Limit or threshold asserted.** 3.2.2, precondition; footnote 10

- Source: NIST SP 800-88r2 3.2.2
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified

### A14.5  [yes]

**Claim.** SP 800-88r2 3.2.2 states 'For ISM consisting of virtual/logical storage, there may not be a purge sanitization technique alternative to CE.' The r2 change log confirms the scope change: 'The term information storage media (ISM) replaces electronic (i.e., "soft copy") media in the document to accommodate logical storage (e.g., cloud storage)'. This is why cryptographic erase is the applicable primitive for a hosted database and object store rather than an exotic one.

**Limit or threshold asserted.** 3.2.2; Appendix D

- Source: NIST SP 800-88r2 3.2.2 and Appendix D Change Log
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Minor: the change-log quotation is truncated mid-sentence without an ellipsis  -  the original continues 'and other types of media (e.g., DNA, ceramic, glass-based)'. Add the ellipsis.

### A14.6  [yes]

**Claim.** SP 800-88r2 also requires, via ISO/IEC 27040, that 'all copies of the target cryptographic keys must be able to be sanitized', recommends zeroization per ISO/IEC 19790 as the key sanitization technique, and warns that if the target key exists outside the media (for example injected from a key management server or escrow) 'there is a possibility that the key can be used in the future to recover encrypted data'.

**Limit or threshold asserted.** 3.2.3 Sanitization of Keys

- Source: NIST SP 800-88r2 3.2.3, 3.2.4
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified

### A14.7  [yes]

**Claim.** SP 800-88r2 3.2.5 lists ten items that documentation of a cryptographic-erase operation should record (media type, key generation, media encryption algorithm, key wrapping, areas addressed, key life-cycle management, key sanitization technique, key escrow or injection history, error-condition handling, interface clarity), and Appendix C specifies a Certificate of Sanitization recording sanitization method, technique, tool and version, verification method, and the identity, date and signature of the person performing it.

**Limit or threshold asserted.** 10 documentation items; Appendix C certificate fields

- Source: NIST SP 800-88r2 3.2.5 and Appendix C
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified

### A14.8  [yes]

**Claim.** SP 800-88r2 footnote 13 gives, as its example of selective sanitization, 'sanitizing the contents of a single file that is encrypted with a unique key using CE', and states that partial sanitization by CE is possible where the interface supports sanitizing only a subset of encryption keys. This is the standards basis for per-tenant keys as a deletion mechanism, though r2 also cautions that whole-media sanitization is preferred to partial sanitization wherever possible.

**Limit or threshold asserted.** footnote 13; 'sanitization of the whole ISM is preferred to partial ISM sanitization whenever possible'

- Source: NIST SP 800-88r2 4.3.6 / Sanitization Scope
- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified

### A14.9  [yes]

**Claim.** SP 800-88r2's change log states that apart from cryptographic erase, 'all sanitization technique and tool details have been replaced with recommendations to comply with IEEE 2883, NSA specifications, or an organizationally approved standard.' IEEE 2883-2022, 'IEEE Standard for Sanitizing Storage', published 17 August 2022, is current and specifies methods of sanitizing logical and physical storage.

**Limit or threshold asserted.** IEEE 2883-2022, published 2022-08-17

- Source: NIST SP 800-88r2 Appendix D; IEEE Standards Association
- URL: <https://standards.ieee.org/ieee/2883/10277/>
- Second source: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r2.pdf>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Add context, not a correction: IEEE 2883.1-2025 (Recommended Practice for Use of Storage Sanitization Methods) is now approved and P2883.2 (Virtualized and Cloud Storage Sanitization) is in development. Neither supersedes 2883-2022, but 2883.2 is the one that will matter for this design.

### A14.10  [yes]

**Claim.** 45 CFR 164.316(b)(2)(i) reads: 'Retain the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later.' Paragraph (b)(1) covers policies and procedures and written records of actions, activities and assessments that the Security Rule requires to be documented. The section does not mention audit logs, patient records, or business data.

**Limit or threshold asserted.** 6 years from creation or last effective date, whichever is later

- Source: 45 CFR 164.316, Cornell LII; cross-checked against GPO/govinfo CFR XML
- URL: <https://www.law.cornell.edu/cfr/text/45/164.316>
- Second source: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-316.xml>
- Accessed: 2026-08-31
- Confidence: verified

### A14.11  [yes]

**Claim.** The parallel Privacy Rule documentation retention requirement, 45 CFR 164.530(j)(2), is also six years from creation or last effective date. Like 164.316 it governs documentation, not the underlying health records; record-retention periods for the records themselves come from state law and contract, not from HIPAA.

**Limit or threshold asserted.** six years from creation or last effective date

- Source: 45 CFR 164.530(j), Cornell LII
- URL: <https://www.law.cornell.edu/cfr/text/45/164.530>
- Accessed: 2026-08-31
- Confidence: verified

### A14.12  [partially]

**Claim.** 45 CFR 164.504(e)(2)(ii)(J) requires a business associate contract to provide that at termination of the contract the business associate will 'return or destroy all protected health information received from, or created or received by the business associate on behalf of, the covered entity that the business associate still maintains in any form and retain no copies', and adds: 'if such return or destruction is not feasible, extend the protections of the contract to the information and limit further uses and disclosures to those purposes that make the return or destruction of the information infeasible.'

**Limit or threshold asserted.** (e)(2)(ii)(J); infeasibility carve-out

- Source: 45 CFR 164.504(e), Cornell LII
- URL: <https://www.law.cornell.edu/cfr/text/45/164.504>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Restore the omitted qualifier: the rule requires return or destruction 'if feasible'. The feasibility condition is inside the operative clause, not a separate carve-out. Quote the provision as a single conditional sentence.

### A14.13  [yes]

**Claim.** 45 CFR 164.502(e)(1)(ii) extends the same satisfactory-assurances requirement down the chain: a business associate may allow a subcontractor to create, receive, maintain or transmit PHI on its behalf only with written assurances meeting 164.504(e). 164.502(e)(2) requires this to be documented in a written contract.

**Limit or threshold asserted.** (e)(1)(ii) subcontractors; (e)(2) written documentation

- Source: 45 CFR 164.502(e), Cornell LII
- URL: <https://www.law.cornell.edu/cfr/text/45/164.502>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Precision note: (e)(1)(ii) cross-references 164.504(e)(1)(i), not 164.504(e) generally, and does not itself say 'written'  -  the writing requirement comes from (e)(2). The claim's substance holds.

### A14.14  [yes]

**Claim.** 45 CFR 164.310(d)(2)(i) requires 'policies and procedures to address the final disposition of electronic protected health information, and/or the hardware or electronic media on which it is stored', and (d)(2)(ii) requires 'procedures for removal of electronic protected health information from electronic media before the media are made available for re-use'. Both are Required, not Addressable.

**Limit or threshold asserted.** (d)(2)(i) and (d)(2)(ii) both Required

- Source: 45 CFR 164.310(d), Cornell LII
- URL: <https://www.law.cornell.edu/cfr/text/45/164.310>
- Accessed: 2026-08-31
- Confidence: verified

### A14.15  [partially]

**Claim.** HHS's disposal guidance cites 45 CFR 164.530(c) and 164.310(d)(2)(i)-(ii), directs entities to 'NIST SP 800-88, Guidelines for Media Sanitization' for handling sanitization of PHI throughout the information life cycle, and describes clearing, purging and destroying as the electronic-media methods.

**Limit or threshold asserted.** cites NIST SP 800-88 by number, not revision

- Source: HHS OCR FAQ 575, disposal of PHI
- URL: <https://www.hhs.gov/hipaa/for-professionals/faq/575/what-does-hipaa-require-of-covered-entities-when-they-dispose-information/index.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: The CFR citations and the clear/purge/destroy description are correct, but the stated threshold is wrong: the link text omits the revision while the hyperlink target is nistsp800-88-rev1.pdf, so HHS's disposal FAQ does point at Rev. 1 specifically. State it as: link text is revision-agnostic, hyperlink resolves to Rev. 1.

### A14.16  [yes]

**Claim.** HHS's guidance on rendering PHI unusable, unreadable or indecipherable specifies two methods: encryption (at rest, consistent with NIST SP 800-111) and destruction (electronic media 'consistent with NIST Special Publication 800-88, Guidelines for Media Sanitization such that the PHI cannot be retrieved'). It notes decryption tools should be stored separately from the data, and excludes redaction as a means of destruction.

**Limit or threshold asserted.** NIST SP 800-111 for at-rest encryption; NIST SP 800-88 for destruction

- Source: HHS, Guidance to Render Unsecured PHI Unusable, Unreadable, or Indecipherable
- URL: <https://www.hhs.gov/hipaa/for-professionals/breach-notification/guidance/index.html>
- Accessed: 2026-08-31
- Confidence: verified

### A14.17  [partially]

**Claim.** HHS's safe-harbour guidance points at 'NIST SP 800-88' by number without a revision, and the revision it was written against is now withdrawn. Complying with that pointer in 2026 therefore means complying with SP 800-88r2, whose technique-specific content has been replaced by a deferral to IEEE 2883. This is my inference from two verified facts, not a statement HHS has made.

- Source: Reasoning over HHS breach guidance and NIST SP 800-88r2 change log
- URL: <https://www.hhs.gov/hipaa/for-professionals/breach-notification/guidance/index.html>
- Second source: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r2.pdf>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Keep the 'partial / my inference' label and add the counter-evidence: HHS's other 800-88 pointer resolves specifically to Rev. 1, so HHS's own practice is not uniformly revision-agnostic. The safe framing is that HHS has not yet updated either pointer post-withdrawal, so a 2026 design should satisfy r2 (and through it IEEE 2883) while documenting that HHS has not said so.

### A14.18  [yes]

**Claim.** The HIPAA Security Rule NPRM issued 6 January 2025 to strengthen cybersecurity of ePHI remains a PROPOSED rule; no final rule has been issued. The HHS Security Rule page records a last review date of 19 March 2026 and identifies the 2013 Omnibus rule as the most recent finalised modification.

**Limit or threshold asserted.** NPRM 2025-01-06; no final rule as of page review 2026-03-19

- Source: HHS, HIPAA Security Rule page
- URL: <https://www.hhs.gov/hipaa/for-professionals/security/index.html>
- Accessed: 2026-08-31
- Confidence: verified

### A14.19  [yes]

**Claim.** 45 CFR 164.512(l) permits a covered entity to disclose PHI 'as authorized by and to the extent necessary to comply with laws relating to workers' compensation or other similar programs, established by law, that provide benefits for work-related injuries or illness without regard to fault.' Workers' compensation disclosure is its own permitted category, distinct from treatment, payment and health care operations.

**Limit or threshold asserted.** (l)

- Source: 45 CFR 164.512(l), Cornell LII
- URL: <https://www.law.cornell.edu/cfr/text/45/164.512>
- Accessed: 2026-08-31
- Confidence: verified

### A14.20  [yes]

**Claim.** 45 CFR 164.524(c)(2)(ii) requires that where PHI is maintained electronically in a designated record set and an individual requests an electronic copy, the covered entity must provide it 'in the electronic form and format requested by the individual, if it is readily producible in such form and format; or, if not, in a readable electronic form and format as agreed to'. This is the individual-access obligation the platform's export must be able to serve for its tenants.

**Limit or threshold asserted.** (c)(2)(ii) readily producible electronic form

- Source: 45 CFR 164.524(c), Cornell LII
- URL: <https://www.law.cornell.edu/cfr/text/45/164.524>
- Accessed: 2026-08-31
- Confidence: verified

### A14.21  [yes]

**Claim.** SQL Server transparent data encryption uses a per-database database encryption key (DEK) secured by a certificate in master. 'Backup files of databases that have TDE enabled, are also encrypted by using the DEK. As a result, when you restore from these backups, the certificate protecting the DEK must be available... Data loss occurs if the certificate is no longer available.' And: 'The certificate used to protect the DEK should never be dropped from the master database. Doing so causes the encrypted database to become inaccessible.' This is the mechanism by which per-tenant key destruction reaches backups.

**Limit or threshold asserted.** per-database DEK; backups encrypted with DEK; certificate required to restore

- Source: Microsoft Learn, Transparent data encryption (TDE)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/transparent-data-encryption>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Quotes verified. Note the concluding sentence ('This is the mechanism by which per-tenant key destruction reaches backups') is the author's inference, not doc text  -  the doc frames certificate loss as accidental data loss, not as a sanitization technique. Also relevant and unquoted: DROP DATABASE is disallowed during an in-progress encryption/decryption scan.

### A14.22  [yes]

**Claim.** 'The tempdb system database is encrypted if any other database on the SQL Server instance is encrypted by using TDE. This encryption might have a performance effect for unencrypted databases on the same SQL Server instance.' Enabling encryption for one tenant therefore has an instance-wide effect on every other tenant.

**Limit or threshold asserted.** tempdb encrypted instance-wide

- Source: Microsoft Learn, Transparent data encryption (TDE)
- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/transparent-data-encryption>
- Accessed: 2026-08-31
- Confidence: verified

### A14.23  [yes]

**Claim.** SQL Server 2022 edition matrix: Transparent data encryption is supported in Enterprise and Standard, not Web or Express. Encryption for backups is Enterprise and Standard only. Backup and restore to S3-compatible object storage over REST API is supported in Enterprise, Standard and Web. Developer edition 'includes all the functionality of Enterprise edition, but is licensed for use as a development and test system, not as a production server.' Maximum relational database size is 524 PB for Enterprise/Standard/Web and 10 GB for Express.

**Limit or threshold asserted.** TDE: Enterprise Yes, Standard Yes, Web No, Express No; Developer not licensed for production; Standard buffer pool 128 GB

- Source: Microsoft Learn, Editions and supported features of SQL Server 2022
- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Accessed: 2026-08-31
- Confidence: verified

### A14.24  [yes]

**Claim.** 'A dropped database can be re-created only by restoring a backup.' And: 'You can't drop a database currently being used. This means locks being held for reading or writing by any user. One way to remove users from the database is to use ALTER DATABASE to set the database to SINGLE_USER... you should execute the ALTER DATABASE and DROP DATABASE in the same batch.' Also, DROP DATABASE must run in autocommit mode and is not allowed in an explicit transaction.

**Limit or threshold asserted.** drop blocked by any open use; SINGLE_USER WITH ROLLBACK IMMEDIATE in same batch

- Source: Microsoft Learn, DROP DATABASE (Transact-SQL)
- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/drop-database-transact-sql>
- Accessed: 2026-08-31
- Confidence: verified

### A14.25  [partially]

**Claim.** In this system the three recurring jobs that run every fifteen minutes open a connection per office database inside an ICurrentTenant.Change scope, and ADO.NET connection pooling keeps the underlying session alive after the logical close. A DROP DATABASE issued while any such session is bound to that database will fail. Offboarding must therefore remove the office from job iteration and clear the pool before attempting a drop. This is my inference from the documented job behaviour combined with the DROP DATABASE constraint, not a sourced statement about this codebase.

**Limit or threshold asserted.** three jobs at 15-minute cadence across 11 offices today, 33 at target

- Source: Reasoning over stated system facts plus Microsoft Learn DROP DATABASE
- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/drop-database-transact-sql>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Split the claim. The DROP-blocked-by-open-use half is sourced here. The pooling behaviour needs its own citation (SqlConnection pooling: Close() returns the connection to the pool and the physical session persists; SqlConnection.ClearPool / ClearAllPools force eviction), and the job cadence and office counts are codebase facts that need a code or config reference, not a Microsoft Learn URL. Also add that the doc's 'regardless of its state: offline, read-only, suspect' remark plus the ALTER DATABASE ... SET OFFLINE path is the documented escape hatch when sessions cannot be cleared.

### A14.26  [yes]

**Claim.** ABP's TenantConfigurationProvider throws a BusinessException with code 010001 when a resolved tenant is not found and 010002 when the resolved tenant is not active, using the TenantNotFound and TenantNotActive localized messages. Deactivation therefore produces a hard, visible failure rather than a degraded experience.

**Limit or threshold asserted.** error codes 010001 (not found), 010002 (not active)

- Source: ABP framework source, TenantConfigurationProvider.cs
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/TenantConfigurationProvider.cs>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Quote the full error codes including the namespace prefix ('Volo.AbpIo.MultiTenancy:010001'/':010002'), since the bare numbers are not what appears in logs. Also note the file is fetched from the moving 'dev' branch  -  pin a tag or commit SHA so the citation stays reproducible.

### A14.27  [yes]

**Claim.** ABP's open-source TenantAppService.DeleteAsync finds the tenant and calls TenantRepository.DeleteAsync  -  nothing more. It does not drop, detach, archive or otherwise touch the tenant's database, and it does not remove tenant connection strings, which are managed by separate methods. The file carries a TODO about handling database creation. The equivalent behaviour of the ABP Commercial SaaS module could not be verified because that source is not public.

**Limit or threshold asserted.** deletes the tenant entity only

- Source: ABP framework source, TenantAppService.cs (open-source Tenant Management module)
- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/modules/tenant-management/src/Volo.Abp.TenantManagement.Application/Volo/Abp/TenantManagement/TenantAppService.cs>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Verified, and one detail strengthens it: CreateAsync publishes a TenantCreatedEto on the distributed event bus but DeleteAsync publishes no corresponding deleted event, so nothing downstream is notified of a tenant deletion. Pin the citation to a commit SHA rather than 'dev'.

### A14.28  [yes]

**Claim.** ABP's SaaS module models tenant activation as three states  -  Active (no deadline), ActiveWithLimitedTime (active until ActivationEndDate, then inactive) and Passive  -  which gives suspension a first-class, already-built representation including a time-boxed variant suitable for a payment grace period.

**Limit or threshold asserted.** Active / ActiveWithLimitedTime / Passive

- Source: ABP documentation, SaaS module
- URL: <https://abp.io/docs/latest/modules/saas>
- Accessed: 2026-08-31
- Confidence: verified

### A14.29  [partially]

**Claim.** ABP's distributed cache default GlobalCacheEntryOptions uses a sliding expiration of 20 minutes, and ABP automatically adds the current tenant id to cache keys. ABP's TenantStore caches tenant configuration in IDistributedCache<TenantConfigurationCacheItem>; the visible source contains no invalidation logic, so cache invalidation on suspension must be verified rather than assumed.

**Limit or threshold asserted.** 20-minute default sliding expiration

- Source: ABP documentation, Caching; ABP source TenantStore.cs
- URL: <https://abp.io/docs/latest/framework/fundamentals/caching>
- Second source: <https://raw.githubusercontent.com/abpframework/abp/dev/modules/tenant-management/src/Volo.Abp.TenantManagement.Domain/Volo/Abp/TenantManagement/TenantStore.cs>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: The 20-minute default and tenant-scoped keys are verified here. The TenantStore/TenantConfigurationCacheItem assertion needs its own citation (the TenantStore source file, pinned to a SHA), and the page also notes the tenant-key behaviour can be turned off with IgnoreMultiTenancy  -  worth stating, since it is the failure mode where a suspended tenant's entry is not tenant-scoped at all.

### A14.30  [yes]

**Claim.** ABP's blob storing abstraction implements multi-tenancy at the PROVIDER level, not through a standardised key naming convention: 'All the providers implement multi-tenancy as a standard feature. They isolate BLOBs of different tenants from each other', and the same BLOB name may be reused across tenants. Multi-tenancy can also be disabled for a specific container so its blobs are available to all tenants. Consequently, 'delete the tenants/{id}/ prefix' is a provider-specific assumption that must be verified per container rather than taken as guaranteed.

**Limit or threshold asserted.** per-container multi-tenancy can be disabled

- Source: ABP documentation, BLOB Storing
- URL: <https://abp.io/docs/latest/framework/infrastructure/blob-storing>
- Accessed: 2026-08-31
- Confidence: verified

### A14.31  [yes]

**Claim.** In a versioned bucket a delete does not remove data: 'Performing a DELETE operation on a versioned object creates a 0-byte DeleteMarker as the latest version of that object... The delete marker hides the object. Nothing is actually removed.' Permanent removal requires deleting by version id, or mc rm --versions. The same semantics are specified by the S3 API this store implements.

**Limit or threshold asserted.** delete without version id = delete marker, data retained

- Source: MinIO documentation, Object Versioning; AWS S3 DeleteObjects API reference
- URL: <https://docs.min.io/community/minio-object-store/administration/object-management/object-versioning.html>
- Second source: <https://docs.aws.amazon.com/AmazonS3/latest/API/API_DeleteObjects.html>
- Accessed: 2026-08-31
- Confidence: verified

### A14.32  [yes]

**Claim.** The S3 multi-object delete request 'can contain a list of up to 1,000 keys that you want to delete', and returns a per-key success or failure result. A prefix deletion over more than 1,000 objects is therefore inherently multi-request and partially-failable, and per-key errors are reported individually rather than failing the request.

**Limit or threshold asserted.** maximum 1,000 keys per DeleteObjects request

- Source: AWS S3 API Reference, DeleteObjects
- URL: <https://docs.aws.amazon.com/AmazonS3/latest/API/API_DeleteObjects.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Add one operational caveat the claim omits: quiet mode returns only errors, so a verification design that counts Deleted elements must force verbose mode (the default) or it will see nothing on success.

### A14.33  [partially]

**Claim.** Object locking in COMPLIANCE mode means 'no MinIO AIStor user can modify the object or its settings, including the MinIO AIStor root user', and legal hold protects objects indefinitely until a privileged user lifts it. Versioning is a prerequisite for object locking. A WORM lock adopted to satisfy retention can therefore make a contractual destruction obligation impossible to perform.

**Limit or threshold asserted.** COMPLIANCE mode: not modifiable even by root until retention expires

- Source: MinIO documentation, Object Retention / Object Locking
- URL: <https://docs.min.io/community/minio-object-store/administration/object-management/object-retention.html>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Substance confirmed, but the sentence presented in quotation marks is not the page's wording. Replace with the actual text: 'An object under COMPLIANCE lock is protected from write operations by all users, including the MinIO AIStor root user.'

### A14.34  [yes]

**Claim.** MinIO lifecycle rules support prefix scoping, expiry of noncurrent versions (--noncurrent-expire-days) and expiry of delete markers (--expire-delete-marker), but the scanner is a low-priority background process, so 'the scanner may therefore not detect an object as eligible for a configured transition or expiration lifecycle rule until after the lifecycle rule period has passed.' Lifecycle expiry is therefore eventual and unsuitable as a deletion guarantee with a deadline.

**Limit or threshold asserted.** expiry timing not guaranteed at the rule boundary

- Source: MinIO documentation, Object Lifecycle Management
- URL: <https://docs.min.io/community/minio-object-store/administration/object-management/object-lifecycle-management.html>
- Accessed: 2026-08-31
- Confidence: verified

### A14.35  [yes]

**Claim.** Microsoft's multitenancy guidance treats offboarding as a distinct lifecycle stage with three named considerations  -  retention period ('Determine how long to maintain the customer data. Identify any legal requirements that mandate data destruction after a specific period'), reonboarding ('Decide whether to support reonboarding. Clarify whether the tenant's data remains available during the retention period') and rebalancing  -  and separates it from deactivation, which 'differs from offboarding because it's intended to be a temporary state.'

**Limit or threshold asserted.** article date 2025-06-13, page updated 2025-10-31

- Source: Microsoft Azure Architecture Center, Tenant life cycle considerations in multitenant solutions
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/tenant-life-cycle>
- Accessed: 2026-08-31
- Confidence: verified

### A14.36  [yes]

**Claim.** The recognised SaaS structural pattern separates a control plane  -  'all the functionality and services that are used to onboard, authenticate, manage, operate, and analyze a multi-tenant environment'  -  from the application plane, and notes 'the control plane and its services are not actually multi-tenant... These services are global to all tenants.' The same source explicitly flags provisioning as contested: it places provisioning in the application plane while acknowledging 'Some could argue that this belongs in the control plane.' Note this AWS whitepaper is banner-marked 'for historical reference only'.

**Limit or threshold asserted.** marked historical reference only

- Source: AWS Whitepaper, SaaS Architecture Fundamentals  -  Control plane vs. application plane
- URL: <https://docs.aws.amazon.com/whitepapers/latest/saas-architecture-fundamentals/control-plane-vs.-application-plane.html>
- Accessed: 2026-08-31
- Confidence: partial
- Verifier note: Fully supported  -  the researcher's 'partial' confidence is more conservative than the evidence requires. Keep the historical-reference caveat prominent, since a design leaning on this taxonomy in 2026 is leaning on a deprecated document.

### A14.37  [yes]

**Claim.** Starting with EF Core 9, Migrate and MigrateAsync automatically acquire a database-wide lock before applying migrations, and calling them throws when the model has pending changes compared to the last migration (RelationalEventId.PendingModelChangesWarning), with 'dotnet ef migrations has-pending-model-changes' available to detect the condition in CI. EF guidance also says to run migrations as a one-shot deployment job and not to 'make every application replica run migrations from its entrypoint.'

**Limit or threshold asserted.** EF Core 9+; database-wide migration lock; has-pending-model-changes command

- Source: Microsoft Learn, Applying Migrations (EF Core)
- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Two limitations worth carrying into the design, both on the same page: 'Wrapping MigrateAsync in an explicit transaction is not supported', and the lock mechanism is provider-specific and can be left abandoned if the process dies (documented for SQLite).

### A14.38  [yes]

**Claim.** MITRE ATT&CK T1584.001 documents dangling-DNS subdomain takeover: 'Subdomain hijacking can occur when organizations have DNS entries that point to non-existent or deprovisioned resources. In such cases, an adversary may take control of a subdomain to conduct operations with the benefit of the trust associated with that domain.' The only listed mitigation is pre-compromise, and detection is noted as difficult because the activity occurs outside the target's visibility.

**Limit or threshold asserted.** no effective post-hoc mitigation listed

- Source: MITRE ATT&CK T1584.001, Compromise Infrastructure: Domains
- URL: <https://attack.mitre.org/techniques/T1584/001/>
- Accessed: 2026-08-31
- Confidence: verified

### A14.39  [yes]

**Claim.** Certificate Transparency logs are public and append-only: a log is 'a single, append-only Merkle Tree of submitted certificate and precertificate entries', the system exists so that 'anyone' can audit CA activity, and log operators 'SHOULD NOT impose any conditions on retrieving or sharing data from the log'. A certificate naming a customer's subdomain is therefore permanently and publicly disclosed and cannot be withdrawn at offboarding.

**Limit or threshold asserted.** append-only; entries cannot be removed

- Source: RFC 9162, Certificate Transparency Version 2.0
- URL: <https://www.rfc-editor.org/rfc/rfc9162.html>
- Accessed: 2026-08-31
- Confidence: verified

### A14.40  [yes]

**Claim.** California Civil Code 56.06 deems, among others, 'any business that offers software or hardware to consumers, including a mobile application or other related device that is designed to maintain medical information' and businesses 'organized for the purpose of maintaining medical information' to be providers of health care subject to the Confidentiality of Medical Information Act. This is a California-specific overlay independent of HIPAA. Whether a business-to-business IME scheduling platform falls within it is a legal question requiring counsel, not an engineering determination.

**Limit or threshold asserted.** deemed 'provider of health care' subject to this part only

- Source: California Civil Code 56.06 (Confidentiality of Medical Information Act)
- URL: <https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=CIV&sectionNum=56.06>
- Accessed: 2026-08-31
- Confidence: verified

### A14.41  [yes]

**Claim.** Federal Rule of Civil Procedure 37(e) applies where electronically stored information 'should have been preserved in the anticipation or conduct of litigation' and is lost because a party failed to take reasonable steps to preserve it; on a finding of prejudice a court may order measures no greater than necessary to cure it, and only on a finding of 'intent to deprive another party of the information's use in the litigation' may it presume the information unfavourable, so instruct a jury, or dismiss or enter default judgment. A scheduled deletion that runs over data under a preservation duty is the failure mode this rule addresses.

**Limit or threshold asserted.** 37(e)(2) requires intent for the severest sanctions

- Source: Federal Rules of Civil Procedure, Rule 37(e), Cornell LII
- URL: <https://www.law.cornell.edu/rules/frcp/rule_37>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: One condition the claim drops: 37(e) applies only where the lost ESI 'cannot be restored or replaced through additional discovery'. That threshold matters here, because a verified restorable export is precisely what would take a deletion outside the rule.

### A14.42  [yes]

**Claim.** The information-blocking regulations apply to 'actors', defined at 45 CFR 171.102 as a health care provider, a health IT developer of certified health IT, or a health information network / health information exchange  -  the last being an entity that determines, controls or has discretion to administer any requirement, policy or agreement enabling electronic health information exchange among multiple unaffiliated parties for treatment, payment or health care operations. Whether this platform is an actor is a legal question; on its face it is not certified health IT, and workers' compensation disclosure is a separate permitted category from treatment, payment and operations.

**Limit or threshold asserted.** three actor categories

- Source: 45 CFR 171.102, Cornell LII
- URL: <https://www.law.cornell.edu/cfr/text/45/171.102>
- Accessed: 2026-08-31
- Confidence: verified

### A14.43  [yes]

**Claim.** The certified-health-IT EHI export criterion at 45 CFR 170.315(b)(10) requires a user to be able to create an export file with all of a single patient's electronic health information, and a population-level export of all EHI in the system, in an electronic and computable format, executable by identified users or administrators without developer assistance and at a time of the user's choosing, with publicly accessible up-to-date documentation of the export format's structure and syntax. This is a useful template for a defensible export even where the criterion does not legally bind.

**Limit or threshold asserted.** single-patient and population export; computable format; published documentation

- Source: ASTP/ONC HealthIT.gov, Electronic Health Information Export test method
- URL: <https://www.healthit.gov/test-method/electronic-health-information-export>
- Accessed: 2026-08-31
- Confidence: verified

### A14.44  [partially]

**Claim.** The recognised bulk health-data export pattern, FHIR Bulk Data Access (Flat FHIR) v3.0.0 STU 3 on FHIR R4, mandates newline-delimited JSON  -  'The server SHALL support Newline Delimited JSON, but MAY choose to support additional output formats'  -  and an asynchronous kick-off/poll pattern returning 202 Accepted with a Content-Location polling URL, then a completion manifest of file URLs. The file format and the async manifest pattern are worth borrowing even where the FHIR resource model is not.

**Limit or threshold asserted.** v3.0.0 STU 3; NDJSON SHALL; 202 + Content-Location + manifest

- Source: HL7 FHIR Bulk Data Access Implementation Guide
- URL: <https://hl7.org/fhir/uv/bulkdata/STU2/export.html>
- Second source: <https://hl7.org/fhir/uv/bulkdata/>
- Accessed: 2026-08-31
- Confidence: verified
- Verifier note: Either change the version to v2.0.0 STU 2 to match the cited page, or keep v3.0.0 STU 3 and cite <https://hl7.org/fhir/uv/bulkdata/export.html>. Do not pair the STU 2 URL with an STU 3 version number, and re-check the NDJSON and 202 wording against STU 3 before quoting it as STU 3 text.

### A14.45  [no]

**Claim.** In this system the audit data lives inside the tenant database  -  16 appointments produced roughly 1,450 audit-log rows and 2,689 entity property-change rows in one office database  -  so the record of who accessed a departed office's PHI is physically inside the object that offboarding destroys. Any export or destruction design must treat the audit tables as first-class export content, not as system noise. This is my inference from the stated measurement, not an external source.

**Limit or threshold asserted.** ~100:1 audit rows to business rows measured

- Source: Reasoning over stated system measurements
- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/tenant-life-cycle>
- Accessed: 2026-08-31
- Confidence: REASONING
- Verifier note: Remove this URL  -  it supports nothing in the claim and its presence implies external corroboration that does not exist. The measurement is a codebase observation and should cite the query or migration that produced it (table names and the counting query), with the ~100:1 figure stated as a single-office sample rather than a platform constant. The design conclusion (audit and entity-change tables are export content, not noise) stands on its own and does not need an external citation.

---

## Question evidence

### Q1. Is database-per-tenant defensible at 33 offices, and what is the actual breaking point? Name the constraint that binds first among connection count, backup window, migration fan-out, per-database platform limits, and operational attention  -  with real numbers and cited limits

Confidence: **determinate-with-caveats**

**Q1.1** Maximum databases per SQL Server instance is 32,767, and maximum user connections is 32,767. At N=33 offices this is roughly 1,000x headroom, so per-database platform limits cannot be the binding constraint.

> | Databases per instance of SQL Server | 32,767 | ... | User connections | 32,767 |

- URL: <https://learn.microsoft.com/en-us/sql/sql-server/maximum-capacity-specifications-for-sql-server?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q1.2** ADO.NET connection pools are keyed on the exact connection string, so N tenant databases produce N distinct pools. Microsoft names this exact architecture as a fragmentation problem  -  and the documented mitigation (connect to one database, then T-SQL USE) is unavailable to this system, which has no raw SQL anywhere in src.

> Each connection pool is associated with a distinct connection string. When a new connection is opened, if the connection string is not an exact match to an existing pool, a new pool is created. ... Pool fragmentation due to many databases ... there is a separate pool of connections to each database, which increase the number of connections to the server. ... Instead of connecting to a separate database for each user or group, connect to the same database on the server and then execute the Transact-SQL USE statement to change to the desired database.

- URL: <https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-connection-pooling>
- Accessed: 2026-08-31

**Q1.3** Default Max Pool Size is 100 per pool and pool exhaustion queues then throws after the connect timeout  -  a loud, obvious failure rather than a silent degradation, which suits this team. Client-side ceiling is therefore (N+1)x100: 1,200 at N=11, 3,400 at N=33, meeting 32,767 only at N=326.

> Connections are added to the pool as needed, up to the maximum pool size specified (100 is the default). ... If the maximum pool size has been reached and no usable connection is available, the request is queued. The pooler then tries to reclaim any connections until the time-out is reached (the default is 15 seconds). If the pooler cannot satisfy the request before the connection times out, an exception is thrown.

- URL: <https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-connection-pooling>
- Accessed: 2026-08-31

**Q1.4** Cross-reference confirming the pooling defaults from a second first-party source: Max Pool Size 100, Min Pool Size 0, Connect Timeout 15, Pooling true.

> | Max Pool Size | 100 | The maximum number of connections that are allowed in the pool. | ... | Min Pool Size | 0 | ... | Connect Timeout -or- Connection Timeout -or- Timeout | 15 |

- URL: <https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-standard-5.2>
- Accessed: 2026-08-31

**Q1.5** Pooled connections are evicted after roughly 4-8 minutes idle. Since the three recurring sweeps run every 15 minutes (>8 min), essentially every sweep opens fresh physical connections to every office  -  3 x N x 4 per hour = 396/hour at N=33, or 0.11 logins/second. Negligible.

> The connection pooler removes a connection from the pool after it has been idle for approximately 4-8 minutes, or if the pooler detects that the connection with the server has been severed.

- URL: <https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-connection-pooling>
- Accessed: 2026-08-31

**Q1.6** On the 4 vCPU VM, SQL Server auto-configures 512 max worker threads. Critically, a worker is assigned only to an ACTIVE request, not to an idle connection  -  so the sequential per-office sweeps consume 3 workers regardless of N, a 170x headroom that does not shrink as N grows.

> The default value for max worker threads is 0. This enables SQL Server to automatically configure the number of worker threads at startup. ... | <= 4 | 256 | 512 | 512 | ... A worker thread is assigned only to active requests and is released once the request is serviced. This happens even if the user session/connection on which the request was made remains open.

- URL: <https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-the-max-worker-threads-server-configuration-option?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q1.7** SQL Server dynamically self-configures user connections up to 32,767 by default, so no server-side connection cap will be hit before the client-side pool ceiling at N=326.

> SQL Server allows a maximum of 32,767 user connections. Because user connections is a dynamic (self-configuring) option, SQL Server adjusts the maximum number of user connections automatically as needed, up to the maximum value allowable. ... The default is 0, which means that the maximum (32,767) user connections are allowed.

- URL: <https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-the-user-connections-server-configuration-option?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q1.8** MSSQL_PID=Developer is a hard blocker that binds TODAY at N=1, independent of N. Standard is the architectural floor; its 128 GB buffer pool cap is 18x the 7,168 MB currently allocated and is not binding at 33. Express is architecturally EXCLUDED  -  not by its 10 GB per-database cap but by its 1,410 MB buffer pool per INSTANCE (42 MB per office at N=33).

> SQL Server Developer edition lets developers build any kind of application on top of SQL Server. It includes all the functionality of Enterprise edition, but is licensed for use as a development and test system, not as a production server. ... | Maximum memory for buffer pool per instance of SQL Server Database Engine | Operating system maximum | 128 GB | 64 GB | 1,410 MB | 1,410 MB | ... | Maximum relational database size | 524 PB | 524 PB | 524 PB | 10 GB | 10 GB |

- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022?view=sql-server-ver16>
- Accessed: 2026-08-31

**Q1.9** A second, independent reason Express is excluded: SQL Server 2022's native off-box backup path to S3-compatible object storage is unsupported on Express. This is directly relevant because the system already runs MinIO (S3-compatible), so BACKUP TO URL s3:// is an available capability requirement for getting backups off the single disk without new tooling.

> Back up to S3-compatible object storage isn't supported in SQL Server Express and SQL Server Express with Advanced Services editions. ... A single backup file can be up to 200,000 MiB per URL (with MAXTRANSFERSIZE set to 20 MB). Backups can be striped across a maximum of 64 URLs. ... Compression is supported and recommended. ... Encryption is supported.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/sql-server-backup-to-url-s3-compatible-object-storage?view=sql-server-ver16>
- Accessed: 2026-08-31

**Q1.10** Microsoft's multitenant guidance identifies schema deployment across a fleet and per-tenant maintenance as the named operational costs of dedicated-database-per-tenant, and recommends idempotent, automated, version-tracked schema deployment  -  precisely the artifact this system lacks. It also warns against exactly the failure mode the migrator produces.

> Schema updates: If you use a database that enforces a schema, plan how to deploy schema updates across your estate. Consider how your application knows which schema version to use for a specific tenant's database queries. ... Manual schema changes. Avoid updating your database schema manually... It's easy to lose track of the updates that you apply, and if you need to scale out to more databases, it's challenging to identify the correct schema to apply. Instead, build tooling or an automated pipeline to deploy your schema changes, and use it consistently. Track the schema version that you use for each tenant in a dedicated database or lookup table. ... It's important to use automated deployment approaches when you provision databases for each tenant. Otherwise, the complexity of manually deploying and managing the databases becomes overwhelming.

- URL: <https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/storage-data>
- Accessed: 2026-08-31

**Q1.11** EF Core's own guidance both names idempotent scripts as the answer to fleets at differing migration states, and explicitly warns against the runtime/entrypoint migration pattern. Migration bundles are the recommended automated-deployment artifact and would give the per-office pass/fail exit code the current loop lacks.

> EF Core also supports generating idempotent scripts, which internally check which migrations have already been applied (via the migrations history table), and only apply missing ones. This is useful if you don't exactly know what the last migration applied to the database was, or if you are deploying to multiple databases that may each be at a different migration. ... Generate the bundle during the build and run it as a one-shot deployment job after the database is healthy. Don't install the SDK or run dotnet ef in the application image, and don't make every application replica run migrations from its entrypoint. ... Carefully consider before using this approach in production. Prefer a migration bundle for automation or a SQL script when review and approval are required.

- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Accessed: 2026-08-31

**Q1.12** CORRECTION TO A BRIEF PREMISE: QueuePollInterval = TimeSpan.Zero is Hangfire's own documented recommendation when SlidingInvisibilityTimeout is set, not aggressive misconfiguration. It is also O(1) in N  -  Hangfire storage is the single host database, so background-job polling load does not grow with office count at all.

> One of the main disadvantage of raw SQL Server job storage implementation - it uses the polling technique to fetch new jobs. [TimeSpan.Zero is] the recommended value in that version, but you can decrease the polling interval if your background jobs can tolerate additional delay. [1.7 recommendations include] DisableGlobalLocks = true, UseRecommendedIsolationLevel = true, SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5)

- URL: <https://docs.hangfire.io/en/latest/configuration/using-sql-server.html>
- Accessed: 2026-08-31

**Q1.13** Hangfire.SqlServer is actively maintained and current: 1.8.25 was published 2026-08-28, three days before this analysis, with a steady cadence (1.8.22 Nov 2025, 1.8.23 Feb 2026, 1.8.24 Jul 2026). The system runs 1.8.21 (published 2025-08-12), which is four releases and about twelve months behind. The dependency is safe to keep; it just needs updating.

> 1.8.21 2025-08-12 | 1.8.22 2025-11-07 | 1.8.23 2026-02-05 | 1.8.24 2026-07-16 | 1.8.25 2026-08-28

- URL: <https://api.nuget.org/v3/registration5-gz-semver2/hangfire.sqlserver/index.json>
- Accessed: 2026-08-31

**Q1.14** Per-database on-disk floor: new databases inherit model's 8 MB data + 8 MB log and autogrow in 64 MB increments. So the fixed storage cost of an empty office is ~16 MB and growth is chunky  -  33 offices cost ~528 MB before any data. This is trivial and confirms per-database storage overhead is NOT a constraint; audit data volume is.

> The default initial size of the model database data and log file is 8 MB. ... | Primary data | modeldev | model.mdf | Autogrow by 64 MB until the disk is full. | | Log | modellog | modellog.ldf | Autogrow by 64 MB to a maximum of 2 terabytes. |

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/databases/model-database?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q1.15** CORRECTION TO A BRIEF PREMISE  -  the HIPAA citation does not say what the brief says it says. 45 CFR 164.316(b)(2)(i) requires six-year retention of the DOCUMENTATION of policies and procedures required by 164.316(b)(1)  -  not of audit logs. The audit standard at 164.312(b) requires mechanisms to record and examine ePHI activity but specifies NO retention period. A six-year audit retention target is a defensible operating assumption (state litigation holds, California workers' comp record rules, contractual terms), but it is NOT established by this CFR cite, and the audit-sizing case should not rest on it.

> Retain the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later. [164.316(b)(1):] Maintain the policies and procedures implemented to comply with this subpart in written (which may be electronic) form. [164.312(b) Audit controls:] Implement hardware, software, and/or procedural mechanisms to record and examine activity in information systems that contain or use electronic protected health information.

- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-316.xml>
- Accessed: 2026-08-31

**Q1.16** SECONDARY CORRECTION affecting the tenancy-resolution caveat: ABP registers FIVE default tenant resolve contributors, not four, and CurrentUserTenantResolveContributor runs FIRST  -  ahead of the custom HostAwareDomainTenantResolveContributor. This means a user's tenant claim, not the Host header, wins resolution for an authenticated request. The brief's 'Host header only' is therefore even less safe as an assumption than recorded. Out of scope for Q1's ranking but it bears on any control that assumes Host-header-only tenancy.

> CurrentUserTenantResolveContributor - Gets the tenant id from claims of the current user, if the current user has logged in. This should always be the first contributor for the security. [then] QueryStringTenantResolveContributor ... RouteTenantResolveContributor ... HeaderTenantResolveContributor ... CookieTenantResolveContributor

- URL: <https://abp.io/docs/latest/framework/architecture/multi-tenancy>
- Accessed: 2026-08-31

---

### Q2. Should audit data be separated from operational data in this ABP/SQL Server database-per-tenant IME platform, and if so how? Critically: what does the six-year obligation in 45 CFR 164.316(b)(2)(i) actually attach to?

Confidence: **determinate-with-caveats**

**Q2.1** The six-year retention in 164.316(b)(2)(i) attaches to 'the documentation required by paragraph (b)(1)'  -  i.e. written policies and procedures, and written records of actions/activities/assessments the Security Rule requires to be documented. It does not attach to system-generated audit logs.

> (b)(2)(i) Time limit (Required): 'Retain the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later.' (b)(1): '(i) Maintain the policies and procedures implemented to comply with this subpart in written (which may be electronic) form; (ii) If an action, activity or assessment is required by this subpart to be documented, maintain a written (which may be electronic) record of the action, activity, or assessment.'

- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-316.xml>
- Accessed: 2026-08-31

**Q2.2** 45 CFR 164.312(b) Audit Controls requires implementing recording/examining mechanisms and specifies NO retention period for audit logs anywhere in the section.

> Section (b) - Audit Controls: 'Implement hardware, software, and/or procedural mechanisms that record and examine activity in information systems that contain or use electronic protected health information.' ... Retention Period Question: No. The regulation contains no paragraph specifying a retention period for audit logs.

- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-312.xml>
- Accessed: 2026-08-31

**Q2.3** NIST SP 800-66r2 (February 2024) places the six-year specification under the Documentation standard (5.5.2), restating that it covers 'documentation required by paragraph (b)(1)'  -  not logs.

> '2. Retain Documentation for at Least Six Years  -  Implementation Specification (Required)  -  Retain documentation required by paragraph (b)(1) of this section for six years from the date of its creation or the date when it last was in effect, whichever is later.'

- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-66r2.pdf>
- Accessed: 2026-08-31

**Q2.4** NIST SP 800-66r2's Audit Controls section (5.3.2) names no retention period, makes audit scope risk-based rather than exhaustive, and explicitly raises physically separating the audit store as a design question  -  direct support for the recommendation.

> 'Determine the appropriate scope of audit controls that will be necessary in information systems that contain or use ePHI based on the regulated entity's risk assessment and other organizational factors.' ... Sample question: 'Where will audit information reside (e.g., separate server)?'

- URL: <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-66r2.pdf>
- Accessed: 2026-08-31

**Q2.5** The pending 2025 HIPAA Security Rule NPRM would require log retention that is risk-based and differentiated per log type  -  not six years. This confirms the current rule imposes no six-year log retention. As of 2026-08-31 it remains a Proposed Rule (RIN 0945-AA22, published 2025-01-06, comments closed 2025-03-07); no final rule amending the Security Rule appears in the Federal Register.

> 'A proposed implementation specification for record retention at proposed 45 CFR 164.308(a)(7)(ii)(D) would require a regulated entity to retain records of activity in its relevant electronic information systems... the regulated entity would be required to retain such records for an amount of time that is reasonable and appropriate for the specific type of report or log. For example, it may be reasonable and appropriate to retain audit trails for a different amount of time than security incident tracking reports...' and proposed 164.312(d)(2)(iii) Retain: '...retain records of all activity in its relevant electronic information systems as determined by the covered entity's or business associate's policies and procedures for information system activity review at Sec. 164.308(a)(7)(ii)(A).'

- URL: <https://www.federalregister.gov/documents/2025/01/06/2024-30983/hipaa-security-rule-to-strengthen-the-cybersecurity-of-electronic-protected-health-information>
- Accessed: 2026-08-31

**Q2.6** THE REAL six-year data obligation for this business: 164.528 accounting of disclosures. The exclusion list (i)-(ix) does NOT exclude 164.512(l) workers' compensation disclosures, which are this platform's core activity. So workers'-comp disclosures are accountable with a six-year lookback.

> 'An individual has a right to receive an accounting of disclosures of protected health information made by a covered entity in the six years prior to the date on which the accounting is requested, except for disclosures: (i) To carry out treatment, payment and health care operations...; (ii) To individuals...; (iii) Incident to...; (iv) Pursuant to an authorization...; (v) For the facility's directory...; (vi) For national security...; (vii) To correctional institutions...; (viii) As part of a limited data set...; or (ix) That occurred prior to the compliance date.'

- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-528.xml>
- Accessed: 2026-08-31

**Q2.7** 45 CFR 164.512(l) is the workers' compensation disclosure permission  -  confirming this platform's disclosures fall under it and are therefore accountable under 164.528.

> '(l) Standard: Disclosures for workers' compensation. A covered entity may disclose protected health information as authorized by and to the extent necessary to comply with laws relating to workers' compensation or other similar programs, established by law, that provide benefits for work-related injuries or illness without regard to fault.'

- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-512.xml>
- Accessed: 2026-08-31

**Q2.8** The Privacy Rule mirrors the Security Rule's documentation-retention structure exactly, corroborating that this drafting pattern means documentation-about-compliance, not system data.

> 164.530(j)(2): 'A covered entity must retain the documentation required by paragraph (j)(1) of this section for six years from the date of its creation or the date when it last was in effect, whichever is later.'

- URL: <https://www.govinfo.gov/content/pkg/CFR-2023-title45-vol2/xml/CFR-2023-title45-vol2-sec164-530.xml>
- Accessed: 2026-08-31

**Q2.9** ABP's audit DbContext declares its own connection string name, so pointing audit at a separate database (or schema) is a configuration-shaped change. DbSchema is a settable static, so a separate schema is also first-class.

> IAuditLoggingDbContext.cs:7 and AbpAuditLoggingDbContext.cs:7: '[ConnectionStringName(AbpAuditLoggingDbProperties.ConnectionStringName)]'; AbpAuditLoggingDbProperties.cs: 'public const string ConnectionStringName = "AbpAuditLogging";' and 'public static string DbSchema { get; set; } = AbpCommonDbProperties.DbSchema;'

- URL: <https://github.com/abpframework/abp/blob/dev/modules/audit-logging/src/Volo.Abp.AuditLogging.Domain/Volo/Abp/AuditLogging/AbpAuditLoggingDbProperties.cs>
- Accessed: 2026-08-31

**Q2.10** The audit log is written in its OWN unit of work, after the business unit of work has already been saved  -  so moving audit to a separate database creates no distributed-transaction requirement. It also means audit-write failures are swallowed by default (HideErrors = true), a silent-failure hazard that separation makes more likely.

> AbpAuditingMiddleware, finally block: 'if (UnitOfWorkManager.Current != null) { await UnitOfWorkManager.Current.SaveChangesAsync(); } ... await saveHandle.SaveAsync();'. AuditingStore.SaveLogAsync: 'using (var uow = UnitOfWorkManager.Begin(true)) { await AuditLogRepository.InsertAsync(...); await uow.CompleteAsync(); }'. SaveAsync: 'catch (Exception ex) { Logger.LogWarning("Could not save the audit log object: " ...); Logger.LogException(ex, LogLevel.Error); }'

- URL: <https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.AspNetCore/Volo/Abp/AspNetCore/Auditing/AbpAuditingMiddleware.cs>
- Accessed: 2026-08-31

**Q2.11** Entity property-change tracking is scopable per entity and per property, and SaveEntityHistoryWhenNavigationChanges defaults to TRUE  -  a likely amplifier of the measured 2,689 property-change rows. Precedence: [Audited] wins first, then [DisableAuditing], then selectors  -  so [DisableAuditing] on a class overrides AddAllEntities().

> AbpAuditingOptions.cs: 'public bool SaveEntityHistoryWhenNavigationChanges { get; set; } = true;'. AuditingHelper.IsEntityHistoryEnabled: checks IgnoredTypes, then AuditedAttribute on type, then AuditedAttribute on any property, then 'if (entityType.IsDefined(typeof(DisableAuditingAttribute), true)) { return false; }', then 'if (Options.EntityHistorySelectors.Any(selector => selector.Predicate(entityType))) { return true; }'. EntityHistoryHelper.ShouldSavePropertyHistory: 'if (propertyInfo != null && propertyInfo.IsDefined(typeof(DisableAuditingAttribute), true)) { return false; }'. AddAllEntities: 'selectors.Add(new NamedTypeSelector(AllEntitiesSelectorName, t => typeof(IEntity).IsAssignableFrom(t)));'

- URL: <https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Auditing/Volo/Abp/Auditing/AbpAuditingOptions.cs>
- Accessed: 2026-08-31

**Q2.12** ABP ships NO audit cleanup, retention or expiry mechanism. Verified by grepping the entire cloned repository for cleanup/retention/expire/purge/olderthan/deleteold across the audit-logging module and for AuditLogCleanup/CleanupAuditLog/DeleteExpiredAuditLog/AuditLogRetention across all of src  -  zero matches. IAuditLogRepository exposes only read/count methods plus the generic IRepository<AuditLog, Guid>; there is no delete-by-date. Pruning must be written by the team. ABP is actively maintained: dev branch HEAD commit dated 2026-08-31, version 10.8.0-preview.

> grep -rniE "cleanup|retention|expire|purge|olderthan|deleteold" modules/audit-logging/src --include=*.cs -> no output. grep -rlniE "AuditLogCleanup|CleanupAuditLog|DeleteExpiredAuditLog|AuditLogRetention" --include=*.cs -> no output.

- URL: <https://github.com/abpframework/abp/blob/dev/modules/audit-logging/src/Volo.Abp.AuditLogging.Domain/Volo/Abp/AuditLogging/IAuditLogRepository.cs>
- Accessed: 2026-08-31

**Q2.13** ABP's AuditLog and EntityPropertyChange entities implement IMultiTenant with a TenantId column  -  which is what would make a consolidated cross-tenant audit store technically possible, and is precisely why I reject it: isolation would then rest on a query filter (silent failure) rather than a connection string (obvious failure).

> '[DisableAuditing] public class AuditLog : AggregateRoot<Guid>, IMultiTenant { ... public virtual Guid? TenantId { get; protected set; } ... }' and '[DisableAuditing] public class EntityPropertyChange : Entity<Guid>, IMultiTenant { public virtual Guid? TenantId { get; protected set; } ... }'

- URL: <https://github.com/abpframework/abp/blob/dev/modules/audit-logging/src/Volo.Abp.AuditLogging.Domain/Volo/Abp/AuditLogging/AuditLog.cs>
- Accessed: 2026-08-31

**Q2.14** ABP's connection-string resolution chain determines how much work step 4 is: for a named string like AbpAuditLogging, it tries the tenant's explicit entry, then a mapped database used by tenants, then FALLS BACK TO THE TENANT'S DEFAULT connection string  -  which is why audit currently lands in the office database. Host-level resolution similarly falls back to Default.

> MultiTenantConnectionStringResolver.ResolveAsync: '//Requesting specific connection string... var connString = tenant.ConnectionStrings?.GetOrDefault(connectionStringName); ... //Fallback to tenant's default connection string if available; if (!tenantDefaultConnectionString.IsNullOrWhiteSpace()) { return tenantDefaultConnectionString!; }'. AbpDbConnectionOptions.GetConnectionStringOrNull: 'if (fallbackToDefault) { connectionString = ConnectionStrings.Default; ... }'

- URL: <https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/MultiTenantConnectionStringResolver.cs>
- Accessed: 2026-08-31

**Q2.15** Append-only ledger tables allow ONLY inserts, block UPDATE and DELETE, and cannot be reverted once created  -  so applying WORM to the live audit tables would permanently foreclose the pruning that is the actual fix. This is the decisive argument against option (F) on live tables.

> 'Append-only ledger tables allow only INSERT operations on your tables... Because there are no UPDATE or DELETE operations on an append-only table, there's no need for a corresponding history table.' and 'After a table is created as a ledger table, it can't be reverted to a table that doesn't have ledger functionality.'

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/security/ledger/ledger-append-only-ledger-tables?view=sql-server-ver16>
- Accessed: 2026-08-31

**Q2.16** Ledger IS available in every SQL Server 2022 edition (not Enterprise-only), so tamper-evidence is not edition-gated  -  the argument against it here is operational, not licensing. Same page confirms Developer edition is not licensed for production, and that table/index partitioning and data compression are available in all editions while backup compression requires Enterprise or Standard.

> Security table: 'Ledger for SQL Server | Yes | Yes | Yes | Yes | Yes'. Editions table: 'Developer  -  ...includes all the functionality of Enterprise edition, but is licensed for use as a development and test system, not as a production server.' Scalability table: 'Table and index partitioning | Yes | Yes | Yes | Yes | Yes'; 'Data compression | Yes | Yes | Yes | Yes | Yes'. High availability table: 'Backup compression | Yes | Yes | No | No | No'; 'Maximum memory for buffer pool per instance | Operating system maximum | 128 GB | 64 GB | 1,410 MB | 1,410 MB'.

- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022?view=sql-server-ver16>
- Accessed: 2026-08-31

**Q2.17** Piecemeal restore  -  the textbook way to restore operational data without restoring audit inside one database  -  is OFFLINE-only outside Enterprise edition, requires the whole database to go offline during the partial-restore sequence, and carries subtle failure modes (defunct filegroups, deferred transactions). This is why I reject option (C) for a two-person team with no tested restore.

> 'This topic is relevant for databases in the Enterprise edition of SQL Server (online restore) or Standard edition (offline restore)...' 'All editions of SQL Server support offline piecemeal restores. In the Enterprise edition, a piecemeal restore can be either online or offline.' 'During the piecemeal-restore sequence, the whole database must go offline.' 'any unrestored filegroups become defunct when you recover the partially restored database.'

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/piecemeal-restores-sql-server?view=sql-server-ver16>
- Accessed: 2026-08-31

**Q2.18** SQL Server 2022 can back up and restore directly to S3-compatible object storage, which the platform already runs (MinIO). This makes per-database off-box backup of separated audit databases a native capability rather than a new tool  -  but it requires HTTPS with a CA trusted by the SQL Server host, and on Linux the CA must be placed in /var/opt/mssql/security/ca-certificates BEFORE the process starts. Path-style URLs (which this deployment already uses) are supported. RESTORE ... WITH PARTIAL is supported over S3.

> 'SQL Server 2022 (16.x) introduces object storage integration... URLs pointing to S3-compatible resources are prefixed with s3://... URLs beginning with s3:// always assume that the underlying protocol is https.' 'TLS must be configured... The endpoint is validated by a certificate installed on the SQL Server OS Host.' 'SQL Server on Linux the CA must be placed on a predefined location to be created at /var/opt/mssql/security/ca-certificates... The CA must be in place before SQL Server process is started.' 'Back up to S3-compatible object storage isn't supported in SQL Server Express and SQL Server Express with Advanced Services editions.' 'Backups can be striped across a maximum of 64 URLs.' 'URLs can be specified either in virtual host or path style format.'

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/sql-server-backup-to-url-s3-compatible-object-storage?view=sql-server-ver16>
- Accessed: 2026-08-31

**Q2.19** Sizing inputs for the audit tables (MY CALCULATION from ABP's declared column limits, not a sourced measurement): a single EntityPropertyChange row can reach ~3.4 KB (NewValue 512 + OriginalValue 512 + PropertyName 128 + PropertyTypeFullName 512 chars, nvarchar at 2 bytes/char), and a single AuditLogAction row can reach ~4 KB of serialised method parameters. Those parameters are the same PHI-bearing arguments the brief flags as exposed by the unauthenticated Hangfire dashboard  -  meaning the audit database is unambiguously a PHI store and must keep full HIPAA controls after separation.

> EntityPropertyChangeConsts: MaxNewValueLength = 512, MaxOriginalValueLength = 512, MaxPropertyNameLength = 128, MaxPropertyTypeFullNameLength = 512. AuditLogActionConsts: MaxParametersLength = 2000. AuditLogConsts: MaxBrowserInfoLength = 512, MaxUrlLength = 256.

- URL: <https://github.com/abpframework/abp/blob/dev/modules/audit-logging/src/Volo.Abp.AuditLogging.Domain.Shared/Volo/Abp/AuditLogging/EntityPropertyChangeConsts.cs>
- Accessed: 2026-08-31

---

### Q3. Can this application run more than one API instance today? Work through each blocker (Hangfire, DataProtection keyring, the distributed lock, the migration runner, and anything else) with cited evidence, give a definitive yes or no, name precisely what blocks it and the smallest change that unblocks

Confidence: **determinate-with-caveats**

**Q3.1** Hangfire explicitly supports multiple server instances against one storage, coordinated by distributed locks.

> It is possible to run multiple server instances inside a process, machine, or on several machines at the same time. Each server use distributed locks to perform the coordination logic.

- URL: <https://docs.hangfire.io/en/latest/background-processing/running-multiple-server-instances.html>
- Accessed: 2026-08-31

**Q3.2** RecurringJobScheduler acquires a storage-wide distributed lock named 'recurring-jobs:lock' with a 1-minute timeout, plus a per-job lock, and treats a lock timeout as evidence another server already did the work  -  so a recurring job does not normally fire twice across N servers. Its XML doc states multi-instance adds fail-over only, not throughput.

> private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(1); ... var resource = "recurring-jobs:lock"; ... using (connection.AcquireDistributedLock(resource, LockTimeout)) ... // DistributedLockTimeoutException here doesn't mean that recurring jobs weren't scheduled. // It just means another Hangfire server did this work. ... using (connection.AcquireDistributedRecurringJobLock(recurringJobId, LockTimeout))

- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/master/src/Hangfire.Core/Server/RecurringJobScheduler.cs>
- Accessed: 2026-08-31

**Q3.3** Hangfire's recurring scheduler polls on a minute-based interval and requires an always-on server.

> A special component in Hangfire Server checks the recurring jobs on a minute-based interval and then enqueues them as fire-and-forget jobs. ... Your Hangfire Server instance should be always on to perform scheduling and processing logic.

- URL: <https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html>
- Accessed: 2026-08-31

**Q3.4** Hangfire concedes that the same job can be processed on different workers in corner cases  -  execution is at-least-once, not exactly-once. This is the decisive risk for the office-iterating outbox drains.

> Mutex doesn't prevent simultaneous execution of the same background job. As there are no reliable automatic failure detectors in distributed systems, it is possible that the same job is being processed on different workers in some corner cases.

- URL: <https://docs.hangfire.io/en/latest/background-processing/throttling.html>
- Accessed: 2026-08-31

**Q3.5** QueuePollInterval=TimeSpan.Zero is only legal when SlidingInvisibilityTimeout is set, and DisableGlobalLocks=true is Hangfire's own recommendation for 1.7+ installations, requiring schema 7.

> Starting from Hangfire 1.7.0 it's possible to use TimeSpan.Zero as a polling interval, when SlidingInvisibilityTimeout option is set. ... [DisableGlobalLocks] Migration to Schema 7 is required.

- URL: <https://docs.hangfire.io/en/latest/configuration/using-sql-server.html>
- Accessed: 2026-08-31

**Q3.6** With QueuePollInterval=Zero the actual idle poll delay is a clamped 200 ms, and a STATIC IN-PROCESS SemaphoreSlim(1) per (storage, queue-set) means only one thread per process polls at a time. The semaphore does not cross process boundaries, so N API instances produce N times the idle query floor against the host database (~5 fetch statements/sec each).

> private static readonly TimeSpan LongPollingThreshold = TimeSpan.FromSeconds(1); private static readonly int PollingQuantumMs = 1000; private static readonly int DefaultPollingDelayMs = 200; private static readonly int MinPollingDelayMs = 100; ... var useLongPolling = configuredPollInterval < LongPollingThreshold; ... var pollingDelayMs = useLongPolling ? TimeSpan.FromMilliseconds(Math.Min(Math.Max(configuredPollInterval == TimeSpan.Zero ? DefaultPollingDelayMs : (int)configuredPollInterval.TotalMilliseconds, MinPollingDelayMs), PollingQuantumMs)) : configuredPollInterval; ... if (!_options.DisableFetchSemaphores) { semaphore = Semaphores.GetOrAdd(resource, CreateSemaphoreFunc); semaphore.Wait(cancellationToken); }

- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/master/src/Hangfire.SqlServer/SqlServerJobQueue.cs>
- Accessed: 2026-08-31

**Q3.7** DisableGlobalLocks only removes coarse sp_getapplock calls on the List/Set/Hash resources inside the write transaction  -  it makes N-server contention LOWER, not higher, and does not affect recurring-job dedup.

> private void AcquireListLock(string key) { AcquireLock(_storage.Options.DisableGlobalLocks ? $"List:{key}" : "List"); } ... private void AcquireLock(string resource) { if (!_storage.Options.DisableGlobalLocks || _storage.Options.UseFineGrainedLocks) { _lockedResources.Add($"{_storage.SchemaName}:{resource}:Lock"); } }

- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/master/src/Hangfire.SqlServer/SqlServerWriteOnlyTransaction.cs>
- Accessed: 2026-08-31

**Q3.8** AcquireDistributedLock (the path RecurringJobScheduler uses) goes to sp_getapplock with Exclusive/Session and is NOT gated on DisableGlobalLocks  -  confirming recurring-job coordination survives that setting.

> public override IDisposable AcquireDistributedLock([NotNull] string resource, TimeSpan timeout) { ... return AcquireLock($"{_storage.SchemaName}:{resource}", timeout); }  // and in SqlServerDistributedLock.cs: private const string LockMode = "Exclusive"; private const string LockOwner = "Session"; ... .Create("sp_getapplock", CommandType.StoredProcedure, ...)

- URL: <https://raw.githubusercontent.com/HangfireIO/Hangfire/master/src/Hangfire.SqlServer/SqlServerConnection.cs>
- Accessed: 2026-08-31

**Q3.9** THE SMALLEST FIX FOR (a), verified in ABP source: setting IsJobExecutionEnabled=false nulls the Hangfire BackgroundJobServerFactory so no processing server starts, while JobStorage is still resolved so enqueueing continues. Default is true. This is the same switch AuthServer already uses.

> public override void OnPreApplicationInitialization(ApplicationInitializationContext context) { var options = context.ServiceProvider.GetRequiredService<IOptions<AbpBackgroundJobOptions>>().Value; if (!options.IsJobExecutionEnabled) { var hangfireOptions = context.ServiceProvider.GetRequiredService<IOptions<AbpHangfireOptions>>().Value; context.ServiceProvider.GetRequiredService<JobStorage>(); hangfireOptions.BackgroundJobServerFactory = _ => null; } }

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.BackgroundJobs.HangFire/Volo/Abp/BackgroundJobs/Hangfire/AbpBackgroundJobsHangfireModule.cs>
- Accessed: 2026-08-31

**Q3.10** ABP registers recurring jobs by deterministic id via RecurringJob.AddOrUpdate, so N instances produce ONE recurring job definition rather than N.

> RecurringJob.AddOrUpdate(recurringJobId, queueName, methodCall, cronExpression ?? GetCron(period!.Value), new RecurringJobOptions { TimeZone = workerAdapter.TimeZone });

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.BackgroundWorkers.Hangfire/Volo/Abp/BackgroundWorkers/Hangfire/HangfireBackgroundWorkerManager.cs>
- Accessed: 2026-08-31

**Q3.11** Data Protection default config is per-node and unsuitable for a web farm; the required fix is a shared key ring plus the same SetApplicationName. Both are already satisfied here, so (b) is NOT a blocker.

> Under the default configuration, a unique key ring is stored on each node of the web farm. Consequently, each web farm node can't decrypt data that's encrypted by an app on any other node. The default configuration isn't generally appropriate for hosting apps in a web farm.

- URL: <https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/web-farm>
- Accessed: 2026-08-31

**Q3.12** The two documented requirements for sharing protected payloads: identical SetApplicationName (which sets ApplicationDiscriminator) and the same Data Protection stack version.

> To share protected payloads among apps: Configure SetApplicationName in each app with the same value. Use the same version of the Data Protection API stack across the apps. ... SetApplicationName internally sets DataProtectionOptions.ApplicationDiscriminator. ... For the apps to be able to read each other's cryptographic payloads, they must have the same application discriminator, which can be set by calling SetApplicationName.

- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview>
- Accessed: 2026-08-31

**Q3.13** Changing the key persistence location (to Redis) disables automatic key encryption at rest  -  the basis for my inference that the keyring is plaintext in Redis today. Microsoft also warns encryption at rest does not stop an attacker with write access from minting keys. Redis must support persistence, which AOF satisfies.

> If you change the key persistence location, the system no longer automatically encrypts keys at rest, since it doesn't know whether DPAPI is an appropriate encryption mechanism. ... You can choose to encrypt keys at rest, but this doesn't prevent cyberattackers from creating new keys. ... Only Redis versions supporting Redis Data Persistence should be used to store keys.

- URL: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview>
- Accessed: 2026-08-31

**Q3.14** ABP's distributed lock wraps the Medallion DistributedLock library.

> ABP's current distributed locking implementation is based on the DistributedLock library.

- URL: <https://abp.io/docs/latest/framework/infrastructure/distributed-locking>
- Accessed: 2026-08-31

**Q3.15** Enumeration of ABP's OWN internal IAbpDistributedLock use sites  -  answering (c) directly. The list includes StaticPermissionSaver, StaticSettingSaver, StaticFeatureSaver, the three Dynamic*DefinitionStores, OutboxSender, InboxProcessor, BackgroundJobWorker, BackgroundJobCleanupWorker, EfCoreRuntimeDatabaseMigratorBase, EfCoreDatabaseMigrationEventHandlerBase, AbpIdentityUserValidator, and OpenIddict's TokenCleanupBackgroundWorker.

> framework/src/Volo.Abp.EventBus/Volo/Abp/EventBus/Distributed/OutboxSender.cs; framework/src/Volo.Abp.BackgroundJobs/Volo/Abp/BackgroundJobs/BackgroundJobCleanupWorker.cs; modules/setting-management/.../StaticSettingSaver.cs; modules/permission-management/.../StaticPermissionSaver.cs; framework/src/Volo.Abp.EntityFrameworkCore/Volo/Abp/EntityFrameworkCore/Migrations/EfCoreRuntimeDatabaseMigratorBase.cs; modules/openiddict/.../TokenCleanupBackgroundWorker.cs

- URL: <https://github.com/abpframework/abp/search?q=IAbpDistributedLock>
- Accessed: 2026-08-31

**Q3.16** ABP's StaticPermissionSaver exists specifically to coordinate MULTIPLE APPLICATION INSTANCES at startup  -  its own comment says so. Without a working lock provider, N instances race to rewrite permission definitions; with one, Redis becomes a startup-path dependency.

> await using var applicationLockHandle = await DistributedLock.TryAcquireAsync(GetApplicationDistributedLockKey()); if (applicationLockHandle == null) { /* Another application instance is already doing it */ return; } ... if (commonLockHandle == null) { /* It will re-try */ throw new AbpException("Could not acquire distributed lock for saving static permissions!"); }

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/modules/permission-management/src/Volo.Abp.PermissionManagement.Domain/Volo/Abp/PermissionManagement/StaticPermissionSaver.cs>
- Accessed: 2026-08-31

**Q3.17** THE EXACT PATTERN TO COPY FOR (d): ABP's own runtime migrator takes a distributed lock keyed on the database name and cleanly cancels if it cannot get it.

> await using (var handle = await DistributedLock.TryAcquireAsync("DatabaseMigration_" + DatabaseName)) { if (handle is null) { Logger.LogInformation($"Distributed lock could not be acquired for database migration: {DatabaseName}. Operation cancelled."); return; }

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.EntityFrameworkCore/Volo/Abp/EntityFrameworkCore/Migrations/EfCoreRuntimeDatabaseMigratorBase.cs>
- Accessed: 2026-08-31

**Q3.18** EF Core 9+ auto-locks migrations against concurrent instances  -  BUT the lock covers only UseSeeding delegates, which ABP seed contributors are not. It also now throws on pending model changes, which is a fail-obvious property worth keeping given the two-DbContext hazard. And it explicitly tells you not to migrate from every replica's entrypoint.

> Starting with EF Core 9, MigrateAsync and Migrate automatically acquire a database-wide lock before applying any migrations. This protects against database corruption that could result from multiple application instances running migrations concurrently, which is a common scenario when applying migrations at runtime. The lock is held for the duration of the migration execution, including any seeding code. ... Starting with EF Core 9, calling Migrate() or MigrateAsync() will throw an exception when the model has pending changes compared to the last migration (warning event ID RelationalEventId.PendingModelChangesWarning). ... don't make every application replica run migrations from its entrypoint. Configure the deployment platform not to restart the migration container after it exits successfully. ... Use a separate identity for deployment that has permission to change the schema. The identity used by the application at run time should normally have only the permissions the application needs to read and write data.

- URL: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying>
- Accessed: 2026-08-31

**Q3.19** Connection pools are per PROCESS and per CONNECTION STRING, default max 100, and Microsoft names the database-per-tenant pool-fragmentation problem explicitly  -  directly applicable since TenantConnectionStringProvider derives a distinct string per office. Exhaustion queues then throws after 15 s.

> Connections are pooled per process, per application domain, per connection string and when integrated security is used, per Windows identity. ... Connections are added to the pool as needed, up to the maximum pool size specified (100 is the default). ... If the maximum pool size has been reached and no usable connection is available, the request is queued. The pooler then tries to reclaim any connections until the time-out is reached (the default is 15 seconds). If the pooler cannot satisfy the request before the connection times out, an exception is thrown. ... Pool fragmentation due to many databases ... there is a separate pool of connections to each database, which increase the number of connections to the server.

- URL: <https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling>
- Accessed: 2026-08-31

**Q3.20** SQL Server's own ceilings are not the binding constraint on pool growth  -  32,767 user connections and 32,767 databases per instance. The 10 GB container's memory is the real limit.

> User connections | 32,767 ... Databases per instance of SQL Server | 32,767 ... Database size | 524,272 terabytes

- URL: <https://learn.microsoft.com/en-us/sql/sql-server/maximum-capacity-specifications-for-sql-server>
- Accessed: 2026-08-31

**Q3.21** ABP's IDistributedEventBus resolves to LocalDistributedEventBus when no broker provider is registered  -  so with NO message broker in this stack, cross-instance 'distributed' events silently do not propagate at N>1, with no error. This is the subtlest multi-instance hazard.

> [ExposeServices(typeof(IDistributedEventBus), typeof(LocalDistributedEventBus))] public class LocalDistributedEventBus : DistributedEventBusBase, ISingletonDependency

- URL: <https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.EventBus/Volo/Abp/EventBus/Distributed/LocalDistributedEventBus.cs>
- Accessed: 2026-08-31

**Q3.22** Counter-evidence for fairness: ABP's tenant-config cache invalidation DOES survive multi-instance, because both TenantStore and its invalidator use IDistributedCache<TenantConfigurationCacheItem>, which is Redis-backed here  -  deleting a key on one node deletes it for all. Cache invalidation through Redis is safe; behaviour triggered by an event handler is not.

> protected IDistributedCache<TenantConfigurationCacheItem> Cache { get; }   -  in both TenantStore.cs and TenantConfigurationCacheItemInvalidator.cs

- URL: <https://github.com/abpframework/abp/blob/dev/modules/tenant-management/src/Volo.Abp.TenantManagement.Domain/Volo/Abp/TenantManagement/TenantStore.cs>
- Accessed: 2026-08-31

**Q3.23** Maintenance currency check. Hangfire.SqlServer is very actively maintained (1.8.25 published 2026-08-28; the app's pinned 1.8.21 dates to 2025-08-12, four patches behind). DistributedLock.Redis 1.1.1 published 2025-10-26  -  current. AspNetCore.HealthChecks.UI's latest stable is 9.0.0 published 2024-12-19 with no 10.x for .NET 10  -  this dependency is drifting behind the runtime.

> hangfire.sqlserver: 1.8.21 2025-08-12, 1.8.22 2025-11-07, 1.8.23 2026-02-05, 1.8.24 2026-07-16, 1.8.25 2026-08-28. distributedlock.redis: 1.1.0 2025-08-10, 1.1.1 2025-10-26. aspnetcore.healthchecks.ui: 8.0.2 2024-08-29, 9.0.0 2024-12-19 (latest).

- URL: <https://api.nuget.org/v3/registration5-semver1/hangfire.sqlserver/index.json>
- Accessed: 2026-08-31

**Q3.24** The AspNetCore.Diagnostics.HealthChecks GitHub releases page corroborates the NuGet finding  -  no tagged release since Feb 2024.

> Most recent releases: DotNet 8 (release-all-8.0.0-with-valid-apikey) February 28, 2024; 7.0.1 UI serialization fix July 31, 2023; V7 Release July 31, 2023. No releases documented from 2025 or 2026.

- URL: <https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/releases>
- Accessed: 2026-08-31

**Q3.25** HIPAA requires the ability to RESTORE, not redundancy. Backup, disaster recovery and emergency mode operation are all Required; testing is only Addressable; high availability is not mentioned. This is the basis for ranking a tested restore above a second instance.

> (7)(i) Standard: Contingency plan. Establish (and implement as needed) policies and procedures for responding to an emergency or other occurrence (for example, fire, vandalism, system failure, and natural disaster) that damages systems that contain electronic protected health information. (ii) Implementation specifications: (A) Data backup plan (Required). Establish and implement procedures to create and maintain retrievable exact copies of electronic protected health information. (B) Disaster recovery plan (Required). Establish (and implement as needed) procedures to restore any loss of data. (C) Emergency mode operation plan (Required). Establish (and implement as needed) procedures to enable continuation of critical business processes for protection of the security of electronic protected health information while operating in emergency mode. (D) Testing and revision procedures (Addressable). Implement procedures for periodic testing and revision of contingency plans. (E) Applications and data criticality analysis (Addressable). Assess the relative criticality of specific applications and data in support of other contingency plan components.

- URL: <https://www.ecfr.gov/api/versioner/v1/full/2026-08-01/title-45.xml?part=164&section=164.308>
- Accessed: 2026-08-31

**Q3.26** The four benefits of a web farm, stated by Microsoft  -  used to make the honest case FOR horizontal scale, and the shared-state requirements (Data Protection plus distributed cache) that come with it. Note the doc also names sticky routing as an alternative to a shared key ring, which this system does not need since it already has one.

> Web farms improve: Reliability/availability... Capacity/performance... Scalability... Maintainability... Data Protection and Caching require configuration for apps deployed to a web farm. ... When Data Protection or caching isn't configured for a web farm environment, intermittent errors occur when requests are processed.

- URL: <https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/web-farm>
- Accessed: 2026-08-31

**Q3.27** With a single Redis you get a lease-based mutex, not a RedLock guarantee  -  the library's own doc says robustness requires a majority across multiple databases. Adequate for startup definition-saving; NOT adequate for an outbox drain that must never double-send.

> The RedisDistributedLock and RedisDistributedReaderWriterLock classes implement the RedLock algorithm. ... This allows you to increase the robustness of those locks by constructing the lock with a set of databases instead of just a single database. The lock is only considered aquired if it is successfully acquired on more than half of the databases.

- URL: <https://github.com/madelson/DistributedLock/blob/master/docs/DistributedLock.Redis.md>
- Accessed: 2026-08-31

---

### Q4. What must be true of every component in the request path for Host-header tenancy to keep working, and which common infrastructure patterns violate it?

Confidence: **determinate-with-caveats**

**Q4.1** ABP registers FIVE default tenant resolvers, not four. CurrentUserTenantResolveContributor is inserted at index 0 by AbpMultiTenancyModule, ahead of the four __tenant resolvers, so an authenticated request resolves tenancy from the token claim rather than the Host header.

> Configure<AbpTenantResolveOptions>(options => { options.TenantResolvers.Insert(0, new CurrentUserTenantResolveContributor()); });

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/AbpMultiTenancyModule.cs>
- Accessed: 2026-08-31

**Q4.2** The four __tenant resolvers and their registration order (QueryString, Route, Header, Cookie), added by the ASP.NET Core multi-tenancy module.

> options.TenantResolvers.Add(new QueryStringTenantResolveContributor()); options.TenantResolvers.Add(new RouteTenantResolveContributor()); options.TenantResolvers.Add(new HeaderTenantResolveContributor()); options.TenantResolvers.Add(new CookieTenantResolveContributor());

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.AspNetCore.MultiTenancy/Volo/Abp/AspNetCore/MultiTenancy/AbpAspNetCoreMultiTenancyModule.cs>
- Accessed: 2026-08-31

**Q4.3** Tenant resolution is first-match-wins with a hard break, making resolver ORDER decisive and total.

> foreach (var tenantResolver in Options.TenantResolvers) { await tenantResolver.ResolveAsync(context); ... if (context.HasResolvedTenantOrHost()) { result.TenantIdOrName = context.TenantIdOrName; ... break; } }

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/TenantResolver.cs>
- Accessed: 2026-08-31

**Q4.4** DomainTenantResolveContributor sets Handled=true UNCONDITIONALLY when Host has a value (before returning the match result), so it terminates the resolver chain whether or not the host matched. It also abstains entirely (without setting Handled) when Host is empty, allowing downstream __tenant resolvers to run. It matches on Request.Host.Value, which includes the port.

> if (!httpContext.Request.Host.HasValue) { return Task.FromResult<string?>(null); } var hostName = httpContext.Request.Host.Value.RemovePreFix(ProtocolPrefixes); var extractResult = FormattedStringValueExtracter.Extract(hostName, _domainFormat, ignoreCase: true); context.Handled = true; return Task.FromResult(extractResult.IsMatch ? extractResult.Matches[0].Value : null);

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.AspNetCore.MultiTenancy/Volo/Abp/AspNetCore/MultiTenancy/DomainTenantResolveContributor.cs>
- Accessed: 2026-08-31

**Q4.5** Handled alone terminates the chain, independent of whether a tenant was actually identified.

> public bool HasResolvedTenantOrHost() { return Handled || TenantIdOrName != null; }

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/TenantResolveContext.cs>
- Accessed: 2026-08-31

**Q4.6** A single ?__tenant= query-string request causes ABP to PERSIST the tenant override into a cookie, which then drives later requests via the Cookie resolver.

> if (_tenantResolveResultAccessor.Result != null && _tenantResolveResultAccessor.Result.AppliedResolvers.Contains(QueryStringTenantResolveContributor.ContributorName)) { AbpMultiTenancyCookieHelper.SetTenantCookie(context, _currentTenant.Id, _options.TenantKey); }

- URL: <https://raw.githubusercontent.com/abpframework/abp/dev/framework/src/Volo.Abp.AspNetCore.MultiTenancy/Volo/Abp/AspNetCore/MultiTenancy/MultiTenancyMiddleware.cs>
- Accessed: 2026-08-31

**Q4.7** nginx's DEFAULT is to rewrite Host to the backend address; the original Host is not passed unless explicitly configured. For HTTP/2 it sends :authority as $proxy_host.

> Default: proxy_set_header Host $proxy_host; proxy_set_header Connection close; ... By default, the header fields "Host" and "Connection" from the original request are not passed to the proxied server. ... For HTTP/2, the ":authority" pseudo-header field with the $proxy_host value is sent by default, unless it is replaced with an explicit "Host" header field.

- URL: <https://nginx.org/en/docs/http/ngx_http_proxy_module.html>
- Accessed: 2026-08-31

**Q4.8** nginx's $host takes the host from the REQUEST LINE first (before the Host header), and falls back to the matching server_name if neither is present  -  a different derivation order from Kestrel's, which is the desync risk between the two components in this stack.

> $host  -  in this order of precedence: host name from the request line, or host name from the "Host" request header field, or the server name matching a request

- URL: <https://nginx.org/en/docs/http/ngx_http_core_module.html>
- Accessed: 2026-08-31

**Q4.9** nginx normalises the host: it lowercases it and strips a trailing dot, and parses the port off separately. Confirms case-normalisation is safe but that $host is not byte-identical to the wire value.

> if (dot_pos == host_len - 1) { host_len--; } ... if (alloc) { host->data = ngx_pnalloc(pool, host_len); ... ngx_strlow(host->data, h, host_len); }

- URL: <https://raw.githubusercontent.com/nginx/nginx/master/src/http/ngx_http_request.c>
- Accessed: 2026-08-31

**Q4.10** Kestrel rejects absolute-form requests whose Host header disagrees with the request-line authority, unless AllowHostHeaderOverride is enabled (default false). It also rejects missing and duplicate Host headers.

> else if (_requestTargetForm == HttpRequestTarget.AbsoluteForm) { ... if (hostText != authority) { if (!_absoluteRequestTarget.IsDefaultPort || hostText != $"{authority}:{_absoluteRequestTarget.Port}") { if (_context.ServiceContext.ServerOptions.AllowHostHeaderOverride) { hostText = authority; HttpRequestHeaders.HeaderHost = hostText; } else { ... KestrelBadHttpRequestException.Throw(RequestRejectionReason.InvalidHostHeader, hostText); } } } }

- URL: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/Servers/Kestrel/Core/src/Internal/Http/Http1Connection.cs>
- Accessed: 2026-08-31

**Q4.11** AllowHostHeaderOverride defaults to false and does not apply to HTTP/2 or HTTP/3  -  so the HTTP/1.1 desync protection is not inherited by h2/h3.

> However, it is still sensible to check whether the request target and Host header match because a mismatch might indicate, for example, a spoofing attempt. Setting this property to true bypasses that check and unconditionally overwrites the Host header with the value from the request target. ... This option does not apply to HTTP/2 or HTTP/3.

- URL: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/Servers/Kestrel/Core/src/KestrelServerOptions.cs>
- Accessed: 2026-08-31

**Q4.12** RFC 9112 requires an origin server to IGNORE the Host header and use the request-target authority for absolute-form requests, and to reject missing/duplicate/invalid Host with 400.

> When an origin server receives a request with an absolute-form of request-target, the origin server MUST ignore the received Host header field (if any) and instead use the host information of the request-target. ... A server MUST respond with a 400 (Bad Request) status code to any HTTP/1.1 request message that lacks a Host header field and to any request message that contains more than one Host header field line or a Host header field with an invalid field value.

- URL: <https://www.rfc-editor.org/rfc/rfc9112.txt>
- Accessed: 2026-08-31

**Q4.13** RFC 9110 confirms host comparison is case-insensitive (so case-folding at a proxy is safe) and warns that Host is an application-level routing mechanism and a frequent attack target.

> The scheme and host are case-insensitive and normally provided in lowercase; all other components are compared in a case-sensitive manner. ... Since the host and port information acts as an application-level routing mechanism, it is a frequent target for malware seeking to poison a shared cache or redirect a request to an unintended server.

- URL: <https://www.rfc-editor.org/rfc/rfc9110.txt>
- Accessed: 2026-08-31

**Q4.14** HTTP/2 permits connection reuse across different authorities when one certificate covers them  -  which a wildcard cert guarantees  -  and explicitly documents SNI-based origin selection as a cause of misdirected requests. 421 is the documented remedy.

> Connections that are made to an origin server ... MAY be reused for requests with multiple different URI authority components. ... In some deployments, reusing a connection for multiple origins can result in requests being directed to the wrong origin server. For example, TLS termination might be performed by a middlebox that uses the TLS Server Name Indication [TLS-EXT] extension to select an origin server. ... A server that does not wish clients to reuse connections can indicate that it is not authoritative for a request by sending a 421 (Misdirected Request) status code

- URL: <https://www.rfc-editor.org/rfc/rfc9113.txt>
- Accessed: 2026-08-31

**Q4.15** In HTTP/2, :authority is authoritative over Host, and an intermediary generating a Host header MUST derive it from :authority, explicitly to avoid HTTP routing vulnerabilities.

> The recipient of an HTTP/2 request MUST NOT use the Host header field to determine the target URI if ":authority" is present. ... An intermediary that needs to generate a Host header field ... MUST use the value from the ":authority" pseudo-header field as the value of the Host field ... This replaces any existing Host field to avoid potential vulnerabilities in HTTP routing.

- URL: <https://www.rfc-editor.org/rfc/rfc9113.txt>
- Accessed: 2026-08-31

**Q4.16** ASP.NET Core does not process X-Forwarded-Host by default (ForwardedHeaders has no initializer, so None); if enabled it overwrites Request.Host, gated only by AllowedHosts (empty = all allowed) and KnownProxies (default loopback only).

> The allowed values from x-forwarded-host. If the list is empty then all hosts are allowed. Failing to restrict this these values may allow an attacker to spoof links generated by your service. ... public IList<IPAddress> KnownProxies { get; } = new List<IPAddress>() { IPAddress.IPv6Loopback };

- URL: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/Middleware/HttpOverrides/src/ForwardedHeadersOptions.cs>
- Accessed: 2026-08-31

**Q4.17** ForwardedHeadersMiddleware overwrites request.Host from X-Forwarded-Host when enabled, confirming the header becomes a tenant selector for a resolver that reads Request.Host.

> request.Host = HostString.FromUriComponent(currentValues.Host);

- URL: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/Middleware/HttpOverrides/src/ForwardedHeadersMiddleware.cs>
- Accessed: 2026-08-31

**Q4.18** HostString.Value retains the port whereas HostString.Host strips it  -  and ABP's domain resolver matches on .Value, so a non-default port in Host breaks tenant matching silently.

> Returns the value of the host part of the value. The port is removed if it was present. IPv6 addresses will have brackets added if they are missing.  -  public string Host

- URL: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/Http/Http.Abstractions/src/HostString.cs>
- Accessed: 2026-08-31

**Q4.19** Kubernetes HTTP probes default the Host to the pod IP, and the API documentation explicitly directs users to set Host in httpHeaders instead. This is the documented bare-IP probe problem.

> // Host name to connect to, defaults to the pod IP. You probably want to set
// "Host" in httpHeaders instead.

- URL: <https://raw.githubusercontent.com/kubernetes/api/master/core/v1/types.go>
- Accessed: 2026-08-31

**Q4.20** kubelet's prober implementation confirms the httpHeaders Host mechanism actually works: it assigns Go's Request.Host from the Host entry, which overrides the wire Host header, while connecting to the pod IP.

> host := httpGet.Host
 if host == "" {
  host = podIP
 }
 ... req.Header = headers
 req.Host = headers.Get("Host")

- URL: <https://raw.githubusercontent.com/kubernetes/kubernetes/master/pkg/probe/http/request.go>
- Accessed: 2026-08-31

**Q4.21** Google Cloud health checks default the Host header to the destination IP address, and expose an explicit host field to override it.

> The value of the host header in the HTTP health check request. If left empty (default value), the host header is set to the destination IP address to which health check packets are sent. The destination IP address depends on the type of load balancer.

- URL: <https://raw.githubusercontent.com/googleapis/google-api-go-client/main/compute/v1/compute-api.json>
- Accessed: 2026-08-31

**Q4.22** AWS ALB health checks have NO Host header setting  -  the documented settings list is exhaustive and contains only protocol, port, path, timeouts, thresholds and matcher. This is the concrete 'component that requires a bare-IP health check'.

> HealthCheckProtocol | HealthCheckPort | HealthCheckPath | HealthCheckTimeoutSeconds | HealthCheckIntervalSeconds | HealthyThresholdCount | UnhealthyThresholdCount | Matcher

- URL: <https://docs.aws.amazon.com/elasticloadbalancing/latest/application/target-group-health-checks.html>
- Accessed: 2026-08-31

**Q4.23** CloudFront overwrites the Host header with the origin's domain name by default  -  the CDN violating pattern, stated verbatim.

> Host | CloudFront sets the value to the domain name of the origin that is associated with the requested object.

- URL: <https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/RequestAndResponseBehaviorCustomOrigin.html>
- Accessed: 2026-08-31

**Q4.24** Azure Front Door's portal default sets the origin host header to the origin hostname (overriding the incoming host), while ARM/API creation without the field preserves the incoming host  -  the same product behaving differently by creation method. Health probes are sent to the origin hostname.

> If you use the Azure portal to configure your origin, the default value for this field is the host name of the origin. ... However, if you use Azure Resource Manager templates or another method without explicitly setting this field, Front Door sends the incoming host name as the value for the host header.

- URL: <https://learn.microsoft.com/en-us/azure/frontdoor/origin?pivots=front-door-standard-premium>
- Accessed: 2026-08-31

**Q4.25** Azure Application Gateway documents two explicit host-override settings, and its backend TLS uses the incoming Host as SNI by default.

> Pick host name from backend address  -  This capability dynamically sets the host header in the request to the host name of the backend pool. It uses an IP address or FQDN. ... Host name override  -  This capability replaces the host header in the incoming request on the application gateway with the host name that you specify. ... By default, the application gateway uses the incoming request's host header as the SNI.

- URL: <https://learn.microsoft.com/en-us/azure/application-gateway/configuration-http-settings>
- Accessed: 2026-08-31

**Q4.26** Microsoft's architecture guidance states the host-preservation invariant, warns never to use Host in a security mechanism, gives the multitenant reasoning, documents API Management's host-overriding default, states the health-probe rule directly, and flags NGFW Host-to-IP validation requiring split-horizon DNS.

> We recommend that you preserve the original HTTP host name when you use a reverse proxy in front of a web application. ... Never use the value of the host in a security mechanism. The browser or another user agent provides the value, and a user can change it. ... If the same application deployment accepts requests from multiple domains, for example, in multitenant scenarios, you can't statically define a single domain. ... In most cases, you shouldn't override the host name. ... Because health probes are sent outside the context of an incoming request, they can't dynamically determine the correct host name. Instead, create a custom health probe, turn off Pick host name from back-end HTTP settings, and explicitly specify the host name. ... By default, API Management overrides the host name that's sent to the back end with the host component of the API's web service URL ... This type of firewall might explicitly check whether the HTTP Host header resolves to the target IP address.

- URL: <https://learn.microsoft.com/en-us/azure/architecture/best-practices/host-name-preservation>
- Accessed: 2026-08-31

**Q4.27** Envoy exposes four route-level host rewrite mechanisms, including one driven by a downstream header (with an explicit security warning) and one that maps a path segment into the subdomain.

> string host_rewrite_literal  -  Indicates that during forwarding, the host header will be swapped with this value. ... google.protobuf.BoolValue auto_host_rewrite  -  Indicates that during forwarding, the host header will be swapped with the hostname of the upstream host chosen by the cluster manager. ... string host_rewrite_header  -  Indicates that during forwarding, the host header will be swapped with the content of given downstream or custom header. ... Pay attention to the potential security implications of using this option. Provided header must come from trusted source. ... host_rewrite_path_regex ... This is useful for transitioning variable content between path segment and subdomain.

- URL: <https://raw.githubusercontent.com/envoyproxy/envoy/main/api/envoy/config/route/v3/route_components.proto>
- Accessed: 2026-08-31

**Q4.28** Istio exposes authority (Host) rewriting as a first-class VirtualService field.

> message HTTPRewrite { ... // rewrite the Authority/Host header with this value.
  string authority = 2;

- URL: <https://raw.githubusercontent.com/istio/api/master/networking/v1alpha3/virtual_service.proto>
- Accessed: 2026-08-31

**Q4.29** ingress-nginx preserves Host by default but exposes a single annotation that overrides it per-Ingress.

> Custom NGINX upstream vhost  -  This configuration setting allows you to control the value for host in the following statement: proxy_set_header Host $host, which forms part of the location block. This is useful if you need to call the upstream server by something other than $host.

- URL: <https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/docs/user-guide/nginx-configuration/annotations.md>
- Accessed: 2026-08-31

**Q4.30** GCP URL maps replace the Host header via hostRewrite when path-based routing rewrites are used.

> hostRewrite: Before forwarding the request to the selected service, the request's host header is replaced with contents of hostRewrite.

- URL: <https://raw.githubusercontent.com/googleapis/google-api-go-client/main/compute/v1/compute-api.json>
- Accessed: 2026-08-31

**Q4.31** PortSwigger documents the four ambiguous-Host techniques the edge must reject: duplicate Host headers, absolute-URL request line, line-wrapped/indented Host, and X-Forwarded-Host override.

> GET /example HTTP/1.1 Host: vulnerable-website.com Host: bad-stuff-here  -  Different systems and technologies will handle this case differently, but it is common for one of the two headers to be given precedence over the other one, effectively overriding its value. ... The ambiguity caused by supplying both an absolute URL and a Host header can also lead to discrepancies between different systems. Officially, the request line should be given precedence when routing the request but, in practice, this isn't always the case. ... Some servers will interpret the indented header as a wrapped line ... When an X-Forwarded-Host header is present, many frameworks will refer to this instead.

- URL: <https://portswigger.net/web-security/host-header/exploiting>
- Accessed: 2026-08-31

**Q4.32** OWASP WSTG-INPV-17 documents 'Accessing Private Virtual Hosts'  -  a vhost not resolvable in public DNS is still reachable by setting the Host header. This maps directly onto the reserved 'admin' host-scope slug.

> In some cases a server may have virtual hosts that are not intended to be externally accessible. ... Although it would not be possible to browse directly to intranet.example.org from outside the network (as the domain would not resolve), it may be possible to access to Intranet by making a request from outside with the following Host header: Host: intranet.example.org ... This could also be achieved by adding an entry for intranet.example.org to your hosts file with the public IP address of <www.example.org>

- URL: <https://raw.githubusercontent.com/OWASP/wstg/master/document/4-Web_Application_Security_Testing/07-Input_Validation_Testing/17-Testing_for_Host_Header_Injection.md>
- Accessed: 2026-08-31

**Q4.33** PortSwigger's stated root cause and prevention list, including disabling host override headers and validating against an allow-list.

> HTTP Host header vulnerabilities typically arise due to the flawed assumption that the header is not user controllable. ... Prevention: ... Validating Host headers against whitelists; Disabling host override header support like X-Forwarded-Host; Configuring load balancers to only forward to permitted domains

- URL: <https://portswigger.net/web-security/host-header>
- Accessed: 2026-08-31

**Q4.34** ASP.NET Core's HostFilteringMiddleware validates the RAW Host header (not Request.Host) against an allow-list and returns 400  -  a genuine second check that can diverge from what the ABP resolver sees.

> var host = context.Request.Headers.Host.ToString(); ... return CheckHostInAllowList(middlewareConfiguration.AllowedHosts, host); ... context.Response.StatusCode = 400;

- URL: <https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/Middleware/HostFiltering/src/HostFilteringMiddleware.cs>
- Accessed: 2026-08-31

**Q4.35** Docker Compose health checks run an arbitrary command inside the container, so a Host header can be set freely  -  but the spec's own canonical example uses <http://localhost>, which on this stack sends Host: localhost and returns 'Tenant not found'.

> healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost"] ... `test` defines the command Compose runs to check container health. It can be either a string or a list.

- URL: <https://raw.githubusercontent.com/compose-spec/compose-spec/main/spec.md>
- Accessed: 2026-08-31

---

### Q5. What does per-office (per-tenant) point-in-time restore require in a database-per-tenant SQL Server deployment with a shared host database, and what must the backup architecture therefore provide?

Confidence: **determinate-with-caveats**

**Q5.1** FULL is the only recovery model permitting point-in-time restore; simple cannot, bulk-logged explicitly cannot.

> Simple  -  Can recover only to the end of a backup. Full  -  Can recover to a specific point in time, assuming that your backups are complete up to that point in time. Bulk-logged  -  Can recover to the end of any backup. Point-in-time recovery isn't supported.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/recovery-models-sql-server?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.2** New databases inherit their recovery model from `model`, and edition changes the default  -  which is why an office database created by the host-UI provisioner may silently lack PITR.

> SQL Server Enterprise and Standard editions use the full recovery model by default, while SQL Server Express edition uses the simple recovery model by default. ... The `model` database sets the default recovery model of new databases.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/view-or-change-the-recovery-model-of-a-database-sql-server?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.3** Setting FULL is not sufficient  -  the log chain does not begin until the first data backup; and FULL without log backups fills the disk.

> Immediately after switching to the full recovery model or bulk-logged recovery model, take a full or differential database backup to start the log chain. The switch to the full or bulk-logged recovery model takes effect only after the first data backup. ... Back up your logs. If you do not back up the log frequently enough, the transaction log can expand until it runs out of disk space.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/view-or-change-the-recovery-model-of-a-database-sql-server?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.4** The documented PITR sequence requires an unbroken chain of every subsequent log backup, with STOPAT repeated identically on every RESTORE LOG. NOTE: this page's prose and its own T-SQL example disagree on RECOVERY vs NORECOVERY for intermediate logs; the example is correct.

> As a prerequisite to a point-in-time restore, you must first restore a full database backup whose endpoint is earlier than your target restore time. That full database backup can be older than the most recent full database backup as long as you then restore every subsequent log backup, up to and including the log backup that contains your target point in time. ... In every RESTORE LOG statement of the restore sequence, you must specify your target time or transaction in an identical STOPAT clause.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-a-sql-server-database-to-a-point-in-time-full-recovery-model?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.5** STANDBY is the documented technique for locating an unknown restore point  -  directly useful for finding when a logical error occurred.

> Recommendations  -  Use STANDBY to find unknown point in time. Specify the point in time early in a restore sequence.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-a-sql-server-database-to-a-point-in-time-full-recovery-model?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.6** A restore takes the restored database offline and forces its users off, holding an exclusive lock on that DATABASE object only  -  so other offices on the same instance stay online. Restore also clears that database's plan cache and cannot run inside a transaction.

> During an offline restore, if the specified database is in use, RESTORE forces the users off after a short delay. ... Any data in the specified database is replaced by the restored data. ... RESTORE is not allowed in an explicit or implicit transaction. ... Restoring a database clears the plan cache for the database being restored. ... Locking: Takes an exclusive lock on the DATABASE object.

- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-transact-sql?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.7** A tail-log backup is required before overwriting a database unless REPLACE or STOPAT is used; REPLACE overrides safety checks and can lose committed work  -  in this system that committed work includes the audit trail of the erased period.

> For a database using the full or bulk-logged recovery model, in most cases you must back up the tail of the log before restoring the database. Restoring a database without first backing up the tail of the log results in an error, unless the RESTORE DATABASE statement contains either the WITH REPLACE or the WITH STOPAT clause. ... REPLACE should be used rarely and only after careful consideration. ... With the REPLACE option, you can lose committed work, because the log written most recently has not been backed up.

- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-transact-sql?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.8** SOURCE TRAP, reported deliberately: the 'only one BACKUP or RESTORE at a time' statement in the RESTORE reference belongs to the Analytics Platform System (PDW) section and does NOT apply to box SQL Server. It must not be quoted into an architecture document about concurrent per-database backups.

> Only one RESTORE DATABASE or BACKUP DATABASE statement can be running on the appliance at any given time. If multiple backup and restore statements are submitted concurrently, the appliance will put them into a queue and process them one at a time.

- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-transact-sql?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.9** Backups run online; the only documented concurrency restrictions are file-management and shrink operations  -  nothing forbids concurrent backups of different databases. What binds in this deployment is destination IO/space, CPU for compression, and buffer memory.

> Backup can occur while the database is online and being used. ... During a backup, most operations are possible; for example, INSERT, UPDATE, or DELETE statements are allowed during a backup operation. ... Operations that cannot run during a database backup or transaction log backup include the following: File-management operations such as the ALTER DATABASE statement with either the ADD FILE or REMOVE FILE options. Shrink database or shrink file operations.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/backup-overview-sql-server?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.10** Microsoft's own log-backup cadence guidance supports a 15-minute RPO target.

> Taking a log backup every 15 to 30 minutes might be enough. If your business requires that you minimize work-loss exposure, consider taking log backups more frequently.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/transaction-log-backups-sql-server?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.11** Ad-hoc backups taken to feed a verification restore must be COPY_ONLY, or they silently reseed the differential base and orphan scheduled differentials.

> A copy-only full backup can't serve as a differential base or differential backup and doesn't affect the differential base. ... A copy-only log backup preserves the existing log archive point and, therefore, doesn't affect the sequencing of regular log backups.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/copy-only-backups-sql-server?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.12** REJECTED PATTERN, sourced: marked transactions are SQL Server's only documented mechanism for consistent multi-database point-in-time recovery, and their own constraints disqualify them for 12-34 coordinated databases operated by two SDE 1s.

> You can recover related databases only to a marked transaction, not to a specific point in time. ... this recovery loses any transaction that is committed after the mark ... The stalls generated by marked transactions that span multiple databases can reduce the transaction processing performance of the server. We recommend that you do not run concurrent marked transactions.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/use-marked-transactions-to-recover-related-databases-consistently?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.13** Marks must be written into every related database and do not propagate automatically across instances  -  confirming the coordination burden scales with office count.

> If a marked transaction spans multiple databases on the same database server or on different servers, the marks must be recorded in the logs of all the affected databases. ... A transaction mark name is not automatically distributed to another server as the transaction spreads there.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/recovery-of-related-databases-that-contain-marked-transaction?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.14** Restoring alongside the original on the same instance requires WITH MOVE because the original files exist; the database name may simply be changed at restore time; ownership transfers to the restoring principal; and logins/metadata must be re-created when restoring to another instance.

> It may be necessary to create a copy of an existing database on the same computer for testing purposes. In this case, the database files for the original database already exist, so different file names must be specified when the database copy is created during the restore operation. ... The database name explicitly supplied when you restore a database is used automatically as the new database name. ... the SQL Server login or Microsoft Windows user who initiates the restore operation becomes the owner of the new database automatically. ... you might have to re-create some or all of the metadata for the database, such as logins and jobs, on the other server instance.

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/databases/copy-databases-with-backup-and-restore?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.15** RESTORE VERIFYONLY is necessary but NOT sufficient as verification  -  it explicitly does not verify the structure of the data. Real proof of restorability requires an actual restore.

> Verifies the backup but does not restore it, and checks to see that the backup set is complete and the entire backup is readable. However, RESTORE VERIFYONLY does not attempt to verify the structure of the data contained in the backup volumes.

- URL: <https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-verifyonly-transact-sql?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.16** Restore scenarios by recovery model, and the edition limit on online restore. NOTE: this page carries two mutually inconsistent footnotes about whether the Enterprise limit applies to online restore only or to file/page/piecemeal generally  -  reported rather than resolved.

> Point-in-time restore: Full  -  Any time covered by the log backups. Bulk-logged  -  Disallowed if the log backup contains any bulk-logged changes. Simple  -  Not supported. ... Online restore is supported only in Enterprise edition. ... [second footnote] Available only in the Enterprise edition of SQL Server

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-and-recovery-overview-sql-server?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.17** Edition constraints that kill patterns a larger organisation would reach for: Always On AGs are unavailable on Standard, and basic AGs cover exactly ONE database  -  so no AG design can cover 12-34 tenant databases. Also confirms Developer edition is not licensed for production, Standard supports backup compression, backup encryption and S3 backup, and Express caps a database at 50 GB.

> Always On availability groups  -  Enterprise: Yes, Standard: No ... Basic availability groups  -  Standard: Yes ... A basic availability group supports two replicas, with one database. ... Online page and file restore  -  Enterprise: Yes, Standard: No ... SQL Server Enterprise Developer edition ... includes all the functionality of Enterprise edition, but is licensed for use as a development and test system, not as a production server. ... Maximum relational database size  -  Enterprise 524 PB, Standard 524 PB, Express 50 GB

- URL: <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2025?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.18** SQL Server 2022+ can back up and restore natively to S3-compatible object storage over HTTPS with path-style URLs  -  a capability that matches a component class this team already operates. Includes the easily-missed Linux CA placement requirement and confirms STOPAT/STOPATMARK and MOVE are supported on restore-from-URL.

> SQL Server 2022 (16.x) extends the existing BACKUP/RESTORE TO/FROM URL syntax by adding support for the new S3 connector using the REST API. ... TLS must be configured. It is assumed that all connections will be securely transmitted over HTTPS not HTTP. ... on SQL Server on Linux the CA must be placed on a predefined location to be created at /var/opt/mssql/security/ca-certificates ... The CA must be in place before SQL Server process is started. ... A single backup file can be up to 200,000 MiB per URL (with MAXTRANSFERSIZE set to 20 MB). Backups can be striped across a maximum of 64 URLs. ... Back up to S3-compatible object storage isn't supported in SQL Server Express

- URL: <https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/sql-server-backup-to-url-s3-compatible-object-storage?view=sql-server-ver17>
- Accessed: 2026-08-31

**Q5.19** Object versioning with delete markers is the mechanism that makes a restored database's blob references resolvable  -  deletes become recoverable by version, and lifecycle rules can expire non-current versions on a window that can be aligned to the database PITR window. Caveat: deleting a specific version by ID is permanent.

> MinIO creates a 0-byte DeleteMarker as the latest version of that object ... clients can retrieve any previous version of the object by specifying the version ID, even if the 'Latest' version is a DeleteMarker ... configure a rule to automatically expire object versions 90 days after they become non-current ... Deleting a specific version is permanent and does not result in the creation of a DeleteMarker.

- URL: <https://docs.min.io/community/minio-object-store/administration/object-management/object-versioning.html>
- Accessed: 2026-08-31

**Q5.20** HIPAA contingency-plan obligations: data backup plan and disaster recovery plan are REQUIRED; testing and revision procedures are ADDRESSABLE. The word 'retrievable' in (A) is what a never-tested backup fails to demonstrate.

> (A) Data backup plan (Required): Establish and implement procedures to create and maintain retrievable exact copies of electronic protected health information. (B) Disaster recovery plan (Required): Establish (and implement as needed) procedures to restore any loss of data. (C) Emergency mode operation plan (Required) ... (D) Testing and revision procedures (Addressable): Implement procedures for periodic testing and revision of contingency plans.

- URL: <https://www.law.cornell.edu/cfr/text/45/164.308>
- Accessed: 2026-08-31

**Q5.21** CORRECTION TO THE BRIEF'S PREMISE: 45 CFR 164.316(b)(2)(i) is a six-year DOCUMENTATION retention rule, not a six-year PHI or audit-row retention mandate. Do not size the backup architecture against it, and do not attempt to satisfy it by hoarding backup files.

> Retain the documentation required by paragraph (b)(1) of this section for 6 years from the date of its creation or the date when it last was in effect, whichever is later.

- URL: <https://www.law.cornell.edu/cfr/text/45/164.316>
- Accessed: 2026-08-31

**Q5.22** The recommended backup tooling is current and actively maintained  -  releases dated 16, 9 and 8 August 2026, well inside a 12-month currency check  -  and supports SQL Server 2017 through 2025 with verification, checksum and log backups.

> a set of scripts for running backups, integrity checks, and index and statistics maintenance on all editions of Microsoft SQL Server 2017, SQL Server 2019, SQL Server 2022, and SQL Server 2025 ... Most Recent Version: 16 August 2026

- URL: <https://ola.hallengren.com/versions.html>
- Accessed: 2026-08-31

**Q5.23** The January 2025 HIPAA Security Rule NPRM exists but remains at proposed stage  -  noted as direction of travel toward mandatory testing, not designed to.

> On January 6, 2025, HHS published a notice titled 'HIPAA Security Rule to Strengthen the Cybersecurity of Electronic Protected Health Information - Proposed Rule' in the Federal Register. ... This is currently in proposed rule stage, not yet finalized.

- URL: <https://www.hhs.gov/hipaa/for-professionals/security/index.html>
- Accessed: 2026-08-31

---
