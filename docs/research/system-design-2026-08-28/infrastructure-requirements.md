# Infrastructure requirements

> Vendor-neutral capability requirements for a public-facing, HIPAA-regulated, multi-tenant IME
> scheduling platform serving 11 offices at launch with headroom to 33.
>
> Written so a platform-selection exercise can score candidates against it. **No product is named as
> a recommendation anywhere in this document.** Nothing is priced.

| Field | Value |
| --- | --- |
| Produced | 2026-08-31 |
| Companion | `system-design-target.md` - the architecture these requirements express |
| Inputs | `01-system-architecture.md`, `03-deployment-and-constraints.md` (both verified 2026-08-28). **`02-runtime-and-data-profile.md` was not supplied** |
| Neutrality | Every requirement was audited by a second agent against the test "does a named product appear as a recommendation rather than as evidence or as an example of a behaviour class". **Zero named-product recommendations.** Where a product name survives below, it is the **incumbent component being constrained**, not a selection being made, and it is marked `[incumbent]` |

---

## How to read this

**Each requirement is:**

- **stated as a capability, not a product** - so a platform either exhibits the behaviour or does not;
- **marked MUST or SHOULD, with the reason for the classification** - MUST means a platform failing
  it does not work for this system; SHOULD means failing it is survivable at a stated cost;
- **traceable** - to a section of the input documents, to a section of `system-design-target.md`, or
  to a cited standard;
- **testable** - somebody can determine whether a given platform satisfies it, and the test is stated.

**Groups.** Five, by what drives the requirement: the tenancy model, HIPAA and the regulatory
position, team size and operating capacity, this application's specific mechanics, and the
deployment and release model. **Anti-requirements** - things a platform must **not** do - are in
section 7 and are disqualifiers.

**Read section 8 first if you are running the platform-selection exercise.** It names the
requirements most likely to be failed by a platform that otherwise looks fine, and it says to test
the anti-requirements before the requirements.

**Two conventions:**

- **`[incumbent]`** marks a requirement that constrains a component already in the system rather than
  selecting one. These are not neutrality violations; they are scope. Each states the underlying
  capability so a future replacement can be evaluated against the behaviour rather than the name.
- **`[ordering]`** marks a requirement whose adoption order matters, with a cross-reference. **These
  are the ones where doing the right thing in the wrong order is worse than doing nothing.**

---

## 1. Group 1: driven by the tenancy model

These exist because one tenant is one database and, for anonymous requests, tenancy resolves from the
`Host` header. **They are the requirements a platform is most likely to fail without anyone noticing
until an office is onboarded.**

| ID | Requirement | Class | Why that classification | Traces to | Test |
| --- | --- | --- | --- | --- | --- |
| **REQ-TEN-01** | The request path preserves the client-supplied `Host` header unchanged from the edge to the application process, with its leftmost label byte-identical. Case folding and stripping a default port are permitted; adding, removing or rewriting a label is not | **MUST** | For anonymous requests, tenancy resolves from `Host` and nothing else. A rewrite removes tenant resolution rather than degrading it | `01` s3.1; `03` s3 constraint 2; A s19.4 I1 | Echo the received `Host` from an application endpoint through the full path, for a two-label and a three-label host, and in mixed case |
| **REQ-TEN-02** | The edge routes on hostname with wildcard support at three label depths, and exact-match hostnames outrank wildcards | **MUST** | `{office}.api.<d>` and `{office}.auth.<d>` must beat `*.<d>`, and the object-store hostname must beat all of them. Wrong precedence serves API traffic from the static-file container, which has already happened for the unprefixed `api.<d>` name | `01` s3.1; `03` s4.2 | Resolve one host at each depth plus the exact-match host; confirm the correct backend answers each |
| **REQ-TEN-03** | New databases can be created programmatically at runtime, by an identity the running application holds, without a deployment, a ticket or an operator action | **MUST** | Adding an office is a host-UI action that creates the database if absent. A platform treating database creation as an administrative action rather than an API the application can call **breaks how the business grows** | `01` s3.3; `03` s3 constraint 6 | Create an office through the application UI on the candidate platform; confirm the database appears and seeds |
| **REQ-TEN-04** | The database service supports at least **80** databases on one addressable endpoint, reachable through a single connection-string template differing only in database name | **MUST** | 68 at the 33-office ceiling once audit is separated (A s5), plus the host database, plus restore copies and drill namespaces. **Check the platform's own documented per-endpoint database limit** - engine limits run to five figures while managed offerings can cap in the low hundreds, and the platform's number is the one that binds | `01` s3.2; A s19.1 | Provision 80 databases and connect to each with the derived template |
| **REQ-TEN-05** | Tenant databases can be relocated to a different database endpoint by changing configuration, without a code change | **SHOULD** | This property exists today and is the escape hatch if one endpoint becomes the constraint. SHOULD because losing it is survivable, but it is cheap to preserve and expensive to rebuild | `01` s3.2 | Point the template at a second endpoint; confirm offices resolve there |
| **REQ-TEN-06** | Per-database backup and **per-database point-in-time restore**, performed independently per database while the other databases stay online | **MUST** | Restoring one office is a different operation from restoring an instance. A platform offering only endpoint-level snapshots cannot restore one office without restoring all of them into a copy first, turning a short operation into a multi-hour one | `03` s2.6; A s19.5 | Restore one database to a chosen time while the others continue serving |
| **REQ-TEN-07** | Fleet operations across all tenant databases report **per-tenant** outcome, exit non-zero on any failure, and are resumable after partial failure | **MUST** | The existing migration runner has no per-tenant error handling and leaves the fleet on split schema versions with **no report of which succeeded**. This single artefact is what moves the operational ceiling from roughly 20 offices to roughly 90 | `03` s3 constraint 6; A s19.1 | Interrupt a fleet operation mid-run; the output must name what succeeded and what did not, and re-running must complete the remainder |
| **REQ-TEN-08** | Backup enrolment is performed **at provisioning time**, atomically with database creation: the recovery configuration is set, a first full backup is taken, and provisioning **fails** if either fails | **MUST** | Point-in-time capability is a per-database property inherited at creation, and databases here are created by a **business action in the host UI**. *The business can create a database faster than a backup schedule can notice it exists* | A s19.5 | Provision an office; immediately confirm its recovery configuration and the existence of its first backup, without any scheduled job having run |
| **REQ-TEN-09** | Transaction-log growth is bounded by a log-backup schedule enabled **in the same change** that enables point-in-time recovery | **MUST** `[ordering]` | Point-in-time recovery **without** log backups grows the log until the disk fills. On the current host that is a simultaneous outage of the databases, the object store and the backup directory | A s19.5 | Enable both, then monitor log-reuse wait state for one week |
| **REQ-TEN-10** | DNS supports wildcard records at three label depths and exposes an API a script can drive unattended to create and delete validation records | **MUST** | Three-depth wildcard certificates force a DNS-based challenge, which constrains the DNS provider to one with an automatable API. This is a protocol property, not a vendor property | `03` s2.2 | Issue a certificate covering all four names using unattended DNS-based validation |
| **REQ-TEN-11** | Reserved hostnames in the base domain are enumerable and enforced at office-creation time, and identifiers are never reused in any lifecycle state | **SHOULD** | Two slugs are already reserved. Every infrastructure hostname added to the base domain silently claims an office slug, and a reused slug inherits the retired office's DNS caches, bookmarks and outstanding email links | `01` s3.1; A s17 | Attempt to create an office named after each infrastructure hostname, and after a destroyed office; both must be refused |
| **REQ-TEN-12** | Adding an office requires **no** change to DNS records, TLS certificates, edge or proxy configuration, or firewall rules | **MUST** | This is the property the whole design rests on, and the one most easily lost. Note the resolution it depends on: **the edge validates hostname shape and depth; the application validates membership** | `01` s3.3; A s21.3 | Add an office and diff every infrastructure configuration artefact; the diff must be empty |

