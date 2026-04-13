[Home](../INDEX.md) > Security > HIPAA Compliance

# HIPAA Technical Safeguards Inventory

> For known security vulnerabilities and remediation status, see [Security Issues](../issues/SECURITY.md).

This is a **technical inventory**, not a legal compliance document. It catalogues the safeguards the application currently provides under each HIPAA Security Rule technical safeguard category, and the gaps that remain. Consult HIPAA compliance counsel for legal obligations.

**Last verified:** 2026-04-13
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
- **Multi-tenant data isolation** -- `IMultiTenant` filter prevents cross-tenant reads (Patient exception noted below).

---

## Priority Gaps for HIPAA Readiness

Ordered by criticality for cloud production deployment:

1. **Patient entity lacks `IMultiTenant`.** Any code path querying Patient without manual tenant filter is a cross-tenant PHI disclosure risk. See [DATA-FLOWS.md](DATA-FLOWS.md#cross-tenant-phi-risk-critical).
2. **SEC-02 PII logging enabled by default.** Full user claims including email, names, and tokens written to log files.
3. **No encryption at rest.** TDE disabled; no field-level encryption for SSN/DOB.
4. **No audit log retention policy.** Required for HIPAA incident investigation.
5. **2FA not mandatory.** Should be required for any role with broad PHI access.
6. **No BAA with cloud provider.** Application not deployed yet; but any cloud deploy requires BAA with the host.
7. **No break-glass procedure.** Emergency PHI access during incidents has no documented process.

---

## Related Documents

- [Threat Model](THREAT-MODEL.md)
- [PHI Data Flows](DATA-FLOWS.md)
- [Authorization Matrix](AUTHORIZATION.md)
- [Secrets Management](SECRETS-MANAGEMENT.md)
- [Security Issues](../issues/SECURITY.md)
- [Project HIPAA Rules](../../.claude/rules/hipaa-data.md)
