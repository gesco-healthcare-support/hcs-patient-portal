# ADR-011: Per-role packet access (PacketVisibility allow-list)

**Status:** Accepted
**Date:** 2026-05-29
**Verified by:** code-inspect

## Context

Different parties on an appointment should see different packet kinds. `PacketKind` has Patient,
Doctor, and AttyCE (attorney / claim-examiner) variants. A naive "anyone on the appointment can
download any packet" rule would deliver the wrong packet kind to the wrong party.

## Decision

`PacketVisibility` defines an allow-list mapping each caller role to the `PacketKind`(s) it may
download, enforced on the packet-download endpoints per kind. The AttyCE packet is accessible to all
attorney and claim-examiner roles (the "all-type AttyCE" grant). The Doctor-kind packet is generated
and stored for completeness but has no delivery/email path.

## Consequences

- Each party downloads only the packet kinds permitted for its role; cross-party leakage is closed.
- The all-type AttyCE grant keeps the attorney/claim-examiner matrix simple (one shared kind).
- Adding a new role or a new `PacketKind` requires updating the allow-list in one place.
- The Doctor packet exists in storage with no outbound delivery path -- intentional, not a gap.

## Alternatives Considered

- A single shared packet for all appointment parties -- rejected: leaks patient/attorney content
  across parties.
- Per-document ACLs -- rejected: too granular for the MVP; packet-kind granularity is sufficient.
- Generate packets on demand per requester -- rejected: expensive, and loses the pre-generated,
  auditable packet artifact.
