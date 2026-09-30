# Architecture Decision Records (ADRs)

This directory captures significant architectural and technical decisions for the
CaseEvaluation Appointment Portal. Each ADR follows the [Nygard format](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
and records the context, decision, consequences, and alternatives considered.

## How to Add a New ADR

1. Copy the template below into a new file: `NNN-short-title.md`
2. Number sequentially (next available: **018**; corrected 2026-09-29, this said 016 while 016 and
   017 already existed, so following it would have overwritten an existing ADR)
3. Fill in all sections -- keep it concise (40-80 lines)
4. Set Status to `Proposed` until reviewed, then `Accepted`
5. If a decision is later reversed, set Status to `Superseded by ADR-NNN`

## ADR Index

| # | Title | Status | Date |
|---|---|---|---|
| [001](001-mapperly-over-automapper.md) | Riok.Mapperly over AutoMapper | Accepted | 2026-04-10 |
| [002](002-manual-controllers-not-auto.md) | Manual controllers instead of ABP auto-controllers | Accepted | 2026-04-10 |
| [003](003-dual-dbcontext-host-tenant.md) | Dual DbContext for host and tenant databases | Accepted | 2026-04-10 |
| [004](004-doctor-per-tenant-model.md) | One doctor per tenant multi-tenancy model | Accepted | 2026-04-10 |
| [005](005-no-ng-serve-vite-workaround.md) | Static serve workaround for Angular 20 Vite bug | Accepted | 2026-04-10 |
| [006](006-subdomain-tenant-routing.md) | Subdomain tenant routing + database-per-tenant | **Accepted, in force**; partly superseded by 007 | 2026-05-05 |
| [007](007-host-aware-tenant-resolver.md) | Host-aware subdomain tenant resolver | Accepted | 2026-05-11 |
| [008](008-capacity-aware-slot-booking.md) | Capacity-aware slot booking | Accepted | 2026-05-15 |
| [009](009-audited-ssn-reveal.md) | Audited SSN reveal (design B) | Accepted | 2026-05-29 |
| [010](010-pdf-packets-replace-docx.md) | PDF packets replace DOCX | Accepted | 2026-05-29 |
| [011](011-per-role-packet-access.md) | Per-role packet access (PacketVisibility) | Accepted | 2026-05-29 |
| [012](012-audit-change-log-redaction.md) | Appointment change-log redaction + diff-at-update email | Accepted | 2026-06-06 |
| [013](013-config-driven-reminder-cadence.md) | Config-driven reminder cadence (Group L / G-05) | Accepted | 2026-06-06 |
| [014](014-appointment-edit-authorization.md) | Appointment edit authorization (permission gate) | Accepted | 2026-06-06 |
| [015](015-reporting-grid-and-pdf.md) | Reporting grid + PDF export (Group M) | Accepted | 2026-06-06 |
| [016](016-remove-gotenberg-html-only-packets.md) | HTML-only packet rendering; remove Gotenberg + the DOCX path | Accepted; supersedes the DOCX part of 010 | 2026-06-10 |
| [017](017-database-per-office-isolation.md) | Database-per-office isolation model | Accepted | 2026-06-25 |

Rows 016 and 017 were **added on 2026-09-29**. Both files existed and neither was in this index, so
`017`, which is the PHI isolation model and the security gate for the database-per-office migration,
could not be found by anyone browsing the index. If you add an ADR, add its row here in the same
change: an index that silently omits entries is worse than no index, because it reads as complete.

**Supersession was only ever recorded forwards, and rule 5 above needs it recorded both ways.** The
newer ADR names what it replaces, and the replaced one said nothing. So ADR-010 read a plain
`Accepted` while ADR-016 had removed its DOCX-rendering half, and ADR-006 read `Proposed` while
ADR-007 recorded superseding part of it. Both were corrected on 2026-09-29. **When you supersede an
ADR, edit the superseded one too** -- its reader is the one who needs to know, and they have no
reason to go looking at a later number.

## Template

```markdown
# ADR-NNN: Title

**Status:** Proposed
**Date:** YYYY-MM-DD
**Verified by:** code-inspect

## Context
{What prompted this decision? What constraints existed?}

## Decision
{What was decided?}

## Consequences
{What are the trade-offs? What becomes easier/harder?}

## Alternatives Considered
{What other approaches were evaluated and why they were rejected?}
```
