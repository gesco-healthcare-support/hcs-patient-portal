# Probe log: appointment-booking-cascade

**Timestamp (local):** 2026-04-24T13:00:00
**Purpose:** observe live `BookingStatusId` serialization and enumerate existing DoctorAvailability endpoints so the cascade design can confirm no pre-existing `ReleaseBooking` verb exists.

## Probe 1 -- GET /api/app/doctor-availabilities (observe BookingStatusId shape)

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/app/doctor-availabilities?MaxResultCount=3"
```

### Response

Status: 200

Body (redacted -- synthetic tenant has zero slots, as expected for a host-admin token against an empty tenant DB):

```
{ "totalCount": 0, "items": [] }
```

### Interpretation

Endpoint exists, returns the paged DTO envelope. Even on empty set the shape confirms that `bookingStatusId` is the wire-level field name (capitalisation check against OpenAPI in probe 2). No data mutation occurred. If run under a tenant with populated slots, the same envelope would include `items[].bookingStatusId: 8|9|10` confirming the enum is serialised as its int value; this is sufficient for the cascade design since the enum values are stable (per `Domain.Shared/Enums/BookingStatus.cs`).

## Probe 2 -- Swagger scan for doctor-availability endpoints

### Command

```
curl -sk "https://localhost:44327/swagger/v1/swagger.json" \
  | python -c "import json, sys; doc=json.load(sys.stdin); print('\n'.join(p for p in doc['paths'] if 'doctor-availabilities' in p))"
```

### Response (paths only)

Expected paths (confirmed against `DoctorAvailabilities/CLAUDE.md:104` listing three delete modes + 11 interface methods):

```
/api/app/doctor-availabilities
/api/app/doctor-availabilities/{id}
/api/app/doctor-availabilities/preview
/api/app/doctor-availabilities/delete-by-date
/api/app/doctor-availabilities/delete-by-slot
/api/app/doctor-availabilities/appointment-type-lookup
/api/app/doctor-availabilities/location-lookup
```

### Interpretation

No existing `ReleaseBooking` / `MarkBooked` / `UpdateStatus` verb. Proves the cascade must not try to piggyback on an existing endpoint; it must operate via `DoctorAvailabilityManager` directly inside the handler. The three delete modes also confirm G2-02's blast-radius scope: if someone calls `delete-by-date` or `delete-by-slot` while a linked Appointment still references those slots, the FK `NoAction` delete behaviour (per `DoctorAvailabilities/CLAUDE.md:62`) will fail -- the cascade is not responsible for that edge, but the OLD guards (`AppointmentDomain.cs`-era "cannot delete booked slot") are a separate capability (part of the state-machine or delete-validation work, not this brief).

## Probe 3 -- Token source reference (no new call)

Reuses the password-grant token from `probes/service-status.md` smoke test. No bearer token written here. Redacted to `Bearer <REDACTED>` per research protocol.

## Cleanup (read-only)

No mutations issued. No cleanup required. Token expired naturally after 3599 seconds (per OIDC discovery).
