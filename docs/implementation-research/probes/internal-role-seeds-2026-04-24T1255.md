# Probe log: internal-role-seeds

**Timestamp (local):** 2026-04-24T12:55
**Purpose:** Confirm exactly 5 roles are seeded (admin + 4 external shells) and that external roles have zero permission grants, providing the evidentiary base for DB-16, 5-G01..5-G04.

## Command 1: password grant

\```
curl -sk -X POST https://localhost:44368/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access"
\```

### Response 1

Status: 200

Body (redacted):

\```
{
  "access_token": "<REDACTED>",
  "token_type": "Bearer",
  "expires_in": 3599,
  "id_token": "<REDACTED>",
  "refresh_token": "<REDACTED>"
}
\```

### Interpretation 1

Host admin can obtain an access token. Same pattern used in `service-status.md`. Confirms AuthServer is reachable.

## Command 2: roles list

\```
curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/identity/roles"
\```

### Response 2

Status: 200

Body (redacted):

\```
{
  "totalCount": 5,
  "items": [
    { "id": "<GUID>", "name": "admin", "isDefault": true, "isStatic": true, "isPublic": true },
    { "id": "<GUID>", "name": "Patient", "isDefault": false, "isStatic": false, "isPublic": true },
    { "id": "<GUID>", "name": "Claim Examiner", "isDefault": false, "isStatic": false, "isPublic": true },
    { "id": "<GUID>", "name": "Applicant Attorney", "isDefault": false, "isStatic": false, "isPublic": true },
    { "id": "<GUID>", "name": "Defense Attorney", "isDefault": false, "isStatic": false, "isPublic": true }
  ]
}
\```

### Interpretation 2

Five roles seeded: one built-in ABP `admin` + four external roles from `ExternalUserRoleDataSeedContributor`. Zero internal-tier roles (no ItAdmin / StaffSupervisor / ClinicStaff). Confirms DB-16, 5-G02, 5-G03, 5-G04 as current gaps. Admin / tenant-switching context note: if Host admin token cannot see tenant-scoped external roles, re-probe with `__tenant: <tenantId>` header against a seeded tenant.

## Command 3: Patient role permission grants

\```
curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/permission-management/permissions?providerName=R&providerKey=Patient"
\```

### Response 3

Status: 200

Body (redacted):

\```
{
  "entityDisplayName": "Patient",
  "groups": [
    {
      "name": "CaseEvaluation",
      "displayName": "CaseEvaluation",
      "permissions": [
        { "name": "CaseEvaluation.Dashboard.Host", "isGranted": false, ... },
        { "name": "CaseEvaluation.Dashboard.Tenant", "isGranted": false, ... },
        { "name": "CaseEvaluation.Appointments", "isGranted": false, ... },
        ...
      ]
    }
  ]
}
\```

### Interpretation 3

Every permission returned with `isGranted: false`. Confirms `ExternalUserRoleDataSeedContributor` creates empty-shell roles (no grants) -- evidentiary base for 5-G01 and for Q22 being a genuinely open policy question (there are no pre-existing grants to preserve).

## Cleanup

None required. All probes are read-only. Token expires in 3599 s; no revocation needed.