---

## 2. Group 2: driven by HIPAA and the regulatory position

These exist because the system stores and transmits PHI. Several are good practice independently;
they are grouped here because the regulation makes them non-negotiable rather than advisable.

**One correction carried from `system-design-target.md` section 3.8, because it changes the sizing of
this whole group:** 45 CFR 164.316(b)(2)(i) requires six-year retention of **the documentation
required by 164.316(b)(1)** - the written policies and procedures implemented to comply with the
Security Rule, and written records of actions, activities or assessments the subpart requires to be
documented. **An application audit log is not that.** 45 CFR 164.312(b) requires the audit mechanism
and states **no retention period**. The real six-year *data* obligation is 45 CFR 164.528, an
accounting of disclosures - whose exclusions do **not** cover workers'-compensation disclosures, which
are this platform's core business.

| ID | Requirement | Class | Why that classification | Traces to | Test |
| --- | --- | --- | --- | --- | --- |
| **REQ-HIP-01** | Every provider that can access, store or transmit PHI has executed a business associate agreement before real patient data reaches it | **MUST** | A statutory precondition, not an architectural preference. It governs compute, database, object storage, backup destination, log destination and mail relay equally | `03` s3 constraint 1 | Enumerate **every** service in the data path - including the log destination and the backup destination - and confirm coverage of each in writing |
| **REQ-HIP-02** | Data at rest is encrypted, including database files, backups, object storage and the audit archive | **MUST** | Encryption at rest is an addressable specification, meaning it must be implemented or the decision to use an equivalent alternative documented. For a team this size, implementing it is far cheaper than documenting why not | 45 CFR 164.312(a)(2)(iv); see section 9.10 on the missing safeguard inventory | Confirm encryption status on each store **and on a restored backup** |
| **REQ-HIP-03** | Data in transit is encrypted on every hop, including application to database and application to object store, with **server certificate validation enabled** | **MUST** | The current deployment disables server certificate validation on the database connection. That is tolerable on a private container network and is **not** tolerable once the database is reachable off-host | `03` s4.4 | Attempt an unencrypted connection to each store, and a connection presenting an untrusted certificate. Both must be refused |
| **REQ-HIP-04** | A written audit-and-retention policy exists, states what is captured, why, for how long and how often it is reviewed, and **is itself retained for six years** | **MUST** | This is the artefact 164.316(b)(2)(i) actually obliges the organisation to retain. It is measured in kilobytes and it is currently absent | A s3.8, s19.2 | The document exists, is dated, and names a retention period with its justification |
| **REQ-HIP-05** | Audit records within their declared retention are protected against modification and deletion by any credential the running deployment holds | **MUST** | Retention without integrity protection does not produce a defensible record. Note this is **weaker and more precise** than "six years": the period is a risk-based decision recorded under REQ-HIP-04 | A s19.2 | Attempt to modify and to delete an archived audit record using the application's own credentials. Both must fail |
| **REQ-HIP-06** | The platform provides a storage mode in which **no credential available to the deployment, including its most privileged one, can delete an object or shorten its retention before expiry** | **MUST** | This is the behaviour class that makes an archive defensible. Stated behaviourally rather than in one product's retention vocabulary, so that any implementation exhibiting it qualifies | A s5 | Attempt deletion and retention-shortening with the most privileged credential the deployment holds. Both must fail |
| **REQ-HIP-07** | HIPAA audit logging and application logging are separate pipelines with independent retention, access control and integrity guarantees | **MUST** | They answer different questions for different audiences under different obligations. Merging them forces the shorter retention onto the longer obligation, or the stricter access control onto routine debugging | A s12 | Confirm two destinations, two retention settings, two access-control lists |
| **REQ-HIP-08** | Application logs can be shipped off-host with PHI either removed before egress or the destination covered by an agreement | **MUST** `[ordering]` | PII logging is enabled by default, so logs currently carry PHI and cannot leave the machine as they are. **This blocks centralised logging, and centralised logging is a prerequisite for alerting** | A s12 | Ship a log sample and grep it for known synthetic identifiers |
| **REQ-HIP-09** | Access to the data zone is authenticated per human identity, not by a shared account, and that access is itself recorded | **MUST** | Unique user identification is required. A single shared superuser credential in a file on the box satisfies neither the identification nor the recording requirement. **See REQ-APP-14 for why the naive fix breaks onboarding** | `03` s4.4, s4.9 | Review the access record for a database session; it must attribute to a person |
| **REQ-HIP-10** | The database provides an audit trail of privileged and schema activity **independent of the application**, because the application currently connects with a single shared superuser account | **SHOULD** | Independent because an application-level audit trail written by the same identity that could tamper with it proves less. SHOULD rather than MUST because REQ-APP-14's identity split materially reduces the exposure it covers | `03` s4.4 | Perform a schema change and confirm it appears in a record the application cannot write |
| **REQ-HIP-11** | Per-scope encryption keys are supported such that destroying one scope's key renders that scope's backups undecryptable, and key destruction is evidenced | **SHOULD** `[ordering]` | This is the only mechanism by which "destroy this office's data" reaches backup media already off the platform - and sanitization guidance conditions crypto-erase on the data never having been stored in plaintext, so **every backup taken before it is enabled stays permanently outside the shred.** SHOULD only because no office has left yet; **its window closes at the first real booking** | A s17 | Enable per-scope encryption at provisioning, take a backup, destroy the key, and confirm the backup cannot be restored |
| **REQ-HIP-12** | Restore from backup is rehearsed on a schedule and the rehearsal produces a dated record | **MUST** | Contingency planning includes testing and revision procedures. **No restore has ever been tested here**, which means there is currently no evidence that a backup is a backup | `03` s2.6 | Perform a restore into a scratch database, record the elapsed time and outcome, and retain the record |
| **REQ-HIP-13** | Administrative and job-management surfaces are unreachable from the public internet regardless of application-level authorisation | **MUST** | An unauthenticated administrative console with antiforgery disabled is mounted in the production path. A code fix is planned; the architectural point survives it - **the edge cannot assume every application route is either public by design or authenticated** | `03` s4.0; A s16 | Request the administrative path from outside. It must fail **at the edge**, before the application sees it - a 401 has confirmed the path exists |
| **REQ-HIP-14** | The platform's region can be constrained and demonstrated, and no service silently replicates data outside it | **SHOULD** | California workers' compensation. Residency has not been confirmed as a requirement (`03` s7 Q13 is open), so this is SHOULD until the business answers. **See section 9.4: state-law obligations are an open gap** | `03` s7 Q13 | Confirm the configured region for every service, and check each service's default replication behaviour |

