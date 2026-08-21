# Parity-research synthesis, grouping, and fix timeline

> Consolidated report for the 81 rows of `docs/parity-review-log.csv`.
> Research records: one file per row at `docs/parity-research/<G-ID>.md`.
> This is a PLANNING artifact estimated from research -- it is NOT a fix plan
> and contains no implementation steps. Generated 2026-06-01.

---

> **DECISION GATES FINALIZED 2026-06-01** (after OLD-codebase + web research; see
> SS 2.4). Net effect on the backlog: matrix (`G-01-04`) downgraded L -> S and its
> admin UI deferred; City lookups (`G-10-04`, `G-07-08`) dropped (keep free-text);
> `G-10-12` Suite verified parity-preserving (no work). Revised active estimate:
> **~445h -> ~13-14 weeks** (down from ~498h / ~15-16 weeks).

## 0. Corpus at a glance

- **81 research records** written (1 per CSV row), pinned to the `### G-XX-XX`
  headings in `docs/parity-v2/`. Two IDs do not follow row order:
  CSV row 37 -> `G-05-INTENT-01`; data-model rows shift past the doc's
  `G-10-06` ("resolved", absent from the CSV), so the file set runs
  `G-10-01..05, 07..13` (no `G-10-06.md`).
- **Repro:** every gap is **`not-reproduced-static-confirmed`** (or `n/a` for
  excluded "no-gap" rows). A true runtime repro would require submitting
  bookings / mutating data, which the read-only mandate forbids; each gap is
  instead confirmed by direct code inspection at the cited lines. No item was
  marked "reproduced" (would need state mutation) and none "infeasible to
  assess" -- all were statically confirmable.
- **Confidence:** 80/81 root-cause hypotheses are **high**; one (`G-09-05`,
  counter time-window) is **medium** because the OLD stored proc is not in
  source and "all-time" is inferred.

| decision | count | depth tier | effort profile |
|---|---|---|---|
| include | 59 | full | 19 S, 26 M, 12 L, 1 XL, 1 none |
| defer | 6 | medium | 2 S, 3 M, 1 L (+1 L duplicate) |
| exclude | 16 | light | all 0h (2 carry a "if-revisited" S/M tag) |

---

## 1. STEP 2 -- Synthesis

### 1.1 Duplicates (build ONCE; do not double-count or double-build)

The CSV authors flagged most of these in notes; the research confirmed them.

| Canonical unit | Duplicate / storage-side rows | What it is |
|---|---|---|
| **`G-01-04`** Role x AppointmentType matrix | `G-06-01` (auth angle) | Same configurable role x type table + admin UI |
| **`G-03-01`** Document-type master | `G-07-07` (admin angle), `G-10-09` (storage) | Same `AppointmentDocumentType` entity + CRUD + picker |
| **`G-09-02`** Submit-Query feature | `G-10-02` (UserQuery table = storage side) | Same feature; storage folds into the vertical |
| **`G-07-02`** AppointmentType.ReEvalId | `G-10-11` (storage side) | Same self-ref column + base->reval seed |
| **`G-07-08`** Country/City/TZ lookups | `G-10-04` (City table = data/UI side -- partial overlap) | City picker is the data+rollout half of the lookup feature; counts its own weight |

Net effect: the 59 `include` rows collapse to **~53 distinct buildable units**
(5 true duplicates removed; 1 "none" = keep-only).

### 1.2 Conflicts and flags (resolve before scheduling)

1. **`G-04-07` decision vs dependency conflict.** Marked `include`, but its own
   note + record gate it on the **deferred** SMS work (`G-04-01`): the 9
   `TemplateCode` events are SMS-side and "were dead/commented in OLD too."
   *Recommendation:* treat `G-04-07` as **defer-with-SMS**, not near-term.
2. **`G-02-04` bug-vs-deviation.** Labeled `Intent deviation` in the CSV but the
   research concludes it is a **regression** (reschedule-reject orphans the
   Reserved slot, permanently shrinking capacity via the
   `AppointmentBookingSlotClosed` gate). Per the repo bug policy it should be
   fixed as a bug. **RESOLVED 2026-06-01 (Gate 2):** restore OLD release-on-reject
   (guarded/idempotent); "Reserved" is a transient hold, not a manual-close state.
   (`G-01-xx-slot-rework` in its frontmatter is not a real ID -- it points at the
   2026-05-15 NEW slot rework.)
