# ADR-015: Reporting grid + PDF export (Group M)

**Status:** Accepted
**Date:** 2026-06-06
**Verified by:** unit + integration tests; live Playwright check (grid)

## Context

The legacy app had an Appointment Request Report -- an internal-staff worklist
listing every appointment request across patients (ten columns, including
PII/PHI: name, DOB, email, phone, SSN), with a quick search, advanced filters
(type, location, status, patient name, date range), and "Export to PDF" plus a
per-appointment demographics print. NEW had nothing in this area. The legacy
report row-selection proc is not in source; its column/filter set is recovered
from the legacy Angular grid. Legacy also shipped full SSN/DOB unmasked behind
an unauthenticated export endpoint -- a PHI bug we must not reproduce.

Group M ships as three slices: the grid (this PR), the report-table PDF, and the
per-appointment demographics PDF.

## Decision

- **Reuse the shared appointment query.** The report is a read-only projection
  over the existing `IAppointmentRepository.GetListWithNavigationPropertiesAsync`
  / `GetCountAsync` (already paged, sorted, and filtered by text/type/location/
  status/date). No report-specific proc or query is introduced.
- **Mask PHI at the Application boundary, in one shared redactor.**
  `ReportRowRedactor` builds the masked row DTO from a raw `ReportRowSource`
  (which never leaves the layer): SSN -> last 4 (`SsnVisibility`), DOB -> birth
  year (`DobVisibility`), name/email/phone shown in full (an internal worklist
  needs them to identify and contact). The full SSN is available only via the
  audited `Patients.RevealSsn` endpoint (ADR-009), never the report. The same
  redactor will feed the report-table PDF so masking cannot diverge.
- **Internal-only.** A new read-only `Reports` permission, granted to IT Admin /
  Staff Supervisor / Clinic Staff only (the legacy audience); external roles
  never receive it. The Angular route, nav entry, and AppService are all gated.
- **PDF via QuestPDF in-process, in the Application layer** (the follow-up PR2/3
  decision, recorded here for the whole group): QuestPDF is already registered
  (Community license, set process-globally in `CaseEvaluationDomainModule`) but
  unused; the report PDFs are the first real renders. The package reference is
  added to `Application` so the renderer can read the already-masked DTOs without
  a Domain->Contracts dependency inversion. This replaces the legacy
  HTML-print/DOCX output with an immutable PDF (ADR-010 mission), and avoids a
  synchronous dependency on the Gotenberg sidecar (which the packet pipeline uses
  for async DOCX conversion).

## Consequences

- The report is additive and low-risk: no existing endpoint/route is modified.
- Masking is enforced server-side in C# (the Angular `ssn-mask` pipe does not
  reach a server-rendered PDF), in a single seam shared by grid and PDF.
- The legacy "Patient Name" advanced filter is folded into the quick search,
  because the shared query's `FilterText` already spans patient first/last name
  (plus panel + confirmation number). One fewer control; same capability.
- The grid uses a manual masking projection (`ReportRowSource` -> redactor)
  rather than a Mapperly mapper, so masking lives in exactly one place.
- The demographics PDF (PR3) will be gated internal-only as well (not the
  per-appointment read guard alone), because it aggregates cross-party PHI and
  the legacy app exposed it only to internal staff.

## Alternatives Considered

- **A report-specific stored proc / query (literal legacy port).** Rejected: the
  proc body is not in source, and it would re-import the legacy auth-bypass +
  static-cache PHI bugs. The EF query is the mission-correct translation.
- **Extend the appointments grid with a "report mode."** Rejected: muddies the
  appointments audience/column model and risks leaking PHI columns to roles that
  see the appointments list but not the report. Parity expects a separate,
  separately-permissioned screen.
- **Gotenberg HTML->PDF for the exports.** Rejected: adds a synchronous sidecar
  dependency to a user-facing download and needs the unwired Chromium route.
  QuestPDF renders in-process and is mission-aligned.
- **A Mapperly row mapper + separate masking step.** Rejected in favor of the
  single redactor seam, so a masked field can never be emitted unmasked.