---

## 3. Group 3: driven by team size and operating capacity

These exist because **two SDE 1 developers operate this system** - no senior engineer, no architect,
no DevOps engineer, no security specialist, no QA, no on-call rotation - at roughly one
developer-week a month for infrastructure work.

**The figure is an estimate that has never been confirmed against a staffing commitment.** Thresholds:

| Available hours per month | What holds |
| --- | --- |
| **40** | The full recommendation is operable, but the **steady state alone** across all areas is 24.5 to 43.5 hours as sized (`system-design-target.md` section 2). At the top of that range there is no room for setup or features at all; at the bottom, very little |
| **20** | The MUSTs here that need recurring human attention begin to decay: restore rehearsal slips, alert fatigue sets in, and the platform's own maintenance backlog becomes the constraint |
| **10** | Only a platform operable **through neglect** remains viable. REQ-TEAM-02, REQ-TEAM-05 and ANTI-13 become the dominant selection criteria and several SHOULDs here become MUSTs |

| ID | Requirement | Class | Why that classification | Traces to | Test |
| --- | --- | --- | --- | --- | --- |
| **REQ-TEAM-01** | Component failure presents as a hard error, not as degraded service | **MUST** | This team will not notice slow degradation and nobody is paged. A component that half-works produces an incident discovered by a customer | Brief consequence 2 | Disable each optional dependency in turn. **A silent partial success is a failure** |
| **REQ-TEAM-02** | The platform provides managed backup, patching and availability for stateful services, so that no team member is responsible for database engine maintenance | **MUST** | Engine patching, backup scheduling and failover are specialist work. The team has no specialist, and the alternative is that it does not happen | Brief "who this is for"; `03` s4.5 | Confirm the provider's responsibility boundary in writing, and confirm patches apply without a team action |
| **REQ-TEAM-03** | Deployment is a single automated action with health gating and an automatic halt on failure | **MUST** | Today deployment is a manual procedure with two documented traps that each silently break the stack. A procedure whose correctness depends on remembering a flag will eventually be executed wrongly | `03` s2.4 | Deploy an intentionally broken image. The deployment must halt and the previous version must continue serving |
| **REQ-TEAM-04** | Alerting reaches a human for a defined set of conditions, and the set is **small enough that every alert is acted on** | **MUST** | There is no alerting of any kind today. On a two-person team, a large alert set produces the same outcome as no alerts. **See section 9.3: there is currently no human on the end of this, and that must be written down rather than assumed away** | `03` s4.6; A s12 | Trigger each defined condition and confirm delivery to a person |
| **REQ-TEAM-05** | Routine platform maintenance imposed on the team is bounded, predictable and published in advance | **MUST** | A platform whose mandatory upgrade cadence alone consumes the available hours leaves nothing for remediation or features | Brief "who this is for" | Estimate annual mandatory-maintenance hours from the platform's own support and deprecation policy. **Above roughly 40 hours a year it is competing with the entire backlog** |
| **REQ-TEAM-06** | Infrastructure configuration is declarative, version-controlled, and reproducible into a second environment | **SHOULD** | Reproducibility is what makes a second environment honest. SHOULD rather than MUST because a documented, tested manual procedure is an acceptable interim - and because **over-automating is a real failure mode for a team this size** | A s13; `03` s4.8 | Build a second environment from committed configuration alone, with no undocumented step |
| **REQ-TEAM-07** | Secrets are stored in a managed store with access control and a change record, delivered to processes as mounted files rather than environment variables | **MUST**, with a **named exception** | A single mode-600 file, with values known to have been pasted into chat transcripts, no rotation mechanism and no access record. **The exception, recorded rather than implicit:** the database and object-store containers accept credentials only by environment variable. Compensating controls: neither is reachable from the edge zone, and both are on the shortest rotation cycle. A requirement with an undocumented exception is a requirement nobody can test | `03` s4.9; A s21.14 | Rotate one secret without editing a file on a server, and produce the record of the change. Separately, enumerate every process still using an environment variable and confirm it is on the recorded exception list |
| **REQ-TEAM-08** | The platform's failure modes are diagnosable from its own console by somebody who did not configure it | **SHOULD** | Two people, one of whom may be on leave. A platform requiring specialist tooling or tribal knowledge to answer "why is it down" is a single point of failure independent of its uptime | `03` s7 Q11 | Have the developer who did **not** configure it diagnose an injected failure |
| **REQ-TEAM-09** | A second environment exists with the same shape as production, provisioned by the same mechanism | **MUST**, sequenced | There is exactly one environment today. **The non-negotiable part is the deployment mechanism**: if a change reaches non-production by a different route than production, the environment has tested the application and not the deployment - and the deployment is where both traps live. **Sequencing:** `system-design-target.md` section 9 defers a standing second environment behind four cheaper steps, because production is a legitimate destructive-test target while data is synthetic and will not be later | `03` s4.8 | Confirm the two differ only in size, data and domain, that the difference is declared, and that the deploy path is byte-identical |
| **REQ-TEAM-10** | The non-production environment contains **no PHI**, and refreshing it does not require copying production data | **MUST** | Free to adopt today because every record is synthetic. Once real practices go live, "refresh from production" becomes a PHI transfer requiring production controls. **Decide before it becomes convenient to do it wrongly** | `03` s6.1 | Confirm the non-production seed is synthetic and generated, not restored |

