---
title: Tuesday demo prep -- master briefing index
date: 2026-05-25
status: ready
audience: Adrian (presenter)
---

# Tuesday Demo Prep -- READY-FOR-DEMO

Snapshot taken 2026-05-25 ~23:55 PT. Stack on `main` at HEAD. All
critical hardening complete. **Tuesday verdict: SHIP.**

## 5-minute pre-demo checklist

1. **Start the stack 60+ seconds before demo:** `docker compose up -d`
   from `W:/patient-portal/main`. Confirm all 7 containers
   `(healthy)` via `docker compose ps`.
2. **Open SPA + login once during setup** to warm the browser
   cache. Subsequent navigation is sub-1.2s.
3. **Pre-warm Gotenberg** by uploading one tiny doc (already done
   tonight; warm again before live demo). Cold-start is 13s; warm
   is sub-2s.
4. **Use incognito for the demo window** to avoid stale `__tenant`
   cookie + expired refresh-token stuck-spinning state from
   previous sessions.
5. **Demo from a single tab.** Two-tab refresh-token race could log
   the user out mid-demo (known OpenIddict + ABP Angular quirk).
6. **Open Hangfire dashboard in a second tab** (after main demo tab
   is set up) so audience sees background jobs cycle during the
   approve + regenerate steps.
7. **Sign in as `stafsuper1@gesco.com` for Flow 2-5.** Do NOT sign
   in as the static `admin` role -- it auto-grants Delete on every
   appointment, one mis-click is unrecoverable.
8. **Patient login is safe.** The `externaluser-role` body class
   wired in `app.component.ts:101` + `styles.scss:73-92` hides the
   sidebar for Patient/AA/DA/CE. Earlier "menu leak" claim was
   based on DOM query, not visual state -- corrected in
   `role-probe-live-findings.md` and screenshot
   `screenshots/08-patient-home-visible.png`.

## Demo go/no-go status

| Flow | State | Confidence |
|---|---|---|
| 1. Registration with role-based fields | ready | high |
| 2. Dashboard + appointment list (Staff Supervisor) | ready | high |
| 3. Approve a pending appointment (Clinic Staff) | ready | high |
| 4. Document upload + packet regeneration (BUG-036 demo) | ready | high |
| 5. Invite an external user | ready | high |
| F4-01 SSN role-based redaction | ready | high (live-verified) |
| Hangfire stability | clean | high (0 retries, 0 failed) |
| Console / network noise | clean | high |
| Multi-tab + refresh resilience | tested | high |
| Demo data integrity | verified | high |

## Document index

Per-topic briefings produced during the hardening pass:

- [Anticipated audience Q&A (35 Q&As)](./demo-qa-anticipated.md)
- [Stack quirks checklist (18 items)](./stack-quirks-checklist.md)
- [HIPAA / security defense brief (10 topics)](./hipaa-security-qa.md)
- [California workers-comp domain brief](./workers-comp-domain-brief.md)
- [Role-vs-permission matrix audit](./permission-matrix-audit.md)
- [Cross-tenant isolation audit](./cross-tenant-audit.md)
- [Live role probe findings (Patient nav leaks)](./role-probe-live-findings.md)
- [Demo data integrity verification](./demo-data-integrity.md)
- [Container logs sweep](./container-logs-sweep.md)
- [SPA bundle + load performance](./spa-load-perf.md)
- [Screenshots of each demo page](./screenshots/) (7 PNGs)
- [Open findings pocket answers (22 items audited)](./open-findings-pocket-answers.md)

## Top 5 audience-likely tangent questions + pocket answers

1. **"Is this HIPAA-compliant?"**
   > "The technical safeguards are in place -- TLS 1.2+ in transit,
   > role-based access via ABP, audit logging, password hashing via
   > PBKDF2. HIPAA compliance is an organizational state -- the
   > system is built to support a compliant deployment; the BAAs,
   > policies, and risk assessment are operational work that goes
   > with go-live."

2. **"Why do some users see SSN masked? Isn't that data in the
   system?"**
   > "We apply server-side role-based redaction per HIPAA Minimum
   > Necessary. Internal staff and the patient themselves see the
   > full value; external attorneys see only the last 4 digits with
   > the prefix masked. The full value never crosses the wire to an
   > unauthorized role -- defense in depth, not just CSS."

