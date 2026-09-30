# Research method and analysis

> **Read this second, after `system-design-target.md` section 1.**
>
> This document exists so the receiving session can tell **my reasoning apart from the agents'
> sourced findings**, and can therefore argue with the right thing. The brief asked for that
> separation explicitly. Architecture is judgement; the citations are not.

| Field | Value |
| --- | --- |
| Produced | 2026-08-31 |
| Session capability | Web access. **No repository access. No access to the running system or its databases.** |
| Research scale | 34 agents, four phases, 5.7M subagent tokens, 1,972 tool calls, 4h13m, zero agent errors |
| Verification | 526 individual claims re-checked by a second adversarial agent: **347 clean, 161 corrected, 12 wrong, 6 unreachable** |
| Inputs received | 3 of 4. `02-runtime-and-data-profile.md` never arrived |

---

## 1. What I was asked, and the three constraints that shaped everything

Determine what **shape** the system and its infrastructure should take, expressed as **vendor-neutral
requirements** a later platform-selection exercise can score against.

Three constraints did most of the work:

**1. "Do not pick a cloud provider. Do not price anything."** This is a deliberate ordering
constraint, and the brief says why: anchoring the architecture to a provider inverts the sequence.
The architecture should generate the platform requirements, not the reverse. I took this
seriously enough to build vendor-neutrality auditing **into the verification pass** rather than
checking it at the end - every capability requirement produced by every area agent was read by a
second agent asking "does a named product appear here as a recommendation rather than as evidence?"

**2. Operational complexity is a first-class cost, and designs that fail obviously beat designs that
fail subtly.** Two SDE 1 developers, no senior engineer, no architect, no DevOps, no security
specialist, no QA, no on-call rotation. This is not a softener and I did not treat it as one. It is
the reason database-per-tenant survives, the reason a cluster orchestrator does not, the reason a
read replica is rejected, and the reason section 2 of Document A exists at all.

**3. "Say what not to build."** The brief asked for the rejection list to be as valuable as the
recommendations, and to engage with each rejected pattern rather than dismiss it. Section 20 of
Document A is eight rejections, each with what the pattern **would** buy stated first.

---

## 2. How the research was structured, and why

Four phases, run as a deterministic workflow rather than as a conversation, so the structure was
fixed before any result could bias it.

```text
Phase 1  DESIGN SPACE     14 agents, one per area, BLIND to the current system's gap list
                          |
Phase 2  VERIFY           14 agents, one per area, each re-fetching every URL its area cited
                          and separately auditing that area's requirements for vendor-neutrality
                          |
Phase 3  QUESTIONS         5 agents, one per blocking question, run independently of the areas
                          |
Phase 4  CRITIQUE          1 agent over the entire body: completeness, contradictions,
                          unjustified complexity, under-weighted risks, and the three decisions
```

**Four design decisions inside that structure are worth naming, because each changed the output.**

**2.1 The area agents were blind to the current system's known problems.** They received the
architecture and the constraints, but not a list of "here is what is wrong today". Handing an agent
the gap list produces a standard shaped around those gaps and nothing else. The comparison against
current state was done afterwards, by me. **That is why Document A section 3 contains fourteen
corrections to the input documents - findings nobody was looking for.** The alternative design would
have produced a document that confirmed what the input documents already said.

**2.2 The verification pass had two jobs, not one.** Every verifier re-fetched every URL its area
cited and judged whether the page supported the claim **as stated**, defaulting to "does not
support". It also audited that area's capability requirements for vendor-neutrality. Combining them
was deliberate: the neutrality violations that matter are not "the document says Azure" - they are
requirements written in one product's vocabulary such that a different platform cannot be scored
against them, and that is only visible to somebody reading the requirement against its evidence.

**2.3 The five questions got their own agents, separate from the areas.** The brief said each has a
determinate answer. Running them separately meant each question had a dedicated adversary rather
than being a paragraph inside a broader area, and it meant the question answers could disagree with
the area answers - which they did, twice, productively. Question 3 (multi-instance) reached a
harder "no" than the scaling area did, for better reasons.