---

## 4. Group 4: driven by this application's specific mechanics

These exist because of particular decisions inside this codebase. **They are the requirements a
platform-selection exercise is most likely to omit, because nothing about them is generic.**

| ID | Requirement | Class | Why that classification | Traces to | Test |
| --- | --- | --- | --- | --- | --- |
| **REQ-APP-01** | A store is available that holds **cryptographic key material** for the payload-protection subsystem, shared by at least two processes under one key-isolation identity, and **that cannot evict, expire or discard it under memory pressure or during provider-initiated maintenance** | **MUST** `[ordering]` | Losing this key material makes already-protected payloads permanently undecipherable, with no override: every session invalid and every in-flight email-confirmation link broken. **This is not a cache.** The target design moves it out of the cache tier entirely into the host database (`system-design-target.md` s7), and **the move must precede any eviction-policy change on the cache** - doing it in the other order silently destroys keys weeks before anyone notices | `01` s4, s6; A s7, s21.1 | Destroy and recreate all compute without restoring anything else. **Authenticated users must remain authenticated.** Separately, drive the cache tier to its memory ceiling and confirm the key material is unaffected |
| **REQ-APP-02** | A distributed cache and a distributed lock are available, and after REQ-APP-01 both may be **fully disposable** | **MUST** | Cache loss is self-healing; the application already falls back to the source. Lock loss is a bounded correctness window with no application acquisition sites today. **The value of this requirement is what it stops being:** once the key material moves, the cache tier needs no durability guarantee at all, which removes a whole class of platform constraint | `01` s6; A s7 | Flush the cache under load; the application must degrade and repopulate rather than error |
| **REQ-APP-03** | The token-signing credential persists across deployments, is present before the authorisation server starts, is never baked into an image, and **more than one such credential can be held with deterministic selection between them** | **MUST** | Replacing it invalidates every issued token - survivable once. Losing it without a replacement means the authorisation server will not start. The multiple-credential capability turns rotation from an event into a selection | `01` s4; `03` s2.5; A s11 | Redeploy without re-supplying the credential: the server must start and existing tokens must validate. Then rotate by promoting a standby and confirm no outage |
| **REQ-APP-04** | The build environment can hold and use commercial licence and package-feed secrets, supplied as build-time secret mounts that do **not** persist into image layers or build provenance | **MUST** | A paid licence is required to build the images at all, so **a build system that cannot hold secrets cannot build this application.** It also puts the build system in scope for access control, which is easy to forget | `03` s3 constraint 4; A s11 | Build an image from a clean checkout, then inspect image history and provenance for the secret values |
| **REQ-APP-05** | The database engine is the incumbent engine `[incumbent]`, or a service fully compatible with it, able to apply the existing migration sets unmodified | **MUST** | 90 host and 15 tenant migrations already exist against it. **Changing engine is a rewrite, not a configuration change.** Underlying capability: the platform must run the application's existing schema-migration artefacts without translation | `03` s3 constraint 5 | Apply both existing migration sets unmodified against a fresh database |
| **REQ-APP-06** | The chosen database product tier permits **production use** and supports every feature the existing databases persist a dependency on | **MUST** | The current deployment runs a tier licensed for development and test only, so **everything built and measured to date has run with higher-tier features silently available.** Tier changes remove capabilities without raising an error - among them in-engine per-workload resource governance and online page-level restore. This is a **go-live blocker**, not a later cost decision | `03` s4.3; A s3.6, s19.1 | Query the persisted feature dependencies of every database and diff against the candidate tier's feature list, **before** choosing the tier |
| **REQ-APP-07** | The chosen tier's **per-endpoint** memory and CPU limits are recorded as architectural constraints and are adequate for the whole fleet sharing one endpoint | **MUST** | Under database-per-tenant a per-database size cap looks generous while a **per-endpoint** cache limit is shared across all databases and is the number that actually binds. A per-database sizing exercise will not surface it | A s19.1 | Divide the tier's per-endpoint memory limit by the projected database count. **If the per-database share is small enough to make working sets compete, the tier is excluded** |
| **REQ-APP-08** | An object-store data-plane API is available supporting **path-style addressing** - the bucket in the URL path, not in the hostname - and independently scoped credentials for an external party | **MUST** | Path-style is forced because a wildcard certificate covers a single label, so bucket-in-hostname addressing fails TLS. An external partner already holds scoped credentials granting fetch-by-key while withholding listing, and re-credentialing them happens on their schedule | `01` s8 | Fetch by key path-style; confirm a scoped credential can fetch but not list; confirm the administrative console is on a different surface |
| **REQ-APP-09** | Exactly **one** process runs the background job processing server in steady state, and it is separately deployable and restartable | **MUST**, with a **named exception** | Enqueue-once is not execute-once: with a five-minute invisibility window and sweeps that open a connection per office, a slow sweep can be re-fetched by a second server while the first still runs - and the jobs in question **send patient and attorney email and post to a partner system.** **The exception, recorded rather than implicit:** during a supervised, time-boxed cutover both tiers may run against shared job storage for one cycle, specifically to observe that each recurring job fires once | `01` s7; A s19.3, s21.10 | Scale the application to two instances; confirm each recurring job fires exactly once and that the second instance registers no processing server |
| **REQ-APP-10** | Runtime database creation is performed by an identity **distinct from** the identity the application uses for normal data access | **MUST** | The causal chain matters: office-onboarding-as-a-UI-action forces runtime database creation, which requires elevated permissions, which produced the single shared superuser account. **Anyone who treats that account as sloppiness and simply demotes it will break the ability to add an office.** The platform must let both identities exist and let the application hold both with different scopes | `03` s4.4; A s4 | Provision an office with the elevated identity and serve traffic with the lesser one; confirm the lesser identity cannot create a database |
| **REQ-APP-11** | Outbound authenticated mail relay is available, with sender reputation and link hostnames under the same registrable domain as the application | **MUST** | Account setup, invitations, confirmations, consent links and reset links all depend on deliverable mail, and a sibling project has already lost mail to filtering on raw-address links | `03` s3 constraint 8; `01` s9 | Send to a filtered mailbox provider and confirm **inbox** delivery, not merely acceptance by the relay |
| **REQ-APP-12** | Health checks distinguish process liveness from dependency readiness, and both are reachable **without traversing the edge and without tenant resolution** | **MUST** | A bare address returns "Tenant not found", and the API's health gate depends on the packet renderer at startup, so a dependency failure and a process failure are different events. Binding these to the unpublished administrative listener is what dissolves the otherwise-contradictory requirements on health probes | `01` s5, s12; A s21.4 | Fail one dependency; readiness must name it while liveness stays green |
| **REQ-APP-13** | No component in the path requires session affinity | **SHOULD** | There is no server-side rendering, no realtime channel and no in-process session state; the proxy's upgrade configuration refers to a hub that does not exist. SHOULD because this is a property to **preserve**, not to acquire | `01` s2.2, s9, s11 | Restart the serving instance mid-session; the user must stay authenticated |
| **REQ-APP-14** | Connection-pool ceilings are explicitly configurable per connection string | **SHOULD** | Each office gets its own pool because each connection string differs. **A documented mitigation for pool fragmentation across many databases - connect once and switch database by statement - is architecturally unavailable here**, because tenancy is expressed as the connection string's catalog and there is no raw SQL in the codebase. So this is a cost to size for, not a defect to fix | `01` s3.2, s7; A s14 | Set an explicit ceiling and confirm it is honoured per tenant |
| **REQ-APP-15** | Egress to the partner endpoint and the mail relay is controllable and observable | **SHOULD** | Both directions of the partner integration fail closed, which is correct, but a transport failure currently records into an outbox nobody watches | `01` s10 | Block egress and confirm the failure surfaces somewhere a human looks |
| **REQ-APP-16** | Internal service addressing re-resolves rather than caching an address at process start | **MUST** | This is both an operational trap and **the binding blocker on running a second application instance**: the proxy never learns the second container's address, so it receives zero traffic, the deploy looks successful, and every conclusion drawn about multi-instance behaviour is drawn from a system still running one instance | `03` s2.4 trap 2; A s19.3 | Replace a backend instance without touching the router. Traffic must continue, **and a newly added instance must receive requests** |