3. **Non-load-bearing agent-invented IDs** (ignore; artifacts, not real items):
   `B-06-05` (in `G-06-03` deps), `E-06-10` (in `G-06-09` dup), `G-10-OQ` (in
   `G-10-05` deps). `G-03-B1` IS real (a backlog heading in `03-documents-packets.md`).
4. **`G-10-12` reverses a prior resolved rename** (Insurance/ClaimExaminer vs
   Suite). Confirm OLD actually captured those numbers AND preserve Suite
   separately before building.
5. **`G-03-02` exclude needs confirmation** -- the only exclude with an open
   question: confirm attorneys actually used the Joint Agreement Letter in OLD
   before finalizing exclude; if used, it re-enters scope as a PDF download.

### 1.3 Dependency chains (must-fix-before)

```
G-01-03 (populate flag) ---------> G-02-08 (act on patient-match override)
G-01-01 + G-01-02 (injury dates) - underpin -> G-02-07 (approval injury gate, keep)
G-07-02 (ReEvalId data) ---------> G-01-07 (reval UI + prefill)
G-02-01 + G-02-02 (audit list + child-entity audit)
        -> G-10-05 (IsMailSent dedup) -> G-02-03 / G-04-03 (intake-changed email)
G-09-02 (+G-10-02 storage) ------> G-09-03 (routing) + G-04-02 (email leg)
G-03-01 (doc-type master) -------> G-03-03 (ad-hoc metadata) + G-10-10 (package FK)
G-08-01 (report grid) -----------> G-08-03 (PDF export) + G-08-04 (demographics PDF)
G-07-08 (lookup feature) --------> G-10-04 (City picker rollout)
G-01-04 (role x type matrix) ----> feeds G-07-01 (admin UI hosts its screen)
G-07-01 (admin shell) -----------> G-07-05 / G-07-11 / G-07-13 (screens within)
```

Two cross-feature decision gates also act as soft dependencies: the **accessor
UX decision** gates `G-01-06 / G-07-09 / G-10-08`, and **"Reserved" slot
semantics** gates `G-02-04`.

### 1.4 Grouping -- criteria and recommendation

**Criteria applied** (in priority order): (a) **shared dependency chain** -- keep
a producer and its consumers together so an interface is designed once; (b)
**same module / feature vertical** -- minimize context re-load of the same files;
(c) **fix archetype** -- batch pure-code domain rules vs data+migration vs
Angular-heavy UI, because they share a build/verify rhythm; (d) **risk** -- isolate
the PHI-redaction and critical-path work so review attention concentrates; (e)
**duplicates build once**.

Recommended **16 groups** (duplicates collapsed into their canonical unit):

| # | Group | Items (canonical) | Archetype | Risk | Effort |
|---|---|---|---|---|---|
| A | Booking intake validations | G-01-01, G-01-02, G-01-05; verify G-01-08, G-02-07 | code (domain) | low | ~18h |
| B | Patient flag + match-override | G-01-03 -> G-02-08 | code | med | ~12h |
| C | Cancel / reschedule slot integrity | G-02-04 (bug), G-02-05, G-02-06 | code (state machine) | med | ~21h |
| D | Auth / RBAC hardening | G-06-04, G-06-05, G-06-06, G-06-08 | code + config (OpenIddict) | med | ~24h |
| E | Document-type master + packet linkage | G-03-01 (=07-07,=10-09), G-03-03, G-03-05, G-10-10 | code + data | low-med | ~37h |
| F | Submit-Query / Contact-Us vertical | G-09-02 (+10-02), G-09-03, G-04-02 | code + data | low-med | ~40h |
| G | Reval / re-request flow | G-07-02 (=10-11) -> G-01-07 | code + data | med | ~31h |
| H | Email reliability + minor wiring | G-04-04, G-04-06, G-04-10 | code (Hangfire) | low | ~9h |
| I | Role x type matrix (HARDEN ONLY; UI deferred) | G-01-04 (=06-01) | code + data | low | ~3h |
| J | Accessor sharing model | G-01-06, G-07-09, G-10-08 | code + data | med (needs UX decision) | ~21h |
| K | Change-log / audit + intake-changed email | G-02-01, G-02-02, G-10-05, G-02-03 (=04-03) | code + data | **HIGH (PHI)** | ~67h |
| L | Reminder jobs | G-05-01, G-05-02, G-05-03 | code (recurring jobs) | low-med | ~27h |
| M | Reporting + PDF export | G-08-01 -> G-08-03, G-08-04 (G-10-12 dropped) | code + data + QuestPDF | med (PHI mask) | ~40h |
| N | Dashboard counters | G-09-01, G-09-05, G-09-06 | code | low | ~15h |
| O | Structured lookups -- DROPPED (Gate 4: keep free-text) | G-07-08, G-10-04 | -- | -- | 0h |
| P | Admin master-data UI suite | G-07-01 (XL) + G-07-05, G-07-11, G-07-13, G-07-04; verify G-07-10 | code (Angular-heavy) | med (volume) | ~80h |