**2.4 The critique agent read the whole body, not individual areas.** This is the phase that
produced the finding that changed the shape of Document A more than any other, and no area agent
could have produced it. See section 4.1.

**Two areas were added beyond the brief's twelve**, and I flagged both before starting:

- **Area 13, administrative surface containment**, from `03` section 4.0 - a finding dated after the
  rest of that document was written. It belongs in a design document rather than a bug list because
  it invalidates an assumption the edge design would otherwise make: that every application route is
  either public by design or authenticated.
- **Area 14, tenant lifecycle including offboarding.** The input documents cover onboarding
  thoroughly and never mention offboarding. In a database-per-tenant HIPAA system, "delete an
  office" is an architectural question about export, retention obligation and deletion evidence.

---

## 3. The judgement calls that were mine, not the agents'

**This is the section to argue with.** Everything here is a decision I made, several of which
overrode what the research produced, and each is stated with the reasoning so it can be reversed.

### 3.1 I reversed my own answer to Question 2

**Before the research landed, I drafted:** separate audit from operational data on a **time
boundary** - a hot window in the office database, older audit to an archive. I explicitly rejected a
separate audit database per office on the grounds that it doubles the fleet count, and fleet size is
what binds first.

**The research argued for the per-office audit database, and it is right**, for a reason my draft
missed entirely: **restore granularity requires no technique at all.** You restore
`CaseEvaluation_{slug}` and simply do not restore `CaseEvaluation_{slug}_Audit`. A time boundary
does not give you that - the hot window still rides along in every operational backup and every
restore.

I adopted the research's answer and **kept my objection visible** rather than deleting it: Document
A section 5 states plainly that the split doubles the database count, that this collides with the
constraint that binds first, and that per-office fleet reporting is therefore a **prerequisite**
rather than a companion. That reconciliation is mine.

**I also adopted the research's sequencing point, which was better than mine:** cut audit generation
at source **before** separating any store, because separating a store you are about to shrink by a
large factor sizes the new store wrong.

### 3.2 I replaced my three decisions with the critique's three

**I drafted:** (1) split the job server out of the API, (2) separate audit on a time boundary,
(3) prove a restore.

**The critique proposed:** (1) prove and pin the tenant boundary, (2) deny by default at the edge and
take the administrative plane off the internet, (3) one off-box immutable actually-restored recovery
path.

**The critique's list is better and I took it**, because its first item outranks everything I had:
it is the only failure in the entire body whose consequence is one practice's patient appearing in
another practice's data - a disclosure that no edge hardening, no backup and no monitoring can undo
or even detect after the fact. My first item was the cheapest useful thing; theirs was the thing
that decides whether the design's central claim is true at all.

My items survive as Document A sections 6 and 8. Nothing was lost; the ordering was wrong.

### 3.3 I decided the DataProtection key material moves to the host database

Four areas held three mutually exclusive models of where the key ring lives. The research's
state-and-session area argued for moving it out of the cache into the host database; three other
areas were written on the premise it stays in the cache.

**I adopted the move**, on one argument that is a team-fit argument rather than an elegance one:
**the host database is the only thing this team actually backs up, so the key material rides along
with no new artefact anyone has to remember.** For a two-person team with no runbook discipline yet,
that beats a technically cleaner store that somebody has to remember to back up.

**And I added the ordering constraint, which is mine and which no area stated:** backups off-box,
**then** move the key material, **then** set an eviction policy on the cache. Applying an eviction
policy first silently destroys key material under memory pressure, and the failure presents weeks
later as intermittent logouts with nothing to correlate it to. **This is the single most dangerous
sequencing error in the whole document set**, it is created by combining two individually correct
recommendations, and it appears nowhere in the underlying research.

### 3.4 I wrote the portfolio section, which nobody was responsible for

Fourteen areas each sized their own recommendation honestly and each concluded it was affordable.
Summed as sized in Document A: **734 to 1,084 hours of setup and 24.5 to 43.5 hours a month of
steady state**, against roughly 40 hours a month. **At the top of that range the steady state alone
exceeds the entire allocation; at the bottom it still consumes more than half of it** - before any
setup and before any feature work. (The underlying research's own per-area figures summed higher
still, at 742-1,090 and 35-57; the gap is the scope Document A already cut.)

