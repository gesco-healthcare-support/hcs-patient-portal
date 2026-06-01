# Architecture Decision Records (ADRs)

This directory captures significant architectural and technical decisions for the
CaseEvaluation Appointment Portal. Each ADR follows the [Nygard format](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
and records the context, decision, consequences, and alternatives considered.

## How to Add a New ADR

1. Copy the template below into a new file: `NNN-short-title.md`
2. Number sequentially (next available: 012)
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
| [006](006-subdomain-tenant-routing.md) | Subdomain tenant routing + database-per-tenant | Proposed | 2026-05-05 |
| [007](007-host-aware-tenant-resolver.md) | Host-aware subdomain tenant resolver | Accepted | 2026-05-11 |
| [008](008-capacity-aware-slot-booking.md) | Capacity-aware slot booking | Accepted | 2026-05-15 |
| [009](009-audited-ssn-reveal.md) | Audited SSN reveal (design B) | Accepted | 2026-05-29 |
| [010](010-pdf-packets-replace-docx.md) | PDF packets replace DOCX | Accepted | 2026-05-29 |
| [011](011-per-role-packet-access.md) | Per-role packet access (PacketVisibility) | Accepted | 2026-05-29 |

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