**Why this grouping minimizes rework/context-switching:** each group is a single
build/verify context (one set of files, one archetype). Producers ship with their
consumers (B, E, F, G, K, M, O), so DTO/endpoint shapes are decided once. The
three duplicate clusters (I, E, F/G) are built a single time. The highest-risk
work (K's PHI redaction, I's hot booking path) is isolated so review can focus.
The XL admin suite (P) is its own block rather than interleaved, avoiding repeated
Angular-context reload.

---

## 2. STEP 3 -- Timeline (for FUTURE fix work)

### 2.1 Method and assumptions

- **Estimated FROM the research, not a fix plan.** Hours are derived from each
  record's S/M/L/XL effort tag (which already weighs blast radius and layer count).
- **Capacity:** ~8 hours/day of active development by **Adrian + Claude Code
  Opus 4.8 at xHigh**. This pairing accelerates authoring but does **not** remove
  this repo's hard serialization costs: **no `ng serve`** (build + `serve` cycle
  per UI change), strict **SQL -> AuthServer -> HttpApi -> Angular** start order,
  and **proxy regeneration** after every backend DTO change. Those make UI-heavy
  M/L/XL items slower than their raw code size suggests.
- **Effort -> hours mapping** (build **including** unit tests + manual
  verification of the slice; confidence: MEDIUM -- these are planning estimates):

  | tag | range | midpoint | meaning |
  |---|---|---|---|
  | S | 2-4h | 3h | 1-2 files, one layer, additive |
  | M | 6-12h | 9h | multi-file, 2-4 layers, one slice |
  | L | 16-28h | 22h | full vertical: entity + migration + EF + AppService + mapper + permission + controller + proxy + Angular + tests |
  | XL | 40-60h | 50h | the ~6-screen admin suite (`G-07-01`) |
  | keep/verify | ~1.5h | 1.5h | "already shipped / matches OLD" -- confirm + pin a test |

- **Buffer:** +20% on top of midpoints for integration verification, Adrian
  review cycles, and rework. (Verification of the individual slice is already in
  the range; the buffer covers cross-slice integration + review latency.)
- **Sequence rule:** producers before consumers (1.3); decision-gated items only
  after their gate (2.4); batch by group to hold one context.

### 2.2 Effort rollup (revised after 2026-06-01 gate decisions)

- Pre-decision midpoint was ~498h. Gate decisions removed ~53h: matrix `G-01-04`
  L->S (-19h), lookups Group O dropped (-31h: `G-07-08`+`G-10-04`), `G-10-12`
  Suite verified no-action (-3h).
- Active `include` build (duplicates collapsed, "keep" items ~0): **~445h**
  midpoint.
- +20% buffer -> **~534h** -> at 8h/day -> **~67 working days -> ~13-14 weeks ->
  ~3.25 months** for the full `include` backlog.
- `defer` backlog (post-launch, NOT in the schedule below): ~46h
  (SMS bundle `G-04-01`+`G-05-INTENT-01`+`G-04-07` ~27h; notes `G-09-04`/`G-10-01`
  ~22h once; forgot-password `G-06-03` ~3h; countries `G-10-03` ~3h).