The critique found this and named it the most consequential contradiction in the body, adding: *no
area was responsible for the portfolio, so each is locally honest and the aggregate is fiction.*

**The "if only 40 hours materialise, do exactly these" list in Document A section 2 is entirely
mine.** Six of the fourteen areas independently said partial adoption in the wrong order is worse
than doing nothing, and several areas each claimed the "do this first" slot. Without an adjudicated
sequence the order gets chosen by whichever document a developer opens first. **That risk is created
by the research body, not merely missed by it**, and owning it was the author's job.

**Two omissions from that list are deliberate and uncomfortable, and I stated both:**

- **Alerting is not in the first 40 hours**, despite being a MUST. There is no on-call rotation, no
  escalation path and no second responder. An alert firing into an unread inbox is a control that
  has already failed. The honest fix is organisational, not technical.
- **The second environment is not in the first 40 hours**, despite being a MUST, because production
  is a legitimate destructive-test target **right now** and will not be later. The no-PHI window is
  worth more spent on drills in production than on building somewhere else to drill.

### 3.5 I reframed why database-per-tenant is right

The obvious argument is "it gives tenant isolation". **The research established that argument is
wrong**, and the correction is load-bearing: first-party cloud guidance states that data
partitioning describes how tenant data is stored and **does not by itself achieve isolation** - a
user can be fully authenticated and authorised and still reach another tenant's data.

**This system has excellent partitioning and thin isolation.** Every office database is reachable
from the same process over the same superuser login, and the only thing between an authenticated
user of office A and office B's records is which string a resolver returns.

So I rewrote the argument as three narrower claims that survive that correction: it is the only
option whose failure mode survives an ordinary coding mistake; per-office restore is a stated
requirement and is nearly free here; the blast-radius property is already banked. **My draft
over-claimed and the research caught it.**

### 3.6 I promoted "the edge validates shape, the application validates membership" to an invariant

The research surfaced this as the resolution to a contradiction (one area required an edge allow-list
of provisioned office hostnames, and in the same list required that adding an office need no edge
change). **I promoted it from a resolution to a stated invariant** (Document A section 19.4, I10)
because it is the property the entire design rests on, and because without stating it, whichever
version a developer reads first gets built - and if it is the allow-list, a new paying office gets
"Tenant not found" until somebody edits the proxy.

### 3.7 I built the four-surface health taxonomy

Two requirements in the research were mutually exclusive: a liveness path answering with an address
as Host, and an edge that terminates any Host that is not an office hostname. The taxonomy in
Document A section 21.4 - liveness, readiness, external synthetic, administrative UI, with the first
two bound to an unpublished listener so they never traverse the edge - is mine, and it dissolves the
contradiction rather than picking a side.

### 3.8 I adjudicated the six-year reading against the more expensive interpretation

Two areas said the six-year clock in 45 CFR 164.316(b)(2)(i) attaches to **documentation**, not to
audit rows. One area built its largest single line item - a 45-70 hour cold archive - on the
contested reading, then hedged in a closing paragraph. Nobody adjudicated.

**I adjudicated in favour of the sceptical reading**, which is also what the regulation's text
supports on a plain read: 164.316(b)(1) covers the written policies and procedures implemented to
comply with the Security Rule, and written records of actions the subpart requires to be documented.
An application audit log is neither, and 164.312(b) states no retention period at all.

**The consequence is that the most expensive item in the audit area is not obligated** and should be
justified on risk grounds or deferred. That is an uncomfortable call to make against my own
research, and it saves the team roughly 45-70 hours.

**But the research also found the obligation that IS real**, and this is the finding I would most
want the receiving session to notice: **45 CFR 164.528 gives individuals a right to an accounting of
disclosures for six years, and its exclusion list does not exclude 164.512(l), disclosures for
workers' compensation - which is this platform's entire business.** The correct artefact is a
purpose-built, append-only disclosure ledger as **domain data**, sized in kilobytes. Nobody was
looking for that and it does not appear in either input document.