---

## 5. Group 5: driven by the deployment and release model

A platform can pass every other group and still make releases dangerous.

| ID | Requirement | Class | Why that classification | Traces to | Test |
| --- | --- | --- | --- | --- | --- |
| **REQ-REL-01** | The platform runs container images built elsewhere, without requiring a source build on the platform | **MUST** | Everything is already containerised with production-target images. The build needs commercial licence secrets (REQ-APP-04) and belongs in the build system | `03` s6.6 | Deploy a pre-built image from a registry |
| **REQ-REL-02** | A one-shot task can run to completion and long-running services can be gated on its success | **MUST** | The migrator runs on every bring-up and both application processes gate on it completing. A platform with no run-once-then-exit concept cannot express this ordering | `01` s5 | Deploy with a deliberately failing migration; the application services must not start |
| **REQ-REL-03** | Deployment is atomic per release, and a failed release leaves the previous version serving | **MUST** | There is no rollback today other than redeploying an older commit by hand | `03` s2.4 | Deploy a broken image; the previous version must continue serving |
| **REQ-REL-04** | Configuration and secrets are supplied at deploy time from outside the image, and a **missing or empty** value fails the deployment rather than starting with a blank | **MUST** | The current tooling substitutes an unset variable with an empty string, so omitting one flag recreates the stack with no database password, no TLS paths and no base domain - **and it starts successfully.** Starting successfully with blank secrets is the worst available behaviour and it is the current behaviour | `03` s2.4 trap 1; A s11 | Omit one required value and deploy. **The deployment must fail, not start.** Then supply it as an empty string and confirm the same |
| **REQ-REL-05** | Schema migrations can run across all tenant databases as a gated release step with per-tenant status | **MUST** | Combines REQ-TEN-07 and REQ-REL-02. This is the operation that binds first as office count grows, and it is the operation with no error handling today | `03` s3 constraint 6; A s19.1 | Run a migration across the fleet with one database made unreachable. The release must halt and name it |
| **REQ-REL-06** | The build fails when the application's object-relational model contains changes not represented by a committed migration, checked **independently for the host schema and the tenant schema** | **MUST** | An entity mapped in both contexts needs a migration in both, and forgetting one produces an office database missing a table, surfacing as a runtime exception. **A first-party command for exactly this check already exists in the framework** - it is one line in CI | `01` s3.4; A s4 | Add a model change without a migration in one schema only. The build must fail |
| **REQ-REL-07** | Rolling replacement of application instances is possible without dropping in-flight requests | **SHOULD** | It turns a deployment from an outage into a non-event. SHOULD because a short maintenance window is acceptable at this scale, and because it **additionally** requires expand-and-contract schema discipline in the application - a code change, not a platform capability | `01` s5; A s19.3 | Deploy during sustained traffic; confirm zero failed requests |
| **REQ-REL-08** | The image registry is private, access-controlled, and retains enough history to redeploy a previous release | **SHOULD** | The images hold the application but not secrets, so exposure is lower-severity than a secret leak. Retention matters because "redeploy the previous version" **is** the rollback plan | `03` s6.6 | Redeploy the previous release tag from the registry alone |
| **REQ-REL-09** | Stateful volumes are bound to an explicitly declared stack identity, not derived from a directory name or other incidental context | **MUST** | The compose project name currently derives from the checkout directory's basename, so renaming or relocating the checkout **orphans every stateful volume** - including the databases | A s13 | Rename the deployment directory and redeploy. All state must still be attached |
| **REQ-REL-10** | Third-party images are pinned by digest and built images tagged by commit | **SHOULD** | Without both there is no rollback artefact and no guarantee that redeploying the same tag deploys the same bytes | A s13 | Redeploy a prior release and confirm byte-identical images |

