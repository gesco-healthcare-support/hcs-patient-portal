# Probe log: document-packages

**Timestamp (local):** 2026-04-24T23:00:00
**Purpose:** Establish that NEW has zero HTTP surface related to `document-packages` / `package-details`, and cross-reference the file-management surface captured in the blob-storage-provider probe log.

## Probe 1 -- Swagger scan for any package-related path

### Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json | jq -r '.paths | keys[] | select(test("package|document-package|package-detail"; "i"))'
```

### Response

Status: 200 (on the JSON fetch).
Body (redacted): an empty line set on the filtered `jq` output. `.paths | length` returns 317. No key matches `package|document-package|package-detail` in any case.

### Interpretation

NEW has zero REST endpoints whose path contains the `package` token today. Combined with the source-tree grep for `DocumentPackage|PackageDetail` under `src/**/*.cs` returning zero matches, this confirms DB-13's "Not in NEW" claim at a live API level, not just at a source level.

### Cleanup (if mutating)

Non-mutating probe.

## Probe 2 -- Cross-reference: file-management surface is live

### Command

(Re-use the output from `../probes/blob-storage-provider-2026-04-24T13-00-00.md` Probe 1; do NOT re-run.)

### Response

Status: 200. 12 paths under `/api/file-management/**` including `file-descriptor/upload`, `directory-descriptor/create`, `file-descriptor/{id}`, etc.

### Interpretation

Folders-as-packages (alternative E) is technically implementable today via the live File Management module, but the structural constraints (no typed metadata beyond folder name) make it semantically lossy vs dedicated aggregates. This probe removes "is the API reachable?" from the alternative-evaluation reasoning; the rejection in the brief rests on schema mismatch, not plumbing.

### Cleanup (if mutating)

Non-mutating probe; cross-reference only.

## Probe 3 -- Appointment Types list endpoint reachability

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" https://localhost:44327/api/app/appointment-types
```

### Response

Status: 200.
Body (redacted):

```
{"totalCount":0,"items":[]}
```

### Interpretation

The host-scoped `AppointmentType` list is reachable via the admin-authenticated bearer, and it is empty today (no rows seeded). Document packages, if Q9=yes, would reference this list via FK and therefore gate on the `lookup-data-seeds` capability delivering AppointmentType rows before the admin UI is usable. The 200 + empty body matches the service-status.md line ("confirms zero seed data").

### Cleanup (if mutating)

Non-mutating probe.
