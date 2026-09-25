# ADR-010: PDF packets replace DOCX

**Status:** Accepted
**Date:** 2026-05-29
**Verified by:** code-inspect

## Context

The legacy app generated appointment packets as `.docx` files. A Word document is trivially
editable, so a delivered medical-evaluation packet could be altered after the fact, undermining the
integrity of what the office sent.

## Decision

The new app generates packets as PDF. A Hangfire background job merges the source documents into an
`AppointmentPacket` row (one per `(TenantId, AppointmentId, PacketKind)`). PDF is immutable enough
for delivery -- recipients cannot trivially edit it. All packet business logic (which documents are
included, role access, ordering) is preserved from the legacy behavior; only the output format and
its mutability change.

## Consequences

- Delivered packets are tamper-resistant.
- Generation is asynchronous (Hangfire), so the booking/upload request is not blocked on rendering.
- The Docker stack includes a PDF renderer service (gotenberg) the dev/prod environment must run.
- "Editing" a packet means regenerating it, not patching the file in place.

## Alternatives Considered

- Keep DOCX output -- rejected: editable by recipients.
- Password-protected / read-only DOCX -- rejected: weak protection, clunky for recipients.
- PDF/A archival format -- deferred: standard PDF is sufficient for the MVP delivery use case.