### 3.9 Everything sized, and nothing invented

Every operational-cost figure in Document A is the research's own estimate, carried through. **I did
not assert a single throughput number, instance size, or headroom multiplier**, because every such
number would have been invented and the previous exercise in this sequence caught a fabricated
standards quotation in its own verification pass. Document A section 14 says so explicitly: this
system has never been measured under load, the measurement is cheap, and the first measurement is
worth more than any sizing estimate the document could produce.

### 3.10 The sixteen adjudications in Document A section 21 are all mine

The critique surfaced the contradictions; resolving each was the author's job. The method was
consistent: prefer the resolution that (a) fails obviously rather than subtly, (b) preserves the
property that adding an office is a host-UI action, and (c) is executable by two junior developers
without a specialist. Where those three pointed different ways I said which one won and why.

---

## 4. Where the evidence conflicted, and how it was resolved

### 4.1 Areas contradicting each other

Sixteen contradictions, all in Document A section 21 with the adjudication. The five that mattered
most:

| Conflict | Resolution | Why |
| --- | --- | --- |
| Cache eviction policy: three areas, three incompatible values on one setting | Ordering, not a value: key material moves out first, then the policy is set | Applying the policy first destroys key material silently |
| Where the key material lives: four areas, three models | Host database, with off-box backups as the prerequisite that satisfies the independence requirement | Section 3.3 above |
| Edge office allow-list vs "adding an office touches nothing" | Edge validates shape, application validates membership | Section 3.6 above |
| Health probes: two mutually exclusive MUSTs | Four distinct surfaces, two of them off the edge entirely | Section 3.7 above |
| Six-year retention: disputed inside the body, largest line item depends on it | Sceptical reading; the real obligation is a different, much smaller artefact | Section 3.8 above |

### 4.2 The verification pass against the area agents

**526 claims, 347 clean.** About two-thirds survived first-pass scrutiny, and ten of fourteen areas
returned an overall verdict of "material errors". Representative catches:

- **A verbatim quotation attributed to a page that does not contain it.** The verifier called this
  the single most serious provenance defect in its set. The correct figures were on the page,
  demonstrated differently; the quotation was invented.
- **A threshold taken from a 32-bit failure mode and applied to a 64-bit deployment.** A category
  error that would have written a spurious memory ceiling into the backup design.
- **A regulation cited to the wrong URL** - the six-year rule pointed at the technical-safeguards
  section rather than the documentation section. The substance was right; the citation would not
  have survived review.
- **Component defaults stated from memory** against pages that never name them. **The fix is not a
  better citation - it is to require the setting be asserted explicitly rather than relying on any
  default**, which is what Document B now does.
- **A tool claimed actively maintained that had been archived upstream six weeks earlier**, which
  inverted a migration step's verification argument.
- **A DNS propagation window misread by roughly eight times**, because a public resolver's own cache
  cap was mistaken for the parent delegation lifetime.
- **Several stale versions**, and several statistics behind paywalled or bot-blocked sources, which
  were **dropped rather than carried into a document a reader cannot check**.

### 4.3 The correction that landed on me personally

Mid-run, before the verification pass completed, I reported a finding to the user: that ABP's
`AddDomainTenantResolver` is documented as a shortcut for `TenantResolvers.Insert(0, ...)`, and that
if the custom resolver was registered with `.Add(...)` the query-string resolver would win.

**The verifier read the framework source and found that wrong.** The quoted phrase is not on the page
cited, and the implementation is `InsertAfter(r => r is CurrentUserTenantResolveContributor, ...)`.

**The correction made the finding more serious, not less.** There are **five** default resolvers, not
four, and the first is `CurrentUserTenantResolveContributor`, which reads the tenant from the access
token claim. So **for an authenticated request, tenancy is decided by the token, not by the Host
header** - and Host-header tenancy governs the *anonymous* surface: login, password reset, anonymous
upload, the partner reconcile endpoint. Two consequences follow that nobody had on a list: a token
minted at office A must be proved to fail at office B's API, and an **empty** `Host` field value -
which RFC 9112 makes legal and Kestrel accepts - causes the domain resolver to abstain and fall
through to the fallback resolvers **even when ordering is correct**.