- `exclude`: 0h (verification done in the records; now includes `G-10-04`,
  `G-07-08`, `G-10-12`, reclassified by the gate decisions).

### 2.3 Phased schedule (dependency-respecting)

> Phase 0 runs partly in parallel with Phase 1 (decisions/spikes are
> low-keyboard). Week numbers assume the groups run roughly in listed order.

**Phase 0 -- Decisions + research spikes (~2-3 days, overlaps Week 1)**
- Adrian decisions (2.4 list).
- Spikes: OpenIddict single-session mechanics (`G-06-06`); ABP audit child-entity
  capture + PHI-redaction approach (`G-02-02`); QuestPDF table layout (`G-08-03/04`).

**Phase 1 -- Integrity & quick wins (Weeks 1-2, ~75h)**
- Group A booking validations; Group B patient flag->override; Group C slot
  integrity (incl. the `G-02-04` bug); Group D auth hardening.
- *Rationale:* low-risk, bug-like, unblocks `G-02-08` and stabilizes the booking
  spine before feature work.

**Phase 2 -- Self-contained verticals (Weeks 3-5, ~117h)**
- Group E doc-type master + packet; Group F Submit-Query; Group G reval
  (`G-07-02` data first, then `G-01-07` UI); Group H email reliability.
- *Rationale:* independent features with clean producer->consumer chains; no
  cross-group coupling, easy to review and merge as parity slices.

**Phase 3 -- Cross-cutting + PHI cluster (Weeks 6-9, ~137h)**
- Group I role x type matrix; Group J accessor model (after UX decision);
  Group K audit + change-email (**highest risk**; after redaction spike, order:
  `G-02-01`/`G-02-02` -> `G-10-05` -> `G-02-03`/`G-04-03`); Group L reminder jobs.
- *Rationale:* concentrates the riskiest, most PHI-sensitive work in one window
  with focused review; matrix lands before its admin screen in Phase 4.

**Phase 4 -- Reporting, dashboard, lookups, admin UI (Weeks 10-15, ~169h)**
- Group M reporting + PDF (after QuestPDF spike); Group N dashboard counters;
  Group O lookups; Group P admin master-data UI suite (XL).
- *Rationale:* the Angular-heavy block last, in one sustained UI context; the XL
  admin suite hosts several earlier-built backends (matrix, templates, types).

**Buffer -- Weeks 15-16** absorb overruns (the +20% is allocated here and inline).

**End state:** full `include` parity in **~15-16 weeks (~3.5-4 months)** at
8h/day; deferred backlog and SMS bundle handled post-launch on their triggers.

### 2.4 Decision gates -- RESOLVED 2026-06-01

All seven gates are decided (validated against OLD code + web research). No
remaining Phase-0 blockers except the standard build spikes (OpenIddict
single-session, ABP audit redaction, QuestPDF).

