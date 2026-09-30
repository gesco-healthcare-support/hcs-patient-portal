# AppointmentInfoRequests -- staff "Send Back / Request more information"

Staff flag specific booking fields on a Pending appointment and add a note; the
appointment moves Pending -> InfoRequested. The external user edits ONLY the flagged
fields and resubmits, which marks the open request Resolved and moves the appointment
back to Pending. One row per send-back round, so the table is the full request
history for an appointment.

No standalone Angular page -- the staff send-back modal lives in the Appointments
detail UI; the external fix-it page consumes the open request.

## What lives here

| File | Purpose |
|---|---|
| `AppointmentInfoRequest.cs` | Aggregate root: `FullAuditedAggregateRoot<Guid>`, `IMultiTenant`. One send-back round: `Note` + `RequestedFields` (JSON) + `Status` (Open/Resolved) + `BeforeValues`/`AfterValues` diff snapshots |

Related types live in sibling layers: the `InfoRequestStatus` enum and
`AppointmentInfoRequestConsts` in Domain.Shared; the manager, repository, and
`AppointmentInfoRequestsAppService` in their respective layers.

## Conventions

- **`Note` + `RequestedFields` are shown to the external user un-masked** (email +
  fix-it page), unlike `InternalUserComments`. Keep PHI out of the note.
- **`RequestedFields` is an opaque JSON array of field keys** (e.g.
  `["panelNumber","dateOfBirth","documents"]`) serialized by the Application layer.
  The keys align with the booking-form control names / the FlaggableField registry
  (`angular .../send-back-fields.ts`).
- **`BeforeValues` / `AfterValues`** are JSON maps captured at send-back vs resubmit
  (the staff diff); SSN is masked at capture. Null on rows created before that feature.

## Gotchas

1. **One Open row per round, never edited in place.** A new send-back opens a NEW
   Open row; resubmit calls `MarkResolved` (idempotent). Read the latest Open row,
   not a single mutable record.
2. **Server-side resubmit gate (QA F-018).** `ResubmitAsync` MUST verify the flagged
   fields were actually addressed before the InfoRequested -> Pending transition --
   the UI disable alone is bypassable by an API caller.
3. **`IMultiTenant` in both DbContexts.** Lives in host + tenant DBs; ABP's tenant
   data filter controls runtime visibility.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- angular `send-back-fields.ts` (the FlaggableField registry the keys map to)
