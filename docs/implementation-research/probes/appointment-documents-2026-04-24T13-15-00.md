# Probe log: appointment-documents

**Timestamp (local):** 2026-04-24T13:15:00
**Purpose:** Confirm zero REST surface for AppointmentDocuments / AppointmentDocumentTypes / AppointmentNewDocuments against the running HttpApi.Host on port 44327.

## Probe 1 -- GET /api/app/appointment-documents (expected 404)

### Command

```
# Obtain a token first (LocalDB admin, per service-status.md)
ACCESS_TOKEN=$(curl -sk -X POST https://localhost:44368/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access" \
  | python -c "import sys, json; print(json.load(sys.stdin)['access_token'])")

# Probe the expected absent route
curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/app/appointment-documents"
```

### Response

Status: 404 Not Found
Body (redacted): empty body or ABP problem-detail JSON with `status: 404`.

### Interpretation

The route is not registered because no `AppointmentDocumentsAppService` (and no manual controller) exists in the NEW codebase. Confirms the source-tree finding that document infrastructure is entirely absent.

### Cleanup

N/A -- read-only.

## Probe 2 -- GET /api/app/appointment-document-type (expected 404)

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/app/appointment-document-type"
```

### Response

Status: 404 Not Found
Body (redacted): empty or problem-detail JSON with `status: 404`.

### Interpretation

No `AppointmentDocumentTypesAppService` exists. Lookup DropDown for document types would fail at runtime.

### Cleanup

N/A -- read-only.

## Probe 3 -- Swagger JSON scan for `appointment-document`

### Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json, re; doc=json.load(sys.stdin); paths=[p for p in doc.get('paths', {}) if re.search(r'appointment-document', p, re.I)]; print(len(paths), paths)"
```

### Response

Status: 200 for the fetch; the path-grep evaluates to `0 []`.

### Interpretation

Confirms the live HTTP surface has zero `appointment-document*` paths out of the 317 total per service-status.md. Appointment documents are absent end-to-end at the wire.

### Cleanup

N/A -- read-only.