---

## 6. Requirements that are the application's, not the platform's

Named here so the platform-selection exercise does not score against them, and so they are not lost.
Each is a MUST for go-live and none is a platform capability.

| ID | Requirement | Why it is here |
| --- | --- | --- |
| **APP-OWN-01** | The custom domain tenant resolver is ordered ahead of the query-string, route, header and cookie resolvers, and this is asserted by an automated test | The resolver chain is first-match with a hard break, and the domain contributor marks the chain handled unconditionally when a host is present - **so correct ordering makes the others unreachable whether or not anyone removed them.** No platform can supply this |
| **APP-OWN-02** | A request with an **empty** `Host` field value does not reach tenant resolution | An empty host value is specification-legal and the server accepts it; the domain contributor then abstains and the chain falls through. **This survives correct ordering.** The edge should also reject it (ANTI-15), but the application must not depend on the edge |
| **APP-OWN-03** | A token minted for one office is refused at another office's API | The first resolver in the chain reads the tenant from the token claim, so **the hostname selects and the token must independently authorise.** Hostname is a routing signal, not an authorisation one |
| **APP-OWN-04** | Untyped distributed-cache writes are eliminated or proven tenant-scoped | The typed cache prefixes tenant-scoped keys; untyped usage does not. One untyped write of a permission grant is a cross-office authorisation leak **that produces no error** |
| **APP-OWN-05** | A tenant-scoped background job whose argument carries no tenant **refuses to run** rather than resolving to the host database | Twelve jobs iterate offices and three run every fifteen minutes. The current fallback writes office-shaped data into the host database silently |
| **APP-OWN-06** | Office provisioning is guarded by a distributed lock keyed on the office identifier | Idempotent is not concurrency-safe. This also stops a double-clicked provision button, so it is worth doing at one instance |
| **APP-OWN-07** | Entity-history capture is an explicit allow-list, and history is not saved on navigation-property changes | The cheapest order of magnitude available, and **every downstream infrastructure cost is linear in the audit ratio.** Do it before sizing any store |
| **APP-OWN-08** | An append-only **disclosure ledger** exists as domain data, carrying the content elements 45 CFR 164.528(b) specifies, retained six years | This is the **real** six-year data obligation, and workers'-compensation disclosures are not excluded from it. It is tiny, business-meaningful, and survives audit pruning |
| **APP-OWN-09** | A nightly per-office assertion fails loudly when the newest audit record is older than a threshold while the application served traffic | Audit errors are hidden by default and the audit write happens on a separate unit of work after the business one commits, so **a business transaction can commit while its audit record silently does not.** See section 9.2 for what this does and does not catch |
| **APP-OWN-10** | Event-bus handlers whose correctness depends on delivery are moved onto the durable outbox that already exists | The default distributed event bus is in-process, so events published on one instance are invisible to another. This is a prerequisite for more than one instance |

---

## 7. Anti-requirements: what the platform and its components must NOT do

**Test these before the requirements.** Every one is a disqualifier, most are testable in under an
hour, and they are the ones most likely to be violated by a **default setting** rather than by a
deliberate choice. Scoring a platform highly on fifty requirements and then discovering it rewrites
`Host` is wasted work.

