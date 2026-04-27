---
last-verified: 2026-04-24
---

<!-- Last reorganized 2026-04-24 -->

# Doc Streams Cross-Reference Index

## Context

Patient Portal documentation is produced by five parallel streams, each owned by a
distinct session and serving a distinct audience. Three streams are entity-keyed
(per-entity Markdown files), two are not. This index lets a reader land on any
entity and pivot across streams without guessing filenames, and lets a session
owner see at a glance which streams have coverage gaps for which entities. When
adding, renaming, or retiring a stream, this index is the single point of
update.

## Streams

### 1. Product intent -- `docs/product/`

- **Purpose:** what each entity is supposed to do, captured per stakeholder and
  per manager. Source of truth for "intended behavior" arguments.
- **Entry doc:** [../product/README.md](../product/README.md)
- **When to consult:** before changing entity behavior, before clarifying a
  business rule, when reconciling OLD vs NEW intent.
- **Session ownership:** docs-intended-behavior session.

### 2. OLD vs NEW gap analysis -- `docs/gap-analysis/`

- **Purpose:** inventory of what the OLD codebase has that NEW lacks, across
  schema, entities, services, APIs, auth, UI, and Angular plumbing.
- **Entry doc:** [../gap-analysis/README.md](../gap-analysis/README.md)
- **When to consult:** scoping MVP work, prioritising backports, answering "did
  OLD do X?". Completed 2026-04-23; treat as a frozen snapshot.
- **Session ownership:** gap-analysis session (closed 2026-04-23).

### 3. Implementation research -- `docs/implementation-research/`

- **Purpose:** per-capability solution briefs for the 39 capabilities derived
  from the gap analysis.
- **Entry doc:** `../implementation-research/README.md` (planned; folder not
  yet present as of 2026-04-24).
- **When to consult:** before starting a capability build; for "how should we
  implement X" decisions.
- **Session ownership:** implementation-research session.

### 4. Maintainer reference -- `docs/features/{entity}/overview.md`

- **Purpose:** how the code actually works per entity (entry points, services,
  data flow, gotchas). Inherited from the previous developer; kept in sync via
  `/sync-feature-to-docs`.
- **Entry doc:** [../features/](../features/) (no top-level README; index via
  `docs/INDEX.md`).
- **When to consult:** before editing code in a feature folder; for onboarding.
- **Session ownership:** maintainer-docs sync (per-feature, code-driven).

### 5. Issues register -- `docs/issues/`

- **Purpose:** bugs, incomplete features, security findings, data integrity
  notes, open questions. Inherited; reorganised in this PR's T4 so the
  `research/` subfolder holds per-ticket detail.
- **Entry doc:** [../issues/OVERVIEW.md](../issues/OVERVIEW.md)
- **When to consult:** triage; before claiming a bug is new; when planning a
  fix that may collide with a known open question.
- **Session ownership:** issues-triage session.

## Entity coverage matrix

Streams 1, 4 are entity-keyed. Streams 2, 3, 5 are not (see entry docs).

| Entity | Product intent | Gap analysis | Implementation research | Maintainer reference | Issues register |
| --- | --- | --- | --- | --- | --- |
| applicant-attorneys | [link](../product/applicant-attorneys.md) | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/applicant-attorneys/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| appointment-accessors | [link](../product/appointment-accessors.md) | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/appointment-accessors/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| appointment-applicant-attorneys | [link](../product/appointment-applicant-attorneys.md) | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/appointment-applicant-attorneys/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| appointment-employer-details | [link](../product/appointment-employer-details.md) | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/appointment-employer-details/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| appointment-languages | none | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/appointment-languages/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| appointment-statuses | none | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/appointment-statuses/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| appointment-types | none | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/appointment-types/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| appointments | [link](../product/appointments.md) | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/appointments/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| books | none | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/books/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| doctor-availabilities | [link](../product/doctor-availabilities.md) | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/doctor-availabilities/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| doctors | [link](../product/doctors.md) | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/doctors/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| locations | none | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/locations/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| patients | [link](../product/patients.md) | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/patients/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| states | none | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/states/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |
| wcab-offices | none | (stream is not entity-keyed -- see [README](../gap-analysis/README.md)) | none | [link](../features/wcab-offices/overview.md) | (stream is not entity-keyed -- see [OVERVIEW](../issues/OVERVIEW.md)) |

## Edit protocol

- **Adding a new stream:** add a section under "Streams" and a new column to
  the matrix. Update `docs/INDEX.md` to point readers here.
- **Renaming a stream's entry doc:** update the matrix link in every row plus
  the per-stream "Entry doc" line. Run a Glob over the new path before saving.
- **Adding a new entity:** add one row to the matrix; populate every cell
  (link, `none`, or the not-entity-keyed sentinel). Do not leave blanks.
- **Session-ownership table:** when a session is opened, closed, or reassigned,
  update the "Session ownership" line for that stream and note the date. The
  closed gap-analysis session (2026-04-23) is the worked example.
- **Date-stamping convention:** the file header carries `last-verified:
  YYYY-MM-DD` (frontmatter) and `<!-- Last reorganized YYYY-MM-DD -->` (HTML
  comment). Bump both when the matrix or stream list changes; bump only
  `last-verified` for routine link re-verification.
