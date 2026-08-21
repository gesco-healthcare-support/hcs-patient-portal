---
id: AF5
title: Model the PQME panel strike list as a boolean flag on AppointmentDocument
type: enhancement
components: [angular/src/app/appointment-documents/appointment-documents.component.ts, angular/src/app/appointments/appointment/components/appointment-view.component.html, src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentDocument.cs, src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs]
related_known_bugs: [parity-v2/03 G-03-01, BUG-037]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
PQME appointments need a "panel strike list" document. Internal staff use it for MANUAL
venue verification (confirming the appointment was booked at the correct doctor office that
appears on the panel). The portal has no way to mark one uploaded document as "the panel
strike list", so staff cannot distinguish it from any other ad-hoc upload.

This note is the CONCEPT/data-model item. It defines the flag that AF6 (the booking-form
checkbox + mandatory-upload gate) and AF7 (pre-submit upload mechanism) build on.

## Current behavior (from investigation)
- No panel-strike-list concept exists. `AppointmentDocument` carries no document-type field
  -- only two domain booleans plus a free-text name (findings AF5 current_behavior, line
  223).
- `IsAdHoc` flag at `AppointmentDocument.cs:71` and `IsJointDeclaration` flag at
  `AppointmentDocument.cs:78` are the established "one boolean per special document kind"
  precedent (each mirrors an OLD sibling table; NEW unifies via a flag, Phase 1.6
  2026-05-01).
- Free-text `DocumentName` at `AppointmentDocument.cs:35` is the only per-document label
  today; `DocumentStatus Status` at `AppointmentDocument.cs:53` already exists for review
  state.
- The `AppointmentDocumentType` master lookup library is DELIBERATELY not ported
  (`AppointmentDocument.cs:16` "free-text DocumentName at MVP"; parity-v2/03 G-03-01).
- PQME is the seeded type currently named "Panel QME" at
  `AppointmentTypeDataSeedContributor.cs:49-51`, keyed by GUID
  `CaseEvaluationSeedIds.AppointmentTypes.PanelQme` (NOT a name substring). Per the locked
  type decision this seed is renamed/labelled PQME.
- Documents UI mounts only on the appointment-view page, after the appointment exists, at
  `appointment-view.component.html:940-944` (`<app-appointment-documents>`).

## Relevant code locations
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentDocument.cs`
  -- add the new flag next to `IsAdHoc` / `IsJointDeclaration` (lines 71, 78); decide the
  constructor/factory surface.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs`
  -- EF config for `AppointmentDocument` (column mapping; bool defaults false -- no config
  change strictly required, verify).
- DTOs:
  `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentDocuments/*`
  (Create/Dto/Update) -- surface `IsPanelStrikeList` for read + the create path AF6/AF7 use.
- `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs`
  -- upload paths (findings cite :149-230) carry the flag through.
- Mapperly: `CaseEvaluationApplicationMappers.cs` partials -- map the new bool.
- `angular/src/app/appointment-documents/appointment-documents.component.ts` -- display/badge
  the panel-strike-list marker in the document list.
- `angular/src/app/proxy/` -- regenerate after the DTO change (never hand-edit).

## Phase 3 cross-reference
- BUG-037 (staff 403 on upload): internal staff are the ones who read the panel strike list
  for venue verification; if staff cannot reliably upload/view documents the flag is
  cosmetic. Confirm BUG-037 is in the AF6/AF7 build window so the strike-list path is
  exercised by the role that consumes it. Bundle the fix verification with this work.
- parity-v2/03 G-03-01 (document-type master deferred): keep it deferred. This note is the
  cheap alternative to reviving that master; cite G-03-01 in the migration note so the
  deferral stays intentional, not forgotten.

## Research findings
- Internal patterns / prior art:
  - The boolean-flag-per-document-kind pattern is already in the codebase TWICE (`IsAdHoc`
    71, `IsJointDeclaration` 78). Each replaced an OLD sibling table. `IsPanelStrikeList`
    is the third instance of the same decided pattern -- zero new architecture.
  - `DocumentStatus` (line 53) shows the entity already carries review-state metadata, so
    adding orthogonal classification metadata is consistent with the entity's role.
  - PQME identity is keyed by seed GUID (`PanelQme` in the seed array, lines 49-51),
    matching the locked "key off PQME seed GUID, not a name substring" decision used by the
    panel-number rule.