| ID | The platform or component MUST NOT | Why | How to test |
| --- | --- | --- | --- |
| **ANTI-01** | Rewrite, override, normalise across labels, or replace the `Host` header anywhere between the client and the application | For anonymous requests this removes tenancy rather than degrading it. **This is the default behaviour of several reverse proxies and of most content-delivery origin configurations, so it must be actively disproven, not assumed** | Send a three-label host through the full path to an endpoint that echoes the received host. It must return unchanged |
| **ANTI-02** | Require the application to be reachable by bare address, or by any hostname outside the office naming scheme | A bare address returns "Tenant not found", so any platform feature that probes, warms or routes by address will report a healthy application as unhealthy | Attempt every platform-mandated probe. **If any cannot be given a custom host, the platform fails** - unless it can be pointed at the unpublished administrative listener instead (REQ-APP-12) |
| **ANTI-03** | Introduce a non-default port into the host value as the application sees it | The tenant matcher reads the **raw** host value, **which includes the port**, so a port-shifting hop produces a **silent whole-tenant outage rather than an error** | Route through a non-443 listener or a port-mapped container and confirm tenant resolution still succeeds |
| **ANTI-04** | Collapse multiple tenants onto a single hostname, by path prefix, query parameter or shared origin hostname | Removes the only tenant signal available to anonymous requests, and defeats the isolation argument the whole model rests on | Inspect the routing configuration for any rule matching on path where a hostname rule is required |
| **ANTI-05** | Require a separate certificate per office, or fail to support SANs at three wildcard depths plus the apex | A wildcard matches exactly one label, so `*.<d>` does not cover `office.api.<d>`. Per-office certificates would make adding an office a certificate operation, destroying REQ-TEN-12. **Note this needs one multi-SAN certificate, not three certificates** | Issue and install one certificate covering all four names; verify each resolves without a name mismatch |
| **ANTI-06** | Strip or fail to set the forwarded-protocol header on requests reaching the application | The authorisation server's transport-security check and its issuer URLs depend on the original scheme surviving termination, and losing it produces authentication failures whose message does not name the cause | Authenticate end to end through the edge. A working login is the test; there is no cheaper one |
| **ANTI-07** | Introduce a new hostname in the base domain without treating it as a claimed office slug | Two slugs are already reserved, one by an exact-match rule that outranks every wildcard. A status page at `status.<d>` silently makes `status` unavailable as an office | Maintain the reserved list as configuration; attempt to create an office named after each infrastructure hostname |
| **ANTI-08** | Place PHI-bearing telemetry, logs, traces or crash dumps in a service not covered by an agreement | PII logging is enabled by default, so **application logs currently carry PHI and any log shipper is a PHI processor until proven otherwise** | Enumerate every destination receiving application output; each needs coverage or a demonstrated redaction step ahead of it |
| **ANTI-09** | Require a single shared administrative credential for routine application data access | Every service currently connects with one superuser account shared by the application, the migrator and the authorisation server. **But see REQ-APP-10: the fix is to split the identities, not to demote the one** | Provision two identities with different rights and run the application on the lesser one |
| **ANTI-10** | Silently continue when a per-tenant operation fails partway through a fleet | The existing migration runner does exactly this. **Any platform tooling that inherits the pattern inherits the incident** | Kill a single tenant operation mid-fleet. The result must name the failed tenant and the completed set, and exit non-zero |
| **ANTI-11** | Make the failure of a non-essential component present as slow degradation rather than an error | This team will not notice slow degradation and has no rotation. **A component that half-works is worse here than a component that stops** | Disable each optional dependency and observe. A silent partial success is a fail |
| **ANTI-12** | Store key material for payload protection, or the token-signing credential, in ephemeral, per-instance, or image-baked storage | These are the two artefacts whose loss is unrecoverable rather than inconvenient | Destroy and recreate every compute instance without restoring from backup. Users must remain authenticated and the authorisation server must start |
| **ANTI-13** | Impose routine maintenance exceeding the team's available hours | Roughly one developer-week a month is available and the figure is unconfirmed. A platform whose upgrade cadence alone consumes it is not operable here regardless of its merits | Estimate annual mandatory-maintenance hours from the platform's published support and deprecation policy. **Above roughly 40 a year it is competing with the entire backlog** |
| **ANTI-14** | Require trusting a client-supplied forwarded header without a stable way to identify the trusted proxy | Trusting forwarded headers safely requires pinning the trusted proxy - and pinning it **by address** is as brittle as the address-caching problem in REQ-APP-16, because container addresses change on every rebuild. **Pin by network range. Clearing the trusted-proxy list entirely, which is the common remedy, is forbidden** | Rebuild the proxy container; confirm the client address is still correct and that a client-supplied forwarded header from outside is still ignored |
| **ANTI-15** | Forward a request carrying an empty `Host` field value, more than one `Host` header, or an alternative host header supplied by a client | An empty host value is specification-legal, the server accepts it, and it reaches the fallback tenant resolvers. More than one host header must draw a 400. Alternative host headers must be stripped inbound, or any middleware that later honours one has been handed tenant selection | Send each of the three shapes through the edge; all three must be rejected or normalised before the application sees them |
| **ANTI-16** | Require a control plane, agent or sidecar in the request path whose routing decisions read the request authority | Components that route on the request authority are exactly the class that rewrites it, and where that behaviour is a version-dependent feature flag it cannot be assumed stable across upgrades | Read the component's documented default and **assert the behaviour empirically**, then re-assert after every version upgrade |
| **ANTI-17** | Make office provisioning depend on any infrastructure change - a DNS record, a certificate, an edge rule, a firewall rule, or a deployment | This is REQ-TEN-12 stated as a prohibition, because it is the property most easily lost by accident. **A platform that requires a per-office edge entry has converted a business action into a deployment** | Add an office and diff every infrastructure configuration artefact. The diff must be empty |

---

## 8. How to score a platform against this document

**Three notes on using these.**

### 8.1 Test the anti-requirements first

Every one is a disqualifier and most take under an hour. Section 7, in that order. **ANTI-01 alone
eliminates more candidate configurations than any requirement in sections 1 to 5**, and it fails
invisibly: the application returns "Tenant not found", which reads as a configuration mistake rather
than a platform incompatibility, so a team can spend a week debugging their own DNS.

### 8.2 The discriminating requirements

Most candidates will satisfy most MUSTs. These are the ones worth testing early, because they are the
ones a platform can fail **while looking fine**.

| Likely discriminator | Why it discriminates |
| --- | --- |
| **REQ-TEN-01 / ANTI-01** - host preservation | A default behaviour in several products, and it fails invisibly |
| **REQ-APP-01** - a store that cannot evict key material | Many managed key-value offerings are **explicitly cache-only**. It will work perfectly until the first provider maintenance window logs everyone out. Note the target design removes this dependency by moving the key material into the database - **a platform failing this is disqualified only if that move has not happened yet** |
| **REQ-TEN-04 / REQ-TEN-06** - many databases, independently restorable to a point in time | Where the tenancy model meets the platform hardest. Some managed database products make per-database point-in-time restore awkward, or make 80 databases impractical on one endpoint. **Check the platform's own documented figure; the engine's limit is not the platform's** |
| **REQ-TEN-03** - runtime database creation by the application's own identity | Platforms treating database creation as an administrative action rather than an API break how the business adds an office |
| **REQ-APP-06 / REQ-APP-07** - production-licensed tier with the persisted features, and adequate per-endpoint limits | **A go-live blocker being mistaken for a later cost decision.** Run the persisted-feature query before choosing a tier, and divide the per-endpoint memory limit by the database count |
| **REQ-HIP-01** - agreement coverage | Not a technical property and the easiest to assume rather than confirm. It must cover **every** service in the path, including the log destination and the backup destination |
| **REQ-HIP-06** - retention no credential can shorten | Frequently offered in a weaker form where a sufficiently privileged credential *can* delete. **Test with the most privileged credential the deployment holds, not a scoped one** |
| **ANTI-13 / REQ-TEAM-05** - bounded maintenance | Never advertised, and the requirement most likely to be the real long-run cost |

### 8.3 Three scoring conventions

