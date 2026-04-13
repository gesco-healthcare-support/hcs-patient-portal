[Home](../INDEX.md) > Runbooks > Incident Response

# Incident Response Runbook

Technical playbook for suspected security incidents, particularly PHI exposure events. This is a **technical runbook only**, not legal advice. Consult HIPAA compliance counsel for legal obligations including breach notification timelines and regulatory reporting.

**Scope:** This runbook applies only to the CaseEvaluation Patient Portal codebase. Organizational incident response (workforce actions, BAA review, public communications) is out of scope.

---

## What Counts as an Incident

Any of the following should trigger this runbook:

1. **PHI exposure** -- PHI visible to an unauthorized user (wrong tenant, unauthenticated, excessive permission scope)
2. **Unauthorized access** -- successful authentication by an account that should not have been authorized (stolen credentials, account takeover)
3. **Data breach** -- PHI read, copied, or exfiltrated by an unauthorized actor
4. **Integrity compromise** -- PHI modified or deleted by an unauthorized actor, or unexplained modifications appearing in audit logs
5. **Credential compromise** -- ABP license code, signing cert password, DB password, or NuGet API key exposed to an unauthorized party

Lower-severity issues (suspicious log entries, failed auth spikes) should be triaged but may not require the full runbook.

---

## Immediate Actions (first 15 minutes)

**Do not delete anything.** Preservation of evidence is critical.

1. **Identify scope** (rough estimate, refine later):
   - Which users are affected?
   - Which patient records (by ID count, not by name)?
   - What time window?
   - Which service logged the event (API, AuthServer, DB)?
2. **Preserve evidence:**
   - Stop log rotation on the affected service(s). Back up current `Logs/` directory.
   - Snapshot the database (backup, not restore).
   - Capture Redis state if incident is recent (`redis-cli --rdb dump.rdb`).
3. **Contain:**
   - Disable affected user accounts (do not delete) via ABP Identity admin UI or direct DB update:
     ```sql
     UPDATE AbpUsers SET IsActive = 0 WHERE Id = '<user-id>';
     ```
   - If credential compromise: rotate the affected secret immediately (see [SECRETS-MANAGEMENT.md](../security/SECRETS-MANAGEMENT.md)) and invalidate all active tokens by restarting AuthServer.
4. **Notify:** Alert the business owner and any on-call engineer. Do not yet notify external parties.

---

## Investigation Steps

### 1. Check ABP audit logs

ABP's audit log table records entity Create/Update/Delete operations with the invoking user ID.

```sql
-- Recent activity by a suspect user
SELECT TOP 100 ExecutionTime, ServiceName, MethodName, HttpStatusCode, Parameters
FROM AbpAuditLogs
WHERE UserId = '<user-id>'
ORDER BY ExecutionTime DESC;

-- Recent modifications to a specific entity
SELECT ExecutionTime, UserId, ServiceName, MethodName
FROM AbpAuditLogs l
JOIN AbpEntityChanges c ON c.AuditLogId = l.Id
WHERE c.EntityId = '<entity-id>'
ORDER BY ExecutionTime DESC;
```

### 2. Check Serilog output files

Logs live at `Logs/logs.txt` (rolling, by default) in the HttpApi.Host and AuthServer projects.

**Critical caveat:** Per SEC-02 (active gap), PII logging is enabled by default. Log files themselves may contain PHI. Treat log files as PHI until SEC-02 is remediated.

Grep patterns to try:
```bash
grep -E "UserId=<id>" Logs/logs*.txt
grep -E "Exception|Error|Warning" Logs/logs*.txt | tail -200
```

### 3. Check database for unauthorized modifications

```sql
-- All appointments modified in the last N hours
SELECT Id, LastModificationTime, LastModifierId, TenantId
FROM Appointments
WHERE LastModificationTime > DATEADD(HOUR, -24, GETUTCDATE())
ORDER BY LastModificationTime DESC;

-- Patients across tenants (Patient lacks IMultiTenant)
SELECT TenantId, COUNT(*) AS PatientCount
FROM Patients
GROUP BY TenantId;
```

### 4. Check Redis cache

Active permission grants and data protection keys live in Redis. An attacker with Redis access may have cached credentials:
```bash
docker compose exec redis redis-cli KEYS "*"
docker compose exec redis redis-cli GET "<key>"
```

### 5. Cross-tenant PHI query check (Patient-specific)

Because `Patient` does not implement `IMultiTenant`, audit any custom query code for missing tenant filter:
```bash
grep -rn "_patientRepository\|IPatientRepository" src/ --include="*.cs"
```
Review each hit; confirm the query includes `TenantId == CurrentTenant.Id` or equivalent.

---

## PHI Inventory (for breach scoping)

Use this to determine what PHI may have been exposed:

| Entity | PHI Fields |
|---|---|
| Patient | First/last name, DOB, SSN, phone, email, address, gender, marital status |
| Appointment | Claim number, date of injury (DOI), status, scheduling notes, patient + doctor link |
| AppointmentEmployerDetail | Employer name, occupation, employer address |
| AppointmentAccessor | Links an identity user to an appointment (exposes the relationship, not PHI fields) |

See [DATA-FLOWS.md](../security/DATA-FLOWS.md) for the full PHI map.

---

## Notification Considerations

> This is a technical summary. Legal obligations take precedence. Confirm with HIPAA counsel.

- **HIPAA Breach Notification Rule (45 CFR 164.404--414):** generally requires notification within 60 days of discovery, to affected individuals and to HHS. Breaches affecting 500+ individuals also require media notification in the affected state.
- **Business Associate Agreement (BAA):** if a cloud vendor is involved, review the BAA for vendor notification timelines (often 24 hours).
- **State laws:** California CMIA imposes faster timelines than federal HIPAA in some cases.
- **Internal:** document the incident, affected records, containment steps, and root cause within the organization's incident log (outside this repo).

---

## Post-Incident Actions

1. **Root cause analysis.** Write an ADR in [docs/decisions/](../decisions/README.md) describing what failed and what was changed to prevent recurrence.
2. **Update threat model.** Add the incident pattern to [THREAT-MODEL.md](../security/THREAT-MODEL.md) if it was not previously captured.
3. **Update security issues tracker.** Add or resolve entries in [docs/issues/SECURITY.md](../issues/SECURITY.md).
4. **Strengthen controls.** Consider:
   - Adding field-level encryption for sensitive PHI (SSN, DOB)
   - Enabling mandatory 2FA for admin roles
   - Reducing permission scope for affected role
   - Adding automated alerts on suspicious audit patterns
5. **Credential rotation** (if credentials involved). Full rotation process: new PFX cert, new SQL password, new ABP NuGet key, new license check, revoke all OpenIddict refresh tokens.

---

## Related Documents

- [Threat Model](../security/THREAT-MODEL.md) -- threat categories and existing mitigations
- [PHI Data Flows](../security/DATA-FLOWS.md) -- where PHI can be found
- [Authorization Matrix](../security/AUTHORIZATION.md) -- which permissions protect which endpoints
- [HIPAA Compliance](../security/HIPAA-COMPLIANCE.md) -- technical safeguard inventory
- [Security Issues](../issues/SECURITY.md) -- known vulnerabilities
- [Secrets Management](../security/SECRETS-MANAGEMENT.md) -- secret rotation process