I corrected this to the user in the same message that delivered the documents. It is recorded here
because it is the clearest illustration of what the verification pass is for: **the first pass was
confident, specific, and wrong in a way that would have sent somebody looking at the wrong line of
code.**

---

## 5. What I drafted before the research landed, and what survived

The workflow took four hours. I drafted the judgement-heavy sections while it ran, from the input
documents alone. Recording what changed is the most honest measure of what the research was worth.

| Draft position | Fate | What changed it |
| --- | --- | --- |
| Three decisions: job-server split, time-boundary audit, prove a restore | **Replaced** | The critique's three outrank them (3.2) |
| Q1: migration fan-out binds first | **Refined** | Operational attention binds at 16-23 offices; fan-out is a principal *cause* of it, not the constraint itself. And the numbers were the research's, not mine |
| Q2: time boundary, reject the per-office audit database | **Reversed** | Restore granularity (3.1) |
| Q3: "almost - one hard blocker, the job server" | **Hardened to NO, and the blocker changed** | The binding blocker is the proxy caching upstream addresses, because it makes the entire experiment invisible. Four blockers, not one |
| Q4: five invariants | **Expanded to eleven**, and the premise corrected | Token-claim resolution, the port-in-Host trap, the empty-Host bypass, the ordering invariant |
| Q5: four requirements | **Kept, and one added that outranks the rest** | Backup enrolment must happen at provisioning, because recovery model is inherited at database creation and databases are created by a business action |
| Keyring stays in managed cache | **Reversed** | Host database (3.3) |
| Availability: single zone, recoverability first | **Survived**, and strengthened | Per-database availability groups carry one database each, so 34 groups fail over independently and a partial failover is close to the worst failure shape. HA must be instance-level |
| "Not recommended" section | **Survived nearly intact**, one addition | A clinical interoperability standard for tenant export was added as 20.7 |
| Capacity model structure | **Survived**, numbers replaced | The research supplied real derived figures; my structure held |
| Target architecture | **Survived**, three components added | Per-office audit database, lifecycle ledger, second unpublished listener |

**Roughly a third of what I drafted from the input documents alone was wrong or incomplete in a way
that mattered.** That is the argument for having run the research rather than reasoning from the
inputs.

---

## 5b. What the post-hoc audit of the finished documents found

The deliverables were audited after they were written. Two things came out of it that belong here
rather than in a changelog, because both are about the method rather than the text.

**The portfolio arithmetic was wrong, and it was wrong in a way that flattered the analysis.**
Document A carried the research's own aggregate rather than the sum of the costs as Document A
itself sizes them. Recomputing from the per-area figures gives 734-1,084 hours of setup and 24.5-43.5
a month, against the 700-1,050 and 35-57 originally stated. **The correction weakens a claim I had
made**: "the steady state alone consumes the whole allocation" holds at the top of the range and not
at the bottom, and the document now says so. Both rows are shown, because the gap between them is
precisely the scope Document A cut relative to the research, and hiding it would hide the cut.

**A promised input was missing and I did not notice for four hours.** The brief's file table says
`03-deployment-and-constraints.md` contains a HIPAA technical-safeguard inventory. It does not. I
flagged the absence of `02-runtime-and-data-profile.md` before starting, correctly, and then failed
to check whether the three files that **did** arrive contained what the brief said they contained.
The consequence is that Document B's HIPAA group rests on the regulation text - adversarially
verified, so not weak - rather than on the project's own assessment of which safeguards it currently
meets. **That assessment exists in the repository and `03` section 5 says explicitly to read it
before proposing an architecture.** It is check 15 in HANDOFF.md and it may retire several
requirements.

**A note on how that audit was run**, because it is a method finding too. I first launched it as a
multi-agent workflow whose verify stage fanned out one agent per finding with no cap - which would
have exceeded a hundred agents. It was the wrong tool and it was stopped. Cross-reference integrity
is a grep; arithmetic is arithmetic; "did the correction land" is a string search. **The one
materially wrong thing in the deliverables was found inline with a six-line script in about two
minutes, while the workflow was still starting up.** Fan-out buys reading breadth a single context
cannot hold. It does not buy checking a document against itself, and reaching for it there cost time
and bought nothing.

