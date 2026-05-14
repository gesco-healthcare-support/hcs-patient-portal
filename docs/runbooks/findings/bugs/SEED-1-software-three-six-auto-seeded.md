---
id: SEED-1
title: SoftwareThree/Four/Five/Six@gesco.com pre-seeded but should not be
severity: n/a
status: fixed
fixed-in: PR #197
found: 2026-05-13
flow: data-seeding
component: Domain/Identity/DemoExternalUsersDataSeedContributor.cs
---

# SEED-1 — `Software{Three..Six}@gesco.com` auto-seeded

## Severity
n/a (data seeding gotcha, not a runtime bug)

## Status
**FIXED in PR #197** — `foreach` loop over `InboxedExternalUsers` removed; the constant array preserved as documentation.

## Adrian's intent (2026-05-13)
*"I never wanted them to be seeded by default; they are for real users tests."*

The four `Software{Three|Four|Five|Six}@gesco.com` users were being created with `EmailConfirmed=0` on every `docker compose down -v && up` cycle, biasing the BUG-001..003 register walks (the email was always "already used" when the test session got to it).

## Root cause
`DemoExternalUsersDataSeedContributor.cs:49-55` defined `InboxedExternalUsers` and lines 134-153 seeded them into every tenant on every fresh DB. Introduced in PR #186 / Issue #119.

## Recommended fix (applied in PR #197)
1. Delete lines 134-153 (the `// Issue #119 ...` comment block + the `foreach (var (email, roleName, first, last, phone) in InboxedExternalUsers)` loop).
2. Keep the `InboxedExternalUsers` static array (lines 49-55) as documentation constant — it's the canonical email→role mapping for manual tests.
3. Update `docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md` Part 4 to drop the @gesco.com rows from the "always seeded" table; note self-register required.

## Related
- [[SEED-2]] (DemoDoctorDataSeedContributor — corresponding seed for doctors that's the inverse problem: we WANT them seeded but they aren't).
