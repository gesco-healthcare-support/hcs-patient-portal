# Architecture Decision Records (ADRs)

This directory captures significant architectural and technical decisions for the
CaseEvaluation Patient Portal. Each ADR follows the [Nygard format](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
and records the context, decision, consequences, and alternatives considered.

## How to Add a New ADR

1. Copy the template below into a new file: `NNN-short-title.md`
2. Number sequentially (next available: 006)
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