- External docs (ABP / EF Core) if relevant:
  - Adding a non-nullable `bool` column to an EF Core code-first entity defaults to
    `false` for existing rows; no manual backfill needed (EF Core treats `bool` default as
    `0`). Verify the generated migration sets `defaultValue: false` or that existing rows
    are acceptable as `false` (they are -- no historical doc is a strike list).

## Approaches considered (with tradeoffs)
1. NEW boolean `IsPanelStrikeList` on `AppointmentDocument` (CHOSEN). Mirrors the two
   existing flags exactly; one nullable-safe column; one migration; trivial Mapperly/DTO/
   proxy delta. Cost: a third boolean accretes on the entity (acceptable -- still well under
   the class/field thresholds and matches established style).
2. Revive the deferred `AppointmentDocumentType` master lookup (parity-v2/03 G-03-01).
   REJECTED: heaviest option -- new entity, seed, CRUD, AppService, controller, FK on
   `AppointmentDocument`, two-DbContext config, more proxy. Solves a generality (arbitrary
   typed documents) that nobody asked for; the feedback names exactly ONE document kind.
   Over-engineering per the locked decision and the code-standards no-speculative-generality
   rule.
3. Encode it in free-text `DocumentName` (e.g. name == "Panel Strike List"). REJECTED:
   string matching is brittle, not queryable for the AF6 mandatory-upload gate, breaks on
   localization/typos, and cannot be a server-enforced invariant. Fails the validation rule
   that integrity gates live server-side.
4. New dedicated table for strike lists. REJECTED: a table to hold one boolean's worth of
   meaning is strictly worse than option 2 and contradicts the locked "no new table"
   decision.

Why the chosen direction wins: it is the minimum change that makes the strike list a
first-class, server-queryable fact (so AF6 can enforce mandatory upload and staff UI can
badge it), reuses a pattern the codebase has committed to twice, and keeps the deferred
document-type master deferred.

## Decision (locked 2026-06-03)
Add a NEW boolean flag `IsPanelStrikeList` on `AppointmentDocument`, mirroring `IsAdHoc` /
`IsJointDeclaration`. No new table. No `AppointmentDocumentType` master. The flag marks the
one uploaded document that internal staff use for manual venue verification on PQME
appointments. This note is the data-model foundation for AF6 (checkbox + mandatory-upload
gate, keyed off the PQME seed GUID) and AF7 (pre-submit upload mechanism).

## Implementation outline (no code)
1. Domain: add `public virtual bool IsPanelStrikeList { get; set; }` to
   `AppointmentDocument.cs` beside the existing flags (71/78); add matching XML docstring.
   Decide whether to thread it through the public constructor or set it post-construct on the
   upload path -- match whatever AF7's create-then-upload flow needs (coordinate with AF7).
2. EF / migration: confirm the bool maps with `default false`; generate ONE migration adding
   the column to BOTH DbContexts' `AppointmentDocument` config if explicit config is present
   (dual-context rule). FLAG: new migration. Existing rows default `false` (no backfill).
3. Application.Contracts: surface `IsPanelStrikeList` on the document DTO (read) and on the
   create DTO the AF6/AF7 upload path uses (write).
4. Application: thread the flag through the relevant upload method(s) in
   `AppointmentDocumentsAppService.cs` (findings :149-230). Mapperly map the new bool in
   `CaseEvaluationApplicationMappers.cs`.
5. Server enforcement (deferred specifics to AF6): the "exactly one strike list when PQME"
   and "mandatory before submit" rules are SERVER-side gates owned by AF6; this note only
   guarantees the flag is persisted and queryable. Note the seam so AF6 enforces in the DTO
   + domain manager and mirrors in UI.
6. Proxy: FLAG `abp generate-proxy` after the DTO change; delete any inline DTO copy.
7. Angular display: in `appointment-documents.component.ts`, render a badge/label when
   `isPanelStrikeList` is true so internal staff can spot it for venue verification.

## Dependencies
- BLOCKS AF6 (mandatory-upload gate needs this queryable flag) and AF7 (the create path that
  sets the flag pre-submit).
- DEPENDS ON: the locked appointment-type decision (PQME seed GUID identity) for AF6's
  keying; no hard code dependency for this note's flag alone.
- Coordinate constructor/factory surface with AF7's pre-submit upload mechanism.

## Residual open questions
- Whether to expose the flag on the public constructor vs set-after-create -- resolve jointly
  with AF7's create-then-upload (Option A) shape. Minor; does not change the data model.