| Gate | Resolution | Backlog effect |
|---|---|---|
| 1. Accessor UX | Restore OLD email-based create-or-invite (free-typed name+email+role, not user-picker). Wire the already-built dead `AppointmentAccessorManager.CreateOrLinkAsync`. Accessors are an additional share set that may overlap AA/DA/CE. **Verify** NEW grants named AA/DA/CE access via their own identity (OLD's scoping proc is not in source). | Group J as planned (~21h), de-risked -- backend largely exists |
| 2. Reserved slot | Restore OLD release-on-reject (guarded/idempotent); Reserved = transient hold. Treat as a bug. | `G-02-04` confirmed (~M, Group C) |
| 3. Matrix scope | OLD evidence: only AME=attorneys / PQME=all-external ever shipped (NEW matches). Harden with a per-type allowed-roles flag; DEFER the configurable matrix + admin UI. | `G-01-04` L->S; one fewer admin screen |
| 4. City/Country | Keep free-text (true parity -- OLD's City table was vestigial). No country (US-only). | Group O DROPPED: `G-10-04`+`G-07-08` -> exclude (0h) |
| 5. Counter window | Provide BOTH: default "This week" + an "All-time" switcher. | `G-09-05` stays S (slightly more than a revert) |
| 6. JAL | Stay excluded; Adrian will add independently if needed. | no change |
| 7. Insurance/ClaimExaminer | OLD's "numbers" were the address SUITE line (form label "STE"); real claim IDs live on injury-detail/worker-comp and are preserved. Keep NEW's Suite rename. | `G-10-12` -> exclude/verified-no-action (0h). A real policy number would be a NEW enhancement, out of parity scope. |

### 2.5 Biggest uncertainties (what could blow the estimate)

1. **`G-07-01` admin UI suite (XL).** Single largest swing: ~6 screens could be
   5-10 days depending on shared-component reuse and the per-change build/serve
   cost. Drives ~13-16% of the whole backlog alone.
2. **PHI redaction in Group K.** `G-02-02`/`G-02-03` put injury/claim values into
   audit views and email bodies. A thorough HIPAA-redaction design + review can
   expand this group materially; it is the highest-risk cluster.
3. **`G-06-06` single-session in OpenIddict/ABP.** Token-revocation mechanics are
   framework-dependent; if the clean hook does not exist, this M can become an L.
4. **`G-01-04` matrix -- RESOLVED to harden-only** (per-type flag, UI deferred).
   Regression risk now low; only care needed is preserving the shipped AME/PQME
   behavior + tests when swapping the substring match for the flag.
5. **City picker -- RESOLVED (removed).** Gate 4 keeps free-text; `G-10-04` +
   `G-07-08` are off the backlog (-31h). No longer an uncertainty. (Available as a
   future enhancement via a Census-Gazetteer-seeded autocomplete if needed.)
6. **Build/serve serialization.** Every UI verification is a full
   build + serve + manual-click cycle (no hot reload). Across Phases 2/4 this is a
   persistent, compounding drag not captured in raw code size.
7. **Unresolved decision gates (2.4).** Each ungated item stalls its group; if
   decisions slip, Phases 1/3/4 slip with them.

---

## 3. Appendices

### 3.1 Deferred backlog (post-launch; not scheduled above)

| ID | Title | Trigger to un-defer | Effort |
|---|---|---|---|
| G-04-01 | SMS channel | SMS provider (Twilio/ACS) + creds rollout | M |
| G-05-INTENT-01 | SMS legs in reminders | same SMS provider | M |
| G-04-07 | 9 TemplateCode SMS events | same SMS provider (reclassify from include) | M |
| G-09-04 | Internal threaded notes | post production launch | L |
| G-10-01 | Notes table (storage of G-09-04) | with G-09-04 (build once) | (L) |
| G-06-03 | Forgot-password 3-message gate | Adrian confirms; mind enumeration leak | S |
| G-10-03 | Countries table | post launch; US-only today | S |

### 3.2 Excluded (no work; verification recorded in each file)

`G-03-02` (JAL -- **needs-confirmation**), `G-03-04` (JDF cascade -- verified
NEW==OLD), `G-04-05` (3-way fan-out -- keep NEW), `G-04-08` (BCC -- dead in OLD),
`G-04-09` (subject suffix -- cosmetic), `G-05-04` (queue digest -- keep NEW),
`G-06-02` (accessor home -- cosmetic), `G-06-07` (rate-limit -- ABP default
stricter), `G-06-09` (anon redirect -- verified implemented), `G-07-03` (slot
all-or-nothing -- keep NEW), `G-07-06` (AvailableTypeId -- verified dead in OLD),
`G-07-12` (per-type cap -- OLD was buggy), `G-08-02` (xlsx export -- skip),
`G-09-07` (Approved card -- keep NEW), `G-10-07` (user profile cols -- mapped/no
unique data), `G-10-13` (WorkerCompensations -- redundant in OLD).

### 3.3 Keep / verify-only (already shipped or matches OLD; ~0 build)

`G-01-08` (self-rep modal), `G-02-07` (approval injury gate, BUG-043),
`G-07-10` (system-param validation). Action: pin a regression test, no feature work.

### 3.4 Per-item index

All records: `docs/parity-research/G-*.md`. Frontmatter on each carries
`effort`, `root_cause_confidence`, `repro_status`, `dependencies`,
`duplicate_of` (and `defer_trigger` / `verification` for medium/light tiers).