---

## 6. What this research could not establish

1. **`02-runtime-and-data-profile.md` was never supplied.** Three of four files arrived. It is the
   document the brief points at most often - measured database sizes, table-level row counts, the
   configuration surface, and the explicit "never measured" list. Area 11 is built as a structure
   plus an instrumentation plan with every input labelled `MEASURED`, `DERIVED` or `UNKNOWN`. The one
   load-bearing figure, the audit ratio, was available from the brief itself and is used as given,
   with every dependent conclusion marked. **Folding `02` in is an edit, not a rebuild.**

2. **No repository access, and this is the limitation that matters most.** Every statement about the
   current system is inherited from the two input documents. Where they are wrong, the deliverables
   are wrong in the same place - **except** where Document A section 3 corrects them from framework
   source, which is a different and weaker kind of check: it establishes what the framework does by
   default, not what this codebase does.

3. **The single most important unchecked fact is one file read**: whether
   `HostAwareDomainTenantResolveContributor` was registered with an insert at position zero or an
   append. **The entire tenant-boundary argument turns on it.**

4. **California and state law are absent.** The analysis cites federal regulation extensively for a
   product handling California workers' compensation and California medical records. State
   confidentiality and breach-notification law can exceed the federal floor, and state
   medical-evaluation record rules bear directly on the retention question the documents spend the
   most effort on. **A verification pass that has already caught one misread statute should be
   pointed here next.**

5. **The one-developer-week-per-month figure was never confirmed** against a staffing commitment, and
   section 2 of Document A is sized against it. Thresholds are stated wherever a recommendation's
   viability turns on it.

6. **Nothing has been measured under load and no restore has ever been performed.** The two cheapest
   measurements in the whole document set - one daily disk sample and one timed restore - would
   change more of it than any further research.

---

## 7. What would change the conclusions

| If this turns out to be true | What changes |
| --- | --- |
| The custom resolver is appended rather than inserted first | The tenant boundary is not real. Everything stops until it is fixed; the platform exercise is irrelevant until then |
| A token minted at one office is accepted at another | Same. This is a live cross-tenant read, not a design gap |
| The team grows a third engineer whose job includes infrastructure, or the organisation already operates a cluster somebody else maintains | Section 20.3 inverts. It is the rejection most likely to be wrong later and should be revisited at any staffing change |
| The business answers that staff stay on VPN and only external-party surfaces go public | The edge and observability work shrink substantially. Areas 7 and 9 are the two most sensitive to this answer |
| Real patient data arrives before the migration completes | The migration stops being a build and becomes a move, with a PHI transfer, a downtime window and a per-database rollback plan. Several recommendations become impossible rather than expensive - per-office encryption most sharply, because crypto-erase is conditioned on the data never having been stored in plaintext |
| State law imposes a longer or different retention obligation | Section 3.8's adjudication may reverse, and the cold archive returns as obligated rather than optional |
| A load test shows the single instance saturating | Section 6 changes from "one instance, blocker removed" to "two instances", and the prerequisites in 19.3 become urgent rather than cheap |
| `02-runtime-and-data-profile.md` arrives | Several `UNKNOWN` cells in the capacity model become `MEASURED`. The model's shape does not change |

---

## 8. How to disagree with this well

The recommendations are judgement and are meant to be argued with. Three ways to do it usefully:

**Attack the classification, not the citation.** The citations were adversarially re-verified and
their corrections are in Appendix B. If a figure looks wrong, check the register first - it may
already be marked `partially` with the correction attached.

**Attack the team-size premise.** More than half the rejections in section 20 turn on two SDE 1
developers with no specialist and no rotation. If that premise changes, several rejections should be
revisited - and section 20.3 names itself as the first.

**Attack the ordering.** Most of the value in Document A section 2 and section 21 is sequencing
rather than selection. If you think a different order is right, that is a substantive disagreement
about the most consequential part of the analysis, and it is a much better argument than disputing
an hour estimate.
