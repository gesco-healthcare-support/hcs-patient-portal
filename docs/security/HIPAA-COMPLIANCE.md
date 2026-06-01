[Home](../INDEX.md) > Security > HIPAA Compliance

# HIPAA Technical Safeguards Inventory

> Purpose: Technical inventory of HIPAA Security Rule safeguards and gaps in the Patient Portal. Audience: developer, security reviewer. Last verified: 2026-06-01 vs main.

> For known security vulnerabilities and remediation status, see [Security Issues](THREAT-MODEL.md).

This is a **technical inventory**, not a legal compliance document. It catalogues the safeguards the application currently provides under each HIPAA Security Rule technical safeguard category, and the gaps that remain. Consult HIPAA compliance counsel for legal obligations.

**Last verified:** 2026-06-01
**Method:** code-inspect + architecture review

---

## 45 CFR 164.312(a) -- Access Control

| Safeguard | Status | Evidence |
|---|---|---|
| Unique User Identification | Implemented | ABP Identity assigns GUID UserId per user |
| Emergency Access Procedure | Not documented | No break-glass account process defined |
| Automatic Logoff | Partial | JWT expiry enforces server-side; idle timeout not enforced client-side |
| Encryption and Decryption (at rest) | **Not implemented** | No TDE on SQL Server; no field-level PHI encryption |

**Gaps:**
- Field-level encryption for SSN, DOB not implemented.
- SQL Server TDE not enabled (dev); not verified for cloud deploy.
- No break-glass procedure for emergency PHI access.

---

## 45 CFR 164.312(b) -- Audit Controls

| Safeguard | Status | Evidence |
|---|---|---|
| System activity recording | Implemented | ABP audit logging captures entity Create/Update/Delete with user ID |
| PHI-field reveal audit | Implemented | `PatientsAppService.GetFullSsnAsync` (FEAT-09 / Design B, 2026-05-29) is the sole endpoint that returns the full SSN; every call is recorded by ABP's HTTP audit log with caller identity and patient ID (`GET api/app/patients/{id}/ssn`). Standard payloads return the last-4 masked value only. |
| Audit log review | Not documented | No scheduled review process |
| Audit log retention | Not configured | Default ABP audit log retention in DB; no explicit retention policy |
| Tamper evidence | Not implemented | Audit logs are regular DB rows; no hash chain or append-only store |

**Gaps:**
- No documented audit log retention or review process.
- No tamper-evident audit trail.

---

## 45 CFR 164.312(c) -- Integrity

| Safeguard | Status | Evidence |
|---|---|---|
| Mechanism to authenticate PHI | Partial | EF Core concurrency tokens (optional per entity; not universally applied) |
| PHI modification detection | Partial | Audit logs record changes but without cryptographic integrity |

**Gaps:**
- Not all PHI entities have concurrency tokens.
- No cryptographic hash to detect out-of-band DB modification.

---

## 45 CFR 164.312(d) -- Person or Entity Authentication

| Safeguard | Status | Evidence |
|---|---|---|
| User authentication | Implemented | OpenIddict OIDC flow with PKCE |
| Multi-factor authentication | Available, not enforced | ABP Identity module supports 2FA; not mandatory |
| Password complexity | Implemented | ABP Identity default: min length, upper/lower, digit requirements |
| Account lockout | Implemented | ABP Identity lockout after N failures |

**Gaps:**
- 2FA not required for admin or host users.
- Password complexity values not audited against HIPAA guidance.

---

## 45 CFR 164.312(e) -- Transmission Security

| Safeguard | Status | Evidence |
|---|---|---|
| Integrity controls in transit | Implemented | HTTPS/TLS between browser and services |
| Encryption in transit | Partial | HTTPS enforced in dev; production TLS policy undocumented |
| Intra-service TLS | Not configured | Dev Docker Compose uses plaintext between containers |

**Gaps:**
- HTTP Strict Transport Security (HSTS) headers: verify enforcement.
- Intra-cluster TLS (API to DB, API to Redis) not configured.

---

## Administrative and Organizational Safeguards (out of scope for this doc)

This document covers only the Security Rule *technical* safeguards. Administrative safeguards (workforce training, risk assessment, Business Associate Agreements) and physical safeguards (facility access, workstation security) are organizational controls outside the codebase.

---

## HIPAA-Relevant Features Already in the Codebase

- **PHI scanner PreToolUse hook** (`.claude/hooks/phi-scanner.sh`) prevents Claude from committing real PHI to the repo.
- **Synthetic-only test data rule** (`.claude/rules/hipaa-data.md`, `.claude/rules/test-data.md`) -- all test fixtures use synthetic data.
- **ABP permission system** -- per-entity CRUD permissions gate every endpoint.
- **Multi-tenant data isolation** -- `IMultiTenant` filter prevents cross-tenant reads. `Patient` implements `IMultiTenant` as of FEAT-09 (2026-05-05); no entity exception remains. Host/IT-Admin paths that must read across tenants use `IDataFilter<IMultiTenant>.Disable()` explicitly, matching the `DoctorsAppService` pattern.

---

## Priority Gaps for HIPAA Readiness

Ordered by criticality for cloud production deployment:

~~1. **Patient entity lacks `IMultiTenant`.**~~ **RESOLVED (FEAT-09, 2026-05-05).** `Patient` now implements `IMultiTenant`; ABP's automatic tenant filter applies with no exception. The former cross-tenant PHI disclosure risk via this path is closed.

1. **SEC-02 PII logging enabled by default.** Full user claims including email, names, and tokens written to log files.
2. **No encryption at rest.** TDE disabled; no field-level encryption for SSN/DOB. SSN is stored as plaintext `nvarchar`; the audited reveal endpoint (`GetFullSsnAsync`) controls egress but does not encrypt the stored value.
3. **No audit log retention policy.** Required for HIPAA incident investigation.
4. **2FA not mandatory.** Should be required for any role with broad PHI access.
5. **No BAA with cloud provider.** Application not deployed yet; but any cloud deploy requires BAA with the host.
6. **No break-glass procedure.** Emergency PHI access during incidents has no documented process.

---

## Related Documents

- [Threat Model](THREAT-MODEL.md)
- [PHI Data Flows](DATA-FLOWS.md)
- [Authorization Matrix](AUTHORIZATION.md)
- [Secrets Management](SECRETS-MANAGEMENT.md)
- [Security Issues](THREAT-MODEL.md)
- [Project HIPAA Rules](../../.claude/rules/hipaa-data.md)
