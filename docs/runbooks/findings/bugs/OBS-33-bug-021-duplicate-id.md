---
id: OBS-33
title: Duplicate BUG-021 id resolved by renaming login-tempdata finding to BUG-017
severity: observation
status: resolved
found: 2026-05-23 hardening Round 3 (meta-finding)
resolved: 2026-05-24
flow: findings-housekeeping
component: docs/runbooks/findings/bugs/
---

# OBS-33 - Duplicate BUG-021 finding ID (resolved)

## Symptom

Two finding files in `docs/runbooks/findings/bugs/` shared the id
`BUG-021` in their frontmatter:

- `BUG-021-ce-cannot-book.md` (filed 2026-05-14, status: open-low,
  downgraded to UX about the datepicker mass-disable)
- `BUG-021-login-tempdata-success-banner.md` (filed 2026-05-15,
  status: open, about login-success banner timing)

Both files had `id: BUG-021` in YAML frontmatter, despite the file
names being distinct.

Per Part 7 of the hardening test suite: "Increment N from the highest
existing in `docs/runbooks/findings/bugs/`." -- this was a process gap.

## Resolution

The earlier-filed finding (`-ce-cannot-book.md`, 2026-05-14) keeps
`BUG-021`. The later-filed finding (`-login-tempdata-success-banner.md`,
2026-05-15) was renamed to **BUG-017** -- the lowest free integer in
the sequence -- and its frontmatter `id:` field was updated to match.
(BUG-027 was the original suggestion in this observation but was
already taken by the reschedule-reason whitespace finding; BUG-017 and
BUG-019 are the only remaining gaps in the [BUG-001, BUG-039] range
across both `main` and `feat/replicate-old-app`.)

No code impact -- documentation hygiene only.

## Related

- HRD-R3 (Round 3 replay sweep surfaced this).
- [[BUG-017]] (post-rename location of the login-tempdata finding).
- [[BUG-021]] (datepicker mass-disable, unchanged).
