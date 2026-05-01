# Probe log: appointment-change-log-audit

**Timestamp (local):** 2026-04-24T22:30:00
**Purpose:** Confirm that (a) ABP's generic audit-logging endpoints are already
live on the NEW API, (b) the entity-change table is empty because no entity is
tagged `[Audited]`, and (c) no appointment-specific change-log endpoint exists.

## Probe 1 -- ABP audit-logs list endpoint

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/audit-logging/audit-logs?MaxResultCount=3"
```

Token obtained via host admin password grant on
`https://localhost:44368/connect/token`
(`admin / 1q2w3E*`). Token lives only in the probe shell; never copied into the
brief.

### Response

Status: 200

Body (redacted, first ~600 chars):

```
{"totalCount":1302,"items":[{"userId":null,"userName":null,"tenantId":null,
 "tenantName":null,"impersonatorUserId":null,"impersonatorUserName":null,
 "impersonatorTenantId":null,"impersonatorTenantName":null,
 "executionTime":"2026-04-24T13:15:38.9344822","executionDuration":335,
 "clientIpAddress":"::1","clientId":null,"clientName":null,
 "browserInfo":"curl/8.18.0","httpMethod":"POST","url":"/connect/token",
 "exceptions":null,"comments":"","httpStatusCode":200,
 "applicationName":"AuthServer","correlationId":"b644dce0abf94b1fb6e72b77f9e73e92",
 "entityChanges":null,"actions":null,"id":"a65f898d-5d30-c433-...
```

### Interpretation

The ABP AuditLogAppService is live on the HttpApi.Host at
`/api/audit-logging/audit-logs`, returning the ABP-standard envelope. Response
carries `applicationName` (stamped by
`CaseEvaluationAuthServerModule.cs:175`), `correlationId`, `entityChanges`
(null here because no entity is yet tagged), and the standard per-request
columns. Captured row count is 1302 across the session -- proves the audit
pipeline is writing on its own without any code changes.

### Cleanup (if mutating)

N/A -- read-only GET.

## Probe 2 -- Entity-change feed

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/audit-logging/audit-logs/entity-changes?MaxResultCount=3"
```

### Response

Status: 200

Body:

```
{"totalCount":0,"items":[]}
```

### Interpretation

The `/entity-changes` endpoint exists and serves 200, but the
`AbpEntityChanges` table has zero rows. Matches the source read: no entity in
the NEW codebase carries `[Audited]` and there is no
`AbpAuditingOptions.EntitySelectors` registration. Confirms Alternative A's
premise (tagging `Appointment` with `[Audited]` will start populating this
feed immediately) and that the built-in Angular `/audit-logs` viewer currently
has an empty entity-change pane.

### Cleanup (if mutating)

N/A -- read-only GET.

## Probe 3 -- Appointment-specific change-log endpoint

### Command

```
curl -sk -o /dev/null -w 'HTTP_STATUS=%{http_code}\n' \
  -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/app/appointment-change-logs"
```

### Response

Status: 404

Body: (empty -- standard ABP 404 for missing route).

### Interpretation

Confirms no `/api/app/appointment-change-logs` controller exists in the NEW
HttpApi. Supports G-API-09 (absent group) and R-03 (absent Angular route).
Cross-checks a broader Swagger scan:
`grep -ioE '/api/app/appointment-change[^"]*'` against
`/swagger/v1/swagger.json` returns zero matches across the 317 total paths.

### Cleanup (if mutating)

N/A -- read-only GET.

## Confirmed invariants

- NEW already ships the full ABP Audit Logging module stack (server + Angular).
- No appointment-scoped change-log endpoint exists; no entity is tagged
  `[Audited]`; no `EntitySelectors` registration was found.
- Appointment audit can be turned on with a single attribute plus a permission
  constant -- no new migration, no new DTO, no new controller.