3. **"Can data leak between tenants?"**
   > "Shared DB with row-level filtering on TenantId, enforced at
   > the ORM layer on every query. Zero raw SQL, zero
   > IgnoreQueryFilters anywhere in the codebase. The tenant ID
   > comes from the JWT -- a user cannot pass a different tenant.
   > A recent audit flagged 4 hardening items for Phase 2 (none
   > demo-blocking)."

4. **"What's that 60-day deadline on the dashboard?"**
   > "CCR Section 31.5 governs replacement-panel timing for QME
   > evaluations. The label is the older 60-day version -- the rule
   > was amended to 90 days (waivable to 120). We haven't updated
   > the dashboard subtitle yet; it's a flagged polish item."

5. **"Why did this BUG-036 happen in the first place?"**
   > "A document-packet regeneration path silently failed when a
   > soft-deleted AttyCE row blocked a fresh INSERT against the
   > unique index. We shipped a 3-layer fix: filtered unique index,
   > OnCompleted deferral, and catch-filter widening. Regression
   > test covers it. The fix is what you're seeing live."

## Known surprises Adrian should know about but not surface unless asked

- (CORRECTED) Patient nav menu items ARE in the DOM but the
  sidebar is hidden by the `externaluser-role` body class. Patient
  login is visually safe. See revised role-probe doc.
- Dashboard "Billed This Month / No-Show / Cancelled This Week"
  cards show 0 (8 placeholder cards; feature ships in W3).
- API container had one transient `Cannot allocate memory`
  IOException at 22:53 PT (file globbing under WSL2 memory
  pressure); self-recovered, did not block.
- Single SQL "Login failed for Falkinstein" at 23:29 PT -- harmless,
  tenant uses shared DB, ABP fell back to default connection.
- MinIO community edition entered maintenance mode Dec 2025;
  production decision (commercial AIStor / fork / migrate to S3)
  is on the roadmap.

## Demo accounts

| Email | Password | Role | Confirmed |
|---|---|---|---|
| stafsuper1@gesco.com | 1q2w3E*r | Staff Supervisor | yes (seeded) |
| clistaff1@gesco.com | 1q2w3E*r | Clinic Staff | yes (seeded) |
| patient1@gesco.com | 1q2w3E*r | Patient (Alex Patient) | yes (SQL) |
| appatty1@gesco.com | 1q2w3E*r | Applicant Attorney (Aria Stone) | yes (SQL) |

## URLs

- SPA: `http://falkinstein.localhost:4200/`
- AuthServer: `http://falkinstein.localhost:44368/`
- API: `http://falkinstein.localhost:44327/`
- Hangfire dashboard: `http://falkinstein.localhost:44327/hangfire`
- MinIO console: `http://localhost:9001/` (creds: minioadmin /
  minioadmin)

## What was hardened today

- F4-01 SSN role-based redaction shipped (T1 server-side helper +
  unit tests, T2 dropped client CSS, T3 parity-flag doc).
- 4 research-backed Q&A briefings for likely audience questions.
- Permission matrix audit confirms role grants.
- Cross-tenant audit confirms 0 raw SQL, 0 IgnoreQueryFilters,
  9 findings (0 high) all medium-or-lower.
- Live role probe confirms Patient sees own appointments correctly.
- Demo data verified (3 appointments, 252 slots, 1 invite, 55
  Hangfire jobs all Succeeded).
- Container logs swept last hour (0 ObjectDisposedException, 0
  concurrency exceptions, 0 5xx, 0 EF queries >500ms).
- SPA load tested (1.2s warm cache, ~600-800 KB over wire
  compressed).
- Demo script doc corrected (removed misleading "23 stubs" framing
  about email templates).

## If something goes wrong mid-demo

- **Hangfire job stuck "Processing":** wait 30 min (InvisibilityTimeout)
  OR restart `main-api-1`. Don't restart between approve and
  packet visibility.
- **White screen / NullInjectorError CORE_OPTIONS:** rebuild
  Angular via `npx ng build --configuration development` then
  re-serve via `npx serve`. Never `ng serve`.
- **Browser hangs / spinning:** open fresh incognito window;
  refresh-token expiry may have caused stuck state.
- **API 401 spam in console:** that's OBS-40 pending-count
  anonymous poll, expected, ignore.
- **Audience asks for an UI feature that doesn't exist:** "That's
  on the post-parity roadmap; the v1 target was strict parity with
  the legacy app."