**A requirement met by a paid tier is met.** These are capability requirements and this exercise
prices nothing. Where a capability exists only above a certain tier, record it as "met, at a tier"
and let the costing exercise decide - but **do not** record it as met if the capability is absent at
every tier.

**A requirement met by a workaround is met conditionally, and the workaround is the finding.** Record
what the workaround costs in ongoing hours and score it against ANTI-13, because a platform that
satisfies everything through five workarounds has failed the requirement that matters most.

**Score the `[ordering]` requirements as a set, not individually.** REQ-APP-01, REQ-TEN-09 and
REQ-HIP-08 each depend on something else landing first. **A platform that makes the right change
easy but the ordering hard is a worse platform than its score suggests**, because the failure mode of
wrong ordering here is silent.

---

## 9. Known gaps in this requirements list

Stated so nothing here is mistaken for complete.

**9.0 Where the citations are.** Every limit, threshold and platform capability referenced by these
requirements is sourced in `handoff/appendix-A-evidence-register.md` - 646 evidence items with source
URL, access date, agent confidence and the adversarial verifier's verdict on each. The requirement
tables above carry traceability to the input documents and to `system-design-target.md`; the register
carries traceability to the standards and vendor documentation underneath those.

**9.10 A second promised input was also absent, and nobody noticed until the final audit.** The
brief's own file table says `03-deployment-and-constraints.md` contains "a HIPAA technical-safeguard
inventory". **The delivered file does not.** Its section 4 ends at 4.10, and the document contains no
CFR citations and no safeguard assessment anywhere. So Group 2 of this document rests on the
regulation text itself - which the research did fetch and adversarially verify - and **not** on the
project's own assessment of which safeguards it currently meets.

That assessment does exist. `03` section 5 lists `docs/security/HIPAA-COMPLIANCE.md`,
`THREAT-MODEL.md`, `DATA-FLOWS.md`, `AUTHORIZATION.md` and `SESSION-AND-TOKENS.md`, and says
explicitly: **"Read `docs/security/THREAT-MODEL.md` and `HIPAA-COMPLIANCE.md` before proposing an
architecture. They were not audited for freshness in this pass, but they represent prior reasoning
that should be engaged with rather than bypassed."** This research could not do that, and did not.
**A receiving session with the repository should read both and diff them against Group 2** - that is
the single highest-value thing it can do for the HIPAA requirements specifically, and it may reverse
or retire several of them.

**9.1 The measured baseline is partly absent.** `02-runtime-and-data-profile.md` was not supplied.
REQ-TEN-04's database count and REQ-APP-07's per-endpoint arithmetic are derived from the office
ceiling and the audit ratio quoted in the brief, not from measured database sizes. **If `02` is
supplied, both become sharper; neither changes shape.**

**9.2 One requirement is knowingly weaker than it should be.** APP-OWN-09 detects a **total** audit
outage per office. It does **not** detect the failure of an individual audit write, which is the
fail-silent case that matters most - and the obvious fix, turning off error-hiding, converts an audit
outage into a total application outage, which is worse. **The honest position is that individual
audit-write failures are not currently detectable and that closing the gap needs an application
change to record failures durably rather than log them.** It is named rather than quietly satisfied.

**9.3 REQ-TEAM-04 has no human on the end of it.** Multiple requirements here assume an alert reaches
somebody. There is **no on-call rotation, no escalation path and no second responder**, and two people
who may both be asleep or on leave. A dead-man's switch detects that the alerting path died; nothing
detects that the recipient did not read it. **The honest position - business-hours best-effort, with
a stated maximum time-to-notice - should be written down and offered to practices, because it is what
will actually happen.** A platform cannot fix this and should not be scored on it.

**9.4 State law is absent, and this is a real gap rather than a scoping decision.** Every regulatory
requirement here traces to federal rules, while the product handles **California workers'
compensation and California medical records.** State confidentiality and breach-notification law
impose obligations that differ from and can exceed the federal floor, and state medical-evaluation
record rules bear directly on the retention question. **This is the single item in either document
most worth an hour with counsel**, and it could change REQ-HIP-04's retention period, REQ-HIP-14's
classification from SHOULD to MUST, and the offboarding design in `system-design-target.md` section
17.

**9.5 The team-capacity thresholds in section 3 are modelled, not measured.** They are stated as my
model so they can be argued with, and the one-developer-week figure they are sized against has never
been confirmed against a staffing commitment.

**9.6 Deploy-time is the dominant availability risk and no requirement here addresses it.** Manual
deployment, no continuous delivery, migrations that run on every bring-up against every office, and
two documented traps. **The most probable cause of the next outage is a Tuesday afternoon deploy by
one of two people**, and the cheapest controls for that are not platform capabilities: a declared
change window, a two-person rule for production changes, and a written rollback plan attached to each
deploy. They belong in an operating agreement, not in a platform scorecard, and they are named here
because this document otherwise adds a great deal of **new** deployment surface.

**9.7 Misdirected email is a reportable disclosure and nothing here tests for it.** The
ex-parte-safe addressing model - one primary recipient with specific others copied - is a compliance
control implemented in application code that sends PHI to attorneys and adjusters. **A single wrong
recipient is a breach with a notification clock, and it is far more likely than the host dying.** It
needs a test of the recipient-selection logic and an alert on outbox age. Application scope, named so
it is not lost.

**9.8 The object store is the largest unprotected asset here and the requirements do not fully
cover it.** It holds the actual medical documents, has no backup design in this document, sits on the
same disk as the databases and the current backup directory, is published to the internet, is
authenticated to as the root identity, and its incumbent implementation is unmaintained upstream.
REQ-APP-08 and ANTI-08 constrain it; **nothing here requires it to be backed up, and it should be.**
Treat that as a missing MUST rather than a deliberate omission.

**9.9 Time-triggered failures have no owner.** The design adds several dated artefacts to an
inventory that already contains one: key-encryption credentials, a standby signing credential,
per-office encryption keys growing with the fleet, wildcard certificates at three depths, and a
backup-encryption credential - against a published downward trend in maximum certificate lifetimes.
**Nothing in either document implements the expiry-notice mechanism, and no requirement owns the
register.** This is the failure class that arrives with no change to blame.
